using System.Linq;
using BaseClasses;
using Codes.Base;
using Entities;
using Managers;

namespace Codes.Passive
{
    public sealed class MarieSupply : UniquePassiveCode
    {
        private bool _working;
        public MarieSupply(PassiveCodeContext context) : base(context) => CodeName = "보급은 약속이다";
        public override void CastCode()
        {
            if (_working) return;
            _working = true;
            ActionScheduler.AnyActionFinished += Finished;
        }
        public override void StopCode()
        { _working = false; ActionScheduler.AnyActionFinished -= Finished; }
        private void Finished(Unit actor, ActionScheduler.ActionKind kind)
        {
            if (!_working || Caster == null || !Caster.isActive || !Caster.IsOnField || actor == null ||
                !actor.isActive || !actor.IsOnField || actor.IsEnemy != Caster.IsEnemy) return;
            actor.Heal(UnityEngine.Mathf.RoundToInt(Caster.StatValue(BaseEnums.StatType.Attack) * .15f), Caster);
        }
        public static float AdditionalCritDamage(Unit attacker)
        {
            ActionScheduler scheduler = GameManager.Instance?.ActionScheduler;
            if (attacker == null || scheduler == null || scheduler.ExecutingUnit != attacker || !ActionScheduler.IsAdditional(scheduler.ActingKind) || GridManager.Instance == null) return 0;
            return GridManager.Instance.heroList.Concat(GridManager.Instance.enemyList)
                .Where(x => x != null && x.isActive && x.IsOnField && x.IsEnemy == attacker.IsEnemy)
                .SelectMany(x => x.ActivePassiveCodes.OfType<MarieSupply>())
                .Where(x => x._working).Select(x => x.Caster.StatValue(BaseEnums.StatType.CritDamage) * .40f).DefaultIfEmpty(0).Max();
        }
    }
}
