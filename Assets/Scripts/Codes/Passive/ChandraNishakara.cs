using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>
    /// 찬드라 — 특성 「니샤카라」. 월광은 궁극기와 특수행동이 쌓고, 위기 회복이 한 겹씩 쓴다.
    /// 치명상은 사망 판정 직전, 비치명상은 피해 처리가 끝난 직후 같은 규칙으로 처리한다.
    /// </summary>
    public sealed class ChandraNishakara : UniquePassiveCode
    {
        public const string ResourceId = "chandra_moonlight";
        private const int ResourceDisplayMaximum = 9;
        private const float TriggerHpRatio = 0.30f;
        private const float HealHpRatio = 0.50f;
        private const int GuardStatusId = 7060;
        private const string GuardStatusKey = "chandra_nishakara_guard";

        private bool _subscribed;

        public ChandraNishakara(PassiveCodeContext context) : base(context)
        {
            CodeName = "니샤카라";
            Combat.CombatResourceLabels.Register(ResourceId, "월광");
        }

        public override void CastCode()
        {
            if (Caster == null) return;
            Caster.SetCombatResourceMaximum(ResourceId, ResourceDisplayMaximum, resetCurrent: true);
            Caster.AddStatus(BuffStatus.Create(
                GuardStatusId, GuardStatusKey, "니샤카라", Caster, Caster,
                new LethalMoonlightGuard(this), duration: 0,
                category: BaseEnums.StatusCategory.Neutral,
                description: "월광이 있으면 체력이 30% 이하로 내려갈 때 1중첩을 소모해 최대 체력의 50%를 회복합니다."));
            ActionAnnouncer.AnnouncePassive(Caster, this);

            if (_subscribed) return;
            Caster.AddListener<EventContext>(BaseEnums.UnitEventType.OnAfterDamageTaken, OnAfterDamageTaken);
            _subscribed = true;
        }

        public override void StopCode()
        {
            if (_subscribed && Caster != null)
                Caster.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnAfterDamageTaken, OnAfterDamageTaken);
            _subscribed = false;
        }

        public int GainMoonlight(int amount = 1, bool playVfx = true)
        {
            if (Caster == null || amount <= 0) return 0;
            int before = Caster.GetCombatResource(ResourceId);
            int now = Caster.AddCombatResource(ResourceId, amount);
            if (now > before)
            {
                ActionAnnouncer.AnnouncePassive(Caster, this);
                if (playVfx) Effects.Vfx.ChandraFx.Moonlight(Caster, gained: true);
            }
            return now;
        }

        private void OnAfterDamageTaken(EventContext context)
        {
            if (Caster == null || !Caster.isActive || Caster.HpCurr <= 0) return;
            if (Caster.HpCurr > Mathf.FloorToInt(Caster.HpMax * TriggerHpRatio)) return;
            ConsumeAndHeal();
        }

        private bool ConsumeAndHeal()
        {
            if (Caster == null || !Caster.TryConsumeCombatResource(ResourceId, 1)) return false;
            ActionAnnouncer.AnnouncePassive(Caster, this);
            Managers.AudioManager.PlayAction(Caster, Managers.SfxActionSlot.Passive, Managers.SfxCuePhase.Cast, CodeName);
            Caster.ModifyHp(Mathf.Min(Caster.HpMax,
                Caster.HpCurr + Mathf.RoundToInt(Caster.HpMax * HealHpRatio)), Caster);
            Effects.Vfx.ChandraFx.Moonlight(Caster, gained: false);
            Managers.AudioManager.PlayAction(Caster, Managers.SfxActionSlot.Passive, Managers.SfxCuePhase.Hit, CodeName);
            return true;
        }

        private sealed class LethalMoonlightGuard : BaseEffect
        {
            private readonly ChandraNishakara _owner;

            public LethalMoonlightGuard(ChandraNishakara owner) : base(0) => _owner = owner;

            public override bool TryPreventDeath(Unit unit, Unit attacker)
            {
                if (_owner?.Caster != unit || !unit.TryConsumeCombatResource(ResourceId, 1)) return false;
                ActionAnnouncer.AnnouncePassive(unit, _owner);
                Managers.AudioManager.PlayAction(unit, Managers.SfxActionSlot.Passive, Managers.SfxCuePhase.Cast,
                    _owner.CodeName);
                unit.ModifyHp(Mathf.RoundToInt(unit.HpMax * HealHpRatio), unit);
                Effects.Vfx.ChandraFx.Moonlight(unit, gained: false);
                Managers.AudioManager.PlayAction(unit, Managers.SfxActionSlot.Passive, Managers.SfxCuePhase.Hit,
                    _owner.CodeName);
                return true;
            }
        }
    }
}
