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
using Effects.Negative;
using Entities;
using UnityEngine;
using Kind = Managers.ActionScheduler.ActionKind;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioEbisuCore(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); _cfActionIdSequence = 0;
            bool enabled = true;
            yield return YvPrepareScene(config, value => enabled = value);
            DebugMode.AllyInvincible = DebugMode.EnemyInvincible = false;
            var game = GameManager.Instance;
            Unit ebisu = CfAlly(163), orion = CfAlly(22), marie = CfAlly(160), enemy = AliveEnemies().First();
            if (ebisu == null || orion == null || marie == null || AliveAllies().Count != 5)
            {
                YvCheck("에비스 완성 편성", "5인 전원", string.Join(",", AliveAllies().Select(unit => unit.ID)), false);
                OsFinish(enabled); yield break;
            }
            var passive = ebisu.ActivePassiveCodes.OfType<EbisuSharedFortune>().Single();
            void Check(string label, bool ok, string actual = "") => YvCheck("에비스 " + label, "기획 사양", actual, ok);
            void ResetHp()
            {
                foreach (Unit unit in AliveAllies()) { unit.RestoreHpOutsideCombat(unit.HpMax); unit.ClearUltimateResource(); unit.RemoveShield(unit.ShieldCurr); }
                foreach (Unit unit in AliveEnemies()) unit.ClearUltimateResource();
            }
            DamageContext Hit(Unit target, int amount = 600)
            {
                var damage = new DamageContext(enemy, amount, BaseEnums.CodeType.Normal,
                    new List<int> { DamageTag.NormalAttack, DamageTag.SingleTarget, DamageTag.NonContactAttack });
                target.TakeDamage(damage); return damage;
            }
            Check("데이터·번개·팩토리", ebisu.Element == "Electro" && ebisu.ActiveNormalCode is EbisuLuckyCast && ebisu.ActiveUltimateCode is EbisuSafeHarbor);
            Check("전원 건강·디버프 없음 초월기 보류", !ebisu.ActiveUltimateCode.HasValidTarget());
            foreach (Unit unit in AliveAllies().Concat(AliveEnemies())) { CfNoCrit(unit); unit.ClearUltimateResource(); }
            passive.StopCode(); ResetHp();
            var baseline = Hit(orion); int total = baseline.ResolvedDamage;
            ResetHp(); passive.CastCode(); passive.CastCode();
            int guardHp = ebisu.HpCurr, victimHp = orion.HpCurr;
            var measured = new DotDiagnosisRow(); DamageContext shared;
            using (var recorder = new DotDiagnosisRecorder(measured)) shared = Hit(orion);
            int expected = Mathf.FloorToInt(total * EbisuSharedFortune.ShareRatio);
            Check("65% 분담·피해 보존", guardHp - ebisu.HpCurr == expected && victimHp - orion.HpCurr == total - expected &&
                shared.ResolvedDamage + shared.SharedDamage == total, $"{shared.ResolvedDamage}+{shared.SharedDamage}={total}");
            Check("분담은 피격 마나 없음", ebisu.ManaCurr == 0 && orion.ManaCurr == Unit.ManaPerHitTaken);
            Check("유지력 계측 분담 포함·중복 없음", measured.incoming == total && measured.hpDamage == total &&
                measured.units.Single(unit => unit.id == 163).incoming == expected &&
                measured.units.Single(unit => unit.id == 22).incoming == total - expected,
                $"총피해{measured.incoming}/HP손실{measured.hpDamage}/기대{total}");
            ResetHp();
            YvAddTestStatus(ebisu, 99881, "ebisu_test_armor", new StatModifierEffect(BaseEnums.StatType.Defense, 10000));
            ebisu.AddShield(500, ebisu); guardHp = ebisu.HpCurr;
            shared = Hit(orion);
            Check("분담 재경감·보호막 흡수 없음", guardHp - ebisu.HpCurr == expected && ebisu.ShieldCurr == 500);
            ebisu.RemoveStatusByKey("ebisu_test_armor"); ResetHp();
            orion.AddShield(total, orion);
            var shieldMeasurement = new DotDiagnosisRow();
            using (var recorder = new DotDiagnosisRecorder(shieldMeasurement)) Hit(orion);
            Check("유지력 계측 원 대상 보호막·분담 HP 분리", shieldMeasurement.incoming == total &&
                shieldMeasurement.hpDamage == expected && shieldMeasurement.shieldsAbsorbed == total - expected && orion.HpCurr == orion.HpMax,
                $"총피해{shieldMeasurement.incoming}/HP{shieldMeasurement.hpDamage}/보호막{shieldMeasurement.shieldsAbsorbed}");
            ResetHp();
            guardHp = ebisu.HpCurr;
            var dot = new DamageContext(enemy, 100, BaseEnums.CodeType.Effect, new List<int> { DamageTag.TrueDamage }) { IsDamageOverTime = true };
            orion.TakeDamage(dot);
            Check("지속피해도 분담·실제 반격 중첩은 제외", dot.SharedDamage == 65 && guardHp - ebisu.HpCurr == 65 && !orion.HasStatusKey(EbisuSharedFortune.BuffKey));
            ResetHp();
            YvAddTestStatus(orion, 99882, "ebisu_test_priority", new TargetPriorityModifierEffect(9999));
            int rain = 0;
            using (var log = new YvRecorder())
            {
                for (int i = 1; i <= 13; i++)
                {
                    ResetHp();
                    if (i == 1) orion.AddShield(1000, orion);
                    yield return NordAction(enemy, Kind.Normal, scheduler => scheduler.AdvanceAction(enemy, 1));
                    var effect = orion.CombatEffects().OfType<EbisuSharedFortune.GratitudeEffect>().SingleOrDefault();
                    Check("실제 피격 대상별 중첩 " + i, effect != null && effect.Stacks == Mathf.Min(i, EbisuSharedFortune.MaximumStacks));
                    Check("분담자는 피격 중첩 중복 없음 " + i, !ebisu.HasStatusKey(EbisuSharedFortune.BuffKey));
                }
                rain = log.Dealt.Count(hit => hit.Attacker == orion && hit.DamageContext.HasTag(DamageTag.CounterAttack));
            }
            Check("분담 후 오리온 반격 유지", rain >= 2, rain.ToString());
            passive.StopCode(); passive.CastCode(); ResetHp();
            yield return NordAction(enemy, Kind.Special, scheduler => scheduler.EnqueueSpecial(enemy, "ebisu_multi_probe", "다단 피격 검증", () =>
            {
                Hit(orion, 50); Hit(orion, 50); Hit(marie, 50); Hit(marie, 50);
            }));
            int Stacks(Unit unit) => unit.CombatEffects().OfType<EbisuSharedFortune.GratitudeEffect>().Select(effect => effect.Stacks).DefaultIfEmpty(0).Max();
            Check("다단은 대상별 행동당1중첩", Stacks(orion) == 1 && Stacks(marie) == 1 && Stacks(ebisu) == 0);
            passive.StopCode(); passive.CastCode(); ResetHp();
            bool contextReset = true;
            yield return NordAction(enemy, Kind.Special, scheduler => scheduler.EnqueueSpecial(enemy, "ebisu_area_probe", "광역 피격 검증", () =>
            {
                var area = new DamageContext(enemy, 50, BaseEnums.CodeType.Normal, new List<int> { DamageTag.NormalAttack, DamageTag.AllTarget });
                foreach (Unit target in AliveAllies())
                {
                    int final = Mathf.Max(1, Mathf.RoundToInt(50f * UnitStats.MitigationMultiplier(target.DefCurr, UnitStats.ArmorConstant(enemy.Level))));
                    target.TakeDamage(area);
                    contextReset &= area.SharedDamage == (target == ebisu ? 0 : Mathf.FloorToInt(final * EbisuSharedFortune.ShareRatio));
                }
            }));
            Check("광역은 피격자마다1중첩", AliveAllies().All(unit => Stacks(unit) == 1));
            Check("재사용 컨텍스트의 분담 기록 초기화", contextReset);
            // 뒤의3회 만료 검사는 이 광역 피격에서 얻은 중첩으로 이어 간다.
            orion.RemoveStatusByKey("ebisu_test_priority"); ResetHp();
            CfEnsureEffectHit(ebisu, AliveEnemies());
            using (var log = new YvRecorder())
            {
                yield return NordAction(ebisu, Kind.Normal, scheduler => scheduler.AdvanceAction(ebisu, 1));
                Check("일반 단일·최대체력 계수·번개 부착", log.Dealt.Count(hit => hit.Attacker == ebisu) == 1 &&
                    log.Elements.Count(element => element.Source == ebisu && element.Element == BaseEnums.UnitElement.Electro) == 1);
            }
            foreach (Unit ally in AliveAllies())
            {
                YvAddTestStatus(ally, 99883, "ebisu_test_slow", new StatModifierEffect(BaseEnums.StatType.Speed, -10), BaseEnums.StatusCategory.Negative);
                if (ally != ebisu) { ally.ControlAppliedCount = 0; ControlStatuses.ApplyFixedStun(ally, enemy, 2, effectHitResolved: true); }
            }
            ebisu.ModifyHp(Mathf.RoundToInt(ebisu.HpMax * .25f)); int beforeHeal = ebisu.HpCurr;
            Check("회복·정화 필요 시 초월기 허용", ebisu.ActiveUltimateCode.HasValidTarget());
            yield return NordAction(ebisu, Kind.Ultimate, _ => ebisu.FillUltimateResource());
            Check("자기 최대체력50% 회복", ebisu.HpCurr >= beforeHeal + Mathf.RoundToInt(ebisu.HpMax * .5f));
            Check("전원 제어·비제어 정화", AliveAllies().All(unit => !unit.HasNegativeStatus() && !unit.isControlled));
            Check("중첩은 피격 대상에 유지", marie.HasStatusKey(EbisuSharedFortune.BuffKey));
            orion.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved, new EventContext(orion));
            orion.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved, new EventContext(orion));
            Check("중첩 두 일반 완료 유지", orion.HasStatusKey(EbisuSharedFortune.BuffKey));
            orion.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved, new EventContext(orion));
            Check("중첩 세 일반 완료 만료", !orion.HasStatusKey(EbisuSharedFortune.BuffKey));
            ResetHp(); ebisu.ModifyHp(1);
            shared = Hit(orion);
            Check("분담자 사망·초과 피해 삭제 없음", !ebisu.isActive && shared.SharedDamage == 1 && shared.ResolvedDamage == total - 1);
            Check("사망 후 버프·분담 정리", !orion.HasStatusKey(EbisuSharedFortune.BuffKey));
            ResetHp(); shared = Hit(orion);
            Check("사망 후 보호 없음", shared.SharedDamage == 0 && shared.ResolvedDamage == total);
            foreach (Unit ally in GridManager.Instance.heroList.ToList()) if (ally != null && ally.isActive) ally.Die(enemy);
            game.gameState = BaseEnums.GameState.Preparation;
            game.RestorePreparationState(false, GameManager.MaxShortRestCharges, 0);
            game.inventoryManager.Initialize(); game.inventoryManager.RestoreGold(0);
            Check("측정 전멸 ID0·정체 보존", GridManager.Instance.heroList.Count(unit => unit != null && unit.ID == 0 && unit.LastActiveId > 0) == 5);
            CampaignPreparation();
            Check("측정 전멸 후 실제 긴 휴식 복구", AliveAllies().Count == 5 && AliveAllies().All(unit => unit.HpCurr == unit.HpMax) && game.inventoryManager.FoodUnits == 0);
            OsFinish(enabled);
        }
    }
}
#endif
