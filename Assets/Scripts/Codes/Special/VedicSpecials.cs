using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Effects.Negative;
using Entities;
using UnityEngine;

namespace Codes.Special
{
    /// <summary>
    /// 특수행동 공통 골격 — 시전 지연 뒤 한 번 해결하고 끝난다.
    /// 궁극기의 <c>SimpleUltimate</c>와 같은 모양이되 자원을 쓰지 않는다.
    /// 특수행동은 인드라의 궁극기가 열어 주는 것이라 스스로 값을 치르지 않는다.
    /// </summary>
    public abstract class SimpleSpecial : SpecialCode
    {
        protected SimpleSpecial(SpecialCodeContext context, string name, float delay = 0.35f)
            : base(context)
        {
            CodeName = name;
            CastingDelay = delay;
        }

        public override void CastCode()
        {
            if (!HasValidTarget()) return;
            Caster.isCasting = true;
            CurrSkillCoroutine = Caster.StartCoroutine(SkillCoroutine());
        }

        protected override IEnumerator SkillCoroutine()
        {
            float elapsed = 0f;
            while (elapsed < CastingDelay)
            {
                if (Caster == null || !Caster.isActive || Caster.isControlled) { StopCode(); yield break; }
                elapsed += Time.deltaTime;
                yield return null;
            }

            Resolve();
            StopCode();
        }

        protected abstract void Resolve();

        protected List<Unit> Enemies() => CombatTargets.AliveEnemies(Caster)
            .Where(unit => !unit.IsUntargetable).ToList();

        protected List<Unit> Allies() => CombatTargets.AliveAlliesIncludingSelf(Caster);
    }

    // ══════════════════════════════════════════════════════════════
    // 찬드라 — 소마의 잔
    // ══════════════════════════════════════════════════════════════

    /// <summary>찬드라 SP — 아군 전체에게 CON 위력 150 방어막과 INT의 절반만큼 마나를 준다.</summary>
    public sealed class ChandraAegis : SimpleSpecial
    {
        private const int ShieldPower = 150;
        private const float ManaRatio = 0.5f;

        public ChandraAegis(SpecialCodeContext context) : base(context, "소마의 잔") { }

