using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Effects.Projectiles;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 아스완 궁극기의 공통 뼈대. 시전 지연 → 판정 → 쿨다운 복귀 흐름이 모든 코드에서 같아
    /// 이집트 궁극기들이 각자 반복하던 부분만 한 겹 걷어 냈다.
    /// </summary>
    public abstract class AswanUltimate : UltimateCode
    {
        protected AswanUltimate(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CastingDelay = 0.5f;
        }

        public override void CastCode()
        {
            if (!HasValidTarget()) return;
            Caster.isCasting = true;
            CurrSkillCoroutine = Caster.StartCoroutine(SkillCoroutine());
        }

        protected override IEnumerator SkillCoroutine()
        {
            yield return new WaitForSeconds(CastingDelay);
            if (Caster == null || !Caster.isActive || Caster.isControlled)
            {
                StopCode();
                yield break;
            }

            yield return Resolve();
            StopCode();
        }

        protected abstract IEnumerator Resolve();

        public override void StopCode()
        {
            if (Caster == null) return;
            Caster.isCasting = false;
        }

        public override bool HasValidTarget() => Caster != null && Caster.isActive &&
            AswanCombat.Enemies(Caster).Count > 0;

        /// <summary>적 전체에게 같은 피해를 한 번에 먹인다. 투사체가 닿은 뒤 판정한다.</summary>
        protected IEnumerator StrikeAll(int power, BaseEnums.PrimaryStat stat, List<int> tags)
        {
            List<Unit> targets = AswanCombat.Enemies(Caster);
            LastTargets = targets;
            if (targets.Count == 0) yield break;

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(power, stat) * (isCrit ? Caster.CritMultiplierCurr : 1f)));
            var context = new DamageContext(Caster, damage, BaseEnums.CodeType.Ultimate, tags, isCrit);

            var token = new ProjectileImpactToken();
            foreach (Unit target in targets)
            {
                GameManager.Instance?.sfxManager?.FireElementalProjectile(
                    Caster, target, 0.42f, ProjectilePathType.Linear,
                    ProjectileFlight.DataFor(ProjectilePathType.Linear), null,
                    token.MarkImpact, context);
            }
            yield return ProjectileFlight.WaitForImpact(token, 0.42f);

            foreach (Unit target in targets.Where(unit => unit != null && unit.isActive).ToList())
            {
                target.TakeDamage(context);
            }
        }

        /// <summary>직전 <see cref="StrikeAll"/>이 노린 대상들. 후속 부여 효과가 읽는다.</summary>
        protected List<Unit> LastTargets { get; private set; } = new();
    }

    /// <summary>
    /// 300 파멸의 심판 — 적 전체에게 위력 200 + CON×0.6의 특수·비접촉 피해.
    /// 파멸과 종말의 성기사가 같은 궁극기를 공유한다. 차이는 화형 심판의 방어 무시 폭에서 난다.
    /// </summary>
    public sealed class AswanPaladinJudgment : AswanUltimate
    {
        public AswanPaladinJudgment(UltimateCodeContext context) : base(context)
        {
            CodeName = "파멸의 심판";
            CastingDelay = 0.55f;
            Power = 200;
        }

        protected override IEnumerator Resolve()
        {
            // 위력 = 고정 200 + CON × 0.6.
            int power = 200 + Mathf.RoundToInt(Caster.GetBaseCon() * 0.6f);
            yield return StrikeAll(power, BaseEnums.PrimaryStat.CON, new List<int>
            {
                DamageTag.AllTarget, DamageTag.UltAttack,
                DamageTag.Special, DamageTag.NonContactAttack,
            });
        }
    }

    /// <summary>
    /// 302·303 화형 선고 — 적 전체에게 INT 기반 피해를 입히고 불 원소를 부여한다.
    /// 이미 불이 붙은 적에게는 패시브 '화형 선고'(362)가 부착을 화상으로 바꾼다.
    /// </summary>
    public sealed class AswanInquisitorPyre : AswanUltimate
    {
        private readonly int _power;

        public AswanInquisitorPyre(UltimateCodeContext context, int power) : base(context)
        {
            CodeName = "화형 선고";
            CastingDelay = 0.55f;
            _power = power;
            Power = power;
        }

        protected override IEnumerator Resolve()
        {
            yield return StrikeAll(_power, BaseEnums.PrimaryStat.INT, new List<int>
            {
                DamageTag.AllTarget, DamageTag.UltAttack,
                DamageTag.Special, DamageTag.NonContactAttack,
            });

            foreach (Unit target in LastTargets.Where(unit => unit != null && unit.isActive).ToList())
            {
                AswanCombat.GrantPyro(Caster, target);
            }
        }
    }

    /// <summary>
    /// 304 사령 소환 — 필드에 빈칸이 있으면 자신과 같은 사령을 하나 세운다.
    /// 빈칸이 없으면 아예 발동하지 않고 자원을 아낀다.
    /// </summary>
    public sealed class AswanWraithSummon : AswanUltimate
    {
        public AswanWraithSummon(UltimateCodeContext context) : base(context)
        {
            CodeName = "사령 소환";
            CastingDelay = 0.45f;
        }

        protected override IEnumerator Resolve()
        {
            AswanCombat.SummonWraiths(Caster, 1);
            yield break;
        }

        public override bool HasValidTarget() =>
            Caster != null && Caster.isActive && AswanCombat.HasEmptyFieldSlot(Caster);
    }

    /// <summary>
    /// 305 망자의 가호 — 아군 전체에게 INT 기반 보호막을 씌우고 사령에게는 두 배로 준다.
    ///
    /// 🔸 타락한 사제는 원안에 코드 정의가 없어 테마 축(사령 운용)에 맞춰 새로 짠 것이다.
    ///    수치를 정하면 여기와 20_codes.yaml의 설명을 함께 고치면 된다.
    /// </summary>
    public sealed class AswanFallenPriestWard : AswanUltimate
    {
        private const int ShieldPower = 50;

        public AswanFallenPriestWard(UltimateCodeContext context) : base(context)
        {
            CodeName = "망자의 가호";
            Power = ShieldPower;
        }

        protected override IEnumerator Resolve()
        {
            int shield = Mathf.Max(1, Caster.SkillDamage(ShieldPower, BaseEnums.PrimaryStat.INT));
            foreach (Unit ally in AswanCombat.AlliesIncludingSelf(Caster))
            {
                ally.AddShield(AswanCombat.IsWraith(ally) ? shield * 2 : shield, Caster);
            }
            yield break;
        }

        /// <summary>보호막은 적이 없어도 유효하다.</summary>
        public override bool HasValidTarget() => Caster != null && Caster.isActive;
    }

    /// <summary>
    /// 306 재생의 격류 — 적 전체에게 INT×1.5의 피해를 입히고 다음 행동을 절반만큼 늦춘다.
    ///
    /// 🔸 원안은 '행동 게이지를 50% 증가시킨다'이지만, 대상이 <b>적</b>이므로 그대로 읽으면
    ///    상대를 도와주는 효과가 된다. 이시스의 '사막의 격류'와 같은 방향(50% 지연)으로 맞췄다.
    /// </summary>
    public sealed class OsirisRebirthFlood : AswanUltimate
    {
        public OsirisRebirthFlood(UltimateCodeContext context) : base(context)
        {
            CodeName = "재생의 격류";
            CastingDelay = 0.55f;
            Power = 150;
        }

        protected override IEnumerator Resolve()
        {
            yield return StrikeAll(150, BaseEnums.PrimaryStat.INT, new List<int>
            {
                DamageTag.AllTarget, DamageTag.UltAttack,
                DamageTag.Special, DamageTag.NonContactAttack,
            });

            foreach (Unit target in LastTargets.Where(unit => unit != null && unit.isActive).ToList())
            {
                GameManager.Instance?.ActionScheduler?.DelayAction(target, 0.5f);
            }
        }
    }

    /// <summary>
    /// 307 태양의 심판 — '승천' 스택 × INT×0.6을 적 전체에게 먹이고 스택을 0으로 되돌린다.
    /// 스택이 없으면 발동하지 않는다. 쌓을수록 한 방이 커지는 대신 맞으면 줄어드는 구조다.
    /// </summary>
    public sealed class AmunRaSolarJudgment : AswanUltimate
    {
        private const int PowerPerStack = 60;

        public AmunRaSolarJudgment(UltimateCodeContext context) : base(context)
        {
            CodeName = "태양의 심판";
            CastingDelay = 0.6f;
            Power = PowerPerStack;
        }

        protected override IEnumerator Resolve()
        {
            int stacks = Caster.GetCombatResource(AswanCombat.AscensionResource);
            if (stacks <= 0) yield break;

            yield return StrikeAll(PowerPerStack * stacks, BaseEnums.PrimaryStat.INT, new List<int>
            {
                DamageTag.AllTarget, DamageTag.UltAttack,
                DamageTag.Special, DamageTag.NonContactAttack,
            });

            Caster.AddCombatResource(AswanCombat.AscensionResource, -stacks);
            Caster.RefreshAttributes();
            Debug.Log($"[태양의 심판] 승천 {stacks}중첩을 모두 소비했습니다.");
        }

        public override bool HasValidTarget() => base.HasValidTarget() &&
            Caster.GetCombatResource(AswanCombat.AscensionResource) > 0;
    }
}
