using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Entities;

namespace Combat
{
    /// <summary>직접 공격의 대상 확정 직후, 공지와 연출 전에 한 번만 대상 목록을 바꾼다.</summary>
    public static class AttackTargetRouter
    {
        private static readonly HashSet<string> HandledPreAttackKeys = new(StringComparer.Ordinal);

        public sealed class RedirectEvent
        {
            public Unit Attacker;
            public Unit Protected;
            public Unit RedirectedTo;
            public string AttackKey;
            public int ActionId;
            internal IAttackTargetInterceptor Interceptor;
        }

        public static event Action<RedirectEvent> TargetRedirected;

        public static List<Unit> Resolve(Unit attacker, IReadOnlyList<Unit> selected,
            BaseEnums.CodeType codeType, IReadOnlyList<int> damageTags, string attackKey = null)
            => Prepare(attacker, selected, codeType, damageTags, attackKey).Targets;

        /// <summary>
        /// 경호 교체를 먼저 확정하고, 그 결과를 기준으로 선제 개입을 기다릴 수 있는 공격 해석을 만든다.
        /// 실제 공격 코드는 <see cref="AttackTargetResolution.ResolvePreAttack"/> 뒤에 공지와 연출을 연다.
        /// </summary>
        public static AttackTargetResolution Prepare(Unit attacker, IReadOnlyList<Unit> selected,
            BaseEnums.CodeType codeType, IReadOnlyList<int> damageTags, string attackKey = null)
        {
            var result = selected?.Where(unit => unit != null && unit.isActive).ToList()
                         ?? new List<Unit>();
            bool directAttack = IsDirectAttack(attacker, codeType, damageTags);
            int actionId = Managers.GameManager.Instance?.ActionScheduler?.CurrentActionId ?? -1;
            if (!directAttack || result.Count == 0)
                return new AttackTargetResolution(attacker, result, codeType, damageTags,
                    attackKey, actionId, directAttack);

            var context = new AttackTargetRoutingContext(attacker, result, codeType, damageTags,
                attackKey, actionId);
            var redirects = new List<RedirectEvent>();
            for (int i = 0; i < result.Count; i++)
            {
                Unit original = result[i];
                if (original == null || !original.isActive) continue;
                foreach (PassiveCode passive in original.ActivePassiveCodes)
                {
                    if (passive is not IAttackTargetInterceptor interceptor) continue;
                    Unit replacement = interceptor.Redirect(context, original);
                    if (replacement == null || replacement == original || !replacement.isActive) continue;
                    result[i] = replacement;
                    var redirect = new RedirectEvent
                    {
                        Attacker = attacker,
                        Protected = original,
                        RedirectedTo = replacement,
                        AttackKey = attackKey,
                        ActionId = actionId,
                        Interceptor = interceptor,
                    };
                    redirects.Add(redirect);
                    TargetRedirected?.Invoke(redirect);
                    break;
                }
            }
            return new AttackTargetResolution(attacker, result, codeType, damageTags,
                attackKey, actionId, directAttack, redirects);
        }

        internal static bool TryMarkPreAttackHandled(AttackTargetRoutingContext context, Unit owner)
        {
            if (context == null || owner == null) return false;
            string key = $"{context.ActionId}:{context.AttackKey}:{owner.GetInstanceID()}";
            return HandledPreAttackKeys.Add(key);
        }

        /// <summary>라운드마다 행동 ID가 다시 시작하므로 중복 키도 함께 비운다.</summary>
        public static void ResetRound() => HandledPreAttackKeys.Clear();

        private static bool IsDirectAttack(Unit attacker, BaseEnums.CodeType codeType,
            IReadOnlyList<int> tags)
        {
            if (attacker == null || !attacker.isActive || codeType == BaseEnums.CodeType.Effect) return false;
            if (tags == null) return true;
            return !tags.Contains(DamageTag.PassiveAttack) &&
                   !tags.Contains(DamageTag.ElementalReaction) &&
                   !tags.Contains(DamageTag.BonusDamage);
        }
    }

    /// <summary>대상 교체 결과와 그 뒤에 실행할 선제 개입 단계를 한 묶음으로 보관한다.</summary>
    public sealed class AttackTargetResolution
    {
        private readonly AttackTargetRoutingContext _context;
        private readonly bool _directAttack;
        private readonly IReadOnlyList<AttackTargetRouter.RedirectEvent> _redirects;

        public List<Unit> Targets { get; }
        public bool CanContinue => _context.Attacker != null && _context.Attacker.isActive &&
                                   !_context.Attacker.isControlled && Targets.Any(unit => unit != null && unit.isActive);

