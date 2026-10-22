#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
using Helpers;
using UnityEngine;
using Kind = Managers.ActionScheduler.ActionKind;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioDotRosterCore(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); _cfActionIdSequence = 0;
            bool enabled = true;
            yield return YvPrepareScene(config, value => enabled = value);
            var game = GameManager.Instance;
            DebugMode.AllyInvincible = true; DebugMode.EnemyInvincible = false;
            Unit neph = CfAlly(164), atal = CfAlly(165), neith = CfAlly(166), eresh = CfAlly(167), astolfo = CfAlly(162);
            Unit enemy = AliveEnemies().First();
            // 강화 해제 검사가 적 생존용 fixture까지 걷어 내지 않도록 중립 예산으로 둔다.
            foreach (Unit target in AliveEnemies())
            {
                target.RemoveStatusByKey(YvHpBoostKey);
                YvAddTestStatus(target, YvHpBoostId, YvHpBoostKey,
                    new StatModifierEffect(BaseEnums.StatType.MaxHp, 200000f), BaseEnums.StatusCategory.Neutral);
                target.RestoreHpOutsideCombat(target.HpMax);
            }
            void Check(string label, bool passed, string actual = "") => YvCheck("신규 지딜 " + label, "기획 사양", actual, passed);
            Check("4인 데이터·원소·실제 팩토리", neph?.Element == "Dendro" && atal?.Element == "Dendro" && neith?.Element == "Electro" && eresh?.Element == "Pyro" &&
                neph.ActiveNormalCode is NephthysPoisonSeed && atal.ActiveUltimateCode is AtalanteHuntingSeason && neith.ActiveUltimateCode is NeithThunderWeave && eresh.ActivePassiveCodes.OfType<EreshkigalUnderworldFeast>().Any());
            var sfxData = new YamlDotNet.Serialization.DeserializerBuilder().IgnoreUnmatchedProperties().Build()
                .Deserialize<SfxCueDataList>(Resources.Load<TextAsset>("Data/95_sfx_cues").text);
            bool assetsConnected = SpriteResource.LoadPortrait("Sprite/UI/UnitTags/TAG_SUPPORT") != null &&
                SpriteResource.LoadPortrait("Sprite/UI/UnitTags/TAG_STURDY") != null;
            foreach (var pair in new[] { (neph, "nephthys"), (atal, "atalante"), (neith, "neith"), (eresh, "ereshkigal") })
            foreach (SfxActionSlot slot in new[] { SfxActionSlot.Normal, SfxActionSlot.Ultimate, SfxActionSlot.Passive })
            {
                string suffix = slot.ToString().ToLowerInvariant();
                var skill = Effects.Vfx.VfxDefinitionCatalog.Skill(pair.Item2 + "." + suffix);
                string cue = SfxCueRoutes.Resolve(pair.Item1, slot, SfxCuePhase.Hit);
                var sound = sfxData.cues.FirstOrDefault(c => c.name == cue);
                assetsConnected &= skill != null && skill.slots.All(s => Effects.Vfx.VfxDefinitionCatalog.Part(s.partId) != null) &&
                    sound != null && sound.clips.Count > 0 && sound.clips.All(c => Resources.Load<AudioClip>("Audio/Sfx/" + c) != null);
            }
            Check("공용 초상·12 VFX·12 SFX 실제 리소스 연결", assetsConnected);
            var garden = neph.ActivePassiveCodes.OfType<NephthysPoisonGarden>().Single();
            var hunt = atal.ActivePassiveCodes.OfType<AtalantePoisonHunt>().Single();
            var weave = neith.ActivePassiveCodes.OfType<NeithCatalyticWeave>().Single();
            var feast = eresh.ActivePassiveCodes.OfType<EreshkigalUnderworldFeast>().Single();
            foreach (Unit unit in AliveAllies().Concat(AliveEnemies())) { CfNoCrit(unit); unit.ClearUltimateResource(); }
            foreach (Unit unit in new[] { neph, atal, neith, eresh, astolfo }) CfEnsureEffectHit(unit, AliveEnemies());
            hunt.StopCode(); weave.StopCode(); feast.StopCode();
            using (var log = new YvRecorder())
            {
                yield return NordAction(neph, Kind.Normal, s => s.AdvanceAction(neph, 1));
                Check("네프 일반 공80%·풀 부착 없음", log.Dealt.Any(h => h.Attacker == neph && !h.DamageContext.IsDamageOverTime && h.DamageContext.Damage == Mathf.RoundToInt(neph.AtkCurr * .80f)) && !enemy.HasAttachedElement(BaseEnums.UnitElement.Dendro));
            }
            Check("독안1 부여", garden.Eyes(enemy) == 1);
            for (int i = 0; i < 8; i++) garden.ApplyPoisonAndEye(enemy);
            Check("독안 상한3 없음", garden.Eyes(enemy) == 9);
            enemy.RemoveAllNegativeStatuses(astolfo);
            Check("맹독 정화·독안 보존·디버프 아님", !enemy.HasStatusKey(ActionDot.Key(ActionDotKind.Poison)) && garden.Eyes(enemy) == 9 &&
                !enemy.GetAllStatuses().Single(s => s.Key == garden.MarkKey).CountsAsDebuff);
            YvAddTestStatus(enemy, 99893, "dot_test_beneficial", new StatModifierEffect(BaseEnums.StatType.Attack, .2f, true), BaseEnums.StatusCategory.Positive);
            YvDispelBeneficial(enemy);
            Check("강화 해제로도 독안 유지", !enemy.HasStatusKey("dot_test_beneficial") && garden.Eyes(enemy) == 9);
            yield return NordAction(enemy, Kind.Normal, s => s.AdvanceAction(enemy, 1));
            Check("맹독 없으면 자연 파열 없음", garden.Eyes(enemy) == 9);
            garden.ApplyPoisonAndEye(enemy);
            long stacks = garden.Eyes(enemy);
            int expectedRupture = (int)System.Math.Round(neph.StatValue(BaseEnums.StatType.SpecialAttack) * NephthysPoisonGarden.RuptureRatio * stacks);
            using (var log = new YvRecorder())
            {
                yield return NordAction(enemy, Kind.Normal, s => s.AdvanceAction(enemy, 1));
                var hits = log.Dealt.Where(h => h.Attacker == neph && h.DamageContext.DotTrigger == DotTriggerKind.Derived).ToList();
                Check("자연 맹독에 독안 전량 소비·선형 특공 파열", garden.Eyes(enemy) == 0 && hits.Count == 1 && hits[0].DamageContext.Damage == expectedRupture && !hits[0].DamageContext.IsCrit);
                Check("파열과 맹독 원본 틱 동일", hits.Count == 1 && log.Dealt.Any(h => h.DamageContext.DotTrigger == DotTriggerKind.Natural && h.DamageContext.DotRootId == hits[0].DamageContext.DotRootId));
            }
            garden.ApplyPoisonAndEye(enemy); garden.ApplyPoisonAndEye(enemy);
            using (var log = new YvRecorder())
            {
                DamageOverTimeDetonation.Detonate(enemy, 1, neph);
                Check("능동 정산은 독안·파열 불변", garden.Eyes(enemy) == 2 && !log.Dealt.Any(h => h.DamageContext.DotTrigger == DotTriggerKind.Derived));
            }
            using (var log = new YvRecorder())
            {
                yield return NordAction(neph, Kind.Ultimate, _ => neph.FillUltimateResource());
                Check("네프 초월 공80%·독안+1", garden.Eyes(enemy) == 3 && log.Dealt.Any(h => h.Attacker == neph && h.DamageContext.CodeType == BaseEnums.CodeType.Ultimate && h.DamageContext.Damage == Mathf.RoundToInt(neph.AtkCurr * .8f)));
            }
            garden.StopCode();
            Check("네프 정지 후 무한 표식 정리", !enemy.HasStatusKey(garden.MarkKey));
            hunt.CastCode();
            var poison = enemy.GetAllStatuses().SelectMany(s => s.Effects).Select(e => e.EffectObject).OfType<ActionDotEffect>().First(e => e.Kind == ActionDotKind.Poison);
            var poisonProbe = poison.CreateDamageOverTimeTick();
            var bleedProbe = new DamageContext(neph, 100, BaseEnums.CodeType.Effect, new List<int>()) { IsDamageOverTime = true, DotKind = ActionDotKind.Bleed };
            var shockProbe = new DamageContext(neph, 100, BaseEnums.CodeType.Effect, new List<int>()) { IsDamageOverTime = true, DotKind = ActionDotKind.Shock };
            Check("아탈 맹독·출혈만 증폭", AtalantePoisonHunt.DotMultiplier(poisonProbe) > 1 && AtalantePoisonHunt.DotMultiplier(bleedProbe) > 1 && Near(AtalantePoisonHunt.DotMultiplier(shockProbe), 1));
            // 치유 초월기는 아군 HP90% 이하가 있어야 실제 예약된다. 버프 검사도 유효한 치유 대상부터 준비한다.
            atal.ModifyHp(atal.HpMax / 2);
            yield return NordAction(astolfo, Kind.Ultimate, _ => astolfo.FillUltimateResource());
            YvAddTestStatus(atal, 99891, "dot_roster_critical", new StatModifierEffect(BaseEnums.StatType.CritRate, 2f));
            atal.ClearUltimateResource();
            using (var log = new YvRecorder())
            {
                int shots = 0;
                void Count(DamageResolvedContext hit) { if (hit.Attacker == atal && hit.DamageContext.HasTag(DamageTag.AdditionalAttack)) shots++; }
                Unit.AnyDamageDealt += Count;
                try { yield return CfRun(_ => { for (int i = 0; i < 5; i++) ActionDot.ResolveNaturalTick(enemy, poison); }, () => shots >= 4); }
                finally { Unit.AnyDamageDealt -= Count; }
                var arrows = log.Dealt.Where(h => h.Attacker == atal && h.DamageContext.HasTag(DamageTag.AdditionalAttack)).ToList();
                float multiplier = 1 + atal.StatValue(BaseEnums.StatType.CritDamage) + astolfo.StatValue(BaseEnums.StatType.CritDamage) * AstolfoReturnGift.AdditionalCritShare;
                int raw = Mathf.RoundToInt(Mathf.RoundToInt(atal.AtkCurr * AtalanteHuntingArrow.PursuitRatio) * multiplier);
                Check("아탈 실제 추가행동4회 상한·공격력 치명·아스톨포 기본 보너스", arrows.Count == 4 && arrows.All(h => h.DamageContext.IsCrit && h.DamageContext.Damage == raw) && hunt.Remaining == 0, $"화살{arrows.Count}/4 · 남은{hunt.Remaining} · 기대 원시{raw} · 실제{string.Join(",", arrows.Select(h => h.DamageContext.Damage))}");
                Check("추격은 일반 마나 충전 없음", atal.ManaCurr == 0);
            }
            atal.RemoveStatusByKey("dot_roster_critical");
            atal.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved, new EventContext(atal));
            Check("일반 완료 추격 보충", hunt.Remaining == 4);
            ActionDot.Apply(eresh, enemy, ActionDotKind.Bleed);
            var bleed = enemy.GetAllStatuses().SelectMany(s => s.Effects).Select(e => e.EffectObject).OfType<ActionDotEffect>().First(e => e.Kind == ActionDotKind.Bleed);
            using (var log = new YvRecorder())
            {
                DamageOverTimeDetonation.Detonate(enemy, 3, neith);
                Check("3배 정산은 추격 예산·추가행동 불변", hunt.Remaining == 4 && !log.Dealt.Any(h => h.Attacker == atal && h.DamageContext.HasTag(DamageTag.AdditionalAttack)));
                yield return CfRun(_ => ActionDot.ResolveNaturalTick(enemy, bleed),
                    () => log.Dealt.Any(h => h.Attacker == atal && h.DamageContext.HasTag(DamageTag.AdditionalAttack)));
                Check("출혈 자연 발동도 실제 추가행동 추격", hunt.Remaining == 3 && log.Dealt.Count(h => h.Attacker == atal && h.DamageContext.HasTag(DamageTag.AdditionalAttack)) == 1);
                yield return CfRun(_ => { for (int i = 0; i < 5; i++) ActionDot.ResolveNaturalTick(enemy, poison); }, () => hunt.Remaining == 0);
                Check("맹독·출혈 추격4회 예산 공유", log.Dealt.Count(h => h.Attacker == atal && h.DamageContext.HasTag(DamageTag.AdditionalAttack)) == 4 && hunt.Remaining == 0);
            }
            hunt.StopCode(); Check("아탈 정지 오라 정리", Near(AtalantePoisonHunt.DotMultiplier(poisonProbe), 1));
            using (var log = new YvRecorder())
            {
                yield return NordAction(atal, Kind.Normal, s => s.AdvanceAction(atal, 1));
                Check("아탈 일반 공110%", log.Dealt.Any(h => h.Attacker == atal && !h.DamageContext.IsCrit && h.DamageContext.Damage == Mathf.RoundToInt(atal.AtkCurr * 1.10f)));
            }
            using (var log = new YvRecorder())
            {
                yield return NordAction(atal, Kind.Ultimate, _ => atal.FillUltimateResource());
                Check("아탈 초월 공150%", log.Dealt.Any(h => h.Attacker == atal && h.DamageContext.CodeType == BaseEnums.CodeType.Ultimate && h.DamageContext.Damage == Mathf.RoundToInt(atal.AtkCurr * 1.50f)));
            }
            enemy.RemoveAllNegativeStatuses(neith);
            float defense = enemy.SpDefCurr;
            weave.CastCode(); ActionDot.Apply(neith, enemy, ActionDotKind.Shock);
            Check("감전 촉진·특방 실제 감소", enemy.HasStatusKey(weave.MarkKey) && enemy.SpDefCurr < defense && enemy.SpDefCurr >= defense * .67f);
            float originalCutDefense = enemy.SpDefCurr;
            YvAddTestStatus(neith, 99892, "dot_catalysis_sp", new StatModifierEffect(BaseEnums.StatType.SpecialAttack, 1000));
            bool strengthened = enemy.SpDefCurr < originalCutDefense;
            neith.RemoveStatusByKey("dot_catalysis_sp");
            Check("촉진은 네이트 특공 변경 즉시 반영", strengthened && Near(enemy.SpDefCurr, originalCutDefense));
            enemy.RemoveStatusByKey(weave.MarkKey);
            Check("감전 유지 중 촉진만 해제하면 재설치", enemy.HasStatusKey(weave.MarkKey) && YamaDeathVerdict.IsShocked(enemy));
            weave.StopCode();
            Check("네이트 정지 시 촉진만 제거", !enemy.HasStatusKey(weave.MarkKey) && YamaDeathVerdict.IsShocked(enemy) && Near(enemy.SpDefCurr, defense));
            weave.CastCode();
            enemy.RemoveAllNegativeStatuses(neith);
            Check("감전 정화 시 촉진 제거·특방 복원", !enemy.HasStatusKey(weave.MarkKey) && Near(enemy.SpDefCurr, defense));
            float originalSp = neith.StatValue(BaseEnums.StatType.SpecialAttack), otherSp = neph.StatValue(BaseEnums.StatType.SpecialAttack);
            yield return NordAction(neith, Kind.Ultimate, _ => neith.FillUltimateResource());
            Check("네이트 번개만 특공 스냅샷 지원", Near(neith.StatValue(BaseEnums.StatType.SpecialAttack), originalSp * 1.45f) && Near(neph.StatValue(BaseEnums.StatType.SpecialAttack), otherSp));
            yield return NordAction(neith, Kind.Ultimate, _ => neith.FillUltimateResource());
            Check("네이트 지원 재시전 자기 증폭 없음", Near(neith.StatValue(BaseEnums.StatType.SpecialAttack), originalSp * 1.45f));
            neith.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved, new EventContext(neith));
            Check("네이트 지원 첫 일반 완료 유지", neith.StatValue(BaseEnums.StatType.SpecialAttack) > originalSp);
            neith.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved, new EventContext(neith));
            Check("네이트 지원 두 일반 완료 만료", Near(neith.StatValue(BaseEnums.StatType.SpecialAttack), originalSp));
            weave.StopCode();
            feast.CastCode();
            foreach (Unit ally in AliveAllies()) { ally.ModifyHp(ally.HpMax / 4); ally.ClearUltimateResource(); }
            int before = neph.HpCurr, heal = Mathf.Max(1, Mathf.RoundToInt(eresh.HpMax * EreshkigalUnderworldFeast.HealRatio));
            yield return NordAction(neph, Kind.Special, s => s.EnqueueSpecial(neph, "dot_roster_heals", "지속피해 치유 검증", () =>
            {
                for (int i = 0; i < 5; i++)
                {
                    long root = ActionDot.NextRootId();
                    for (int repeat = 0; repeat < 2; repeat++)
                        enemy.TakeDamage(new DamageContext(neph, 10, BaseEnums.CodeType.Effect, new List<int>())
                        { IsDamageOverTime = true, DotKind = ActionDotKind.Poison, DotRootId = root, DotTrigger = repeat == 0 ? DotTriggerKind.Natural : DotTriggerKind.Derived });
                }
            }));
            Check("치유 행동당3회·파생 중복 없음·최대체력 계수", neph.HpCurr - before == heal * 3 && feast.HealsThisAction == 3);
            before = neph.HpCurr;
            void DotProbe() => enemy.TakeDamage(new DamageContext(neph, 10, BaseEnums.CodeType.Effect, new List<int>())
                { IsDamageOverTime = true, DotKind = ActionDotKind.Poison, DotRootId = ActionDot.NextRootId() });
            IEnumerator ChildHeal() { DotProbe(); yield break; }
            IEnumerator ParentHeal()
            {
                try
                {
                    for (int i = 0; i < 3; i++) DotProbe();
                    yield return game.ActionScheduler.RunPreAttackInterrupt(atal, "회복 예산 선제 개입", ChildHeal);
                    for (int i = 0; i < 3; i++) DotProbe();
                }
                finally { neph.isCasting = false; }
            }
            yield return NordAction(neph, Kind.Special, s => s.EnqueueSpecial(neph, "dot_nested_budget", "중첩 행동 회복 상한", () =>
                { neph.isCasting = true; neph.StartCoroutine(ParentHeal()); }));
            Check("선제 개입 뒤 부모 행동 회복 상한 보존", neph.HpCurr - before == heal * 4);
            before = neph.HpCurr; int untouched = atal.HpCurr;
            DebugMode.AllyInvincible = false;
            neph.TakeDamage(new DamageContext(enemy, 100, BaseEnums.CodeType.Effect, new List<int>()) { IsDamageOverTime = true, DotRootId = ActionDot.NextRootId() });
            Check("적이 아군에게 준 실제 지속피해는 회복 연료 아님", neph.HpCurr < before && atal.HpCurr == untouched);
            DebugMode.AllyInvincible = true;
            foreach (Unit ally in AliveAllies()) ally.ModifyHp(ally.HpMax / 4);
            int partyBefore = neph.HpCurr, selfBefore = eresh.HpCurr;
            yield return NordAction(eresh, Kind.Ultimate, _ => eresh.FillUltimateResource());
            Check("에레쉬 전체18%·자신 추가30% 회복", neph.HpCurr - partyBefore == Mathf.RoundToInt(eresh.HpMax * .18f) &&
                eresh.HpCurr - selfBefore == Mathf.RoundToInt(eresh.HpMax * .18f) + Mathf.RoundToInt(eresh.HpMax * .30f));
            Check("에레쉬 출혈·불 부착 없음", AliveEnemies().All(u => u.HasStatusKey(ActionDot.Key(ActionDotKind.Bleed)) && !u.HasAttachedElement(BaseEnums.UnitElement.Pyro)));
            feast.StopCode();
            Check("에레쉬 정지 회복 예산 정리", feast.HealsThisAction == 0);
            DebugMode.AllyInvincible = false;
            game.gameState = BaseEnums.GameState.RoundInProgress;
            OsFinish(enabled);
        }
    }
}
#endif
