using System;
using BaseClasses;
using Codes.Base;
using Combat;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>
    /// 아누비스 — 특성 「영혼 수확」(Detail_08 §9.1). 아누비스가 <b>살아서 필드에 있는 동안</b> 필드의 유닛이 쓰러지면
    /// 영혼(영구 중첩 <see cref="SoulKey"/>)을 거둔다. 일반 적 1 · 정예 2 · 보스 3 · 아군 1 · 소환수 0.25(진영 무관).
    /// 한 유닛은 해당하는 값 중 가장 큰 하나로 한 번만 센다.
    ///
    /// 영혼 1당 최대 체력 +8 · 방어 +0.4 · 특방 +0.4(합연산, 전투 밖에서도 보인다). 영혼과 레벨이 모두 스테이지에 비례해
    /// 자라므로 기초 대비 비율이 무한 모드에서 거의 일정하다 — 깡 수치인데도 후반에 죽지 않는 이유다.
    /// 이정표(누적): 50 피격 마나 2배 · 150 사자의 서 지속 4턴 · 300 전투 시작 방어력 270% 보호막.
    /// </summary>
    public sealed class AnubisSoulHarvest : UniquePassiveCode
    {
        public const string SoulKey = "anubis.soul";
        public const string ResourceId = "anubis_soul";

        public const int MilestoneHitMana = 50;
        public const int MilestoneLongerBook = 150;
        public const int MilestoneOpeningShield = 300;

        private const float HpPerSoul = 8f;
        private const float DefensePerSoul = 0.4f;
        private const float SummonSoul = 0.25f;
        private const float OpeningShieldRatio = 2.7f;

        private bool _subscribed;

        public AnubisSoulHarvest(PassiveCodeContext context) : base(context)
        {
            CodeName = "영혼 수확";
            CombatResourceLabels.Register(ResourceId, "영혼", unit =>
                $"영혼 {unit.GetPermanentStacks(SoulKey) + unit.GetPermanentStackFraction(SoulKey):0.0}");
        }

        public override string PermanentStackKey => SoulKey;

        public int Souls => Caster != null ? Caster.GetPermanentStacks(SoulKey) : 0;

        public override float PermanentStatFlatModifier(BaseEnums.StatType stat) => stat switch
        {
            BaseEnums.StatType.MaxHp => Souls * HpPerSoul,
            BaseEnums.StatType.Defense => Souls * DefensePerSoul,
            BaseEnums.StatType.SpecialDefense => Souls * DefensePerSoul,
            _ => 0f,
        };

        public override void CastCode()
        {
            if (Caster == null) return;
            // 카드 배지는 정수 영혼 수를 늘 보여 준다. 전투 자원 칸은 라운드마다 비워지므로 영구 값을 다시 비춘다.
            Caster.SetCombatResourceMaximum(ResourceId, int.MaxValue, resetCurrent: true);
            Caster.AddCombatResource(ResourceId, Souls);
            if (Souls >= MilestoneOpeningShield)
                Caster.AddShield(Mathf.RoundToInt(Caster.StatValue(BaseEnums.StatType.Defense) * OpeningShieldRatio), Caster);

            if (_subscribed) return;
            Unit.AnyUnitDied += OnAnyUnitDied;
            Unit.AnyPermanentStackGained += OnPermanentStackGained;
            Caster.AddListener<EventContext>(BaseEnums.UnitEventType.OnAfterDamageTaken, OnAfterDamageTaken);
            _subscribed = true;
        }

        public override void StopCode()
        {
            if (!_subscribed) return;
            Unit.AnyUnitDied -= OnAnyUnitDied;
            Unit.AnyPermanentStackGained -= OnPermanentStackGained;
            Caster?.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnAfterDamageTaken, OnAfterDamageTaken);
            _subscribed = false;
        }

        /// <summary>쓰러진 유닛 하나의 영혼 값. 해당하는 값 중 가장 큰 하나다.</summary>
        public static float SoulValueOf(Unit dead, Unit anubis)
        {
            if (dead == null || anubis == null || dead == anubis) return 0f;
            float value = 0f;
            if (dead.IsSummon) value = Math.Max(value, SummonSoul);
            else if (dead.IsEnemy != anubis.IsEnemy)
            {
                value = Math.Max(value, string.Equals(dead.UnitTier, "boss", StringComparison.OrdinalIgnoreCase) ? 3f
                    : string.Equals(dead.UnitTier, "elite", StringComparison.OrdinalIgnoreCase) ? 2f : 1f);
            }
            else value = Math.Max(value, 1f);
            return value;
        }

        private void OnAnyUnitDied(Unit dead, Unit attacker)
        {
            if (Caster == null || !Caster.isActive || !Caster.IsOnField) return;
            float value = SoulValueOf(dead, Caster);
            if (value > 0f)
            {
                int before = Souls;
                Caster.GainPermanentStacks(SoulKey, value);
                Effects.Vfx.AnubisFx.Soul(Caster, Souls > before);
            }
        }

        private void OnPermanentStackGained(Unit unit, string key, int gained)
        {
            if (unit != Caster || key != SoulKey || gained <= 0) return;
            Caster.AddCombatResource(ResourceId, gained);
        }

        /// <summary>영혼 50 — 피격 마나 2배. 공용 피격 마나와 같은 조건의 피격에만 한 번 더 준다.</summary>
        private void OnAfterDamageTaken(EventContext context)
        {
            DamageContext damage = context?.DmgCtx;
            if (Caster == null || damage == null || damage.ResolvedDamage <= 0 || Souls < MilestoneHitMana) return;
            if (damage.CodeType == BaseEnums.CodeType.Effect || damage.HasTag(DamageTag.BonusDamage)) return;
            Caster.RecoverMana(Unit.ManaPerHitTaken);
        }
    }
}
