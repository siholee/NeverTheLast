using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Entities.Status;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>노르드 적 계열이 쓰는 상태 ID 대역.</summary>
    public static class NordEnemyStatusIds
    {
        public const int BerserkerRage = 8000;
        public const int Yeti = 8001;
        public const int LastGasp = 8002;
        public const int KingsRage = 8012;
        public const int KingsShout = 8013;
        public const int Stimpack = 8004;
        public const int StimpackHaste = 8005;
        public const int Snowcaller = 8006;
        public const int FolkRemedy = 8007;
        public const int FrostPriest = 8008;
        public const int PerfectCure = 8009;
    }

    /// <summary>노르드 적 코드 ID. 광전사 계열이 1600대, 볼바 계열이 1610대다.</summary>
    public static class NordEnemyCodeIds
    {
        public const int BerserkerRage = 1600;
        public const int Yeti = 1601;
        public const int LastGasp = 1602;
        public const int KingsRage = 1604;
        public const int KingsShout = 1605;

        public const int Stimpack = 1610;
        public const int Snowcaller = 1611;
        public const int FolkRemedy = 1612;
        public const int FrostPriest = 1613;
        public const int PerfectCure = 1614;
    }

    // ══════════════════════════════════════════════════════════════
    // 광전사
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 광전사 고유 P — 광전사의 격노.
    ///
    /// 잃은 체력 비율 하나가 DEX와 흡혈률을 함께 올린다. 체력이 깎일수록 빨라지고
    /// 더 많이 빨아들이는, 리그 오브 레전드의 올라프와 같은 모양이다.
    ///
    /// <b>DEX는 가산, 흡혈은 곱연산이다.</b> 행동치가 <c>10000 ÷ (1 + DEX×0.01)</c>이라
    /// DEX는 이미 쌍곡선이라 저절로 체감이 줄어든다. 반면 흡혈은 그냥 더하면 상한 없이 자라
    /// 저체력 광전사가 죽지 않는 진동자가 되므로, <b>남은 비흡혈분에서 일정 비율씩 떼어 가는</b>
    /// 방식으로 쌓는다. 상한을 두지 않아도 100%에 닿지 않고 늘수록 효율이 떨어진다.
    /// </summary>
    public sealed class BerserkerRage : PersistentStatusPassive
    {
        /// <summary>잃은 체력 100%에서 DEX에 더해지는 비율.</summary>
        public const float DexPerMissingHp = 0.60f;

        /// <summary>잃은 체력 10%p마다 남은 비흡혈분에서 가져가는 몫.</summary>
        public const float LifestealStep = 0.04f;

        public BerserkerRage(PassiveCodeContext context)
            : base(context, NordEnemyStatusIds.BerserkerRage, "berserker_rage", "광전사의 격노",
                "잃은 체력에 비례해 DEX와 흡혈률이 오릅니다. 흡혈은 쌓을수록 효율이 줄어듭니다.")
        {
            IsUniquePassive = true;
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect()
            => new BerserkerRageEffect(DexPerMissingHp, LifestealStep);
    }

    /// <summary>
    /// 라그나르 고유 P — 왕의 격노.
    ///
    /// 광전사의 격노와 <b>같은 구조에 계수만 크다.</b> 새 규칙을 얹지 않는 것이 이 엘리트의 설계다.
    /// 정직하게 굵은 값이라 앞 세 슬롯에서 배운 것이 그대로 통하되, 처형만은 통하지 않는다 —
    /// 천살성은 일반 등급만 잡는다.
    /// </summary>
    public sealed class KingsRage : PersistentStatusPassive
    {
        public const float DexPerMissingHp = 0.90f;
        public const float LifestealStep = 0.05f;

        public KingsRage(PassiveCodeContext context)
            : base(context, NordEnemyStatusIds.KingsRage, "kings_rage", "왕의 격노",
                "잃은 체력에 비례해 DEX와 흡혈률이 오릅니다. 광전사의 격노보다 계수가 큽니다.")
        {
            IsUniquePassive = true;
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect()
            => new BerserkerRageEffect(DexPerMissingHp, LifestealStep);
    }

    /// <summary>
    /// 왕의 함성 — 필드의 Berserker 아군이 내는 격노를 1.5배로 키운다.
    ///
    /// 4슬롯의 선택을 만드는 유일한 장치다. 라그나르를 먼저 죽이면 광전사들이 순해지지만
    /// 그동안 왕의 저체력 구간을 그대로 받아야 하고, 광전사들을 먼저 치우면 왕이 혼자 남는 대신
    /// 볼바 둘이 계속 채워 준다. 새 시스템 없이 <b>처치 순서만으로</b> 갈리게 한다.
    /// </summary>
    public sealed class KingsShout : PersistentStatusPassive
    {
        public const float RageMultiplier = 1.5f;

        public KingsShout(PassiveCodeContext context)
            : base(context, NordEnemyStatusIds.KingsShout, "kings_shout", "왕의 함성",
                "필드의 광전사 계열 아군이 내는 격노 계수가 1.5배가 됩니다.")
        {
        }

        protected override BaseEffect CreateInitialEffect() => new KingsShoutEffect(RageMultiplier);
    }

    /// <summary>격노의 계수를 키우는 쪽이 다는 표식. 격노 효과가 아군을 훑어 찾는다.</summary>
    internal interface IBerserkerRageAmplifier
    {
        float RageMultiplier { get; }
    }

    internal sealed class KingsShoutEffect : BaseEffect, IBerserkerRageAmplifier
    {
        public KingsShoutEffect(float multiplier) : base(0, multiplier) => RageMultiplier = multiplier;

        public override bool IsBeneficial => true;

        public float RageMultiplier { get; }
    }

    internal sealed class BerserkerRageEffect : BaseEffect
    {
        private readonly float _dexPerMissingHp;
        private readonly float _lifestealStep;

        private Action<DamageResolvedContext> _dealtHandler;
        private Action<EventContext> _refreshHandler;

        public BerserkerRageEffect(float dexPerMissingHp, float lifestealStep) : base(0)
        {
            _dexPerMissingHp = dexPerMissingHp;
            _lifestealStep = lifestealStep;
        }

        /// <summary>
        /// 왕의 함성이 서 있으면 계수가 커진다. 여럿이 들고 있어도 <b>가장 큰 하나</b>만 센다.
        /// 아우라 계열의 공통 규칙이라 라그나르가 둘 서도 겹쳐 쌓이지 않는다.
        ///
        /// 판의 목록을 직접 돈다. <c>CombatTargets</c>의 헬퍼는 호출마다 리스트를 새로 만드는데,
        /// 이 값은 <b>스탯을 읽을 때마다</b> 필요하므로 그대로 쓰면 전투 내내 쓰레기를 뿌린다.
        /// </summary>
        private float RageMultiplier
        {
            get
            {
                if (Target == null) return 1f;

                var side = Target.IsEnemy
                    ? GridManager.Instance?.enemyList
                    : GridManager.Instance?.heroList;
                if (side == null) return 1f;

                float best = 1f;
                for (int i = 0; i < side.Count; i++)
                {
                    Unit ally = side[i];
                    if (ally == null || !ally.isActive || !ally.IsOnField) continue;

                    var statuses = ally.ActiveStatuses;
                    for (int j = 0; j < statuses.Count; j++)
                    {
                        var effects = statuses[j]?.Effects;
                        if (effects == null) continue;

                        for (int k = 0; k < effects.Count; k++)
                        {
                            if (effects[k]?.EffectObject is IBerserkerRageAmplifier amp && amp.RageMultiplier > best)
                            {
                                best = amp.RageMultiplier;
                            }
                        }
                    }
                }

                return best;
            }
        }

        private float MissingRatio => Target == null || Target.HpMax <= 0
            ? 0f
            : Mathf.Clamp01(1f - (float)Target.HpCurr / Target.HpMax);

        public override void OnApply()
        {
            if (Target == null) return;

            _dealtHandler = OnDamageDealt;
            // 체력이 움직여도 파생 스탯은 저절로 다시 돌지 않는다. DEX가 잃은 체력을 보므로
            // 피격 직후와 자기 턴 시작에 직접 갱신한다. 치유로 올라간 쪽은 턴 시작이 잡는다.
            _refreshHandler = _ => Target?.RefreshDerivedAttributes();
            Target.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _dealtHandler);
            Target.AddListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _refreshHandler);
        }

        public override void OnOwnerTurn() => Target?.RefreshDerivedAttributes();

        public override void OnRemove()
        {
            if (Target == null) return;
            Target.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _dealtHandler);
            Target.RemoveListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _refreshHandler);
        }

        public override float PrimaryStatMultiplierModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Target && stat == BaseEnums.PrimaryStat.DEX
                ? 1f + MissingRatio * _dexPerMissingHp * RageMultiplier
                : 1f;

        private void OnDamageDealt(DamageResolvedContext context)
        {
            if (Target == null || context?.Attacker != Target || context.DamageDealt <= 0) return;

            float rate = LifestealRate();
            if (rate <= 0f) return;

            int healed = Mathf.RoundToInt(context.DamageDealt * rate);
            if (healed <= 0) return;

            Target.ModifyHp(Target.HpCurr + healed, Target);
        }

        /// <summary>
        /// 잃은 체력 10%p를 한 겹으로 세어 곱연산으로 쌓는다.
        /// <c>1 − (1 − 0.04)^(잃은 체력%p ÷ 10)</c> — 100%를 잃어도 33.5%에서 멈춘다.
        /// </summary>
        private float LifestealRate()
            => 1f - Mathf.Pow(1f - Mathf.Clamp01(_lifestealStep * RageMultiplier), MissingRatio * 10f);
    }

    /// <summary>
    /// 설인 — 눈 필드에서 DEX가 두 배가 된다.
    ///
    /// 속도가 <c>1 + DEX × 0.01</c>이므로 DEX를 두 배로 하는 것과
    /// "DEX가 제공하는 속도를 두 배로 하는 것"은 같은 식이다.
    /// 광전사는 불 속성이라 눈밭에서 피해가 20% 깎이는데, 그 손해를 속도로 되받는 자리다.
    /// </summary>
    public sealed class BerserkerYeti : PersistentStatusPassive
    {
        public BerserkerYeti(PassiveCodeContext context)
            : base(context, NordEnemyStatusIds.Yeti, "berserker_yeti", "설인",
                "눈 필드에 있는 동안 DEX가 두 배가 됩니다.")
        {
        }

        protected override BaseEffect CreateInitialEffect() => new BerserkerYetiEffect();
    }

    internal sealed class BerserkerYetiEffect : BaseEffect
    {
        public BerserkerYetiEffect() : base(0) { }

        public override float PrimaryStatMultiplierModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Target && stat == BaseEnums.PrimaryStat.DEX && Battlefield.Is(FieldKind.Snow)
                ? 2f
                : 1f;
    }

    /// <summary>최후의 발악 — 클러치 플레이어(42)의 금색 상위. 잃은 체력 비율의 80%를 피해로 바꾼다.</summary>
    public sealed class BerserkerLastGasp : PersistentStatusPassive
    {
        private const float Ratio = 0.80f;

        public BerserkerLastGasp(PassiveCodeContext context)
            : base(context, NordEnemyStatusIds.LastGasp, "berserker_last_gasp", "최후의 발악",
                "잃은 체력 비율의 80%만큼 주는 피해가 증가합니다. 클러치 플레이어와 중첩되지 않습니다.")
        {
            Grade = BaseEnums.CodeGrade.Enhanced;
        }

        protected override BaseEffect CreateInitialEffect() => new MissingHpDamageEffect(Ratio);
    }

    /// <summary>잃은 체력 비율에 비례해 주는 피해를 올린다. 클러치 플레이어 계열의 공용 구현.</summary>
    internal sealed class MissingHpDamageEffect : BaseEffect
    {
        private readonly float _ratio;

        public MissingHpDamageEffect(float ratio) : base(0, ratio) => _ratio = ratio;

        public override bool IsBeneficial => true;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target || attacker.HpMax <= 0) return 1f;
            float missing = 1f - Mathf.Clamp01((float)attacker.HpCurr / attacker.HpMax);
            return 1f + missing * _ratio;
        }
    }

    /// <summary>받는 치유와 보호막에 같은 배율을 곱한다. 천상의 신체 계열의 공용 구현.</summary>
    internal sealed class ReceivedSupportMultiplierEffect : BaseEffect
    {
        private readonly float _multiplier;

        public ReceivedSupportMultiplierEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;

        public override bool IsBeneficial => true;

        public override float HealingReceivedMultiplierModifier(Unit unit)
            => unit == Target ? _multiplier : 1f;

        public override float ShieldReceivedMultiplierModifier(Unit unit)
            => unit == Target ? _multiplier : 1f;
    }

    // ══════════════════════════════════════════════════════════════
    // 볼바
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 볼바 고유 P — 스팀팩.
    ///
    /// 치유한 대상에게 자기 INT의 30%만큼 DEX를 1턴 얹는다.
    /// 계수를 1로 두면 볼바의 INT가 광전사의 DEX보다 크게 자라 치유 한 번에 속도가 배로 뛴다.
    /// 힐러가 곧 가속기라 <b>광전사의 격노와 같은 축</b>을 민다.
    /// </summary>
    public sealed class VolvaStimpack : PersistentStatusPassive
    {
        public const float IntCoefficient = 0.30f;
        public const int DurationTurns = 1;

        public VolvaStimpack(PassiveCodeContext context)
            : base(context, NordEnemyStatusIds.Stimpack, "volva_stimpack", "스팀팩",
                "치유한 대상이 1턴 동안 볼바 INT의 30%만큼 DEX를 얻습니다.")
        {
            IsUniquePassive = true;
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new StimpackEffect();
    }

    internal sealed class StimpackEffect : BaseEffect
    {
        public StimpackEffect() : base(0) { }

        public override void OnHealingOrShieldGranted(Unit source, Unit target)
        {
            if (source != Target || target == null || !target.isActive) return;

            int bonus = Mathf.RoundToInt(
                source.GetBaseInt() * VolvaStimpack.IntCoefficient);
            if (bonus <= 0) return;

            target.AddStatus(BuffStatus.Create(
                NordEnemyStatusIds.StimpackHaste, "volva_stimpack_haste", "스팀팩",
                source, target, new PrimaryStatBonusBuffEffect(BaseEnums.PrimaryStat.DEX, bonus),
                duration: VolvaStimpack.DurationTurns,
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                isBeneficial: true,
                description: $"DEX가 {bonus} 증가합니다."));
        }
    }

    /// <summary>눈바라기 — 전투 시작 시 판을 눈으로 덮는다.</summary>
    public sealed class VolvaSnowcaller : PersistentStatusPassive
    {
        private const int DurationTurns = 3;

        public VolvaSnowcaller(PassiveCodeContext context)
            : base(context, NordEnemyStatusIds.Snowcaller, "volva_snowcaller", "눈바라기",
                "전투 시작 시 전장을 3턴 동안 눈으로 덮습니다.")
        {
        }

        public override void CastCode()
        {
            base.CastCode();
            if (Caster == null) return;

            // 지속은 깐 유닛의 턴으로 센다. 볼바가 쓰러져도 남은 턴은 그대로 흐른다.
            Battlefield.Set(FieldKind.Snow, Caster, DurationTurns);
        }

        protected override BaseEffect CreateInitialEffect() => new MarkerBuffEffect();
    }

    /// <summary>혹한의 사제 — 눈 필드에서 부여하는 치유량 +50%.</summary>
    public sealed class VolvaFrostPriest : PersistentStatusPassive
    {
        private const float Multiplier = 1.50f;

        public VolvaFrostPriest(PassiveCodeContext context)
            : base(context, NordEnemyStatusIds.FrostPriest, "volva_frost_priest", "혹한의 사제",
                "눈 필드에 있는 동안 부여하는 치유량이 50% 증가합니다.")
        {
        }

        protected override BaseEffect CreateInitialEffect() => new FrostPriestEffect(Multiplier);
    }

    internal sealed class FrostPriestEffect : BaseEffect
    {
        private readonly float _multiplier;

        public FrostPriestEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;

        public override bool IsBeneficial => true;

        public override float OutgoingHealingMultiplierModifier(Unit source, Unit target)
            => source == Target && Battlefield.Is(FieldKind.Snow) ? _multiplier : 1f;
    }

    /// <summary>
    /// 민간요법 — 치유할 때 대상의 해로운 상태를 최대 두 개까지 지운다.
    /// 금색 상위인 완벽한 치료는 같은 자리에서 전부 지운다.
    /// </summary>
    public sealed class VolvaFolkRemedy : PersistentStatusPassive
    {
        public VolvaFolkRemedy(PassiveCodeContext context)
            : base(context, NordEnemyStatusIds.FolkRemedy, "volva_folk_remedy", "민간요법",
                "치유할 때 대상의 해로운 상태를 최대 2개까지 정화합니다.")
        {
            SupersededByCodeId = NordEnemyCodeIds.PerfectCure;
        }

        protected override BaseEffect CreateInitialEffect() => new CleanseOnHealEffect(2);
    }

    /// <summary>완벽한 치료 — 민간요법(1612)의 금색 상위. 치유할 때 해로운 상태를 전부 지운다.</summary>
    public sealed class VolvaPerfectCure : PersistentStatusPassive
    {
        public VolvaPerfectCure(PassiveCodeContext context)
            : base(context, NordEnemyStatusIds.PerfectCure, "volva_perfect_cure", "완벽한 치료",
                "치유할 때 대상의 해로운 상태를 모두 정화합니다.")
        {
            Grade = BaseEnums.CodeGrade.Enhanced;
        }

        protected override BaseEffect CreateInitialEffect() => new CleanseOnHealEffect(int.MaxValue);
    }

    /// <summary>치유가 들어갈 때 대상의 해로운 상태를 지운다. 지울 개수만 다른 같은 코드다.</summary>
    internal sealed class CleanseOnHealEffect : BaseEffect
    {
        private readonly int _maxCount;

        public CleanseOnHealEffect(int maxCount) : base(0, maxCount) => _maxCount = Mathf.Max(1, maxCount);

        public override bool IsBeneficial => true;

        public override void OnHealingOrShieldGranted(Unit source, Unit target)
        {
            if (source != Target || target == null || !target.isActive) return;

            List<UnitStatus> negatives = target.GetAllStatuses()
                .Where(status => status != null && status.Category == BaseEnums.StatusCategory.Negative)
                .Take(_maxCount)
                .ToList();

            foreach (UnitStatus status in negatives)
            {
                target.RemoveStatusByKey(status.Key);
            }
        }
    }
}
