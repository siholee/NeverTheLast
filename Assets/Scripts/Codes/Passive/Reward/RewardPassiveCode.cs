using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Entities.Status;
using Managers;

namespace Codes.Passive.Reward
{
    /// <summary>
    /// 보상 코드(Detail_17)의 공통 뼈대. 이름 · 상위 · 등급을 `20_codes.yaml`에서 읽고,
    /// 라운드 시작에 건 상태와 구독한 이벤트를 라운드 종료 · 코드 제거 때 한 번에 걷는다.
    ///
    /// 하위 클래스는 <see cref="OnCast"/>에서 상태를 걸거나 이벤트를 구독하고, 정리는 <see cref="OnStop"/>에 둔다.
    /// 상태는 <see cref="AddStatus"/>로 걸면 자동으로 걷힌다.
    /// </summary>
    public abstract class RewardPassiveCode : PassiveCode
    {
        /// <summary>보상 코드 상태 ID 구간. 캐릭터 상태(70000번대)와 겹치지 않는다.</summary>
        protected const int StatusIdBase = 95000;

        protected readonly CodeCatalog.Entry Entry;
        protected readonly int RewardId;
        private readonly List<(Unit owner, string key)> _ownedStatuses = new();
        private bool _active;

        protected RewardPassiveCode(PassiveCodeContext context, int rewardId) : base(context)
        {
            RewardId = rewardId;
            Entry = RewardCodeCatalog.Find(rewardId);
            CodeName = Entry?.verbalName ?? $"보상 코드 {rewardId}";
            SupersededByCodeId = Entry?.supersededBy ?? 0;
            Grade = (Entry?.tier ?? 1) >= 3 ? BaseEnums.CodeGrade.Enhanced : BaseEnums.CodeGrade.Normal;
        }

        public int Tier => Entry?.tier ?? 0;

        public sealed override void CastCode()
        {
            if (Caster == null) return;
            if (_active) StopCode();
            _active = true;
            OnCast();
        }

        public sealed override void StopCode()
        {
            if (!_active) return;
            _active = false;
            OnStop();
            foreach ((Unit owner, string key) in _ownedStatuses.ToList())
            {
                if (owner != null) owner.RemoveStatusByKey(key);
            }
            _ownedStatuses.Clear();
        }

        /// <summary>라운드 시작(또는 장착 직후 전투 중)에 한 번. 상태 · 구독을 건다.</summary>
        protected abstract void OnCast();

        /// <summary>정리. <see cref="AddStatus"/>로 건 상태는 자동으로 걷히므로 이벤트 구독만 푼다.</summary>
        protected virtual void OnStop() { }

        protected bool IsActive => _active && Caster != null;

        /// <summary>이 코드 고유의 상태 키. 보유자마다 따로라 두 유닛이 같은 코드를 들어도 섞이지 않는다.</summary>
        protected string StatusKey(string suffix = "") => $"reward_{RewardId}_{Caster?.GetInstanceID() ?? 0}{suffix}";

        protected int StatusId(int offset = 0) => StatusIdBase + RewardId * 4 + offset;

        /// <summary>상태를 걸고 정리 목록에 올린다.</summary>
        protected UnitStatus AddStatus(Unit owner, BaseEffect effect, string suffix = "", int duration = 0,
            BaseEnums.StatusCategory category = BaseEnums.StatusCategory.Positive,
            BaseEnums.StatusStackPolicy stackPolicy = BaseEnums.StatusStackPolicy.Replace, string key = null,
            bool skipEffectHitRoll = false)
        {
            if (owner == null || effect == null) return null;
            string statusKey = key ?? StatusKey(suffix);
            UnitStatus status = BuffStatus.Create(StatusId(), statusKey, CodeName, Caster, owner, effect,
                duration: duration, stackPolicy: stackPolicy, category: category,
                isBeneficial: category == BaseEnums.StatusCategory.Positive,
                description: Entry?.description ?? "");
            status.SkipEffectHitRoll = skipEffectHitRoll;
            // 보상 코드의 상시 상태는 카드 위에 따로 빛나지 않는다 — 코드 칸이 이미 설명한다.
            status.AffectedVfxPartId = Effects.Vfx.StatusAffectedVfx.SuppressPartId;
            owner.AddStatus(status);
            if (!_ownedStatuses.Contains((owner, statusKey))) _ownedStatuses.Add((owner, statusKey));
            return status;
        }

        /// <summary>보유자 쪽 진영의 살아 있는 필드 아군(자신 포함).</summary>
        protected List<Unit> FieldAllies()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || Caster == null) return new List<Unit>();
            List<Unit> pool = Caster.IsEnemy ? grid.enemyList : grid.heroList;
            return pool.Where(unit => unit != null && unit.isActive && unit.IsOnField).ToList();
        }

        protected List<Unit> FieldEnemies()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || Caster == null) return new List<Unit>();
            List<Unit> pool = Caster.IsEnemy ? grid.heroList : grid.enemyList;
            return pool.Where(unit => unit != null && unit.isActive && unit.IsOnField && !unit.IsUntargetable).ToList();
        }

        /// <summary>보유자가 지금 실제로 일하는가(살아서 필드에 섰고 상위에 대체되지 않았다).</summary>
        protected bool HolderWorking => IsActive && Caster.isActive && Caster.IsOnField;
    }

    /// <summary>
    /// "필드에 있으면 아군 전원 ~, 여러 명이 들면 높은 하나만" 오라의 공용 판정(Detail_17 §2.1).
    /// 같은 계열(family)을 가진 보유자 중 살아서 필드에 선 사람의 가장 큰 값을 돌려준다.
    /// </summary>
    public interface IRewardAuraSource
    {
        string AuraFamily { get; }
        float AuraValue { get; }
    }

    public static class RewardAura
    {
        /// <summary>그 진영에서 이 계열 오라의 가장 큰 값. 없으면 0.</summary>
        public static float Best(Unit anyAlly, string family)
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || anyAlly == null) return 0f;
            List<Unit> pool = anyAlly.IsEnemy ? grid.enemyList : grid.heroList;
            float best = 0f;
            foreach (Unit holder in pool)
            {
                if (holder == null || !holder.isActive || !holder.IsOnField) continue;
                foreach (PassiveCode code in holder.ActivePassiveCodesResolved())
                {
                    if (code is IRewardAuraSource source && source.AuraFamily == family)
                        best = Math.Max(best, source.AuraValue);
                }
            }
            return best;
        }

        /// <summary>오라 계열 하나당 아군마다 한 상태. 값은 질의할 때마다 <see cref="Best"/>로 다시 계산한다.</summary>
        public static string KeyOf(string family) => $"reward_aura_{family}";
    }
}
