using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using Effects.Buffs;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 우사 U(180) — 거느린 분신에게 치명타 피해 +40%(3턴)를 두르고 즉시 한 번 더 움직이게 한다.
    /// 분신이 하나도 없으면 즉시 행동 대신 분신 2기를 부른다 — 빈손으로 터뜨려도 손해만 보지 않는다.
    /// </summary>
    public sealed class UsaCloneRally : SimpleUltimate
    {
        private const int StatusId = 6481;
        private const float CritDamageBonus = 0.40f;
        private const int BuffDuration = 3;
        private const int EmptyHandCloneCount = 2;

        public UsaCloneRally(UltimateCodeContext context) : base(context, "분신술 — 만천화우", 0.5f) { }

        protected override void Resolve()
        {
            if (Caster == null || !Caster.isActive) return;

            var clones = new List<Unit>();
            foreach (Unit summon in Caster.ActiveSummons)
            {
                if (summon != null && summon.isActive) clones.Add(summon);
            }

            if (clones.Count == 0)
            {
                UsaTaoistNature.SummonClones(Caster, EmptyHandCloneCount);
                return;
            }

            foreach (Unit clone in clones)
            {
                clone.AddStatus(BuffStatus.Create(
                    StatusId, $"usa_clone_rally_{Caster.GetEntityId()}", CodeName,
                    Caster, clone, new CritMultiplierBonusEffect(CritDamageBonus),
                    duration: BuffDuration,
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    isBeneficial: true,
                    description: $"{BuffDuration}턴 동안 치명타 피해 +{Mathf.RoundToInt(CritDamageBonus * 100)}%."));

                // 소환수 본인을 주체로 예약한다 — 분신마다 한 번씩만 움직인다.
                Unit acting = clone;
                GameManager.Instance?.ActionScheduler?.EnqueueAdditional(
                    acting, $"usa_clone_rally_{acting.GetEntityId()}", CodeName,
                    () => ActImmediately(acting));
            }
        }

        private static void ActImmediately(Unit clone)
        {
            if (clone == null || !clone.isActive) return;
            clone.CastNormalCode();
        }
    }

    /// <summary>분신 U(502) — 단일 적에게 240 + INT×0.7. 소환수 공격이다.</summary>
    public sealed class CloneBurst : SimpleUltimate
    {
        private const int BasePower = 240;
        private const float IntCoefficient = 0.7f;

        public CloneBurst(UltimateCodeContext context) : base(context, "분신 일격", 0.3f) { }

        protected override void Resolve()
        {
            if (Caster == null || !Caster.isActive) return;

            Unit target = CombatTargets.PickByPriority(Enemies());
            if (target == null) return;

            int power = BasePower + Mathf.RoundToInt(Caster.GetBaseInt() * IntCoefficient);
            Summons.Deal(Caster.SummonOwner ?? Caster, target, power, BaseEnums.PrimaryStat.INT,
                DamageTag.Special, DamageTag.ContactAttack);
        }
    }
}
