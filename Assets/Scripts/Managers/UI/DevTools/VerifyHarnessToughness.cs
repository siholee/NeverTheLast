#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Normal;
using Codes.Passive;
using Codes.Ultimate;
using Combat;
using Core;
using Entities;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioToughnessCore(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();
            yield return PrepareStage(config, 8, false, false, false);
            var game = GameManager.Instance;
            var allies = AliveAllies();
            var enemies = AliveEnemies();
            Unit orpheus = allies.First(u => u.ID == 25), shi = allies.First(u => u.ID == 2), anubis = allies.First(u => u.ID == 121);
            Unit enemy = enemies.OrderByDescending(u => u.UnitTier == "boss").First();
            void Check(string name, bool ok, string actual = "") => RuiCheck(name, "사양 일치", actual, ok);
            Check("부여자 없는 준비 화면 강인도 없음", enemies.All(u => u.ToughnessMax == 0));
            Check("오르페우스 기존 원화·팩토리", orpheus.currentCell.portraitRenderer.sprite != null &&
                orpheus.ActiveNormalCode is OrpheusMelody && orpheus.ActiveUltimateCode is OrpheusLament);
            foreach (int id in new[] { 633, 731, 717, 822, 823 })
                Check("보상 " + id + " 등록·티어", Codes.Passive.Reward.RewardCodeFactory.IsImplemented(id) &&
                    RewardCodeCatalog.Find(id)?.tier == (id == 633 ? 2 : id == 731 ? 3 : 4));
            foreach (Unit ally in allies) ally.Invoke(UnitEventType.OnRoundStart, new EventContext(ally));
            Check("특성 전장 전체 강인도 생성", enemies.All(u => u.ToughnessCurr == u.ToughnessMax && u.ToughnessMax > 0));
            Check("보스 강인도 고정150", enemy.ToughnessMax == 150, enemy.UnitTier + ":" + enemy.ToughnessMax);
            // 추가 생성은 기존 삭감량을 덮어쓰지 않는다.
            DamageContext Attack(Unit attacker, int raw = 1) => new(attacker, raw, CodeType.Normal,
                new() { DamageTag.NormalAttack, DamageTag.SingleTarget, DamageTag.NonContactAttack });
            var initial = Attack(shi); int hp = enemy.HpCurr;
            enemy.TakeDamage(initial);
            Check("체력 피해와 고정 강인도30 병렬", enemy.HpCurr < hp && enemy.ToughnessCurr == 120);
            Toughness.Ensure(enemy);
            Check("특성 재확인이 현재 강인도를 채우지 않음", enemy.ToughnessCurr == 120);
            enemy.AddShield(1000, orpheus); hp = enemy.HpCurr;
            enemy.TakeDamage(Attack(shi));
            Check("보호막 피격도 삭감", enemy.HpCurr == hp && enemy.ToughnessCurr == 90);
            int t = enemy.ToughnessCurr;
            enemy.TakeDamage(new DamageContext(shi, 1, CodeType.Effect, new()) { IsDamageOverTime = true });
            enemy.TakeDamage(new DamageContext(shi, 1, CodeType.Passive, new() { DamageTag.ElementalReaction }));
            enemy.TakeDamage(new DamageContext(shi, 1, CodeType.Passive, new() { DamageTag.TrueDamage, DamageTag.BonusDamage }));
            Check("지속·반응·덧붙는 피해 제외", enemy.ToughnessCurr == t);
            DebugMode.EnemyInvincible = true; enemy.TakeDamage(Attack(shi)); DebugMode.EnemyInvincible = false;
            Check("무효화 타격 삭감 없음", enemy.ToughnessCurr == t);
            Check("허허실실 장착", shi.TryGrantRewardCode(633));
            shi.CastPassiveCode();
            enemy.TakeDamage(Attack(shi));
            Check("허허실실25% 삭감 증가", enemy.ToughnessCurr == t - 38, enemy.ToughnessCurr.ToString());
            // 실제 초월기 연출과 정점 콜백을 통과한다.
            var ultimate = (OrpheusLament)orpheus.ActiveUltimateCode;
            ultimate.CastCode();
            float until = Time.realtimeSinceStartup + 8f;
            while (orpheus.isCasting && Time.realtimeSinceStartup < until) yield return null;
            Check("비탄의 연주 아군5명 적용", ultimate.CompletedNormally && allies.All(u => u.GetAllStatuses().Any(s => s.Key == OrpheusLament.BuffKey)));
            float bonus = OrpheusLament.Bonus(orpheus);
            Check("특공 기반 효율·허허실실 합산", Mathf.Abs(Toughness.Efficiency(shi) - (1.25f + bonus)) < .001f);
            float bonus0 = bonus;
            var boost = Effects.Buffs.BuffStatus.Create(992250, "verify_orpheus_power", "검증", orpheus, orpheus,
                new Effects.Buffs.StatModifierEffect(StatType.SpecialAttack, 1000f), isBeneficial: true);
            orpheus.AddStatus(boost); orpheus.RefreshAttributes();
            Check("특공 투자 효율 상승·상한", OrpheusLament.Bonus(orpheus) > bonus0 && OrpheusLament.Bonus(orpheus) < .5f);
            orpheus.RemoveStatusByKey("verify_orpheus_power");
            for (int n = 0; n < 2; n++) shi.Invoke(UnitEventType.OnNormalActionResolved, new EventContext(shi));
            Check("버프 일반행동2회 후 유지", shi.GetAllStatuses().Any(s => s.Key == OrpheusLament.BuffKey));
            shi.Invoke(UnitEventType.OnNormalActionResolved, new EventContext(shi));
            Check("버프 일반행동3회 후 만료", !shi.GetAllStatuses().Any(s => s.Key == OrpheusLament.BuffKey));

            Check("처치 성장2종·공물 장착", shi.TryGrantRewardCode(717) && shi.TryGrantRewardCode(822) && orpheus.TryGrantRewardCode(823));
            shi.CastPassiveCode(); orpheus.CastPassiveCode();
            float atk0 = shi.StatValue(StatType.Attack), speed0 = shi.StatValue(StatType.Speed);
            float souls0 = anubis.GetPermanentStacks(AnubisSoulHarvest.SoulKey) + anubis.GetPermanentStackFraction(AnubisSoulHarvest.SoulKey);
            int gold0 = game.inventoryManager.Gold;
            foreach (Unit ally in allies) ally.ModifyHp(Mathf.Max(1, ally.HpMax / 2));
            int healBefore = shi.HpCurr, manaBefore = shi.ManaCurr;
            int defeats = 0, breaks = 0, realDeaths = 0;
            void Defeat(CombatDefeatContext c) { if (c.Grantor == enemy) defeats++; }
            void Break(CombatDefeatContext c) { if (c.Grantor == enemy) breaks++; }
            void Death(Unit dead, Unit attacker) { if (dead == enemy) realDeaths++; }
            Toughness.EnemyDefeated += Defeat; Toughness.Broken += Break; Unit.AnyUnitDied += Death;
            try
            {
                enemy.TakeDamage(Attack(shi)); enemy.TakeDamage(Attack(shi));
                Check("격파 처치1회·실제 사망 없음", breaks == 1 && defeats == 1 && realDeaths == 0 && enemy.isActive && enemy.HpCurr > 0);
                Check("격파 골드·영혼 불변", game.inventoryManager.Gold == gold0 &&
                    Mathf.Abs(souls0 - anubis.GetPermanentStacks(AnubisSoulHarvest.SoulKey) - anubis.GetPermanentStackFraction(AnubisSoulHarvest.SoulKey)) < .001f);
                Check("처치 공격력·속도 증가", Mathf.Abs(shi.StatValue(StatType.Attack) - atk0 * 1.1f) < .1f && Mathf.Abs(shi.StatValue(StatType.Speed) - speed0 * 1.05f) < .1f);
                Check("공물 실제 치유", shi.HpCurr > healBefore);
                Check("격파 처치 마나", shi.ManaCurr > manaBefore || manaBefore == shi.ManaMax);
                enemy.TakeDamage(Attack(shi));
                Check("이미 격파한 강인도 중복 발동 없음", breaks == 1);
                enemy.BeginTurn(); ActionDot.OnActionStarted(enemy, ActionScheduler.ActionKind.Normal);
                Check("다음 일반행동 시작 회복·제어 없음", enemy.ToughnessCurr == 150 && !enemy.isControlled);
                for (int n = 0; n < 6; n++)
                {
                    enemy.BeginTurn(); ActionDot.OnActionStarted(enemy, ActionScheduler.ActionKind.Normal);
                    for (int j = 0; j < 4; j++) enemy.TakeDamage(Attack(shi));
                }
                Check("처치 성장 전투 상한", Mathf.Abs(shi.StatValue(StatType.Attack) - atk0 * 1.4f) < .1f && Mathf.Abs(shi.StatValue(StatType.Speed) - speed0 * 1.2f) < .1f);
                Check("잔향산가 장착", orpheus.TryGrantRewardCode(731));
                orpheus.CastPassiveCode();
                enemy.RemoveShield(enemy.ShieldCurr); enemy.BeginTurn(); ActionDot.OnActionStarted(enemy, ActionScheduler.ActionKind.Normal);
                for (int j = 0; j < 3; j++) enemy.TakeDamage(Attack(shi));
                int hp0 = enemy.HpCurr, break0 = breaks;
                enemy.TakeDamage(Attack(shi));
                int echo = Mathf.RoundToInt(orpheus.StatValue(StatType.SpecialAttack) * .8f);
                Check("잔향산가 고정피해·재격파 없음", hp0 - enemy.HpCurr == echo + 1 && breaks == break0 + 1, (hp0 - enemy.HpCurr).ToString());
                enemy.BeginTurn(); ActionDot.OnActionStarted(enemy, ActionScheduler.ActionKind.Normal);
                for (int j = 0; j < 3; j++) enemy.TakeDamage(Attack(shi));
                Check("시 보스 강인도 처형각", enemy.HpCurr > ((ShiGhostSlash)shi.ActiveNormalCode).ExecutionEstimate(enemy) &&
                    ((ShiGhostSlash)shi.ActiveNormalCode).CanExecuteFromStats(enemy));
                int d0 = defeats, b0 = breaks;
                enemy.TakeDamage(Attack(shi, enemy.HpMax * 100));
                Check("체력·강인도 동시 종료 실처치1회", !enemy.isActive && defeats == d0 + 1 && breaks == b0 && realDeaths == 1);
            }
            finally { Toughness.EnemyDefeated -= Defeat; Toughness.Broken -= Break; Unit.AnyUnitDied -= Death; }

            var save = new UnitSaveData(); shi.WriteRunState(save);
            Check("신규 보상 코드 저장", new[] { 633, 717, 822 }.All(id => save.activeRewardCodeSlots.Contains(id)));
            shi.RestoreRunState(save);
            Check("신규 보상 코드 실제 복원", new[] { 633, 717, 822 }.All(id => shi.RewardCodeIds.Contains(id)));
            foreach (Unit ally in allies) ally.Invoke(UnitEventType.OnRoundEnd, new EventContext(ally));
            Check("라운드 종료 효율·중첩 정리", Mathf.Abs(Toughness.Efficiency(shi) - 1f) < .001f && Mathf.Abs(shi.StatValue(StatType.Attack) - atk0) < .1f);
            Check("부여자 정리 강인도 없음", enemies.All(u => u.ToughnessMax == 0));
            Check("자료실·중첩 툴팁 연결", Managers.UI.Core.GameGuideCatalog.Find("toughness") != null &&
                Managers.UI.Core.GameGuideCatalog.LinkTerms("강인도 삭감 효율").Contains("term:toughness"));
            yield return CaptureStill("toughness_core_complete");
            RuiFinish();
        }

        private IEnumerator ScenarioToughnessCombat(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();
            yield return BuildFreshRun("party5_toughness");
            yield return PrepareStage(config, Stage(config, 8), false, false, false);
            var game = GameManager.Instance;
            Unit shi = AliveAllies().First(u => u.ID == 2), orpheus = AliveAllies().First(u => u.ID == 25);
            foreach (int id in new[] { 633, 717, 822 }) shi.TryGrantRewardCode(id);
            orpheus.TryGrantRewardCode(731); orpheus.TryGrantRewardCode(823);
            int breaks = 0, reprises = 0, normals = 0;
            void Break(CombatDefeatContext _) => breaks++;
            void Slash(Unit target, ShiGhostSlash.SpecialSource source, bool killed) { if (source == ShiGhostSlash.SpecialSource.Reprise) reprises++; }
            void Action(Unit unit, ActionScheduler.ActionKind kind, string label) { if (unit == shi && kind == ActionScheduler.ActionKind.Normal) normals++; }
            Toughness.Broken += Break; ((ShiGhostSlash)shi.ActiveNormalCode).SlashResolved += Slash; ActionScheduler.AnyActionStarted += Action;
            try
            {
                game.StartRound();
                yield return WaitUnscaled(.35f);
                RuiCheck("실제 적 HUD 강인도", "아군 없음·적 강인도 표시", string.Join(",", AliveEnemies().Select(u => u.ToughnessCurr)),
                    AliveEnemies().Any(u => u.ToughnessMax > 0) && AliveAllies().All(u => u.ToughnessMax == 0));
                yield return CaptureStill("toughness_live_start");
                float until = Time.realtimeSinceStartup + 120f;
                bool shot = false;
                while (game.gameState == GameState.RoundInProgress && Time.realtimeSinceStartup < until)
                {
                    if (!shot && breaks > 0) { shot = true; yield return CaptureStill("toughness_live_first_break"); }
                    yield return null;
                }
                RuiCheck("실제 자연 전투 완료", "제한시간 내 정산", game.gameState.ToString(), game.gameState != GameState.RoundInProgress);
                RuiCheck("실제 격파·시 재행동 연계", "격파와 재행동 발생", $"격파{breaks}·재행동{reprises}·일반{normals}", breaks > 0 && reprises > 0);
                yield return CaptureStill("toughness_live_result");
            }
            finally
            {
                Toughness.Broken -= Break; ((ShiGhostSlash)shi.ActiveNormalCode).SlashResolved -= Slash; ActionScheduler.AnyActionStarted -= Action;
            }
            RuiFinish();
        }
    }
}
#endif

