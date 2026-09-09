using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Managers;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 특수행동의 문 — <b>인드라의 궁극기 하나만이</b> 여는 곳.
    ///
    /// 붕괴: 스타레일의 환락(아하)과 같은 모양이되 결정적으로 다른 점이 있다.
    /// 환락은 여러 경로로 자주 열리지만, 여기서는 <b>인드라의 '신들의 왕' 외에는
    /// 어떤 수단으로도 열리지 않는다.</b> 추가행동이 아무리 잦아도 이 문은 안 열린다.
    /// 그래서 로카팔라를 몇 명 세웠는지가 그대로 인드라 궁극기 한 번의 값이 된다.
    ///
    /// <b>모든 예약은 여기를 지난다.</b> 다른 코드가 <c>EnqueueSpecial</c>을 직접 부르면
    /// 그 규칙이 조용히 깨지므로, 여는 경로를 이 한 곳으로 묶어 둔다.
    /// </summary>
    public static class SpecialAction
    {
        /// <summary>이 태그를 가진 아군만 특수행동을 받는다.</summary>
        public const string LokapalaTag = "Lokapala";

        private const int GoldRushStatusId = 6542;

        /// <summary>
        /// 필드에 선 로카팔라 가운데 <b>실제로 특수행동을 가진</b> 아군.
        ///
        /// 지금은 로카팔라 여덟이 모두 SP를 갖지만, 태그만 붙고 SP가 없는 유닛이 생기면
        /// 여기서 걸러진다 — 야마의 바운스 타수도 이 인원수를 쓰므로 빈손을 세면 안 된다.
        /// </summary>
        public static List<Unit> Participants(Unit caster)
        {
            if (caster == null) return new List<Unit>();

            return CombatTargets.AliveAlliesIncludingSelf(caster)
                .Where(unit => unit.HasUnitTag(LokapalaTag) && unit.HasSpecialAction)
                .ToList();
        }

        /// <summary>야마의 바운스 타수처럼 "몇 명이 함께 나가는가"를 묻는 자리.</summary>
        public static int ParticipantCount(Unit caster) => Participants(caster).Count;

        /// <summary>
        /// 필드의 로카팔라 전원에게 특수행동을 연다. 인드라의 궁극기만 부른다.
        /// </summary>
        /// <returns>실제로 줄을 선 인원.</returns>
        public static int OpenGate(Unit caster)
        {
            ActionScheduler scheduler = GameManager.Instance?.ActionScheduler;
            if (scheduler == null) return 0;

            // 앞서야 값을 하는 행동(바유의 선봉의 바람)이 먼저 줄을 서도록 정렬한다.
            // 스케줄러는 같은 종류 안에서 예약 순서를 지키므로 이 정렬이 곧 실행 순서다.
            List<Unit> lineup = Participants(caster)
                .OrderBy(unit => unit.ActiveSpecialCode.OpenOrder)
                .ToList();

            _batchRemaining = 0;
            int opened = 0;
            foreach (Unit unit in lineup)
            {
                // 같은 유닛의 특수행동이 큐에 둘 이상 쌓이지 않도록 키를 유닛으로 고정한다.
                if (scheduler.EnqueueSpecial(unit, "special", unit.ActiveSpecialCode.CodeName,
                        unit.CastSpecialCode))
                {
                    opened++;
                }
            }

            _batchRemaining = opened;
            Debug.Log($"[특수행동] {caster?.UnitName}이(가) 문을 열어 로카팔라 {opened}명이 나선다");
            return opened;
        }

        /// <summary>이번 개방에서 아직 나서지 않은 인원.</summary>
        private static int _batchRemaining;

        /// <summary>이번 개방이 끝날 때 함께 걷을 것들.</summary>
        private static readonly List<System.Action> _batchCleanups = new();

        /// <summary>개방이 끝날 때까지만 살아 있어야 하는 처리를 맡긴다.</summary>
        public static void RegisterBatchCleanup(System.Action cleanup)
        {
            if (cleanup != null) _batchCleanups.Add(cleanup);
        }

        /// <summary>특수행동 하나가 끝났음을 알린다. <see cref="Codes.Base.SpecialCode.StopCode"/>가 부른다.</summary>
        public static void NotifyResolved()
        {
            if (_batchRemaining <= 0) return;
            if (--_batchRemaining > 0) return;

            foreach (System.Action cleanup in _batchCleanups) cleanup?.Invoke();
            _batchCleanups.Clear();
        }

        /// <summary>쿠베라의 골드 러쉬가 쌓는 골드 배율. 중첩된다.</summary>
        public static void AddGoldRushStack(Unit owner)
        {
            if (owner == null) return;

            owner.AddStatus(BuffStatus.Create(
                GoldRushStatusId, "kubera_gold_rush_reward", "골드 러쉬",
                owner, owner, new GoldRushEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Stack,
                isBeneficial: true,
                description: "전투에서 획득하는 골드가 5% 증가합니다. 중첩됩니다."));
        }

        /// <summary>필드 아군이 쌓아 둔 골드 러쉬 배율. <see cref="Codes.Passive.RewardModifiers"/>가 읽는다.</summary>
        public static float GoldRushMultiplier()
        {
            GridManager grid = GridManager.Instance;
            if (grid?.heroList == null) return 1f;

            int stacks = grid.heroList
                .Where(hero => hero != null && hero.isActive && hero.ActiveStatuses != null)
                .Sum(hero => hero.ActiveStatuses.Count(status => status.Effects
                    .Any(effect => effect.EffectObject is GoldRushEffect)));
            return 1f + stacks * Codes.Special.KuberaGoldRush.GoldBonusPerUse;
        }
    }

    /// <summary>표식 전용. 배율 계산은 <see cref="SpecialAction.GoldRushMultiplier"/>가 한다.</summary>
    public sealed class GoldRushEffect : BaseEffect
    {
        public GoldRushEffect() : base(0) { }
    }
}
