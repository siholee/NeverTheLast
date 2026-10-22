using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Managers;

namespace Codes.Passive
{
    /// <summary>
    /// 인드라 — 특성 「데바의 왕」(Detail_08 §13.1). 필드에 있는 동안 아군 전원의 <b>특수행동 피해 +25%</b>,
    /// 다른 아군이 특수행동을 마칠 때마다 인드라 마나 +10. 전투 개시에는 마나 최소40%를 준비한다.
    ///
    /// 로카팔라에 묶지 않았다 — 시의 처치 재행동처럼 <c>SpecialAttack</c> 태그를 단 피해라면 출처를 묻지 않는다.
    /// </summary>
    public sealed class IndraDevaKing : UniquePassiveCode
    {
        private const int AuraStatusId = 70630;
        private const string AuraKey = "indra_deva_king";
        private const float SpecialDamageBonus = 1.25f;
        private const int ManaPerAllySpecial = 10;
        public const float OpeningManaFloor = .40f;

        private bool _subscribed;
        private bool _openingApplied;

        public IndraDevaKing(PassiveCodeContext context) : base(context) => CodeName = "데바의 왕";

        public override void CastCode()
        {
            if (Caster == null) return;
            // 첫 묶음을 일반전 예산 안에 준비한다. 더 많이 보유한 마나는 보존하며 재시전으로 재충전하지 않는다.
            if (!_openingApplied && Caster.IsOnField && GameManager.Instance?.gameState == BaseEnums.GameState.RoundInProgress)
            {
                _openingApplied = true;
                int floor = UnityEngine.Mathf.RoundToInt(Caster.ManaMax * OpeningManaFloor);
                Caster.AddUltimateResource(UnityEngine.Mathf.Max(0, floor - Caster.ManaCurr));
            }
            foreach (Unit ally in FieldAllies())
            {
                ally.AddStatus(BuffStatus.Create(AuraStatusId, AuraKey, CodeName, Caster, ally,
                    new DevaKingEffect(Caster), duration: 0,
                    category: BaseEnums.StatusCategory.Neutral,
                    description: "인드라가 필드에 있는 동안 특수행동 피해 +25%."));
            }

            if (_subscribed) return;
            ActionScheduler.AnyActionFinished += OnAnyActionFinished;
            _subscribed = true;
        }

        public override void StopCode()
        {
            if (GameManager.Instance?.gameState != BaseEnums.GameState.RoundInProgress) _openingApplied = false;
            if (_subscribed) ActionScheduler.AnyActionFinished -= OnAnyActionFinished;
            _subscribed = false;
            GridManager grid = GridManager.Instance;
            if (grid == null || Caster == null) return;
            List<Unit> pool = Caster.IsEnemy ? grid.enemyList : grid.heroList;
            foreach (Unit ally in pool.Where(unit => unit != null)) ally.RemoveStatusByKey(AuraKey);
        }

        /// <summary>묶음이 클수록 다음 개방을 앞당긴다 — 특수행동 하나당 인드라 마나 +10(효율 적용).</summary>
        private void OnAnyActionFinished(Unit unit, ActionScheduler.ActionKind kind)
        {
            if (kind != ActionScheduler.ActionKind.Special || Caster == null || !Caster.isActive || !Caster.IsOnField) return;
            if (unit == null || unit == Caster || unit.IsSummon || unit.IsEnemy != Caster.IsEnemy) return;
            Caster.RecoverMana(ManaPerAllySpecial);
            Effects.Vfx.LokapalaFx.ResourceGained(Caster);
        }

        private List<Unit> FieldAllies()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || Caster == null) return new List<Unit>();
            List<Unit> pool = Caster.IsEnemy ? grid.enemyList : grid.heroList;
            return pool.Where(unit => unit != null && unit.isActive && unit.IsOnField).ToList();
        }

        private sealed class DevaKingEffect : BaseEffect
        {
            private readonly Unit _indra;
            public DevaKingEffect(Unit indra) : base(0) => _indra = indra;

            public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            {
                if (attacker != Target || context == null || !context.HasTag(DamageTag.SpecialAttack)) return 1f;
                return _indra != null && _indra.isActive && _indra.IsOnField ? SpecialDamageBonus : 1f;
            }
        }
    }
}
