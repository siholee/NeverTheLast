using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>토르가 쓰는 상태·코드 ID. 노르드 블록의 1620대다.</summary>
    public static class ThorIds
    {
        public const int MjolnirRhythm = 1620;
        public const int PrinceOfNord = 1621;
        public const int Momentum = 1622;
        public const int WinterWind = 1623;

        public const int StatusRhythm = 8020;
        public const int StatusPrince = 8021;
        public const int StatusMomentum = 8022;
        public const int StatusWinterWind = 8023;
        public const int StatusGrowth = 8024;
    }

    /// <summary>
    /// 토르 고유 P — 뇌신의 박자.
    ///
    /// 일반행동을 두 번 하면 세 번째가 대체행동으로 바뀌고, 대체행동이 나갈 때마다
    /// STR과 CON이 3씩 영구히 쌓인다. <b>길어질수록 커지는 보스</b>라 시간이 플레이어 편이 아니다.
    ///
    /// 박자를 세는 것은 일반행동 코드(<c>ThorNormalAttack</c>)이고 여기서는 성장만 맡는다.
    /// 카운터가 두 곳에 있으면 어긋나므로 세는 쪽을 하나로 둔다.
    /// </summary>
    public sealed class ThorMjolnirRhythm : PersistentStatusPassive
    {
        /// <summary>대체행동 한 번이 주는 STR·CON.</summary>
        public const int GrowthPerSubstitute = 3;

        public ThorMjolnirRhythm(PassiveCodeContext context)
            : base(context, ThorIds.StatusRhythm, "thor_rhythm", "뇌신의 박자",
                "일반행동 2회 뒤의 일반행동이 대체행동으로 바뀌고, 대체행동마다 STR과 CON이 3씩 증가합니다.")
        {
            IsUniquePassive = true;
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new ThorGrowthEffect(GrowthPerSubstitute);

        /// <summary>대체행동이 실제로 나갔을 때 일반행동 코드가 부른다.</summary>
        public static void NotifySubstitute(Unit thor)
        {
            if (thor == null) return;
            foreach (var status in thor.ActiveStatuses.ToList())
            {
                foreach (var effect in status.Effects)
                {
                    if (effect.EffectObject is ThorGrowthEffect growth) growth.Grow();
                }
            }
        }
    }

    /// <summary>대체행동마다 STR·CON이 쌓인다. 스탯 질의는 매번 살아 있는 값을 돌려준다.</summary>
    internal sealed class ThorGrowthEffect : BaseEffect
    {
        private readonly int _perStack;
        private int _stacks;

        public ThorGrowthEffect(int perStack) : base(0, perStack) => _perStack = Mathf.Max(1, perStack);

        public override bool IsBeneficial => true;

        public void Grow()
        {
            _stacks++;
            // CON이 오르면 최대 체력이 함께 오른다. AttributesUpdate가 체력 비율을 보존하므로
            // 현재 체력도 같은 비율로 따라 올라간다 — 보스가 전투 도중 조금씩 두꺼워진다.
            Target?.RefreshDerivedAttributes();
        }

        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
        {
            if (unit != Target) return 0;
            return stat is BaseEnums.PrimaryStat.STR or BaseEnums.PrimaryStat.CON
                ? _stacks * _perStack
                : 0;
        }
    }

    /// <summary>
    /// 노르드의 왕자 — 체력 칸이 처음 깨질 때 판을 눈으로 덮는다.
    ///
    /// 토르는 `알파 개체`(1541)로 체력 바가 두 칸이라 <b>첫 칸이 깨지는 순간</b>이 곧 절반이다.
    /// 보스의 2페이즈 신호를 필드 전환으로 삼은 것이고, 같이 드는 `설인`(1601)이
    /// 그 순간부터 DEX를 두 배로 만든다. 눈이 깔리면 토르가 빨라진다.
    /// </summary>
    public sealed class ThorPrinceOfNord : PersistentStatusPassive
    {
        private const int DurationTurns = 3;

        public ThorPrinceOfNord(PassiveCodeContext context)
            : base(context, ThorIds.StatusPrince, "thor_prince_of_nord", "노르드의 왕자",
                "체력 칸이 처음 깨질 때 전장을 3턴 동안 눈으로 덮습니다.")
        {
        }

        protected override BaseEffect CreateInitialEffect() => new ThorPrinceEffect(DurationTurns);
    }

    internal sealed class ThorPrinceEffect : BaseEffect
    {
        /// <summary>체력 칸을 나누는 코드가 없을 때 쓰는 기본 칸 수. 절반에서 한 번 깨진다.</summary>
        private const int FallbackSegments = 2;

        private readonly int _durationTurns;
        private Action<EventContext> _handler;
        private bool _fired;

        public ThorPrinceEffect(int durationTurns) : base(0, durationTurns) => _durationTurns = durationTurns;

        public override void OnApply()
        {
            if (Target == null) return;
            _handler = _ => CheckBreak();
            Target.AddListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _handler);
        }

        public override void OnRemove()
        {
            if (Target == null || _handler == null) return;
            Target.RemoveListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _handler);
            _handler = null;
        }

        private void CheckBreak()
        {
            if (_fired || Target == null || !Target.isActive || Target.HpMax <= 0) return;

            int segments = Mathf.Max(FallbackSegments, Target.HpSegmentCount);
            float threshold = Target.HpMax * (segments - 1) / (float)segments;
            if (Target.HpCurr > threshold) return;

            _fired = true;
            Battlefield.Set(FieldKind.Snow, Target, _durationTurns);
            Debug.Log($"[노르드의 왕자] {Target.UnitName}의 첫 칸이 깨져 눈이 내린다");
        }
    }

    /// <summary>
    /// 승승장구 — 적을 처치하면 <b>물리</b> 피해가 5%씩 오른다. 중첩된다.
    /// 자기과신(66)의 물리 짝이다. 둘은 태그가 달라 함께 들어도 서로를 재우지 않는다.
    /// </summary>
    public sealed class ThorMomentum : PersistentStatusPassive
    {
        private const float BonusPerKill = 0.05f;

        public ThorMomentum(PassiveCodeContext context)
            : base(context, ThorIds.StatusMomentum, "thor_momentum", "승승장구",
                "적을 처치할 때마다 물리 태그로 가하는 피해가 5%씩 증가합니다. 중첩됩니다.")
        {
            SupersededByCodeId = 445;
        }

        protected override BaseEffect CreateInitialEffect() => new KillStackTaggedDamageEffect(
            DamageTag.Physical, BonusPerKill);
    }

    /// <summary>처치할 때마다 특정 태그의 피해가 쌓인다. 자기과신·승승장구가 함께 쓴다.</summary>
    internal sealed class KillStackTaggedDamageEffect : BaseEffect
    {
        private readonly int _tag;
        private readonly float _bonusPerKill;
        private Action<EventContext> _handler;
        private int _stacks;

        public KillStackTaggedDamageEffect(int tag, float bonusPerKill) : base(0, bonusPerKill)
        {
            _tag = tag;
            _bonusPerKill = bonusPerKill;
        }

        public override bool IsBeneficial => true;

        public override void OnApply()
        {
            if (Target == null) return;
            _handler = _ => _stacks++;
            Target.AddListener(BaseEnums.UnitEventType.OnKill, _handler);
        }

        public override void OnRemove()
        {
            if (Target == null || _handler == null) return;
            Target.RemoveListener(BaseEnums.UnitEventType.OnKill, _handler);
            _handler = null;
        }

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && _stacks > 0 && context?.DamageTags != null && context.DamageTags.Contains(_tag)
                ? 1f + _stacks * _bonusPerKill
                : 1f;
    }

    /// <summary>
    /// 겨울바람 — 눈 위에 부는 바람.
    ///
    /// 눈이 깔려 있는 동안 <b>판 위의 모든 유닛</b>에게 피해를 주고 얼음을 부착한다.
    /// 적아를 가리지 않는 것은 전장 상태와 같은 성질이기 때문이다 — 판에 걸리는 것은 양쪽을 친다.
    ///
    /// 고정 주기로 돈다. 보유자의 DEX를 따르면 토르가 빨라질수록 바람도 빨라져
    /// `설인`과 곱해지는데, 그러면 2페이즈에서 판이 걷잡을 수 없이 기운다.
    /// </summary>
    public sealed class ThorWinterWind : PersistentStatusPassive
    {
        /// <summary>바람이 부는 주기(보유자 턴).</summary>
        public const int IntervalTurns = 2;

        /// <summary>바람 한 번의 위력. 보유자의 CON을 탄다.</summary>
        public const int GustPower = 40;

        public ThorWinterWind(PassiveCodeContext context)
            : base(context, ThorIds.StatusWinterWind, "thor_winter_wind", "겨울바람",
                "눈 필드에 있는 동안 2턴마다 전장의 모든 유닛에게 CON 위력 40의 피해를 주고 얼음을 부착합니다.")
        {
        }

        protected override BaseEffect CreateInitialEffect() => new ThorWinterWindEffect();
    }

    internal sealed class ThorWinterWindEffect : BaseEffect
    {
        private int _turnsUntilGust = ThorWinterWind.IntervalTurns;

        public ThorWinterWindEffect() : base(0, ThorWinterWind.GustPower) { }

        public override void OnOwnerTurn()
        {
            if (Target == null || !Target.isActive) return;
            if (!Battlefield.Is(FieldKind.Snow))
            {
                // 눈이 걷히면 주기도 처음부터 다시 센다. 눈이 다시 깔리자마자 터지지 않게 한다.
                _turnsUntilGust = ThorWinterWind.IntervalTurns;
                return;
            }

            if (--_turnsUntilGust > 0) return;
            _turnsUntilGust = ThorWinterWind.IntervalTurns;
            Gust();
        }

        private void Gust()
        {
            int damage = Mathf.Max(1, Target.SkillDamage(ThorWinterWind.GustPower, BaseEnums.PrimaryStat.CON));
            var tags = new List<int>
            {
                DamageTag.AllTarget, DamageTag.Special, DamageTag.NonContactAttack,
            };

            // 판 위의 전원이다. 아군 목록과 적 목록을 함께 훑는다.
            var everyone = new List<Unit>(CombatTargets.AliveAlliesIncludingSelf(Target));
            everyone.AddRange(CombatTargets.AliveEnemies(Target));

            foreach (Unit unit in everyone)
            {
                if (unit == null || !unit.isActive) continue;
                unit.TakeDamage(new DamageContext(Target, damage, BaseEnums.CodeType.Passive, tags, false));
                if (unit.isActive)
                {
                    unit.GrantCombatElement(BaseEnums.UnitElement.Cryo, Unit.CommonElementAuraDuration, Target);
                }
            }

            Debug.Log($"[겨울바람] 판 위 전원에게 {damage}");
        }
    }
}
