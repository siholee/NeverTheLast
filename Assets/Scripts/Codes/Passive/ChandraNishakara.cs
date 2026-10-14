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
    ///
    /// <b>달빛 순환</b>(Detail_08 §3.7): 필드에 있는 동안 아군이 <b>특수행동</b>을 마칠 때마다 그 아군이
    /// 찬드라 최대 체력 4%를 회복한다. 같은 아군은 자기 턴 사이에 한 번. 추가행동은 다른 개념이라 세지 않는다.
    /// 로카팔라에 묶지 않았으므로 시의 처치 재행동 같은 다른 특수행동 파티에서도 일한다.
    /// </summary>
    public sealed class ChandraNishakara : UniquePassiveCode
    {
        public const string ResourceId = "chandra_moonlight";
        private const int ResourceDisplayMaximum = 9;
        private const float TriggerHpRatio = 0.30f;
        private const float HealHpRatio = 0.50f;
        private const int GuardStatusId = 7060;
        private const string GuardStatusKey = "chandra_nishakara_guard";
        private const float MoonCycleHealRatio = 0.04f;

        private bool _subscribed;
        // 아군별로 마지막으로 달빛 순환을 받은 그 아군의 턴 번호. 자기 턴이 지나야 다시 받는다.
        private readonly System.Collections.Generic.Dictionary<Unit, int> _moonCycleTurn = new();

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

            _moonCycleTurn.Clear();
            if (_subscribed) return;
            Caster.AddListener<EventContext>(BaseEnums.UnitEventType.OnAfterDamageTaken, OnAfterDamageTaken);
            Managers.ActionScheduler.AnyActionFinished += OnAnyActionFinished;
            _subscribed = true;
        }

        public override void StopCode()
        {
            if (_subscribed && Caster != null)
                Caster.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnAfterDamageTaken, OnAfterDamageTaken);
            if (_subscribed) Managers.ActionScheduler.AnyActionFinished -= OnAnyActionFinished;
            _subscribed = false;
            _moonCycleTurn.Clear();
        }

        private void OnAnyActionFinished(Unit unit, Managers.ActionScheduler.ActionKind kind)
        {
            if (kind != Managers.ActionScheduler.ActionKind.Special) return;
            if (Caster == null || !Caster.isActive || !Caster.IsOnField) return;
            if (unit == null || !unit.isActive || unit.IsSummon || unit.IsEnemy != Caster.IsEnemy) return;
            if (_moonCycleTurn.TryGetValue(unit, out int turn) && turn == unit.TurnCount) return;

            _moonCycleTurn[unit] = unit.TurnCount;
            Effects.Vfx.VfxComposer.PlayScreen("chandra.moon_cycle", Caster, unit);
            using (Effects.Vfx.StatusAffectedVfx.SupportVariant("chandra.moon_cycle"))
                unit.Heal(Mathf.Max(1, Mathf.RoundToInt(Caster.HpMax * MoonCycleHealRatio)), Caster);
            Managers.AudioManager.PlayCue("chandra.moon_cycle.resolve");
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
