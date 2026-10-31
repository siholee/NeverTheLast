#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Normal;
using Codes.Passive;
using Codes.Ultimate;
using Core;
using Combat;
using Effects.Buffs;
using Entities;
using UnityEngine;
using Kind = Managers.ActionScheduler.ActionKind;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        // 특정 캐릭터의 옛 기술 대신 공통 상태 제거 API로 해제 경계를 검증한다.
        private static void YvDispelBeneficial(Unit target)
        {
            foreach (var status in target.GetAllStatuses().Where(s => s.IsBeneficial || s.Category == BaseEnums.StatusCategory.Positive).ToList())
                target.RemoveStatus(status);
        }
        private IEnumerator ScenarioNicolCore(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); _cfActionIdSequence = 0;
            bool enabled = true;
            yield return YvPrepareScene(config, v => enabled = v);
            DebugMode.AllyInvincible = DebugMode.EnemyInvincible = false;
            Unit nicol = CfAlly(168), astolfo = CfAlly(162), jean = CfAlly(7), enemy = AliveEnemies().First();
            var passive = nicol.ActivePassiveCodes.OfType<NicolTeslaGenerator>().Single();
            void Check(string label, bool ok, string actual = "") => YvCheck("니콜·치유 " + label, "설계 및 실제 공통 전투 경로", actual, ok);
            foreach (Unit u in AliveAllies().Concat(AliveEnemies()))
            {
                foreach (var p in u.ActivePassiveCodes.ToList()) p.StopCode();
                if (u != nicol && u != astolfo) u.ActiveUltimateCode?.StopCode();
                u.RemoveShield(u.ShieldCurr); u.ResetCombatElements(); CfNoCrit(u);
                u.RemoveStatusByKey(EnemyCombatBudget.StatusKey);
            }
            passive.CastCode(); passive.CastCode();
            CfEnsureEffectHit(nicol, AliveEnemies()); CfEnsureEffectHit(jean, AliveEnemies());
            int Charges(Unit u, bool power) => u.GetAllStatuses().Where(s => s.Key == (power ? NicolTeslaGenerator.PowerKey : NicolTeslaGenerator.GuardKey))
                .SelectMany(s => s.Effects).Select(e => e.EffectObject).OfType<NicolTeslaGenerator.ChargeEffect>().Sum(e => e.Charges);
            Check("데이터·팩토리·후열", nicol.Element == "Electro" && nicol.ActiveNormalCode is NicolAlternatingMotor &&
                nicol.ActiveUltimateCode is NicolThunderGrid && nicol.DefCurr < jean.DefCurr);
            Check("운영 새 치유 요원 인식", DotStudyCodeValue(astolfo, 648) > 90 && DotStudyCodeValue(astolfo, 650) > 90);
            Check("운영 초전도 편성에 지딜 오라 제외", !CampaignHasDotSource() && DotStudyCodeValue(astolfo, 738) == 1 && DotStudyCodeValue(astolfo, 732) == 1);
            foreach (Unit u in AliveAllies()) u.RestoreHpOutsideCombat(u.HpMax);
            Check("치유 자동 시전 만피에서 보류", !astolfo.ActiveUltimateCode.HasValidTarget());
            jean.ModifyHp(Mathf.FloorToInt(jean.HpMax * .89f));
            Check("치유 자동 시전 체력90% 이하에서 허용", astolfo.ActiveUltimateCode.HasValidTarget());
            jean.RestoreHpOutsideCombat(jean.HpMax);
            var catalog = CampaignBuildCatalog();
            Check("편성 비교 표시는 고정 별칭 사용", new[] { "b48_nicol_sei", "b48_shi_hypercarry" }.All(alias =>
                catalog.teams.Single(s => s.aliases.Contains("검증 편성/" + alias)).currentComparison));
            nicol.ClearUltimateResource();
            using (var log = new YvRecorder())
            {
                yield return NordAction(nicol, Kind.Normal, s => s.AdvanceAction(nicol, 1));
                Check("실제6타·단일 정상 완료", log.Dealt.Count(h => h.Attacker == nicol && !h.DamageContext.HasTag(DamageTag.BonusDamage)) == 6 && nicol.ManaCurr == Unit.ManaPerNormalAction);
            }
            Check("번개 부착6회·중복 구독 없음", AliveAllies().All(u => Charges(u, false) == 6) && AliveAllies().All(u => Charges(u, true) == 0));
            var charged = AliveEnemies().First(u => u.HasAttachedElement(BaseEnums.UnitElement.Electro));
            charged.GrantCombatElement(BaseEnums.UnitElement.Cryo, source: jean);
            Check("번개가 후행하지 않아도 초전도 증폭", AliveAllies().All(u => Charges(u, true) == 1));
            foreach (Unit e in AliveEnemies()) e.ResetCombatElements();
            enemy.GrantCombatElement(BaseEnums.UnitElement.Pyro, source: nicol);
            enemy.GrantCombatElement(BaseEnums.UnitElement.Hydro, source: nicol);
            Check("번개 없는 증발 제외", Charges(jean, true) == 1);
            int guard = Charges(jean, false);
            var randomState = UnityEngine.Random.state;
            EhStat(enemy, BaseEnums.StatType.SpecialDefense, nicol.StatValue(BaseEnums.StatType.SpecialAttack) * 10f);
            EhSeed(false, DamageMitigation.EffectHitChance(nicol, enemy));
            Check("부착 저항 보상 없음", !enemy.GrantCombatElement(BaseEnums.UnitElement.Electro, source: nicol) && Charges(jean, false) == guard);
            enemy.RemoveStatusByKey("verify_eh_stat_SpecialDefense");
            UnityEngine.Random.state = randomState;
            enemy.ResetCombatElements(); enemy.GrantCombatElement(BaseEnums.UnitElement.Electro, source: enemy);
            Check("적 자신의 부착 제외", Charges(jean, false) == guard);
            void Hit(Unit a, Unit t, BaseEnums.CodeType kind, params int[] tags) => t.TakeDamage(new DamageContext(a, 100, kind, tags.ToList()));
            using (var log = new YvRecorder())
            {
                Hit(jean, enemy, BaseEnums.CodeType.Normal, DamageTag.TrueDamage, DamageTag.NormalAttack);
                Check("증폭20% 실제 적용·1회 소비", log.Dealt.Last(h => h.Attacker == jean).DamageDealt == 120 && Charges(jean, true) == 0);
            }
            jean.AddShield(1000, jean); int shield = jean.ShieldCurr;
            Hit(enemy, jean, BaseEnums.CodeType.Normal, DamageTag.TrueDamage, DamageTag.NormalAttack);
            Check("보호막 피격 경감15%·1개 소비", shield - jean.ShieldCurr == 85 && Charges(jean, false) == guard - 1, (shield - jean.ShieldCurr) + "/" + Charges(jean, false));
            Hit(enemy, jean, BaseEnums.CodeType.Effect, DamageTag.TrueDamage, DamageTag.BonusDamage);
            Check("덧붙는 피해 소비 제외", Charges(jean, false) == guard - 1);
            foreach (Unit e in AliveEnemies()) e.ResetCombatElements();
            yield return NordAction(nicol, Kind.Ultimate, _ => nicol.FillUltimateResource());
            Check("실제 광역 번개·방어력 감소", AliveEnemies().All(e => e.HasAttachedElement(BaseEnums.UnitElement.Electro) && e.HasStatusKey("nicol_defense_shred")));
            enemy.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved, new EventContext(enemy));
            Check("방깎 첫 일반 완료 유지", enemy.HasStatusKey("nicol_defense_shred"));
            enemy.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved, new EventContext(enemy));
            Check("방깎 두 번째 일반 완료 만료", !enemy.HasStatusKey("nicol_defense_shred"));
            int healedTotal = 0, healedTargets = 0;
            Action<Unit, Unit, int, int> healing = (target, source, effective, overflow) =>
            { if (source == astolfo) { healedTotal += effective + overflow; healedTargets++; } };
            foreach (Unit u in AliveAllies()) u.ModifyHp(1);
            Unit.AnyHealed += healing;
            try { yield return NordAction(astolfo, Kind.Ultimate, _ => astolfo.FillUltimateResource()); }
            finally { Unit.AnyHealed -= healing; }
            int expected = Mathf.RoundToInt(astolfo.StatValue(BaseEnums.StatType.Attack) * AstolfoReturnGift.HealAttackRatio);
            Check("공600% 치유·최대체력 무관", healedTargets == AliveAllies().Count && healedTotal == expected * healedTargets, healedTotal + "/" + expected * healedTargets);
            int original = healedTotal;
            YvAddTestStatus(astolfo, 99876, "nicol_atk_heal", new StatModifierEffect(BaseEnums.StatType.Attack, 1f, true));
            foreach (Unit u in AliveAllies()) u.ModifyHp(1);
            healedTotal = healedTargets = 0; Unit.AnyHealed += healing;
            try { yield return NordAction(astolfo, Kind.Ultimate, _ => astolfo.FillUltimateResource()); }
            finally { Unit.AnyHealed -= healing; }
            Check("공격력 두 배면 치유 두 배", Mathf.Abs(healedTotal - original * 2) <= healedTargets);
            passive.StopCode();
            Check("정리 후 전원 충전 버프 제거", AliveAllies().All(u => Charges(u, false) == 0 && Charges(u, true) == 0));
            enemy.ResetCombatElements(); enemy.GrantCombatElement(BaseEnums.UnitElement.Electro, source: nicol);
            Check("정리 후 부착 이벤트 잔류 없음", Charges(jean, false) == 0);
            passive.CastCode();
            enemy.GrantCombatElement(BaseEnums.UnitElement.Electro, source: nicol);
            Check("재설치1회 구독", Charges(jean, false) == 1);
            int powerBefore = Charges(jean, true);
            enemy.ResetCombatElements();
            enemy.GrantCombatElement(BaseEnums.UnitElement.Hydro, source: nicol);
            enemy.GrantCombatElement(BaseEnums.UnitElement.Anemo, source: nicol);
            Check("물 확산은 번개 반응 아님", Charges(jean, true) == powerBefore);
            enemy.ResetCombatElements();
            enemy.GrantCombatElement(BaseEnums.UnitElement.Electro, source: nicol);
            enemy.GrantCombatElement(BaseEnums.UnitElement.Anemo, source: nicol);
            Check("번개 확산은 실제 원소 쌍으로 인정", Charges(jean, true) > powerBefore);
            nicol.TakeDamage(new DamageContext(enemy, 1000000, BaseEnums.CodeType.Effect, new List<int> { DamageTag.TrueDamage }));
            Check("시전자 사망 즉시 전원 정리", Charges(jean, false) == 0 && Charges(jean, true) == 0);
            OsFinish(enabled);
        }
    }
}
#endif
