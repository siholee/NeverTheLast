#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using BaseClasses;
using Combat;
using Codes.Passive;
using Core;
using Effects.Base;
using Effects.Buffs;
using Effects.Negative;
using Entities;
using Entities.Status;
using UnityEngine;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private sealed class EhResistance : BaseEffect
        {
            public EhResistance() : base(0) { }
            public override float NegativeStatusResistanceChanceModifier(Unit unit, UnitStatus status) => 1f;
        }

        private static void EhStat(Unit unit, BaseEnums.StatType stat, float value)
        {
            string key = "verify_eh_stat_" + stat;
            unit.RemoveStatusByKey(key);
            YvAddTestStatus(unit, 99801, key, new StatModifierEffect(stat, value - unit.StatValue(stat)));
        }

        private static void EhSeed(bool hit, float chance)
        {
            for (int seed = 1; seed < 10000; seed++)
            {
                Random.InitState(seed);
                if ((Random.value < chance) != hit) continue;
                Random.InitState(seed);
                return;
            }
            throw new System.InvalidOperationException("효과 명중 검증 난수 시드를 찾지 못했습니다.");
        }

        private static UnitStatus EhDebuff(Unit source, Unit target, string key, int duration = 2)
            => BuffStatus.Create(99802, key, "검증 취약", source, target,
                new ReceivingDamageMultiplierEffect(1.1f), duration: duration,
                category: BaseEnums.StatusCategory.Negative);

        private IEnumerator ScenarioEffectHitCore(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();
            bool wasEnabled = true;
            yield return YvPrepareScene(config, value => wasEnabled = value);
            Unit source = AliveAllies().First(unit => unit.ID == 64);
            Unit ally = AliveAllies().First(unit => unit.ID == 66);
            Unit target = AliveEnemies().First();
            Random.State priorRandom = Random.state;
            EhStat(source, BaseEnums.StatType.SpecialAttack, 100f);
            EhStat(target, BaseEnums.StatType.SpecialDefense, 100f);
            float equal = DamageMitigation.EffectHitChance(source, target);
            RuiCheck("같은 특공·특방 기본 명중", "75%", equal.ToString("P1"), Near(equal, .75f));
            EhStat(source, BaseEnums.StatType.SpecialAttack, 200f);
            RuiCheck("특공 두 배 투자", "100%", DamageMitigation.EffectHitChance(source, target).ToString("P1"),
                Near(DamageMitigation.EffectHitChance(source, target), 1f));
            EhStat(source, BaseEnums.StatType.SpecialAttack, 50f);
            RuiCheck("특방 두 배 투자", "50%", DamageMitigation.EffectHitChance(source, target).ToString("P1"),
                Near(DamageMitigation.EffectHitChance(source, target), .5f));
            EhStat(source, BaseEnums.StatType.SpecialAttack, 100f);
            Random.InitState(6106); int hits = 0;
            for (int i = 0; i < 4000; i++) if (DamageMitigation.RollEffectHit(source, target)) hits++;
            RuiCheck("명중 분포 4000회", "72~78%", (hits / 4000f).ToString("P1"), hits >= 2880 && hits <= 3120);

            YvResetEnemy(target);
            target.GrantCombatElement(BaseEnums.UnitElement.Hydro, 2);
            EhSeed(false, equal);
            bool attached = target.GrantCombatElement(BaseEnums.UnitElement.Electro, 3, source);
            RuiCheck("실패 부착은 반응·소모 없음", "물 2턴 유지·번개/감전 없음", target.GetCombatElementDisplay(),
                !attached && target.GetAttachedElementRemainingTurns(BaseEnums.UnitElement.Hydro) == 2 &&
                !target.HasAttachedElement(BaseEnums.UnitElement.Electro) && !target.HasStatus(ElementalReaction.ShockStatusId));
            EhSeed(false, equal);
            target.GrantCombatElement(BaseEnums.UnitElement.Hydro, 6, source, suppressReaction: true);
            RuiCheck("실패 재부착 지속 보존", "2턴", target.GetAttachedElementRemainingTurns(BaseEnums.UnitElement.Hydro).ToString(),
                target.GetAttachedElementRemainingTurns(BaseEnums.UnitElement.Hydro) == 2);
            EhSeed(true, equal);
            attached = target.GrantCombatElement(BaseEnums.UnitElement.Electro, source: source);
            RuiCheck("성공 부착 감전 파생", "감전 생성·재료 소모", target.GetCombatElementDisplay(),
                attached && target.HasStatus(ElementalReaction.ShockStatusId) && !target.HasAnyCombatElement);

            UnitStatus old = EhDebuff(source, target, "verify_eh_replace"); old.SkipEffectHitRoll = true;
            target.AddStatus(old);
            EhSeed(false, equal);
            bool refreshed = target.TryAddStatus(EhDebuff(source, target, old.Key, 7));
            RuiCheck("실패 디버프 갱신 보존", "기존 객체·2턴 유지", old.RemainingTurns.ToString(),
                !refreshed && target.GetAllStatuses().Contains(old) && old.RemainingTurns == 2);

            UnitStatus neutralDot = BuffStatus.Create(99803, "verify_eh_neutral_dot", "중립 지속피해", source, target,
                new DamageOverTimeEffect(1001, 20f), duration: 3, category: BaseEnums.StatusCategory.Neutral);
            EhSeed(false, equal); bool dotAdded = target.TryAddStatus(neutralDot);
            RuiCheck("중립 지속피해 명중 판정", "실패·디버프 인식", dotAdded.ToString(), !dotAdded && neutralDot.CountsAsDebuff);
            var factoryDot = new UnitStatus(1, source, target); factoryDot.AddEffect(1001, 20f);
            EhSeed(false, equal); dotAdded = target.TryAddStatus(factoryDot);
            RuiCheck("ID 효과 생성 전 판정 우회 방지", "객체 해석 후 실패", dotAdded.ToString(),
                !dotAdded && factoryDot.Effects[0].EffectObject != null && factoryDot.CountsAsDebuff);
            UnitStatus neutralMark = BuffStatus.Create(99804, "verify_eh_mark", "중립 표식", source, target,
                new MarkerBuffEffect(), duration: 2, category: BaseEnums.StatusCategory.Neutral);
            EhSeed(false, equal);
            RuiCheck("중립 적 표식 명중 판정", "실패", neutralMark.Key, !target.TryAddStatus(neutralMark));

            YvAddTestStatus(ally, 99805, "verify_eh_resist", new EhResistance());
            Random.InitState(101); float expectedNext = Random.value; Random.InitState(101);
            bool friendlyRoll = DamageMitigation.RollEffectHit(source, ally);
            RuiCheck("아군 명중 확정·난수 보존", "확정·난수 미소모", friendlyRoll.ToString(), friendlyRoll && Random.value == expectedNext);
            bool friendlyStatus = ally.TryAddStatus(EhDebuff(source, ally, "verify_eh_friendly"));
            bool friendlyElement = ally.GrantCombatElement(BaseEnums.UnitElement.Hydro, source: source, suppressReaction: true);
            RuiCheck("아군 부여 추가 저항 무시", "디버프·원소 적용", $"{friendlyStatus}/{friendlyElement}", friendlyStatus && friendlyElement);
            ally.RemoveStatusByKey("verify_eh_friendly"); ally.RemoveStatusByKey("verify_eh_resist"); ally.ResetCombatElements();
            YvAddTestStatus(target, 99805, "verify_eh_resist", new EhResistance());
            EhSeed(true, equal);
            RuiCheck("적 추가 상태 저항 유지", "실패", "100% 상태 저항", !target.TryAddStatus(EhDebuff(source, target, "verify_eh_resisted")));
            target.RemoveStatusByKey("verify_eh_resist");

            YvResetEnemy(target);
            EhSeed(true, equal);
            bool controlled = ControlStatuses.ApplyFreeze(target, source, 1);
            int count = target.ControlAppliedCount;
            UnitStatus freeze = target.GetStatus(ControlStatuses.FrozenStatusId);
            EhSeed(false, equal);
            bool renewed = ControlStatuses.ApplyFreeze(target, source, 3);
            RuiCheck("실패 제어는 기존 상태·분쇄 보존", "기존 빙결 유지·횟수 동일", $"{controlled}/{renewed}/{target.ControlAppliedCount}",
                controlled && !renewed && freeze != null && target.GetAllStatuses().Contains(freeze) && target.ControlAppliedCount == count);
            target.RemoveAllNegativeStatuses();

            EhSeed(true, equal);
            neutralDot = BuffStatus.Create(99803, "verify_eh_neutral_dot", "중립 지속피해", source, target,
                new DamageOverTimeEffect(1001, 20f), duration: 3, category: BaseEnums.StatusCategory.Neutral);
            dotAdded = target.TryAddStatus(neutralDot);
            EhStat(source, BaseEnums.StatType.SpecialAttack, 1f);
            EhStat(target, BaseEnums.StatType.SpecialDefense, 10000f);
            int hp = target.HpCurr;
            int ticks = DamageOverTimeDetonation.Detonate(target, .5f, source);
            RuiCheck("기존 지속피해 즉발은 재명중·소모 없음", "1회 피해·객체/3턴 유지", ticks.ToString(),
                dotAdded && ticks == 1 && target.HpCurr < hp && target.GetAllStatuses().Contains(neutralDot) && neutralDot.RemainingTurns == 3);
            hp = target.HpCurr; neutralDot.OnOwnerTurn();
            RuiCheck("자연 지속피해 틱은 명중 재판정 없음", "피해·경과 1턴", (hp - target.HpCurr).ToString(),
                target.HpCurr < hp && neutralDot.ElapsedTurns == 1);
            UnitStatus benefit = BuffStatus.Create(neutralDot.StatusId, "verify_eh_cleanse_benefit", "검증 이로운 상태", null, target,
                new StatModifierEffect(BaseEnums.StatType.Attack, 1f), category: BaseEnums.StatusCategory.Positive);
            target.AddStatus(benefit);
            target.RemoveAllNegativeStatuses();
            RuiCheck("중립 지속피해 정화·동일 ID 버프 보존", "지속피해 제거·버프 유지", target.HasNegativeStatus().ToString(),
                !target.GetAllStatuses().Contains(neutralDot) && target.GetAllStatuses().Contains(benefit));

            foreach (Unit enemy in AliveEnemies()) YvResetEnemy(enemy);
            Unit resistant = AliveEnemies().First(unit => unit != target);
            EhStat(source, BaseEnums.StatType.SpecialAttack, 200f);
            EhStat(target, BaseEnums.StatType.SpecialDefense, 100f);
            foreach (Unit enemy in AliveEnemies().Where(unit => unit != target)) EhStat(enemy, BaseEnums.StatType.SpecialDefense, 100000000f);
            target.GrantCombatElement(BaseEnums.UnitElement.Hydro, 2);
            resistant.GrantCombatElement(BaseEnums.UnitElement.Hydro, 1);
            ElementalReaction.SpreadResult spread = null;
            System.Action<ElementalReaction.SpreadResult> onSpread = value => spread = value;
            ElementalReaction.AnySpreadResolved += onSpread;
            Random.InitState(6106);
            try { target.GrantCombatElement(BaseEnums.UnitElement.Anemo, source: source); }
            finally { ElementalReaction.AnySpreadResolved -= onSpread; }
            RuiCheck("확산 실제 전달 목록", "기존 물 부착만 있는 저항자는 전달 제외", spread?.Delivered.Count.ToString(),
                spread != null && spread.Delivered.Contains(target) && !spread.Delivered.Contains(resistant) &&
                resistant.GetAttachedElementRemainingTurns(BaseEnums.UnitElement.Hydro) == 1);
            RuiCheck("바유 확산 취약 명중 상속", "원본 취약·저항 대상 취약 없음", target.HasStatusKey(VayuVulnerability.BaseKey(BaseEnums.UnitElement.Hydro)).ToString(),
                target.HasStatusKey(VayuVulnerability.BaseKey(BaseEnums.UnitElement.Hydro)) &&
                !resistant.HasStatusKey(VayuVulnerability.BaseKey(BaseEnums.UnitElement.Hydro)));
            EhStat(source, BaseEnums.StatType.SpecialAttack, 100f);
            EhStat(target, BaseEnums.StatType.SpecialDefense, 100f);
            EhSeed(false, equal);
            bool allWeakness = VayuVulnerability.ApplyAll(source, target);
            RuiCheck("바유 전 속성 취약 단일 판정", "실패·전 속성 상태 없음", allWeakness.ToString(),
                !allWeakness && !target.HasStatusKey(VayuVulnerability.GaleAllKey));
            EhSeed(true, equal);
            allWeakness = VayuVulnerability.ApplyAll(source, target);
            RuiCheck("바유 전 속성 취약 단일 성공", "상태 1개·7속성 25%", allWeakness.ToString(),
                allWeakness && target.GetAllStatuses().Count(status => status.Key == VayuVulnerability.GaleAllKey) == 1 &&
                VayuVulnerability.AllElements.All(element => Near(VayuVulnerability.BonusFor(target, element), .25f)));

            yield return CaptureStill("effect_hit_resistance");
            GameManager.Instance.uiManager.ShowGuide("effecthit");
            yield return WaitUnscaled(.3f);
            string guideText = GameManager.Instance.uiManager.GuideForVerification.ArticleText;
            RuiCheck("자료실 실제 효과 명중 설명", "75%·두 배·아군 확정·원소 부착", guideText,
                guideText != null && guideText.Contains("75%") && guideText.Contains("두 배") && guideText.Contains("아군") && guideText.Contains("부착"));
            yield return CaptureStill("effect_hit_guide");
            GameManager.Instance.uiManager.GuideForVerification.Hide();
            Random.state = priorRandom;
            GameManager.Instance.ActionScheduler.EndRound(); GridManager.Instance.OnRoundEnd();
            GameManager.Instance.gameState = BaseEnums.GameState.Preparation;
            GameManager.Instance.enabled = wasEnabled;
            RuiFinish();
        }
    }
}
#endif
