using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Effects.Vfx;
using Entities;
using Entities.Status;
using Managers;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>히미코 — 집중 2턴. 적 행동마다 맹화를 찍고 아군 전체가 방어력을 20% 무시한다.</summary>
    public sealed class HimikoBlazeFocus : BaseUltimateCode
    {
        private const int DurationTurns = 2;
        private const int MarkerStatusId = 70442;
        private const int AuraStatusId = 70443;
        private const string ConcentrationKey = "himiko_blaze_focus";
        private const string MarkerKey = "himiko_blaze_focus_marker";
        private const string AuraKey = "himiko_blaze_focus_aura";
        private bool _subscribed;

        public HimikoBlazeFocus(UltimateCodeContext context) : base(context) => CodeName = "맹화의 제전";

        protected override IEnumerator Perform()
        {
            if (Caster == null) yield break;

            Concentration.Begin(Caster, ConcentrationKey, DurationTurns, _ => Cleanup());
            ActionScheduler.AnyActionStarted += OnActionStarted;
            _subscribed = true;

            List<Unit> allies = AllAllies();

            UnitStatus marker = BuffStatus.Create(
                MarkerStatusId, MarkerKey, "집중: 맹화의 제전", Caster, Caster,
                new MarkerBuffEffect(), duration: 0,
                category: BaseEnums.StatusCategory.Neutral,
                description: "적이 행동할 때마다 그 적에게 맹화를 부여합니다.");
            marker.AffectedVfxPartId = StatusAffectedVfx.SuppressPartId;
            Caster.AddStatus(marker);
            foreach (Unit ally in allies)
            {
                UnitStatus aura = BuffStatus.Create(
                    AuraStatusId, AuraKey, "맹화의 시야", Caster, ally,
                    new DefenseIgnoreEffect(0.80f), duration: 0,
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    isBeneficial: true,
                    description: "히미코의 집중 동안 적의 방어력을 20% 무시합니다.");
                aura.AffectedVfxPartId = StatusAffectedVfx.SuppressPartId;
                ally.AddStatus(aura);
            }

            AudioManager.PlayAction(Caster, SfxActionSlot.Ultimate, SfxCuePhase.Screen, CodeName);
            Caster.StartCoroutine(CardMotion.Channel(Caster, 0.12f, 0.46f, 0.20f));
            Unit directionTarget = FirstEnemy();
            VfxComposer.PlayScreen("himiko.blaze_focus", Caster, directionTarget);
            VfxComposer.PlaySlot("himiko.blaze_focus", "cast", 0, Caster, directionTarget,
                VfxSpace.WorldPoint(Caster), VfxSpace.SortingLayer(Caster));
            // 상태 판정은 위에서 즉시 유지하고, 수신 연출만 시전광 정점 뒤에 연다.
            yield return new WaitForSeconds(0.14f);
            foreach (Unit ally in allies)
                if (ally != null && ally != Caster && ally.isActive)
                    StatusAffectedVfx.PlaySupport(ally, Caster, "hit.support.buff");
            AudioManager.PlayAction(Caster, SfxActionSlot.Ultimate, SfxCuePhase.Hit, CodeName);
            yield return new WaitForSeconds(0.22f);
        }

        private void OnActionStarted(Unit unit, ActionScheduler.ActionKind kind, string label)
        {
            if (!_subscribed || Caster == null || !Caster.isActive || unit == null || !unit.isActive) return;
            if (unit.IsEnemy == Caster.IsEnemy || !unit.IsOnField) return;
            Caster.ActivePassiveCodes.OfType<HimikoBlaze>().FirstOrDefault()?.ApplyBlaze(unit);
        }

        private void Cleanup()
        {
            if (_subscribed) ActionScheduler.AnyActionStarted -= OnActionStarted;
            _subscribed = false;
            if (Caster != null) Caster.RemoveStatusByKey(MarkerKey);
            foreach (Unit ally in AllAllies()) ally.RemoveStatusByKey(AuraKey);
        }

        private List<Unit> AllAllies()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || Caster == null) return new List<Unit>();
            List<Unit> pool = Caster.IsEnemy ? grid.enemyList : grid.heroList;
            return pool.Where(unit => unit != null && unit.IsEnemy == Caster.IsEnemy).ToList();
        }

        private Unit FirstEnemy()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || Caster == null) return null;
            List<Unit> pool = Caster.IsEnemy ? grid.heroList : grid.enemyList;
            return pool.FirstOrDefault(unit => unit != null && unit.isActive && unit.IsOnField);
        }

        private sealed class DefenseIgnoreEffect : BaseEffect
        {
            private readonly float _multiplier;
            public DefenseIgnoreEffect(float multiplier) : base(0) => _multiplier = Mathf.Clamp01(multiplier);
            public override bool IsBeneficial => true;
            public override float DefenseStatMultiplierModifier(Unit attacker, Unit target, DamageContext context)
                => attacker == Target ? _multiplier : 1f;
        }
    }
}
