using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Entities;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>범용 거인 N — 단일 대상에게 STR 기반 접촉·물리 피해.</summary>
    public sealed class GenericGiantNormalAttack : BaseNormalCode
    {
        public GenericGiantNormalAttack(NormalCodeContext context, int power) : base(context)
        {
            CodeName = "일반행동";
            Power = power;
            CodeTags = new List<int> { DamageTag.ContactAttack, DamageTag.Physical };
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.ContactAttack, DamageTag.Physical,
        };
    }

    /// <summary>
    /// 서리 짓밟기 — 적 <b>전열 전체</b>를 밟고 얼음을 부착한다.
    ///
    /// 보스 등급 거인의 일반행동이다. DEX가 10이라 좀처럼 오지 않는 대신 한 번에 전열을 쓴다.
    /// 얼음을 매번 얹으므로 같은 아군에게 두 번 쌓이면 둔화가 터진다 — 이 테마가 내내 가르친
    /// 그 반응이 보스에게서도 같은 방식으로 나온다.
    /// </summary>
    public sealed class FrostGiantStomp : BaseNormalCode
    {
        public FrostGiantStomp(NormalCodeContext context) : base(context)
        {
            CodeName = "서리 짓밟기";
            Power = 100;
            PowerStatCoefficient = 0.8f;
            PowerStat = BaseEnums.PrimaryStat.STR;
            CastingDelay = 0.4f;
            CodeTags = new List<int> { DamageTag.ContactAttack, DamageTag.Physical };
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.AllTarget, DamageTag.NormalAttack,
            DamageTag.ContactAttack, DamageTag.Physical,
        };

        protected override List<Unit> SelectTarget()
        {
            List<Unit> enemies = GetAvailableEnemies();
            if (enemies.Count == 0) return new List<Unit>();

            // 전열이 비었으면 후열을 밟는다. 밟을 것이 없으면 행동이 통째로 사라진다.
            List<Unit> front = enemies
                .Where(unit => unit.currentCell != null && Mathf.Abs(unit.currentCell.xPos) == 1)
                .ToList();
            return front.Count > 0 ? front : enemies;
        }

        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            if (target == null || !target.isActive) return;
            target.GrantCombatElement(BaseEnums.UnitElement.Cryo, Unit.CommonElementAuraDuration, Caster);
        }
    }
}
