using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Managers;
using UnityEngine;

namespace Codes.Special
{
    /// <summary>
    /// 쿠베라 — 특수행동 「골드 러쉬」(Detail_08 §11.3). 로카팔라 묶음 참여자(순서 0, 여는 것은 인드라뿐).
    /// 적 전체에 공격력 100% × <b>금력 배율</b>. 이번 전투 골드 +5%(전투 안에서 중첩, 최대 +25%). 그 밖의 효과는 없다.
    ///
    /// 금력 = 보유 골드 ÷ (현재 스테이지 × 100), 배율 = 1 + 금력 × 15%(상한 ×4). 수입 · 상점가가 모두 스테이지에 비례하므로
    /// 스테이지로 나눠야 무한 모드 어디서든 "몇 전투치를 모았는가"가 같은 배율을 낸다.
    /// </summary>
    public sealed class KuberaGoldRush : BaseSpecialCode
    {
        private const float AttackRatio = 1.00f;
        private const float GoldPerStageUnit = 100f;
        private const float BonusPerUnit = 0.15f;
        private const float MaxMultiplier = 4f;
        private const float BattleGoldBonusPerUse = 0.05f;
        private const float BattleGoldBonusCap = 0.25f;

        private float _battleGoldBonusGiven;

        public override int AttackForm => DamageTag.Shot;
        public override int OpenOrder => 0;

        public KuberaGoldRush(SpecialCodeContext context) : base(context) => CodeName = "골드 러쉬";

        /// <summary>쿠베라 특성이 라운드 시작에 부른다 — "이번 전투" 중첩을 0부터 센다.</summary>
        public void ResetBattle() => _battleGoldBonusGiven = 0f;

        /// <summary>지금 금력 배율. TAB 캐릭터 창의 코드 탭이 보여 준다.</summary>
        public static float GoldMultiplier() => GoldMultiplier(out _, out _);

        /// <summary>금력 배율과 그 계산에 실제로 쓴 보유 골드 · 스테이지(0이면 1로 본다). UI가 식을 다시 쓰지 않게 함께 돌려준다.</summary>
        public static float GoldMultiplier(out int gold, out int stage)
        {
            gold = GameManager.Instance?.inventoryManager?.Gold ?? 0;
            stage = Mathf.Max(1, GameManager.Instance?.RoundManager?.Stage ?? 1);
            float power = gold / (stage * GoldPerStageUnit);
            return Mathf.Min(MaxMultiplier, 1f + Mathf.Max(0f, power) * BonusPerUnit);
        }

        protected override IEnumerator Perform()
        {
            float multiplier = GoldMultiplier();
            yield return HitAllEnemies(_ => Mathf.RoundToInt(StatDamage(0f, BaseEnums.StatType.Attack, AttackRatio) * multiplier),
                new List<int> { DamageTag.AllTarget, DamageTag.SpecialAttack, DamageTag.NonContactAttack, DamageTag.Shot },
                presentation: (targets, impact) => Effects.Vfx.LokapalaFx.Area("kubera.gold_rush", Caster, targets,
                    impact));

            float give = Mathf.Min(BattleGoldBonusPerUse, BattleGoldBonusCap - _battleGoldBonusGiven);
            if (give > 0f)
            {
                _battleGoldBonusGiven += give;
                GameManager.Instance?.AddBattleGoldBonus(give);
            }
            Debug.Log($"[골드 러쉬] 금력 배율 ×{multiplier:0.00}, 이번 전투 골드 보너스 누적 +{_battleGoldBonusGiven:P0}");
        }
    }
}
