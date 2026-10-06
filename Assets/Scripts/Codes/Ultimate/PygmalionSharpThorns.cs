using System.Collections;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Effects.Negative;
using Effects.Vfx;
using Entities;
using Entities.Status;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>피그말리온 — 집중 3턴 「날카로운 가시」.</summary>
    public sealed class PygmalionSharpThorns : BaseUltimateCode
    {
        private const int DurationTurns = 3;
        private const int StatusId = 70202;
        private const string ConcentrationKey = "pygmalion_sharp_thorns_focus";
        private const string StatusKey = "pygmalion_sharp_thorns";

        public PygmalionSharpThorns(UltimateCodeContext context) : base(context) => CodeName = "날카로운 가시";

        protected override IEnumerator Perform()
        {
            if (Caster == null) yield break;
            VfxComposer.PlayScreen("pygmalion.sharp_thorns", Caster, Caster);
            VfxComposer.PlaySlot("pygmalion.sharp_thorns", "cast", 0, Caster, Caster,
                VfxSpace.WorldPoint(Caster), VfxSpace.SortingLayer(Caster));
            Concentration.Begin(Caster, ConcentrationKey, DurationTurns,
                _ => Caster?.RemoveStatusByKey(StatusKey));
            UnitStatus status = BuffStatus.Create(
                StatusId, StatusKey, "날카로운 가시", Caster, Caster,
                new SharpThornsEffect(), duration: 0,
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                isBeneficial: true,
                description: "타겟 우선도 +1, 받는 피해 -50%. 접촉 공격에는 방어력을 2배로 계산하고 공격자에게 맹독을 부여합니다.");
            status.AffectedVfxPartId = StatusAffectedVfx.SuppressPartId;
            Caster.AddStatus(status);

            Managers.AudioManager.PlayAction(Caster, Managers.SfxActionSlot.Ultimate,
                Managers.SfxCuePhase.Screen, CodeName);
            Caster.StartCoroutine(CardMotion.Channel(Caster, 0.13f, 0.52f, 0.22f));
            // 시전·화면·정점 소리가 같은 프레임에 뭉치지 않게 가시 문양의 전개 정점에 맞춘다.
            yield return new WaitForSeconds(0.15f);
            VfxComposer.PlayAffectedSlot("pygmalion.sharp_thorns", 0, Caster, Caster);
            Managers.AudioManager.PlayAction(Caster, Managers.SfxActionSlot.Ultimate,
                Managers.SfxCuePhase.Hit, CodeName);
            yield return new WaitForSeconds(0.23f);
        }

        private sealed class SharpThornsEffect : BaseEffect
        {
            private System.Action<EventContext> _handler;

            public SharpThornsEffect() : base(0) { }
            public override bool IsBeneficial => true;

            public override void OnApply()
            {
                if (Target == null) return;
                _handler = OnAfterDamageTaken;
                Target.AddListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _handler);
            }

            public override float ReceivingDamageModifier(Unit unit, DamageContext context)
                => unit == Target ? 0.50f : 1f;

            public override float OwnedDefenseStatMultiplierModifier(Unit unit, DamageContext context)
                => unit == Target && context != null && context.HasTag(DamageTag.ContactAttack) ? 2f : 1f;

            public override int TargetPriorityAdditiveModifier(Unit unit) => unit == Target ? 1 : 0;

            private void OnAfterDamageTaken(EventContext context)
            {
                DamageContext damage = context?.DmgCtx;
                Unit attacker = context?.Grantor;
                if (Target == null || attacker == null || !attacker.isActive || damage == null) return;
                if (damage.ResolvedDamage <= 0 || !damage.HasTag(DamageTag.ContactAttack)) return;

                var poison = new UnitStatus(1, Target, attacker);
                poison.AddEffect(new DamageOverTimeEffect(1001, 20f));
                if (attacker.TryAddStatus(poison)) EmberFx.Blaze(attacker, detonate: false);
            }

            public override void OnRemove()
            {
                if (Target != null && _handler != null)
                    Target.RemoveListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _handler);
                _handler = null;
            }
        }
    }
}
