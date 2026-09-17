using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using Effects.Negative;
using Entities;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>천공 전선 단일 대상 일반행동의 공용 뼈대. 위력·스탯·태그만 다르다.</summary>
    public abstract class SkySingleNormal : BaseNormalCode
    {
        private readonly BaseEnums.PrimaryStat _stat;
        private readonly bool _contact;

        protected SkySingleNormal(NormalCodeContext context, string name, int power,
            BaseEnums.PrimaryStat stat, bool contact) : base(context)
        {
            CodeName = name;
            Power = power;
            _stat = stat;
            _contact = contact;
            CastingDelay = 0.35f;
            CodeTags = contact
                ? new List<int> { DamageTag.Physical, DamageTag.ContactAttack }
                : new List<int> { DamageTag.Special, DamageTag.NonContactAttack };
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(Caster.SkillDamage(CurrentPower, _stat) * critMultiplier));

        protected override List<int> GetDamageTags() => _contact
            ? new List<int> { DamageTag.SingleTarget, DamageTag.NormalAttack, DamageTag.Physical, DamageTag.ContactAttack }
            : new List<int> { DamageTag.SingleTarget, DamageTag.NormalAttack, DamageTag.Special, DamageTag.NonContactAttack };
    }

    /// <summary>공허의 등불나방 N — 인분탄. INT 위력 45, 원소를 부착하지 않는다.</summary>
    public sealed class SkyLanternScaleShot : SkySingleNormal
    {
        public SkyLanternScaleShot(NormalCodeContext context)
            : base(context, "인분탄", 45, BaseEnums.PrimaryStat.INT, contact: false) { }
    }

    /// <summary>
    /// 공허의 나비 N — 불씨 인분. INT 위력 40.
    /// 타오르는 인분(1731)을 배웠다면 이미 불을 지닌 대상에게 화상 2턴을 시도한다(CON 대결).
    /// </summary>
    public sealed class SkyEmberScales : SkySingleNormal
    {
        public SkyEmberScales(NormalCodeContext context)
            : base(context, "불씨 인분", 40, BaseEnums.PrimaryStat.INT, contact: false) { }

        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            if (target == null || !target.isActive || context.ResolvedDamage <= 0) return;
            if (!Caster.HasLearnedPassiveCode(SkyFrontlineCodeIds.BurningScales)) return;
            if (!target.HasCombatElement(BaseEnums.UnitElement.Pyro)) return;
            ElementalReaction.TryApplyBurn(Caster, target, 2);
        }
    }

    /// <summary>공허의 포식자 N — 포식의 발톱. STR 위력 110, 접촉 물리.</summary>
    public sealed class SkyPredatorTalon : SkySingleNormal
    {
        public SkyPredatorTalon(NormalCodeContext context)
            : base(context, "포식의 발톱", 110, BaseEnums.PrimaryStat.STR, contact: true) { }
    }

    /// <summary>천공의 포식자 N — 찢는 부리. 후열에서 전열 너머를 치므로 비접촉이다.</summary>
    public sealed class SkyTearingBeak : SkySingleNormal
    {
        public SkyTearingBeak(NormalCodeContext context)
            : base(context, "찢는 부리", 110, BaseEnums.PrimaryStat.STR, contact: false) { }
    }

    /// <summary>
    /// 종말의 포식자 N — 종말의 발톱. STR 위력 110 한 번.
    /// 쌍발톱(1752)을 배우면 같은 대상에게 위력 60을 두 번으로 바뀐다 — 무적을 두 번 깎는다.
    /// </summary>
    public sealed class SkyApocalypseTalon : SkySingleNormal
    {
        private const int TwinPower = 60;

        public SkyApocalypseTalon(NormalCodeContext context)
            : base(context, "종말의 발톱", 110, BaseEnums.PrimaryStat.STR, contact: true) { }

        private bool IsTwin => Caster != null && Caster.HasLearnedPassiveCode(SkyFrontlineCodeIds.TwinTalons);

        protected override int CalculateDamage(float critMultiplier)
            => IsTwin
                ? Mathf.Max(1, Mathf.RoundToInt(Caster.SkillDamage(TwinPower, BaseEnums.PrimaryStat.STR) * critMultiplier))
                : base.CalculateDamage(critMultiplier);

        protected override IEnumerator FireProjectile(List<Unit> targets, float delay, DamageContext context)
        {
            yield return base.FireProjectile(targets, delay, context);
            if (!IsTwin) yield break;

            List<Unit> alive = targets.Where(target => target != null && target.isActive).ToList();
            if (alive.Count > 0) yield return base.FireProjectile(alive, delay, context);
        }
    }

    /// <summary>
    /// 공허의 고치 N — 생명 맥동. 공격하지 않고 연결 대상의 최대 체력 2%(종말전 1.5%)를 치유한다.
    /// 무적이 다 벗겨져도 살아 있으면 계속 치유한다. 끊는 방법은 처치뿐이다.
    /// </summary>
    public sealed class SkyLifePulse : BaseNormalCode
    {
        public SkyLifePulse(NormalCodeContext context) : base(context)
        {
            CodeName = "생명 맥동";
            Power = 0;
            CastingDelay = 0.35f;
            CodeTags = new List<int>();
        }

        protected override IEnumerator SkillCoroutine()
        {
            bool cast = false;
            yield return WaitForCast(result => cast = result);
            if (!cast)
            {
                StopCode();
                yield break;
            }

            CocoonCoreEffect core = CocoonCoreEffect.Of(Caster);
            Unit link = core?.Link;
            if (link != null) SkyFrontline.HealRatio(Caster, link, core.HealRatio);

            NotifyActionResolved();
            StopCode();
        }

        public override bool HasValidTarget() => Caster != null && Caster.isActive && CocoonCoreEffect.Of(Caster)?.Link != null;
    }

    /// <summary>
    /// 허기 결정 · 파열 비늘 N — 카운트다운. 피해를 주지 않는다.
    ///
    /// 자기 턴 시작의 지속피해가 먼저 처리되므로, 발동 직전 턴의 틱으로 쓰러뜨리면 발동하지 않는다.
    /// 0이 되면 흡수 또는 파열을 일으키고 <b>처치가 아닌 퇴장</b>을 한다.
    /// </summary>
    public sealed class SkyCrystalCountdown : BaseNormalCode
    {
        private const float HungerHealRatio = 0.10f;
        private const float GreedHealRatio = 0.15f;
        private const float RuptureRatio = 0.35f;

        public SkyCrystalCountdown(NormalCodeContext context) : base(context)
        {
            CodeName = "카운트다운";
            Power = 0;
            CastingDelay = 0.25f;
            CodeTags = new List<int>();
        }

        protected override IEnumerator SkillCoroutine()
        {
            bool cast = false;
            yield return WaitForCast(result => cast = result);
            if (!cast)
            {
                StopCode();
                yield break;
            }

            int remaining = Caster.AddCombatResource(SkyFrontlineResources.Countdown, -1);
            NotifyActionResolved();

            if (remaining <= 0 && Caster.isActive)
            {
                if (Caster.ID == SkyFrontlineIds.RuptureScale) Rupture();
                else Absorb();
                Caster.Withdraw();
            }

            StopCode();
        }

        public override bool HasValidTarget() => Caster != null && Caster.isActive;

        /// <summary>
        /// 흡수 — 본체가 삼켜 최대 체력 10%(탐욕 15%)를 회복하고 포만을 하나 얻는다.
        /// 본체 CON 보너스는 걷어 낸다. 고정 비율로 읽혀야 흡수를 몇 번 허용했는지 가늠할 수 있다.
        /// 받는 쪽의 치유 감소는 그대로 받는다 — 치유 감소 디버프가 흡수를 견제하는 수단으로 남는다.
        /// </summary>
        private void Absorb()
        {
            Unit summoner = SkyFrontline.SummonerOf(Caster);
            if (summoner == null || !summoner.isActive) return;

            float ratio = summoner.HasLearnedPassiveCode(SkyFrontlineCodeIds.Greed) ? GreedHealRatio : HungerHealRatio;
            float conBonus = 1f + Mathf.Max(0f, summoner.HealingBonusCurr);
            int amount = Mathf.Max(1, Mathf.RoundToInt(summoner.HpMax * ratio / conBonus));
            summoner.ModifyHp(summoner.HpCurr + amount, Caster);
            summoner.AddCombatResource(SkyFrontlineResources.Satiety, 1);
            Debug.Log($"[허기 결정] {summoner.UnitName}이(가) 결정을 삼켰습니다. 포만 " +
                      $"{summoner.GetCombatResource(SkyFrontlineResources.Satiety)}");
        }

        /// <summary>파열 — 상대 진영 필드 전원에게 각자 최대 체력 35%. 방어력·피해 배율을 무시하고 보호막은 흡수한다.</summary>
        private void Rupture()
        {
            List<Unit> victims = CombatTargets.AliveEnemies(Caster);
            foreach (Unit victim in victims)
            {
                int damage = Mathf.Max(1, Mathf.RoundToInt(victim.HpMax * RuptureRatio));
                victim.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Normal,
                    new List<int>
                    {
                        DamageTag.AllTarget, DamageTag.Special, DamageTag.NonContactAttack, DamageTag.FixedRatioBurst,
                    }));
            }
            Debug.Log($"[파열 비늘] {Caster.UnitName}이(가) 터져 {victims.Count}명에게 피해를 줬습니다.");
        }
    }
}
