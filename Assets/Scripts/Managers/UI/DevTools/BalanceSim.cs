#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BaseClasses;
using Codes.Passive;
using Core;
using Entities;
using Managers.UI.Screens;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Managers.UI.DevTools
{
    /// <summary>
    /// 밸런스 시뮬레이터. 입문 추천 편성(수르트 + 아그리파·프레이아·세이·스카디)으로 새 여정을
    /// 여러 시드로 끝까지 자동 진행하고, 스테이지마다 무슨 일이 있었는지 JSON으로 남긴다.
    ///
    /// <see cref="DebugVerification"/>의 캠페인 검증과 달리 <b>테마를 고정하지 않는다</b> —
    /// 실제 플레이처럼 라운드마다 추첨된다. 통과/실패를 판정하지 않고 기록만 한다.
    /// 판단은 기록을 읽는 쪽의 몫이다(목표: 운이 나빠도 30스테이지 돌파).
    ///
    /// 봇의 선택 규칙은 "초보가 추천 편성을 그대로 따라 할 때"를 흉내 낸다.
    ///   · 준비: 훈련 체력 45 미만이거나 파티 체력이 60% 아래면 휴식, 아니면 메인의 주·부 스탯과 CON 중
    ///     (주 상승량 + 부 스탯 몫)이 가장 큰 훈련
    ///   · 배울 수 있는 스킬은 바로 배운다
    ///   · 보상: 쓸 수 있는 장비 → 등급 높은 것. 착용 가능한 장비는 더 좋은 것으로 갈아 낀다
    ///   · 사건: 전투·영입이 아닌 선택지 → 없으면 첫 선택지
    /// 저장은 디버그 세션 안에서만 일어나 실제 세이브를 건드리지 않는다.
    /// </summary>
    public sealed class BalanceSim : MonoBehaviour
    {
        public static readonly string ReportDir = Path.GetFullPath("Logs/BalanceSim");
        public static string DoneMarker => Path.Combine(ReportDir, "DONE");

        /// <summary>편성. 첫 번째가 메인이다. 기본은 수르트 입문 편성.</summary>
        private static int[] Lineup = { 80, 83, 100, 81, 1 };
        private static HashSet<int> FrontRow = new() { 80, 83, 100 };
        private static int MainId => Lineup[0];

        public static bool Running { get; private set; }

        private int _runs;
        private int _maxStage;
        private int _seedBase;
        private float _timeScale;
        private bool _sabahProbe;

        private GameManager game;
        private GridManager grid;
        private int _mainUltimateCasts;
        private int _stageUltimateStart;

        [Serializable]
        public class StageRecord
        {
            public int stage;
            public string theme;
            public string kind;          // battle / event / boss
            public int attempts;         // 이 스테이지에서 치른 전투 수(현재 규칙상 0 또는 1)
            public int defeats;
            public int lifeBefore;
            public int lifeAfter;
            public float battleSeconds;  // 마지막 전투의 게임 시간(초)
            public int heroesAliveAtEnd;
            public float partyHpRatioAtEnd;
            public string prepAction;    // train:STR / rest / none
            public int surtrLevel;
            public int surtrStr;
            public int surtrCon;
            public int surtrHpMax;
            public string enemies;

            // 마지막 전투가 끝나기 직전(라운드 종료의 필드 복원 전) 모습. 전멸과 시간 초과를 가른다.
            public string outcome;            // win / wipe / timeout / loss
            public int endHeroesAlive;
            public int endEnemiesAlive;
            public float enemyHpRemovedRatio; // 전투 시작 대비 적 체력(보호막 제외)을 깎은 비율
            public int endTurnsLeft;
            public float minHeroHpRatio;      // 전투 중 가장 낮았던 파티 평균 체력 비율
            public string heroDeathOrder;     // 먼저 쓰러진 순서
            public int mainUltimateCasts;     // 이 스테이지의 메인 캐릭터 궁극기 발동 횟수
            public int mainUltimateBeforeBattle;
            public int mainUltimateAfterBattle;
        }

        [Serializable]
        public class RunReport
        {
            public int seed;
            public int reachedStage;
            public int finalLife;
            public bool gameOver;
            public string endReason;
            public float realSeconds;
            public int trainings;
            public int rests;
            public int skillsLearned;
            public int mainUltimateCasts;
            public int sabahDebuffResource;
            public int sabahSuperconductResource;
            public int sabahDamageOverTimeResource;
            public List<StageRecord> stages = new();
        }

        public static void Begin(int runs, int maxStage, int seedBase, float timeScale,
            string lineup = null, string front = null, bool sabahProbe = false)
        {
            if (Running) return;
            int[] parsed = ParseIds(lineup);
            if (parsed.Length > 0) Lineup = parsed;
            int[] frontIds = ParseIds(front);
            if (frontIds.Length > 0) FrontRow = new HashSet<int>(frontIds);
            var host = new GameObject("BalanceSim").AddComponent<BalanceSim>();
            DontDestroyOnLoad(host.gameObject);
            host._runs = Mathf.Max(1, runs);
            host._maxStage = Mathf.Max(1, maxStage);
            host._seedBase = seedBase;
            host._timeScale = Mathf.Clamp(timeScale, 1f, 20f);
            host._sabahProbe = sabahProbe;
            host.StartCoroutine(host.RunAll());
        }

        private IEnumerator RunAll()
        {
            Running = true;
            DebugMode.SuiteRunning = true;
            Directory.CreateDirectory(ReportDir);
            if (File.Exists(DoneMarker)) File.Delete(DoneMarker);

            var summary = new List<string>();
            for (int i = 0; i < _runs; i++)
            {
                int seed = _seedBase + i;
                var report = new RunReport { seed = seed };
                float started = Time.realtimeSinceStartup;
                _mainUltimateCasts = 0;
                Action<Unit> ultimateHandler = unit =>
                {
                    if (unit != null && !unit.IsEnemy && unit.ID == MainId) _mainUltimateCasts++;
                };
                Unit.AnyActiveUltimateActivated += ultimateHandler;
                yield return RunOne(report);
                Unit.AnyActiveUltimateActivated -= ultimateHandler;
                report.mainUltimateCasts = _mainUltimateCasts;
                report.realSeconds = Time.realtimeSinceStartup - started;

                string path = Path.Combine(ReportDir, $"run_{seed}.json");
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
                string line = $"seed={seed} reached={report.reachedStage} life={report.finalLife} gameOver={report.gameOver} " +
                              $"end={report.endReason} trainings={report.trainings} rests={report.rests} skills={report.skillsLearned} " +
                              $"mainUlts={report.mainUltimateCasts} " +
                              $"real={report.realSeconds:0}s";
                summary.Add(line);
                File.AppendAllText(Path.Combine(ReportDir, "summary.txt"), line + Environment.NewLine);
                Debug.Log("[BalanceSim] " + line);
            }

            File.WriteAllText(DoneMarker, string.Join(Environment.NewLine, summary));
            Debug.Log("[BalanceSim] 완료");
            DebugMode.SuiteRunning = false;
            Running = false;
        }

        private IEnumerator RunOne(RunReport report)
        {
            SabahDebuffHunter.ResetAuditCounters();
            UnityEngine.Random.InitState(report.seed);
            DebugMode.ResetAll();
            GameStartIntent.Current = GameStartIntent.Intent.NewGame;
            GameManager.LoadBattleScene();

            float loadDeadline = Time.realtimeSinceStartup + 60;
            while ((GameManager.Instance?.RoundManager == null || CharacterSelectionManager.Instance == null) &&
                   Time.realtimeSinceStartup < loadDeadline) yield return null;
            game = GameManager.Instance;
            if (game?.RoundManager == null) { report.endReason = "scene load timeout"; yield break; }
            grid = game.gridManager;
            yield return null;

            var selection = CharacterSelectionManager.Instance;
            selection.ClearLineup();
            foreach (int id in Lineup)
            {
                if (!selection.AddHero(id)) { report.endReason = "lineup rejected " + id; yield break; }
            }
            int front = 1, rear = 1;
            foreach (var entry in selection.Lineup)
            {
                bool isFront = FrontRow.Contains(entry.UnitId);
                entry.XPos = isFront ? -1 : -2;
                entry.YPos = isFront ? front++ : rear++;
            }
            selection.ConfirmSelection();
            game.uiManager?.HideCharacterSelection();
            yield return null;
            DebugMode.SetTimeScale(_timeScale);

            StageRecord current = null;
            int lastStage = 0;
            float runDeadline = Time.realtimeSinceStartup + 60f * 60f;
            float lastProgress = Time.realtimeSinceStartup;
            GameState lastState = game.gameState;

            while (true)
            {
                if (game == null || GameManager.Instance != game) { report.endReason = "game instance lost"; break; }
                int stage = game.RoundManager.Stage;
                if (stage > _maxStage) { report.endReason = "reached max stage"; break; }
                if (game.gameState == GameState.GameOver) { report.gameOver = true; report.endReason = "game over"; break; }
                if (game.gameState == GameState.RunComplete) { report.endReason = "run complete"; break; }
                if (Time.realtimeSinceStartup > runDeadline) { report.endReason = "run timeout"; break; }
                if (Time.realtimeSinceStartup - lastProgress > 240f)
                {
                    report.endReason = $"stall in {game.gameState} at stage {stage}";
                    break;
                }

                if (stage != lastStage)
                {
                    if (current != null) FinishRecord(current);
                    current = NewRecord(stage);
                    report.stages.Add(current);
                    report.reachedStage = stage;
                    lastStage = stage;
                    lastProgress = Time.realtimeSinceStartup;
                }
                if (game.gameState != lastState)
                {
                    lastState = game.gameState;
                    lastProgress = Time.realtimeSinceStartup;
                }

                // 전투 HUD가 생길 때마다 배속을 자기 값으로 덮으므로 매번 되돌린다.
                if (!Mathf.Approximately(Time.timeScale, _timeScale) && Time.timeScale > 0f)
                    Time.timeScale = _timeScale;

                switch (game.gameState)
                {
                    case GameState.CharacterSelection:
                        report.endReason = "stuck in character selection";
                        yield break;

                    case GameState.Preparation:
                        yield return Prepare(report, current);
                        if (game.gameState == GameState.Preparation)
                        {
                            int lifeBefore = game.life;
                            float battleStart = Time.time;
                            current.mainUltimateBeforeBattle = MainUnit()?.ManaCurr ?? 0;
                            game.StartRound();
                            if (game.gameState == GameState.RoundInProgress)
                            {
                                current.attempts++;
                                current.enemies = string.Join(",", Enemies.Select(u => u.UnitName));
                                yield return WaitWhileBattle(current);
                                current.mainUltimateAfterBattle = MainUnit()?.ManaCurr ?? 0;
                                current.battleSeconds = Time.time - battleStart;
                                bool lost = game.life < lifeBefore || game.gameState == GameState.GameOver;
                                if (lost) current.defeats++;
                                current.outcome = !lost ? "win"
                                    : current.endHeroesAlive == 0 ? "wipe"
                                    : current.endTurnsLeft <= 0 ? "timeout" : "loss";
                                current.heroesAliveAtEnd = Heroes.Count;
                            }
                        }
                        break;

                    case GameState.EventStage:
                        if (current.kind == "battle") current.kind = "event";
                        yield return HandleEvent();
                        break;

                    case GameState.RewardSelection:
                        PickReward();
                        break;

                    case GameState.TrainingPhase:
                        // 사건·보상 뒤에 훈련 화면이 남아 있으면 결과를 확정한다.
                        game.CompleteTrainingPhaseWithFocus(PrimaryStat.STR);
                        game.CompleteTrainingResult();
                        break;
                }

                yield return null;
            }

            if (current != null) FinishRecord(current);
            report.finalLife = game != null ? game.life : 0;
            CaptureSabahResourceAudit(report);
        }

        private void CaptureSabahResourceAudit(RunReport report)
        {
            report.sabahDebuffResource = SabahDebuffHunter.AuditDebuffResourceGained;
            report.sabahSuperconductResource = SabahDebuffHunter.AuditSuperconductResourceGained;
            report.sabahDamageOverTimeResource = SabahDebuffHunter.AuditDamageOverTimeResourceGained;
        }

        // ── 준비 ─────────────────────────────────────────────────────

        private IEnumerator Prepare(RunReport report, StageRecord record)
        {
            LearnSkills(report);

            if (!game.PreparationActionUsed && !game.IsPreparationLimitedToDeck())
            {
                float partyHp = PartyHpRatio();
                if (TrainingManager.State.Energy < 45 || partyHp < 0.60f)
                {
                    game.RestFromPreparation();
                    report.rests++;
                    record.prepAction = "rest";
                }
                else
                {
                    PrimaryStat focus = PickFocus();
                    game.OpenTrainingFromPreparation();
                    if (game.gameState == GameState.TrainingPhase)
                    {
                        game.CompleteTrainingPhaseWithFocus(focus);
                        game.CompleteTrainingResult();
                        report.trainings++;
                        record.prepAction = "train:" + focus;
                    }
                }
                yield return null;
                LearnSkills(report);
            }
            else if (string.IsNullOrEmpty(record.prepAction))
            {
                record.prepAction = game.IsPreparationLimitedToDeck() ? "boss-locked" : "used";
            }
        }

        private static int[] ParseIds(string csv)
            => string.IsNullOrWhiteSpace(csv)
                ? Array.Empty<int>()
                : csv.Split(',').Select(part => int.TryParse(part.Trim(), out int id) ? id : 0).Where(id => id > 0).ToArray();

        /// <summary>
        /// 메인의 주·부 스탯과 CON 가운데 (주 상승량 + 부 스탯 몫)이 가장 큰 훈련.
        /// 동점이면 주 스탯 → 부 스탯 → CON 순서. 수르트(STR/CON)는 예전 규칙과 같다.
        /// </summary>
        private static PrimaryStat PickFocus()
        {
            Unit main = TrainingManager.GetMainUnit();
            var candidates = new List<PrimaryStat>();
            void Add(string name)
            {
                if (Enum.TryParse(name, true, out PrimaryStat stat) && !candidates.Contains(stat)) candidates.Add(stat);
            }
            if (main != null)
            {
                Add(main.MainStat);
                foreach (string sub in main.SubStats) Add(sub);
            }
            Add("CON");

            PrimaryStat best = candidates[0];
            int bestScore = int.MinValue;
            foreach (PrimaryStat stat in candidates)
            {
                int score = TrainingManager.GetProjectedGain(stat) + TrainingManager.GetProjectedSecondaryGain(stat);
                if (score <= bestScore) continue;
                bestScore = score;
                best = stat;
            }
            return best;
        }

        private static void LearnSkills(RunReport report)
        {
            foreach (var offer in TrainingManager.GetSkillOffers().Where(offer => offer.CanLearn).OrderBy(offer => offer.Cost))
            {
                if (TrainingManager.TryLearnSkill(offer.CodeId, out _))
                {
                    report.skillsLearned++;
                    GameManager.Instance?.NotifySkillLearned();
                }
            }
        }

        // ── 전투 ─────────────────────────────────────────────────────

        private IEnumerator WaitWhileBattle(StageRecord record = null)
        {
            float deadline = Time.realtimeSinceStartup + 300f;
            var startEnemies = Enemies;
            // 사바흐 회귀 검증에서는 피그말리온 궁극기를 즉시 열어 실제 반격 화상과
            // 지속피해 틱이 아즈라엘을 채우는 전투 이벤트 경로를 짧게 재현한다.
            if (_sabahProbe)
                Heroes.FirstOrDefault(unit => unit.ID == 20)?.FillUltimateResource(false);
            float startEnemyHp = startEnemies.Sum(u => (float)u.HpCurr);
            var deaths = new List<string>();
            var aliveHeroes = new HashSet<Unit>(Heroes);
            float minHp = 1f;
            while (game != null && game.gameState == GameState.RoundInProgress && Time.realtimeSinceStartup < deadline)
            {
                if (!Mathf.Approximately(Time.timeScale, _timeScale) && Time.timeScale > 0f)
                    Time.timeScale = _timeScale;

                if (record != null)
                {
                    var heroes = Heroes;
                    foreach (Unit fallen in aliveHeroes.Where(h => !heroes.Contains(h)).ToList())
                    {
                        deaths.Add(fallen.UnitName);
                        aliveHeroes.Remove(fallen);
                    }
                    var enemies = Enemies;
                    record.endHeroesAlive = heroes.Count;
                    record.endEnemiesAlive = enemies.Count;
                    // 도중에 소환된 적(사령·씨앗)은 분모에 없으므로 1을 넘지 않게 자른다.
                    record.enemyHpRemovedRatio = startEnemyHp > 0f
                        ? Mathf.Clamp01(1f - enemies.Sum(u => (float)u.HpCurr) / startEnemyHp)
                        : 0f;
                    record.endTurnsLeft = game.PhaseRemainingSeconds;   // 전투 중에는 남은 턴 수다
                    float hp = PartyHpRatio();
                    if (hp < minHp) minHp = hp;
                }
                yield return null;
            }
            if (record != null)
            {
                record.minHeroHpRatio = minHp;
                record.heroDeathOrder = string.Join(">", deaths);
            }
            if (game != null && game.gameState == GameState.RoundInProgress)
            {
                Debug.LogWarning("[BalanceSim] 전투가 실시간 300초를 넘어 패배로 끝낸다");
                game.DebugEndBattle(false);
            }
        }

        // ── 사건 ─────────────────────────────────────────────────────

        private IEnumerator HandleEvent()
        {
            var evt = ReadField<StageEventData>(game, "currentStageEvent");
            game.SkipEventDialogue();
            yield return null;
            if (game.gameState != GameState.EventStage) yield break;

            // 자리가 없어 영입이 막힌 화면이면 합류를 포기한다.
            if (ReadField<StageEventChoiceData>(game, "pendingRecruitChoice") != null)
            {
                game.CancelRecruitForRoster();
                yield return null;
            }

            var choices = evt?.choices ?? new List<StageEventChoiceData>();
            var choice = choices.FirstOrDefault(c => c.battleEnemyId <= 0 && c.grantUnitId <= 0 && c.action != "pay_gold")
                         ?? choices.FirstOrDefault(c => c.battleEnemyId <= 0 && c.grantUnitId <= 0)
                         ?? choices.FirstOrDefault();
            if (choice != null)
            {
                game.SelectEventChoice(choice.id);
                yield return null;
                if (game.gameState == GameState.RoundInProgress) yield return WaitWhileBattle();
                if (ReadField<StageEventChoiceData>(game, "pendingRecruitChoice") != null) game.CancelRecruitForRoster();
            }
            if (game.gameState == GameState.EventStage) game.CompleteEventStage();
        }

        // ── 보상 ─────────────────────────────────────────────────────

        private void PickReward()
        {
            var screen = ReadField<RewardScreen>(game.uiManager, "_reward");
            var offered = screen != null ? ReadField<List<RewardDef>>(screen, "_rewards") : null;
            if (offered == null || offered.Count == 0)
            {
                // 고를 것이 없으면 보상 없이 다음 스테이지로 간다(보상 화면의 건너뛰기와 같다).
                game.uiManager?.HideRewardPanel();
                RunManager.Instance?.AdvanceToNextStage();
                return;
            }

            var heroes = Heroes;
            RewardDef chosen = offered
                .OrderByDescending(r => r.item != null && heroes.Any(h => h.CanUseEquipmentEffects(r.item)))
                .ThenByDescending(r => r.tier)
                .First();
            Unit carrier = heroes
                .OrderByDescending(h => chosen.item != null && h.CanUseEquipmentEffects(chosen.item))
                .ThenBy(h => (float)h.CarryWeightCurrent / Mathf.Max(1, h.CarryWeightFirstCap))
                .FirstOrDefault();
            game.rewardManager.ApplyReward(chosen, carrier);
            EquipBetterItems();
        }

        private void EquipBetterItems()
        {
            foreach (var hero in Heroes)
            {
                foreach (int id in hero.CarriedItemIds.ToList())
                {
                    var item = game.itemDataList.items.FirstOrDefault(i => i.id == id);
                    if (item == null || !hero.CanUseEquipmentEffects(item)) continue;
                    var current = game.itemDataList.items.FirstOrDefault(i => hero.EquippedItemIds.Contains(i.id) && i.slot == item.slot);
                    if (current == null || item.rarity > current.rarity)
                        game.inventoryManager.TryEquipStoredItem(hero, id, out _);
                }
            }
        }

        // ── 기록 ─────────────────────────────────────────────────────

        private StageRecord NewRecord(int stage)
        {
            var round = game.RoundManager;
            _stageUltimateStart = _mainUltimateCasts;
            return new StageRecord
            {
                stage = stage,
                theme = round.CurrentThemeName,
                kind = round.IsCurrentBossStage ? "boss" : "battle",
                lifeBefore = game.life,
            };
        }

        private void FinishRecord(StageRecord record)
        {
            if (game == null) return;
            record.lifeAfter = game.life;
            record.partyHpRatioAtEnd = PartyHpRatio();
            record.mainUltimateCasts = _mainUltimateCasts - _stageUltimateStart;
            Unit surtr = grid?.heroList?.FirstOrDefault(u => u != null && u.ID == MainId);
            if (surtr != null)
            {
                record.surtrLevel = surtr.Level;
                record.surtrStr = surtr.GetBaseStr();
                record.surtrCon = surtr.GetBaseCon();
                record.surtrHpMax = surtr.HpMax;
            }
        }

        private float PartyHpRatio()
        {
            var heroes = grid?.heroList?.Where(u => u != null && !u.IsEnemy && u.currentCell != null &&
                                                    !grid.IsBenchCell(u.currentCell)).ToList();
            if (heroes == null || heroes.Count == 0) return 0f;
            return heroes.Average(u => u.isActive && u.HpMax > 0 ? (float)u.HpCurr / u.HpMax : 0f);
        }

        private Unit MainUnit()
            => grid?.heroList?.FirstOrDefault(unit =>
                unit != null && unit.isActive && !unit.IsEnemy && unit.ID == MainId);

        private List<Unit> Enemies => grid.enemyList.Where(u => u != null && u.isActive).ToList();
        private List<Unit> Heroes => grid.heroList.Where(u => u != null && u.isActive).ToList();

        private static T ReadField<T>(object owner, string name)
        {
            if (owner == null) return default;
            FieldInfo field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            return field == null ? default : (T)field.GetValue(owner);
        }
    }
}
#endif
