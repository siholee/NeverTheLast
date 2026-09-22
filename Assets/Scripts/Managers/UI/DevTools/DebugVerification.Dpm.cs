#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BaseClasses;
using Codes.Base;
using Codes.Normal;
using Combat;
using Core;
using Effects.Buffs;
using Entities;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Managers.UI.DevTools
{
    public sealed partial class DebugVerification
    {
        private const float DpmAuditCombatSeconds = 60f;

        [Serializable]
        private sealed class DpmSample
        {
            public int unitId;
            public string unitName;
            public int level;
            public float combatSeconds;
            public long totalDamage;
            public float dpm;
            public long normalDamage;
            public long ultimateDamage;
            public long additionalDamage;
            public long passiveDamage;
            public float targetMultiplier;
            public float normalizedDpm;
            public float deviationPercent;
        }

        [Serializable]
        private sealed class DpmReport
        {
            public string generatedUtc;
            public float requestedCombatSeconds;
            public string dummyRule;
            public List<DpmSample> samples = new();
            public List<PartyDpmSample> partySamples = new();
        }

        [Serializable]
        private sealed class PartyUnitDamage
        {
            public int unitId;
            public string unitName;
            public long damage;
            public float dpm;
            public long normalDamage;
            public long additionalDamage;
            public long otherDamage;
        }

        [Serializable]
        private sealed class PartyDpmSample
        {
            public string key;
            public string name;
            public int level;
            public int enemyCount;
            public float combatSeconds;
            public long totalDamage;
            public float dpm;
            public List<PartyUnitDamage> units = new();
        }

        /// <summary>
        /// 외부 지원이 없는 단독 딜러를 행동치 기준 1분간 굴린다.
        /// 허수아비는 행동·회피·방어·내구가 없고 매 피해 뒤 체력을 회복한다.
        /// 대상 최대 체력에 따라 값이 바뀌는 지속/반응 피해는 비교에서 제외한다.
        /// </summary>
        private IEnumerator SoloDpmAudit()
        {
            Status = "단독 DPM Lv.1/50/100";
            var output = new DpmReport
            {
                generatedUtc = DateTime.UtcNow.ToString("O"),
                requestedCombatSeconds = DpmAuditCombatSeconds,
                dummyRule = "solo; zero defense/durability/evasion; inactive immortal dummy; direct damage only"
            };

            float previousScale = Time.timeScale;
            bool previousLogging = Debug.unityLogger.logEnabled;
            DebugMode.SetTimeScale(20f);
            // 검증 전용 배치에서는 HUD 상한보다 빠르게 돌린다. 행동 순서는 AV로 결정되며
            // 이 값은 연출 대기만 줄이므로 측정 DPM에는 영향을 주지 않는다.
            Time.timeScale = 100f;
            // 장기 전투에서 매 타격 로그가 파일 I/O를 일으키면 실제 시간 제한이 먼저 끝난다.
            // 검증 결과는 Assert 목록과 JSON에 남기므로 측정 구간의 일반 로그만 잠시 끈다.
            Debug.unityLogger.logEnabled = false;
            try
            {
                foreach (int level in new[] { 1, 50, 100 })
                {
                    foreach (int unitId in new[] { 2, 24, 40, 82, 101, 4, 6 })
                    {
                        DpmSample sample = null;
                        yield return MeasureSoloDpm(unitId, level, value => sample = value);
                        if (sample != null) output.samples.Add(sample);
                    }

                    foreach ((string key, string name, int[] ids, int enemyCount) in PartyAuditLineups())
                    {
                        PartyDpmSample party = null;
                        yield return MeasurePartyDpm(key, name, ids, enemyCount, level,
                            value => party = value);
                        if (party != null) output.partySamples.Add(party);
                    }
                }
            }
            finally
            {
                Debug.unityLogger.logEnabled = previousLogging;
                DebugMode.SetTimeScale(previousScale);
                Clear();
            }

            AgrippaPriorityAudit();
            DpmArchetypeAudit();
            GaudiBastetDataAudit();
            yield return LightMechanicsAudit();
            yield return JasonMechanicsAudit();
            Clear();

            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs"));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "SoloDpmAudit.json"),
                JsonUtility.ToJson(output, true));

            Assert("solo DPM audit sample count", output.samples.Count == 21,
                "21", output.samples.Count.ToString());

            foreach (IGrouping<int, DpmSample> levelGroup in output.samples
                         .Where(sample => sample.unitId is 2 or 24 or 40 or 82 or 101)
                         .GroupBy(sample => sample.level))
            {
                List<DpmSample> ordered = levelGroup.OrderBy(sample => sample.normalizedDpm).ToList();
                float baseline = ordered[ordered.Count / 2].normalizedDpm;
                foreach (DpmSample sample in ordered)
                {
                    sample.deviationPercent = baseline <= 0f
                        ? 0f
                        : (sample.normalizedDpm / baseline - 1f) * 100f;
                    Assert($"solo DPM {sample.unitName} Lv.{sample.level} within ±20%",
                        Mathf.Abs(sample.deviationPercent) <= 20f,
                        "-20.0%..+20.0%", $"{sample.deviationPercent:+0.0;-0.0;0.0}%");
                }
            }

            foreach (int level in new[] { 1, 50, 100 })
            {
                DpmSample gaudi = output.samples.FirstOrDefault(sample => sample.unitId == 4 && sample.level == level);
                DpmSample nicole = output.samples.FirstOrDefault(sample => sample.unitId == 6 && sample.level == level);
                float ratio = gaudi == null || nicole == null || nicole.dpm <= 0f ? 0f : gaudi.dpm / nicole.dpm;
                Assert($"Gaudi matches Nicole DPM Lv.{level}", ratio >= 0.8f && ratio <= 1.2f,
                    "80%..120%", $"{ratio * 100f:0.0}%");

                DpmSample shi = output.samples.FirstOrDefault(sample => sample.unitId == 2 && sample.level == level);
                PartyDpmSample bastetBenchmark = output.partySamples.FirstOrDefault(sample =>
                    sample.key == "bastet_counter_benchmark" && sample.level == level);
                PartyUnitDamage bastet = bastetBenchmark?.units.FirstOrDefault(unit => unit.unitId == 122);
                float bastetRatio = shi == null || bastet == null || shi.dpm <= 0f ? 0f : bastet.dpm / shi.dpm;
                Assert($"Bastet two-counter DPM matches Shi Lv.{level}",
                    bastetRatio >= 0.8f && bastetRatio <= 1.2f,
                    "80%..120%", $"{bastetRatio * 100f:0.0}%");

                PartyDpmSample counter = output.partySamples.FirstOrDefault(sample =>
                    sample.key == "bastet_counter" && sample.level == level);
                PartyDpmSample surtr = output.partySamples.FirstOrDefault(sample =>
                    sample.key == "surtr_recommended" && sample.level == level);
                PartyDpmSample sabah = output.partySamples.FirstOrDefault(sample =>
                    sample.key == "sabah_recommended" && sample.level == level);
                Assert($"Bastet counter party exceeds Surtr Lv.{level}",
                    counter != null && surtr != null && counter.dpm > surtr.dpm,
                    $">{surtr?.dpm:0}", $"{counter?.dpm:0}");
                float sabahRatio = counter == null || sabah == null || sabah.dpm <= 0f
                    ? 0f
                    : counter.dpm / sabah.dpm;
                Assert($"Bastet counter party is comparable to Sabah Lv.{level}",
                    sabahRatio >= 0.8f,
                    ">=80%", $"{sabahRatio * 100f:0.0}%");
            }

            // 편차 계산 결과까지 보고서에 남긴다.
            File.WriteAllText(Path.Combine(directory, "SoloDpmAudit.json"),
                JsonUtility.ToJson(output, true));
        }

        private static IEnumerable<(string key, string name, int[] ids, int enemyCount)> PartyAuditLineups()
        {
            yield return ("bastet_counter_benchmark", "바스테트 2반격 무버프", new[] { 22, 7, 122 }, 1);
            // 배치 순서: 오리온·잔 전열, 메인 바스테트·세이·힐러/디버퍼 후열.
            yield return ("bastet_counter", "바스테트-오리온-잔-세이-바유", new[] { 22, 7, 122, 1, 64 }, 4);
            yield return ("bastet_light_beginner", "바스테트-오리온-잔-세이-라이트", new[] { 22, 7, 122, 1, 5 }, 4);
            yield return ("surtr_recommended", "수르트-아그리파-스카디-세이-프레이아", new[] { 80, 83, 100, 1, 81 }, 4);
            yield return ("sabah_recommended", "사바흐-니콜-스카디-바유-세이", new[] { 83, 3, 6, 64, 1 }, 4);
            // 이아손·찬드라 전열, 옥타비아·아그리파·라이트 후열.
            yield return ("octavia_recommended", "옥타비아-아그리파-이아손-찬드라-라이트",
                new[] { 103, 60, 101, 100, 5 }, 4);
        }

        /// <summary>이아손의 궁극기 반응 제한·턴 리셋·누적 버프·궁극기 INT 버프를 실제 행동으로 검증한다.</summary>
        private IEnumerator JasonMechanicsAudit()
        {
            Status = "이아손 전투 메커니즘";
            Clear();

            Unit jason = grid.SpawnUnit(-1, 1, false, 103);
            // 직전 파티 DPM의 후열 첫 칸은 퇴장 직후 예약 시간이 남아 있을 수 있다.
            // 이 검증은 배치가 목적이 아니므로 직전 표본이 사용하지 않은 전열 3행을 쓴다.
            Unit octavia = grid.SpawnUnit(-1, 3, false, 101);
            Unit enemy = SpawnEnemy(1020, 100, 1, 1);
            Assert("Jason mechanics fixture", jason != null && octavia != null && enemy != null,
                "Jason/Octavia/enemy", $"{jason != null}/{octavia != null}/{enemy != null}");
            if (jason == null || octavia == null || enemy == null) yield break;

            jason.DebugSetLevel(70);
            octavia.DebugSetLevel(70);
            grid.OnRoundStart();
            MakeUnavoidable(jason);
            MakeUnavoidable(octavia);
            MakeUnavoidable(enemy);

            Assert("Jason Gunner aura reaches ally",
                octavia.ActiveStatuses.Any(status => status.Key.StartsWith("jason_gunner_")),
                "Gunner status", string.Join(",", octavia.ActiveStatuses.Select(status => status.Key)));

            int specialHits = 0;
            enemy.AddListener<EventContext>(UnitEventType.OnAfterDamageTaken, context =>
            {
                if (context?.DmgCtx?.Attacker == jason && context.DmgCtx.CodeType == CodeType.Special)
                    specialHits++;
                enemy.ModifyHp(enemy.HpMax);
            });

            game.ActionScheduler.BeginRound();
            game.gameState = GameState.RoundInProgress;
            octavia.FillUltimateResource(false);
            octavia.CastUltimateCode();
            // 같은 턴 안에서 궁극기 신호를 두 번 발생시켜도 이아손은 한 번만 예약해야 한다.
            octavia.FillUltimateResource(false);
            octavia.CastUltimateCode();
            yield return Settle(1.5f);
            Equal("Jason reacts once to allied ultimate", 1, specialHits);
            Equal("Jason SP grants one ultimate stack", 1,
                octavia.ActiveStatuses.Count(status => status.Key == "jason_argonaut_support_stack"));
            Equal("Jason cannot react twice before own turn", 1, specialHits);

            jason.BeginTurn();
            octavia.FillUltimateResource(false);
            octavia.CastUltimateCode();
            yield return Settle(1.5f);
            Equal("Jason reaction resets on own turn", 2, specialHits);
            Equal("Jason SP ultimate bonus stacks", 2,
                octavia.ActiveStatuses.Count(status => status.Key == "jason_argonaut_support_stack"));

            int intBefore = octavia.GetBaseInt();
            int expectedIntBonus = Mathf.FloorToInt(jason.GetBaseInt() * 0.5f);
            jason.FillUltimateResource(false);
            jason.CastUltimateCode();
            yield return Settle(1.2f);
            Equal("Jason ultimate grants half INT", intBefore + expectedIntBonus, octavia.GetBaseInt());
        }

        private IEnumerator MeasurePartyDpm(
            string key, string name, int[] unitIds, int enemyCount, int level,
            Action<PartyDpmSample> completed)
        {
            Clear();
            UnityEngine.Random.InitState(20260922 + level * 1009 + key.GetHashCode());

            var heroes = new List<Unit>();
            for (int i = 0; i < unitIds.Length; i++)
            {
                bool soloFrontline = key == "sabah_recommended";
                int frontCount = soloFrontline ? 1 : 2;
                int x = i < frontCount ? -1 : -2;
                int y = i < frontCount ? i + 1 : i - frontCount + 1;
                Unit hero = grid.SpawnUnit(x, y, false, unitIds[i]);
                if (hero != null)
                {
                    hero.DebugSetLevel(level);
                    heroes.Add(hero);
                }
            }

            var enemies = new List<Unit>();
            for (int i = 0; i < enemyCount; i++)
            {
                int x = i < 2 ? 1 : 2;
                int y = i % 2 + 1;
                Unit enemy = SpawnEnemy(1020 + i, level, x, y);
                if (enemy != null) enemies.Add(enemy);
            }

            if (heroes.Count != unitIds.Length || enemies.Count != enemyCount)
            {
                Assert($"party DPM fixture {name} Lv.{level}", false,
                    $"{unitIds.Length}/{enemyCount}", $"{heroes.Count}/{enemies.Count}");
                completed(null);
                yield break;
            }

            grid.OnRoundStart();
            foreach (Unit enemy in enemies)
            {
                foreach (PassiveCode passive in enemy.ActivePassiveCodes.ToList()) passive?.StopCode();
                enemy.DebugClearStatuses();
                MakeUnavoidable(enemy);
                enemy.AddStatus(BuffStatus.Create(
                    999902, $"verification_party_dpm_health_{enemy.GetEntityId()}", "검증: 파티 DPM 체력",
                    enemy, enemy, new PrimaryStatBonusBuffEffect(PrimaryStat.CON, 1000),
                    stackPolicy: StatusStackPolicy.Ignore, isBeneficial: true));
                enemy.ModifyHp(enemy.HpMax);
            }
            foreach (Unit hero in heroes) MakeUnavoidable(hero);

            var damageByUnit = heroes.ToDictionary(unit => unit, _ => 0L);
            var normalByUnit = heroes.ToDictionary(unit => unit, _ => 0L);
            var additionalByUnit = heroes.ToDictionary(unit => unit, _ => 0L);
            Action<EventContext> zeroDefense = context =>
            {
                if (context?.DmgCtx == null) return;
                context.DmgCtx.DefenseStatMultiplier = 0f;
                context.DmgCtx.DamageTags ??= new List<int>();
                if (!context.DmgCtx.DamageTags.Contains(DamageTag.DurabilityPenetration))
                    context.DmgCtx.DamageTags.Add(DamageTag.DurabilityPenetration);
            };
            foreach (Unit enemy in enemies) enemy.AddListener(UnitEventType.OnBeforeDamageTaken, zeroDefense);

            Action<DamageResolvedContext> damageHandler = context =>
            {
                if (context?.Target == null || context.DamageDealt <= 0) return;
                if (enemies.Contains(context.Target))
                {
                    context.Target.ModifyHp(context.Target.HpMax);
                    Unit owner = context.Attacker?.SummonOwner ?? context.Attacker;
                    if (owner != null && damageByUnit.ContainsKey(owner))
                    {
                        damageByUnit[owner] += context.DamageDealt;
                        if (context.DamageContext?.DamageTags?.Contains(DamageTag.AdditionalAttack) == true)
                            additionalByUnit[owner] += context.DamageDealt;
                        else if (context.DamageContext?.CodeType == CodeType.Normal)
                            normalByUnit[owner] += context.DamageDealt;
                    }
                }
                else if (heroes.Contains(context.Target))
                {
                    context.Target.ModifyHp(context.Target.HpMax);
                }
            };
            Unit.AnyDamageDealt += damageHandler;

            game.ActionScheduler.BeginRound();
            game.gameState = GameState.RoundInProgress;
            float timeout = Time.realtimeSinceStartup + 30f;
            while (game.ActionScheduler.CombatSecondsElapsed < DpmAuditCombatSeconds &&
                   Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            game.gameState = GameState.Preparation;
            float settleUntil = Time.realtimeSinceStartup + 2f;
            while (Heroes.Concat(Enemies).Any(unit => unit != null && unit.isCasting) &&
                   Time.realtimeSinceStartup < settleUntil)
            {
                yield return null;
            }

            Unit.AnyDamageDealt -= damageHandler;
            foreach (Unit enemy in enemies) enemy.RemoveListener(UnitEventType.OnBeforeDamageTaken, zeroDefense);

            float elapsed = Mathf.Max(0.001f, game.ActionScheduler.CombatSecondsElapsed);
            Assert($"party DPM {name} Lv.{level} reaches duration",
                elapsed >= DpmAuditCombatSeconds,
                $">={DpmAuditCombatSeconds:0}", elapsed.ToString("0.00"));

            var sample = new PartyDpmSample
            {
                key = key,
                name = name,
                level = level,
                enemyCount = enemyCount,
                combatSeconds = elapsed,
                totalDamage = damageByUnit.Values.Sum(),
            };
            sample.dpm = sample.totalDamage * 60f / elapsed;
            sample.units = heroes.Select(unit => new PartyUnitDamage
            {
                unitId = unit.ID,
                unitName = unit.UnitName,
                damage = damageByUnit[unit],
                dpm = damageByUnit[unit] * 60f / elapsed,
                normalDamage = normalByUnit[unit],
                additionalDamage = additionalByUnit[unit],
                otherDamage = damageByUnit[unit] - normalByUnit[unit] - additionalByUnit[unit],
            }).ToList();
            completed(sample);
        }

        private IEnumerator MeasureSoloDpm(int unitId, int level, Action<DpmSample> completed)
        {
            Clear();
            UnityEngine.Random.InitState(20260922 + unitId * 101 + level * 1009);

            Unit dealer = grid.SpawnUnit(-1, 1, false, unitId);
            Unit dummy = SpawnEnemy(1020, 1);
            if (dealer == null || dummy == null)
            {
                Assert($"DPM fixture {unitId} Lv.{level}", false,
                    "dealer and dummy", $"{dealer != null}/{dummy != null}");
                completed(null);
                yield break;
            }

            dealer.DebugSetLevel(level);
            dummy.DebugSetLevel(1);

            grid.OnRoundStart();
            foreach (PassiveCode dummyPassive in dummy.ActivePassiveCodes.ToList()) dummyPassive?.StopCode();
            dummy.DebugClearStatuses();
            MakeUnavoidable(dummy);
            dummy.AddStatus(BuffStatus.Create(
                999902, "verification_dpm_health", "검증: DPM 체력",
                dummy, dummy,
                new PrimaryStatBonusBuffEffect(PrimaryStat.CON, 1000),
                stackPolicy: StatusStackPolicy.Ignore,
                isBeneficial: true));
            dummy.ModifyHp(dummy.HpMax);
            dummy.ApplyControlAtLeast(dealer, 10000);

            Action<EventContext> zeroDefense = context =>
            {
                if (context?.DmgCtx == null) return;
                context.DmgCtx.DefenseStatMultiplier = 0f;
                context.DmgCtx.DamageTags ??= new List<int>();
                if (!context.DmgCtx.DamageTags.Contains(DamageTag.DurabilityPenetration))
                    context.DmgCtx.DamageTags.Add(DamageTag.DurabilityPenetration);
            };
            dummy.AddListener(UnitEventType.OnBeforeDamageTaken, zeroDefense);

            long total = 0;
            long normal = 0;
            long ultimate = 0;
            long additional = 0;
            long passiveDamage = 0;
            Action<DamageResolvedContext> damageHandler = context =>
            {
                if (context?.Target != dummy || context.DamageDealt <= 0) return;

                // 피해 이벤트는 사망 판정보다 먼저 오므로 이 자리에서 되돌리면 허수아비가 쓰러지지 않는다.
                dummy.ModifyHp(dummy.HpMax);

                Unit attacker = context.Attacker;
                bool belongsToDealer = attacker == dealer || attacker?.SummonOwner == dealer;
                if (!belongsToDealer || context.DamageContext?.CodeType == CodeType.Effect) return;

                int amount = context.DamageDealt;
                total += amount;
                if (context.DamageContext.DamageTags?.Contains(DamageTag.AdditionalAttack) == true)
                    additional += amount;
                else if (context.DamageContext.CodeType == CodeType.Normal)
                    normal += amount;
                else if (context.DamageContext.CodeType == CodeType.Ultimate)
                    ultimate += amount;
                else
                    passiveDamage += amount;
            };
            Unit.AnyDamageDealt += damageHandler;

            game.ActionScheduler.BeginRound();
            game.gameState = GameState.RoundInProgress;
            float timeout = Time.realtimeSinceStartup + 25f;
            while (game.ActionScheduler.CombatSecondsElapsed < DpmAuditCombatSeconds &&
                   Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            game.gameState = GameState.Preparation;
            float settleUntil = Time.realtimeSinceStartup + 2f;
            while (Heroes.Concat(Enemies).Any(unit => unit != null && unit.isCasting) &&
                   Time.realtimeSinceStartup < settleUntil)
            {
                yield return null;
            }

            Unit.AnyDamageDealt -= damageHandler;
            dummy.RemoveListener(UnitEventType.OnBeforeDamageTaken, zeroDefense);

            float elapsed = Mathf.Max(0.001f, game.ActionScheduler.CombatSecondsElapsed);
            Assert($"DPM fixture {dealer.UnitName} Lv.{level} reaches duration",
                elapsed >= DpmAuditCombatSeconds,
                $">={DpmAuditCombatSeconds:0}", elapsed.ToString("0.00"));

            float roleMultiplier = TargetDpmMultiplier(unitId, level);
            float dpm = total * 60f / elapsed;
            completed(new DpmSample
            {
                unitId = unitId,
                unitName = dealer.UnitName,
                level = level,
                combatSeconds = elapsed,
                totalDamage = total,
                dpm = dpm,
                normalDamage = normal,
                ultimateDamage = ultimate,
                additionalDamage = additional,
                passiveDamage = passiveDamage,
                targetMultiplier = roleMultiplier,
                normalizedDpm = dpm / roleMultiplier,
            });
        }

        private static float TargetDpmMultiplier(int unitId, int level)
        {
            return unitId switch
            {
                // Lv.1은 1.0, Lv.100은 1.2. 중간 레벨은 선형 보간한다.
                2 => Mathf.Lerp(1f, 1.2f, Mathf.Clamp01((level - 1f) / 99f)),
                24 => 0.9f,
                82 => 0.8f,
                _ => 1f,
            };
        }

        private void AgrippaPriorityAudit()
        {
            Unit agrippa = grid.SpawnUnit(-1, 1, false, 100);
            Unit octavia = grid.SpawnUnit(-2, 1, false, 101);
            Unit asclepius = grid.SpawnUnit(-1, 2, false, 24);
            Unit indra = grid.SpawnUnit(-2, 2, false, 63);
            Assert("Agrippa priority fixture", agrippa != null && octavia != null &&
                    asclepius != null && indra != null,
                "four allies", $"{agrippa?.ID}/{octavia?.ID}/{asclepius?.ID}/{indra?.ID}");
            if (agrippa == null || octavia == null || asclepius == null || indra == null) return;

            FieldInfo manaMaxField = typeof(Unit).GetField("manaMax",
                BindingFlags.Instance | BindingFlags.NonPublic);
            manaMaxField?.SetValue(asclepius, 140);
            Equal("Agrippa prioritizes Burst before mana pool", octavia.ID,
                LegionNormal.SelectAgrippaSupportTarget(agrippa)?.ID ?? -1);

            octavia.isActive = false;
            Equal("Agrippa prioritizes largest mana pool before INT", asclepius.ID,
                LegionNormal.SelectAgrippaSupportTarget(agrippa)?.ID ?? -1);

            manaMaxField?.SetValue(asclepius, 100);
            Equal("Agrippa uses INT as final tiebreaker", indra.ID,
                LegionNormal.SelectAgrippaSupportTarget(agrippa)?.ID ?? -1);
        }

        private void DpmArchetypeAudit()
        {
            var expected = new Dictionary<int, string[]>
            {
                [2] = new[] { "Precision" },
                [24] = new[] { "Precision", "Healing" },
                [40] = new[] { "Precision" },
                [82] = new[] { "Swift", "Precision", "Control" },
                [101] = new[] { "Burst" },
            };

            foreach (KeyValuePair<int, string[]> pair in expected)
            {
                UnitData definition = game.unitDataList.units.FirstOrDefault(unit => unit.id == pair.Key);
                Assert($"DPM archetype tags unit {pair.Key}", definition?.archetypeTags != null &&
                        new HashSet<string>(definition.archetypeTags).SetEquals(pair.Value),
                    string.Join(",", pair.Value),
                    string.Join(",", definition?.archetypeTags ?? new List<string>()));
            }

            UnitData octavia = game.unitDataList.units.FirstOrDefault(unit => unit.id == 101);
            Assert("Octavia no longer has Sharp overcrit passive",
                octavia?.levelPassives?.All(code => code.codeId != 17) == true,
                "code 17 absent", string.Join(",", octavia?.levelPassives?.Select(code => code.codeId)
                    ?? Enumerable.Empty<int>()));

            UnitData jason = game.unitDataList.units.FirstOrDefault(unit => unit.id == 103);
            int[] jasonCodes = { 109, 139, 16, 27, 141, 67 };
            int[] jasonLevels = { 1, 10, 14, 15, 45, 70 };
            Assert("Jason is initial Burst/Swift/Support card", jason?.characterType == "Support" &&
                    jason.canStartAsSupport && !jason.canStartAsMain &&
                    new HashSet<string>(jason.archetypeTags ?? new List<string>())
                        .SetEquals(new[] { "Burst", "Swift", "Support" }),
                "Support/Burst,Swift,Support", $"{jason?.characterType}/{string.Join(",", jason?.archetypeTags ?? new List<string>())}");
            Assert("Jason unlocks Octavia on support clear",
                jason?.unlocksUnitIdsOnClear?.SequenceEqual(new[] { 101 }) == true &&
                octavia != null && !CharacterSelectionManager.HasNoUnlockPath(octavia),
                "Jason -> Octavia explicit path", "missing");
            Assert("Jason unlock progression", jason?.levelPassives != null &&
                    jason.levelPassives.Select(passive => passive.codeId).SequenceEqual(jasonCodes) &&
                    jason.levelPassives.Select(passive => passive.unlockLevel).SequenceEqual(jasonLevels),
                string.Join(",", jasonCodes),
                string.Join(",", jason?.levelPassives?.Select(passive => passive.codeId)
                    ?? Enumerable.Empty<int>()));

            SynergyRecommendationData octaviaRecommendation = SynergyCatalog.RecommendationFor(101);
            var octaviaMembers = new HashSet<int> { 100, 103, 60, 5 };
            Assert("Octavia recommendation uses Agrippa-Jason-Chandra-Light",
                octaviaRecommendation?.best?.members != null &&
                octaviaMembers.SetEquals(octaviaRecommendation.best.members) &&
                octaviaRecommendation.basic?.members != null &&
                octaviaMembers.SetEquals(octaviaRecommendation.basic.members),
                "100,103,60,5", string.Join(",", octaviaRecommendation?.best?.members ?? new List<int>()));
        }

        private void GaudiBastetDataAudit()
        {
            UnitData gaudiData = game.unitDataList.units.FirstOrDefault(unit => unit.id == 4);
            int[] gaudiCodes = { 56, 64, 133, 119, 134, 135, 136 };
            int[] gaudiLevels = { 1, 8, 14, 25, 33, 46, 52 };
            Assert("Gaudi archetype tags", gaudiData?.archetypeTags != null &&
                    new HashSet<string>(gaudiData.archetypeTags).SetEquals(new[] { "Precision", "Infusion" }),
                "Precision,Infusion", string.Join(",", gaudiData?.archetypeTags ?? new List<string>()));
            Assert("Gaudi unlock progression", gaudiData?.levelPassives != null &&
                    gaudiData.levelPassives.Select(passive => passive.codeId).SequenceEqual(gaudiCodes) &&
                    gaudiData.levelPassives.Select(passive => passive.unlockLevel).SequenceEqual(gaudiLevels),
                string.Join(",", gaudiCodes),
                string.Join(",", gaudiData?.levelPassives?.Select(passive => passive.codeId)
                    ?? Enumerable.Empty<int>()));

            UnitData bastetData = game.unitDataList.units.FirstOrDefault(unit => unit.id == 122);
            Assert("Bastet is a starter main", bastetData?.characterType == "Starter" &&
                    bastetData.canStartAsMain && !bastetData.canStartAsSupport && bastetData.canUseInInfinite,
                "Starter/main-only", $"{bastetData?.characterType}/{bastetData?.canStartAsMain}");
            UnitData orionData = game.unitDataList.units.FirstOrDefault(unit => unit.id == 22);
            Assert("Orion is an initial support card", orionData?.characterType == "Support" &&
                    orionData.canStartAsSupport && !orionData.canStartAsMain && !orionData.canUseInInfinite,
                "Support/support-only", $"{orionData?.characterType}/{orionData?.canStartAsSupport}");
            Assert("Bastet archetype tags", bastetData?.archetypeTags != null &&
                    new HashSet<string>(bastetData.archetypeTags).SetEquals(new[] { "Precision", "Swift" }),
                "Precision,Swift", string.Join(",", bastetData?.archetypeTags ?? new List<string>()));

            SynergyRecommendationData sabah = SynergyCatalog.RecommendationFor(3);
            var expectedSabah = new HashSet<int> { 6, 83, 64, 1 };
            Assert("Sabah recommendation uses Nicole-Skadi-Vayu-Sei",
                sabah?.best?.members != null && expectedSabah.SetEquals(sabah.best.members),
                "6,83,64,1", string.Join(",", sabah?.best?.members ?? new List<int>()));

            Clear();
            Unit gaudi = grid.SpawnUnit(-1, 1, false, 4);
            Unit enemy = SpawnEnemy(1020, 1);
            gaudi?.DebugSetLevel(100);
            if (gaudi == null || enemy == null)
            {
                Assert("Gaudi passive fixture", false, "Gaudi/enemy", $"{gaudi != null}/{enemy != null}");
                return;
            }

            grid.OnRoundStart();
            int conBefore = gaudi.GetBaseCon();
            enemy.GrantCombatElement(UnitElement.Dendro, Unit.CommonElementAuraDuration, gaudi);
            Equal("Gaudi Infuser grants CON +10", conBefore + 10, gaudi.GetBaseCon());
            Assert("Gaudi Hacker applies defense -20%", enemy.HasStatus(Codes.Passive.GaudiStatusIds.Hacker),
                "Hacker negative status", string.Join(",", enemy.ActiveStatuses.Select(status => status.StatusName)));

            gaudi.ResetCombatElements();
            gaudi.BeginTurn();
            Assert("Gaudi Nature Understanding attaches Geo", gaudi.HasAttachedElement(UnitElement.Geo),
                "Geo attached", gaudi.GetCombatElementDisplay());
            gaudi.EndTurn();
            Equal("Gaudi Recharge rest bonus", 20, Codes.Passive.GaudiRecharge.ConditionChanceBonus);
            Equal("Gaudi Recharge party rest chance", 40, TrainingManager.GetRestConditionUpChance());
        }

        private IEnumerator LightMechanicsAudit()
        {
            Clear();
            Unit orion = grid.SpawnUnit(-1, 1, false, 22);
            Unit light = grid.SpawnUnit(-2, 3, false, 5);
            Unit sei = grid.SpawnUnit(-2, 2, false, 1);
            Unit enemy = SpawnEnemy(1020, 50);
            light?.DebugSetLevel(100);

            Assert("Light mechanics fixture", orion != null && light != null && sei != null && enemy != null,
                "four units", $"{orion != null}/{light != null}/{sei != null}/{enemy != null}");
            if (orion == null || light == null || sei == null || enemy == null) yield break;

            grid.OnRoundStart();

            UnitData data = game.unitDataList.units.FirstOrDefault(unit => unit.id == 5);
            int[] expectedCodes = { 112, 137, 68, 22, 138, 5 };
            int[] expectedLevels = { 1, 1, 15, 20, 32, 35 };
            Assert("Light archetype tags", data?.archetypeTags != null &&
                    new HashSet<string>(data.archetypeTags).SetEquals(new[] { "Summon", "Support", "Healing" }),
                "Summon,Support,Healing", string.Join(",", data?.archetypeTags ?? new List<string>()));
            Assert("Light unlock progression", data?.levelPassives != null &&
                    data.levelPassives.Select(passive => passive.codeId).SequenceEqual(expectedCodes) &&
                    data.levelPassives.Select(passive => passive.unlockLevel).SequenceEqual(expectedLevels),
                string.Join(",", expectedCodes),
                string.Join(",", data?.levelPassives?.Select(passive => passive.codeId) ?? Enumerable.Empty<int>()));

            Codes.Passive.LightGrit grit = light.ActivePassiveCodes
                .OfType<Codes.Passive.LightGrit>().FirstOrDefault();
            Assert("Light Grit reduces seated failure rate by 25%",
                grit != null && Mathf.Approximately(grit.SupportTrainingFailureRateMultiplier, 0.75f),
                "0.75", grit?.SupportTrainingFailureRateMultiplier.ToString("0.00") ?? "missing");
            Assert("Light Summoner grants stackable summon bonus",
                Mathf.Approximately(Summons.DamageMultiplier(light), 1.10f),
                "1.10", Summons.DamageMultiplier(light).ToString("0.00"));

            light.CastNormalCode();
            yield return Settle(3f);
            Unit flyer = light.ActiveSummons.FirstOrDefault(summon => summon != null && summon.isActive);
            Assert("Light normal summons Flyer", flyer != null && flyer.UnitName == "플라이어",
                "active Flyer", flyer?.UnitName ?? "none");
            if (flyer == null) yield break;

            int orionBeforeHeal = Mathf.Max(1, orion.HpMax / 2);
            int seiBeforeHeal = Mathf.Max(1, sei.HpMax / 2);
            orion.ModifyHp(orionBeforeHeal);
            sei.ModifyHp(seiBeforeHeal);
            light.CastNormalCode();
            yield return Settle(3f);
            Assert("Light normal heals all allies while Flyer exists",
                orion.HpCurr > orionBeforeHeal && sei.HpCurr > seiBeforeHeal,
                "both healed", $"{orion.HpCurr - orionBeforeHeal}/{sei.HpCurr - seiBeforeHeal}");

            orion.AddUltimateResource(-orion.ManaCurr);
            sei.AddUltimateResource(-sei.ManaCurr);
            int flyerInt = flyer.GetBaseInt();
            flyer.AddUltimateResource(flyer.ManaMax);
            flyer.CastUltimateCode();
            yield return Settle(3f);
            Equal("Flyer ultimate injects Orion mana", Mathf.Min(orion.ManaMax, flyerInt), orion.ManaCurr);
            Equal("Flyer ultimate injects Sei mana", Mathf.Min(sei.ManaMax, flyerInt), sei.ManaCurr);

            SynergyRecommendationData recommendation = SynergyCatalog.RecommendationFor(122);
            var expectedMembers = new HashSet<int> { 22, 7, 1, 5 };
            Assert("Bastet beginner recommendation uses Orion-Jean-Sei-Light",
                recommendation?.starterRecommended == true && recommendation.best?.members != null &&
                expectedMembers.SetEquals(recommendation.best.members),
                "22,7,1,5", string.Join(",", recommendation?.best?.members ?? new List<int>()));
        }
    }
}
#endif
