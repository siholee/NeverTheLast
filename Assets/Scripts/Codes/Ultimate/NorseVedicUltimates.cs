using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Buffs;
using Effects.Negative;
using Entities;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>신규 캐릭터군이 쓰는 상태 ID 대역.</summary>
    public static class NewWaveStatusIds
    {
        public const int FreyaHarvest = 5300;
        public const int VarunaMakara = 5302;
    }

    /// <summary>
    /// 프레이아 U — 풍요의 산물.
    ///
    /// 이번 전투에서 프레이아가 실제로 채운 체력(순수치유량)을 자원으로 쓴다.
    /// `순수치유량 ÷ (프레이아 최대 체력 × 10%)`만큼 아군 전체에게 STR을 7초간 주고 기록을 비운다.
    /// 분모가 최대 체력에 연동되므로 레벨이 올라도 체감 배율이 유지된다.
    /// </summary>
    public sealed class FreyaHarvest : UltimateCode
    {
        private const int Duration = 4;   // 7초 → 4턴
        private const float HpRatioPerPoint = 0.1f;
        private const int MaxBonus = 25;

        public FreyaHarvest(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = "풍요의 산물";
            Power = 0;   // 피해를 주지 않는 궁극기
            Cooldown = 4;
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
            float elapsed = 0f;
            while (elapsed < CastingDelay)
            {
                if (Caster == null || !Caster.isActive || Caster.isControlled) { StopCode(); yield break; }
                elapsed += Time.deltaTime;
                yield return null;
            }

            int divisor = Mathf.Max(1, Mathf.RoundToInt(Caster.HpMax * HpRatioPerPoint));
            int bonus = Mathf.Clamp(Caster.RoundEffectiveHealingDone / divisor, 0, MaxBonus);
            Caster.ResetEffectiveHealingRecord();

            if (bonus > 0)
            {
                foreach (Unit ally in Allies())
                {
                    ally.AddStatus(BuffStatus.Create(
                        NewWaveStatusIds.FreyaHarvest, "freya_harvest", CodeName,
                        Caster, ally, new PrimaryStatBonusBuffEffect(BaseEnums.PrimaryStat.STR, bonus),
                        duration: Duration,
                        stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                        isBeneficial: true,
                        description: $"STR이 {bonus} 증가합니다."));
                }
            }

            Debug.Log($"[풍요의 산물] 순수치유량 정산 → 아군 전체 STR +{bonus} ({Duration}턴)");
            StopCode();
        }

        private List<Unit> Allies()
        {
            List<Unit> allies = global::Target.GetAllAllies(Caster)
                .Where(unit => unit != null && unit.isActive)
                .ToList();
            if (!allies.Contains(Caster)) allies.Add(Caster);
            return allies;
        }

        public override void StopCode()
        {
            if (Caster == null) return;
            Caster.ultimateCooldown = Cooldown;
            Caster.isCasting = false;
        }

        public override bool HasValidTarget() => Caster != null && Caster.isActive;
    }

    /// <summary>
    /// 로키 U — 발드르의 살해자.
    /// 최대 체력이 가장 높은 적 하나에게 STR 위력 120 + 받는 치유량 −50%(6초).
    /// </summary>
    public sealed class LokiBaldrSlayer : SimpleUltimate
    {
        private const int SlashPower = 120;
        private const int HealCutDuration = 3;   // 6초 → 3턴

        public LokiBaldrSlayer(UltimateCodeContext context)
            : base(context, "발드르의 살해자", 4, 0.5f) { Power = SlashPower; }

        protected override void Resolve()
        {
            Unit target = Enemies().OrderByDescending(enemy => enemy.HpMax).FirstOrDefault();
            if (target == null) return;

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(SlashPower, BaseEnums.PrimaryStat.STR) * critMultiplier));

            target.TakeDamage(new DamageContext(Caster, damage, BaseEnums.CodeType.Ultimate, new List<int>
            {
                DamageTag.SingleTarget, DamageTag.UltAttack,
                DamageTag.NonContactAttack, DamageTag.Physical,
            }, isCrit));

            if (target.isActive)
            {
                // 공용 치유량 감소 상태를 쓴다. 중첩되지 않고 남은 시간이 긴 쪽만 남는다.
                HealingReductionStatus.Apply(target, Caster, HealCutDuration, CodeName);
            }
        }
    }

    /// <summary>스카디 U — 고드름 스파이크. 적 전체에 INT 위력 80 + 얼음 원소 부착.</summary>
    public sealed class SkadiIcicleSpike : SimpleUltimate
    {
        private const int SpikePower = 80;

        public SkadiIcicleSpike(UltimateCodeContext context)
            : base(context, "고드름 스파이크", 4, 0.6f) { Power = SpikePower; }

        protected override void Resolve()
        {
            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(SpikePower, BaseEnums.PrimaryStat.INT) * critMultiplier));

            var tags = new List<int>
            {
                DamageTag.AllTarget, DamageTag.UltAttack,
                DamageTag.NonContactAttack, DamageTag.Special,
            };

            foreach (Unit target in Enemies())
            {
                target.TakeDamage(new DamageContext(Caster, damage, BaseEnums.CodeType.Ultimate, tags, isCrit));
                if (target != null && target.isActive)
                {
                    target.GrantCombatElement(BaseEnums.UnitElement.Cryo, Unit.CommonElementAuraDuration, Caster);
                }
            }
        }
    }

    /// <summary>쿠베라 U — 황금의 지진. 적 전체에 STR 위력 70 + 에어본.</summary>
    public sealed class KuberaGoldenQuake : SimpleUltimate
    {
        private const int QuakePower = 70;

        public KuberaGoldenQuake(UltimateCodeContext context)
            : base(context, "황금의 지진", 4, 0.6f) { Power = QuakePower; }

        protected override void Resolve()
        {
            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(QuakePower, BaseEnums.PrimaryStat.STR) * critMultiplier));

            var tags = new List<int>
            {
                DamageTag.AllTarget, DamageTag.UltAttack,
                DamageTag.NonContactAttack, DamageTag.Special,
            };

            foreach (Unit target in Enemies())
            {
                target.TakeDamage(new DamageContext(Caster, damage, BaseEnums.CodeType.Ultimate, tags, isCrit));
                if (target != null && target.isActive)
                {
                    ControlStatuses.ApplyAirborne(target, Caster);
                }
            }
        }
    }

    /// <summary>
    /// 바루나 U — 마카라.
    /// 소환수 마카라를 8초간 부른다. 2초마다 적 전체에게 바루나 INT 위력 40 + 물 원소를 뿌린다.
    ///
    /// 소환수는 칸을 차지하지 않으므로 별도 유닛을 만들지 않고 코루틴으로 굴린다.
    /// 피해는 <see cref="Combat.Summons"/>를 거치므로 바루나의 주는 피해 버프를 물려받지 않는다.
    /// </summary>
    public sealed class VarunaMakara : UltimateCode
    {
        private const int Duration = 4;   // 8초 → 4턴
        private const int IntervalTurns = 1;   // 2초 → 1턴
        private const int TickPower = 40;

        public VarunaMakara(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = "마카라";
            Power = TickPower;
            Cooldown = 5;
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
            float elapsed = 0f;
            while (elapsed < CastingDelay)
            {
                if (Caster == null || !Caster.isActive || Caster.isControlled) { StopCode(); yield break; }
                elapsed += Time.deltaTime;
                yield return null;
            }

            // 벽시계 코루틴 대신 바루나의 턴마다 도는 상태로 굴린다.
            // 상태 지속시간이 곧 소환 시간이라 둘이 어긋날 일이 없다.
            Caster.AddStatus(BuffStatus.Create(
                NewWaveStatusIds.VarunaMakara, "varuna_makara", CodeName,
                Caster, Caster, new MakaraEffect(TickPower, IntervalTurns),
                duration: Duration,
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                isBeneficial: true,
                description: $"소환수 마카라가 {IntervalTurns}턴마다 적 전체를 공격합니다."));

            Debug.Log($"[마카라] {Duration}턴간 소환");
            StopCode();
        }

        public override void StopCode()
        {
            if (Caster == null) return;
            Caster.ultimateCooldown = Cooldown;
            Caster.isCasting = false;
        }

        public override bool HasValidTarget() => Caster != null && Caster.isActive;
    }

    /// <summary>
    /// 오르페우스 U — 비탄의 연주.
    /// 강인도가 없는 모든 적에게 `INT × 12`만큼 강인도를 새로 씌운다.
    /// </summary>
    public sealed class OrpheusLament : SimpleUltimate
    {
        private const int IntRatio = 12;

        public OrpheusLament(UltimateCodeContext context)
            : base(context, "비탄의 연주", 5, 0.5f) { Power = 0; }   // 피해를 주지 않는 궁극기

        protected override void Resolve()
        {
            int amount = Mathf.Max(1, Caster.GetBaseInt() * IntRatio);
            int granted = 0;
            foreach (Unit target in Enemies())
            {
                if (target.GrantToughness(amount, Caster)) granted++;
            }
            Debug.Log($"[비탄의 연주] {granted}명에게 강인도 {amount} 부여");
        }
    }

    /// <summary>
    /// 마카라 본체. 바루나의 턴마다 세고, 주기가 차면 적 전체를 쓸고 물을 부착한다.
    /// 소환수 피해이므로 <see cref="Combat.Summons"/>를 거쳐 바루나의 주는 피해 버프를 받지 않는다.
    /// </summary>
    internal sealed class MakaraEffect : Effects.Base.BaseEffect
    {
        private readonly int _power;
        private readonly int _intervalTurns;
        private int _turnsSinceSweep;

        public MakaraEffect(int power, int intervalTurns) : base(0, power)
        {
            _power = power;
            _intervalTurns = Mathf.Max(1, intervalTurns);
        }

        public override void OnOwnerTurn()
        {
            if (Target == null || !Target.isActive) return;

            _turnsSinceSweep++;
            if (_turnsSinceSweep < _intervalTurns) return;
            _turnsSinceSweep = 0;

            foreach (Unit enemy in global::Target.GetAllEnemies(Target)
                         .Where(unit => unit != null && unit.isActive && !unit.IsUntargetable)
                         .ToList())
            {
                Combat.Summons.Deal(Target, enemy, _power, BaseEnums.PrimaryStat.INT,
                    DamageTag.Special, DamageTag.NonContactAttack);
                if (enemy.isActive)
                {
                    enemy.GrantCombatElement(BaseEnums.UnitElement.Hydro, Unit.CommonElementAuraDuration, Target);
                }
            }
        }
    }

    /// <summary>
    /// 스사노오 U — 천총운검. 자동 시전하지 않는 <b>상시형 궁극기</b>다.
    /// 실제 발동 조건과 처리는 고유 패시브 <c>SusanooRaijin</c>이 관리한다.
    /// 수르트의 라그나로크와 같은 취급이며 마나를 쓰지 않는다.
    /// </summary>
    public sealed class SusanooAmenoMurakumo : UltimateCode
    {
        public override bool IsAutoCast => false;

        public SusanooAmenoMurakumo(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = "천총운검";
            Cooldown = 0;
            CastingDelay = 0f;
        }

        public override bool HasValidTarget() => false;
    }
}
