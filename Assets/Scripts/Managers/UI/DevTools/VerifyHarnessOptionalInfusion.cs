#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Normal;
using Codes.Passive;
using Combat;
using Core;
using Effects.Negative;
using Entities;
using UnityEngine;
using Kind = Managers.ActionScheduler.ActionKind;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioOptionalInfusion(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); _cfActionIdSequence = 0;
            bool enabled = true;
            yield return YvPrepareScene(config, v => enabled = v);
            yield return EtPrepareEnemies(config);
            DebugMode.AllyInvincible = DebugMode.EnemyInvincible = false;
            Unit bastet = CfAlly(122), uchi = CfAlly(180), orion = CfAlly(22), pyg = CfAlly(20), jean = CfAlly(7);
            List<Unit> foes = AliveEnemies(); Unit target = foes.First();
            void Check(string label, bool passed, string actual = "") => YvCheck("선택 부착4인 " + label, "허용된 공격·공통 비례 명중", actual, passed);
            foreach (Unit unit in AliveAllies().Concat(foes))
            {
                foreach (var code in unit.ActivePassiveCodes.ToList()) code.StopCode();
                unit.ActiveUltimateCode?.StopCode(); unit.ClearUltimateResource(); unit.RemoveShield(unit.ShieldCurr); CfNoCrit(unit);
            }
            foreach (Unit unit in new[] { bastet, uchi, orion, pyg }) EhStat(unit, BaseEnums.StatType.SpecialAttack, 1000);
            foreach (Unit foe in foes) EhStat(foe, BaseEnums.StatType.SpecialDefense, 100);
            void Reset()
            {
                foreach (Unit foe in foes.Where(f => f.isActive)) YvResetEnemy(foe);
                foreach (Unit ally in AliveAllies()) ally.ClearUltimateResource();
            }
            void ClearClones() { foreach (Unit clone in Summons.Of(uchi).ToList()) clone.Die(null); }
            Check("명시적4인 적", foes.Count == 4, foes.Count.ToString());
            foreach (var entry in new[] { (bastet, .15f), (uchi, .15f), (orion, .20f), (pyg, .20f) })
            {
                foreach (int factor in new[] { 1, 3, 10 })
                {
                    EhStat(entry.Item1, BaseEnums.StatType.SpecialAttack, factor * 100);
                    float chance = DamageMitigation.EffectHitChance(entry.Item1, target, entry.Item2, EffectHitScaling.Proportional);
                    Check(entry.Item1.UnitName + " 명중비율" + factor, Mathf.Abs(chance - Mathf.Min(1, entry.Item2 * factor)) < .0001f, chance.ToString("P1"));
                }
            }
            foreach (float basis in new[] { .15f, .20f })
            {
                EhStat(bastet, BaseEnums.StatType.SpecialAttack, 100);
                int hits = 0;
                using (CombatRandom.BeginSeed(20261104))
                    for (int i = 0; i < 4000; i++) if (DamageMitigation.RollEffectHit(bastet, target, basis, EffectHitScaling.Proportional)) hits++;
                Check("4000회 분포 " + basis, Mathf.Abs(hits / 4000f - basis) < .025f, (hits / 4000f).ToString("P2"));
            }
            EhStat(bastet, BaseEnums.StatType.SpecialAttack, 1000);

            Reset();
            using (var log = new CfLog(AliveAllies()))
            {
                yield return NordAction(bastet, Kind.Normal, s => s.AdvanceAction(bastet, 1));
                Check("바스테트 일반 제외", !log.Elements.Any(e => e.Source == bastet) && log.Dealt.Where(d => d.Attacker == bastet).All(d => !d.DamageContext.AppliesElement) &&
                    log.NormalResolved.GetValueOrDefault(bastet) == 1 && bastet.ManaCurr == bastet.ManaMax,
                    $"부착{log.Elements.Count(e => e.Source == bastet)}/일반완료{log.NormalResolved.GetValueOrDefault(bastet)}/상시자원{bastet.ManaCurr}");
            }
            Reset(); jean.ModifyHp(jean.HpMax / 2); int beforeHeal = jean.HpCurr;
            using (var log = new CfLog(AliveAllies()))
            {
                yield return NordAction(bastet, Kind.PriorityAdditional, s => s.EnqueuePriorityAdditional(bastet, "infusion_bastet", "냥냥펀치", () =>
                    ((BastetCatPunch)bastet.ActiveNormalCode).CastAdditional(target, jean)));
                var hits = log.Dealt.Where(d => d.Attacker == bastet && d.DamageContext.CodeType != BaseEnums.CodeType.Effect).ToList();
                Check("바스테트 후속 단일 바위", hits.Count == 1 && hits[0].DamageContext.AppliesElement && log.Elements.Count(e => e.Source == bastet && e.Element == BaseEnums.UnitElement.Geo) == 1);
                Check("바스테트 후속 치유·일반완료 보존", jean.HpCurr - beforeHeal == Mathf.RoundToInt(hits.Single().DamageDealt * .5f) &&
                    log.NormalResolved.GetValueOrDefault(bastet) == 0 && bastet.ManaCurr == bastet.ManaMax,
                    $"치유{jean.HpCurr - beforeHeal}/본타{hits.Single().DamageDealt}/일반완료{log.NormalResolved.GetValueOrDefault(bastet)}/상시자원{bastet.ManaCurr}");
            }
            Reset();
            using (var log = new YvRecorder())
            {
                yield return bastet.ActiveNormalCode.PerformCommandedAttack(target, jean);
                Check("바스테트 지휘 일반 제외", log.Dealt.Count(d => d.Attacker == bastet) == 1 && !log.Elements.Any(e => e.Source == bastet));
            }

            Reset();
            using (var log = new CfLog(AliveAllies()))
            {
                yield return NordAction(pyg, Kind.Normal, s => s.AdvanceAction(pyg, 1));
                Check("피그말리온 불씨 단일 부착", log.Dealt.Count(d => d.Attacker == pyg) == 1 && log.Dealt.Single(d => d.Attacker == pyg).DamageContext.AppliesElement &&
                    log.Elements.Count(e => e.Source == pyg && e.Element == BaseEnums.UnitElement.Pyro) == 1 && log.NormalResolved.GetValueOrDefault(pyg) == 1 && pyg.ManaCurr == 20);
            }
            Reset(); target.GrantCombatElement(BaseEnums.UnitElement.Hydro, 2);
            EhStat(pyg, BaseEnums.StatType.SpecialAttack, 1);
            foreach (Unit foe in foes) EhStat(foe, BaseEnums.StatType.SpecialDefense, 100000000);
            using (CombatRandom.BeginSeed(20261104))
            using (var log = new YvRecorder())
            {
                // 우선도를 고정해 준비한 물 대상에게 실제 일반행동이 적중하게 한다.
                int priority = target.Priority; target.Priority = priority + 9999;
                try { yield return NordAction(pyg, Kind.Normal, s => s.AdvanceAction(pyg, 1)); }
                finally { target.Priority = priority; }
                Check("부착 실패는 직접 피해·기존 물 보존", log.Dealt.Any(d => d.Attacker == pyg && d.DamageDealt > 0) &&
                    !log.Elements.Any(e => e.Source == pyg) && log.Reactions.All(r => r.Source != pyg) && target.GetAttachedElementRemainingTurns(BaseEnums.UnitElement.Hydro) == 2);
            }
            EhStat(pyg, BaseEnums.StatType.SpecialAttack, 1000);
            foreach (Unit foe in foes) EhStat(foe, BaseEnums.StatType.SpecialDefense, 100);
            foreach (var material in new[] { BaseEnums.UnitElement.Hydro, BaseEnums.UnitElement.Dendro })
            {
                Reset(); foreach (Unit foe in foes) foe.GrantCombatElement(material);
                using (var log = new YvRecorder())
                {
                    yield return NordAction(pyg, Kind.Normal, s => s.AdvanceAction(pyg, 1));
                    Check("불씨 실제 반응 " + material, log.Reactions.Count(r => r.Source == pyg && r.Name == (material == BaseEnums.UnitElement.Hydro ? "증발" : "연소")) == 1 &&
                        log.Dealt.Count(d => d.Attacker == pyg && d.DamageContext.CodeType == BaseEnums.CodeType.Effect) == (material == BaseEnums.UnitElement.Hydro ? 0 : 1));
                }
            }
            Reset();
            using (var log = new YvRecorder())
            {
                yield return NordAction(pyg, Kind.Ultimate, _ => pyg.FillUltimateResource());
                Check("피그말리온 초월기 부착 제외", !log.Elements.Any(e => e.Source == pyg));
            }

            Reset();
            using (var log = new YvRecorder())
            {
                yield return NordAction(orion, Kind.Normal, s => s.AdvanceAction(orion, 1));
                yield return NordAction(orion, Kind.Ultimate, _ => orion.FillUltimateResource());
                Check("오리온 일반·초월기 제외", !log.Elements.Any(e => e.Source == orion) && log.Dealt.Where(d => d.Attacker == orion).All(d => !d.DamageContext.AppliesElement));
            }
            Reset(); var sirius = orion.ActivePassiveCodes.OfType<OrionSirius>().Single(); sirius.CastCode();
            foreach (Unit foe in foes) foe.GrantCombatElement(BaseEnums.UnitElement.Dendro);
            using (var log = new YvRecorder())
            {
                yield return NordAction(orion, Kind.Additional, _ => sirius.Gain(5));
                var hits = log.Dealt.Where(d => d.Attacker == orion && d.DamageContext.CodeType == BaseEnums.CodeType.Passive).ToList();
                Check("화살비 대상마다1회·장식타수 제외", hits.Count == 4 && hits.All(h => h.DamageContext.AppliesElement) &&
                    log.Elements.Count(e => e.Source == orion && e.Element == BaseEnums.UnitElement.Geo) == 4 && !sirius.Pending && sirius.Charges == 0);
                Check("화살비 실제 광역 풍화", log.Reactions.Count(r => r.Source == orion && r.Name == "풍화") == 4 &&
                    foes.All(foe => foe.HasStatus(ElementalReaction.WeatheringStatusId)));
            }
            Reset();
            using (var log = new YvRecorder())
            {
                void StopRain(DamageResolvedContext damage) { if (damage.Attacker == orion && damage.DamageContext.CodeType == BaseEnums.CodeType.Passive) sirius.StopCode(); }
                Unit.AnyDamageDealt += StopRain;
                try { yield return NordAction(orion, Kind.Additional, _ => sirius.Gain(5)); yield return new WaitForSeconds(1.5f); }
                finally { Unit.AnyDamageDealt -= StopRain; }
                Check("화살비 정지 후 늦은 부착 차단", !log.Elements.Any(e => e.Source == orion) && log.Dealt.Count(d => d.Attacker == orion) <= 1 && !sirius.Pending);
            }

            Reset(); uchi.ActivePassiveCodes.OfType<UchiTaoist>().Single().CastCode(); ClearClones();
            Unit clone = Summons.Create(uchi); EhStat(clone, BaseEnums.StatType.SpecialAttack, 1);
            using (var log = new YvRecorder())
            {
                yield return NordAction(clone, Kind.Normal, s => s.AdvanceAction(clone, 1));
                Check("분신 자연 일반·본체 특공/귀속", log.Dealt.Count(d => d.Attacker == uchi) == 1 &&
                    log.Dealt.Single(d => d.Attacker == uchi).DamageContext.AppliesElement && log.Elements.Count(e => e.Source == uchi && e.Element == BaseEnums.UnitElement.Anemo) == 1 &&
                    log.Elements.All(e => e.Source != clone) && !clone.isActive && Summons.Of(uchi).Count == 0);
            }
            Reset(); foreach (Unit foe in foes) foe.GrantCombatElement(BaseEnums.UnitElement.Anemo);
            clone = Summons.Create(uchi); EhStat(clone, BaseEnums.StatType.SpecialAttack, 1);
            using (var log = new YvRecorder())
            {
                yield return NordAction(clone, Kind.Normal, s => s.AdvanceAction(clone, 1));
                var reactions = log.Dealt.Where(d => d.DamageContext.CodeType == BaseEnums.CodeType.Effect).ToList();
                Check("분신 초확산은 본체 특공1000·귀속", log.Reactions.Count(r => r.Source == uchi && r.Name == "초확산") == 1 &&
                    reactions.Count == 4 && reactions.All(d => d.Attacker == uchi && d.DamageContext.Damage == 600), string.Join(",", reactions.Select(d => d.DamageContext.Damage)));
            }
            Reset(); ClearClones(); for (int i = 0; i < 3; i++) Summons.Create(uchi);
            using (var log = new YvRecorder())
            {
                Summons.Create(uchi);
                Check("넘침105% 바람 제외", log.Dealt.Count(d => d.Attacker == uchi) == 1 && !log.Elements.Any(e => e.Source == uchi) &&
                    log.Dealt.All(d => !d.DamageContext.AppliesElement) && Summons.Of(uchi).Count == 3);
            }
            Reset(); ClearClones(); Summons.Create(uchi); Summons.Create(uchi);
            using (var log = new YvRecorder())
            {
                yield return NordAction(uchi, Kind.Ultimate, _ => uchi.FillUltimateResource());
                Check("초월기 소모 바람 제외", log.Dealt.Count(d => d.Attacker == uchi) == 2 && !log.Elements.Any(e => e.Source == uchi) &&
                    log.Dealt.All(d => !d.DamageContext.AppliesElement) && Summons.Of(uchi).Count == 0);
            }
            Reset(); clone = Summons.Create(uchi);
            using (var log = new YvRecorder())
            {
                yield return clone.ActiveNormalCode.PerformCommandedAttack(target, jean);
                Check("분신 지휘 사격 바람 제외", log.Dealt.Count(d => d.Attacker == uchi) == 1 && !log.Elements.Any(e => e.Source == uchi) && !clone.isActive);
            }
            Reset(); clone = Summons.Create(uchi);
            using (var log = new YvRecorder())
            {
                yield return NordAction(clone, Kind.Additional, s => s.EnqueueAdditional(clone, "optional_clone_additional", "분신 추가", clone.CastNormalCode));
                Check("분신 추가행동 바람 제외", log.Dealt.Count(d => d.Attacker == uchi) == 1 && !log.Elements.Any(e => e.Source == uchi) && !clone.isActive);
            }
            GridManager.Instance.OnRoundEnd();
            Check("전투 종료 소환/예약 정리", Summons.Of(uchi).Count == 0 && !sirius.Pending);
            OsFinish(enabled);
        }
    }
}
#endif
