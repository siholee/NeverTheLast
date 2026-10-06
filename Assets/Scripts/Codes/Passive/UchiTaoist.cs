using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Normal;
using Combat;
using Entities;
using Managers;

namespace Codes.Passive
{
    /// <summary>
    /// 우치 — 특성 「도사란 무엇인가」(Detail_08 §10.1 · §10.2). 다수형 소환자로 분신을 최대 3기 거느린다.
    ///   · 분신: 칸 없음 · 대상 불가 · 고정 속도 70, 제 차례에 우치 공격력 120%로 한 번 치고 사라진다.
    ///   · 넘침: 상한에서 새 분신이 생기면 가장 오래된 분신부터 즉시 60%로 치고 사라진다(일반 · 초월기 · 외부 생성 모두).
    ///   · 대신 맞기: 우치가 최대 체력 15% 이상인 한 방을 맞으면 분신 1기가 사라지며 그 피해를 무효화한다(한 적 행동당 1기, 우치만).
    /// 분신이 사라지는 모든 경우는 사망 판정이다 — 아누비스의 영혼이 0.25씩 찬다.
    /// </summary>
    public sealed class UchiTaoist : UniquePassiveCode, ISummonerPolicy
    {
        public const string CloneKind = "uchi_clone";
        private const int CloneCap = 3;
        private const float CloneSpeed = 70f;
        private const float GuardHpRatio = 0.15f;
        private const float OverflowDamageRatio = 0.5f;

        private static readonly SummonSpec CloneSpec = new()
        {
            Id = 901,
            Kind = CloneKind,
            Name = "분신",
            PortraitKey = "CLONE_PORTRAIT",
            Hp = 1f,
            Speed = CloneSpeed,
            NormalCodeId = UchiCloneStrike.CodeId,
            Targetable = false,
        };

        private bool _subscribed;
        private int _lastGuardActionId = -1;
        private DamageContext _guarded;

        public UchiTaoist(PassiveCodeContext context) : base(context) => CodeName = "도사란 무엇인가";

        public SummonStyle Style => SummonStyle.Swarm;
        public int Cap => CloneCap;
        public SummonSpec Spec => CloneSpec;

        /// <summary>넘침 — 가장 오래된 분신이 자리를 비우며 평소의 절반으로 친다.</summary>
        public void OnOverflow(Unit owner, Unit oldest)
        {
            if (oldest == null) return;
            UchiCloneStrike.StrikeAndVanish(oldest, OverflowDamageRatio);
        }

        public override void CastCode()
        {
            if (Caster == null) return;
            Summons.Register(Caster, this);
            _lastGuardActionId = -1;
            _guarded = null;
            if (_subscribed) return;
            Caster.AddListener<EventContext>(BaseEnums.UnitEventType.OnBeforeDamageTaken, OnBeforeDamageTaken);
            Caster.AddListener<EventContext>(BaseEnums.UnitEventType.OnAfterDamageTaken, OnAfterDamageTaken);
            _subscribed = true;
        }

        public override void StopCode()
        {
            if (Caster != null) Summons.Unregister(Caster);
            if (!_subscribed || Caster == null) return;
            Caster.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnBeforeDamageTaken, OnBeforeDamageTaken);
            Caster.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnAfterDamageTaken, OnAfterDamageTaken);
            _subscribed = false;
        }

        private void OnBeforeDamageTaken(EventContext context)
        {
            DamageContext damage = context?.DmgCtx;
            if (Caster == null || damage == null || damage.IsCancelled || damage.Damage <= 0) return;
            // 지속피해 · 원소 반응은 "한 방"이 아니다.
            if (damage.CodeType == BaseEnums.CodeType.Effect || damage.HasTag(DamageTag.ElementalReaction)) return;
            // 경감 전 값으로 본다 — 피해 계산이 끝나기 전에 막아야 해서 최종 피해를 알 수 없다.
            if (damage.Damage < Caster.HpMax * GuardHpRatio) return;

            int actionId = GameManager.Instance?.ActionScheduler?.CurrentActionId ?? -1;
            if (actionId == _lastGuardActionId) return;
            List<Unit> clones = Summons.Of(Caster);
            if (clones.Count == 0) return;

            _lastGuardActionId = actionId;
            damage.IsCancelled = true;
            _guarded = damage;
            // 대신 맞은 분신은 공격하지 않고 사라진다.
            Effects.Vfx.UchiFx.Guard(clones[0], damage.Attacker);
            clones[0].Die(null);
            Debug($"{Caster.UnitName}의 분신이 {damage.Damage} 피해를 대신 맞았다");
        }

        /// <summary>광역 공격은 같은 피해 정보를 여러 대상에 쓰므로, 우치 몫만 막고 취소를 되돌린다.</summary>
        private void OnAfterDamageTaken(EventContext context)
        {
            if (_guarded == null || !ReferenceEquals(context?.DmgCtx, _guarded)) return;
            _guarded.IsCancelled = false;
            _guarded = null;
        }

        private static void Debug(string message) => UnityEngine.Debug.Log($"[도사란 무엇인가] {message}");
    }
}
