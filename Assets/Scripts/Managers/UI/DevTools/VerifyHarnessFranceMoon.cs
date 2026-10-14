#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Passive;
using Codes.Special;
using Codes.Ultimate;
using Combat;
using Core;
using Effects.Buffs;
using Entities;
using Managers.UI.Core;
using UnityEngine;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator FranceAction(Unit actor, ActionScheduler.ActionKind kind, Action<ActionScheduler> setup)
        {
            foreach (Unit ally in AliveAllies()) ally.ClearUltimateResource();
            bool finished = false;
            void End(Unit unit, ActionScheduler.ActionKind action) { if (unit == actor && action == kind) finished = true; }
            ActionScheduler.AnyActionFinished += End;
            yield return YvRunScheduled(setup, () => finished && GameManager.Instance.ActionScheduler.ExecutingUnit == null &&
                GameManager.Instance.ActionScheduler.DebugQueuedCount(ActionScheduler.ActionKind.Additional) == 0 &&
                GameManager.Instance.ActionScheduler.DebugQueuedCount(ActionScheduler.ActionKind.PriorityAdditional) == 0);
            ActionScheduler.AnyActionFinished -= End;
        }
        private IEnumerator ScenarioFranceMoon(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();
            bool wasEnabled = true;
            yield return YvPrepareScene(config, enabled => wasEnabled = enabled);
            Unit marie = AliveAllies().Single(x => x.ID == 160), napoleon = AliveAllies().Single(x => x.ID == 161),
                surya = AliveAllies().Single(x => x.ID == 67), moon = AliveAllies().Single(x => x.ID == 45);
            Unit enemy = AliveEnemies().First();
            foreach (Unit target in AliveEnemies())
                YvAddTestStatus(target, 99954, "verify_zero_spdef", new StatModifierEffect(BaseEnums.StatType.SpecialDefense, -target.StatValue(BaseEnums.StatType.SpecialDefense)));
            var execution = surya.ActivePassiveCodes.OfType<SuryaExecution>().Single();
            var emergency = (MarieEmergencySupply)marie.ActiveUltimateCode;
            var damage = new List<DamageResolvedContext>();
            void Damage(DamageResolvedContext context) => damage.Add(context);
            Unit.AnyDamageDealt += Damage;
            YvCheck("등록된 신규 3인 + 개편 수리야", "코드·스프라이트 4인", string.Join(",", new[] { marie, napoleon, surya, moon }.Select(x => x.UnitName)),
                new[] { marie, napoleon, surya, moon }.All(x => x.ActiveNormalCode != null && x.ActiveUltimateCode != null));

            // 실제 일반행동 3회: 표식 1 → 2 → 종언탄. 적이 바뀌지 않게 우선도를 고정한다.
            YvAddTestStatus(enemy, 99955, "verify_priority", new TrioPriority());
            for (int shot = 1; shot <= 3; shot++)
            {
                damage.Clear();
                yield return FranceAction(surya, ActionScheduler.ActionKind.Normal, s => s.AdvanceAction(surya, 1));
                int expected = shot < 3 ? shot : 0;
                YvCheck("수리야 사격 " + shot + " 표식", expected.ToString(), execution.Marks(enemy).ToString(), execution.Marks(enemy) == expected);
                var hit = damage.FirstOrDefault(x => x.Attacker == surya && x.Target == enemy && x.DamageContext.CodeType == BaseEnums.CodeType.Normal);
                YvCheck("수리야 사격 " + shot + " 방어 인정", shot == 3 ? "0.75" : "1", hit?.DamageContext.DefenseStatMultiplier.ToString(),
                    hit != null && Near(hit.DamageContext.DefenseStatMultiplier, shot == 3 ? .75f : 1));
            }
            yield return FranceAction(surya, ActionScheduler.ActionKind.Ultimate, s => surya.FillUltimateResource());
            YvCheck("수리야 U 전체 준비 표식", "전원 2", string.Join(",", AliveEnemies().Select(execution.Marks)), AliveEnemies().All(x => execution.Marks(x) == 2));
            YvCheck("수리야 U 공격력 버프", "버프 존재", surya.HasStatusKey("surya_sunset").ToString(), surya.HasStatusKey("surya_sunset"));
            execution.Consume(enemy); // 특수행동은 미표식 대상을 맞히지 않아야 한다.
            damage.Clear();
            yield return FranceAction(surya, ActionScheduler.ActionKind.Special, s => s.EnqueueSpecial(surya, "verify_volley", "석양의 일제사격", () => surya.ActiveSpecialCode.CastCode()));
            var finale = damage.Where(x => x.Attacker == surya && x.DamageContext.CodeType == BaseEnums.CodeType.Normal).ToList();
            YvCheck("수리야 SP 표식 대상만 사격", (AliveEnemies().Count - 1).ToString(), finale.Count.ToString(), finale.Count == AliveEnemies().Count - 1 && finale.All(x => x.Target != enemy));
            YvCheck("수리야 SP 표식 소모", "전원 0", string.Join(",", AliveEnemies().Select(execution.Marks)), AliveEnemies().All(x => execution.Marks(x) == 0));
            YvCheck("수리야 SP 일반 차례 버프 보존", "남은 2", surya.GetAllStatuses().First(x => x.Key == "surya_sunset").RemainingTurns.ToString(), surya.GetAllStatuses().First(x => x.Key == "surya_sunset").RemainingTurns == 2);

            // 2026-10-08 개편: 나폴레옹은 추가행동 응답 사격 대신 지휘 특수행동을 연다. 상세 계약은 characters_followup_core가 검증하고,
            // 여기서는 프랑스 편성 안의 회귀(응답 사격 없음 · 일반행동 한 번 → 일제 사격 한 번 · 마리 오라 유지)만 본다.
            Unit marked = enemy;
            damage.Clear();
            float critAura = -1;
            yield return FranceAction(marie, ActionScheduler.ActionKind.Additional, s => s.EnqueueAdditional(marie, "ally1", "검증 추가행동", () =>
            {
                critAura = MarieSupply.AdditionalCritDamage(marie);
                marked.TakeDamage(new DamageContext(marie, 100, BaseEnums.CodeType.Passive,
                    new List<int> { DamageTag.AdditionalAttack, DamageTag.NonContactAttack, DamageTag.Shot }));
            }));
            YvCheck("나폴레옹 추가행동 응답 사격 없음", "0", damage.Count(x => x.Attacker == napoleon).ToString(), damage.All(x => x.Attacker != napoleon));
            YvCheck("마리 추가행동 치명 피해 비례", (marie.StatValue(BaseEnums.StatType.CritDamage) * .4f).ToString(), critAura.ToString(), Near(critAura, marie.StatValue(BaseEnums.StatType.CritDamage) * .4f));
            int volleys = 0;
            void Volley(Unit commander, Unit target, int participants) { if (commander == napoleon) volleys++; }
            NapoleonGrandVolley.AnyVolleyStarted += Volley;
            damage.Clear();
            yield return FranceAction(napoleon, ActionScheduler.ActionKind.Special, s => s.AdvanceAction(napoleon, 1));
            NapoleonGrandVolley.AnyVolleyStarted -= Volley;
            int allyNormalHits = damage.Count(x => x.Attacker != napoleon && !x.Attacker.IsEnemy && x.DamageContext.HasTag(DamageTag.NormalAttack));
            YvCheck("나폴레옹 일반행동 → 일제 사격 1회", "특수 1 / 나폴레옹 피해 0 / 동료 일반 공격 ≥1", volleys + "/" + damage.Count(x => x.Attacker == napoleon) + "/" + allyNormalHits,
                volleys == 1 && damage.All(x => x.Attacker != napoleon) && allyNormalHits >= 1);
            YvCheck("나폴레옹 물 속성 유지", "Hydro", napoleon.Element, napoleon.Element == "Hydro");

            moon.ModifyHp(moon.HpMax / 2);
            int hp = moon.HpCurr;
            yield return FranceAction(moon, ActionScheduler.ActionKind.Additional, s => s.EnqueueAdditional(moon, "heal_actor", "행동 종료 치유", () => { }));
            int smallHeal = Mathf.RoundToInt(marie.StatValue(BaseEnums.StatType.Attack) * .15f);
            YvCheck("마리 행동 종료 치유", smallHeal.ToString(), (moon.HpCurr - hp).ToString(), moon.HpCurr - hp == smallHeal);
            moon.ModifyHp(moon.HpMax / 4);
            moon.RemoveShield(moon.ShieldCurr);
            hp = moon.HpCurr;
            int reserve = marie.GetCombatResource(MarieEmergencySupply.Resource);
            yield return FranceAction(enemy, ActionScheduler.ActionKind.Additional, s => s.EnqueueAdditional(enemy, "hurt_ally", "치유 트리거", () =>
                moon.TakeDamage(new DamageContext(enemy, 100, BaseEnums.CodeType.Effect, new List<int>()))));
            YvCheck("마리 긴급지원 우선 치유", "중첩 1 소모 / HP 증가", $"중첩 {reserve}→{marie.GetCombatResource(MarieEmergencySupply.Resource)} / HP {hp}→{moon.HpCurr}",
                marie.GetCombatResource(MarieEmergencySupply.Resource) == reserve - 1 && moon.HpCurr > hp);
            moon.ModifyHp(moon.HpMax / 4);
            moon.RemoveShield(moon.ShieldCurr);
            reserve = marie.GetCombatResource(MarieEmergencySupply.Resource);
            yield return FranceAction(enemy, ActionScheduler.ActionKind.Additional, s => s.EnqueueAdditional(enemy, "cancel_heal", "예약 후 회복", () =>
            {
                moon.TakeDamage(new DamageContext(enemy, 100, BaseEnums.CodeType.Effect, new List<int>()));
                moon.Heal(moon.HpMax, moon);
            }));
            YvCheck("마리 조건 해소 중첩 반환", reserve.ToString(), marie.GetCombatResource(MarieEmergencySupply.Resource).ToString(),
                marie.GetCombatResource(MarieEmergencySupply.Resource) == reserve);

            foreach (Unit target in AliveEnemies()) foreach (var st in target.GetAllStatuses().Where(x => x.NormalActionDuration).ToArray()) target.RemoveStatusByKey(st.Key);
            ActionDot.Apply(moon, enemy, ActionDotKind.Curse, inheritedHit: true);
            var curse = enemy.GetAllStatuses().Single(x => x.Key == "curse");
            ActionDot.Apply(napoleon, enemy, ActionDotKind.Curse, inheritedHit: true);
            YvCheck("저주 단일 인스턴스 / 원 생성자 유지", "1 / 츠쿠요미 / 2회", enemy.GetAllStatuses().Count(x => x.Key == "curse") + "/" + curse.Caster.UnitName + "/" + curse.RemainingTurns,
                enemy.GetAllStatuses().Count(x => x.Key == "curse") == 1 && curse.Caster == moon && curse.RemainingTurns == 2);
            int before = enemy.HpCurr;
            ActionDot.OnActionStarted(enemy, ActionScheduler.ActionKind.Normal);
            YvCheck("저주 대상 일반행동에는 발동 없음", before.ToString(), enemy.HpCurr.ToString(), before == enemy.HpCurr);
            ActionDot.OnActionStarted(moon, ActionScheduler.ActionKind.Additional);
            YvCheck("저주 생성자 추가행동에는 발동 없음", before.ToString(), enemy.HpCurr.ToString(), before == enemy.HpCurr);
            damage.Clear();
            yield return FranceAction(moon, ActionScheduler.ActionKind.Normal, s => s.AdvanceAction(moon, 1));
            YvCheck("저주 생성자 실제 일반행동에 발동", "지속피해 1회", damage.Count(x => x.Target == enemy && x.DamageContext.IsDamageOverTime).ToString(), damage.Count(x => x.Target == enemy && x.DamageContext.IsDamageOverTime) == 1);
            YvCheck("생성자 행동은 대상 수명 소모 없음", "2", curse.RemainingTurns.ToString(), curse.RemainingTurns == 2);
            ActionDot.OnNormalResolved(enemy);
            YvCheck("저주 대상 일반행동 1회 후", "1", curse.RemainingTurns.ToString(), curse.RemainingTurns == 1);
            ActionDot.OnNormalResolved(enemy);
            YvCheck("저주 대상 일반행동 2회 만료", "제거", enemy.HasStatusKey("curse").ToString(), !enemy.HasStatusKey("curse"));
            foreach (var kind in new[] { ActionDotKind.Shock, ActionDotKind.Burn, ActionDotKind.Poison }) ActionDot.Apply(moon, enemy, kind, inheritedHit: true);
            damage.Clear();
            ActionDot.OnActionStarted(enemy, ActionScheduler.ActionKind.Special);
            YvCheck("화상은 특수행동에도 발동", "1회", damage.Count.ToString(), damage.Count == 1);
            damage.Clear();
            ActionDot.OnActionStarted(enemy, ActionScheduler.ActionKind.Normal);
            YvCheck("일반행동 감전·화상·맹독 동시 발동", "3회", damage.Count.ToString(), damage.Count == 3);
            YvCheck("츠쿠요미 특공 비례 오라", "1보다 큰 독립 배율", TsukuyomiNight.DotMultiplier(moon).ToString(), TsukuyomiNight.DotMultiplier(moon) > 1);
            float moonBase = moon.StatValue(BaseEnums.StatType.SpecialAttack);
            float allyBase = marie.StatValue(BaseEnums.StatType.SpecialAttack);
            yield return FranceAction(moon, ActionScheduler.ActionKind.Ultimate, s => moon.FillUltimateResource());
            float enhanced = moon.StatValue(BaseEnums.StatType.SpecialAttack);
            YvCheck("츠쿠요미 U 특공 고정 가산", (moonBase * .2f).ToString(), (marie.StatValue(BaseEnums.StatType.SpecialAttack) - allyBase).ToString(),
                Near(marie.StatValue(BaseEnums.StatType.SpecialAttack) - allyBase, moonBase * .2f));
            yield return FranceAction(moon, ActionScheduler.ActionKind.Ultimate, s => moon.FillUltimateResource());
            YvCheck("츠쿠요미 U 재시전 자기 증폭 방지", enhanced.ToString(), moon.StatValue(BaseEnums.StatType.SpecialAttack).ToString(),
                Near(enhanced, moon.StatValue(BaseEnums.StatType.SpecialAttack)));
            var snapshots = enemy.GetAllStatuses().Where(x => x.NormalActionDuration).ToArray();
            int[] durations = snapshots.Select(x => x.RemainingTurns).ToArray();
            damage.Clear();
            int ticks = DamageOverTimeDetonation.Detonate(enemy, 1f, moon);
            YvCheck("야마 공통 발동 4종·DoT 식별 보존", "4종", ticks.ToString(), ticks == 4 && damage.All(x => x.DamageContext.IsDamageOverTime));
            YvCheck("즉시 발동 상태·기간 보존", "참조와 기간 동일", enemy.GetAllStatuses().Count.ToString(),
                snapshots.All(x => enemy.GetAllStatuses().Contains(x)) && snapshots.Select(x => x.RemainingTurns).SequenceEqual(durations));
            yield return FranceAction(moon, ActionScheduler.ActionKind.Normal, s => s.AdvanceAction(moon, 1));
            YvCheck("츠쿠요미 U 첫 일반행동 후 유지", enhanced.ToString(), moon.StatValue(BaseEnums.StatType.SpecialAttack).ToString(), Near(enhanced, moon.StatValue(BaseEnums.StatType.SpecialAttack)));
            yield return FranceAction(moon, ActionScheduler.ActionKind.Normal, s => s.AdvanceAction(moon, 1));
            YvCheck("츠쿠요미 U 두 번째 일반행동 후 해제", moonBase.ToString(), moon.StatValue(BaseEnums.StatType.SpecialAttack).ToString(),
                Near(moonBase, moon.StatValue(BaseEnums.StatType.SpecialAttack)) && Near(allyBase, marie.StatValue(BaseEnums.StatType.SpecialAttack)));
            YvCheck("동일 원소 반응 삭제 / 불+풀 화상", "불+불 없음 / 불+풀 화상", Effects.Negative.ElementalReaction.AllReactions().Count().ToString(),
                !Effects.Negative.ElementalReaction.AllReactions().Any(x => x.A == BaseEnums.UnitElement.Pyro && x.B == BaseEnums.UnitElement.Pyro) &&
                Effects.Negative.ElementalReaction.AllReactions().Any(x => x.A == BaseEnums.UnitElement.Pyro && x.B == BaseEnums.UnitElement.Dendro && x.Name == "화상"));
            // 실제 공용 피해 경로에서 DoT 보상과 즉발 반응을 구별한다.
            int Probe(bool dot)
            {
                var context = new DamageContext(moon, 1000, BaseEnums.CodeType.Effect, new List<int>()) { IsDamageOverTime = dot };
                enemy.TakeDamage(context);
                return context.ResolvedDamage;
            }
            int plain = Probe(false), baseDot = Probe(true);
            moon.TryGrantRewardCode(617); YvCode(moon, 617)?.CastCode();
            int plague = Probe(true);
            YvCheck("617 지속피해 보상 1회 적용", "DoT ×1.15", plague.ToString(), Mathf.Abs(plague - baseDot * 1.15f) <= 2);
            int instant = Probe(false);
            YvCheck("617 즉발 피해는 제외", plain.ToString(), instant.ToString(), instant == plain);
            moon.RemoveRewardCode(617);
            moon.TryGrantRewardCode(811); YvCode(moon, 811)?.CastCode();
            int pact = Probe(true);
            YvCheck("811 지속피해 보상 1회 적용", "DoT ×1.35", pact.ToString(), Mathf.Abs(pact - baseDot * 1.35f) <= 2);
            instant = Probe(false);
            YvCheck("811 즉발 피해는 제외", plain.ToString(), instant.ToString(), instant == plain);
            foreach (var status in enemy.GetAllStatuses().Where(x => x.NormalActionDuration).ToArray()) enemy.RemoveStatusByKey(status.Key);
            ActionDot.Apply(moon, enemy, ActionDotKind.Curse, inheritedHit: true);
            ActionDot.Apply(moon, enemy, ActionDotKind.Curse, inheritedHit: true);
            var extended = enemy.GetAllStatuses().Single(x => x.Key == "curse");
            YvCheck("811 기간 보너스 갱신은 누적 없음", "3회", extended.RemainingTurns.ToString(), extended.RemainingTurns == 3);
            var tick = extended.Effects.Single().EffectObject.CreateDamageOverTimeTick();
            enemy.TakeDamage(tick);
            damage.Clear(); DamageOverTimeDetonation.Detonate(enemy, 1, moon);
            YvCheck("811 자연·즉시 발동 보상 동일", tick.ResolvedDamage.ToString(), damage.Single().DamageDealt.ToString(), damage.Single().DamageDealt == tick.ResolvedDamage);
            moon.RemoveRewardCode(811);
            yield return CaptureStill("france_moon_combat");
            Unit.AnyDamageDealt -= Damage;
            GameManager.Instance.enabled = wasEnabled;
            if (_yvFailures.Count > 0) FailCurrent(string.Join(" / ", _yvFailures));
            _yvFailures.Clear();
        }
    }
}
#endif
