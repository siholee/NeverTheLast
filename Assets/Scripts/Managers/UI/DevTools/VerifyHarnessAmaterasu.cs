#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Normal;
using Codes.Passive;
using Codes.Ultimate;
using Combat;
using Core;
using Effects.Buffs;
using Entities;
using UnityEngine;
using Kind = Managers.ActionScheduler.ActionKind;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioAmaterasuCore(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); _cfActionIdSequence = 0;
            bool enabled = true;
            yield return YvPrepareScene(config, v => enabled = v);
            DebugMode.AllyInvincible = false; DebugMode.EnemyInvincible = false;
            Unit sun = CfAlly(40), ally = CfAlly(83), enemy = AliveEnemies().First();
            void Check(string label, bool ok, string value = "") => YvCheck("아마테라스 " + label, "현행 리워크 사양", value, ok);
            if (sun == null || ally == null) { Check("편성", false); OsFinish(enabled); yield break; }
            var passive = sun.ActivePassiveCodes.OfType<AmaterasuRisingSun>().Single();
            var ultimate = (AmaterasuDaybreak)sun.ActiveUltimateCode;
            if (AliveEnemies().Count == 1)
            {
                Unit spare = GridManager.Instance.SpawnUnit(2, 4, true, enemy.ID);
                Check("다수 적 fixture 생성", spare != null);
                YvBoostEnemy(spare);
            }
            foreach (Unit unit in AliveAllies())
            {
                CfNoCrit(unit); unit.ClearUltimateResource();
                if (unit != sun) foreach (var code in unit.ActivePassiveCodes) code.StopCode();
                if (unit.ActiveUltimateCode?.IsPersistent == true) unit.ActiveUltimateCode.StopCode();
            }
            foreach (Unit foe in AliveEnemies()) { foreach (var code in foe.ActivePassiveCodes) code.StopCode(); foe.ClearUltimateResource(); }
            CfEnsureEffectHit(sun, AliveEnemies());
            void SetStacks(int value) { sun.AddCombatResource(AmaterasuRisingSun.ResourceKey, -passive.Stacks); sun.AddCombatResource(AmaterasuRisingSun.ResourceKey, value); }
            int serial = 0;
            IEnumerator Extra(Unit actor, Action hit, bool priority = false)
            {
                yield return NordAction(actor, priority ? Kind.PriorityAdditional : Kind.Additional, s =>
                {
                    if (priority) s.EnqueuePriorityAdditional(actor, "sun_probe_" + (++serial), "일광 검증", hit);
                    else s.EnqueueAdditional(actor, "sun_probe_" + (++serial), "일광 검증", hit);
                });
            }
            Check("등록·마나형 공격 초월기", sun.ActiveNormalCode is AmaterasuSunVolley && ultimate.IsAutoCast && ultimate.SupportsUltimateDamage && sun.ManaMax == 100 && sun.UltimateResourceType == BaseEnums.UltimateResourceType.Mana);
            Check("기존 원화·추천·용어", Helpers.SpriteResource.LoadPortrait("AMATERASU_PORTRAIT") != null && SynergyCatalog.RecommendationFor(40)?.best?.members?.Contains(122) == true && UI.Core.GameGuideCatalog.Find("sunlight") != null);
            float speed = sun.StatValue(BaseEnums.StatType.Speed);
            using (var log = new CfLog(AliveAllies()))
            {
                yield return NordAction(sun, Kind.Normal, s => s.AdvanceAction(sun, 1));
                var hits = log.Dealt.Where(x => x.Attacker == sun).ToList();
                Check("일반100%1발·마나20·일광0", hits.Count == 1 && hits[0].DamageContext.Damage == Mathf.RoundToInt(sun.StatValue(BaseEnums.StatType.Attack)) && sun.ManaCurr == 20 && passive.Stacks == 0);
                Check("일반 불 부착 없음·완료1회", !log.Elements.Any(x => x.Source == sun) && log.NormalResolved.GetValueOrDefault(sun) == 1);
            }
            sun.ClearUltimateResource();
            using (var log = new CfLog(AliveAllies()))
            {
                yield return NordAction(ally, Kind.Special, s => s.EnqueueSpecial(ally, "sun_command", "지휘 사격 검증", () =>
                {
                    ally.isCasting = true;
                    ally.StartCoroutine(CfFixtureCommand(sun.ActiveNormalCode, enemy, ally));
                }));
                var hit = log.Dealt.Single(x => x.Attacker == sun);
                Check("지휘50%·마나/일광/일반완료 없음", Near(hit.DamageContext.OutgoingDamageMultiplier, .5f) && sun.ManaCurr == 0 && passive.Stacks == 0 && log.NormalResolved.GetValueOrDefault(sun) == 0);
            }
            CfHit(ally, enemy, CfAdditionalTags);
            Check("스케줄러 밖 태그 호출 제외", passive.Stacks == 0);
            enemy.AddShield(1000, enemy);
            yield return Extra(ally, () => { CfHit(ally, enemy, CfAdditionalTags); CfHit(ally, enemy, CfAdditionalTags); foreach (var foe in AliveEnemies()) CfHit(ally, foe, CfAdditionalTags); });
            Check("보호막·다단·광역 행동당2·마나 불변", passive.Stacks == 2 && sun.ManaCurr == 0 && enemy.ShieldCurr < 1000, passive.Stacks.ToString());
            enemy.RemoveShield(enemy.ShieldCurr);
            yield return Extra(ally, () => CfHit(ally, enemy, CfAdditionalTags), true);
            Check("우선 추가공격+2", passive.Stacks == 4);
            yield return Extra(sun, () => CfHit(sun, enemy, CfAdditionalTags));
            yield return Extra(ally, () => CfHit(ally, enemy, new List<int> { DamageTag.AdditionalAttack, DamageTag.BonusDamage }));
            yield return Extra(ally, () => CfHit(ally, enemy, CfAdditionalTags, BaseEnums.CodeType.Effect));
            yield return Extra(ally, () => CfHit(ally, enemy, new List<int> { DamageTag.AdditionalAttack, DamageTag.ElementalReaction }));
            yield return Extra(ally, () => { });
            yield return NordAction(ally, Kind.Special, s => s.EnqueueSpecial(ally, "sun_special", "특수 제외", () => CfHit(ally, enemy, CfAdditionalTags)));
            Check("자신·덧붙음·지속·반응·비공격·특수 제외", passive.Stacks == 4, passive.Stacks.ToString());
            for (int i = 0; i < 6; i++) yield return Extra(ally, () => CfHit(ally, enemy, CfAdditionalTags));
            Check("상한12·속도 불변", passive.Stacks == 12 && Near(speed, sun.StatValue(BaseEnums.StatType.Speed)));
            // 모든 문턱과 자투리 소멸은 실제 초월기를 실행해 검사한다.
            foreach (int stacks in new[] { 0, 2, 3, 5, 6, 8, 9, 11, 12 })
            {
                SetStacks(stacks); sun.ClearUltimateResource();
                foreach (var foe in AliveEnemies()) { foe.ResetCombatElements(); foe.ClearUltimateResource(); }
                using (var log = new CfLog(AliveAllies()))
                {
                    yield return NordAction(sun, Kind.Ultimate, s => sun.FillUltimateResource());
                    var hits = log.Dealt.Where(x => x.Attacker == sun && x.DamageContext.CodeType == BaseEnums.CodeType.Ultimate).ToList();
                    int expected = 1 + stacks / 3;
                    Check("중첩별 탄수·전량 소비 " + stacks, hits.Count == expected && ultimate.LastHits == expected && ultimate.LastSpentStacks == stacks && passive.Stacks == 0 && ultimate.CompletedNormally, hits.Count.ToString());
                    Check("탄별160%·초월·독립 컨텍스트 " + stacks, hits.All(x => x.DamageContext.Damage == Mathf.RoundToInt(sun.StatValue(BaseEnums.StatType.Attack) * 1.6f) && x.DamageContext.HasTag(DamageTag.UltAttack) && !x.DamageContext.HasTag(DamageTag.AdditionalAttack)) && hits.Select(x => x.DamageContext).Distinct().Count() == expected);
                    if (AliveEnemies().Count > 1) Check("직전 적 외 바운스 " + stacks, hits.Zip(hits.Skip(1), (a, b) => a.Target != b.Target).All(x => x));
                    Check("탄별 불 부착 " + stacks, log.Elements.Count(x => x.Source == sun) == expected);
                }
            }
            // 첫 적중이 취소되면 스냅샷을 반환한다. 마나는 기존 공통 시전 규칙대로 소비한다.
            SetStacks(12); sun.ClearUltimateResource();
            Action<EventContext> cancel = evt => { if (evt.DmgCtx?.Attacker == sun) evt.DmgCtx.IsCancelled = true; };
            foreach (var foe in AliveEnemies()) foe.AddListener(BaseEnums.UnitEventType.OnBeforeDamageTaken, cancel);
            try
            {
                yield return NordAction(sun, Kind.Ultimate, s => sun.FillUltimateResource());
                Check("첫 적중 취소 일광 반환·완료 없음", passive.Stacks == 12 && sun.ManaCurr == 0 && ultimate.LastHits == 0 && !ultimate.CompletedNormally);
            }
            finally { foreach (var foe in AliveEnemies()) foe.RemoveListener(BaseEnums.UnitEventType.OnBeforeDamageTaken, cancel); }
            // 유효 첫 적중 이후 제어에는 잔탄을 멈추고 소비 자원을 돌려주지 않는다.
            using (var log = new CfLog(AliveAllies()))
            {
                log.OnHit = hit => { if (hit.Attacker == sun) Effects.Negative.ControlStatuses.ApplyFixedStun(sun, enemy, 1, effectHitResolved: true); };
                yield return NordAction(sun, Kind.Ultimate, s => sun.FillUltimateResource());
                Check("첫 적중 후 제어 잔탄 중지·일광 소비", ultimate.LastHits == 1 && passive.Stacks == 0 && sun.isControlled);
            }
            sun.RemoveAllNegativeStatuses();
            // 첫 대상의 실제 사망 뒤 남은 탄은 다른 생존 적에게 이전한다.
            Unit victim = AliveEnemies().Last();
            YvAddTestStatus(victim, 99843, "sun_dying_priority", new TargetPriorityModifierEffect(9999));
            victim.ModifyHp(1); SetStacks(12); sun.ClearUltimateResource();
            using (var log = new CfLog(AliveAllies()))
            {
                yield return NordAction(sun, Kind.Ultimate, s => sun.FillUltimateResource());
                var hits = log.Dealt.Where(x => x.Attacker == sun && x.DamageContext.CodeType == BaseEnums.CodeType.Ultimate).ToList();
                Check("대상 사망 후 잔탄 재선택", hits.Count == 5 && hits[0].Target == victim && !victim.isActive && hits.Skip(1).All(x => x.Target != victim));
            }
            // 단일 적 집중과 독립 치명. 다른 적은 실제 사망시켜 남은 탄의 재선택도 확인한다.
            foreach (var foe in AliveEnemies().Where(x => x != enemy).ToList()) foe.Die(sun);
            foreach (var unit in AliveAllies()) unit.ClearUltimateResource();
            sun.RemoveStatusByKey("verify_cf_nocrit");
            YvAddTestStatus(sun, 99840, "amatera_crit", new StatModifierEffect(BaseEnums.StatType.CritRate, 1f));
            SetStacks(12);
            using (var log = new CfLog(AliveAllies()))
            {
                yield return NordAction(sun, Kind.Ultimate, s => sun.FillUltimateResource());
                var hits = log.Dealt.Where(x => x.Attacker == sun && x.DamageContext.CodeType == BaseEnums.CodeType.Ultimate).ToList();
                Check("보스1기5발 집중·치명", hits.Count == 5 && hits.All(x => x.Target == enemy && x.DamageContext.IsCrit));
            }
            // 물/얼음을 먼저 공급했을 때 반응은 첫 탄만 증폭하며 후속 탄에 재료를 복제하지 않는다.
            foreach (var element in new[] { BaseEnums.UnitElement.Hydro, BaseEnums.UnitElement.Cryo })
            {
                enemy.ResetCombatElements(); enemy.GrantCombatElement(element, source: enemy);
                SetStacks(6); sun.ClearUltimateResource(); enemy.ClearUltimateResource();
                using (var log = new YvRecorder())
                {
                    yield return NordAction(sun, Kind.Ultimate, s => sun.FillUltimateResource());
                    Check("증폭 재료 소비 " + element, log.Reactions.Count(x => x.Source == sun && (x.Name == "증발" || x.Name == "융해")) == 1 && log.Dealt.Count(x => x.Attacker == sun && x.DamageContext.CodeType == BaseEnums.CodeType.Ultimate) == 3);
                }
            }
            // 충전 배율은 일광을 늘리지 않는다. 과충전은 덜 모인 상태에서도 초월기를 연다.
            SetStacks(2); sun.ClearUltimateResource();
            YvAddTestStatus(sun, 99842, "amatera_energy", new ManaEfficiencyEffect(6f));
            yield return CfRun(s => sun.RecoverMana(20), () => ultimate.CompletedNormally && sun.ManaCurr == 0);
            Check("과충전 저중첩 자동1발", ultimate.LastSpentStacks == 2 && ultimate.LastHits == 1 && passive.Stacks == 0);
            sun.RemoveStatusByKey("amatera_energy");
            passive.StopCode();
            yield return Extra(ally, () => CfHit(ally, enemy, CfAdditionalTags));
            Check("정지 구독 정리", passive.Stacks == 0);
            passive.CastCode(); passive.CastCode();
            yield return Extra(ally, () => CfHit(ally, enemy, CfAdditionalTags));
            Check("재시작 중복 구독 없음", passive.Stacks == 2);
            // 시전자 사망 이후 잔탄·부착·완료가 남지 않는다.
            SetStacks(12); sun.ClearUltimateResource();
            using (var log = new CfLog(AliveAllies()))
            {
                log.OnHit = hit => { if (hit.Attacker == sun && sun.isActive) sun.Die(enemy); };
                yield return NordAction(sun, Kind.Ultimate, s => sun.FillUltimateResource());
                Check("시전자 사망 잔탄·완료 없음", log.Dealt.Count(x => x.Attacker == sun && x.DamageContext.CodeType == BaseEnums.CodeType.Ultimate) == 1 && !ultimate.CompletedNormally && passive.Stacks == 0);
            }
            OsFinish(enabled);
        }
        private IEnumerator ScenarioAmaterasuCombat(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();
            yield return PrepareStage(config, Stage(config, 6), false, false, false, 4);
            int shots = 0, volleys = 0, stacked = 0;
            void Hit(DamageResolvedContext ctx) { if (ctx.Attacker?.ID == 40 && ctx.DamageContext.CodeType == BaseEnums.CodeType.Ultimate) shots++; }
            void StartAction(Unit unit, Kind kind, string label)
            {
                if (unit?.ID != 40 || kind != Kind.Ultimate) return;
                volleys++; if (unit.GetCombatResource(AmaterasuRisingSun.ResourceKey) >= 3) stacked++;
            }
            Unit.AnyDamageDealt += Hit; ActionScheduler.AnyActionStarted += StartAction;
            try
            {
                DebugMode.SuiteRunning = false;
                GameManager.Instance.StartRound();
                yield return WaitForState(BaseEnums.GameState.RoundEnd, 120);
                YvCheck("아마테라스 자연 전투 종료", "RoundEnd", GameManager.Instance.gameState.ToString(), GameManager.Instance.gameState == BaseEnums.GameState.RoundEnd);
                YvCheck("아마테라스 자연 중첩·바운스", "중첩 초월기1회 이상·탄수>초월기수", $"shots={shots}, volleys={volleys}, stacked={stacked}", stacked > 0 && shots > volleys);
            }
            finally { Unit.AnyDamageDealt -= Hit; ActionScheduler.AnyActionStarted -= StartAction; }
            OsFinish(true);
        }
    }
}
#endif
