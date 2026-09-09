using System.Linq;
using BaseClasses;
using Effects.Base;
using Effects.Buffs;
using Entities;

namespace Effects.Neutral
{
    /// <summary>
    /// 오사(誤射) 방지 — <b>아군이 아군에게 만든 해로운 원소 반응</b>을 통째로 막는다.
    ///
    /// 버프 반응은 자기 진영에 원소를 둘러 만드는 것이 정규 운용인데, 그 과정에서
    /// 감전·과부하·초전도처럼 해로운 쌍이 걸리면 <b>자동 전투라 되돌릴 방법이 없다.</b>
    /// 잔의 <c>오사방지</c>가 그 사고만 골라 지운다. 적이 아군에게 건 반응은 막지 않는다 —
    /// 그건 상대의 정당한 수단이다.
    /// </summary>
    public static class FriendlyReactionGuard
    {
        public const int StatusId = 5941;
        public const string StatusKey = "friendly_reaction_guard";

        /// <summary>진영에 오사방지를 든 유닛이 서 있는가.</summary>
        public static bool Protects(Unit unit)
        {
            if (unit == null || Managers.GridManager.Instance == null) return false;

            var roster = unit.IsEnemy
                ? Managers.GridManager.Instance.enemyList
                : Managers.GridManager.Instance.heroList;
            return roster != null && roster.Any(ally =>
                ally != null && ally.isActive && ally.ActiveStatuses != null &&
                ally.ActiveStatuses.Any(status => status.Effects
                    .Any(effect => effect.EffectObject is FriendlyReactionGuardEffect)));
        }

        public static void Apply(Unit owner)
        {
            if (owner == null) return;

            owner.AddStatus(BuffStatus.Create(
                StatusId, StatusKey, "오사방지", owner, owner,
                new FriendlyReactionGuardEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                category: BaseEnums.StatusCategory.Neutral,
                isBeneficial: true,
                description: "아군이 아군에게 만드는 해로운 원소 반응이 일어나지 않습니다."));
        }
    }

    /// <summary>표식 전용. 판정은 <see cref="FriendlyReactionGuard"/>가 한다.</summary>
    public sealed class FriendlyReactionGuardEffect : BaseEffect
    {
        public FriendlyReactionGuardEffect() : base(0) { }
    }
}
