using System.Linq;
using BaseClasses;
using Codes.Base;
using Entities;
using Managers;

namespace Codes.Passive
{
    public sealed class TsukuyomiNight : UniquePassiveCode
    {
        private bool _working;
        public TsukuyomiNight(PassiveCodeContext context) : base(context) => CodeName = "달이 보지 않는 밤";
        public override void CastCode() => _working = true;
        public override void StopCode() => _working = false;
        public static float DotMultiplier(Unit source)
        {
            if (source == null || GridManager.Instance == null) return 1;
            float bonus = GridManager.Instance.heroList.Concat(GridManager.Instance.enemyList)
                .Where(x => x != null && x.isActive && x.IsOnField && x.IsEnemy == source.IsEnemy)
                .SelectMany(x => x.ActivePassiveCodes.OfType<TsukuyomiNight>()).Where(x => x._working)
                .Select(x => .20f * x.Caster.StatValue(BaseEnums.StatType.SpecialAttack) / (100f + 6f * (x.Caster.Level - 1)))
                .DefaultIfEmpty(0).Max();
            return 1f + bonus;
        }
    }
}
