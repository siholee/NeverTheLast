using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Effects.Buffs;
using Effects.Negative;
using Entities;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>신규 캐릭터군이 쓰는 상태 ID 대역.</summary>
    public static class NewWaveStatusIds
    {
        public const int VarunaMakara = 5302;
    }

    /// <summary>
    /// 프레이아 U — 풍요의 산물.
    ///
    /// 이번 전투에서 프레이아가 실제로 채운 체력(순수치유량)을 자원으로 쓴다.
    /// 적 전체를 INT 위력 60으로 때리되 <b>쌓인 순수치유량에 비례해 피해가 커지고</b>,
    /// 적중한 모든 적에게 풀 원소를 부착한 뒤 기록을 비운다.
    ///
    /// 순수치유량 기록과 정산은 이 궁극기 자체가 전담한다. 피톤치드는 전투 중 CON 오라다.
    ///
    /// 부착이 피해 <b>뒤</b>에 오는 것은 연소를 노린 순서다. 수르트가 먼저 불을 깔아 두면
    /// 풀이 덮이는 순간 유발자가 프레이아가 되어 그녀의 CON이 연소 위력을 정한다.
    /// </summary>
    public sealed class FreyaHarvest : SimpleUltimate
    {
        private const int HarvestPower = 60;

        /// <summary>피해 배율이 두 배가 되는 데 필요한 순수치유량 = 최대 체력의 이 배수.</summary>
        private const float FullScaleHpMultiple = 2f;

        /// <summary>순수치유량이 더해 줄 수 있는 배율의 상한. 1이면 최대 두 배다.</summary>
        private const float MaxBonusScale = 1f;

        public FreyaHarvest(UltimateCodeContext context)
            : base(context, "풍요의 산물", 0.5f) { Power = HarvestPower; }

        protected override void Resolve()
        {
            // 정산 전 기록을 먼저 붙든다. 피해 계산 뒤 이 궁극기가 직접 기록을 비운다.
            int record = Caster.RoundEffectiveHealingDone;
            float scale = 1f + Mathf.Min(
                MaxBonusScale, record / Mathf.Max(1f, Caster.HpMax * FullScaleHpMultiple));

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(HarvestPower, BaseEnums.PrimaryStat.INT) * critMultiplier * scale));

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
                    target.GrantCombatElement(
                        BaseEnums.UnitElement.Dendro, Unit.CommonElementAuraDuration, Caster);
                }
            }

            Caster.ResetEffectiveHealingRecord();

            Debug.Log($"[풍요의 산물] 순수치유량 {record} 정산 → 피해 배율 {scale:0.00}");
        }

        /// <summary>
        /// 적이 없으면 쓰지 않는다. 발동은 곧 기록 정산이라, 때릴 대상이 없는데 나가면
        /// 쌓아 둔 순수치유량이 아무것도 사지 못하고 사라진다.
        /// </summary>
        public override bool HasValidTarget()
            => Caster != null && Caster.isActive && Enemies().Count > 0;
    }

    /// <summary>
    /// 로키 U — 발드르의 살해자.
    ///
    /// 최대 체력이 가장 높은 적에게 STR 위력 120을 꽂고 <b>표식</b>을 남긴다.
    /// 표식은 지속시간이 없다. 로키가 쓰러지거나 다른 대상에게 다시 새길 때까지 남으며,
    /// 표식이 붙은 적은 받는 치유량 −25%와 방어력 −20%를 지고 펜리르에게 물린다.
    ///
    /// 표식의 관리는 고유 패시브 <c>펜리르</c>가 통째로 맡는다. 반응 물기와 표식이
    /// 한 곳에서 붙고 떨어져야 아군 방아쇠 기록도 함께 초기화되기 때문이다.
    /// </summary>
    public sealed class LokiBaldrSlayer : SimpleUltimate
    {
        private const int SlashPower = 92;
        private const float SlashStrCoefficient = 0.27f;

        public LokiBaldrSlayer(UltimateCodeContext context)
            : base(context, "발드르의 살해자", 0.5f)
        {
            Power = SlashPower;
            PowerStatCoefficient = SlashStrCoefficient;
            PowerStat = BaseEnums.PrimaryStat.STR;
        }

        protected override void Resolve()
        {
            Unit target = Enemies().OrderByDescending(enemy => enemy.HpMax).FirstOrDefault();
            if (target == null) return;

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR) * critMultiplier));

            target.TakeDamage(new DamageContext(Caster, damage, BaseEnums.CodeType.Ultimate, new List<int>
            {
                DamageTag.SingleTarget, DamageTag.UltAttack,
                DamageTag.NonContactAttack, DamageTag.Physical,
            }, isCrit));

            if (!target.isActive) return;

            Caster.ActivePassiveCodes.OfType<LokiFenrir>().FirstOrDefault()?.MarkTarget(target);
        }
    }

    /// <summary>스카디 U — 고드름 스파이크. 적 전체에 고정 위력 80 + CON×0.8 + 얼음 원소 부착.</summary>
    public sealed class SkadiIcicleSpike : SimpleUltimate
    {
        private const int SpikePower = 80;

        public SkadiIcicleSpike(UltimateCodeContext context)
            : base(context, "고드름 스파이크", 0.6f)
        {
            Power = SpikePower;
            PowerStatCoefficient = 0.8f;
            PowerStat = BaseEnums.PrimaryStat.CON;
        }

        protected override void Resolve()
        {
            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.CON) * critMultiplier));

            var tags = new List<int>
            {
                DamageTag.AllTarget, DamageTag.UltAttack,
                DamageTag.ContactAttack, DamageTag.Physical,
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
            : base(context, "황금의 지진", 0.6f) { Power = QuakePower; }

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
    /// 소환수 마카라를 4턴간 부른다. 턴마다 적 전체에게 바루나 INT 위력 40 + 물 원소를 뿌린다.
    ///
    /// 소환수는 칸을 차지하지 않으므로 별도 유닛을 만들지 않고 코루틴으로 굴린다.
    /// 피해는 <see cref="Combat.Summons"/>를 거치므로 바루나의 주는 피해 버프를 물려받지 않는다.
    /// </summary>
    public sealed class VarunaMakara : UltimateCode
    {
        private const int Duration = 4;
        private const int IntervalTurns = 1;
        private const int TickPower = 40;

        public VarunaMakara(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = "마카라";
            Power = TickPower;
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
            : base(context, "비탄의 연주", 0.5f) { Power = 0; }   // 피해를 주지 않는 궁극기

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
    /// 마나를 쓰지 않으며 고유 패시브가 조건을 감시한다.
    /// </summary>
    public sealed class SusanooAmenoMurakumo : UltimateCode
    {
        public override bool IsAutoCast => false;

        public SusanooAmenoMurakumo(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = "천총운검";
            CastingDelay = 0f;
        }

        public override bool HasValidTarget() => false;
    }
}
