using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Vfx;
using Entities;
using Managers;

namespace Codes.Normal
{
    /// <summary>
    /// 사바흐 — 평소에는 단일 검격, 일반행동 세 번째마다 최대 세 명을 긋는 번개 베기로 대체한다.
    /// 대상 수·번개 부착 박자는 옛 구현 그대로이며 피해 스탯만 DEX에서 공격력으로 기계적으로 옮겼다.
    /// </summary>
    public sealed class SabahLightningSlash : BaseNormalCode
    {
        public override int AttackForm => DamageTag.Slash;

        private const int SubstituteInterval = 3;
        private const int SubstituteTargetCount = 3;
        private int _actionCount;
        private bool _substitute;
        private bool _multiDelivered;
        private bool _resetRegistered;

        public SabahLightningSlash(NormalCodeContext context) : base(context)
        {
            CodeName = "검격";
            CastingDelay = 0.10f;
        }

        protected override bool IsContact => true;

        // 스케줄러 공지는 CastCode보다 먼저 나가므로 다음 횟수를 미리 보아 대체행동을 구분한다.
        public override bool IsSubstituteAction => (_actionCount + 1) % SubstituteInterval == 0;
        public override string AnnouncementName => IsSubstituteAction ? "번개 베기" : "검격";

        public override void CastCode()
        {
            RegisterRoundReset();
            _actionCount++;
            _substitute = _actionCount % SubstituteInterval == 0;
            _multiDelivered = false;
            CodeName = _substitute ? "번개 베기" : "검격";
            base.CastCode();
        }

        protected override int BaseDamage(Unit target)
            => StatDamage(110f, BaseEnums.StatType.Attack, 1f);

        protected override List<Unit> SelectTarget()
            => _substitute
                ? GetAvailableEnemies()
                    .OrderByDescending(unit => unit.Priority)
                    .ThenBy(unit => unit.HpCurr)
                    .Take(SubstituteTargetCount)
                    .ToList()
                : base.SelectTarget();

        protected override IEnumerator Deliver(Unit target)
        {
            if (_substitute)
            {
                if (_multiDelivered) yield break;
                _multiDelivered = true;
                List<Unit> targets = TargetUnits.Where(unit => unit != null && unit.isActive).ToList();
                var contexts = targets.ToDictionary(unit => unit, BuildContext);
                AudioManager.PlayAction(Caster, SfxActionSlot.Normal, SfxCuePhase.Draw, CodeName);
                yield return SabahFx.LightningSweep(Caster, targets, hitTarget =>
                {
                    if (hitTarget == null || !contexts.TryGetValue(hitTarget, out DamageContext hitContext)) return;
                    Hit(hitTarget, hitContext);
                });
                yield break;
            }

            DamageContext context = BuildContext(target);
            AudioManager.PlayAction(Caster, SfxActionSlot.Normal,
                VfxStyleSettings.UseHandpaint(VfxStyleCharacter.Sabah)
                    ? SfxCuePhase.Draw
                    : SfxCuePhase.Projectile,
                CodeName);
            yield return SabahFx.ShadowAmbush(Caster, target, () => Hit(target, context));
        }

        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            if (!_substitute || target == null || !target.isActive) return;
            target.GrantCombatElement(BaseEnums.UnitElement.Electro, Unit.CommonElementAuraDuration, Caster);
        }

        protected override List<int> GetDamageTags() => new()
        {
            _substitute ? DamageTag.MultiTarget : DamageTag.SingleTarget,
            DamageTag.NormalAttack,
            DamageTag.ContactAttack,
            DamageTag.Slash,
        };

        private void RegisterRoundReset()
        {
            if (_resetRegistered || Caster == null) return;
            Action<EventContext> reset = _ => _actionCount = 0;
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundStart, reset);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, reset);
            _resetRegistered = true;
        }
    }
}
