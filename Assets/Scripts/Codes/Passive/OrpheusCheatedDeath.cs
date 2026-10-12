using BaseClasses;
using Codes.Base;
using Combat;
using Entities;
using Managers;

namespace Codes.Passive
{
    public sealed class OrpheusCheatedDeath : UniquePassiveCode
    {
        public bool IsProviding { get; private set; }
        public OrpheusCheatedDeath(PassiveCodeContext context) : base(context) => CodeName = "죽음마저 속인 자";
        public override void CastCode()
        {
            if (Caster == null || !Caster.isActive || !Caster.IsOnField || IsProviding) return;
            IsProviding = true;
            Caster.AddListener<EventContext>(BaseEnums.UnitEventType.OnDeath, OnDeath);
            if (GridManager.Instance != null)
                foreach (Unit enemy in GridManager.Instance.enemyList) Toughness.Ensure(enemy);
            ActionAnnouncer.AnnouncePassive(Caster, this);
        }
        private void OnDeath(EventContext _) => StopCode();
        public override void StopCode()
        {
            if (!IsProviding) return;
            IsProviding = false;
            Caster?.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnDeath, OnDeath);
            if (GridManager.Instance != null)
                foreach (Unit enemy in GridManager.Instance.enemyList) if (enemy != null && enemy.ToughnessSource == Caster) enemy.ClearToughness();
        }
    }
}
