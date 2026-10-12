using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Entities;

namespace Codes.Ultimate
{
    public sealed class TsukuyomiMoonlitPact : BaseUltimateCode
    {
        private readonly List<Unit> _buffed = new();
        private System.Action<EventContext> _normal;
        private int _remaining;
        private System.Action<EventContext> _end;
        private System.Action<Unit, Unit> _death;
        private float _selfBonus;
        private string Key => $"tsukuyomi_pact_{Caster.GetEntityId()}";
        public TsukuyomiMoonlitPact(UltimateCodeContext context) : base(context) => CodeName = "달 아래의 계약";
        protected override IEnumerator Perform()
        {
            float amount = UnityEngine.Mathf.Max(0, Caster.StatValue(BaseEnums.StatType.SpecialAttack) - _selfBonus) * .20f;
            ClearBuff();
            // 광역 아군 버프는 공격 대상 지정 가능 여부와 무관하며 독립 소환수도 포함한다.
            var grid = Managers.GridManager.Instance;
            foreach (Unit ally in grid.heroList.Concat(grid.enemyList).Concat(grid.summonList).Distinct()
                         .Where(unit => unit != null && unit.isActive && unit.IsOnField && unit.IsEnemy == Caster.IsEnemy))
            {
                ally.AddStatus(BuffStatus.Create(70451, Key, "달 아래의 계약", Caster, ally,
                    new PactEffect(amount), duration: 0, isBeneficial: true));
                _buffed.Add(ally);
                if (ally == Caster) _selfBonus = amount;
            }
            _remaining = 2;
            _normal = _ => { if (--_remaining <= 0) ClearBuff(); };
            Caster.AddListener(BaseEnums.UnitEventType.OnNormalActionResolved, _normal);
            _end = _ => ClearBuff();
            _death = (unit, attacker) => { if (unit == Caster) ClearBuff(); };
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _end);
            Unit.AnyUnitDied += _death;
            foreach (Unit enemy in AliveEnemies()) ActionDot.Apply(Caster, enemy, ActionDotKind.Curse);
            Effects.Vfx.VfxComposer.PlayScreen("tsukuyomi.ultimate", Caster, Caster);
            Managers.AudioManager.PlayCue("tsukuyomi.ultimate.resolve");
            yield return new UnityEngine.WaitForSeconds(.25f);
        }
        private void ClearBuff()
        {
            if (_normal != null) Caster?.RemoveListener(BaseEnums.UnitEventType.OnNormalActionResolved, _normal);
            if (_end != null) Caster?.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _end);
            if (_death != null) Unit.AnyUnitDied -= _death;
            _end = null; _death = null;
            foreach (Unit ally in _buffed) if (ally != null) ally.RemoveStatusByKey(Key);
            _buffed.Clear(); _selfBonus = 0; _normal = null;
        }
        // 시전 종료의 StopCode와 별개로 버프는 일반행동 2회 뒤 걷힌다. 라운드 정리는 공통 상태 정리를 따른다.
        private sealed class PactEffect : BaseEffect
        {
            private readonly float _amount;
            public PactEffect(float amount) : base(0, amount) => _amount = amount;
            public override float StatFlatModifier(Unit unit, BaseEnums.StatType stat) =>
                Caster != null && Caster.isActive && Caster.IsOnField && stat == BaseEnums.StatType.SpecialAttack ? _amount : 0;
        }
    }
}
