#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BaseClasses;
using Codes.Checkpoints;
using Combat;
using Core;
using Effects.Buffs;
using Entities;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private static readonly Dictionary<string, int[]> Checkpoint48Lineups = new()
        {
            ["b48_aoe"] = new[] { 81, 120, 2, 64, 82 },
            ["b48_single"] = new[] { 67, 161, 22, 83, 82 },
            ["b48_execute"] = new[] { 2, 25, 60, 1, 82 },
            ["b48_tank"] = new[] { 121, 2, 3, 66, 82 },
            ["b48_counter"] = new[] { 7, 122, 22, 160, 82 },
            ["b48_summon"] = new[] { 125, 20, 124, 81, 64 },
            ["b48_summon_balanced"] = new[] { 125, 20, 124, 67, 161 },
            ["b48_theatre"] = new[] { 180, 10, 20, 124, 67 },
            ["b48_dot"] = new[] { 123, 61, 45, 66, 82 },
            ["b48_lokapala"] = new[] { 63, 61, 66, 64, 60 },
            ["b48_treasury"] = new[] { 65, 200, 220, 64, 82 },
        };

        private static VoidLegionCommand B48Command(Unit boss)
            => boss.ActivePassiveCodes.OfType<VoidLegionCommand>().Single();
        private static List<Unit> B48Escorts() => GridManager.Instance.enemyList
            .Where(u => u != null && u.IsOnField && u.ID == Checkpoint48.EscortId).ToList();
        private static void B48Kill(Unit target, Unit source)
            => target.TakeDamage(new DamageContext(source, 1000000, CodeType.Effect, new() { DamageTag.TrueDamage }));
        private IEnumerator B48WaitCast(Unit unit)
        {
            float deadline = Time.realtimeSinceStartup + 10;
            while (unit != null && unit.isCasting && Time.realtimeSinceStartup < deadline) yield return null;
            RuiCheck("48층 실제 시전 완료", "watchdog 이전 종료", unit?.isCasting.ToString(), unit == null || !unit.isCasting);
        }

        private IEnumerator ScenarioCheckpoint48Layout(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();
            yield return PrepareStage(config, 41, false, false, false, 4);
            var game = GameManager.Instance;
            var data = game.dataManager.FetchStageThemeDataList();
            RuiCheck("고정 보스 24·48 등록", "중복 없음", string.Join(",", data.fixedBossStages.Select(x => x.stage)),
                data.fixedBossStages.Count == 2 && data.fixedBossStages.Any(x => x.stage == 24 && x.bossId == 3201) &&
                data.fixedBossStages.Any(x => x.stage == 48 && x.bossId == Checkpoint48.BossId));
            foreach (var theme in data.stageThemes.Where(t => t.enabled))
            {
                game.DebugResetBattle(); DebugMode.ForcedThemeId = theme.id; game.DebugLoadStage(41);
                bool correct = true;
                for (int floor = 41; floor <= 48; floor++)
                {
                    var wanted = floor is 47 or 48 ? StageKind.Boss : floor == 45 ?
                        (theme.isGeneric ? StageKind.Elite : StageKind.MidBoss) : floor == 44 && !theme.isGeneric ? StageKind.Event : StageKind.Normal;
                    correct &= game.RoundManager.GetStageKind(floor) == wanted;
                }
                RuiCheck(theme.name + " 6스테이지 예고", "사건44·엘리트45·테마보스47·고정48", correct.ToString(), correct);
                foreach (int floor in new[] { 45, 47, 48 })
                {
                    game.DebugResetBattle(); game.DebugLoadStage(floor);
                    int expected = floor == 45 ? theme.midBossId : floor == 47 ? theme.bossId : Checkpoint48.BossId;
                    RuiCheck(theme.name + " 실제 " + floor, expected.ToString(), string.Join(",", AliveEnemies().Select(u => u.ID)),
                        AliveEnemies().Any(u => u.ID == expected) && (floor != 48 || AliveEnemies().Count == 3 && B48Escorts().Count == 2));
                }
                RuiCheck(theme.name + " 7스테이지 보존", "54엘리트·56보스", game.RoundManager.GetStageKind(56).ToString(),
                    game.RoundManager.GetStageKind(54) == (theme.isGeneric ? StageKind.Elite : StageKind.MidBoss) && game.RoundManager.GetStageKind(56) == StageKind.Boss);
                yield return null;
            }
            Unit boss = AliveEnemies().Single(u => u.ID == Checkpoint48.BossId);
            RuiCheck("48층 본체·호위 스탯", "11000/620/250/230/72·600/160", boss.HpMax + "/" + boss.AtkCurr,
                boss.Level == 48 && boss.HpMax == 11000 && boss.AtkCurr == 620 && boss.DefCurr == 250 && boss.SpDefCurr == 230 &&
                boss.SpeedCurr == 72 && B48Escorts().All(u => u.HpMax == 600 && u.SpAtkCurr == 160));
            RuiCheck("48층 전후열·자원·원화", "전열호위2/후열본체·지휘3·이미지", boss.currentCell.xPos.ToString(),
                boss.currentCell.xPos == 2 && B48Escorts().All(u => u.currentCell.xPos == 1) && boss.ManaMax == 3 &&
                boss.UltimateResourceType == UltimateResourceType.Stack && AliveEnemies().All(u => u.currentCell.portraitRenderer.sprite != null));
            var saved = RunManager.Instance.CaptureDebugSnapshot();
            game.DebugLoadStage(49); RunManager.Instance.RestoreDebugSnapshot(saved);
            RuiCheck("48층 준비 저장/복원", "본체1·호위2·48층", game.RoundManager.Stage.ToString(),
                game.RoundManager.Stage == 48 && AliveEnemies().Count == 3 && B48Escorts().Count == 2);
            yield return WaitUnscaled(1.2f);
            yield return CaptureStill("checkpoint48_preparation");
            DebugMode.ForcedThemeId = 0; game.DebugResetBattle(); RuiFinish();
        }

        private IEnumerator ScenarioCheckpoint48Core(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();
            yield return PrepareStage(config, 48, false, false, false, 4);
            var game = GameManager.Instance;
            game.enabled = false; // 직접 코드의 경계를 시험하는 동안 자동 스케줄러만 멈춘다.
            game.StartRound();
            var boss = AliveEnemies().Single(u => u.ID == Checkpoint48.BossId);
            var command = B48Command(boss);
            var tank = AliveAllies()[0];
            tank.AddStatus(BuffStatus.Create(994800, "verify48_priority", "검증 우선도", tank, tank,
                new TargetPriorityModifierEffect(1000), isBeneficial: true));
            foreach (int _ in Enumerable.Range(0, 8))
                boss.TakeDamage(new DamageContext(tank, 1, CodeType.Normal, new() { DamageTag.NormalAttack, DamageTag.SingleTarget }));
            RuiCheck("다단/피격 지휘 충전 없음", "0", boss.ManaCurr.ToString(), boss.ManaCurr == 0);
            boss.AddStatus(BuffStatus.Create(994803, "verify48_boss_hit", "검증 명중", boss, boss,
                new StatModifierEffect(StatType.SpecialAttack, tank.SpDefCurr * 2 - boss.SpAtkCurr + 10), isBeneficial: true));
            foreach (int n in new[] { 1, 2, 3 })
            {
                boss.ActiveNormalCode.CastCode(); yield return B48WaitCast(boss);
                RuiCheck("지휘 일반 완료 " + n, n.ToString(), boss.ManaCurr.ToString(), boss.ManaCurr == n);
                RuiCheck("일반 우선도 존중", tank.UnitName, boss.currentNormalTarget?.UnitName, boss.currentNormalTarget == tank);
            }
            RuiCheck("본체 일반 공격도 실제 쇠약 부여", "우선도 대상에 쇠약", tank.HasStatusKey(Checkpoint48.FrailtyKey).ToString(),
                tank.HasStatusKey(Checkpoint48.FrailtyKey));
            boss.RemoveStatusByKey("verify48_boss_hit");
            Unit survivor = B48Escorts()[1];
            survivor.ModifyHp(survivor.HpMax - 200);
            int oldHp = survivor.HpCurr;
            B48Kill(B48Escorts()[0], tank);
            boss.CastUltimateCode(); yield return B48WaitCast(boss); yield return WaitUnscaled(.35f);
            RuiCheck("첫 초월기 빈1기 보충", "잔존HP 유지·총2·추가1·지휘0", B48Escorts().Count.ToString(),
                command.ResolvedUltimates == 1 && command.AddedEscorts == 1 && B48Escorts().Count == 2 && survivor.HpCurr == oldHp && boss.ManaCurr == 0);
            Unit fresh = B48Escorts().First(u => u != survivor);
            // 실제 양수 Tick으로 동기화한다. 다른 행동은 예고 단계에 둬 합류만 분리한다.
            var scheduler = game.ActionScheduler;
            scheduler.EnqueueSpecial(tank, "verify48.join", "합류 동기화 확인", () => {});
            for (int i = 0; i < 3; i++) { scheduler.Tick(.001f); yield return null; }
            float progress = scheduler.ActionProgress(fresh);
            RuiCheck("새 호위 즉시 공격 없음", "양수 Tick3회·일반 완료0·AV1미만", fresh.TurnCount + "/" + progress, fresh.TurnCount == 0 && progress < 1f);
            // 미등록 유닛도 조회값0이므로 공개 AV변경으로 등록을 확인한 뒤 원복한다.
            float probeAdvance = Mathf.Min(.1f, (1f - progress) * .5f);
            scheduler.AdvanceAction(fresh, probeAdvance);
            float registeredProgress = scheduler.ActionProgress(fresh);
            scheduler.DelayAction(fresh, probeAdvance);
            RuiCheck("새 호위 실제 스케줄러 등록", "앞당김 반영·기존 AV로 원복", registeredProgress + "/" + scheduler.ActionProgress(fresh),
                registeredProgress > progress && Mathf.Abs(registeredProgress - progress - probeAdvance) < .001f && Mathf.Abs(scheduler.ActionProgress(fresh) - progress) < .001f);
            RuiCheck("새 호위 전열 좌표", "전열 열1·행1/2", fresh.currentCell.xPos + "/" + fresh.currentCell.yPos,
                fresh.currentCell.xPos == 1 && fresh.currentCell.yPos is 1 or 2);
            RuiCheck("새 호위 필드와 레이아웃", "실제 필드·원화 표시", fresh.IsOnField.ToString(),
                fresh.IsOnField && !fresh.IsSummon && fresh.currentCell.gameObject.activeInHierarchy && fresh.currentCell.portraitRenderer.sprite != null);
            foreach (Unit escort in B48Escorts()) B48Kill(escort, tank);
            boss.FillUltimateResource(); boss.CastUltimateCode(); yield return B48WaitCast(boss);
            RuiCheck("둘째 초월기 빈2기 보충", "추가총3·활성2", command.AddedEscorts.ToString(), command.ResolvedUltimates == 2 && command.AddedEscorts == 3 && B48Escorts().Count == 2);
            foreach (Unit escort in B48Escorts()) B48Kill(escort, tank);
            foreach (Unit ally in AliveAllies()) ally.RemoveStatusByKey(Checkpoint48.FrailtyKey);
            boss.FillUltimateResource(); boss.CastUltimateCode(); yield return B48WaitCast(boss);
            RuiCheck("셋째 초월기 보충 종료", "광역만·호위0·추가3", B48Escorts().Count.ToString(), command.ResolvedUltimates == 3 && command.AddedEscorts == 3 && B48Escorts().Count == 0);
            RuiCheck("본체 광역에 쇠약 없음", "전원 미부여", AliveAllies().Count(u => u.HasStatusKey(Checkpoint48.FrailtyKey)).ToString(),
                AliveAllies().All(u => !u.HasStatusKey(Checkpoint48.FrailtyKey)));
            yield return CaptureStill("checkpoint48_after_reinforcement");

            // 둘째 개전은 보충 기회가 모두 살아 있는 호위로 소진되는 경계를 확인한다.
            game.DebugResetBattle(); game.DebugLoadStage(48); game.StartRound();
            boss = AliveEnemies().Single(u => u.ID == Checkpoint48.BossId); command = B48Command(boss); tank = AliveAllies()[0];
            Unit shooter = B48Escorts()[0];
            for (int i = 0; i < 2; i++) { boss.FillUltimateResource(); boss.CastUltimateCode(); yield return B48WaitCast(boss); }
            RuiCheck("살아 있는 호위 보충 미저장", "2회 소진·추가0", command.ResolvedUltimates + "/" + command.AddedEscorts,
                command.ResolvedUltimates == 2 && command.AddedEscorts == 0);

            tank.ModifyHp(tank.HpMax - 600);
            shooter.AddStatus(BuffStatus.Create(994801, "verify48_hit", "검증 명중", shooter, shooter,
                new StatModifierEffect(StatType.SpecialAttack, tank.SpDefCurr * 2 - shooter.SpAtkCurr + 10), isBeneficial: true));
            RuiCheck("쇠약 실제 부여", "해로운 상태1", Checkpoint48.ApplyFrailty(shooter, tank).ToString(), tank.HasStatusKey(Checkpoint48.FrailtyKey));
            Checkpoint48.ApplyFrailty(B48Escorts()[1], tank); Checkpoint48.ApplyFrailty(shooter, tank);
            RuiCheck("복수 시전자 쇠약 비중첩", "상태1·효과2", tank.GetAllStatuses().Count(s => s.Key == Checkpoint48.FrailtyKey).ToString(),
                tank.GetAllStatuses().Count(s => s.Key == Checkpoint48.FrailtyKey) == 1 && tank.GetAllStatuses().Single(s => s.Key == Checkpoint48.FrailtyKey).Effects.Count == 2);
            int before = tank.HpCurr; tank.Heal(100);
            RuiCheck("쇠약 치유 감소", "75", (tank.HpCurr - before).ToString(), tank.HpCurr - before == 75);
            tank.Invoke(UnitEventType.OnNormalActionResolved, new EventContext(tank));
            RuiCheck("쇠약 일반1회 유지", "남은1", tank.GetAllStatuses().Single(s => s.Key == Checkpoint48.FrailtyKey).RemainingTurns.ToString(),
                tank.GetAllStatuses().Single(s => s.Key == Checkpoint48.FrailtyKey).RemainingTurns == 1);
            Checkpoint48.ApplyFrailty(shooter, tank);
            RuiCheck("쇠약 성공 갱신", "남은2", tank.GetAllStatuses().Single(s => s.Key == Checkpoint48.FrailtyKey).RemainingTurns.ToString(),
                tank.GetAllStatuses().Single(s => s.Key == Checkpoint48.FrailtyKey).RemainingTurns == 2);
            shooter.RemoveStatusByKey("verify48_hit");
            var oldFrailty = tank.GetAllStatuses().Single(s => s.Key == Checkpoint48.FrailtyKey);
            tank.Invoke(UnitEventType.OnNormalActionResolved, new EventContext(tank));
            tank.AddStatus(BuffStatus.Create(994804, "verify48_reject", "검증 저항", tank, tank,
                new StatModifierEffect(StatType.SpecialDefense, 10000000), isBeneficial: true));
            UnityEngine.Random.InitState(48);
            Checkpoint48.ApplyFrailty(shooter, tank);
            RuiCheck("쇠약 부여 실패가 기존 기간을 늘리지 않음", "같은 상태·남은1", oldFrailty.RemainingTurns.ToString(),
                oldFrailty == tank.GetAllStatuses().Single(s => s.Key == Checkpoint48.FrailtyKey) && oldFrailty.RemainingTurns == 1);
            tank.RemoveStatusByKey("verify48_reject");
            tank.Invoke(UnitEventType.OnNormalActionResolved, new EventContext(tank));
            RuiCheck("쇠약 일반2회 만료", "없음", tank.HasStatusKey(Checkpoint48.FrailtyKey).ToString(), !tank.HasStatusKey(Checkpoint48.FrailtyKey));
            shooter.RemoveStatusByKey("verify48_hit");
            float originalChance = DamageMitigation.EffectHitChance(shooter, tank);
            tank.AddStatus(BuffStatus.Create(994802, "verify48_resist", "검증 특방", tank, tank,
                new StatModifierEffect(StatType.SpecialDefense, 1000), isBeneficial: true));
            float resistantChance = DamageMitigation.EffectHitChance(shooter, tank);
            RuiCheck("특방 효과 명중 감소", "명중률 감소", originalChance + " -> " + resistantChance, resistantChance < originalChance * .5f);
            tank.RemoveStatusByKey("verify48_resist");
            shooter.AddStatus(BuffStatus.Create(994801, "verify48_hit", "검증 명중", shooter, shooter,
                new StatModifierEffect(StatType.SpecialAttack, tank.SpDefCurr * 2 - shooter.SpAtkCurr + 10), isBeneficial: true));
            Checkpoint48.ApplyFrailty(shooter, tank);
            Unit shielder = AliveAllies()[1];
            RuiCheck("정화 보상 실제 장착", "810", "810", shielder.TryGrantRewardCode(810)); shielder.CastPassiveCode();
            tank.AddShield(50, shielder);
            RuiCheck("실제 보호막 정화", "쇠약 제거", tank.HasStatusKey(Checkpoint48.FrailtyKey).ToString(), !tank.HasStatusKey(Checkpoint48.FrailtyKey));
            Checkpoint48.ApplyFrailty(shooter, tank);
            tank.Heal(50, shielder);
            RuiCheck("실제 치유 정화", "쇠약 제거", tank.HasStatusKey(Checkpoint48.FrailtyKey).ToString(), !tank.HasStatusKey(Checkpoint48.FrailtyKey));
            Checkpoint48.ApplyFrailty(shooter, tank);
            B48Kill(shooter, tank);
            RuiCheck("시전자 죽어도 쇠약 유지", "만료/정화 전 존속", tank.HasStatusKey(Checkpoint48.FrailtyKey).ToString(), tank.HasStatusKey(Checkpoint48.FrailtyKey));
            int kills = game.KillCount;
            Unit other = B48Escorts().Single(); other.GrantToughness(30, tank);
            var breakHit = new DamageContext(tank, 1, CodeType.Normal, new() { DamageTag.SingleTarget, DamageTag.NormalAttack });
            other.TakeDamage(breakHit);
            RuiCheck("가상 처치가 호위 자리를 비우지 않음", "실제 격파·살아 있음·경제 그대로", other.IsOnField + "/" + breakHit.ToughnessBroken,
                breakHit.ToughnessBroken && other.IsOnField && game.KillCount == kills && B48Escorts().Count == 1);
            boss.FillUltimateResource(); boss.CastUltimateCode(); yield return B48WaitCast(boss);
            RuiCheck("비었던 보충 기회 이월 없음", "활성1·추가0", B48Escorts().Count.ToString(), B48Escorts().Count == 1 && command.AddedEscorts == 0);
            B48Kill(boss, tank);
            RuiCheck("본체 사망 뒤 보충 금지", "추가0", command.CompleteUltimate().ToString(), command.AddedEscorts == 0);
            game.DebugResetBattle(); game.DebugLoadStage(48); game.StartRound();
            boss = AliveEnemies().Single(u => u.ID == Checkpoint48.BossId); command = B48Command(boss); tank = AliveAllies()[0];
            int lateHits = 0;
            void LateHit(DamageResolvedContext context) { if (context.Attacker == boss) lateHits++; }
            Unit.AnyDamageDealt += LateHit;
            boss.FillUltimateResource(); boss.CastUltimateCode(); yield return WaitUnscaled(.03f);
            B48Kill(boss, tank); yield return B48WaitCast(boss); yield return WaitUnscaled(.8f);
            Unit.AnyDamageDealt -= LateHit;
            RuiCheck("초월기 도중 본체 사망", "후속 타격·보충 없음", command.ResolvedUltimates + "/" + command.AddedEscorts + "/hits" + lateHits,
                command.ResolvedUltimates == 0 && command.AddedEscorts == 0 && B48Escorts().Count == 2 && lateHits == 0);
            var recommendation = SynergyCatalog.RecommendationFor(125);
            RuiCheck("소환 코어 UI 추천과 보스 화력 보완", "피그말리온·토트·수리야·나폴레옹", string.Join(',', recommendation.best.members),
                recommendation.best.members.SequenceEqual(new[] { 20, 124, 67, 161 }) && recommendation.basic.members.SequenceEqual(recommendation.best.members));
            game.DebugResetBattle(); game.enabled = true; RuiFinish();
        }

        private static int[] B48Build(Unit unit, string mode)
        {
            int[] ids = unit.ID switch
            {
                1 or 125 => new[] { 502, 503, 603, 624, 613, 705 },
                121 or 83 => new[] { 502, 602, 603, 601, 701, 704 },
                82 or 60 => new[] { 502, 602, 623, 606, 625, 701 },
                200 => new[] { 502, 503, 603, 624, 606, 625 },
                64 or 45 or 124 => new[] { 502, 505, 605, 606, 602, 613 },
                120 => new[] { 502, 505, 605, 606, 602, 623 },
                65 => new[] { 502, 508, 608, 606, 728, 730 },
                220 => new[] { 501, 601, 607, 608, 631, 723 },
                61 or 123 => new[] { 501, 601, 505, 605, 617, 711 },
                180 or 20 => new[] { 501, 601, 619, 607, 608, 704 },
                10 => new[] { 502, 505, 605, 606, 619, 602 },
                67 or 161 => new[] { 501, 601, 607, 608, 724, 711 },
                160 => new[] { 501, 601, 508, 608, 606, 623 },
                25 => new[] { 501, 601, 605, 606, 633, 711 },
                66 => new[] { 502, 503, 603, 624, 605, 606 },
                81 => new[] { 501, 601, 602, 607, 608, 723 },
                7 or 22 => new[] { 501, 601, 602, 607, 725, 704 },
                122 or 2 => new[] { 501, 601, 607, 608, 724, 711 },
                _ => new[] { 501, 601, 607, 608, 723, 711 },
            };
            if (mode == "sdef") { ids[0] = 504; ids[1] = 604; }
            if (mode == "cleanse" && unit.ID is 82 or 66) ids[2] = 810;
            if (mode == "tank" && unit.ID == 121) ids[3] = 629;
            if (mode == "treasury" && unit.ID is 200 or 82 or 64) { ids[4] = 728; ids[5] = 730; }
            return ids.Distinct().ToArray();
        }

        private IEnumerator ScenarioCheckpoint48Battle(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();
            GameManager.Instance.life = 20;
            GameManager.Instance.KillCount = 0;
            yield return BuildFreshRun(ResolveLineup(config));
            yield return PrepareStage(config, 48, false, false, false, 4);
            var game = GameManager.Instance;
            int seed = config.stage > 0 ? config.stage : 4801;
            UnityEngine.Random.InitState(seed);
            string mode = config.trigger ?? "baseline";
            var party = AliveAllies().ToList();
            foreach (Unit ally in party)
            {
                if (mode != "unbuilt")
                    RuiCheck(ally.UnitName + " 실제 보상 지급", "6개 이하·상하위 충돌 없음", string.Join(",", B48Build(ally, mode)),
                        B48Build(ally, mode).All(id => ally.TryGrantRewardCode(id)));
                ally.ModifyHp(Mathf.RoundToInt(ally.HpMax * .75f));
            }
            var boss = AliveEnemies().Single(u => u.ID == Checkpoint48.BossId);
            var command = B48Command(boss);
            int debuffs = 0, allyDeaths = 0, escortKills = 0, bossHp = boss.HpCurr, turns = 0;
            var victims = new Dictionary<int, int>();
            var damage = new Dictionary<int, int>();
            void Dealt(DamageResolvedContext context)
            {
                if (context.Attacker == null || context.Attacker.IsEnemy || context.Target == null || !context.Target.IsEnemy) return;
                int id = context.Attacker.IsSummon ? -context.Attacker.ID : context.Attacker.ID;
                damage[id] = damage.GetValueOrDefault(id) + context.DamageDealt;
            }
            void Applied(Unit source, Unit target, Entities.Status.UnitStatus status)
            {
                if (status.Key != Checkpoint48.FrailtyKey || target.IsEnemy) return;
                debuffs++; victims[target.ID] = victims.GetValueOrDefault(target.ID) + 1;
            }
            void Died(Unit target, Unit source)
            {
                if (!target.IsEnemy && !target.IsSummon) allyDeaths++;
                if (target.IsEnemy && target.ID == Checkpoint48.EscortId) escortKills++;
            }
            Unit.AnyNegativeStatusGranted += Applied; Unit.AnyUnitDied += Died; Unit.AnyDamageDealt += Dealt;
            float start = Time.realtimeSinceStartup, deadline = start + 180;
            try
            {
                DebugMode.SuiteRunning = false; game.StartRound();
                yield return WaitUnscaled(.7f);
                yield return CaptureStill("checkpoint48_entry_" + ResolveLineup(config) + "_" + mode + "_" + seed);
                while (game.gameState == GameState.RoundInProgress && Time.realtimeSinceStartup < deadline)
                { bossHp = boss.HpCurr; turns = game.ActionScheduler.TurnsTaken; yield return null; }
            }
            finally
            {
                DebugMode.SuiteRunning = true;
                Unit.AnyNegativeStatusGranted -= Applied; Unit.AnyUnitDied -= Died; Unit.AnyDamageDealt -= Dealt;
            }
            bool won = CombatLog.Current.LastOrDefault(e => e.Text.StartsWith("전투 종료 — ")).Text?.Contains("전투 종료 — 승리") == true;
            string actual = $"seed={seed},lineup={ResolveLineup(config)},mode={mode},level={config.partyLevel},won={won},turns={turns},bossHp={bossHp},seconds={Time.realtimeSinceStartup-start:F1},ult={command.ResolvedUltimates},spawn={command.AddedEscorts},escortKills={escortKills},debuff={debuffs},allyDeaths={allyDeaths},victims={string.Join(',',victims.Select(p=>$"{p.Key}:{p.Value}"))}";
            File.AppendAllText(Path.Combine(_requestDirectory, "checkpoint48-balance.txt"), actual + Environment.NewLine);
            File.AppendAllText(Path.Combine(_requestDirectory, "checkpoint48-damage.txt"), actual + ";damage=" + string.Join(',', damage.Select(p => $"{p.Key}:{p.Value}")) + Environment.NewLine);
            RuiCheck("48층 밸런스 " + ResolveLineup(config) + "/" + mode,
                mode == "unbuilt" ? "부족한 빌드 대조 결과 기록" : "유효 빌드 승리", actual,
                game.gameState != GameState.RoundInProgress && (mode == "unbuilt" || won));
            RuiCheck("48층 보충 상한", "추가4 이하·처치6 이하", command.AddedEscorts + "/" + escortKills,
                command.AddedEscorts <= 4 && escortKills <= 6);
            yield return CaptureStill("checkpoint48_result_" + ResolveLineup(config) + "_" + mode + "_" + seed);
            game.DebugResetBattle(); RuiFinish();
        }
    }
}
#endif
