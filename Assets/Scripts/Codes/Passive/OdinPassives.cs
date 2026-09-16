using System;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Effects.Negative;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>오딘이 쓰는 코드·상태 ID.</summary>
    public static class OdinIds
    {
        public const int VoidPact = 3001;
        public const int HuginnMuninn = 3002;
        public const int Foresight = 3003;

        public const int StatusVoidPact = 8040;
        public const int StatusAwakened = 8041;
        public const int StatusForesight = 8042;

        /// <summary>2페이즈에서 세우는 까마귀. 공허의 괴조를 그대로 쓴다.</summary>
        public const int RavenEnemyId = 1070;

        /// <summary>세 칸 체력 중 두 칸이 깨지면 2페이즈로 넘어간다.</summary>
        public const int PhaseTwoBreakCount = 2;

        /// <summary>2브레이크 뒤에도 전투가 이어지도록 오딘의 체력을 세 칸으로 표시한다.</summary>
        public const int PhaseSegmentCount = PhaseTwoBreakCount + 1;

        /// <summary>공허 각성 이후 전장에서 사용할 초상화.</summary>
        public const string PhaseTwoPortrait = "ODIN_PHASE2_PORTRAIT";

        /// <summary>1페이즈 속성.</summary>
        public const string PhaseOneElement = "Electro";
    }

    /// <summary>
    /// 오딘 고유 P — 공허와의 계약.
    ///
    /// <b>노르드의 폭군은 지식을 훔쳐본 것이 아니라 스스로 넘어간 쪽이다.</b>
    /// 1페이즈에서는 번개를 두른 노르드의 신으로 서 있다가, 체력 칸이 두 번 깨지는 순간
    /// 속성 자체가 <b>공허</b>로 바뀐다 — 가면이 벗겨지는 자리다.
    /// 노르드 중부의 뒷 절반이 공허로 채워지는 것도 그가 문을 열어 줬기 때문이다.
    ///
    /// 기믹은 순환 하나다. <b>파티는 못 채우는데 오딘은 채운다.</b>
    /// 로키의 `발드르의 살해자`가 최대 체력이 가장 높은 적을 자동으로 겨누고
    /// 받는 치유량 −25%를 남기므로, 이 순환을 끊는 답을 로키가 이미 들고 있다.
    /// </summary>
    public sealed class OdinVoidPact : PersistentStatusPassive
    {
        /// <summary>적중한 대상에게 거는 치유량 감소.</summary>
        public const float HealingReduction = 0.40f;
        public const int ReductionTurns = 3;

        /// <summary>가한 피해 중 자신이 회복하는 비율.</summary>
        public const float Lifesteal = 0.15f;

        public OdinVoidPact(PassiveCodeContext context)
            : base(context, OdinIds.StatusVoidPact, "odin_void_pact", "공허와의 계약",
                "공격이 적중하면 대상의 받는 치유량이 3턴 동안 40% 감소하고, " +
                "가한 피해의 15%를 회복합니다. 체력이 세 칸으로 나뉘며, " +
                "두 칸이 깨지면 속성과 모습이 공허로 바뀝니다.")
        {
            IsUniquePassive = true;
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new OdinVoidPactEffect();
    }

    internal sealed class OdinVoidPactEffect : BaseEffect
    {
        private Action<DamageResolvedContext> _dealtHandler;
        private Action<EventContext> _takenHandler;
        private bool _awakened;

        public OdinVoidPactEffect() : base(0) { }

        // 알파 개체가 최대 체력을 불리는 역할은 그대로 맡는다. 이 효과는 총 체력은 건드리지 않고
        // 오딘 전용 3칸 표시와 한 번에 한 칸만 깨지는 상한만 제공한다.
        public override int HpSegmentCount(Unit unit)
            => unit == Target ? OdinIds.PhaseSegmentCount : 0;

        public override float IncomingDamageCapRatio(Unit unit, DamageContext context)
            => unit == Target ? 1f / OdinIds.PhaseSegmentCount : 0f;

        public override void OnApply()
        {
            if (Target == null) return;

            _awakened = false;
            _dealtHandler = OnDamageDealt;
            _takenHandler = _ => CheckPhase();
            Target.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _dealtHandler);
            Target.AddListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _takenHandler);
        }

        public override void OnRemove()
        {
            if (Target == null) return;
            Target.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _dealtHandler);
            Target.RemoveListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _takenHandler);
            _dealtHandler = null;
            _takenHandler = null;
        }

        private void OnDamageDealt(DamageResolvedContext context)
        {
            if (Target == null || !Target.isActive || context?.Target == null) return;
            if (context.DamageDealt <= 0) return;

            // 빼앗는다 — 맞은 쪽은 채울 수 없게 된다.
            if (context.Target.isActive)
            {
                HealingReductionStatus.Apply(context.Target, Target,
                    OdinVoidPact.ReductionTurns, "공허와의 계약", OdinVoidPact.HealingReduction);
            }

            // 채운다 — 계약의 대가로 흘러 들어온다.
            // 자기 자신을 source로 넘겨야 `공격으로 얻는 회복`으로 잡히고,
            // 로키의 표식이 거는 치유량 감소가 이 회복에 그대로 적용된다.
            int healed = Mathf.RoundToInt(context.DamageDealt * OdinVoidPact.Lifesteal);
            if (healed > 0) Target.ModifyHp(Target.HpCurr + healed, Target);
        }

        /// <summary>체력 칸이 두 번 깨지는 순간 가면이 벗겨진다.</summary>
        private void CheckPhase()
        {
            if (_awakened || Target == null || !Target.isActive || Target.HpMax <= 0) return;

            int segments = Mathf.Max(OdinIds.PhaseSegmentCount, Target.HpSegmentCount);
            float segmentSize = Target.HpMax / (float)segments;
            int remainingSegments = Mathf.CeilToInt(Target.HpCurr / segmentSize);
            int brokenSegments = Mathf.Clamp(segments - remainingSegments, 0, segments - 1);
            if (brokenSegments < OdinIds.PhaseTwoBreakCount) return;

            _awakened = true;
            Target.ChangeInnateElement(BaseEnums.UnitElement.Void);
            Target.ChangePortraitSprite(OdinIds.PhaseTwoPortrait);
            Target.AddStatus(BuffStatus.Create(
                OdinIds.StatusAwakened, "odin_void_awakened", "공허 각성",
                Target, Target, new MarkerBuffEffect(), 0));
            Debug.Log($"[공허와의 계약] {Target.UnitName}의 두 번째 체력 칸이 깨졌다 — 속성과 모습이 공허가 된다");
        }
    }

    /// <summary>
    /// 후긴과 무닌 — <b>2페이즈에서만</b> 까마귀를 부른다.
    ///
    /// 그의 까마귀는 이미 공허의 것이다. 새 적도 새 스프라이트도 만들지 않고
    /// 공허의 괴조(1070)를 그대로 세우는 것이 배신의 증거이기도 하다.
    ///
    /// 까마귀는 얇지만 맞히면 계약과 같은 치유량 감소를 나른다. 본체를 때리는 동안
    /// 힐 잠금을 계속 새로 걸어 오므로, 광역으로 무리를 정리하지 않으면 잠금이 풀리지 않는다.
    /// 수르트의 `라그나로크`가 단일과 광역을 한 궁극기에 담는 유일한 노르드 아군이다.
    /// </summary>
    public sealed class OdinHuginnMuninn : PeriodicTurnPassive
    {
        private const int IntervalTurns = 3;
        private const int RavensPerCall = 2;

        public OdinHuginnMuninn(PassiveCodeContext context) : base(context, IntervalTurns)
        {
            CodeName = "후긴과 무닌";
        }

        protected override void OnPeriodElapsed()
        {
            if (Caster == null || !Caster.isActive) return;

            // 1페이즈에는 부르지 않는다. 가면이 벗겨진 뒤에야 까마귀가 공허에서 날아온다.
            if (!Caster.HasStatusKey("odin_void_awakened"))
            {
                RetryNextTurn();
                return;
            }

            GridManager grid = GridManager.Instance;
            if (grid == null) return;

            int rear = grid.GetRearColumn(true);
            int called = 0;
            for (int y = 1; y <= 4 && called < RavensPerCall; y++)
            {
                if (!grid.IsCellAvailable(rear, y)) continue;
                if (grid.SpawnUnit(rear, y, true, OdinIds.RavenEnemyId) != null) called++;
            }

            // 자리가 없으면 다음 턴에 다시 시도한다. 주기를 통째로 날리지 않는다.
            if (called == 0) RetryNextTurn();
            else Debug.Log($"[후긴과 무닌] 까마귀 {called}마리가 공허에서 날아왔다");
        }
    }

    /// <summary>예언 — 앞을 보므로 피한다. 회피 15%.</summary>
    public sealed class OdinForesight : PersistentStatusPassive
    {
        public const float EvasionChance = 0.15f;

        public OdinForesight(PassiveCodeContext context)
            : base(context, OdinIds.StatusForesight, "odin_foresight", "예언",
                "회피 확률이 15% 증가합니다.")
        {
        }

        protected override BaseEffect CreateInitialEffect() => new OdinForesightEffect(EvasionChance);
    }

    internal sealed class OdinForesightEffect : BaseEffect
    {
        private readonly float _chance;

        public OdinForesightEffect(float chance) : base(0, chance) => _chance = chance;

        public override bool IsBeneficial => true;

        public override float EvasionChanceAdditiveModifier(Unit unit, DamageContext context)
            => unit == Target ? _chance : 0f;
    }
}