        protected override void Resolve()
        {
            int shield = Mathf.Max(1, Caster.SkillDamage(ShieldPower, BaseEnums.PrimaryStat.CON));
            int mana = Mathf.Max(1, Mathf.RoundToInt(Caster.GetBaseInt() * ManaRatio));

            foreach (Unit ally in Allies())
            {
                ally.AddShield(shield, Caster);
                ally.AddUltimateResource(mana);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 야마 — 죽음의 순회
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 야마 SP — DEX 위력 120의 단일 타격을 <b>이번에 특수행동을 하는 유닛 수</b>만큼 튕긴다.
    ///
    /// 로카팔라를 많이 세울수록 타수가 늘어나는 것이 이 행동의 전부다.
    /// 인드라가 문을 열어야만 나가므로, 한 번의 궁극기가 편성 전체의 화력으로 환산된다.
    /// </summary>
    public sealed class YamaBounceStrike : SimpleSpecial
    {
        private const int StrikePower = 120;
        private const float ElectroChance = 0.30f;

        public YamaBounceStrike(SpecialCodeContext context) : base(context, "죽음의 순회") { }

        protected override void Resolve()
        {
            List<Unit> pool = Enemies();
            if (pool.Count == 0) return;

            int bounces = Mathf.Max(1, Combat.SpecialAction.ParticipantCount(Caster));
            var tags = new List<int>
            {
                DamageTag.SingleTarget, DamageTag.Special, DamageTag.NonContactAttack,
            };

            for (int i = 0; i < bounces; i++)
            {
                pool = Enemies();
                if (pool.Count == 0) return;

                Unit target = pool[Random.Range(0, pool.Count)];
                bool isCrit = Random.value <= Caster.CritChanceCurr;
                float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
                int damage = Mathf.Max(1, Mathf.RoundToInt(
                    Caster.SkillDamage(StrikePower, BaseEnums.PrimaryStat.DEX) * critMultiplier));

                target.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Special, tags, isCrit));

                if (target.isActive && Random.value <= ElectroChance)
                {
                    target.GrantCombatElement(
                        BaseEnums.UnitElement.Electro, Unit.CommonElementAuraDuration, Caster);
                }
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 아그니 — 겁화
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 아그니 SP — 적 전체에게 INT 위력 80. 3턴간 특수 피해에 취약해지고,
    /// 이미 불을 두른 대상에게는 화상까지 얹는다.
    /// </summary>
    public sealed class AgniConflagration : SimpleSpecial
    {
        private const int BlastPower = 80;
        private const int VulnerabilityTurns = 3;
        private const int BurnTurns = 3;
        private const float Vulnerability = 1.5f;
        private const int StatusId = 6540;

        public AgniConflagration(SpecialCodeContext context) : base(context, "겁화") { }

        protected override void Resolve()
        {
            List<Unit> targets = Enemies();
            if (targets.Count == 0) return;

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(BlastPower, BaseEnums.PrimaryStat.INT) * critMultiplier));

            var tags = new List<int>
            {
                DamageTag.AllTarget, DamageTag.Special, DamageTag.NonContactAttack,
            };

            foreach (Unit target in targets)
            {
                // 취약을 피해보다 먼저 걸어야 이번 일격부터 값을 한다.
                target.AddStatus(BuffStatus.Create(
                    StatusId, "agni_conflagration", CodeName, Caster, target,
                    new TaggedVulnerabilityEffect(DamageTag.Special, Vulnerability),
                    duration: VulnerabilityTurns,
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    category: BaseEnums.StatusCategory.Negative,
                    description: "특수 공격에게 받는 피해가 50% 증가합니다."));

                // 불을 이미 두른 대상만 화상까지 간다. 아그니가 직접 불을 붙이지는 않는다.
                bool burning = target.HasCombatElement(BaseEnums.UnitElement.Pyro);

                target.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Special, tags, isCrit));

                if (burning && target.isActive) ElementalReaction.TryApplyBurn(Caster, target, BurnTurns);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 인드라 — 천벌 (옛 궁극기)
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 인드라 SP — 천벌. 단일 대상에게 INT의 [레벨]%를 꽂고, 계수를 20%p씩 깎으며 튕긴다.
    ///
    /// 예전에는 인드라의 궁극기였다. 궁극기 자리는 편성 전체의 특수행동을 여는
    /// <c>신들의 왕</c>이 가져갔고, 이 화력은 그 문을 여는 순간 자신도 함께 나가는 몫이 됐다.
    /// </summary>
    public sealed class IndraThunderbolt : SimpleSpecial
    {
        public IndraThunderbolt(SpecialCodeContext context) : base(context, "천벌", 0.4f) { }

        protected override void Resolve()
        {
            List<Unit> pool = Enemies();
            if (pool.Count == 0) return;

            int power = Mathf.Max(1, Caster.Level);
            int decay = Mathf.Max(1, Mathf.RoundToInt(power * 0.2f));

            Unit target = pool[Random.Range(0, pool.Count)];
            var hit = new HashSet<Unit>();
            var tags = new List<int>
            {
                DamageTag.SingleTarget, DamageTag.Special, DamageTag.NonContactAttack,
            };

            while (power > 0 && target != null && target.isActive)
            {
                bool isCrit = Random.value <= Caster.CritChanceCurr;
                float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
                int damage = Mathf.Max(1, Mathf.RoundToInt(
                    Caster.SkillDamage(power, BaseEnums.PrimaryStat.INT) * critMultiplier));
                target.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Special, tags, isCrit));
                hit.Add(target);

                power -= decay;
                if (power <= 0) break;

                List<Unit> remaining = Enemies().Where(unit => !hit.Contains(unit)).ToList();
                if (remaining.Count == 0) break;
                target = remaining[Random.Range(0, remaining.Count)];
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 바유 — 선봉의 바람
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 바유 SP — 선봉의 바람. <b>개방된 특수행동 가운데 가장 먼저</b> 나가서
    /// 아군 전체에게 올스탯 +10%를 두른다.
    ///
    /// 버프는 <b>이번 개방이 끝날 때</b> 걷힌다. 턴으로 세지 않는 이유는 특수행동이 턴을
    /// 쓰지 않기 때문이다 — 몇 턴이라고 적으면 개방이 끝난 뒤에도 남는다.
    /// 마지막 한 명의 행동까지 살아 있어야 하므로, 걷는 시점은 <b>실제 해결</b> 기준이다
    /// (<see cref="Combat.SpecialAction.NotifyResolved"/>).
    /// </summary>
    public sealed class VayuVanguardWind : SimpleSpecial
    {
        private const int StatusId = 6543;
        private const float Bonus = 1.10f;
        private const string Key = "vayu_vanguard_wind";

        public VayuVanguardWind(SpecialCodeContext context) : base(context, "선봉의 바람", 0.25f) { }

        /// <summary>남들보다 먼저 나가야 값을 한다.</summary>
        public override int OpenOrder => -1;

        protected override void Resolve()
        {
            var blessed = new List<Unit>();
            foreach (Unit ally in Allies())
            {
                foreach (BaseEnums.PrimaryStat stat in AllStats)
                {
                    ally.AddStatus(BuffStatus.Create(
                        StatusId + (int)stat, $"{Key}_{stat}", CodeName, Caster, ally,
                        new PrimaryStatMultiplierEffect(Bonus, stat),
                        stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                        isBeneficial: true,
                        description: "특수행동이 끝날 때까지 올스탯이 10% 증가합니다."));
                }
                blessed.Add(ally);
            }

            Combat.SpecialAction.RegisterBatchCleanup(() =>
            {
                foreach (Unit ally in blessed)
                {
                    if (ally == null) continue;
                    foreach (BaseEnums.PrimaryStat stat in AllStats) ally.RemoveStatusByKey($"{Key}_{stat}");
                }
            });
        }

        private static readonly BaseEnums.PrimaryStat[] AllStats =
        {
            BaseEnums.PrimaryStat.STR, BaseEnums.PrimaryStat.DEX, BaseEnums.PrimaryStat.CON,
            BaseEnums.PrimaryStat.INT, BaseEnums.PrimaryStat.LUK,
        };
    }

    // ══════════════════════════════════════════════════════════════
    // 쿠베라 — 골드 러쉬
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 쿠베라 SP — 골드 러쉬. 적 전체에게 STR과 CON을 함께 실은 접촉 타격을 넣고
    /// 3턴간 물리에 취약하게 만든다. 쓸 때마다 이번 전투의 골드 획득이 5%씩 쌓인다.
    /// </summary>
    public sealed class KuberaGoldRush : SimpleSpecial
    {
        private const int StrPower = 60;
        private const int ConPower = 60;
        private const int VulnerabilityTurns = 3;
        private const float Vulnerability = 1.5f;
        private const int StatusId = 6541;

        /// <summary>한 번 쓸 때마다 쌓이는 골드 배율. 중첩된다.</summary>
        public const float GoldBonusPerUse = 0.05f;

        public KuberaGoldRush(SpecialCodeContext context) : base(context, "골드 러쉬") { }

        protected override void Resolve()
        {
            Combat.SpecialAction.AddGoldRushStack(Caster);

            List<Unit> targets = Enemies();
            if (targets.Count == 0) return;

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                (Caster.SkillDamage(StrPower, BaseEnums.PrimaryStat.STR) +
                 Caster.SkillDamage(ConPower, BaseEnums.PrimaryStat.CON)) * critMultiplier));

            var tags = new List<int>
            {
                DamageTag.AllTarget, DamageTag.Physical, DamageTag.ContactAttack,
            };

            foreach (Unit target in targets)
            {
                target.AddStatus(BuffStatus.Create(
                    StatusId, "kubera_gold_rush", CodeName, Caster, target,
                    new TaggedVulnerabilityEffect(DamageTag.Physical, Vulnerability),
                    duration: VulnerabilityTurns,
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    category: BaseEnums.StatusCategory.Negative,
                    description: "물리 공격에게 받는 피해가 50% 증가합니다."));

                target.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Special, tags, isCrit));
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 수리야 — 일천
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 수리야 SP — 일천. <b>중첩을 태우지 않고</b> 최대 중첩짜리 라비를 그대로 쏜다.
    ///
    /// 즉 100 ÷ 10 = 10발이 고정이다. 평소의 라비가 "모은 만큼 쏘고 비우는" 것이라면,
    /// 이쪽은 모아 둔 것을 지키면서 한 번 더 쏘는 셈이라 인드라의 개방이 곧 두 배의 값이 된다.
    /// 이 공격은 <b>특수행동으로만 간주</b>한다 — 일반행동 카운터에는 잡히지 않는다.
    /// </summary>
    public sealed class SuryaIlcheon : SimpleSpecial
    {
        private const int BouncePower = 80;

        public SuryaIlcheon(SpecialCodeContext context) : base(context, "일천", 0.4f) { }

        protected override void Resolve()
        {
            int bounces = Codes.Passive.SuryaSavitr.MaxStacks / Codes.Normal.SuryaRavi.StacksPerBounce;
            var tags = new List<int>
            {
                DamageTag.SingleTarget, DamageTag.Special, DamageTag.NonContactAttack,
            };

            for (int i = 0; i < bounces; i++)
            {
                List<Unit> pool = Enemies();
                if (pool.Count == 0) return;

                Unit target = pool[Random.Range(0, pool.Count)];
                bool isCrit = Random.value <= Caster.CritChanceCurr;
                int damage = Mathf.Max(1, Mathf.RoundToInt(
                    Caster.SkillDamage(BouncePower, BaseEnums.PrimaryStat.CON) *
                    (isCrit ? Caster.CritMultiplierCurr : 1f)));

                target.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Special, tags, isCrit));
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 바루나 — 정화의 밀물
    // ══════════════════════════════════════════════════════════════

    /// <summary>바루나 SP — 아군 전체를 CON 위력 150만큼 치유하고 해로운 효과를 모두 씻는다.</summary>
    public sealed class VarunaTide : SimpleSpecial
    {
        private const int HealPower = 150;

        public VarunaTide(SpecialCodeContext context) : base(context, "정화의 밀물") { }

        protected override void Resolve()
        {
            int heal = Mathf.Max(1, Caster.SkillDamage(HealPower, BaseEnums.PrimaryStat.CON));

            foreach (Unit ally in Allies())
            {
                ally.ModifyHp(ally.HpCurr + heal, Caster);
                ally.RemoveAllNegativeStatuses();
            }
        }
    }
}
