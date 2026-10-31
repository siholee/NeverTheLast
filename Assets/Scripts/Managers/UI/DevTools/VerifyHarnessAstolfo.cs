#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BaseClasses;
using Codes.Base;
using Codes.Normal;
using Codes.Passive;
using Codes.Ultimate;
using Combat;
using Core;
using Effects.Buffs;
using Entities;
using UnityEngine;
using Kind = Managers.ActionScheduler.ActionKind;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioAstolfoCore(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); _cfActionIdSequence = 0;
            bool enabled = true;
            yield return YvPrepareScene(config, value => enabled = value);
            var game = GameManager.Instance;
            DebugMode.AllyInvincible = DebugMode.EnemyInvincible = false;
            Unit astolfo = CfAlly(162), jean = CfAlly(7), enemy = AliveEnemies().First();
            var passive = astolfo.ActivePassiveCodes.OfType<AstolfoTravelKit>().Single();
            void Check(string label, bool ok, string actual = "") => YvCheck("아스톨포 " + label, "기획 사양", actual, ok);
            Check("데이터·팩토리", astolfo.Element == "Anemo" && astolfo.ActiveNormalCode is AstolfoProvokingHorn && astolfo.ActiveUltimateCode is AstolfoReturnGift);
            foreach (Unit unit in AliveAllies().Concat(AliveEnemies())) { CfNoCrit(unit); unit.ClearUltimateResource(); }
            CfEnsureEffectHit(astolfo, AliveEnemies());
            int priority = astolfo.Priority;
            passive.StopCode();
            Check("우선도 -1·정리", astolfo.Priority == priority + 1);
            passive.CastCode(); passive.CastCode();
            Check("우선도 재시전 중복 없음", astolfo.Priority == priority);
            using (var log = new YvRecorder())
            {
                yield return NordAction(astolfo, Kind.Normal, scheduler => scheduler.AdvanceAction(astolfo, 1));
                Check("일반행동 해결·피해 없음", !log.Dealt.Any(hit => hit.Attacker == astolfo) && astolfo.ManaCurr >= Unit.ManaPerNormalAction);
                Check("광역 피해감소 부여", AliveEnemies().All(unit => unit.HasStatusKey("astolfo_provocation")));
            }
            var normalStatus = enemy.GetAllStatuses().Single(status => status.Key == "astolfo_provocation");
            Check("피해감소 25%·제어 아님", !normalStatus.IsControl && Near(normalStatus.Effects[0].EffectObject.OutgoingDamageModifier(enemy, jean, null), .75f));
            enemy.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved, new EventContext(enemy));
            Check("다음 일반행동 완료 만료", !enemy.HasStatusKey("astolfo_provocation"));
            var scheduler = game.ActionScheduler;
            scheduler.BeginRound();
            float before = scheduler.ActionProgress(enemy);
            Check("명중한 적 AV20% 선행", AstolfoProvokingHorn.Apply(astolfo, enemy) && Near(scheduler.ActionProgress(enemy) - before, .20f));
            scheduler.EndRound();
            var good = BuffStatus.Create(99871, "astolfo_test_good", "검증 강화", enemy, enemy,
                new StatModifierEffect(BaseEnums.StatType.Attack, .2f, true), isBeneficial: true);
            enemy.AddStatus(good);
            int pressureStage = game.RoundManager.Stage;
            typeof(RoundManager).GetProperty("Stage").SetValue(game.RoundManager, 25);
            var finalDamage = typeof(Unit).GetMethod("CalculateFinalDamage", BindingFlags.Instance | BindingFlags.NonPublic);
            var probeDamage = new DamageContext(enemy, 10000, BaseEnums.CodeType.Normal, new List<int> { DamageTag.TrueDamage });
            int beforePressure = (int)finalDamage.Invoke(jean, new object[] { probeDamage });
            enemy.RemoveStatusByKey(YvHpBoostKey);
            int baseHp = enemy.HpMax; float baseSpeed = enemy.SpeedCurr;
            int debuffs = enemy.GetAllStatuses().Count(status => status.CountsAsDebuff);
            float hpRatio = enemy.HpCurr / (float)enemy.HpMax;
            EnemyCombatBudget.Apply(enemy);
            Check("일반 체력·속도 예산과 HP비율", Mathf.Abs(enemy.HpMax - baseHp * EnemyCombatBudget.NormalHpMultiplier) <= 1 &&
                Near(enemy.SpeedCurr, baseSpeed * EnemyCombatBudget.SpeedMultiplier(StageKind.Normal, 25)) &&
                Mathf.Abs(enemy.HpCurr / (float)enemy.HpMax - hpRatio) < .01f);
            EnemyCombatBudget.Apply(enemy);
            Check("기본 예산 재적용 중복·디버프 증가 없음", enemy.GetAllStatuses().Count(status => status.Key == EnemyCombatBudget.StatusKey) == 1 &&
                enemy.GetAllStatuses().Count(status => status.CountsAsDebuff) == debuffs);
            var pressure = enemy.GetAllStatuses().Single(status => status.Key == EnemyCombatBudget.StatusKey);
            Check("일반층 기본 화력은 강화·디버프 아님", pressure.Category == BaseEnums.StatusCategory.Neutral && !pressure.IsBeneficial && !pressure.CountsAsDebuff);
            int afterPressure = (int)finalDamage.Invoke(jean, new object[] { probeDamage });
            Check("기본 화력 실제 피해 경로 반영", Mathf.Abs(afterPressure - beforePressure * EnemyCombatBudget.DamageMultiplier(StageKind.Normal, 25)) <= 1);
            float defense = enemy.StatValue(BaseEnums.StatType.Defense);
            Check("강화와 기본 화력 유지", enemy.HasStatusKey(good.Key) && enemy.HasStatusKey(EnemyCombatBudget.StatusKey) &&
                (int)finalDamage.Invoke(jean, new object[] { probeDamage }) == afterPressure);
            enemy.RemoveStatusByKey(EnemyCombatBudget.StatusKey);
            Check("기본 예산 제거 HP·속도 복원", enemy.HpMax == baseHp && Near(enemy.SpeedCurr, baseSpeed));
            YvBoostEnemy(enemy);
            typeof(RoundManager).GetProperty("Stage").SetValue(game.RoundManager, pressureStage);
            enemy.RemoveStatusByKey("astolfo_disarm");
            enemy.RemoveStatusByKey("astolfo_provocation");
            YvAddTestStatus(enemy, 99872, "astolfo_test_resist", new YvResistAllEffect());
            enemy.AddStatus(good);
            scheduler.BeginRound(); before = scheduler.ActionProgress(enemy);
            Check("저항 시 가속 없음", !AstolfoProvokingHorn.Apply(astolfo, enemy) &&
                Near(scheduler.ActionProgress(enemy), before) && enemy.HasStatusKey(good.Key));
            scheduler.EndRound(); enemy.RemoveStatusByKey("astolfo_test_resist");
            foreach (var ally in AliveAllies()) ally.ClearUltimateResource();
            foreach (var ally in AliveAllies()) ally.ModifyHp(ally.HpMax / 2);
            var hpBeforeHeal = AliveAllies().ToDictionary(u => u, u => u.HpCurr);
            yield return NordAction(astolfo, Kind.Ultimate, _ => astolfo.FillUltimateResource());
            Check("초월기 전원 반격 버프", AliveAllies().All(unit => unit.HasStatusKey(AstolfoReturnGift.BuffKey)));
            Check("초월기 전원 공격력 기반 치유", AliveAllies().All(unit => unit.HpCurr > hpBeforeHeal[unit]) && AstolfoReturnGift.HealAttackRatio == 6f);
            Check("초월기 적 강화·방어력 보존", enemy.HasStatusKey(good.Key) && !enemy.HasStatusKey("astolfo_disarm") && Near(enemy.StatValue(BaseEnums.StatType.Defense), defense));
            var bonus = jean.CombatEffects().OfType<AstolfoReturnGift.CounterCritEffect>().Single();
            float additionalShare = astolfo.StatValue(BaseEnums.StatType.CritDamage) * AstolfoReturnGift.AdditionalCritShare;
            float counterShare = astolfo.StatValue(BaseEnums.StatType.CritDamage) * AstolfoReturnGift.CounterExtraShare;
            float share = additionalShare + counterShare;
            Check("추가행동 기본·반격 추가·치피 스냅샷", Near(bonus.TaggedCritDamageBonus(jean, new[] { DamageTag.CounterAttack }), share) &&
                Near(bonus.TaggedCritDamageBonus(jean, new[] { DamageTag.AdditionalAttack }), additionalShare) && bonus.TaggedCritDamageBonus(jean, null) == 0);
            YvAddTestStatus(jean, 99873, "astolfo_test_crit", new StatModifierEffect(BaseEnums.StatType.CritRate, 2f));
            var critical = typeof(Code).GetMethod("RollCriticalFor", BindingFlags.NonPublic | BindingFlags.Static);
            float Crit(params int[] tags)
            {
                object[] args = { jean, jean, enemy, BaseEnums.CodeType.Normal, 0f, tags };
                critical.Invoke(null, args); return (float)args[4];
            }
            Check("실제 치명타 입구 차등 가산", Near(Crit(DamageTag.CounterAttack) - Crit(DamageTag.AdditionalAttack), counterShare) &&
                Near(Crit(DamageTag.AdditionalAttack) - Crit(DamageTag.NormalAttack), additionalShare));
            using (var log = new YvRecorder())
            {
                yield return ((JeanHolyStrike)jean.ActiveNormalCode).CastCounter(enemy);
                int expected = Mathf.RoundToInt(Mathf.RoundToInt(jean.AtkCurr * JeanHolyStrike.CounterRatio) * Crit(DamageTag.CounterAttack));
                Check("잔 실제 반격의 치명 피해", log.Dealt.Any(hit => hit.Attacker == jean && hit.DamageContext.HasTag(DamageTag.CounterAttack) && hit.DamageContext.IsCrit && hit.DamageContext.Damage == expected));
            }
            Unit bastet = CfAlly(122);
            YvAddTestStatus(bastet, 99874, "astolfo_bastet_crit", new StatModifierEffect(BaseEnums.StatType.CritRate, 2f));
            using (var log = new YvRecorder())
            {
                yield return NordAction(bastet, Kind.PriorityAdditional, s => s.EnqueuePriorityAdditional(bastet,
                    "astolfo_bastet_probe", "추가행동 치피 검증", () => ((BastetCatPunch)bastet.ActiveNormalCode).CastAdditional(enemy)));
                int expected = Mathf.RoundToInt(Mathf.RoundToInt(bastet.AtkCurr * BastetCatPunch.FollowUpRatio) *
                    (1f + bastet.StatValue(BaseEnums.StatType.CritDamage) + additionalShare));
                Check("바스테트 실제 후속에는 기본 치피만", log.Dealt.Any(hit => hit.Attacker == bastet && hit.DamageContext.IsCrit &&
                    hit.DamageContext.HasTag(DamageTag.AdditionalAttack) && !hit.DamageContext.HasTag(DamageTag.CounterAttack) && hit.DamageContext.Damage == expected));
            }
            jean.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved, new EventContext(jean));
            Check("반격 버프 첫 일반 완료 유지", jean.HasStatusKey(AstolfoReturnGift.BuffKey));
            jean.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved, new EventContext(jean));
            Check("반격 버프 두 번째 일반 완료 만료", !jean.HasStatusKey(AstolfoReturnGift.BuffKey));
            int kept = 0;
            var applyTonic = typeof(GameManager).GetMethod("ApplyPendingTonic", BindingFlags.Instance | BindingFlags.NonPublic);
            UnityEngine.Random.InitState(16225);
            for (int i = 0; i < 1000; i++)
            {
                game.gameState = BaseEnums.GameState.Preparation;
                game.inventoryManager.AddItem(5101);
                int count = game.inventoryManager.CountItem(5101);
                bool used = game.TryUseTonic(5101, out _);
                bool duplicate = game.TryUseTonic(5101, out _);
                applyTonic.Invoke(game, null);
                int left = game.inventoryManager.CountItem(5101);
                if (!used || duplicate || game.PendingTonicItemId != 0 || (left != count && left != count - 1))
                { Check(" 비약 적용·중복 잠금·수량", false); break; }
                if (left == count) kept++;
                while (game.inventoryManager.CountItem(5101) > 0) game.inventoryManager.RemoveItem(5101);
            }
            Check("실제 비약 1000회 보존25%", kept >= 200 && kept <= 300, kept.ToString());
            passive.StopCode();
            Check("특성 정리 후 보존 없음", !Enumerable.Range(0, 100).Any(_ => AstolfoTravelKit.TryPreserveBattleConsumable()));
            passive.CastCode();
            foreach (var kind in new[] { StageKind.Elite, StageKind.MidBoss, StageKind.Boss })
                Check("엘리트·보스 전투 예산 " + kind, EnemyCombatBudget.DamageMultiplier(kind, 48) > 1 &&
                    Near(EnemyCombatBudget.HpMultiplier(kind,48),kind==StageKind.Elite?1.15f:1.4f) && EnemyCombatBudget.SpeedMultiplier(kind, 48) > 1);
            Check("사건 예산 제외", Near(EnemyCombatBudget.DamageMultiplier(StageKind.Event, 72), 1) &&
                Near(EnemyCombatBudget.SpeedMultiplier(StageKind.Event, 72), 1) && Near(EnemyCombatBudget.HpMultiplier(StageKind.Event,72), 1));
            Check("초반 기본 압박·16층 이후 점증", EnemyCombatBudget.DamageMultiplier(StageKind.Normal, 1) > 1 &&
                EnemyCombatBudget.DamageMultiplier(StageKind.Normal, 65) > EnemyCombatBudget.DamageMultiplier(StageKind.Normal, 16));
            game.gameState = BaseEnums.GameState.RoundInProgress;
            int spawnStage = game.RoundManager.Stage;
            typeof(RoundManager).GetProperty("Stage").SetValue(game.RoundManager, 25);
            Unit reinforcement = GridManager.Instance.SpawnUnit(2, 4, true, 1001, playEntry: false);
            GridManager.Instance.BeginEnemyCombatLife(reinforcement);
            Check("전투 중 적 증원에도 기본 예산", reinforcement != null && reinforcement.HasStatusKey(EnemyCombatBudget.StatusKey));
            if (reinforcement != null) reinforcement.TakeDamage(new DamageContext(astolfo, 1000000, BaseEnums.CodeType.Effect, new List<int> { DamageTag.TrueDamage }));
            typeof(RoundManager).GetProperty("Stage").SetValue(game.RoundManager, spawnStage);
            OsFinish(enabled);
        }
    }
}
#endif
