#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Ultimate;
using Combat;
using Core;
using Entities;
using Effects.Vfx;
using UnityEngine;
namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator QaKillShiAfter(Unit source, Unit enemy, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (source != null && source.isActive) source.Die(enemy);
        }
        private IEnumerator ScenarioCoreShiSafety(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); bool enabled = true;
            yield return YvPrepareScene(config, value => enabled = value);
            DebugMode.AllyInvincible = DebugMode.EnemyInvincible = false;
            Unit source = AliveAllies().First(x => x.ID == ShiId), enemy = AliveEnemies().First();
            CfNoCrit(source); source.RemoveShield(source.ShieldCurr);
            var bell = (ShiFinalBell)source.ActiveUltimateCode;
            bell.CastCode(); source.AddCombatResource(ShiFinalBell.ResourceId, 2 - source.GetCombatResource(ShiFinalBell.ResourceId));
            bool beforeHit = config.name.EndsWith("before_hit", StringComparison.Ordinal);
            bool positive = config.name.EndsWith("positive", StringComparison.Ordinal);
            int hits = 0, bellHits = 0, lateHits = 0, lateNormalEvents = 0, normalEvents = 0; bool finished = false;
            void Damage(DamageResolvedContext hit)
            {
                if (hit.Attacker != source) return;
                if (!source.isActive) lateHits++;
                if (hit.DamageContext.HasTag(DamageTag.BonusDamage)) bellHits++;
                else
                {
                    hits++;
                    if (!positive && !beforeHit && hits == 1) source.Die(enemy);
                }
            }
            void NormalHit(EventContext _) { normalEvents++; if (!source.isActive) lateNormalEvents++; }
            void Finished(Unit actor, ActionScheduler.ActionKind kind) { if (actor == source) finished = true; }
            Unit.AnyDamageDealt += Damage;
            source.AddListener(BaseEnums.UnitEventType.OnNormalAttackHit, (Action<EventContext>)NormalHit);
            ActionScheduler.AnyActionFinished += Finished;
            try
            {
                yield return CfRun(s => s.EnqueueAdditional(source, "qa_shi_death", "검증 멸참", () =>
                {
                    source.CastNormalCode();
                    if (beforeHit) VfxRunner.Run(QaKillShiAfter(source, enemy, .18f));
                }), () => finished, 25f);
                yield return new WaitForSeconds(2f);
                YvCheck("멸참 생존 조건", positive ? "활성" : "비활성", source.isActive.ToString(), source.isActive == positive);
                YvCheck(positive ? "생존 멸참 적중" : "멸참 사망 후 피해 차단", beforeHit ? "타격0" : "멸참1·늦은피해0", hits + "/" + lateHits,
                    hits == (beforeHit ? 0 : 1) && lateHits == 0);
                YvCheck(positive ? "생존 멸참 적중 이벤트" : "멸참 사망 후 적중 이벤트 차단", positive ? "적중 이벤트1·사망 후0" : "이벤트0", normalEvents + "/" + lateNormalEvents, normalEvents == (positive ? 1 : 0) && lateNormalEvents == 0);
                YvCheck("멸참 만종 생존 판정", positive ? "3단계 만종1회" : "만종 피해0", bellHits.ToString(), bellHits == (positive ? 1 : 0));
                yield return CaptureStill("core_shi_death_guard_" + (beforeHit ? "before" : "after"));
            }
            finally
            {
                Unit.AnyDamageDealt -= Damage;
                source.RemoveListener(BaseEnums.UnitEventType.OnNormalAttackHit, (Action<EventContext>)NormalHit);
                ActionScheduler.AnyActionFinished -= Finished; OsFinish(enabled);
            }
        }
    }
}
#endif
