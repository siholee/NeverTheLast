#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Passive;
using Combat;
using Core;
using Entities;
using Managers.UI.Screens;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Managers.UI.DevTools
{
    public sealed partial class DebugVerification
    {
        private IEnumerator IntegrationCoverage()
        {
            yield return AllUnitActions();
            yield return SpecialMechanics();
            yield return LokapalaLifecycle();
            yield return SunlightMechanics();
            yield return PassiveUltimateMechanics();
            yield return DefeatRecovery();
            SaveIntegrationCheckpoint();
            // 에디터 배치 폴러가 체크포인트를 감지할 프레임을 보장한다. 같은 프레임에
            // 새 Game 씬을 다시 로드하면 headless 에디터가 종료 요청 전에 교착될 수 있다.
            yield return null;
            yield return Campaign();
}
        private void SaveIntegrationCheckpoint()
        {
            report.unityVersion = Application.unityVersion;
            report.completedUtc = DateTime.UtcNow.ToString("O");
            report.passed = report.checks.Count(c => c.passed);
            report.failed = report.checks.Count - report.passed;
            string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Logs"));
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "IntegrationVerification-checkpoint.json"), JsonUtility.ToJson(report, true));
        }

        // Every definition executes its real coroutines with all level passives enabled.
        // This is action/lifecycle coverage; numerical contracts are tested separately.
        private IEnumerator AllUnitActions()
        {
            var definitions = game.unitDataList.units.Select(d => (d.id, enemy: false))
                .Concat(game.dataManager.FetchEnemyDataList().enemies
                    .Where(d => d.tags?.Contains("VoidMonster") != true)
                    .Select(d => (d.id, enemy: true))).ToList();
            foreach (var definition in definitions)
                yield return UnitActions(definition.id, definition.enemy);
        }

        private IEnumerator UnitActions(int id, bool isEnemy)
        {
            Status = $"전체 유닛 실제 행동 {id}";
            Clear();
            var unit = isEnemy ? SpawnEnemy(id, 100) : grid.SpawnUnit(-1, 1, false, id);
            unit.DebugSetLevel(100);
            if (isEnemy) { SpawnHero(); SpawnHero(-2, 1); }
            else { SpawnEnemy(1062, 100); SpawnEnemy(1063, 100, 2, 1); }
            var ally = isEnemy ? SpawnEnemy(1062, 100, 1, 2) : grid.SpawnUnit(-2, 1, false, 60);
            ally.ModifyHp(Mathf.Max(1, ally.HpMax / 2));
            foreach (var target in Heroes.Concat(Enemies)) MakeUnavoidable(target);
            grid.OnRoundStart();
            int resolved = 0;
            Action<EventContext> onResolved = _ => resolved++;
            unit.AddListener(UnitEventType.OnNormalActionResolved, onResolved);
            int errors = report.exceptions.Count;
            Assert($"action {id} normal target", unit.ActiveNormalCode.HasValidTarget(), "valid fixture", "checked");
            unit.CastNormalCode();
            yield return Settle(3);
            Assert($"action {id} normal completion", resolved > 0 && !unit.isCasting,
                "resolved and released", $"{resolved}/{unit.isCasting}");
            unit.RemoveListener(UnitEventType.OnNormalActionResolved, onResolved);
            if (!isEnemy && id == 83)
            {
                Equal("Skadi normal fills counter stacks", SkadiNorthernGuardian.MaxStacks,
                    unit.GetCombatResource(SkadiNorthernGuardian.ResourceId));
                Unit attacker = Enemies.FirstOrDefault();
                if (attacker != null)
                {
                    int hpBefore = attacker.HpCurr;
                    game.ActionScheduler.BeginRound();
                    unit.TakeDamage(new DamageContext(attacker, 1, CodeType.Normal,
                        new List<int> { DamageTag.SingleTarget, DamageTag.Physical, DamageTag.NonContactAttack }));
                    Equal("Skadi hit consumes one stack on reservation", SkadiNorthernGuardian.MaxStacks - 1,
                        unit.GetCombatResource(SkadiNorthernGuardian.ResourceId));
                    game.ActionScheduler.Tick(0f);
                    yield return Settle(1f);
                    Assert("Skadi counter damages attacker", attacker.HpCurr < hpBefore,
                        "HP decreased", (hpBefore - attacker.HpCurr).ToString());
                    Assert("Skadi counter applies Cryo", attacker.HasAttachedElement(UnitElement.Cryo),
                        "Cryo attached", attacker.GetCombatElementDisplay());
                    game.ActionScheduler.EndRound();
                }
            }
            // 강한 보스의 일반행동이 표적을 전멸시켰다면 다음 행동용 표적을 다시 준비한다.
            if (!(isEnemy ? Heroes : Enemies).Any())
            {
                foreach (var victim in (isEnemy ? grid.heroList : grid.enemyList).ToList()) grid.RetireUnit(victim);
                if (isEnemy) SpawnHero(); else SpawnEnemy(1062, 100);
            }
            if (id == 3012) unit.AddCombatResource(AswanCombat.AscensionResource, 1);
            if (unit.Chemistry != null)
            {
                // 복합 자원 궁극기: 마나/준비도만 채워서는 발동하지 않는다.
                unit.Chemistry.Receive(ally, ReagentKind.Fuel);
                unit.Chemistry.Receive(ally, ReagentKind.Stabilizer);
                unit.Chemistry.Receive(ally, ReagentKind.Catalyst);
            }
            unit.FillUltimateResource(false);
            bool valid = unit.ActiveUltimateCode.HasValidTarget();
            Assert($"action {id} ultimate target", valid || !unit.ActiveUltimateCode.IsAutoCast,
                "valid target or explicitly passive ultimate", valid.ToString());
            if (unit.ActiveUltimateCode.IsAutoCast) unit.CastUltimateCode();
            yield return Settle(4);
            Assert($"action {id} ultimate releases", !unit.isCasting, "released", unit.isCasting.ToString());
            Equal($"action {id} no runtime errors", errors, report.exceptions.Count);
            Clear();
            yield return null;
        }

        private IEnumerator LokapalaLifecycle()
        {
            foreach (string scenario in new[] { "normal", "controlled", "dead", "duplicate", "round end" })
                yield return SpecialBatch(scenario);
        }

        private IEnumerator SpecialMechanics()
        {
            foreach (int id in Enumerable.Range(60, 8))
            {
                Status = "로카팔라 개별 특수행동 " + id;
                Clear();
                var caster = grid.SpawnUnit(-1, 1, false, id);
                if (id == 63) caster.DebugSetLevel(10);
                var ally = grid.SpawnUnit(-2, 1, false, id == 60 ? 63 : 60);
                var first = SpawnEnemy(1062, 100);
                var second = SpawnEnemy(1063, 100, 2, 1);
                MakeUnavoidable(first); MakeUnavoidable(second);
                ally.ModifyHp(Mathf.Max(1, ally.HpMax / 2));
                int hp = ally.HpCurr, mana = ally.ManaCurr, shield = ally.ShieldCurr, hits = 0;
                foreach (var enemy in new[] { first, second })
                    enemy.AddListener<EventContext>(UnitEventType.OnAfterDamageTaken, ctx =>
                    { if (ctx.DmgCtx?.Attacker == caster && ctx.DmgCtx.CodeType == CodeType.Special) hits++; });
                if (id == 62) first.GrantCombatElement(UnitElement.Pyro, 3, caster);
                if (id == 66) Effects.Negative.ControlStatuses.ApplyAirborne(ally, first);
                caster.CastSpecialCode();
                yield return Settle(2);
                Assert("individual SP " + id + " completes", !caster.isCasting, "released", caster.isCasting.ToString());
                if (id == 60)
                {
                    Assert("Chandra SP party shield", ally.ShieldCurr > shield, "shield gained", ally.ShieldCurr.ToString());
                    Assert("Chandra SP party mana", ally.ManaCurr > mana, "mana gained", ally.ManaCurr.ToString());
                }
                if (id == 61) Equal("Yama SP hits per participant", 2, hits);
                if (id == 62)
                {
                    Equal("Agni SP all targets", 2, hits);
                    Assert("Agni SP special vulnerability", first.ActiveStatuses.Any(s => s.Key == "agni_conflagration"), "present", "checked");
                }
                if (id == 63) Equal("Indra SP bounces without repeat", 2, hits);
                if (id == 65)
                {
                    Equal("Kubera SP all targets", 2, hits);
                    Near("Kubera SP gold bonus", 1.05f, SpecialAction.GoldRushMultiplier());
                    Assert("Kubera SP physical vulnerability", first.ActiveStatuses.Any(s => s.Key == "kubera_gold_rush"), "present", "checked");
                }
                if (id == 66)
                {
                    Assert("Varuna SP heals", ally.HpCurr > hp, "healed", ally.HpCurr.ToString());
                    Assert("Varuna SP cleanses", !ally.isControlled, "control removed", ally.isControlled.ToString());
                }
                if (id == 67) Equal("Surya SP ten strikes", 10, hits);
                Clear();
            }
            var horus = grid.SpawnUnit(-1, 1, false, 120);
            int strength = horus.GetBaseStr();
            new HorusSkyFalcon(new PassiveCodeContext { Caster = horus }).CastCode();
            int dex = horus.GetUnburdenedBaseDex();
            int convertedStrength = horus.GetBaseStr();
            Equal("Horus speed converts into STR", strength + dex, convertedStrength);
            Near("Horus speed fixed", 1f, horus.ActionSpeedCurr);
            var heavy = game.itemDataList.items.OrderByDescending(i => i.weight).First();
            for (int i = 0; i < 100 && horus.EncumbranceTier < 2; i++) horus.TryStoreItem(heavy.id, out _);
            Equal("Horus burden tier", 2, horus.EncumbranceTier);
            Equal("Horus burden applies once without recursion", Mathf.RoundToInt(convertedStrength * .5f), horus.GetBaseStr());
            Clear();
        }

        private IEnumerator SpecialBatch(string scenario)
        {
            Status = "로카팔라 예약 수명 " + scenario;
            Clear();
            var indra = grid.SpawnUnit(-1, 1, false, 63);
            var vayu = grid.SpawnUnit(-2, 1, false, 64);
            var chandra = grid.SpawnUnit(-1, 2, false, 60);
            SpawnEnemy(1062, 100);
            var scheduler = game.ActionScheduler;
            scheduler.BeginRound();
            var order = new List<int>();
            int additional = 0;
            foreach (var member in new[] { indra, vayu, chandra })
            {
                member.AddListener<EventContext>(UnitEventType.OnSpecialActivates, _ => order.Add(member.ID));
                member.AddListener<EventContext>(UnitEventType.OnAdditionalActivates, _ => additional++);
            }
            Equal("SP " + scenario + " opens 3", 3, SpecialAction.OpenGate(indra));
            if (scenario == "duplicate") Equal("SP duplicate rejects queued", 0, SpecialAction.OpenGate(indra));
            bool cleaned = false;
            SpecialAction.RegisterBatchCleanup(() => cleaned = true);
            scheduler.Tick(0);
            yield return Settle(1);
            Equal("SP " + scenario + " Vayu first", 64, order.FirstOrDefault());
            Assert("SP " + scenario + " wind active during batch",
                indra.ActiveStatuses.Any(s => s.Key.StartsWith("vayu_vanguard_wind")), "buff present", "checked");
            if (scenario == "controlled") chandra.isControlled = true;
            if (scenario == "dead") chandra.Die(null);
            if (scenario == "round end") scheduler.EndRound();
            else
            {
                float deadline = Time.realtimeSinceStartup + 5;
                while (!cleaned && Time.realtimeSinceStartup < deadline)
                { scheduler.Tick(Time.deltaTime); yield return null; }
            }
            Assert("SP " + scenario + " batch cleanup", cleaned, "cleaned", cleaned.ToString());
            Assert("SP " + scenario + " wind removed",
                !indra.ActiveStatuses.Any(s => s.Key.StartsWith("vayu_vanguard_wind")), "buff absent", "checked");
            Equal("SP " + scenario + " separate from additional", 0, additional);
            scheduler.EndRound();
            Clear();
        }

        private IEnumerator SunlightMechanics()
        {
            Status = "수리야 햇빛/중첩/라비";
            Clear();
            var surya = grid.SpawnUnit(-1, 1, false, 67);
            var agni = grid.SpawnUnit(-2, 1, false, 62);
            var enemy = SpawnEnemy(1062, 100);
            MakeUnavoidable(enemy);
            grid.OnRoundStart();
            Equal("Savitr likeness Agni", 4, SuryaSavitr.Likeness(surya, agni));
            agni.Invoke(UnitEventType.OnNormalActivates, new EventContext(agni));
            Equal("Savitr no sunlight no gain", 0, surya.GetCombatResource(SuryaSavitr.ResourceId));
            surya.FillUltimateResource(false); surya.CastUltimateCode();
            yield return Settle(1);
            Assert("Surya ultimate sunlight", Battlefield.Is(FieldKind.Sunlight), "sunlight", Battlefield.Current.ToString());
            Near("sunlight Pyro multiplier", 1.2f, Battlefield.OutgoingMultiplier(agni));
            agni.Invoke(UnitEventType.OnSpecialActivates, new EventContext(agni));
            Equal("Savitr special gains likeness", 4, surya.GetCombatResource(SuryaSavitr.ResourceId));
            surya.CastSpecialCode();
            yield return Settle(1);
            Equal("Ilcheon preserves stacks", 4, surya.GetCombatResource(SuryaSavitr.ResourceId));
            surya.CastNormalCode();
            yield return Settle(1);
            Assert("Bhaskara builds stacks", surya.GetCombatResource(SuryaSavitr.ResourceId) > 4, ">4", surya.GetCombatResource(SuryaSavitr.ResourceId).ToString());
            Turns(surya, 3);
            Assert("sunlight expires after 3 anchor turns", !Battlefield.Is(FieldKind.Sunlight), "none", Battlefield.Current.ToString());
            Battlefield.Set(FieldKind.Sunlight, surya, 3);
            Clear();
            Assert("round reset clears field", Battlefield.Current == FieldKind.None, "none", Battlefield.Current.ToString());
        }

        private IEnumerator PassiveUltimateMechanics()
        {
            Status = "상시형 궁극기 조건/화합 수명";
            Clear();
            var susanoo = grid.SpawnUnit(-1, 1, false, 42);
            var enemy = SpawnEnemy(1062, 100);
            MakeUnavoidable(enemy);
            grid.OnRoundStart();
            game.ActionScheduler.BeginRound();
            Effects.Negative.ControlStatuses.ApplyAirborne(enemy, susanoo);
            int before = enemy.HpCurr;
            susanoo.BeginTurn();
            game.ActionScheduler.Tick(0);
            Assert("Susanoo airborne conditional strike", enemy.HpCurr < before, "damage", (before - enemy.HpCurr).ToString());
            Clear();
            var bastet = grid.SpawnUnit(-1, 1, false, 122);
            var ally = grid.SpawnUnit(-2, 1, false, 21);
            ally.DebugSetLevel(100);
            enemy = SpawnEnemy(1062, 100);
            MakeUnavoidable(enemy);
            grid.OnRoundStart();
            game.ActionScheduler.BeginRound();
            enemy.TakeDamage(new DamageContext(ally, 10, CodeType.Normal,
                new List<int>{DamageTag.AdditionalAttack, DamageTag.Physical}));
            before = enemy.HpCurr;
            game.ActionScheduler.Tick(0);
            Assert("Bastet additional damage pursuit", enemy.HpCurr < before, "pursuit damage", (before - enemy.HpCurr).ToString());
            Clear();
            var agrippa = grid.SpawnUnit(-1, 1, false, 100);
            agrippa.DebugSetLevel(100);
            ally = grid.SpawnUnit(-2, 1, false, 60);
            SpawnEnemy(1062, 100);
            grid.OnRoundStart();
            ally.AddShield(10, agrippa);
            Equal("Concordia grants once", 1, ally.ActiveStatuses.Count(s => s.Key == LegionCombat.ConcordiaKey));
            agrippa.BeginTurn(); agrippa.EndTurn();
            Equal("Concordia survives first owner turn", 1, ally.ActiveStatuses.Count(s => s.Key == LegionCombat.ConcordiaKey));
            agrippa.BeginTurn(); agrippa.EndTurn();
            Equal("Concordia expires second owner turn", 0, ally.ActiveStatuses.Count(s => s.Key == LegionCombat.ConcordiaKey));
            Clear();
            yield return null;
        }

        private IEnumerator DefeatRecovery()
        {
            Status = "패배 전진/궁극기 보존/게임 오버";
            Clear();
            Unit hero = SpawnHero();
            int heroId = hero?.ID ?? 0;
            DebugMode.ForcedThemeId = 8;
            game.life = 20;
            game.DebugLoadStage(1);
            game.StartRound();
            hero = Heroes.FirstOrDefault(unit => unit.ID == heroId);
            hero?.FillUltimateResource(false);
            game.DebugEndBattle(false);
            Equal("defeat advances to next stage", 2, game.RoundManager.Stage);
            Equal("defeat costs remaining enemies", 15, game.life);
            Assert("defeat loads next enemies", Enemies.Count > 0, "enemies present", Enemies.Count.ToString());
            Assert("defeat allows preparation", game.gameState == GameState.Preparation && !game.PreparationActionUsed, "preparation available", game.gameState.ToString());
            hero = Heroes.FirstOrDefault(unit => unit.ID == heroId);
            Equal("defeat preserves ultimate resource", hero?.ManaMax ?? 0, hero?.ManaCurr ?? -1);

            game.life = 20;
            game.DebugLoadStage(10);
            hero = Heroes.FirstOrDefault(unit => unit.ID == heroId);
            hero?.FillUltimateResource(false);
            game.StartRound();
            game.DebugEndBattle(false);
            Equal("theme boundary advances to stage 11", 11, game.RoundManager.Stage);
            hero = Heroes.FirstOrDefault(unit => unit.ID == heroId);
            Equal("theme boundary clears ultimate resource", 0, hero?.ManaCurr ?? -1);

            game.life = 1;
            game.StartRound();
            game.DebugEndBattle(false);
            Equal("fatal defeat life zero", 0, game.life);
            Assert("fatal defeat keeps GameOver", game.gameState == GameState.GameOver, "GameOver", game.gameState.ToString());
            Assert("fatal defeat deletes sandbox save", !SaveSystem.HasSave(), "no save", SaveSystem.HasSave().ToString());
            game.life = 20;
            Clear();
            yield return null;
        }

        private IEnumerator Campaign()
        {
            Status = "공허 3테마 연속 새 게임";
            // 전체 통합 검증 앞 구간이 소비한 난수량과 무관하게 단독 실행과 같은 캠페인이어야 한다.
            UnityEngine.Random.InitState(20260908);
            Clear(); DebugMode.ResetAll(); DebugMode.SetTimeScale(8);
            // 통합 검증의 직전 DefeatRecovery가 GameOver 상태로 끝난다. 여기서 MainMenu를
            // 한 번 거쳐 다시 Game으로 들어가면, 파괴 예약된 런 매니저들이 남은 프레임에
            // 장면을 재차 바꾸면서 이 검증 호스트의 코루틴까지 끊길 수 있다. 새 게임 의도를
            // 먼저 세우고 Battle 장면을 한 번만 다시 로드해 완전히 새 런을 만든다.
            GameStartIntent.Current = GameStartIntent.Intent.NewGame;
            DebugMode.ForcedThemeId = 8;
            GameManager.LoadBattleScene();
            float loadDeadline = Time.realtimeSinceStartup + 30;
            while (GameManager.Instance?.RoundManager == null && Time.realtimeSinceStartup < loadDeadline) yield return null;
            game = GameManager.Instance; grid = game.gridManager;
            var selection = CharacterSelectionManager.Instance;
            selection.ClearLineup();
            // 현행 수르트 입문 추천 편성: 수르트 · 아그리파 · 세이 · 스카디 · 프레이아.
            // 추천 데이터가 바뀌었는데 과거 피그말리온 편성을 남기면 캠페인 검증만 10스테이지에서
            // 조기 종료되어 실제 초보자 동선을 검증하지 못한다.
            foreach (int id in new[] { 80, 100, 1, 83, 81 })
                Assert("campaign legal roster " + id, selection.AddHero(id), "accepted", "checked");
            int rearRow = 1;
            foreach (var entry in selection.Lineup)
            {
                bool front = entry.UnitId == 80 || entry.UnitId == 83;
                entry.XPos = front ? -1 : -2;
                entry.YPos = entry.UnitId == 80 ? 2 : entry.UnitId == 83 ? 3 : rearRow++;
            }
            selection.ConfirmSelection();
            DebugMode.SetTimeScale(8);
            Assert("campaign grid singleton matches scene", grid == GridManager.Instance, "same grid", "compared");
            Equal("campaign five heroes spawned", 5, Heroes.Count);
            if (Heroes.Count != 5 || grid != GridManager.Instance) yield break;
            int lastStage = 0, battles = 0, trainings = 0, events = 0, rewards = 0;
            float deadline = Time.realtimeSinceStartup + 900;
            while (game.RoundManager.Stage <= 30 && game.gameState != GameState.GameOver && Time.realtimeSinceStartup < deadline)
            {
                int stage = game.RoundManager.Stage;
                if (lastStage != stage)
                {
                    Equal("campaign sequential stage " + stage, lastStage + 1, stage);
                    lastStage = stage;
                    Debug.Log($"[Campaign] stage={stage} theme={game.RoundManager.CurrentThemeName} life={game.life} party={string.Join(",", Heroes.Select(u => u.ID + ":L" + u.Level))}");
                    Status = $"공허 연속 플레이 {stage}/30 {game.RoundManager.CurrentThemeName}";
                    SaveCampaignEvidence(stage);
                }
                // Choose next theme before the ordinary reward/event progression loads it.
                DebugMode.ForcedThemeId = stage >= 20 ? 10 : stage >= 10 ? 9 : 8;
                if (game.gameState == GameState.Preparation)
                {
                    Assert("campaign battle fixture " + stage, Heroes.Count > 0 && Enemies.Count > 0,
                        "heroes and enemies present", Heroes.Count + "/" + Enemies.Count);
                    if (Heroes.Count == 0 || Enemies.Count == 0) yield break;
                    if (!game.PreparationActionUsed && !game.IsPreparationLimitedToDeck())
                    {
                        if (TrainingManager.State.Energy < 60 || Heroes.Any(h => h.HpCurr < h.HpMax * .85f)) game.RestFromPreparation();
                        else
                        {
                            game.OpenTrainingFromPreparation();
                            if (game.gameState == GameState.TrainingPhase)
                            {
                                var main = TrainingManager.GetMainUnit();
                                int level = main.TrainingLevel;
                                game.CompleteTrainingPhaseWithFocus(PrimaryStat.STR);
                                game.CompleteTrainingResult(); trainings++;
                                Equal("campaign training level " + stage, level + 1, main.TrainingLevel);
                            }
                        }
                    }
                    game.StartRound();
                    if (game.gameState == GameState.RoundInProgress) battles++;
                }
                else if (game.gameState == GameState.EventStage)
                {
                    var evt = ReadField<StageEventData>(game, "currentStageEvent");
                    game.SkipEventDialogue();
                    if (game.gameState == GameState.EventStage)
                    {
                        var choice = evt?.choices?.FirstOrDefault(c => c.battleEnemyId <= 0 && c.action != "pay_gold");
                        if (choice != null) game.SelectEventChoice(choice.id);
                        game.CompleteEventStage(); events++;
                    }
                }
                else if (game.gameState == GameState.RewardSelection)
                {
                    Assert("campaign natural victory " + stage, Enemies.Count == 0, "no enemies", Enemies.Count.ToString());
                    var screen = ReadField<RewardScreen>(game.uiManager, "_reward");
                    var offered = ReadField<List<RewardDef>>(screen, "_rewards");
                    Assert("campaign real reward offers " + stage, offered.Count == 3, "3", offered.Count.ToString());
                    var chosen = offered.OrderByDescending(r => r.item != null && Heroes.Any(h => h.CanUseEquipmentEffects(r.item)))
                        .ThenByDescending(r => r.tier).First();
                    var carrier = Heroes.OrderByDescending(h => chosen.item != null && h.CanUseEquipmentEffects(chosen.item))
                        .ThenBy(h => (float)h.CarryWeightCurrent / h.CarryWeightFirstCap).First();
                    game.rewardManager.ApplyReward(chosen, carrier); rewards++;
                    EquipCampaignItems();
                }
                yield return null;
            }
            // 이 루프의 목적은 30스테이지 자연 완주다. 첫 엘리트(6)만 넘기면 성공으로
            // 처리하면 중도 GameOver도 녹색 보고서가 되어 실제 캠페인 회귀를 숨긴다.
            Assert("campaign natural progress reaches stage 30", lastStage >= 30,
                ">=30", $"{lastStage} ({game.gameState}, life {game.life})");
            Assert("campaign training exercised", trainings > 0, ">0", trainings.ToString());
            Assert("campaign events exercised", events > 0, ">0", events.ToString());
            Assert("campaign rewards exercised", rewards > 0, ">0", rewards.ToString());
            Assert("campaign retries exercised", battles > rewards, "> rewards", battles + "/" + rewards);
            Assert("campaign no invincibility", !DebugMode.AllyInvincible && !DebugMode.EnemyInvincible, "disabled", "checked");
            Debug.Log($"[Campaign] natural complete stage={game.RoundManager.Stage} battles={battles} trainings={trainings} events={events} rewards={rewards} life={game.life}");
            SaveCampaignEvidence(game.RoundManager.Stage);
            yield return ThreeVoidThemeSmoke();
        }

        private IEnumerator ThreeVoidThemeSmoke()
        {
            Status = "공허 계열 3테마 실제 전투 스모크";
            Clear(); DebugMode.ResetAll();
            GameManager.LoadMainMenuScene();
            yield return null; yield return null;
            GameStartIntent.Current = GameStartIntent.Intent.NewGame;
            DebugMode.ForcedThemeId = 8;
            GameManager.LoadBattleScene();
            float loadDeadline = Time.realtimeSinceStartup + 30;
            while (GameManager.Instance?.RoundManager == null && Time.realtimeSinceStartup < loadDeadline) yield return null;
            game = GameManager.Instance; grid = game.gridManager;
            var selection = CharacterSelectionManager.Instance;
            selection.ClearLineup();
            foreach (int id in new[] { 80, 66, 62, 160, 81 }) selection.AddHero(id);
            int rearRow = 1;
            foreach (var entry in selection.Lineup)
            {
                entry.XPos = entry.UnitId == 80 ? -1 : -2;
                entry.YPos = entry.UnitId == 80 ? 2 : rearRow++;
            }
            selection.ConfirmSelection();
            DebugMode.SetTimeScale(8);

            var exercised = new HashSet<int>();
            foreach (var fixture in new[] { (theme: 8, stage: 1), (theme: 9, stage: 11), (theme: 10, stage: 21) })
            {
                DebugMode.ForcedThemeId = fixture.theme;
                game.DebugLoadStage(fixture.stage);
                foreach (Unit hero in Heroes) hero.DebugSetLevel(fixture.stage);
                Equal("theme smoke binds " + fixture.theme, fixture.theme, game.RoundManager.CurrentThemeId);
                Assert("theme smoke fixture " + fixture.theme, Heroes.Count == 5 && Enemies.Count > 0,
                    "5 heroes and enemies", Heroes.Count + "/" + Enemies.Count);
                SaveCampaignEvidence(fixture.stage);

                DebugMode.AllyInvincible = true;
                int errors = report.exceptions.Count;
                game.StartRound();
                float actionDeadline = Time.realtimeSinceStartup + 30;
                while (game.gameState == GameState.RoundInProgress && game.ActionScheduler.TurnsTaken < 12 &&
                       Time.realtimeSinceStartup < actionDeadline) yield return null;
                Assert("theme smoke actions " + fixture.theme, game.ActionScheduler.TurnsTaken >= 8,
                    ">=8 real turns", game.ActionScheduler.TurnsTaken.ToString());
                Equal("theme smoke runtime errors " + fixture.theme, errors, report.exceptions.Count);
                if (game.gameState == GameState.RoundInProgress) game.DebugEndBattle(true);
                Assert("theme smoke reward transition " + fixture.theme, game.gameState == GameState.RewardSelection,
                    "RewardSelection", game.gameState.ToString());
                exercised.Add(game.RoundManager.CurrentThemeId);
                DebugMode.AllyInvincible = false;

                var screen = ReadField<RewardScreen>(game.uiManager, "_reward");
                var offered = ReadField<List<RewardDef>>(screen, "_rewards");
                Assert("theme smoke reward offers " + fixture.theme, offered.Count == 3, "3", offered.Count.ToString());
                game.rewardManager.ApplyReward(offered[0], Heroes.FirstOrDefault());
                EquipCampaignItems();
            }
            Assert("three void themes exercised", exercised.SetEquals(new[] { 8, 9, 10 }),
                "8,9,10", string.Join(",", exercised));
            DebugMode.AllyInvincible = false;
            DebugMode.ForcedThemeId = 0;
            Debug.Log("[Campaign] three-theme smoke complete: " + string.Join(",", exercised));
        }

        private void EquipCampaignItems()
        {
            foreach (var hero in Heroes)
                foreach (int id in hero.CarriedItemIds.ToList())
                {
                    var item = game.itemDataList.items.First(i => i.id == id);
                    if (!hero.CanUseEquipmentEffects(item)) continue;
                    var current = game.itemDataList.items.FirstOrDefault(i => hero.EquippedItemIds.Contains(i.id) && i.slot == item.slot);
                    if (current == null || item.rarity > current.rarity)
                        game.inventoryManager.TryEquipStoredItem(hero, id, out _);
                }
        }

        private void SaveCampaignEvidence(int stage)
        {
            string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Logs/Integration"));
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, $"stage-{stage:00}.json"),
                JsonUtility.ToJson(game.runManager.CaptureDebugSnapshot(), true));
            if (stage == 1 || stage == 11 || stage == 21 || stage == 31)
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"stage-{stage:00}.png"));
        }
    }
}
#endif
