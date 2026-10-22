#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Codes.Passive.Reward;
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
        private IEnumerator ScenarioSecretRewardsCore(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); _cfActionIdSequence = 0;
            bool enabled = true;
            yield return YvPrepareScene(config, value => enabled = value);
            DebugMode.AllyInvincible = true; DebugMode.EnemyInvincible = false;
            var game = GameManager.Instance;
            var party = AliveAllies();
            Unit a = CfAlly(164), b = CfAlly(165), c = CfAlly(166), healer = CfAlly(167), astolfo = CfAlly(162);
            Unit enemy = AliveEnemies().First();
            foreach (Unit unit in party.Concat(AliveEnemies()))
            {
                foreach (var code in unit.ActivePassiveCodes.ToList()) code.StopCode();
                unit.ActiveUltimateCode?.StopCode(); CfNoCrit(unit);
                unit.ApplyRewardCodeInventory(Enumerable.Repeat(0, 7).ToList(), new List<int>());
            }
            foreach (Unit target in AliveEnemies())
            {
                target.RemoveStatusByKey(YvHpBoostKey);
                YvAddTestStatus(target, YvHpBoostId, YvHpBoostKey,
                    new StatModifierEffect(BaseEnums.StatType.MaxHp, 200000f), BaseEnums.StatusCategory.Neutral);
                target.RestoreHpOutsideCombat(target.HpMax);
            }
            void Check(string label, bool passed, string actual = "") => YvCheck("비밀·지원 보상 " + label, "설계 사양", actual, passed);
            void Equip(Unit holder, params int[] ids)
            {
                holder.ApplyRewardCodeInventory(ids.Concat(Enumerable.Repeat(0, 7 - ids.Length)).ToList(), new List<int>());
                foreach (int id in ids) YvCode(holder, id)?.CastCode();
            }
            int Dot(Unit source, Unit target, int raw = 1000, DotTriggerKind trigger = DotTriggerKind.Natural, bool shared = false)
            {
                var ctx = new DamageContext(source, raw, BaseEnums.CodeType.Effect, new List<int>())
                { IsDamageOverTime = true, DotKind = ActionDotKind.Poison, DotTrigger = trigger, DotRootId = ActionDot.NextRootId(), IsSharedDamage = shared };
                target.TakeDamage(ctx); return ctx.ResolvedDamage;
            }
            int Direct(Unit source, Unit target, int raw = 1000)
            {
                var ctx = new DamageContext(source, raw, BaseEnums.CodeType.Normal,
                    new List<int> { DamageTag.NormalAttack, DamageTag.NonContactAttack, DamageTag.Spell });
                target.TakeDamage(ctx); return ctx.ResolvedDamage;
            }
            void ClearDots(Unit target) => target.RemoveAllNegativeStatuses();
            int Tier(int id) => id >= 900 ? 5 : id >= 800 ? 4 : id >= 700 ? 3 : 2;
            foreach (int id in SecretSupportRewardCode.Ids)
            {
                Equip(a, id);
                Check($"{id} YAML·티어·실제 팩토리", RewardCodeCatalog.TierOf(id) == Tier(id) &&
                    YvCode(a, id) is SecretSupportRewardCode && YvCode(a, id).CatalogId == id);
            }
            Equip(a);
            var pool = RewardManager.Instance.BuildCodePool();
            Check("비밀·지원 보상 무작위 후보 연결", SecretSupportRewardCode.Ids.All(id => pool.Any(e => e.codeId == id)));
            Check("장례식 상위 사다리", RewardCodeCatalog.IsUpperOf(824, 732));
            var stock = (List<RewardDef>)typeof(Managers.UI.Screens.ShopScreen).GetMethod("BuildCodeStock",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, null);
            Check("제세동기 실제 상점 재고150", stock.Any(x => x.codeId == 638 && x.goldCost == 150));
            a.TryGrantRewardCode(732); string original = a.ActiveRewardInstance(732).instanceId;
            bool upgrade = a.TryGrantRewardCode(824);
            Check("상위 획득·하위 보관·역순 거부", upgrade && a.RewardCodeIds.Contains(824) &&
                !a.RewardCodeIds.Contains(732) && a.StoredRewardCodeIds.Contains(732) && !a.TryGrantRewardCode(732));
            Equip(a, 636, 637, 638, 639, 648, 649, 650);
            a.TryGrantRewardCode(651);
            Check("활성7·8번째 보관", a.RewardCodeIds.Count == 7 && a.StoredRewardCodeIds.Contains(651) && YvCode(a, 651) == null);
            var saved = new UnitSaveData { currentHP = a.HpCurr, ultimateResource = a.ManaCurr };
            a.WriteRunState(saved); string instance = a.ActiveRewardInstance(638).instanceId;
            Equip(a); a.RestoreRunState(JsonUtility.FromJson<UnitSaveData>(JsonUtility.ToJson(saved)));
            Check("신규 코드 JSON·인스턴스 복원", a.ActiveRewardInstance(638)?.instanceId == instance && a.StoredRewardCodeIds.Contains(651));
            Equip(a, 732);
            ClearDots(enemy);
            ActionDot.Apply(a, enemy, ActionDotKind.Poison, inheritedHit: true);
            Check("장례식 실제 부여 +1", SecretRewardRules.Stacks(enemy) == 1);
            ActionDot.Apply(a, enemy, ActionDotKind.Poison, inheritedHit: true);
            Check("성공한 기간 갱신 +1", SecretRewardRules.Stacks(enemy) == 2);
            Equip(b, 824); Equip(c, 824);
            ActionDot.Apply(a, enemy, ActionDotKind.Bleed, inheritedHit: true);
            Check("상·하위·복수 보유 최고3만", SecretRewardRules.Stacks(enemy) == 5);
            enemy.RemoveAllNegativeStatuses(astolfo);
            YvDispelBeneficial(enemy);
            Check("정화·강화해제로 비밀 보존·중립", SecretRewardRules.Stacks(enemy) == 5 &&
                !enemy.GetAllStatuses().Single(s => s.Key == SecretRewardRules.MarkKey).CountsAsDebuff);
            YvAddTestStatus(enemy, 99871, "secret_resist", new YvResistAllEffect());
            bool rejected = !ActionDot.Apply(a, enemy, ActionDotKind.Poison);
            enemy.RemoveStatusByKey("secret_resist");
            Check("실패 부여는 비밀 없음", rejected && SecretRewardRules.Stacks(enemy) == 5);
            Equip(c, 825);
            Dot(a, enemy); Dot(a, enemy, trigger: DotTriggerKind.Immediate); Dot(a, enemy, trigger: DotTriggerKind.Derived);
            Check("피해망상 자연·즉시·파열 각1", SecretRewardRules.Stacks(enemy) == 8);
            Dot(a, enemy, 0); Dot(a, enemy, shared: true); Direct(a, enemy);
            Check("0피해·공유피해·직접피해 제외", SecretRewardRules.Stacks(enemy) == 8);
            Equip(c); int baseline = Dot(a, enemy);
            Equip(healer, 910); int amplified = Dot(a, enemy);
            Equip(astolfo, 910); int twice = Dot(a, enemy, trigger: DotTriggerKind.Immediate);
            Check("발푸르기스 현재8중첩16%·동일오라 비중첩", Math.Abs(amplified - baseline * 1.16f) <= 2 && twice == amplified, $"{baseline}/{amplified}/{twice}");
            Equip(c, 733); float attack = enemy.AtkCurr;
            YvCode(c, 733).StopCode(); float fullAttack = enemy.AtkCurr;
            Check("의심암귀8중첩 공격4.8%·중단 즉시 복원", Near(attack, fullAttack * .952f, 1));
            YvCode(c, 733).CastCode();
            for (int i = 0; i < 20; i++) ActionDot.Apply(a, enemy, ActionDotKind.Poison, inheritedHit: true);
            Check("비밀50상한·공격30%감소", SecretRewardRules.Stacks(enemy) == 50 && Near(enemy.AtkCurr, fullAttack * .7f, 1));
            float hpRatio = (float)enemy.HpCurr / enemy.HpMax;
            Equip(a); Equip(b); Equip(c); Equip(healer); Equip(astolfo);
            Check("마지막 구독·표식·스탯 정리·HP비율", SecretRewardRules.Stacks(enemy) == 0 && Near(enemy.AtkCurr, fullAttack) && Near((float)enemy.HpCurr / enemy.HpMax, hpRatio));
            ClearDots(enemy);
            int noIgnore = Dot(a, enemy), directBase = Direct(a, enemy);
            Equip(a, 636); int ignore = Dot(a, enemy), directAfter = Direct(a, enemy);
            Check("막말 특방무시·직접피해 제외", ignore > noIgnore && directAfter == directBase, $"DoT {noIgnore}→{ignore}, direct {directBase}→{directAfter}");
            int foreign = Dot(b, enemy); Equip(a);
            Check("막말 타인 원본 제외", foreign == Dot(b, enemy));
            Equip(a, 637);
            foreach (ActionDotKind kind in Enum.GetValues(typeof(ActionDotKind)))
            {
                ClearDots(enemy); ActionDot.Apply(a, enemy, kind, inheritedHit: true);
                var status = enemy.GetAllStatuses().Single(s => SecretRewardRules.IsDot(s));
                bool duration = status.Duration == 3;
                ActionDot.Apply(a, enemy, kind, inheritedHit: true);
                Check($"악몽 {kind} 2→3·갱신 비누적", duration && enemy.GetAllStatuses().Single(s => SecretRewardRules.IsDot(s)).Duration == 3);
            }
            Equip(a, 637, 811); ClearDots(enemy); ActionDot.Apply(a, enemy, ActionDotKind.Poison, inheritedHit: true);
            Check("악몽+죽음의 계약 기간4", enemy.GetAllStatuses().Single(s => SecretRewardRules.IsDot(s)).Duration == 4);
            Equip(a); Equip(c, 639); c.ClearUltimateResource();
            for (int i = 0; i < 8; i++) Dot(a, enemy);
            Check("고통 에너지2×5·일반차례 예산", c.ManaCurr == 10, c.ManaCurr.ToString());
            c.BeginTurn(); Dot(a, enemy);
            Check("고통 소유자 다음차례 충전 재개", c.ManaCurr == 12);
            c.ClearUltimateResource(); Direct(a, enemy); Dot(a, enemy, shared: true); Dot(a, enemy, 0);
            Check("고통 직접·공유·0피해 제외", c.ManaCurr == 0);
            Equip(c); ClearDots(enemy);
            DebugMode.AllyInvincible = false;
            a.RestoreHpOutsideCombat(a.HpMax); int normalHit = Direct(enemy, a, 100);
            a.RestoreHpOutsideCombat(a.HpMax); Equip(a, 735); int fullHit = Direct(enemy, a, 100);
            a.ModifyHp(a.HpMax / 2); int partialHit = Direct(enemy, a, 100);
            Check("포만감 fullHP20%·부상시0", Math.Abs(fullHit - normalHit * .8f) <= 1 && partialHit == normalHit, $"{normalHit}/{fullHit}/{partialHit}");
            Equip(a, 737); CfEnsureEffectHit(a, new[] { enemy }); float sdef = enemy.SpDefCurr;
            Direct(enemy, a, 1);
            Check("배짱 피격 공격자 특방15% 2턴", Near(enemy.SpDefCurr, sdef * .85f, 1) && enemy.GetAllStatuses().Single(s => s.Key == "reward_nerve").Duration == 2);
            Direct(enemy, a, 1);
            Check("배짱 중첩 없음", Near(enemy.SpDefCurr, sdef * .85f, 1));
            enemy.RemoveAllNegativeStatuses(); Dot(enemy, a, 10);
            Check("배짱 지속피해는 제외", !enemy.HasStatusKey("reward_nerve"));
            Equip(a); a.RestoreHpOutsideCombat(a.HpMax);
            int voltageBase = Direct(enemy, a, 100);
            Equip(c, 651); ActionDot.Apply(b, enemy, ActionDotKind.Shock, inheritedHit: true);
            int voltage = Direct(enemy, a, 100); Equip(b, 651); int voltageDouble = Direct(enemy, a, 100);
            enemy.RemoveAllNegativeStatuses(); int cleanVoltage = Direct(enemy, a, 100);
            Check("하이볼티지 감전15%·비중첩·정화즉시복원", Math.Abs(voltage - voltageBase * .85f) <= 1 && voltage == voltageDouble && cleanVoltage == voltageBase, $"{voltageBase}/{voltage}/{voltageDouble}/{cleanVoltage}");
            Equip(b); Equip(c); DebugMode.AllyInvincible = true;
            float atkBefore = a.AtkCurr, speedBefore = healer.SpeedCurr;
            Equip(healer, 734, 649, 736); a.ModifyHp(a.HpMax / 4);
            int shieldBefore = healer.ShieldCurr;
            healer.Heal(1, healer); a.Heal(100, healer); b.Heal(100, healer);
            Check("천사 치유받은 아군 공격15%", Near(a.AtkCurr, atkBefore * 1.15f, 1));
            Check("찬가 소유자 속도10%", Near(healer.SpeedCurr, speedBefore * 1.1f, 1));
            Check("헌신 광역·다중치유 동일차례1회", healer.ShieldCurr - shieldBefore == Mathf.RoundToInt(healer.StatValue(BaseEnums.StatType.Defense) * 1.5f));
            a.Heal(100, healer);
            Check("천사 반복치유 비누적", Near(a.AtkCurr, atkBefore * 1.15f, 1));
            a.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved, new EventContext(a));
            bool stays = a.HasStatusKey("reward_lantern");
            a.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved, new EventContext(a));
            Check("천사 대상 일반완료2회 수명", stays && !a.HasStatusKey("reward_lantern"));
            Equip(c, 734); a.Heal(1, healer); a.Heal(1, c); Equip(healer);
            Check("다른 치유자 최신버프 정지 안전", a.HasStatusKey("reward_lantern") && Near(a.AtkCurr, atkBefore * 1.15f, 1));
            Equip(c); Equip(healer, 648, 650); a.ModifyHp(a.HpMax / 4); b.ModifyHp(b.HpMax / 4); healer.ModifyHp(healer.HpMax / 4);
            int echo = Mathf.Max(1, Mathf.RoundToInt(healer.HpMax * .01f));
            int beforeA = a.HpCurr, beforeB = b.HpCurr, beforeSelf = healer.HpCurr;
            yield return NordAction(healer, Kind.Special, s => s.EnqueueSpecial(healer, "secret_heal_probe", "치유 반복 검증", () =>
            { a.Heal(100, healer); a.Heal(100, healer); b.Heal(100, healer); }));
            Check("메아리 같은행동 대상별1회·광역보장", a.HpCurr - beforeA == 200 + echo && b.HpCurr - beforeB == 100 + echo, $"{a.HpCurr-beforeA}/{b.HpCurr-beforeB}, echo={echo}");
            Check("의료비 실제치유20%·메아리 재귀없음", healer.HpCurr - beforeSelf == 60, (healer.HpCurr-beforeSelf).ToString());
            // 동일 하네스는 매번 BeginRound로 행동ID를 재설정하므로 다음 실제 행동 검사 전에 코드도 초기화한다.
            YvCode(healer, 648).CastCode(); beforeA = a.HpCurr;
            yield return NordAction(healer, Kind.Additional, s => s.EnqueueAdditional(healer, "secret_heal_additional", "추가행동 치유 검증", () => { a.Heal(1, healer); a.Heal(1, healer); }));
            Check("추가행동도 대상별 독립 치유권", a.HpCurr - beforeA == 2 + echo);
            Equip(healer, 650); a.RestoreHpOutsideCombat(a.HpMax); beforeSelf = healer.HpCurr;
            a.Heal(100, healer);
            Check("의료비 과치유0 제외", healer.HpCurr == beforeSelf);
            healer.Heal(100, healer);
            Check("의료비 자기치유 재귀없음", healer.HpCurr - beforeSelf == 100);
            Equip(healer); ClearDots(enemy);
            Equip(b, 824); Equip(c, 733, 910);
            ActionDot.Apply(a, enemy, ActionDotKind.Poison, inheritedHit: true);
            float cutAttack = enemy.AtkCurr; int livingAuraHit = Dot(a, enemy);
            DebugMode.AllyInvincible = false; Direct(enemy, c, 10000000);
            int deadAuraHit = Dot(a, enemy);
            Check("오라 보유자 실제사망 즉시 공격·지딜배율 복원", !c.isActive && enemy.AtkCurr > cutAttack && Math.Abs(livingAuraHit - deadAuraHit * 1.06f) <= 2);
            Equip(b); ClearDots(enemy);
            DebugMode.AllyInvincible = false;
            Equip(a, 638);
            a.ApplyRewardCodeInventory(a.RewardCodeSlots.ToList(), new List<int> { 638 }); // 다른 아군에게서 옮긴 보관 복사본을 모사한다.
            string active = a.ActiveRewardInstance(638).instanceId;
            string reserve = a.StoredRewardCodeInstances.FirstOrDefault(x => x.codeId == 638)?.instanceId;
            int deaths = 0, heals = 0;
            void Died(Unit dead, Unit attacker) { if (dead == a) deaths++; }
            void Healed(Unit target, Unit source, int effective, int overflow) { if (target == a) heals++; }
            Unit.AnyUnitDied += Died; Unit.AnyHealed += Healed;
            try
            {
                Direct(enemy, a, 10000000);
                Check("제세동기30%·실제사망/치유없음", a.isActive && a.HpCurr == Mathf.RoundToInt(a.HpMax * .3f) && deaths == 0 && heals == 0, $"HP {a.HpCurr}/{a.HpMax},death={deaths},heal={heals}");
                Check("활성한장만 소비·보관복사본 보존", a.ActiveRewardInstance(638) == null && a.OwnedRewardCodeInstances.All(x => x.instanceId != active) &&
                    reserve != null && a.StoredRewardCodeInstances.Any(x => x.instanceId == reserve));
                saved = new UnitSaveData { currentHP = a.HpCurr, ultimateResource = a.ManaCurr }; a.WriteRunState(saved);
                a.RestoreRunState(JsonUtility.FromJson<UnitSaveData>(JsonUtility.ToJson(saved)));
                Check("소비 후 JSON복원 재생성없음", a.ActiveRewardInstance(638) == null && a.StoredRewardCodeInstances.Any(x => x.instanceId == reserve));
                var kit = astolfo.ActivePassiveCodes.OfType<AstolfoTravelKit>().Single(); kit.CastCode();
                UnityEngine.Random.InitState(6382026); int preserved = 0;
                for (int i = 0; i < 200; i++)
                {
                    Equip(a, 638); a.RestoreHpOutsideCombat(a.HpMax); Direct(enemy, a, 10000000);
                    if (a.ActiveRewardInstance(638) != null) preserved++;
                    if (i % 20 == 0) yield return null;
                }
                Check("아스톨포25% 보존 200회·사망없음", preserved >= 30 && preserved <= 70 && deaths == 0 && a.isActive, $"보존 {preserved}/200 · 사망 {deaths}");
                kit.StopCode(); Equip(a, 638); a.RestoreHpOutsideCombat(a.HpMax);
                typeof(GameManager).GetMethod("SaveAllyFieldState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(game, null);
                Direct(enemy, a, 10000000);
                Check("아스톨포 정지시 반드시소비", a.ActiveRewardInstance(638) == null);
            }
            finally { Unit.AnyUnitDied -= Died; Unit.AnyHealed -= Healed; }
            // 실제 런 저장·전장 재생성: 소비한 활성 코드는 돌아오지 않는다.
            typeof(GameManager).GetMethod("CaptureAllyBattleState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(game, null);
            game.ActionScheduler.EndRound(); GridManager.Instance.OnRoundEnd(); game.gameState = BaseEnums.GameState.Preparation;
            game.RestoreAllyFieldState(); a = CfAlly(164);
            Check("실제 전투후 필드스냅샷 복원 소비유지", a != null && !a.RewardCodeIds.Contains(638));
            game.runManager.SaveCurrentRun(); bool restored = game.runManager.LoadSavedRun();
            a = CfAlly(164);
            Check("실제 런 저장·전장 복원 소비유지", restored && a != null && !a.RewardCodeIds.Contains(638));
            DebugMode.AllyInvincible = false; OsFinish(enabled);
        }
    }
}
#endif
