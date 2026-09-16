using System;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Entities;
using Entities.Status;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>시구르드·브륀힐드가 쓰는 코드·상태 ID.</summary>
    public static class OathIds
    {
        public const int SigurdOath = 284;
        public const int BrynhildOath = 285;

        public const int StatusSigurdOath = 5340;
        public const int StatusBrynhildOath = 5341;

        /// <summary>공명이 터지는 값. 여기 닿은 다음 일반행동이 협공이 된다.</summary>
        public const int ResonanceMax = 3;

        /// <summary>둘이 같은 열에 설 때 협공 위력에 곱하는 값.</summary>
        public const float SameColumnBonus = 1.3f;

        /// <summary>복수 상태에서 받는 피해에 곱하는 값.</summary>
        public const float RevengeTakenMultiplier = 1.25f;
    }

    /// <summary>맹세의 양쪽. 짝을 찾을 때 <b>역할이 다른 쪽</b>을 고른다.</summary>
    public enum OathRole
    {
        Sigurd,
        Brynhild,
    }

    /// <summary>
    /// 맹세를 든 쪽이 다는 표식.
    ///
    /// 유닛 ID로 짝을 찾지 않는다. 아군(84·85)과 적(2051·2052)이 다른 번호를 쓰기 때문에,
    /// <b>역할</b>로 찾아야 한 벌의 코드가 양쪽에서 그대로 돈다.
    /// </summary>
    internal interface IOathPartner
    {
        OathRole Role { get; }

        /// <summary>협공에서 이 쪽이 맡는 몫. 주도자가 고른 대상을 그대로 받는다.</summary>
        void RunCoordinatedShare(Unit target, float multiplier);
    }

    /// <summary>
    /// 공명 — <b>두 사람이 공유하는 스택</b>.
    ///
    /// 진영마다 한 쌍만 서므로 진영(아군/적)으로 가른다. 라운드가 끝나면 함께 지워진다
    /// (<c>GridManager.OnRoundEnd</c>). 두 효과가 각자 세면 어느 쪽이 먼저 행동했느냐에 따라
    /// 값이 갈리므로, 판에 한 번만 적어 두고 양쪽이 같은 자리를 읽는다 — 전장 상태와 같은 이유다.
    /// </summary>
    public static class OathBond
    {
        private static int _ally;
        private static int _enemy;

        public static int Get(bool isEnemy) => isEnemy ? _enemy : _ally;

        public static void Set(bool isEnemy, int value)
        {
            value = Mathf.Max(0, value);
            if (isEnemy) _enemy = value;
            else _ally = value;
        }

        public static void Add(bool isEnemy, int amount) => Set(isEnemy, Get(isEnemy) + amount);

        /// <summary>라운드가 끝나면 공명도 함께 걷힌다.</summary>
        public static void Clear()
        {
            _ally = 0;
            _enemy = 0;
        }
    }

    /// <summary>
    /// 시구르드 고유 P — 맹세.
    ///
    /// <b>짝이 없으면 협동은 일어나지 않는다.</b> 혼자 선 시구르드는 평범한 전열 검사이고,
    /// 공명도 차지 않는다. 짝이 함께 섰다가 <b>쓰러진 뒤에만</b> 복수가 그 자리를 대신한다 —
    /// 처음부터 혼자인 것과 짝을 잃은 것은 다른 상태다.
    /// </summary>
    public sealed class SigurdOath : PersistentStatusPassive
    {
        public SigurdOath(PassiveCodeContext context)
            : base(context, OathIds.StatusSigurdOath, "oath_sigurd", "맹세",
                "브륀힐드와 함께 서면 공명이 쌓이고, 3에서 다음 일반행동이 협공이 됩니다. " +
                "궁극기를 쓰면 공명과 무관하게 협공이 한 번 따라붙습니다. " +
                "짝이 쓰러지면 복수 상태가 되어 그 몫까지 대신 맡습니다.")
        {
            IsUniquePassive = true;
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new OathEffect(OathRole.Sigurd);
    }

    /// <summary>브륀힐드 고유 P — 맹세. 시구르드와 같은 코드의 반대편이다.</summary>
    public sealed class BrynhildOath : PersistentStatusPassive
    {
        public BrynhildOath(PassiveCodeContext context)
            : base(context, OathIds.StatusBrynhildOath, "oath_brynhild", "맹세",
                "시구르드와 함께 서면 공명이 쌓이고, 3에서 다음 일반행동이 협공이 됩니다. " +
                "궁극기를 쓰면 공명과 무관하게 협공이 한 번 따라붙습니다. " +
                "짝이 쓰러지면 복수 상태가 되어 그 몫까지 대신 맡습니다.")
        {
            IsUniquePassive = true;
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new OathEffect(OathRole.Brynhild);
    }

    /// <summary>맹세 본체. 두 역할이 같은 클래스를 쓰고 몫만 갈린다.</summary>
    internal sealed class OathEffect : BaseEffect, IOathPartner
    {
        // ── 협공 몫의 위력 ───────────────────────────────────────────
        //
        // 고정 위력이다. 스탯 비례 위력으로 잡으면 `피해 = 위력 × 주스탯 × 0.2`에서
        // 스탯이 두 번 곱해져 레벨에 제곱으로 커진다 — 일반행동(고정 위력 90)과 곡선이 갈린다.
        // 70은 일반행동의 약 78%이며, 턴을 쓰지 않는 몫이라 그만큼을 뗀 값이다.
        private const int SigurdSharePower = 70;        // 단일
        private const int BrynhildSharePower = 50;      // 열 광역
        private const int InheritedSinglePower = 70;
        private const int InheritedColumnPower = 50;

        private Action<EventContext> _normalResolvedHandler;
        private Action<EventContext> _ultimateHandler;
        private Action<EventContext> _hitHandler;
        private Action<Unit, Unit> _deathHandler;

        /// <summary>주도자가 방금 때린 대상. 협공이 같은 자리를 친다 — 그래야 원소가 겹친다.</summary>
        private Unit _lastTarget;

        /// <summary>이번 전투에서 짝을 한 번이라도 본 적이 있는가. 복수의 전제다.</summary>
        private bool _partnerSeen;

        public OathEffect(OathRole role) : base(0, (int)role) => Role = role;

        public override bool IsBeneficial => true;

        public OathRole Role { get; }

        // ── 수명 ────────────────────────────────────────────────────

        public override void OnApply()
        {
            if (Target == null) return;

            _normalResolvedHandler = _ => OnNormalResolved();
            _ultimateHandler = _ => OnUltimate();
            // 일반행동이 실제로 때린 대상을 기억한다. 협공이 같은 자리를 쳐야 원소가 겹친다.
            _hitHandler = context => _lastTarget = context?.Grantor;
            _deathHandler = (_, __) => RefreshPartnerState();

            Target.AddListener(BaseEnums.UnitEventType.OnNormalActionResolved, _normalResolvedHandler);
            Target.AddListener(BaseEnums.UnitEventType.OnUltimateActivates, _ultimateHandler);
            Target.AddListener(BaseEnums.UnitEventType.OnNormalAttackHit, _hitHandler);
            Unit.AnyUnitDied += _deathHandler;

            RefreshPartnerState();
        }

        public override void OnRemove()
        {
            if (Target != null)
            {
                Target.RemoveListener(BaseEnums.UnitEventType.OnNormalActionResolved, _normalResolvedHandler);
                Target.RemoveListener(BaseEnums.UnitEventType.OnUltimateActivates, _ultimateHandler);
                Target.RemoveListener(BaseEnums.UnitEventType.OnNormalAttackHit, _hitHandler);
            }

            Unit.AnyUnitDied -= _deathHandler;
            _normalResolvedHandler = null;
            _ultimateHandler = null;
            _hitHandler = null;
            _deathHandler = null;
        }

        public override void OnOwnerTurn() => RefreshPartnerState();

        // ── 복수 ────────────────────────────────────────────────────

        /// <summary>짝을 잃었는가. <b>처음부터 혼자였던 것은 복수가 아니다.</b></summary>
        private bool IsAvenging => _partnerSeen && FindPartner() == null;

        private void RefreshPartnerState()
        {
            if (FindPartner() != null) _partnerSeen = true;
        }

        public override float ReceivingDamageModifier(Unit unit)
            => unit == Target && IsAvenging ? OathIds.RevengeTakenMultiplier : 1f;

        // ── 공명 ────────────────────────────────────────────────────

        private void OnNormalResolved()
        {
            if (Target == null || !Target.isActive) return;

            OathEffect partner = FindPartner();
            // 짝이 서 있지도 않고 잃은 적도 없으면 맹세는 잠들어 있다.
            if (partner == null && !IsAvenging) return;

            bool isEnemy = Target.IsEnemy;
            if (OathBond.Get(isEnemy) >= OathIds.ResonanceMax)
            {
                OathBond.Set(isEnemy, 0);
                FireCoordinated(partner);
                return;
            }

            // 복수 중에는 짝이 채우던 몫까지 혼자 받는다. 협공 주기가 듀오 시절로 돌아온다.
            OathBond.Add(isEnemy, IsAvenging ? 2 : 1);
        }

        /// <summary>궁극기는 공명을 쓰지 않고 협공을 한 번 불러온다.</summary>
        private void OnUltimate()
        {
            if (Target == null || !Target.isActive) return;

            OathEffect partner = FindPartner();
            if (partner == null && !IsAvenging) return;

            FireCoordinated(partner);
        }

        private void FireCoordinated(OathEffect partner)
        {
            Unit target = ResolveTarget();
            if (target == null) return;

            float multiplier = SameColumn(partner) ? OathIds.SameColumnBonus : 1f;
            ActionScheduler scheduler = GameManager.Instance?.ActionScheduler;
            if (scheduler == null) return;

            if (partner != null)
            {
                Unit ally = partner.Target;
                if (ally == null || !ally.isActive) return;

                scheduler.EnqueueCoordinated(ally, "oath_coordinated", "협공",
                    () => partner.RunCoordinatedShare(target, multiplier));
                return;
            }

            // 복수 — 생존자가 자기 몫과 죽은 짝의 몫을 함께 넣는다.
            scheduler.EnqueueCoordinated(Target, "oath_revenge", "복수 협공", () =>
            {
                RunCoordinatedShare(target, multiplier);
                RunInheritedShare(target, multiplier);
            });
        }

        // ── 협공 몫 ─────────────────────────────────────────────────

        public void RunCoordinatedShare(Unit target, float multiplier)
        {
            if (Target == null || !Target.isActive || target == null || !target.isActive) return;

            if (Role == OathRole.Sigurd)
            {
                StrikeSingle(target, SigurdSharePower, multiplier,
                    BaseEnums.PrimaryStat.STR, BaseEnums.UnitElement.Pyro, DamageTag.Slash);
            }
            else
            {
                StrikeColumn(target, BrynhildSharePower, multiplier,
                    BaseEnums.PrimaryStat.DEX, BaseEnums.UnitElement.Cryo, DamageTag.Pierce);
            }
        }

        /// <summary>
        /// 죽은 짝의 몫. <b>생존자의 주스탯으로 계산한다</b> —
        /// 죽은 쪽 스탯을 참조하면 "약한 쪽이 먼저 죽으면 손해"가 생긴다.
        /// 모양과 원소만 물려받으므로 혼자서도 불과 얼음을 한 협공에 담는다.
        /// </summary>
        private void RunInheritedShare(Unit target, float multiplier)
        {
            if (Target == null || !Target.isActive || target == null || !target.isActive) return;

            if (Role == OathRole.Sigurd)
            {
                // 브륀힐드의 몫 — 열 광역 + 얼음
                StrikeColumn(target, InheritedColumnPower, multiplier,
                    BaseEnums.PrimaryStat.STR, BaseEnums.UnitElement.Cryo, DamageTag.Pierce);
            }
            else
            {
                // 시구르드의 몫 — 단일 + 불
                StrikeSingle(target, InheritedSinglePower, multiplier,
                    BaseEnums.PrimaryStat.DEX, BaseEnums.UnitElement.Pyro, DamageTag.Slash);
            }
        }

        private void StrikeSingle(Unit target, int power, float multiplier,
            BaseEnums.PrimaryStat stat, BaseEnums.UnitElement element, int weaponTag)
        {
            int damage = ShareDamage(power, multiplier, stat);
            Hit(target, damage, DamageTag.SingleTarget, weaponTag, element);
        }

        private void StrikeColumn(Unit target, int power, float multiplier,
            BaseEnums.PrimaryStat stat, BaseEnums.UnitElement element, int weaponTag)
        {
            int damage = ShareDamage(power, multiplier, stat);
            int column = target.currentCell != null ? Mathf.Abs(target.currentCell.xPos) : 0;

            foreach (Unit enemy in CombatTargets.AliveEnemies(Target))
            {
                if (enemy == null || !enemy.isActive) continue;
                // 같은 열만 훑는다. 대상의 칸을 모르면 그 대상만 친다.
                if (column > 0 && (enemy.currentCell == null || Mathf.Abs(enemy.currentCell.xPos) != column))
                {
                    if (enemy != target) continue;
                }

                Hit(enemy, damage, DamageTag.AllTarget, weaponTag, element);
            }
        }

        private int ShareDamage(int power, float multiplier, BaseEnums.PrimaryStat stat)
            => Mathf.Max(1, Mathf.RoundToInt(Target.SkillDamage(power, stat) * multiplier));

        private void Hit(Unit target, int damage, int scopeTag, int weaponTag, BaseEnums.UnitElement element)
        {
            target.TakeDamage(new DamageContext(
                Target, damage, BaseEnums.CodeType.Passive,
                new List<int>
                {
                    scopeTag, DamageTag.CoordinatedAttack,
                    DamageTag.ContactAttack, DamageTag.Physical, weaponTag,
                }));

            if (target.isActive)
            {
                target.GrantCombatElement(element, Unit.CommonElementAuraDuration, Target);
            }
        }

        // ── 짝 찾기 ─────────────────────────────────────────────────

        private Unit ResolveTarget()
        {
            if (_lastTarget != null && _lastTarget.isActive) return _lastTarget;
            return CombatTargets.PickByPriority(CombatTargets.AliveEnemies(Target));
        }

        private bool SameColumn(OathEffect partner)
        {
            Cell mine = Target?.currentCell;
            Cell theirs = partner?.Target?.currentCell;
            if (mine == null || theirs == null) return false;
            return Mathf.Abs(mine.xPos) == Mathf.Abs(theirs.xPos);
        }

        /// <summary>판에 선 같은 편에서 <b>역할이 다른</b> 맹세 보유자를 찾는다.</summary>
        private OathEffect FindPartner()
        {
            if (Target == null) return null;

            var side = Target.IsEnemy
                ? GridManager.Instance?.enemyList
                : GridManager.Instance?.heroList;
            if (side == null) return null;

            for (int i = 0; i < side.Count; i++)
            {
                Unit ally = side[i];
                if (ally == null || ally == Target || !ally.isActive || !ally.IsOnField) continue;

                var statuses = ally.ActiveStatuses;
                for (int j = 0; j < statuses.Count; j++)
                {
                    var effects = statuses[j]?.Effects;
                    if (effects == null) continue;

                    for (int k = 0; k < effects.Count; k++)
                    {
                        if (effects[k]?.EffectObject is OathEffect other && other.Role != Role) return other;
                    }
                }
            }

            return null;
        }
    }
}
