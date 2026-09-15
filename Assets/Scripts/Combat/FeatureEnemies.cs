using System;
using System.Collections.Generic;
using System.Linq;
using Entities;
using Managers;

namespace Combat
{
    /// <summary>
    /// 엘리트·보스를 어떻게 보여 줄지 가리는 두 판정.
    ///
    /// <b>둘은 조건이 다르다.</b> 상단 체력 띠는 엘리트가 하나라도 있으면 뜨고,
    /// 카드 확대는 판에 그 하나둘만 서 있을 때만 켜진다. 띠는 화면 위쪽의 빈 자리를 쓰지만
    /// 커진 카드는 옆 칸을 침범하므로, 잡졸이 함께 선 판에서 켜면 서로 가린다.
    /// </summary>
    public static class FeatureEnemies
    {
        /// <summary>상단 띠가 한 번에 보여 줄 수 있는 수.</summary>
        public const int MaxCount = 2;

        /// <summary>
        /// 상단 체력 띠에 올릴 적. <b>엘리트가 하나라도 있으면</b> 그것들을 올린다.
        /// 셋 이상이면 보스를 먼저, 그다음 최대 체력이 큰 순으로 둘만 고른다.
        /// </summary>
        public static List<Unit> BannerTargets()
            => AliveEnemies()
                .Where(IsFeatureTier)
                .OrderByDescending(IsBoss)
                .ThenByDescending(unit => unit.HpMax)
                .Take(MaxCount)
                .ToList();

        /// <summary>
        /// 카드를 키울 적. 살아 있는 적이 1~2기이고 <b>전원이</b> 엘리트나 보스여야 한다.
        /// 잡졸이 하나라도 끼면 보통 크기다 — 호위를 달고 나오는 중간 보스는 여기 들지 않는다.
        /// </summary>
        public static List<Unit> SoloStageTargets()
        {
            List<Unit> enemies = AliveEnemies();
            if (enemies.Count == 0 || enemies.Count > MaxCount) return new List<Unit>();
            return enemies.All(IsFeatureTier) ? enemies : new List<Unit>();
        }

        private static List<Unit> AliveEnemies()
            => GridManager.Instance?.enemyList?
                .Where(unit => unit != null && unit.isActive && unit.IsOnField)
                .ToList() ?? new List<Unit>();

        private static bool IsFeatureTier(Unit unit) => IsBoss(unit) || IsTier(unit, "elite");

        private static bool IsBoss(Unit unit) => IsTier(unit, "boss");

        private static bool IsTier(Unit unit, string tier)
            => string.Equals(unit.UnitTier, tier, StringComparison.OrdinalIgnoreCase);
    }
}
