#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using BaseClasses;
using Entities;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        // 실제 피해/유효 회복/실제 부여 보호막만 읽는다. 소환 피해는 기존 DamageMeter가 소환자에게 합산한다.
        private sealed class IntentBattleMetrics : IDisposable
        {
            public long Taken, Healing, Shielding;
            public IntentBattleMetrics()
            { Unit.AnyDamageDealt += Damage; Unit.AnyHealed += Heal; Unit.AnyShieldGranted += Shield; }
            private void Damage(DamageResolvedContext context)
            { if (context.Target != null && !context.Target.IsEnemy) Taken += context.DamageDealt; }
            private void Heal(Unit target, Unit source, int effective, int overflow)
            { if (target != null && !target.IsEnemy) Healing += effective; }
            private void Shield(Unit target, Unit source, int amount)
            { if (target != null && !target.IsEnemy) Shielding += amount; }
            public void Dispose()
            { Unit.AnyDamageDealt -= Damage; Unit.AnyHealed -= Heal; Unit.AnyShieldGranted -= Shield; }
        }
    }
}
#endif
