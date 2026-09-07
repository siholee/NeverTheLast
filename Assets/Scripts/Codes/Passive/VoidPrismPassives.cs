using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    public static class VoidPrismCodeIds
    {
        public const int Core = 1440;
        public const int Resonance = 1441;
        public const int Split = 1442;
        public const int DeathEcho = 1443;
        public const int DestructionRay = 1444;
    }

    public static class VoidPrismStatusIds
    {
        public const int Core = 7870;
        public const int Resonance = 7871;
        public const int Split = 7872;
        public const int DeathEcho = 7873;
        public const int DeathEchoStats = 7874;
        public const int SplitStats = 7875;
        public const int DestructionRay = 7876;
        public const int DestroyedArmor = 7877;
    }

    internal static class VoidPrismCombat
    {
        public const string IdentityTag = "VoidPrism";
        public const string AttributeTag = "VoidMonster";

        public static IEnumerable<Unit> FieldAllies(Unit unit)
        {
            if (unit == null || GridManager.Instance == null) return Enumerable.Empty<Unit>();
            List<Unit> side = unit.IsEnemy ? GridManager.Instance.enemyList : GridManager.Instance.heroList;
            return side.Where(ally => ally != null && ally.isActive && ally.IsOnField);
        }

        public static IEnumerable<Unit> MatchingAttributeAllies(Unit unit)
            => FieldAllies(unit).Where(ally => ally != unit && ally.HasUnitTag(AttributeTag));

        /// <summary>필드에 서 있는 공허의 프리즘 수. 팔레트 변형도 같은 유닛으로 센다.</summary>
        public static int CountFieldPrisms(Unit unit)
        {
            if (unit == null || GridManager.Instance == null) return 0;

            List<Unit> side = unit.IsEnemy ? GridManager.Instance.enemyList : GridManager.Instance.heroList;
            int count = 0;
            // LINQ 대신 for문을 쓴다. 이 메서드는 INT를 물을 때마다 불리는 자리라
            // 열거자와 클로저 할당이 그대로 프레임 비용이 된다.
            for (int i = 0; i < side.Count; i++)
            {
                Unit ally = side[i];
                if (ally != null && ally.isActive && ally.IsOnField && ally.HasUnitTag(IdentityTag)) count++;
            }
            return count;
        }
    }

    /// <summary>필드의 공허의 프리즘 하나마다 자신의 INT +2%. 팔레트 변형도 같은 유닛으로 센다.</summary>
    public sealed class VoidPrismCore : PersistentStatusPassive
    {
        public VoidPrismCore(PassiveCodeContext context)
            : base(context, VoidPrismStatusIds.Core, "void_prism_core", "프리즘 동조",
                "필드에 있는 공허의 프리즘 하나마다 INT가 2% 증가합니다.")
        {
            IsUniquePassive = true;
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new VoidPrismCoreEffect();
    }

    /// <summary>
    /// 프리즘 동조.
    ///
    /// <see cref="PrimaryStatMultiplierModifier"/>는 <c>GetBaseInt()</c>를 물을 때마다 불린다.
    /// 프리즘이 여덟 기 깔린 편성이라면 질의 한 번에 진영 전체를 여덟 번 훑게 되므로,
    /// <b>같은 프레임 안에서는 센 값을 재사용한다.</b> 프리즘이 죽거나 분열해 수가 바뀌어도
    /// 다음 프레임이면 반영되고, 전투는 프레임 단위로 판정하지 않으므로 어긋나지 않는다.
    /// </summary>
    internal sealed class VoidPrismCoreEffect : BaseEffect
    {
        private const float PerPrism = 0.02f;

        private int _cachedFrame = -1;
        private float _cachedMultiplier = 1f;

        public VoidPrismCoreEffect() : base(0) { }

        public override float PrimaryStatMultiplierModifier(Unit unit, BaseEnums.PrimaryStat stat)
        {
            if (unit != Target || stat != BaseEnums.PrimaryStat.INT) return 1f;

            int frame = Time.frameCount;
            if (frame == _cachedFrame) return _cachedMultiplier;

            _cachedFrame = frame;
            _cachedMultiplier = 1f + VoidPrismCombat.CountFieldPrisms(unit) * PerPrism;
            return _cachedMultiplier;
        }
    }

    /// <summary>사망 시 공허 괴수 속성 아군의 다음 행동을 50% 늦춘다.</summary>
    public sealed class VoidPrismResonance : PersistentStatusPassive
    {
        public VoidPrismResonance(PassiveCodeContext context)
            : base(context, VoidPrismStatusIds.Resonance, "void_prism_resonance", "공명",
                "처치될 경우 공허 괴수 속성 아군의 행동 게이지를 50% 감소시킵니다.")
        {
            SupersededByCodeId = VoidPrismCodeIds.DeathEcho;
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new VoidPrismDeathEffect(grantStats: false);
    }

    /// <summary>공명의 강화 등급. 행동 지연과 함께 사망한 프리즘의 현재 스탯 절반을 부여한다.</summary>
    public sealed class VoidPrismDeathEcho : PersistentStatusPassive
    {
        public VoidPrismDeathEcho(PassiveCodeContext context)
            : base(context, VoidPrismStatusIds.DeathEcho, "void_prism_death_echo", "죽음의 메아리",
                "처치될 경우 공허 괴수 속성 아군의 행동 게이지를 50% 감소시키고 자신의 현재 스탯 절반을 부여합니다.")
        {
            Grade = BaseEnums.CodeGrade.Enhanced;
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new VoidPrismDeathEffect(grantStats: true);
    }

    internal sealed class VoidPrismDeathEffect : BaseEffect
    {
        private readonly bool _grantStats;
        private Action<EventContext> _handler;
        private bool _triggered;

        public VoidPrismDeathEffect(bool grantStats) : base(0) => _grantStats = grantStats;

        public override void OnApply()
        {
            if (Target == null) return;
            _triggered = false;
            _handler = OnDeath;
            Target.AddListener(BaseEnums.UnitEventType.OnDeath, _handler);
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnDeath, _handler);
            _handler = null;
        }

        private void OnDeath(EventContext context)
        {
            Unit self = Target;
            if (_triggered || self == null || context?.Grantee != self) return;
            _triggered = true;

            var stats = new Dictionary<BaseEnums.PrimaryStat, int>();
            if (_grantStats)
            {
                foreach (BaseEnums.PrimaryStat stat in Enum.GetValues(typeof(BaseEnums.PrimaryStat)))
                {
                    stats[stat] = Mathf.Max(0, Mathf.FloorToInt(self.GetBasePrimaryStat(stat) * 0.5f));
                }
            }

            foreach (Unit ally in VoidPrismCombat.MatchingAttributeAllies(self).ToList())
            {
                GameManager.Instance?.ActionScheduler?.DelayAction(ally, 0.5f);
                if (!_grantStats) continue;

                ally.AddStatus(BuffStatus.Create(
                    VoidPrismStatusIds.DeathEchoStats,
                    $"void_prism_death_echo_stats_{Guid.NewGuid():N}", "죽음의 메아리 — 유산",
                    self, ally, new VoidPrismStatGiftEffect(stats),
                    stackPolicy: BaseEnums.StatusStackPolicy.Stack,
                    isBeneficial: true,
                    description: "쓰러진 공허의 프리즘의 현재 스탯 절반을 얻습니다."));
            }
        }
    }

    internal sealed class VoidPrismStatGiftEffect : BaseEffect
    {
        private readonly IReadOnlyDictionary<BaseEnums.PrimaryStat, int> _stats;

        public VoidPrismStatGiftEffect(IReadOnlyDictionary<BaseEnums.PrimaryStat, int> stats) : base(0)
            => _stats = stats;

        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Target && _stats != null && _stats.TryGetValue(stat, out int value) ? value : 0;
    }

    /// <summary>사망 시 빈칸 최대 둘에 같은 프리즘을 절반 스탯으로 생성한다. 원종만 분열한다.</summary>
    public sealed class VoidPrismSplit : PersistentStatusPassive
    {
        public VoidPrismSplit(PassiveCodeContext context)
            : base(context, VoidPrismStatusIds.Split, "void_prism_split", "분열",
                "처치될 경우 빈칸 최대 둘에 절반 스탯을 가진 같은 공허의 프리즘을 생성합니다. 분열체는 다시 분열하지 않습니다.")
        {
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new VoidPrismSplitEffect();
    }

    internal sealed class VoidPrismSplitEffect : BaseEffect
    {
        /// <summary>분열체가 물려받는 스탯 배율.</summary>
        private const float ChildScale = 0.5f;

        private Action<EventContext> _handler;
        private bool _triggered;

        public VoidPrismSplitEffect() : base(0) { }

        public override void OnApply()
        {
            if (Target == null) return;
            _triggered = false;
            _handler = OnDeath;
            Target.AddListener(BaseEnums.UnitEventType.OnDeath, _handler);
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnDeath, _handler);
            _handler = null;
        }

        private void OnDeath(EventContext context)
        {
            Unit self = Target;
            GridManager grid = GridManager.Instance;
            if (_triggered || self == null || context?.Grantee != self || grid == null) return;
            _triggered = true;

            // 원종만 분열한다. 분열체가 다시 분열하면 죽은 칸이 매번 새 개체로 채워져
            // 적 수가 줄지 않고, 라운드가 전멸이 아니라 턴 제한으로만 끝난다.
            if (IsSplitChild(self)) return;

            int unitId = self.ID;
            bool isEnemy = self.IsEnemy;
            int deathX = self.currentCell != null ? self.currentCell.xPos : 0;
            int deathY = self.currentCell != null ? self.currentCell.yPos : 0;

            self.EnqueuePostDeathAction(() => SpawnChildren(
                self, grid, unitId, isEnemy, deathX, deathY, ChildScale));
        }

        /// <summary>분열로 태어난 개체인가. 반감 상태를 표식으로 쓴다.</summary>
        private static bool IsSplitChild(Unit unit)
            => unit.ActiveStatuses
                .SelectMany(status => status.Effects)
                .Any(effect => effect.EffectObject is VoidPrismSplitStatEffect);

        private static void SpawnChildren(
            Unit self, GridManager grid, int unitId, bool isEnemy, int deathX, int deathY, float childScale)
        {
            int spawnedCount = 0;

            // 사망한 본체의 칸은 DeactivateUnit 직후 비어 있으므로 분열 후보에 가장 먼저 넣는다.
            if (grid.IsValidFieldPosition(deathX, deathY) && self.currentCell != null &&
                !self.currentCell.isOccupied)
            {
                spawnedCount += SpawnChild(self, grid, unitId, isEnemy, deathX, deathY, childScale) ? 1 : 0;
            }

            foreach (int xPos in new[] { grid.GetRearColumn(isEnemy), grid.GetFrontColumn(isEnemy) })
            {
                for (int yPos = grid.yMin; yPos <= grid.yMax && spawnedCount < 2; yPos++)
                {
                    if (xPos == deathX && yPos == deathY) continue;
                    if (!grid.IsCellAvailable(xPos, yPos)) continue;
                    if (SpawnChild(self, grid, unitId, isEnemy, xPos, yPos, childScale)) spawnedCount++;
                }
                if (spawnedCount >= 2) break;
            }

            Debug.Log($"[분열] {self.UnitName}이(가) 절반 스탯 개체 {spawnedCount}기를 생성했습니다.");
        }

        private static bool SpawnChild(
            Unit self, GridManager grid, int unitId, bool isEnemy, int xPos, int yPos, float childScale)
        {
            Unit spawned = grid.SpawnUnit(xPos, yPos, isEnemy, unitId);
            if (spawned == null) return false;

            spawned.AddStatus(BuffStatus.Create(
                VoidPrismStatusIds.SplitStats, "void_prism_split_stats", "분열 — 반감",
                self, spawned, new VoidPrismSplitStatEffect(childScale),
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                isBeneficial: false,
                description: $"분열로 모든 스탯이 {childScale * 100f:F0}%가 되었습니다."));
            spawned.Invoke(BaseEnums.UnitEventType.OnRoundStart, new EventContext(spawned));
            return true;
        }
    }

    /// <summary>분열체의 반감 표식이자 스탯 배율. 이 효과가 붙어 있으면 다시 분열하지 않는다.</summary>
    internal sealed class VoidPrismSplitStatEffect : BaseEffect
    {
        public float Scale { get; }

        public VoidPrismSplitStatEffect(float scale) : base(0, scale)
            => Scale = Mathf.Clamp01(scale);

        public override float PrimaryStatMultiplierModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Target ? Scale : 1f;
    }

    /// <summary>파쇄의 강화 등급. 단일 대상 궁극기 적중 후 방어력 -40%, 3턴.</summary>
    public sealed class VoidPrismDestructionRay : PersistentStatusPassive
    {
        public VoidPrismDestructionRay(PassiveCodeContext context)
            : base(context, VoidPrismStatusIds.DestructionRay, "void_prism_destruction_ray", "파괴광선",
                "단일 대상 궁극기 적중 후 대상의 방어력이 3턴 동안 40% 감소합니다.")
        {
            Grade = BaseEnums.CodeGrade.Enhanced;
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new VoidPrismDestructionRayEffect();
    }

    internal sealed class VoidPrismDestructionRayEffect : BaseEffect
    {
        private Action<DamageResolvedContext> _handler;

        public VoidPrismDestructionRayEffect() : base(0) { }

        public override void OnApply()
        {
            if (Target == null) return;
            _handler = OnDamageDealt;
            Target.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _handler);
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _handler);
            _handler = null;
        }

        private void OnDamageDealt(DamageResolvedContext context)
        {
            DamageContext damage = context?.DamageContext;
            if (context?.Attacker != Target || context.Target == null || context.DamageDealt <= 0 ||
                damage?.CodeType != BaseEnums.CodeType.Ultimate ||
                damage.DamageTags?.Contains(DamageTag.SingleTarget) != true) return;

            context.Target.AddStatus(BuffStatus.Create(
                VoidPrismStatusIds.DestroyedArmor, "void_prism_destroyed_armor", "파괴광선 — 방어 붕괴",
                Target, context.Target, new VoidPrismArmorShredEffect(),
                duration: 3,
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                category: BaseEnums.StatusCategory.Negative,
                isBeneficial: false,
                description: "방어력이 40% 감소합니다."));
        }
    }

    internal sealed class VoidPrismArmorShredEffect : BaseEffect
    {
        public VoidPrismArmorShredEffect() : base(0, 0.6f) { }

        public override float OwnedDefenseStatMultiplierModifier(Unit unit, DamageContext context)
            => unit == Target ? 0.6f : 1f;
    }
}
