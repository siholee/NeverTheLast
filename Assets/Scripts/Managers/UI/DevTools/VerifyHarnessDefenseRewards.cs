#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive.Reward;
using Combat;
using Core;
using Entities;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioDefenseRewardsCore(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); bool enabled = true;
            config.lineup = CampaignBuildCatalog().teams.Single(s => s.aliases.Contains("검증 편성/b48_dot_active")).key;
            yield return EnsureLineup(config); yield return YvPrepareScene(config, v => enabled = v);
            var party = AliveAllies().ToList(); var enemy = AliveEnemies().First();
            var moon = CfAlly(45); var varuna = CfAlly(66); var healer = CfAlly(167); var yama = CfAlly(61); var neith = CfAlly(166);
            foreach (var unit in party.Concat(AliveEnemies()))
            {
                foreach (var passive in unit.ActivePassiveCodes.ToList()) passive.StopCode();
                unit.ActiveUltimateCode?.StopCode(); CfNoCrit(unit);
                unit.ApplyRewardCodeInventory(Enumerable.Repeat(0, 7).ToList(), new List<int>());
                unit.RemoveShield(unit.ShieldCurr);
            }
            void Check(string label, bool ok, string actual = "") => YvCheck("방호 보상 " + label, "실제 공통 판정", actual, ok);
            void Equip(Unit unit, params int[] ids)
            {
                unit.ApplyRewardCodeInventory(ids.Concat(Enumerable.Repeat(0, 7 - ids.Length)).ToList(), new List<int>());
                foreach (int id in ids) YvCode(unit, id)?.CastCode();
            }
            void ClearShields() { foreach (var u in party) u.RemoveShield(u.ShieldCurr); }
            int Hit(Unit target, int raw = 100)
            {
                target.RestoreHpOutsideCombat(target.HpMax); target.RemoveShield(target.ShieldCurr);
                var damage = new DamageContext(enemy, raw, CodeType.Normal, new() { DamageTag.NormalAttack, DamageTag.NonContactAttack, DamageTag.Spell });
                target.TakeDamage(damage); return damage.ResolvedDamage;
            }
            DebugMode.AllyInvincible = DebugMode.EnemyInvincible = false;
            foreach (int id in new[] { 738, 739 })
            {
                Equip(neith, id);
                Check($"{id} T3·팩토리·표시", RewardCodeCatalog.TierOf(id) == 3 && YvCode(neith, id)?.CatalogId == id && !string.IsNullOrEmpty(YvCode(neith, id)?.CodeName));
            }
            Equip(neith);
            var pool = RewardManager.Instance.BuildCodePool();
            var stock = (List<RewardDef>)typeof(Managers.UI.Screens.ShopScreen).GetMethod("BuildCodeStock",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, null);
            Check("두 코드 실제 보상·상점300", new[] { 738, 739 }.All(id => pool.Any(x => x.codeId == id) && stock.Any(x => x.codeId == id && x.goldCost == 300)));
            Equip(neith, 738, 739); var saved = new UnitSaveData { currentHP = neith.HpCurr, ultimateResource = neith.ManaCurr };
            neith.WriteRunState(saved); var instances = neith.OwnedRewardCodeInstances.Select(x => x.instanceId).ToList();
            Equip(neith); neith.RestoreRunState(JsonUtility.FromJson<UnitSaveData>(JsonUtility.ToJson(saved)));
            Check("저장·복원 기존 인스턴스 보존", neith.RewardCodeIds.SequenceEqual(new[] { 738, 739 }) && neith.OwnedRewardCodeInstances.Select(x => x.instanceId).SequenceEqual(instances));
            Equip(neith);
            int baseline = Hit(yama);
            foreach (ActionDotKind kind in Enum.GetValues(typeof(ActionDotKind)))
            {
                enemy.RemoveAllNegativeStatuses(); Equip(neith, 738);
                ActionDot.Apply(yama, enemy, kind, inheritedHit: true);
                int reduced = Hit(yama); Equip(varuna, 738); int duplicate = Hit(yama);
                enemy.RemoveAllNegativeStatuses(); int cleaned = Hit(yama);
                Check($"운명의 재 {kind}20%·중복 없음·정화 즉시", Math.Abs(reduced - baseline * .8f) <= 1 && duplicate == reduced && cleaned == baseline,
                    $"{baseline}/{reduced}/{duplicate}/{cleaned}");
                Equip(varuna);
            }
            // 다른 비제어 디버프만 있거나 비밀 표식만 있으면 조건을 충족하지 않는다.
            YvAddTestStatus(enemy, 99893, "defense_test_non_dot", new Effects.Buffs.StatModifierEffect(BaseEnums.StatType.Defense, -.2f, true), BaseEnums.StatusCategory.Negative);
            Check("일반 디버프는 지속피해 조건 아님", Hit(yama) == baseline);
            enemy.RemoveAllNegativeStatuses(); ActionDot.Apply(yama, enemy, ActionDotKind.Shock, inheritedHit: true);
            Equip(varuna, 651); int mixedAura = Hit(yama);
            Check("하이볼티지와 각1회 곱연산", Math.Abs(mixedAura - baseline * .8f * .85f) <= 1);
            Equip(varuna); YvCode(neith, 738).StopCode();
            Check("중단 직후 피해 복원", Hit(yama) == baseline);
            Equip(neith); enemy.RemoveAllNegativeStatuses();
            ClearShields(); Equip(yama, 739); yama.AddShield(9000, moon);
            int remaining = yama.ShieldCurr; yama.Invoke(UnitEventType.OnPassiveActivates, new EventContext(yama));
            Check("생성 능력 없는 보유자·받은 보호막 복제 금지", ShieldCapability.StrongestBaseAmount(yama) == 0 && yama.ShieldCurr == remaining && party.Where(u => u != yama).All(u => u.ShieldCurr == 0));
            ClearShields(); Equip(yama); Equip(healer, 736, 739);
            int rewardShield = Mathf.RoundToInt(healer.StatValue(StatType.Defense) * 1.5f);
            healer.Invoke(UnitEventType.OnPassiveActivates, new EventContext(healer));
            Check("활성 보상 코드의 자기 보호막도 전원 복제", party.All(u => u.ShieldCurr == rewardShield), rewardShield.ToString());
            healer.Invoke(UnitEventType.OnPassiveActivates, new EventContext(healer));
            Check("개전 알림 재발행 중복 방지", party.All(u => u.ShieldCurr == rewardShield));
            Equip(healer, 739); healer.ApplyRewardCodeInventory(healer.RewardCodeSlots.ToList(), new List<int> { 736 });
            Check("보관 보호막 능력 제외", ShieldCapability.StrongestBaseAmount(healer) == 0);
            Equip(healer); ClearShields(); Equip(moon, 739, 624); Equip(yama, 625);
            int strongest = Mathf.RoundToInt(moon.StatValue(StatType.Defense) * Codes.Ultimate.TsukuyomiMoonlitPact.PartyShieldRatio);
            using (var recorder = new YvRecorder())
            {
                moon.Invoke(UnitEventType.OnPassiveActivates, new EventContext(moon));
                Check("최강 고유 초월기1개 선택·원 부여 합산 금지", ShieldCapability.StrongestBaseAmount(moon) == strongest &&
                    varuna.ShieldCurr == Mathf.RoundToInt(strongest * 1.2f));
                Check("공여·수신 보정 각각1회", yama.ShieldCurr == Mathf.RoundToInt(strongest * 1.2f * 1.2f), yama.ShieldCurr.ToString());
                Check("원 시전자·공통 부여 대상별1회", recorder.Shields.Count(s => s.Source == moon) == party.Count);
                int count = recorder.Shields.Count; YvCode(moon, 739).StopCode();
                moon.Invoke(UnitEventType.OnPassiveActivates, new EventContext(moon));
                Check("코드 제거 후 개전 구독 정리", recorder.Shields.Count == count);
            }
            Equip(yama); Equip(moon, 739); ClearShields();
            var beforeResources = (moon.ManaCurr, GameManager.Instance.inventoryManager.Gold);
            moon.Invoke(UnitEventType.OnPassiveActivates, new EventContext(moon));
            Check("시전·마나·재화·생명 소비 없음", beforeResources == (moon.ManaCurr, GameManager.Instance.inventoryManager.Gold));
            Equip(neith, 738); ActionDot.Apply(yama, enemy, ActionDotKind.Poison, inheritedHit: true);
            Hit(neith, 10000000);
            Check("보유자 실제 사망 즉시 오라 해제", !neith.IsOnField && Hit(yama) == baseline);
            DebugMode.AllyInvincible = false; OsFinish(enabled);
        }
    }
}
#endif
