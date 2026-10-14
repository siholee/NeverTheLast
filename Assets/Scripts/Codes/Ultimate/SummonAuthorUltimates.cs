using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Effects.Vfx;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Ultimate
{
    public sealed class ThothScribe : BaseUltimateCode
    {
        public ThothScribe(UltimateCodeContext context) : base(context) => CodeName = "사유의 서기관";
        protected override bool RequiresCompletionEvidence => true;
        public override bool HasValidTarget() => Caster != null && Caster.isActive && Caster.IsOnField;
        protected override IEnumerator Perform()
        {
            yield return SummonAuthorsFx.Prayer(Caster, false, true);
            if (Caster == null || !Caster.isActive || Caster.isControlled) yield break;
            foreach (var code in Caster.ActivePassiveCodes)
                if (code is ThothLivingRecords records && records.SummonScribe() != null) MarkEffectApplied();
            AudioManager.PlayAction(Caster, SfxActionSlot.Ultimate, SfxCuePhase.Hit, CodeName);
        }
    }
    public sealed class ShakespeareQuestion : BaseUltimateCode
    {
        public const string FieldKey = "shakespeare_summon_field";
        public ShakespeareQuestion(UltimateCodeContext context) : base(context) => CodeName = "사느냐, 죽느냐, 그것이 문제로다";
        protected override bool RequiresCompletionEvidence => true;
        public override bool HasValidTarget() => Caster != null && Caster.isActive && Caster.IsOnField;
        protected override IEnumerator Perform()
        {
            yield return SummonAuthorsFx.Prayer(Caster, true, true);
            if (Caster == null || !Caster.isActive || Caster.isControlled) yield break;
            var status = BuffStatus.Create(73210, FieldKey, "사느냐, 죽느냐", Caster, Caster,
                new TheatreField(), duration: 3, stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                isBeneficial: true, description: "자신의 일반행동3회 동안 소환 피해 증가(특공 비례, 최대50%). 실제 소환 개체마다 전투 중1회 치명상을 막고 체력30%로 생존. 수명 소멸 제외.");
            status.NormalActionDuration = true;
            Caster.AddStatus(status);
            AudioManager.PlayAction(Caster, SfxActionSlot.Ultimate, SfxCuePhase.Hit, CodeName);
            MarkEffectApplied();
        }
        private sealed class TheatreField : BaseEffect
        {
            private readonly List<Unit> _holders = new();
            private bool _active;
            private string Key => $"shakespeare_theatre_{Caster?.GetEntityId()}";
            public TheatreField() : base(0) { }
            public override bool IsBeneficial => true;
            public bool Working => _active && Caster != null && Caster.isActive && Caster.IsOnField;
            public float Bonus => Working ? Mathf.Min(.50f, .20f + .10f * SummonSupportCombat.PowerRatio(Caster)) : 0;
            public override void OnApply()
            {
                _active = true;
                BindAllies();
                Summons.AnySummonCreated += Created;
                ActionScheduler.AnyActionStarted += Started;
            }
            private void Created(Unit owner, Unit summon, SummonSource source, object tag)
            { if (Working && owner != null && owner.IsEnemy == Caster.IsEnemy) BindAllies(); }
            private void Started(Unit actor, ActionScheduler.ActionKind kind, string label) { if (Working) BindAllies(); }
            private void BindAllies()
            {
                foreach (Unit unit in SummonSupportCombat.Allies(Caster, true))
                {
                    if (unit.HasStatusKey(Key)) continue;
                    unit.AddStatus(BuffStatus.Create(73211, Key, "무대의 축복", Caster, unit,
                        new TheatreBlessing(this), 0, isBeneficial: true,
                        description: "소환 피해 증가. 실제 소환수는 전투 중1회 치명상 방지(체력30%)."));
                    _holders.Add(unit);
                }
            }
            public bool Save(Unit unit)
            {
                if (!Working || unit == null || !unit.IsSummon || unit.IsEnemy != Caster.IsEnemy) return false;
                foreach (var code in Caster.ActivePassiveCodes)
                    if (code is ShakespeareLanguage language && language.TrySave(unit)) return true;
                return false;
            }
            public override void OnRemove()
            {
                _active = false;
                Summons.AnySummonCreated -= Created;
                ActionScheduler.AnyActionStarted -= Started;
                foreach (Unit unit in _holders) unit?.RemoveStatusByKey(Key);
                _holders.Clear();
            }
        }
        private sealed class TheatreBlessing : BaseEffect
        {
            private readonly TheatreField _source;
            public TheatreBlessing(TheatreField source) : base(0) => _source = source;
            public override bool IsBeneficial => true;
            public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
                => attacker == Target && SummonSupportCombat.IsSummonDamage(context) ? 1 + _source.Bonus : 1;
            public override bool TryPreventDeath(Unit unit, Unit attacker)
            {
                if (unit != Target || !_source.Save(unit)) return false;
                // 치유가 아니라 치명상 방지다. 과잉 피해·치유 배율·온기 저축에 영향을 받지 않는다.
                if (!unit.RestoreHpAfterPreventedDeath(.30f)) return false;
                SummonAuthorsFx.Restore(unit);
                return true;
            }
        }
    }
}
