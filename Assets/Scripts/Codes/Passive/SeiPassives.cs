using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Effects.Projectiles;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>
    /// 세이 고유 패시브 — 빅뱅. 붕괴: 스타레일의 어벤츄린을 참고했다.
    ///
    /// <b>우주의 파편</b>을 최대 10개까지 모은다. 7개가 모이면 7개를 소모해 무작위 적에게
    /// 고정 위력 60 + INT×0.5의 투사체 7발을 쏘고(우선 추가행동), 이어서 아군 전체에
    /// 150 + STR×10 보호막을 씌운다.
    ///
    /// 파편은 두 가지로 모인다.
    ///   · 세이의 턴이 올 때마다 1개 — 아무도 맞지 않아도 주기가 돈다
    ///   · 보호막을 두른 아군이나 세이 자신이 적에게 공격받을 때마다 1개 — 보호막이 곧 충전기다
    ///
    /// 예전에는 3턴마다 6발을 쏘는 시계였다. 지금은 세이의 보호막이 맞아 줄수록 빨리 돌아
    /// "범용 쉴더"가 스스로 굴러가는 구조다. 지속피해와 반사 피해는 파편을 주지 않는다.
    /// </summary>
    public sealed class SeiBigBang : UniquePassiveCode
    {
        public const int MaxStacks = 10;
        public const int TriggerStacks = 7;
        public const int MissileFlatPower = 60;
        public const float MissileIntCoefficient = 0.5f;
        public const int ShieldFlat = 150;
        public const float ShieldStrCoefficient = 10f;
        private const float MissileLaunchInterval = 0.06f;
        private const float MissileFlightDuration = 0.42f;

        public SeiBigBang(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "빅뱅";
            IgnoresActivationChance = true;
            Transferable = false;
        }

        public override void CastCode()
        {
            if (Caster == null || Caster.HasStatus(SeiStatusIds.BigBang)) return;
            Caster.AddStatus(BuffStatus.Create(
                SeiStatusIds.BigBang, "sei_big_bang", CodeName,
                Caster, Caster, new BigBangEffect(MissileLaunchInterval, MissileFlightDuration),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: $"우주의 파편을 최대 {MaxStacks}개 모읍니다(자기 턴마다, 보호막을 두른 아군이나 자신이 공격받을 때마다 1개). " +
                             $"{TriggerStacks}개가 모이면 소모해 고정 위력 {MissileFlatPower} + INT×{MissileIntCoefficient:0.#}의 " +
                             $"투사체 {TriggerStacks}발을 쏘고, " +
                             $"아군 전체에 {ShieldFlat} + STR×{ShieldStrCoefficient:0}의 보호막을 씌웁니다."));
        }

        public static int GetMissilePower(Unit caster)
            => Mathf.Max(1, MissileFlatPower + Mathf.RoundToInt(
                (caster?.GetBaseInt() ?? 0) * MissileIntCoefficient));
    }

    /// <summary>세이가 쓰는 상태 ID 대역.</summary>
    public static class SeiStatusIds
    {
        public const int BigBang = 5111;

        /// <summary>우주의 파편 한 개. 같은 키로 여러 개를 얹어 카드에 개수가 뜨게 한다.</summary>
        public const int Fragment = 5112;
    }

    /// <summary>
    /// 빅뱅 본체. 파편을 세고, 아군이 맞는 것을 듣고, 7개가 되면 추가행동을 예약한다.
    /// 발사와 명중을 분리해 투사체가 날아가는 동안 대상이 죽어도 다른 적으로 넘어가게 했다.
    /// </summary>
    internal sealed class BigBangEffect : BaseEffect
    {
        private const string FragmentKey = "sei_big_bang_fragment";
        private const string ActionKey = "sei_big_bang";

        private readonly float _launchInterval;
        private readonly float _flightDuration;
        private readonly Dictionary<Unit, Action<EventContext>> _allyHandlers = new();

        private int _fragments;

        public BigBangEffect(float launchInterval, float flightDuration) : base(0, SeiBigBang.MissileFlatPower)
        {
            _launchInterval = launchInterval;
            _flightDuration = flightDuration;
        }

        public override void OnApply()
        {
            if (Target == null) return;
            // 라운드 시작 시점의 아군 전원을 듣는다. 도중에 들어오는 소환수는 보호막 대상이 아니라 제외한다.
            foreach (Unit ally in Combat.CombatTargets.AliveAlliesIncludingSelf(Target))
            {
                if (ally == null || _allyHandlers.ContainsKey(ally)) continue;
                Unit captured = ally;
                Action<EventContext> handler = context => OnAllyAttacked(captured, context);
                _allyHandlers[ally] = handler;
                ally.AddListener(BaseEnums.UnitEventType.OnBeforeDamageTaken, handler);
            }
        }

        public override void OnRemove()
        {
            foreach (KeyValuePair<Unit, Action<EventContext>> pair in _allyHandlers)
            {
                if (pair.Key != null) pair.Key.RemoveListener(BaseEnums.UnitEventType.OnBeforeDamageTaken, pair.Value);
            }
            _allyHandlers.Clear();
            // 파편 표식은 라운드 종료의 상태 일괄 정리가 함께 걷는다. 여기서 지우면 정리 중인 목록을 건드린다.
            _fragments = 0;
        }

        public override void OnOwnerTurn() => Gain(1);

        private void OnAllyAttacked(Unit ally, EventContext context)
        {
            DamageContext damage = context?.DmgCtx;
            if (Target == null || !Target.isActive || ally == null || damage == null) return;
            Unit attacker = damage.Attacker;
            if (attacker == null || attacker.IsEnemy == Target.IsEnemy) return;
            // 지속피해와 되돌린 피해는 "공격받았다"가 아니다.
            if (damage.CodeType == BaseEnums.CodeType.Effect) return;
            if (damage.DamageTags != null && damage.DamageTags.Contains(DamageTag.TrueDamage)) return;
            if (ally != Target && ally.ShieldCurr <= 0) return;
            Gain(1);
        }

        private void Gain(int amount)
        {
            if (Target == null || !Target.isActive || amount <= 0) return;

            int before = _fragments;
            _fragments = Mathf.Min(SeiBigBang.MaxStacks, _fragments + amount);
            for (int i = before; i < _fragments; i++)
            {
                Target.AddStatus(BuffStatus.Create(
                    SeiStatusIds.Fragment, FragmentKey, "우주의 파편",
                    Target, Target, new MarkerBuffEffect(),
                    stackPolicy: BaseEnums.StatusStackPolicy.Stack,
                    isBeneficial: true,
                    description: $"{SeiBigBang.TriggerStacks}개가 모이면 빅뱅이 터집니다. 최대 {SeiBigBang.MaxStacks}개."));
            }

            if (_fragments < SeiBigBang.TriggerStacks || RandomEnemy() == null) return;

            // 같은 키의 추가행동은 큐에 하나만 선다. 파편은 실제로 쏠 때 뺀다.
            Managers.GameManager.Instance?.ActionScheduler.EnqueuePriorityAdditional(
                Target, ActionKey, "빅뱅", () => Target.StartCoroutine(FireVolley()));
        }

        /// <summary>파편 7개를 쓰고 남은 개수만큼 표식을 다시 얹는다.</summary>
        private bool Consume()
        {
            if (_fragments < SeiBigBang.TriggerStacks) return false;
            _fragments -= SeiBigBang.TriggerStacks;
            Target.RemoveStatusByKey(FragmentKey);
            int remain = _fragments;
            _fragments = 0;
            Gain(remain);
            return true;
        }

        private IEnumerator FireVolley()
        {
            Unit caster = Target;
            if (caster == null || !caster.isActive || !Consume()) yield break;

            for (int index = 0; index < SeiBigBang.TriggerStacks; index++)
            {
                if (caster == null || !caster.isActive) yield break;

                Unit target = RandomEnemy();
                if (target == null) break;

                float angle = index * Mathf.PI * 2f / SeiBigBang.TriggerStacks;
                Vector3 midpoint = (caster.transform.position + target.transform.position) * 0.5f;
                Vector3 fanOffset = new(Mathf.Cos(angle) * 3.2f, Mathf.Sin(angle) * 2.4f, 0f);
                ProjectilePathData path = ProjectilePathData.CreateBezier(0f);
                path.bezierControlPoint = midpoint + fanOffset;

                var missilePrefab = ElementalProjectiles.PrefabFor(BaseEnums.UnitElement.Anemo)
                    ?? ElementalProjectiles.PrefabFor(BaseEnums.UnitElement.Geo);
                GameManager.Instance?.sfxManager?.FireTintedProjectile(
                    missilePrefab,
                    BaseEnums.UnitElement.Geo,
                    caster,
                    target,
                    _flightDuration,
                    ProjectilePathType.BezierCurve,
                    path);

                caster.StartCoroutine(ResolveMissile(caster, target));
                yield return new WaitForSeconds(_launchInterval);
            }

            // 마지막 발이 닿은 뒤에 보호막을 씌운다. 연출 순서가 "쏘고 → 막는다"로 읽혀야 한다.
            yield return new WaitForSeconds(_flightDuration);
            if (caster == null || !caster.isActive) yield break;

            int shield = Mathf.Max(1, Mathf.RoundToInt(
                SeiBigBang.ShieldFlat + caster.GetBaseStr() * SeiBigBang.ShieldStrCoefficient));
            foreach (Unit ally in Combat.CombatTargets.AliveAlliesIncludingSelf(caster))
            {
                if (ally != null && ally.isActive) ally.AddShield(shield, caster);
            }
            Debug.Log($"[빅뱅] {caster.UnitName}: 아군 전체 보호막 {shield}");
        }

        private IEnumerator ResolveMissile(Unit caster, Unit plannedTarget)
        {
            yield return new WaitForSeconds(_flightDuration);
            if (caster == null || !caster.isActive) yield break;

            Unit target = plannedTarget != null && plannedTarget.isActive && !plannedTarget.IsUntargetable
                ? plannedTarget
                : RandomEnemy();
            if (target == null) yield break;

            bool isCrit = UnityEngine.Random.value <= caster.CritChanceCurr;
            float critMultiplier = isCrit ? caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                caster.SkillDamage(SeiBigBang.GetMissilePower(caster), BaseEnums.PrimaryStat.INT) *
                critMultiplier));

            var tags = new List<int>
            {
                DamageTag.SingleTarget,
                DamageTag.AdditionalAttack,
                DamageTag.Special,
                DamageTag.NonContactAttack,
            };
            target.TakeDamage(new DamageContext(caster, damage, BaseEnums.CodeType.Passive, tags, isCrit));
        }

        private Unit RandomEnemy()
        {
            if (Target == null) return null;
            List<Unit> enemies = global::Target.GetAllEnemies(Target)
                .Where(unit => unit != null && unit.isActive && !unit.IsUntargetable)
                .ToList();
            return enemies.Count > 0 ? enemies[UnityEngine.Random.Range(0, enemies.Count)] : null;
        }
    }

    /// <summary>
    /// 숙련된 조교(14) — 서포트로서 <b>어느 훈련이든</b> 배치되면 그 훈련의 효율 +10%.
    ///
    /// 피지컬 코치(STR) · 면벽수련(CON)처럼 특정 스탯에 묶인 코치 코드와 달리 스탯을 가리지 않는다.
    /// 특기 스탯이 메인과 맞지 않는 서포트도 훈련 카드로서 값을 하게 하는 범용 코드다.
    /// </summary>
    public sealed class SkilledInstructor : PassiveCode
    {
        public const float TrainingBonus = 0.10f;

        public SkilledInstructor(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "숙련된 조교";
            IgnoresActivationChance = true;
        }

        // 스탯을 가리지 않는다 — 그 훈련이 올리는 모든 몫에 붙는다.
        public override float SupportTrainingBonus(BaseEnums.PrimaryStat stat) => TrainingBonus;
    }

    /// <summary>
    /// 사전준비 (Lv.50) — 전투 시작 시 자신의 궁극기 충전량이 100%가 된다.
    /// 마나를 쓰지 않는 특수 궁극기 보유자에게는 효과가 없다.
    /// </summary>
    public sealed class SeiPreparation : PassiveCode
    {
        public SeiPreparation(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "사전준비";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster?.FillUltimateResource(manaOnly: true);
        }
    }

    /// <summary>
    /// 창세의 노래 (Lv.70) — 전투 중 <b>최초로</b> 궁극기를 발동하면 아군 전체의 궁극기 충전량이 100%가 된다.
    /// 마나를 쓰지 않는 특수 궁극기 보유자에게도 적용된다.
    /// </summary>
    public sealed class SeiFirstSong : PassiveCode
    {
        private bool _registered;
        private bool _consumed;
        private Action<EventContext> _ultimateHandler;
        private Action<EventContext> _cleanupHandler;

        public SeiFirstSong(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "창세의 노래";
            IgnoresActivationChance = true;
            Transferable = false;   // 전수 불가능
        }

        public override void CastCode()
        {
            _consumed = false;
            if (Caster == null || _registered) return;

            _ultimateHandler = OnUltimateActivated;
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnUltimateActivates, _ultimateHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered) return;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnUltimateActivates, _ultimateHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = false;
        }

        private void OnUltimateActivated(EventContext context)
        {
            if (_consumed || Caster == null || !Caster.isActive) return;
            _consumed = true;

            foreach (Unit ally in Target.GetAllAllies(Caster).Where(unit => unit != null && unit.isActive))
            {
                // manaOnly: false — 스택형 자원을 쓰는 아군도 가득 채운다.
                ally.FillUltimateResource(manaOnly: false);
            }
            Debug.Log($"[창세의 노래] {Caster.UnitName}: 아군 전체 궁극기 충전 100%");
        }
    }
}
