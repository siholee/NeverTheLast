using BaseClasses;
using Codes.Base;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>공용 해금 패시브 182 막기 — 2턴마다 단계별 확률로 CON 비례 방어막을 얻는다.</summary>
    public class Block : PeriodicTurnPassive
    {
        private const int IntervalTurnCount = 2;

        public Block(PassiveCodeContext context) : base(context, IntervalTurnCount)
        {
            CodeName = "막기";
            MaxStage = 3;
            CodeTags = new System.Collections.Generic.List<int> { DamageTag.Physical };
        }

        protected override void OnPeriodElapsed()
        {
            if (UnityEngine.Random.value > GetStageChance()) return;

            int shieldAmount = Caster.SkillDamage(40, BaseEnums.PrimaryStat.CON) + Mathf.RoundToInt(GetStageShieldBonus());
            Caster.AddShield(shieldAmount, Caster);
            Debug.Log($"[막기] {Caster.UnitName}이 {shieldAmount} 방어막 획득");
        }

        private float GetStageChance()
        {
            return Mathf.Clamp(CurrentStage, 1, MaxStage) switch
            {
                1 => 0.05f,
                2 => 0.15f,
                _ => 0.25f,
            };
        }

        private int GetStageShieldBonus()
        {
            return Mathf.Clamp(CurrentStage, 1, MaxStage) switch
            {
                1 => 100,
                2 => 200,
                _ => 300,
            };
        }
    }
}
