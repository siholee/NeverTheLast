using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Entities;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>공통 골격 — 시전 지연 후 한 번 해결하고 끝나는 궁극기.</summary>
    public abstract class SimpleUltimate : UltimateCode
    {
        protected SimpleUltimate(UltimateCodeContext context, string name, float cooldown, float delay)
            : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = name;
            Cooldown = cooldown;
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

        protected List<Unit> Enemies() => global::Target.GetAllEnemies(Caster)
            .Where(unit => unit != null && unit.isActive && !unit.IsUntargetable).ToList();

        public override void StopCode()
        {
            if (Caster == null) return;
            Caster.ultimateCooldown = Cooldown;
            Caster.isCasting = false;
        }

        public override bool HasValidTarget() => Caster != null && Caster.isActive;
    }

    /// <summary>
    /// 야마 U — 언젠가 다다를 마지막.
    /// 적 전체를 베어 DEX 기반 위력 60의 피해를 주고 번개 원소를 부여한다.
    /// 이후 대상이 지닌 지속피해 디버프를 즉시 정산해 `INT × 0.8`배만큼 추가로 터뜨린다.
    /// (붕괴: 스타레일 카프카 궁극기와 같은 매커니즘)
    /// </summary>
    public sealed class YamaFinalArrival : SimpleUltimate
    {
        private const int SlashPower = 60;
        private const float DetonateRatio = 0.8f;

        public YamaFinalArrival(UltimateCodeContext context)
            : base(context, "언젠가 다다를 마지막", 8f, 0.5f) { }

        protected override void Resolve()
        {
            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
            int slash = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(SlashPower, BaseEnums.PrimaryStat.DEX) * critMultiplier));

            var tags = new List<int>
            {
                DamageTag.AllTarget, DamageTag.UltAttack,
                DamageTag.Special, DamageTag.ContactAttack, DamageTag.Slash,
            };

            foreach (Unit target in Enemies())
            {
                target.TakeDamage(new DamageContext(Caster, slash, BaseEnums.CodeType.Ultimate, tags, isCrit));
                if (target == null || !target.isActive) continue;

                target.GrantCombatElement(BaseEnums.UnitElement.Electro, Unit.CommonElementAuraDuration, Caster);

                // 지속피해 즉시 정산 — 대상이 받고 있던 초당 피해를 INT 비례로 터뜨린다.
                int dotPerSecond = target.GetEstimatedDamageOverTimePerSecond();
                if (dotPerSecond <= 0) continue;

                int detonate = Mathf.Max(1, Mathf.RoundToInt(
                    dotPerSecond * Caster.GetBaseInt() * DetonateRatio * 0.01f));
                target.TakeDamage(new DamageContext(
                    Caster, detonate, BaseEnums.CodeType.Ultimate,
                    new List<int> { DamageTag.SingleTarget, DamageTag.Special }));
            }
        }
    }

    /// <summary>아그니 U — 백화(白火). 적 전체에게 INT 기반 위력 65 + 불 원소 부여.</summary>
    public sealed class AgniWhiteFlame : SimpleUltimate
    {
        private const int FlamePower = 65;

        public AgniWhiteFlame(UltimateCodeContext context)
            : base(context, "백화", 8f, 0.5f) { }

        protected override void Resolve()
        {
            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(FlamePower, BaseEnums.PrimaryStat.INT) * critMultiplier));

            var tags = new List<int>
            {
                DamageTag.AllTarget, DamageTag.UltAttack,
                DamageTag.Special, DamageTag.NonContactAttack,
            };

            foreach (Unit target in Enemies())
            {
                target.TakeDamage(new DamageContext(Caster, damage, BaseEnums.CodeType.Ultimate, tags, isCrit));
                if (target != null && target.isActive)
                {
                    target.GrantCombatElement(BaseEnums.UnitElement.Pyro, Unit.CommonElementAuraDuration, Caster);
                }
            }
        }
    }

    /// <summary>
    /// 인드라 U — 천벌.
    /// 단일 대상에게 `INT × 레벨%` 피해를 주고, 계수를 20%p씩 깎으며 다른 적에게 튕긴다.
    /// 계수가 0이 되거나 남은 적이 없으면 멈춘다.
    /// </summary>
    public sealed class IndraThunderbolt : SimpleUltimate
    {
        public IndraThunderbolt(UltimateCodeContext context)
            : base(context, "천벌", 8f, 0.4f) { }

        protected override void Resolve()
        {
            List<Unit> pool = Enemies();
            if (pool.Count == 0) return;

            // 1차 계수 = 인드라의 레벨(%) → 위력으로 그대로 읽는다.
            int power = Mathf.Max(1, Caster.Level);
            int decay = Mathf.Max(1, Mathf.RoundToInt(power * 0.2f));

            Unit target = pool[Random.Range(0, pool.Count)];
            var hit = new HashSet<Unit>();

            var tags = new List<int>
            {
                DamageTag.SingleTarget, DamageTag.UltAttack,
                DamageTag.Special, DamageTag.NonContactAttack,
            };

            while (power > 0 && target != null && target.isActive)
            {
                bool isCrit = Random.value <= Caster.CritChanceCurr;
                float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
                int damage = Mathf.Max(1, Mathf.RoundToInt(
                    Caster.SkillDamage(power, BaseEnums.PrimaryStat.INT) * critMultiplier));
                target.TakeDamage(new DamageContext(Caster, damage, BaseEnums.CodeType.Ultimate, tags, isCrit));
                hit.Add(target);

                power -= decay;
                if (power <= 0) break;

                List<Unit> remaining = Enemies().Where(unit => !hit.Contains(unit)).ToList();
                if (remaining.Count == 0) break;
                target = remaining[Random.Range(0, remaining.Count)];
            }
        }
    }

    /// <summary>
    /// 바유 U — 남풍.
    /// 적 전체에게 DEX 기반 위력 50 + 바람 원소 부여 + 방어력 20% 감소.
    /// 이후 6초간 매초 CON 기반 위력 25의 잔풍 피해를 남긴다.
    /// </summary>
    public sealed class VayuSouthWind : SimpleUltimate
    {
        private const int BurstPower = 50;
        private const int LingerPower = 25;
        private const float LingerDuration = 6f;
        private const int ArmorShredStatusId = 6100;

        public VayuSouthWind(UltimateCodeContext context)
            : base(context, "남풍", 8f, 0.5f) { }

        protected override void Resolve()
        {
            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
            int burst = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(BurstPower, BaseEnums.PrimaryStat.DEX) * critMultiplier));

            var tags = new List<int>
            {
                DamageTag.AllTarget, DamageTag.UltAttack,
                DamageTag.Special, DamageTag.NonContactAttack,
            };

            int lingerPerSecond = Mathf.Max(1, Caster.SkillDamage(LingerPower, BaseEnums.PrimaryStat.CON));

            foreach (Unit target in Enemies())
            {
                target.TakeDamage(new DamageContext(Caster, burst, BaseEnums.CodeType.Ultimate, tags, isCrit));
                if (target == null || !target.isActive) continue;

                target.GrantCombatElement(BaseEnums.UnitElement.Anemo, Unit.CommonElementAuraDuration, Caster);

                target.AddStatus(Effects.Buffs.BuffStatus.Create(
                    ArmorShredStatusId, $"vayu_shred_{Caster.GetEntityId()}", "남풍 — 방어 붕괴",
                    Caster, target, new VayuArmorShredEffect(0.8f),
                    duration: LingerDuration,
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    category: BaseEnums.StatusCategory.Negative,
                    isBeneficial: false,
                    description: "방어력이 20% 감소합니다."));

                target.AddStatus(Effects.Buffs.BuffStatus.Create(
                    ArmorShredStatusId + 1, $"vayu_linger_{Caster.GetEntityId()}", "남풍 — 잔풍",
                    Caster, target, new Effects.Negative.ReactionDotEffect(lingerPerSecond),
                    duration: LingerDuration,
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    category: BaseEnums.StatusCategory.Negative,
                    isBeneficial: false,
                    description: $"매초 {lingerPerSecond}의 바람 피해를 받습니다."));
            }
        }
    }

    /// <summary>남풍의 방어 감소. 대상이 받을 때 자기 방어력을 깎는다.</summary>
    internal sealed class VayuArmorShredEffect : Effects.Base.BaseEffect
    {
        private readonly float _multiplier;
        public VayuArmorShredEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;

        public override float OwnedDefenseStatMultiplierModifier(Unit unit, DamageContext context)
            => unit == Target ? _multiplier : 1f;
    }
}
