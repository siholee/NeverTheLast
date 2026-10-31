using System.Linq;
using BaseClasses;
using Codes.Base;
using Entities;
using Managers;

namespace Codes.Passive
{
    public sealed class TsukuyomiNight : UniquePassiveCode
    {
        public const float OpeningShieldRatio = 3f;
        public override int BaseShieldAmount => StatDamage(0, BaseEnums.StatType.Defense, OpeningShieldRatio);
        private bool _working;
        private bool _shieldGranted;
        private System.Action<EventContext> _start, _cleanup;
        public TsukuyomiNight(PassiveCodeContext context) : base(context) => CodeName = "달이 보지 않는 밤";
        public override void CastCode()
        {
            if (_working || Caster == null) return;
            _working = true; _shieldGranted = false;
            _start = _ => OpeningShield(); _cleanup = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundStart, _start);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanup);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanup);
            OpeningShield();
        }
        private void OpeningShield()
        {
            if (!_working || _shieldGranted || Caster == null || !Caster.isActive || !Caster.IsOnField) return;
            _shieldGranted = true;
            ShieldParty(Caster, OpeningShieldRatio);
        }
        public static void ShieldParty(Unit caster, float ratio)
        {
            var grid = GridManager.Instance;
            if (grid == null || caster == null || !caster.isActive || !caster.IsOnField) return;
            int amount = UnityEngine.Mathf.Max(0, UnityEngine.Mathf.RoundToInt(caster.StatValue(BaseEnums.StatType.Defense) * ratio));
            foreach (Unit ally in grid.heroList.Concat(grid.enemyList).Concat(grid.summonList).Distinct()
                         .Where(x => x != null && x.isActive && x.IsOnField && x.IsEnemy == caster.IsEnemy))
                ally.AddShield(amount, caster);
        }
        public override void StopCode()
        {
            _working = false;
            if (_start != null) Caster?.RemoveListener(BaseEnums.UnitEventType.OnRoundStart, _start);
            if (_cleanup != null)
            {
                Caster?.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanup);
                Caster?.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanup);
            }
            _start = _cleanup = null;
        }
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
