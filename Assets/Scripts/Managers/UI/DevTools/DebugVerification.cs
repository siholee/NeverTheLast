#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Core;
using Effects.Base;
using Effects.Buffs;
using Effects.Negative;
using Entities;
using Helpers;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Managers.UI.DevTools
{
    /// <summary>실제 Game 씬/Unit/코루틴/피해 파이프라인을 사용하는 재실행 가능한 검증.</summary>
    public sealed partial class DebugVerification : MonoBehaviour
    {
        [Serializable] public sealed class Check
        {
            public string name;
            public bool passed;
            public string expected;
            public string actual;
        }
        [Serializable] public sealed class Report
        {
            public string unityVersion;
            public string completedUtc;
            public bool completed;
            public int passed;
            public int failed;
            public List<Check> checks = new();
            public List<string> exceptions = new();
        }
        public static string Status { get; private set; } = "실행 전";
        private readonly Report report = new();
        private GameManager game;
        private GridManager grid;
        private string originalSave;
        private int originalHasSave;
        private string originalTrained;
        private UnityEngine.Random.State randomState;
        private bool integrationOnly;
        private bool campaignOnly;

        public static void StartSuite(bool integrationOnly = false, bool campaignOnly = false)
        {
            if (DebugMode.SuiteRunning) return;
            DebugMode.BeginSession();
            DebugMode.SuiteRunning = true;
            var host = new GameObject("DebugVerification").AddComponent<DebugVerification>();
            host.integrationOnly = integrationOnly;
            host.campaignOnly = campaignOnly;
            DontDestroyOnLoad(host.gameObject);
            host.StartCoroutine(host.GuardedRun());
        }

        private IEnumerator GuardedRun()
        {
            originalSave = PlayerPrefs.GetString("NTL_RunSave", "");
            originalHasSave = PlayerPrefs.GetInt("NTL_HasSave", 0);
            originalTrained = PlayerPrefs.GetString("NTL_TrainedCharacters", "");
            randomState = UnityEngine.Random.state;
            Application.logMessageReceived += OnLog;
            var stack = new Stack<IEnumerator>();
            stack.Push(Run());
            while (stack.Count > 0)
            {
                object current = null;
                bool moved = false;
                try { moved = stack.Peek().MoveNext(); if (moved) current = stack.Peek().Current; }
                catch (Exception ex)
                {
                    // 터진 구획만 버리고 부모로 돌아간다. 예전에는 여기서 break를 걸어
                    // 구획 하나가 죽으면 뒤의 모든 검사를 잃고 completed=false로 끝났다.
                    Assert("suite exception in " + Status, false, "no exception", ex.ToString());
                    (stack.Pop() as IDisposable)?.Dispose();
                    continue;
                }
                if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (current is IEnumerator nested) stack.Push(nested);
                else yield return current;
            }
            try
            {
                if (game != null) game.DebugResetBattle();
                Assert("persistent run unchanged", originalSave == PlayerPrefs.GetString("NTL_RunSave", "") &&
                    originalHasSave == PlayerPrefs.GetInt("NTL_HasSave", 0), "original PlayerPrefs", "compared run JSON and flag");
                Assert("persistent progression unchanged", originalTrained == PlayerPrefs.GetString("NTL_TrainedCharacters", ""),
                    "original progression JSON", "compared");
                Assert("no runtime exceptions", report.exceptions.Count == 0, "0", report.exceptions.Count.ToString());
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
                UnityEngine.Random.state = randomState;
                DebugMode.ResetAll();
                DebugMode.SuiteRunning = false;
                report.unityVersion = Application.unityVersion;
                report.completedUtc = DateTime.UtcNow.ToString("O");
                report.passed = report.checks.Count(c => c.passed);
                report.failed = report.checks.Count - report.passed;
                Status = $"{report.passed} PASS / {report.failed} FAIL / completed={report.completed}";
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs"));
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, integrationOnly ? "IntegrationVerification.json" : "DebugVerification.json"), JsonUtility.ToJson(report, true));
                Debug.Log("[DebugVerification] " + Status);
                Destroy(gameObject);
            }
        }

        private void OnLog(string message, string trace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
                report.exceptions.Add(message + "\n" + trace);
        }
        private void Assert(string name, bool passed, string expected, string actual)
        {
            report.checks.Add(new Check {name=name, passed=passed, expected=expected, actual=actual});
            if (!passed) Debug.Log("[DebugVerification FAIL] " + name + " expected=" + expected + " actual=" + actual);
        }
        private void Equal(string name, int expected, int actual) => Assert(name, expected == actual, expected.ToString(), actual.ToString());
        private List<Unit> Enemies => grid.enemyList.Where(u => u != null && u.isActive).ToList();
        private List<Unit> Heroes => grid.heroList.Where(u => u != null && u.isActive).ToList();

        private IEnumerator Run()
        {
            if (GameManager.Instance == null) GameManager.LoadBattleScene();
            float deadline = Time.realtimeSinceStartup + 30;
            while (GameManager.Instance?.RoundManager == null && Time.realtimeSinceStartup < deadline) yield return null;
            game = GameManager.Instance;
            if (game?.RoundManager == null) throw new InvalidOperationException("Game scene did not initialize in 30 seconds");
            grid = game.gridManager;
            DebugMode.SetTimeScale(8f);
            UnityEngine.Random.InitState(20260908);
            if (integrationOnly)
            {
                if (campaignOnly) yield return Campaign();
                else yield return IntegrationCoverage();
                report.completed = true;
                yield break;
            }
            yield return SpriteCoverage();
            yield return SynergyCoverage();
            yield return Transitions();
            yield return DataAndLevels();
            yield return Actions();
            yield return Mechanics();
            yield return Reactions();
            yield return FullSystems();
            yield return LiveBattle();
            yield return SceneCycles();
            report.completed = true;
        }

        private void Clear()
        {
            game.DebugResetBattle();
            foreach (var hero in grid.heroList.ToList()) grid.RetireUnit(hero);
            game.RoundManager.StopRound();
            game.gameState = GameState.Preparation;
        }
        private Unit SpawnEnemy(int id, int level = 90, int x=1, int y=1)
        {
            game.RoundManager.InitializeStage(level);
            return grid.SpawnUnit(x,y,true,id);
        }
        private Unit SpawnHero(int x=-1, int y=1)
        {
            var hero = grid.SpawnUnit(x,y,false,game.unitDataList.units[0].id);
            // 칸이 막혀 있으면 null이 온다. 그대로 두면 NRE로 스위트가 통째로 죽으므로
            // 여기서 실패로 기록하고 넘어간다 — 원인은 '왜 칸이 막혔나'이지 이 줄이 아니다.
            if (hero == null)
            {
                Assert($"hero spawn at ({x},{y})", false, "spawned",
                    grid.IsCellAvailable(x,y) ? "cell free but spawn returned null" : "cell unavailable");
                return null;
            }
            hero.DebugSetLevel(90);
            return hero;
        }
        private void RoundStart(Unit unit) => unit.Invoke(UnitEventType.OnRoundStart, new EventContext(unit));
        private IEnumerator Settle(float seconds=2.5f)
        {
            float until = Time.time + seconds;
            float timeout = Time.realtimeSinceStartup + 10;
            int frames=0;
            while ((Time.time < until || frames < 12 || Enemies.Concat(Heroes).Any(u=>u.isCasting)) && Time.realtimeSinceStartup < timeout)
            { frames++; yield return null; }
            Assert("coroutine clock progresses", Time.time >= until, seconds + " game seconds", (Time.time - until + seconds).ToString("F2"));
        }

        /// <summary>
        /// 초상화·스탠딩·아이템 아이콘이 전부 실제로 로드되는지 본다.
        ///
        /// 스프라이트를 아군/적 분류 폴더로 옮기면서 키만으로는 못 찾게 된 적이 있다.
        /// 데이터가 가리키는 키를 한 번씩 다 읽어 보는 것이 그 사고를 잡는 가장 싼 방법이다.
        /// </summary>
        private IEnumerator SpriteCoverage()
        {
            Status = "스프라이트 연결";

            foreach (var unit in game.dataManager.FetchUnitDataList().units)
            {
                Assert($"unit {unit.id} {unit.name} portrait",
                    SpriteResource.LoadPortrait(unit.portrait) != null, "Sprite", unit.portrait);
                Assert($"unit {unit.id} {unit.name} standing",
                    SpriteResource.LoadStanding(unit.standing) != null, "Sprite", unit.standing);
            }
            yield return null;

            foreach (var enemy in game.dataManager.FetchEnemyDataList().enemies)
            {
                Assert($"enemy {enemy.id} {enemy.name} portrait",
                    SpriteResource.LoadPortrait(enemy.portrait) != null, "Sprite", enemy.portrait);
                Assert($"enemy {enemy.id} {enemy.name} standing",
                    SpriteResource.LoadStanding(enemy.standing) != null, "Sprite", enemy.standing);
            }
            yield return null;

            foreach (var item in game.dataManager.FetchItemDataList().items)
            {
                if (string.IsNullOrWhiteSpace(item.icon)) continue;
                Assert($"item {item.id} {item.name} icon",
                    Resources.Load<Sprite>($"Sprite/Items/{item.icon}") != null, "Sprite", item.icon);
            }

            // 소환수는 데이터가 아니라 코드가 키를 들고 있어 위 순회에 걸리지 않는다.
            foreach (string key in new[] { "FLYER_PORTRAIT", "FENRIR_PORTRAIT" })
            {
                Assert($"summon {key}", SpriteResource.LoadPortrait(key) != null, "Sprite", key);
            }
            yield return null;
        }

        /// <summary>
        /// 자료실 <c>추천 조합</c> 탭이 읽는 30_synergies.yaml이 유닛 데이터와 어긋나지 않는지 본다.
        ///
        /// 유닛을 추가하고 이 표를 안 고치면 탭이 조용히 빈 칸으로 나온다.
        /// 참조가 깨진 ID는 화면에 <c>#7</c> 같은 날것으로 찍히므로 여기서 먼저 잡는다.
        /// </summary>
        private IEnumerator SynergyCoverage()
        {
            Status = "추천 조합 데이터";

            SynergyCatalog.Invalidate();
            var units = game.dataManager.FetchUnitDataList().units;
            var synergy = game.dataManager.FetchSynergyDataList();

            Assert("synergy data loads", synergy?.units != null, "SynergyDataList", synergy == null ? "null" : "loaded");
            if (synergy?.units == null) yield break;

            Equal("synergy unit rows", units.Count, synergy.units.Count);

            foreach (var unit in units)
            {
                SynergyUnitData profile = SynergyCatalog.UnitOf(unit.id);
                Assert($"synergy {unit.id} {unit.name} tagged", profile != null, "profile", profile?.name);
                if (profile == null) continue;

                Assert($"synergy {unit.id} name", profile.name == unit.name, unit.name, profile.name);
                Assert($"synergy {unit.id} type", profile.type == unit.characterType,
                    unit.characterType, profile.type);
                Assert($"synergy {unit.id} roles named", profile.roles != null && profile.roles.Count > 0 &&
                    profile.roles.TrueForAll(role => SynergyCatalog.RoleName(role) != role),
                    "all roles defined", string.Join(",", profile.roles ?? new List<string>()));
            }
            yield return null;

            // 메인이 될 수 있는 캐릭터는 전부 추천 한 벌씩 있어야 한다.
            foreach (var unit in units)
            {
                if (unit.characterType == "Support") continue;

                SynergyRecommendationData entry = SynergyCatalog.RecommendationFor(unit.id);
                Assert($"synergy {unit.id} {unit.name} recommendation", entry != null, "recommendation",
                    entry?.mainName);
                if (entry == null) continue;

                foreach (var (label, lineup) in new[] { ("best", entry.best), ("basic", entry.basic) })
                {
                    Assert($"synergy {unit.id} {label} size", lineup?.members?.Count == 4, "4",
                        lineup?.members?.Count.ToString());
                    if (lineup?.members == null) continue;

                    foreach (int memberId in lineup.members)
                    {
                        Assert($"synergy {unit.id} {label} member {memberId}",
                            SynergyCatalog.UnitOf(memberId) != null, "known unit",
                            SynergyCatalog.NameOf(memberId));
                    }

                    // 대체 조합은 최초 로스터만으로 짤 수 있어야 한다.
                    if (label != "basic") continue;
                    foreach (int memberId in lineup.members)
                    {
                        Assert($"synergy {unit.id} basic starter-only {memberId}",
                            SynergyCatalog.UnitOf(memberId)?.type == "Support", "Support",
                            SynergyCatalog.UnitOf(memberId)?.type);
                    }
                }
            }
            yield return null;
        }

        private IEnumerator Transitions()
        {
            Status = "스테이지/보상/사건 전환";
            Clear();
            SpawnHero();
            DebugMode.ForcedThemeId = 8;
            foreach (int stage in new[] {1,11,21,31,32,36,39,40,31})
            {
                game.DebugLoadStage(stage);
                int slot=(stage-1)%10+1;
                int expected=slot==2 ? 6 : slot==6 || slot==9 || slot==10 ? 1 : 5;
                Equal("jump " + stage + " enemy count",expected,Enemies.Count);
                Equal("jump " + stage + " preserves party",1,Heroes.Count);
                Assert("jump " + stage + " cells unique",Enemies.Select(e=>e.currentCell).Distinct().Count()==Enemies.Count,"unique cells",Enemies.Count.ToString());
                yield return null;
            }
            game.StartRound();
            game.DebugEndBattle(true);
            Assert("forced victory opens rewards",game.gameState==GameState.RewardSelection,"RewardSelection",game.gameState.ToString());
            game.DebugLoadStage(31);
            Assert("reward jump closes modal",GameObject.Find("RewardScreen")==null,"no active RewardScreen","checked scene");
            Equal("reward jump party preserved",1,Heroes.Count);
            game.DebugLoadStage(35);
            game.DebugLoadStage(36);
            Assert("event jump returns preparation",game.gameState==GameState.Preparation,"Preparation",game.gameState.ToString());
            game.StartRound();
            yield return null;
            game.DebugLoadStage(31);
            Assert("battle jump scheduler cleared",game.ActionScheduler.ActingUnit==null,"no acting unit",game.ActionScheduler.ActingUnit?.UnitName);
            Equal("battle jump party restored",1,Heroes.Count);
            game.DebugEndBattle(true);
            Equal("F2 in preparation preserves party",1,Heroes.Count);
            SaveSystem.SaveRun(game.runManager.CaptureDebugSnapshot());
            SaveSystem.DeleteSave();
            Assert("sandbox delete applies in memory",!SaveSystem.HasSave(),"false",SaveSystem.HasSave().ToString());
            SaveSystem.SaveRun(game.runManager.CaptureDebugSnapshot());
            Equal("sandbox save load",31,SaveSystem.LoadRun().currentStage);
        }

        private IEnumerator DataAndLevels()
        {
            Status = "공허 데이터/팩토리/아트/레벨";
            var definitions=game.dataManager.FetchEnemyDataList().enemies.Where(e=>e.tags?.Contains("VoidMonster")==true).ToList();
            Equal("void themed enemy definitions (generic seed excluded)",80,definitions.Count(e=>e.themeId>0));
            Equal("generic void seed",1,definitions.Count(e=>e.id==1061));
            foreach(var def in definitions)
            {
                Clear();
                var unit=SpawnEnemy(def.id,1);
                Assert(def.id+" normal factory",unit.ActiveNormalCode!=null,"normal code",unit.ActiveNormalCode?.GetType().Name);
                Assert(def.id+" ultimate factory",unit.ActiveUltimateCode!=null,"ultimate code",unit.ActiveUltimateCode?.GetType().Name);
                Assert(def.id+" standing loads",SpriteResource.LoadStanding(def.standing)!=null,"Sprite",def.standing);
                Assert(def.id+" portrait loads",SpriteResource.LoadPortrait(def.portrait)!=null,"Sprite",def.portrait);
                foreach(int level in new[]{30,60,90,1,90,1})
                {
                    unit.DebugSetLevel(level);
                    var expected=new HashSet<int>((def.levelPassives??new()).Where(p=>p.unlockLevel<=level).Select(p=>p.codeId)){def.codes["passive"]};
                    var actual=new HashSet<int>(unit.LearnedPassiveRecords.Select(p=>p.codeId));
                    Assert(def.id+" level "+level+" passives",expected.SetEquals(actual),string.Join(",",expected.OrderBy(x=>x)),string.Join(",",actual.OrderBy(x=>x)));
                }
                yield return null;
            }
            foreach(var theme in game.dataManager.FetchStageThemeDataList().stageThemes.Where(t=>t.id>=8&&t.id<=15))
            {
                DebugMode.ForcedThemeId=theme.id;
                foreach(var pattern in theme.stagePatterns)
                {
                    Clear();
                    game.DebugLoadStage(30+pattern.stageInRound);
                    var p=pattern.patterns[0];
                    var expected=(p.frontIds??new()).Concat(p.rearIds??new()).OrderBy(id=>id);
                    Assert("theme "+theme.id+" slot "+pattern.stageInRound+" composition",expected.SequenceEqual(Enemies.Select(e=>e.ID).OrderBy(id=>id)),string.Join(",",expected),string.Join(",",Enemies.Select(e=>e.ID).OrderBy(id=>id)));
                    Equal("theme forced "+theme.id,theme.id,game.RoundManager.CurrentThemeId);
                    yield return null;
                }
            }
        }

        private IEnumerator Actions()
        {
            Status="공허 8원소 일반행동/궁극기";
            foreach(int baseId in new[]{1062,1070,1078,1086,1094,1102,1110,2034,2042,3050})
            for(int element=0;element<8;element++)
            {
                int id=baseId+element;
                Clear();
                var enemy=SpawnEnemy(id,1);
                var front=SpawnHero();
                var rear=SpawnHero(-2,1);
                var victims=new[]{front,rear};
                foreach(var victim in victims) MakeUnavoidable(victim);
                RoundStart(enemy);
                int before=victims.Sum(u=>u.HpCurr);
                bool resolvedAfterImpact=false;
                Action<EventContext> resolved=_=>resolvedAfterImpact=victims.Sum(u=>u.HpCurr)<before;
                enemy.AddListener(UnitEventType.OnNormalActionResolved,resolved);
                enemy.CastNormalCode();
                yield return Settle();
                enemy.RemoveListener(UnitEventType.OnNormalActionResolved,resolved);
                Assert(id+" normal action resolves after impact",resolvedAfterImpact,"impact before completion",resolvedAfterImpact.ToString());
                Assert(id+" normal resolves",victims.Sum(u=>u.HpCurr)<before,"HP reduced",(before-victims.Sum(u=>u.HpCurr)).ToString());
                if(baseId==1102) Assert(id+" marksman rear priority",rear.HpCurr<rear.HpMax&&front.HpCurr==front.HpMax,"rear damaged only",front.HpCurr+"/"+rear.HpCurr);
                foreach(var victim in victims){victim.ModifyHp(victim.HpMax);victim.ResetCombatElements();}
                if(baseId==2042) foreach(var victim in victims) victim.SetShield(100);
                before=victims.Sum(u=>u.HpCurr);
                int shields=Enemies.Sum(u=>u.ShieldCurr);
                int count=Enemies.Count;
                enemy.FillUltimateResource(false);
                enemy.CastUltimateCode();
                yield return Settle(3);
                if(baseId==2034)
                {
                    Equal(id+" deer summons",count+1,Enemies.Count);
                    var summoned=Enemies.FirstOrDefault(u=>u!=enemy);
                    Assert(id+" summon element",summoned!=null&&summoned.Element==enemy.Element,enemy.Element,summoned?.Element);
                    Equal(id+" summon level",enemy.Level,summoned?.Level??-1);
                    enemy.Die(null);
                    Equal(id+" owner death retires summon",0,Enemies.Count);
                }
                else if(baseId==1110) Assert(id+" vanguard shields",Enemies.Sum(u=>u.ShieldCurr)>shields,"shield increases",Enemies.Sum(u=>u.ShieldCurr).ToString());
                else Assert(id+" ultimate resolves",victims.Sum(u=>u.HpCurr)<before,"HP reduced",(before-victims.Sum(u=>u.HpCurr)).ToString());
                if(baseId==2042) Equal(id+" crusher removes shields",0,victims.Sum(u=>u.ShieldCurr));
                if(baseId==1094) Assert(id+" knight gains shield",enemy.ShieldCurr>0,">0",enemy.ShieldCurr.ToString());
            }
        }

        private IEnumerator Mechanics()
        {
            Status="피해/부활/무적/패시브 정리";
            Clear();
            var enemy=SpawnEnemy(3050,90);
            var hero=SpawnHero();
            RoundStart(enemy);
            DebugMode.EnemyInvincible=true;
            int hp=enemy.HpCurr;
            Damage(enemy,hero,1000000);
            Equal("enemy invincibility",hp,enemy.HpCurr);
            DebugMode.EnemyInvincible=false;
            DebugMode.AllyInvincible=true;
            hp=hero.HpCurr;
            Damage(hero,enemy,1000000);
            Equal("ally invincibility",hp,hero.HpCurr);
            DebugMode.AllyInvincible=false;
            enemy.DebugResetCombatState();
            var resurrection=CodeFactory.CreatePassiveCode(1592,new PassiveCodeContext{Caster=enemy});
            resurrection.CastCode();
            Damage(enemy,hero,1000000);
            Assert("perfect resurrection full HP",enemy.isActive&&enemy.HpCurr==enemy.HpMax,enemy.HpMax.ToString(),enemy.HpCurr.ToString());
            Damage(enemy,hero,1000000);
            Assert("perfect resurrection once per battle",!enemy.isActive,"dead",enemy.isActive.ToString());
            Clear();
            enemy=SpawnEnemy(2042,90);
            hero=SpawnHero();
            new VoidCrusherMass(new PassiveCodeContext{Caster=enemy}).CastCode();
            hp=enemy.HpCurr;
            Damage(enemy,hero,1000000);
            Assert("crusher single damage cap",hp-enemy.HpCurr<=Mathf.RoundToInt(enemy.HpMax*.25f),"<=25% HP",(hp-enemy.HpCurr).ToString());
            enemy.DebugResetCombatState();
            new VoidAdaptiveArmor(new PassiveCodeContext{Caster=enemy}).CastCode();
            var values=new List<int>();
            for(int i=0;i<7;i++) {enemy.ModifyHp(enemy.HpMax);hp=enemy.HpCurr;Damage(enemy,hero,100);values.Add(hp-enemy.HpCurr);}
            Assert("adaptive armor reduces repeated technique",values[0]>values[5]&&values[5]==values[6],"decreases then caps",string.Join(",",values));
            enemy.DebugResetCombatState();
            Equal("round end removes statuses",0,enemy.ActiveStatuses.Count);
            yield return null;
        }
        private void Damage(Unit victim,Unit attacker,int amount)
        {
            victim.TakeDamage(new DamageContext(attacker,amount,CodeType.Effect,new List<int>{DamageTag.SingleTarget,DamageTag.TrueDamage}));
        }


        // ══════════════════════════════════════════════════════════
        // 원소 반응
        // ══════════════════════════════════════════════════════════

        /// <summary>28쌍 전부. Kind는 계열별 심화 검사를 고르는 꼬리표다.</summary>
        private static readonly (UnitElement A, UnitElement B, string Name, string Kind)[] ReactionTable =
        {
            (UnitElement.Pyro,    UnitElement.Pyro,    "화상",   "dot-burn"),
            (UnitElement.Pyro,    UnitElement.Hydro,   "증발",   "amplify"),
            (UnitElement.Pyro,    UnitElement.Dendro,  "연소",   "amplify"),
            (UnitElement.Pyro,    UnitElement.Electro, "과부하", "burst"),
            (UnitElement.Pyro,    UnitElement.Cryo,    "융해",   "amplify"),
            (UnitElement.Pyro,    UnitElement.Geo,     "단조",   "buff"),
            (UnitElement.Hydro,   UnitElement.Hydro,   "정수",   "buff"),
            (UnitElement.Hydro,   UnitElement.Dendro,  "개화",   "buff"),
            (UnitElement.Hydro,   UnitElement.Electro, "감전",   "dot-shock"),
            (UnitElement.Hydro,   UnitElement.Cryo,    "빙결",   "control"),
            (UnitElement.Hydro,   UnitElement.Geo,     "풍화",   "dot-weather"),
            (UnitElement.Dendro,  UnitElement.Dendro,  "착근",   "root"),
            (UnitElement.Dendro,  UnitElement.Electro, "활성",   "vuln"),
            (UnitElement.Dendro,  UnitElement.Cryo,    "휴면",   "null"),
            (UnitElement.Dendro,  UnitElement.Geo,     "성장",   "buff"),
            (UnitElement.Electro, UnitElement.Electro, "축전",   "buff"),
            (UnitElement.Electro, UnitElement.Cryo,    "초전도", "vuln"),
            (UnitElement.Electro, UnitElement.Geo,     "접지",   "null"),
            (UnitElement.Cryo,    UnitElement.Cryo,    "둔화",   "buff"),
            (UnitElement.Cryo,    UnitElement.Geo,     "경화",   "shield"),
            (UnitElement.Geo,     UnitElement.Geo,     "진동",   "control"),
            (UnitElement.Anemo,   UnitElement.Pyro,    "확산",   "spread"),
            (UnitElement.Anemo,   UnitElement.Hydro,   "확산",   "spread"),
            (UnitElement.Anemo,   UnitElement.Dendro,  "확산",   "spread"),
            (UnitElement.Anemo,   UnitElement.Electro, "확산",   "spread"),
            (UnitElement.Anemo,   UnitElement.Cryo,    "확산",   "spread"),
            (UnitElement.Anemo,   UnitElement.Geo,     "확산",   "spread"),
            (UnitElement.Anemo,   UnitElement.Anemo,   "확산",   "burst"),
        };

        /// <summary>
        /// 반응 실험대. 공허의 프리즘을 쓰는 이유는 <b>속성이 Void라 재료를 오염시키지 않기</b> 때문이다.
        /// 패시브를 켜지 않고(=RoundStart를 부르지 않고) 몸만 세워, 반응 외의 변수를 없앤다.
        /// 증인(witness)은 확산이 진영 전체로 퍼지는지 보기 위한 두 번째 적이다.
        /// </summary>
        private (Unit target, Unit witness, Unit source) ReactionField()
        {
            Clear();
            Unit target = SpawnEnemy(1062, 90);
            Unit witness = grid.SpawnUnit(1, 2, true, 1062);
            Unit source = SpawnHero();
            foreach (Unit unit in new[] { target, witness, source })
            {
                unit.DebugResetCombatState();
                unit.ResetCombatElements();
            }
            return (target, witness, source);
        }

        private IEnumerator Reactions()
        {
            Status = "원소 반응 28쌍";
            DebugReactionLog.Start();

            foreach (var pair in ReactionTable)
            {
                string label = "반응 " + pair.A + "+" + pair.B + " " + pair.Name;
                var (target, witness, source) = ReactionField();
                DebugReactionLog.Restart();

                // 증폭은 부착 직전에 실제로 들어간 피해를 되짚는다. 곱할 피해가 없으면 성립하지 않는다.
                if (pair.Kind == "amplify") Damage(target, source, 500);

                target.GrantCombatElement(pair.A, Unit.CommonElementAuraDuration, source);
                Equal(label + " 첫 부착 무반응", 0, DebugReactionLog.Count);

                int hpBefore = target.HpCurr;
                int witnessHpBefore = witness.HpCurr;
                // 화상의 CON 명중(10~90%)을 통과하는 난수로 고정한다. 별도 검사에서 확률 경계도 판정한다.
                if(pair.Kind == "dot-burn") SeedNextRandomBelow(EffectContest.ConHitChance(source,target));
                target.GrantCombatElement(pair.B, Unit.CommonElementAuraDuration, source);

                Assert(label + " 발동", DebugReactionLog.Last == pair.Name, pair.Name,
                    DebugReactionLog.Count == 0 ? "(무반응)" : DebugReactionLog.Last);

                if (pair.Kind == "spread")
                {
                    Assert(label + " 바람 소모", !target.HasAttachedElement(UnitElement.Anemo), "소모", "남음");
                    Assert(label + " 진영 전체 부착", witness.HasAttachedElement(pair.B), pair.B.ToString(), "없음");
                }
                else
                {
                    Assert(label + " 두 원소 소모",
                        !target.HasAttachedElement(pair.A) && !target.HasAttachedElement(pair.B),
                        "둘 다 소모",
                        (target.HasAttachedElement(pair.A) ? pair.A + " 남음 " : "") +
                        (target.HasAttachedElement(pair.B) ? pair.B + " 남음" : ""));
                }

                switch (pair.Kind)
                {
                    case "dot-burn":
                        Assert(label + " 화상 상태", target.HasStatus(ElementalReaction.BurnStatusId), "화상", "없음");
                        break;
                    case "dot-shock":
                        Assert(label + " 감전 상태", target.HasStatus(ElementalReaction.ShockStatusId), "감전", "없음");
                        break;
                    case "dot-weather":
                        Assert(label + " 풍화 상태", target.HasStatus(ElementalReaction.WeatheringStatusId), "풍화", "없음");
                        break;
                    case "control":
                        Assert(label + " 행동 불가", target.isControlled, "제어됨", "자유");
                        break;
                    case "root":
                        // 착근은 미는 것이지 막는 것이 아니다.
                        Assert(label + " 제어가 아니다", !target.isControlled, "자유", "제어됨");
                        break;
                    case "burst":
                        Assert(label + " 피해", target.HpCurr < hpBefore, "HP 감소", (hpBefore - target.HpCurr).ToString());
                        if (pair.A == UnitElement.Anemo && pair.B == UnitElement.Anemo)
                        {
                            Assert(label + " 진영 전체 피해", witness.HpCurr < witnessHpBefore,
                                "증인 HP 감소", (witnessHpBefore - witness.HpCurr).ToString());
                        }
                        break;
                    case "amplify":
                        Assert(label + " 피해", target.HpCurr < hpBefore, "HP 감소", (hpBefore - target.HpCurr).ToString());
                        break;
                    case "buff":
                        Assert(label + " 스탯 상태", target.HasStatus(ElementalReaction.BuffStatusId), "스탯 상태", "없음");
                        break;
                    case "vuln":
                        Assert(label + " 취약 상태", target.HasStatus(ElementalReaction.VulnerableStatusId), "취약 상태", "없음");
                        break;
                    case "shield":
                        Assert(label + " 보호막", target.ShieldCurr > 0, ">0", target.ShieldCurr.ToString());
                        break;
                    case "null":
                        Equal(label + " 상태 없음", 0, target.ActiveStatuses.Count);
                        break;
                }
                yield return null;
            }

            yield return ReactionSystems();
        }

        /// <summary>우선순위·내부 쿨다운·확산 재귀·제어 분쇄·풍화·아스완.</summary>
        private IEnumerator ReactionSystems()
        {
            Status = "반응 부가 규칙";

            // ── 재부착 내부 쿨다운과 우선순위 ─────────────────────────
            // 쿨다운이 반응을 한 번 걸러야 비로소 두 원소가 공존한다. 공존이 없으면
            // 우선순위 규칙 자체가 발동할 일이 없으므로 두 검사를 한 흐름에서 본다.
            {
                var (target, _, source) = ReactionField();
                DebugReactionLog.Restart();
                target.GrantCombatElement(UnitElement.Pyro, Unit.CommonElementAuraDuration, source);
                target.GrantCombatElement(UnitElement.Electro, Unit.CommonElementAuraDuration, source);
                Assert("과부하 1회차", DebugReactionLog.Last == "과부하", "과부하", DebugReactionLog.Last);

                DebugReactionLog.Restart();
                target.GrantCombatElement(UnitElement.Pyro, Unit.CommonElementAuraDuration, source);
                target.GrantCombatElement(UnitElement.Electro, Unit.CommonElementAuraDuration, source);
                Equal("과부하 재부착 쿨다운으로 불발", 0, DebugReactionLog.Count);
                Assert("쿨다운 중에도 부착은 남는다",
                    target.HasAttachedElement(UnitElement.Pyro) && target.HasAttachedElement(UnitElement.Electro),
                    "불·전기 모두 부착", "소모됨");

                Damage(target, source, 500);
                DebugReactionLog.Restart();
                target.GrantCombatElement(UnitElement.Hydro, Unit.CommonElementAuraDuration, source);
                Assert("우선순위 — 증폭이 지속피해보다 앞선다",
                    DebugReactionLog.Last == "증발", "증발(불+물)", DebugReactionLog.Last + " (감전이면 우선순위 역전)");
                yield return null;
            }

            // ── 확산 재귀 차단 ────────────────────────────────────────
            {
                var (target, witness, source) = ReactionField();
                target.GrantCombatElement(UnitElement.Pyro, Unit.CommonElementAuraDuration, source);
                witness.GrantCombatElement(UnitElement.Pyro, Unit.CommonElementAuraDuration, source);

                DebugReactionLog.Restart();
                target.GrantCombatElement(UnitElement.Anemo, Unit.CommonElementAuraDuration, source);

                Equal("확산 한 번만 발동", 1, DebugReactionLog.Names().Count(name => name == "확산"));
                Assert("확산이 뿌린 부착은 재반응하지 않는다",
                    !DebugReactionLog.Names().Contains("화상"),
                    "화상 없음", string.Join(",", DebugReactionLog.Names()));
                Assert("확산 뒤 진영 전체가 불을 갖는다",
                    target.HasAttachedElement(UnitElement.Pyro) && witness.HasAttachedElement(UnitElement.Pyro),
                    "둘 다 불", target.HasAttachedElement(UnitElement.Pyro) + "/" + witness.HasAttachedElement(UnitElement.Pyro));
                yield return null;
            }

            // ── 제어 분쇄 ─────────────────────────────────────────────
            {
                var (target, _, source) = ReactionField();
                var durations = new List<int>();
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    target.RemoveStatusByKey(ControlStatuses.StunKey);
                    bool applied = ControlStatuses.ApplyFixedStun(target, source, 2);
                    var status = target.ActiveStatuses.FirstOrDefault(entry => entry.Key == ControlStatuses.StunKey);
                    durations.Add(applied && status != null ? status.Duration : 0);
                }
                Assert("제어 분쇄 2턴 기준 2/1/1/면역",
                    durations.SequenceEqual(new List<int> { 2, 1, 1, 0 }), "2,1,1,0", string.Join(",", durations));
                Equal("분쇄 회차 기록", 3, target.ControlAppliedCount);

                var (fresh, _, freshSource) = ReactionField();
                Equal("새 대상은 1회차", 2, ControlStatuses.DiminishedTurns(fresh, 2));
                ControlStatuses.ApplyFixedStun(fresh, freshSource, 2);
                Equal("분쇄는 대상마다 센다", 1, ControlStatuses.DiminishedTurns(fresh, 2));
                yield return null;
            }

            // ── 풍화는 행동마다 터진다 ────────────────────────────────
            {
                var (target, _, source) = ReactionField();
                target.GrantCombatElement(UnitElement.Hydro, Unit.CommonElementAuraDuration, source);
                target.GrantCombatElement(UnitElement.Geo, Unit.CommonElementAuraDuration, source);
                Assert("풍화 부여", target.HasStatus(ElementalReaction.WeatheringStatusId), "풍화", "없음");

                foreach (UnitEventType action in new[]
                {
                    UnitEventType.OnNormalActivates,
                    UnitEventType.OnUltimateActivates,
                    UnitEventType.OnAdditionalActivates,
                })
                {
                    int hp = target.HpCurr;
                    target.Invoke(action, new EventContext(target));
                    Assert("풍화 " + action, target.HpCurr < hp, "HP 감소", (hp - target.HpCurr).ToString());
                }
                yield return null;
            }

            // ── 속성만으로는 같은 원소 반응이 성립하지 않는다 ─────────
            {
                var geoUnit = game.unitDataList.units.FirstOrDefault(unit => unit.element == "Geo");
                if (geoUnit != null)
                {
                    Clear();
                    Unit hero = grid.SpawnUnit(-1, 1, false, geoUnit.id);
                    hero.DebugSetLevel(90);
                    hero.DebugResetCombatState();
                    hero.ResetCombatElements();
                    Unit enemy = SpawnEnemy(1062, 90);

                    DebugReactionLog.Restart();
                    hero.GrantCombatElement(UnitElement.Geo, Unit.CommonElementAuraDuration, enemy);
                    Assert("바위 속성 유닛이 바위를 한 번 받아도 진동이 없다",
                        DebugReactionLog.Count == 0 && !hero.isControlled,
                        "무반응", DebugReactionLog.Count + "건 " + DebugReactionLog.Last);
                }
                yield return null;
            }

            // ── 아스완 화형 선고 ──────────────────────────────────────
            {
                Clear();
                Unit inquisitor = SpawnEnemy(1051, 90);
                Unit hero = SpawnHero();
                RoundStart(inquisitor);
                hero.DebugResetCombatState();
                hero.ResetCombatElements();

                // 화상은 CON 명중 판정을 거치므로 한 번으로는 확정할 수 없다.
                bool burned = false;
                for (int attempt = 0; attempt < 10 && !burned; attempt++)
                {
                    hero.DebugResetCombatState();
                    hero.ResetCombatElements();
                    AswanCombat.GrantPyro(inquisitor, hero);
                    burned = hero.HasStatus(ElementalReaction.BurnStatusId);
                }
                Assert("화형 선고 — 불 부착이 소모되지 않는다",
                    hero.HasAttachedElement(UnitElement.Pyro), "불 부착 유지", "없음");
                Assert("화형 선고 — 화상을 함께 건다", burned, "10회 내 화상", "한 번도 없음");

                Clear();
                Unit plain = SpawnEnemy(1062, 90);
                Unit hero2 = SpawnHero();
                hero2.DebugResetCombatState();
                hero2.ResetCombatElements();
                AswanCombat.GrantPyro(plain, hero2);
                Assert("심문관이 없으면 불만 붙는다",
                    hero2.HasAttachedElement(UnitElement.Pyro) && !hero2.HasStatus(ElementalReaction.BurnStatusId),
                    "불만", "화상 동반");
                yield return null;
            }

            DebugReactionLog.Dump();
        }

        private IEnumerator LiveBattle()
        {
            Status="실제 스케줄러 전투 및 승패 전환";
            foreach(int slot in new[]{1,3,4,6,7,8,9,10})
            {
                Clear();
                DebugMode.ForcedThemeId=8;
                SpawnHero();SpawnHero(-2,1);
                game.DebugLoadStage(30+slot);
                DebugMode.AllyInvincible=true;
                game.StartRound();
                float deadline=Time.realtimeSinceStartup+20;
                while(game.gameState==GameState.RoundInProgress&&game.ActionScheduler.TurnsTaken<8&&Time.realtimeSinceStartup<deadline) yield return null;
                Assert("live slot "+slot+" makes progress",game.ActionScheduler.TurnsTaken>=8||game.gameState!=GameState.RoundInProgress,"8 turns or battle ends",game.ActionScheduler.TurnsTaken+" / "+game.gameState);
                if(game.gameState==GameState.RoundInProgress) game.DebugEndBattle(true);
                Equal("live slot "+slot+" party restored",2,Heroes.Count);
                DebugMode.AllyInvincible=false;
                yield return null;
            }
        }
    }
}
#endif