        internal AttackTargetResolution(Unit attacker, List<Unit> targets, BaseEnums.CodeType codeType,
            IReadOnlyList<int> damageTags, string attackKey, int actionId, bool directAttack,
            IReadOnlyList<AttackTargetRouter.RedirectEvent> redirects = null)
        {
            Targets = targets ?? new List<Unit>();
            _context = new AttackTargetRoutingContext(attacker, Targets, codeType, damageTags,
                attackKey, actionId);
            _directAttack = directAttack;
            _redirects = redirects ?? Array.Empty<AttackTargetRouter.RedirectEvent>();
        }

        /// <summary>
        /// 최종 대상에 붙은 선제 개입을 한 행동당 한 번 실행한다. 공격자를 쓰러뜨리거나 제어하면
        /// 호출자는 <see cref="CanContinue"/>를 보고 원래 공지·연출·피해를 열지 않는다.
        /// </summary>
        public IEnumerator ResolvePreAttack()
        {
            if (!_directAttack || Targets.Count == 0) yield break;
            Managers.ActionScheduler scheduler = Managers.GameManager.Instance?.ActionScheduler;
            if (scheduler == null) yield break;

            bool previewed = false;
            foreach (AttackTargetRouter.RedirectEvent redirect in _redirects)
            {
                if (redirect?.Interceptor is not IRedirectInterventionVfx visual) continue;
                if (!previewed)
                {
                    yield return Effects.Vfx.CardMotion.InterruptionWindup(_context.Attacker,
                        redirect.Protected ?? redirect.RedirectedTo);
                    previewed = true;
                }
                Effects.BattlefieldBackdrop.ClearFocus();
                yield return new UnityEngine.WaitForSeconds(0.10f);
                yield return visual.PlayIntervention(_context, redirect.Protected, redirect.RedirectedTo);
            }

            foreach (Unit target in Targets.Where(unit => unit != null && unit.isActive).Distinct().ToList())
            {
                foreach (PassiveCode passive in target.ActivePassiveCodes.ToList())
                {
                    if (passive is not IPreAttackInterceptor interceptor ||
                        !interceptor.CanIntercept(_context, target) ||
                        !AttackTargetRouter.TryMarkPreAttackHandled(_context, target)) continue;

                    if (!previewed)
                    {
                        yield return Effects.Vfx.CardMotion.InterruptionWindup(_context.Attacker, target);
                        previewed = true;
                    }
                    Effects.BattlefieldBackdrop.ClearFocus();
                    yield return new UnityEngine.WaitForSeconds(0.10f);

                    yield return scheduler.RunPreAttackInterrupt(target, interceptor.InterruptLabel,
                        () => interceptor.Intercept(_context, target));
                    if (!CanContinue) yield break;
                }
            }

            if (previewed && CanContinue)
            {
                Unit resumedTarget = Targets.FirstOrDefault(unit => unit != null && unit.isActive);
                UnityEngine.Vector3 from = Effects.Vfx.VfxSpace.WorldPoint(_context.Attacker);
                UnityEngine.Vector3 to = Effects.Vfx.VfxSpace.WorldPoint(resumedTarget);
                Effects.BattlefieldBackdrop.PushFocus(0.46f, 0.04f, 0.16f, 0.16f,
                    new UnityEngine.Color(0.12f, 0.20f, 0.31f), from, to);
                yield return new UnityEngine.WaitForSeconds(0.08f);
            }
        }
    }

    public sealed class AttackTargetRoutingContext
    {
        public readonly Unit Attacker;
        public readonly IReadOnlyList<Unit> SelectedTargets;
        public readonly BaseEnums.CodeType CodeType;
        public readonly IReadOnlyList<int> DamageTags;
        public readonly string AttackKey;
        public readonly int ActionId;

        public AttackTargetRoutingContext(Unit attacker, IReadOnlyList<Unit> selectedTargets,
            BaseEnums.CodeType codeType, IReadOnlyList<int> damageTags, string attackKey, int actionId)
        {
            Attacker = attacker;
            SelectedTargets = selectedTargets;
            CodeType = codeType;
            DamageTags = damageTags;
            AttackKey = attackKey;
            ActionId = actionId;
        }
    }

    public interface IAttackTargetInterceptor
    {
        Unit Redirect(AttackTargetRoutingContext context, Unit originalTarget);
    }

    /// <summary>대상 교체가 판정되기 전에 패시브 공지와 가로막기 동작을 시간축에 끼우는 시각 계약.</summary>
    public interface IRedirectInterventionVfx
    {
        IEnumerator PlayIntervention(AttackTargetRoutingContext context, Unit protectedUnit, Unit redirectedTo);
    }

    /// <summary>대상 교체가 끝난 뒤, 원래 공격의 공지·연출·피해보다 먼저 해결되는 행동.</summary>
    public interface IPreAttackInterceptor
    {
        string InterruptLabel { get; }
        bool CanIntercept(AttackTargetRoutingContext context, Unit finalTarget);
        IEnumerator Intercept(AttackTargetRoutingContext context, Unit finalTarget);
    }
}
