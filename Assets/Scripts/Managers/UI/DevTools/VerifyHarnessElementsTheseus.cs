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
using Effects.Negative;
using Effects.Vfx;
using Entities;
using UnityEngine;
using Kind = Managers.ActionScheduler.ActionKind;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator EtPrepareEnemies(VerifyScenarioRequest config)
        {
            if (config.trigger != "four_enemies") yield break;
            GridManager.Instance.ClearActiveEnemies();
            foreach (var cell in new[] { (1, 1), (1, 3), (2, 1), (2, 3) })
            {
                Unit foe = GridManager.Instance.SpawnUnit(cell.Item1, cell.Item2, true, 1001, playEntry: false);
                if (foe == null) throw new InvalidOperationException("원소 검증의 4인 적 편성 생성 실패");
                foe.DebugSetLevel(20); YvBoostEnemy(foe); foe.CastPassiveCode();
            }
            yield return WaitUnscaled(.3f);
        }

        private IEnumerator ScenarioElementsTheseus(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); _cfActionIdSequence = 0;
            bool enabled = true;
            yield return YvPrepareScene(config, value => enabled = value);
            yield return EtPrepareEnemies(config);
            DebugMode.AllyInvincible = DebugMode.EnemyInvincible = false;
            Unit hero = CfAlly(21), sei = CfAlly(1), shi = CfAlly(2), enemy = AliveEnemies().First();
            var foes = AliveEnemies();
            void Check(string label, bool ok, string actual = "") => YvCheck("원소·테세우스 " + label, "현행 개편 사양", actual, ok);
            Check("명시적 적 편성", foes.Count == (config.trigger == "four_enemies" ? 4 : 1), foes.Count.ToString());
            foreach (Unit unit in AliveAllies().Concat(foes))
            {
                foreach (var passive in unit.ActivePassiveCodes) passive.StopCode();
                unit.ActiveUltimateCode?.StopCode();
                unit.ClearUltimateResource(); unit.RemoveShield(unit.ShieldCurr); CfNoCrit(unit);
            }
            foreach (Unit target in foes) { YvResetEnemy(target); EhStat(target, BaseEnums.StatType.SpecialDefense, 100); }
            Check("Factory·기존 원화·마나", hero.ActiveNormalCode is TheseusSeaCleaver && hero.ActiveUltimateCode is TheseusReturningCurrent &&
                hero.ManaMax == 60 && hero.Element == "Hydro" && Resources.Load<Sprite>("Sprite/Portraits/Allies/THESEUS_PORTRAIT") != null);
            var table = ElementalReaction.AllReactions().ToList();
            Check("활성 반응 표", table.Count == 21 && table.All(r => r.Name is not ("성장" or "단조" or "경화")) &&
                table.Count(r => r.Name == "풍화") == 2 && table.Count(r => r.Name == "초확산") == 1, table.Count.ToString());
            foreach (int factor in new[] { 1, 2, 3, 4, 5 })
            {
                EhStat(hero, BaseEnums.StatType.SpecialAttack, factor * 100);
                float chance = DamageMitigation.EffectHitChance(hero, enemy, .25f, EffectHitScaling.Proportional);
                Check("파장 명중 곡선 " + factor, Mathf.Abs(chance - Mathf.Min(1, .25f * factor)) < .0001f, chance.ToString("P1"));
            }
            EhStat(hero, BaseEnums.StatType.SpecialAttack, 100);
            Check("통상 명중 보존", Mathf.Abs(DamageMitigation.EffectHitChance(hero, enemy) - .75f) < .0001f);
            foreach (int factor in new[] { 1, 3 })
            {
                EhStat(hero, BaseEnums.StatType.SpecialAttack, factor * 100);
                int hits = 0;
                using (CombatRandom.BeginSeed(20261104))
                    for (int i = 0; i < 4000; i++) if (DamageMitigation.RollEffectHit(hero, enemy, .25f, EffectHitScaling.Proportional)) hits++;
                float observed = hits / 4000f, expected = .25f * factor;
                Check("부착 분포4000회 " + factor, Mathf.Abs(observed - expected) < .025f, observed.ToString("P2"));
            }
            EhStat(hero, BaseEnums.StatType.SpecialAttack, 100);
            int chosenSeed = 0; float secondRandom = 0;
            for (int seed = 1; seed < 10000; seed++)
            {
                using (CombatRandom.BeginSeed(seed))
                {
                    float first = CombatRandom.value, second = CombatRandom.value;
                    if (first < .25f && second > .75f) { chosenSeed = seed; secondRandom = second; break; }
                }
            }
            using (CombatRandom.BeginSeed(chosenSeed))
            {
                bool attached = enemy.GrantCombatElement(BaseEnums.UnitElement.Hydro, source: hero,
                    baseChance: .25f, hitScaling: EffectHitScaling.Proportional);
                Check("부착 단일 명중·난수1회", attached && CombatRandom.value == secondRandom);
            }
            int duration = enemy.GetAttachedElementRemainingTurns(BaseEnums.UnitElement.Hydro);
            bool failed = enemy.GrantCombatElement(BaseEnums.UnitElement.Hydro, duration: 9, source: hero,
                baseChance: 0, hitScaling: EffectHitScaling.Proportional);
            Check("실패 갱신 보존", !failed && enemy.GetAttachedElementRemainingTurns(BaseEnums.UnitElement.Hydro) == duration);

            EhStat(hero, BaseEnums.StatType.SpecialAttack, 200);
            void Reset() { foreach (Unit target in foes) YvResetEnemy(target); hero.ClearUltimateResource(); }
            void Attach(BaseEnums.UnitElement a, BaseEnums.UnitElement b)
            {
                enemy.GrantCombatElement(a, source: hero, suppressReaction: true);
                enemy.GrantCombatElement(b, source: hero);
            }
            Reset();
            using (var log = new YvRecorder())
            {
                int hp = enemy.HpCurr; Attach(BaseEnums.UnitElement.Pyro, BaseEnums.UnitElement.Hydro);
                Check("증발 상쇄", enemy.HpCurr == hp && !enemy.HasAnyCombatElement && log.Reactions.Count(r => r.Name == "증발") == 1 && log.Dealt.Count == 0);
            }
            foreach (var pair in new[] { (BaseEnums.UnitElement.Hydro, BaseEnums.UnitElement.Dendro), (BaseEnums.UnitElement.Pyro, BaseEnums.UnitElement.Electro) })
            {
                Reset(); int hpMax = enemy.HpMax;
                using (var log = new YvRecorder())
                {
                    Attach(pair.Item1, pair.Item2);
                    var damage = log.Incoming.Single();
                    Check("즉발 특공160% " + pair.Item1, damage.Damage == 320 && !damage.IsCrit && damage.HasTag(DamageTag.ElementalReaction) &&
                        enemy.HpMax == hpMax && DamageMitigation.TryGetDefenseStat(damage, out var stat) && stat == BaseEnums.StatType.SpecialDefense);
                }
            }
            Reset();
            using (var log = new YvRecorder())
            {
                YvProbe(hero, enemy, BaseEnums.CodeType.Normal, 1000);
                Attach(BaseEnums.UnitElement.Pyro, BaseEnums.UnitElement.Dendro);
                Check("연소 증폭·화상 없음", log.Reactions.Any(r => r.Name == "연소") && log.Dealt.Any(d => d.DamageContext.HasTag(DamageTag.ElementalReaction)) &&
                    !enemy.HasStatus(ElementalReaction.BurnStatusId));
            }
            Reset();
            using (var log = new YvRecorder())
            {
                Attach(BaseEnums.UnitElement.Anemo, BaseEnums.UnitElement.Anemo);
                Check("초확산 별도 광역60%", log.Reactions.Count(r => r.Name == "초확산") == 1 && log.Incoming.Count == foes.Count &&
                    log.Incoming.All(d => d.Damage == 120 && !d.IsCrit && d.CodeType == BaseEnums.CodeType.Effect), log.Incoming.Count.ToString());
            }
            foreach (var first in new[] { BaseEnums.UnitElement.Hydro, BaseEnums.UnitElement.Dendro })
            {
                Reset(); Attach(first, BaseEnums.UnitElement.Geo);
                var statuses = enemy.GetAllStatuses().Where(s => s.Key == ElementalReaction.WeatheringStatusKey).ToList();
                var tick = statuses.Single().Effects.Select(e => e.EffectObject).OfType<WeatheringEffect>().Single().CreateDamageOverTimeTick();
                Check("동일 풍화45% " + first, statuses.Count == 1 && tick.Damage == 90);
            }
            Reset(); Attach(BaseEnums.UnitElement.Hydro, BaseEnums.UnitElement.Geo);
            var weathering = enemy.GetStatus(ElementalReaction.WeatheringStatusId);
            Attach(BaseEnums.UnitElement.Dendro, BaseEnums.UnitElement.Geo);
            Check("풍화 조합 간 대기 공유", enemy.GetStatus(ElementalReaction.WeatheringStatusId) == weathering &&
                enemy.HasAttachedElement(BaseEnums.UnitElement.Dendro) && enemy.HasAttachedElement(BaseEnums.UnitElement.Geo));
            foreach (var first in new[] { BaseEnums.UnitElement.Pyro, BaseEnums.UnitElement.Cryo })
            {
                Reset(); int hpMax = enemy.HpMax, shield = enemy.ShieldCurr;
                using (var log = new YvRecorder())
                {
                    Attach(first, BaseEnums.UnitElement.Geo);
                    Check("삭제 조합 무반응 " + first, log.Reactions.Count == 0 && enemy.HasAttachedElement(first) &&
                        enemy.HasAttachedElement(BaseEnums.UnitElement.Geo) && enemy.HpMax == hpMax && enemy.ShieldCurr == shield);
                }
            }

            Reset(); hero.CastPassiveCode();
            var waves = hero.ActivePassiveCodes.OfType<TheseusReturningWave>().Single();
            hero.ModifyHp(hero.HpMax / 2);
            int startHp = hero.HpCurr;
            int expectedHeal = Mathf.RoundToInt(hero.StatValue(BaseEnums.StatType.Attack) * (2 - startHp / (float)hero.HpMax));
            using (var log = new YvRecorder())
            {
                yield return NordAction(hero, Kind.Ultimate, _ => hero.FillUltimateResource());
                Check("초월 단일·회복·파장2·마나0", log.Dealt.Count(d => d.Attacker == hero && d.DamageContext.CodeType == BaseEnums.CodeType.Ultimate) == 1 &&
                    hero.HpCurr - startHp == expectedHeal && hero.GetCombatResource(TheseusReturningWave.ResourceId) == 2 && hero.ManaCurr == 0);
            }
            for (int i = 1; i <= 3; i++)
            {
                Reset();
                using (var log = new CfLog(AliveAllies()))
                {
                    yield return NordAction(hero, Kind.Normal, s => s.AdvanceAction(hero, 1));
                    int waveCount = log.Dealt.Count(d => d.Attacker == hero && d.DamageContext.HasTag(DamageTag.AllTarget));
                    Check("일반·파장·완료·마나 " + i, waveCount == (i <= 2 ? foes.Count : 0) &&
                        log.NormalResolved.GetValueOrDefault(hero) == 1 && hero.ManaCurr == 20 &&
                        hero.GetCombatResource(TheseusReturningWave.ResourceId) == Mathf.Max(0, 2 - i), waveCount.ToString());
                }
            }
            waves.Recharge(); Reset();
            using (var log = new CfLog(AliveAllies()))
            {
                yield return NordAction(hero, Kind.Additional, s => s.EnqueueAdditional(hero, "theseus_no_wave", "추가행동", hero.CastNormalCode));
                Check("추가행동은 파장 미소비", hero.GetCombatResource(TheseusReturningWave.ResourceId) == 2 &&
                    log.Dealt.All(d => d.Attacker != hero || !d.DamageContext.HasTag(DamageTag.AllTarget)));
            }
            waves.StopCode(); Check("특성 정리", hero.GetCombatResource(TheseusReturningWave.ResourceId) == 0);
            waves.CastCode(); waves.Recharge(); waves.Recharge();
            Check("재충전은2회 갱신", hero.GetCombatResource(TheseusReturningWave.ResourceId) == 2);
            Reset();
            using (var log = new CfLog(AliveAllies()))
            {
                bool interrupted = false;
                void StopAtHit(VfxSlotPlaybackEvent item)
                {
                    if (item.Caster != hero || item.SkillKey != "theseus.normal" || item.SlotName != "hit" || item.Phase != "start") return;
                    interrupted = true; hero.ActiveNormalCode.StopCode();
                }
                VfxComposer.SlotPlayed += StopAtHit;
                try { hero.CastNormalCode(); yield return new WaitForSeconds(1.5f); }
                finally { VfxComposer.SlotPlayed -= StopAtHit; }
                Check("일반 시전 취소·늦은 콜백 차단", log.Dealt.All(d => d.Attacker != hero) &&
                    interrupted && log.NormalResolved.GetValueOrDefault(hero) == 0 && hero.ManaCurr == 0 && hero.GetCombatResource(TheseusReturningWave.ResourceId) == 2,
                    $"취소{interrupted}/피해{log.Dealt.Count(d => d.Attacker == hero)}/완료{log.NormalResolved.GetValueOrDefault(hero)}/마나{hero.ManaCurr}/파장{hero.GetCombatResource(TheseusReturningWave.ResourceId)}");
            }
            waves.StopCode(); waves.CastCode(); Reset();
            hero.ModifyHp(hero.HpMax / 2); int cancelledHp = hero.HpCurr;
            using (var log = new YvRecorder())
            {
                hero.CastUltimateCode();
                yield return new WaitForSeconds(.1f);
                hero.ActiveUltimateCode.StopCode();
                yield return new WaitForSeconds(1.5f);
                Check("초월 취소·늦은 회복/파장 차단", log.Dealt.All(d => d.Attacker != hero) && hero.HpCurr == cancelledHp &&
                    hero.GetCombatResource(TheseusReturningWave.ResourceId) == 0 && !((TheseusReturningCurrent)hero.ActiveUltimateCode).CompletedNormally);
            }

            Reset(); sei.CastPassiveCode(); EhStat(sei, BaseEnums.StatType.SpecialAttack, 1000);
            using (var log = new YvRecorder())
            {
                bool released = false;
                yield return NordAction(sei, Kind.PriorityAdditional, s => s.EnqueuePriorityAdditional(sei, "theseus_sei_probe", "빅뱅", () =>
                    released = sei.ActivePassiveCodes.OfType<SeiBigBang>().Single().DebugReleaseExecutable()));
                Check("세이7발·부착1회", released && log.Dealt.Count(d => d.Attacker == sei && d.DamageContext.CodeType == BaseEnums.CodeType.Passive) == 7 &&
                    log.Elements.Count(e => e.Source == sei && e.Element == BaseEnums.UnitElement.Geo) == 1);
            }
            foreach (int tier in new[] { 3, 6, 9 })
            {
                Reset(); EhStat(shi, BaseEnums.StatType.SpecialAttack, 1000);
                using (var log = new YvRecorder())
                {
                    yield return ((ShiFinalBell)shi.ActiveUltimateCode).ResolveBell(tier, enemy);
                    Check("만종 대상별 부착 " + tier, log.Elements.Count(e => e.Source == shi) == (tier == 3 ? 1 : foes.Count) &&
                        log.Dealt.Count(d => d.Attacker == shi && d.DamageContext.CodeType == BaseEnums.CodeType.Ultimate) == (tier == 3 ? 1 : foes.Count));
                }
            }
            foreach (string key in new[] { "theseus.normal", "theseus.wave", "theseus.ultimate" })
                Check("VFX 등록 " + key, VfxDefinitionCatalog.Skill(key)?.slots?.Count > 0);
            Reset(); hero.ModifyHp(hero.HpMax / 2);
            foreach (Unit target in AliveEnemies()) target.ModifyHp(1);
            int beforeKillHeal = hero.HpCurr;
            using (var log = new YvRecorder())
            {
                yield return NordAction(hero, Kind.Ultimate, _ => hero.FillUltimateResource());
                Check("초월 처치에도 회복·파장", log.Dealt.Any(d => d.Attacker == hero && !d.Target.isActive) &&
                    hero.HpCurr > beforeKillHeal && hero.GetCombatResource(TheseusReturningWave.ResourceId) == 2);
            }
            hero.ClearUltimateResource(); hero.RecoverMana(27);
            var savedHero = new UnitSaveData { unitId = 21, level = hero.Level, currentHP = hero.HpCurr, ultimateResource = hero.ManaCurr };
            hero.WriteRunState(savedHero);
            waves.StopCode(); hero.ClearUltimateResource();
            hero.RestoreRunState(JsonUtility.FromJson<UnitSaveData>(JsonUtility.ToJson(savedHero)));
            Check("저장 복원 마나·일시 파장 분리", hero.ManaCurr == 27 && hero.GetCombatResource(TheseusReturningWave.ResourceId) == 0 &&
                hero.ActiveNormalCode is TheseusSeaCleaver && hero.ActiveUltimateCode is TheseusReturningCurrent);
            GridManager.Instance.OnRoundEnd();
            Check("전투 종료 파장 초기화", hero.GetCombatResource(TheseusReturningWave.ResourceId) == 0);
            OsFinish(enabled);
        }

        /// <summary>같은 레벨·넥타르32회·T2 활성3칸. 고정된 선부착을 공급한 수동 3N+1U 두 주기 비교.</summary>
        private IEnumerator ScenarioTheseusBuilds(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); _cfActionIdSequence = 0;
            bool enabled = true;
            yield return YvPrepareScene(config, value => enabled = value);
            yield return EtPrepareEnemies(config);
            DebugMode.AllyInvincible = DebugMode.EnemyInvincible = false;
            Unit hero = CfAlly(21), provider = CfAlly(64);
            var foes = AliveEnemies(); Unit enemy = foes.First();
            foreach (Unit unit in AliveAllies().Concat(foes))
            {
                foreach (var code in unit.ActivePassiveCodes) code.StopCode();
                unit.ActiveUltimateCode?.StopCode(); unit.ClearUltimateResource();
                unit.RemoveShield(unit.ShieldCurr);
            }
            foreach (Unit foe in foes) EhStat(foe, BaseEnums.StatType.SpecialDefense, 100);
            var saved = new UnitSaveData { unitId = hero.ID, level = hero.Level, exp = hero.Exp, currentHP = hero.HpCurr };
            hero.WriteRunState(saved);
            var totals = new List<(string name, int direct, int reaction, float attachment)>();
            foreach (bool special in new[] { false, true })
            {
                foreach (var code in hero.ActivePassiveCodes) code.StopCode();
                hero.RestoreRunState(saved);
                int[] rewards = special ? new[] { 605, 618, 617 } : new[] { 601, 607, 608 };
                hero.ApplyRewardCodeInventory(rewards.Concat(Enumerable.Repeat(0, 4)).ToList(), new List<int>());
                for (int i = 0; i < 32; i++) hero.ApplyNectar(new ItemData
                {
                    nectarFlatStats = new Dictionary<string, float> { [special ? "SpecialAttack" : "Attack"] = 8f }
                });
                hero.CastPassiveCode();
                string name = special ? "특공" : "공격치명";
                float chance = DamageMitigation.EffectHitChance(hero, enemy, .25f, EffectHitScaling.Proportional);
                YvCheck("동일예산 스탯 " + name, "Lv20·넥타르32·T2코드3칸", $"공{hero.AtkCurr}/특공{hero.SpAtkCurr}/치확{hero.StatValue(BaseEnums.StatType.CritRate):P1}/치피{hero.StatValue(BaseEnums.StatType.CritDamage):P1}/파장{chance:P1}",
                    hero.Level == 20 && hero.RewardCodeIds.Count == 3);
                using (CombatRandom.BeginSeed(20261104))
                using (var log = new YvRecorder())
                {
                    for (int cycle = 0; cycle < 2; cycle++)
                    {
                        foreach (Unit foe in foes) { YvResetEnemy(foe); foe.GrantCombatElement(BaseEnums.UnitElement.Dendro, source: provider, suppressReaction: true, effectHitResolved: true); }
                        yield return NordAction(hero, Kind.Ultimate, _ => hero.FillUltimateResource());
                        for (int normal = 0; normal < 3; normal++)
                        {
                            hero.ClearUltimateResource();
                            foreach (Unit foe in foes) { YvResetEnemy(foe); foe.GrantCombatElement(BaseEnums.UnitElement.Dendro, source: provider, suppressReaction: true, effectHitResolved: true); }
                            yield return NordAction(hero, Kind.Normal, s => s.AdvanceAction(hero, 1));
                        }
                    }
                    int direct = log.Dealt.Where(d => d.Attacker == hero && d.DamageContext.CodeType != BaseEnums.CodeType.Effect).Sum(d => d.DamageDealt);
                    int reaction = log.Dealt.Where(d => d.Attacker == hero && d.DamageContext.HasTag(DamageTag.ElementalReaction)).Sum(d => d.DamageDealt);
                    totals.Add((name, direct, reaction, chance));
                    YvCheck("동일예산 실제3N+U×2 " + name, "직접/반응 분리·일반6·초월2", $"직접{direct}/반응{reaction}/부착{log.Elements.Count(e => e.Source == hero)}/개화{log.Reactions.Count(r => r.Source == hero && r.Name == "개화")}",
                        log.Started.Count(a => a.Unit == hero && a.Kind == Kind.Normal) == 6 && log.Started.Count(a => a.Unit == hero && a.Kind == Kind.Ultimate) == 2);
                }
            }
            YvCheck("동일 예산의 빌드 분기", "공격치명 직접피해 우세·특공 반응/부착 우세", string.Join(" / ", totals),
                totals[0].direct > totals[1].direct && totals[1].reaction > totals[0].reaction && totals[1].attachment > totals[0].attachment * 2f);
            foreach (var code in hero.ActivePassiveCodes) code.StopCode();
            hero.RestoreRunState(saved);
            OsFinish(enabled);
        }
    }
}
#endif
