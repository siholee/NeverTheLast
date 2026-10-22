#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Codes.Passive.Reward;
using Codes.Ultimate;
using Combat;
using Core;
using Effects.Base;
using Effects.Buffs;
using Effects.Negative;
using Entities;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private static float OpeningShieldCampaignValue(Unit unit)
        {
            if (unit == null || !unit.IsOnField) return 1;
            int amount = ShieldCapability.StrongestBaseAmount(unit);
            if (amount <= 0) return 1;
            int strongest = GridManager.Instance.heroList.Where(u => u != null && u.IsOnField)
                .Select(ShieldCapability.StrongestBaseAmount).DefaultIfEmpty(amount).Max();
            return 60f + 52f * amount / Mathf.Max(1, strongest);
        }
        private static float SingleRewardCampaignValue(Unit unit, int id)
        {
            bool single = unit.ID is 1 or 2 or 3 or 4 or 7 or 20 or 22 or 25 or 40 or 44 or 60 or 61 or 62 or 63 or 64 or 65 or 66 or 67 or 80 or 81 or 82 or 83 or 120 or 121 or 122 or 123 or 124 or 125 or 160 or 161 or 164 or 165 or 166 or 167;
            bool front = unit.currentCell?.xPos == GridManager.Instance.GetFrontColumn(false);
            // 실제 초월기 본체가 공격해야 처치 환급을 받을 수 있다. 소집 뒤의 Special이나
            // 지원 초월기가 강화한 후속 추공은 마나를 소비한 그 행동의 처치가 아니다.
            bool offensiveUltimate = unit.ActiveUltimateCode is { AttackForm: > 0, IsAutoCast: true, IsPersistent: false };
            bool mana = unit.UltimateResourceType == UltimateResourceType.Mana;
            bool hasShields = GridManager.Instance.heroList.Any(u => u != null && u.IsOnField && ShieldCapability.StrongestBaseAmount(u) > 0);
            bool duplicateAura = id is 661 or 758 && GridManager.Instance.heroList.Any(u => u != null && u != unit && u.IsOnField && u.RewardCodeIds.Contains(id));
            if (duplicateAura) return 1;
            return id switch
            {
                914 => single ? unit.ID == 2 ? 108 : 72 : 1,
                915 => mana && single ? unit.ID == 60 ? 95 : 86 : 1,
                916 => mana && offensiveUltimate ? 95 : 1,
                834 => unit.ID == 2 ? 98 : 82,
                835 => single ? 54 : 1,
                836 => unit.ID == 2 ? 92 : 67,
                758 => hasShields ? unit.ID == 60 ? 95 : unit.ID == 44 ? 92 : 76 : 1,
                759 => single ? unit.ID == 2 ? 102 : 83 : 1,
                760 => 60 + Mathf.Clamp01(unit.AtkCurr * 4f / Mathf.Max(1, unit.HpMax)) * 35 + (front ? 10 : 0),
                761 => unit.ID == 2 ? 96 : front ? 87 : 78,
                762 => unit.ID == 2 || unit.RewardCodeIds.Any(c => c is 805 or 902) ? 70 : 1,
                661 => unit.ID == 60 ? 99 : unit.ID == 44 ? 94 : 82,
                662 => unit.ID == 2 ? 96 : unit.RewardCodeIds.Contains(813) ? 70 : 1,
                _ => 1
            };
        }
        private IEnumerator ScenarioSingleRewardsCore(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); _cfActionIdSequence = 0; bool enabled = true;
            yield return YvPrepareScene(config, v => enabled = v);
            DebugMode.AllyInvincible = DebugMode.EnemyInvincible = false;
            Unit shi = CfAlly(2), sei = CfAlly(1), astolfo = CfAlly(162), agni = CfAlly(81), himiko = CfAlly(44), enemy = AliveEnemies().First();
            void Check(string label, bool passed, string actual = "") => YvCheck("단일 보상 " + label, "신규 사양·기존 공통 판정", actual, passed);
            foreach (Unit unit in AliveAllies().Concat(AliveEnemies()))
            { foreach (var code in unit.ActivePassiveCodes.ToList()) code.StopCode(); unit.ActiveUltimateCode?.StopCode(); CfNoCrit(unit); unit.RemoveShield(unit.ShieldCurr); }
            void Equip(Unit unit, params int[] ids)
            {
                foreach (var code in unit.ActivePassiveCodes.ToList()) code.StopCode();
                unit.ApplyRewardCodeInventory(ids.Concat(Enumerable.Repeat(0, 7 - ids.Length)).ToList(), new List<int>());
                foreach (int id in ids) YvCode(unit, id)?.CastCode();
            }
            foreach (Unit unit in AliveAllies()) Equip(unit);
            var pool = RewardManager.Instance.BuildCodePool();
            foreach (int id in SingleAttackRewardCode.Ids)
            {
                var entry = RewardCodeCatalog.Find(id);
                Check("등록·추첨·상점 " + id, entry != null && RewardCodeFactory.IsImplemented(id) && entry.shopFixed && !entry.shopOnly && entry.shopPrice > 0 && pool.Any(x => x.codeId == id));
            }
            Check("정밀 명칭 보존·별도 간파", RewardCodeCatalog.Find(755).verbalName == "정밀" && EnemyInsight.MarkKey != FollowUpRewardCode.PrecisionKey);
            Check("시 마나 보상 제외·분배 허용", CampaignCodeValue(shi, 915) == 1 && CampaignCodeValue(shi, 916) == 1 && CampaignCodeValue(shi, 662) > 90);
            var scheduler = GameManager.Instance.ActionScheduler;
            DamageContext Hit(Unit actor, Unit target, int damage, bool normal = true, bool single = true, bool distributed = false, bool execution = false, bool ultimate = false)
            {
                var ctx = new DamageContext(actor, damage, ultimate ? CodeType.Ultimate : normal ? CodeType.Normal : CodeType.Special,
                    new List<int> { DamageTag.TrueDamage, single ? DamageTag.SingleTarget : DamageTag.AllTarget,
                        ultimate ? DamageTag.UltAttack : normal ? DamageTag.NormalAttack : DamageTag.SpecialAttack }) { IsDistributed = distributed, IsExecution = execution };
                target.TakeDamage(ctx); return ctx;
            }
            IEnumerator Extra(Unit actor, Action action) => NordAction(actor, ActionScheduler.ActionKind.Additional,
                s => s.EnqueueAdditional(actor, "single_reward_probe", "단일 보상 검증", action));
            Equip(shi, 914);
            using (var log = new YvRecorder())
            {
                yield return Extra(shi, () => { Hit(shi, enemy, 100); Hit(shi, enemy, 100); });
                var bonus = log.Dealt.Where(h => h.Attacker == shi && h.DamageContext.HasTag(DamageTag.BonusDamage)).ToList();
                Check("유언 본타2회·추가1회·공격력 상한", bonus.Count == 1 && bonus[0].DamageContext.Damage == Mathf.RoundToInt(Mathf.Min(enemy.HpMax * .02f, shi.AtkCurr * 3)), bonus.Count.ToString());
            }
            using (var log = new YvRecorder())
            {
                yield return Extra(shi, () => { Hit(shi, enemy, 100, normal: false); Hit(shi, enemy, 100, single: false); });
                Check("유언 특수·광역 제외", !log.Dealt.Any(h => h.DamageContext.HasTag(DamageTag.BonusDamage)));
            }
            Equip(shi); Equip(himiko, 915); himiko.ClearUltimateResource();
            yield return NordAction(himiko, ActionScheduler.ActionKind.Normal, s => s.AdvanceAction(himiko, 1));
            Check("실제 단일 일반 마나30", himiko.ManaCurr == Mathf.RoundToInt(30 * himiko.ManaEfficiencyCurr), himiko.ManaCurr.ToString());
            int mana = himiko.ManaCurr;
            yield return Extra(himiko, () => Hit(enemy, himiko, 1));
            Check("피격 마나 시선 증폭 제외", himiko.ManaCurr - mana == Mathf.RoundToInt(Unit.ManaPerHitTaken * himiko.ManaEfficiencyCurr));
            Equip(himiko, 834); float speed = himiko.SpeedCurr;
            yield return NordAction(himiko, ActionScheduler.ActionKind.Normal, s => s.AdvanceAction(himiko, 1));
            yield return NordAction(himiko, ActionScheduler.ActionKind.Normal, s => s.AdvanceAction(himiko, 1));
            Check("헤르메스 일반2회 속도10%", Near(himiko.SpeedCurr, speed * 1.10f), himiko.SpeedCurr.ToString());
            yield return Extra(himiko, () => { });
            Check("추가행동 속도 중첩 제외", Near(himiko.SpeedCurr, speed * 1.10f));
            Equip(himiko); Check("헤르메스 해제 스탯 복원", Near(himiko.SpeedCurr, speed));
            Equip(shi, 759); CfEnsureEffectHit(shi, AliveEnemies());
            yield return Extra(shi, () => { Hit(shi, enemy, 10, normal: false); Hit(shi, enemy, 10, normal: false); });
            enemy.RemoveAllNegativeStatuses();
            foreach (var status in enemy.GetAllStatuses().Where(s => s.IsBeneficial).ToList()) enemy.RemoveStatus(status);
            Check("간파2·정화/강화해제 보존", EnemyInsight.Stacks(enemy) == 2);
            using (var log = new YvRecorder())
            {
                yield return Extra(shi, () => Hit(shi, enemy, 10, normal: false));
                Check("간파3 소모·고정 피해1회·노출", EnemyInsight.Stacks(enemy) == 0 && enemy.HasStatusKey(EnemyInsight.VulnerableKey) &&
                    log.Dealt.Count(h => h.DamageContext.HasTag(DamageTag.BonusDamage)) == 1);
            }
            Equip(shi);
            int dealt = 0;
            yield return Extra(shi, () => dealt = Hit(shi, enemy, 100).ResolvedDamage);
            Check("간파 단일 취약15%", dealt == 115, dealt.ToString());
            enemy.RemoveAllNegativeStatuses(); Check("노출은 정화 가능", !enemy.HasStatusKey(EnemyInsight.VulnerableKey));
            Equip(shi, 835);
            yield return Extra(shi, () => Hit(shi, enemy, 10));
            int hp = enemy.HpCurr; enemy.Heal(1000, enemy);
            Check("사산 검 받는 치유 절반", enemy.HpCurr - hp == 500, (enemy.HpCurr - hp).ToString());
            enemy.RemoveAllNegativeStatuses(); Equip(shi, 836); enemy.AddShield(1000, enemy);
            yield return Extra(shi, () => dealt = Hit(shi, enemy, 100).ResolvedDamage);
            Check("방패 분쇄자 보호막 흡수 전30%", dealt == 130, dealt.ToString());
            enemy.RemoveShield(enemy.ShieldCurr);
            yield return Extra(shi, () => dealt = Hit(shi, enemy, 100).ResolvedDamage);
            Check("보호막 소진 뒤 분쇄자 종료", dealt == 100);
            Equip(shi, 662);
            yield return Extra(shi, () => dealt = Hit(shi, enemy, 100, single: false, distributed: true).ResolvedDamage);
            Check("분배 총량25%", dealt == 125, dealt.ToString());
            yield return Extra(shi, () => dealt = Hit(shi, enemy, 100, single: false).ResolvedDamage);
            Check("일반 광역 분배 오인 없음", dealt == 100);
            ((ShiFinalBell)shi.ActiveUltimateCode).DebugSetStack(5);
            using (var log = new YvRecorder())
            {
                yield return NordAction(shi, ActionScheduler.ActionKind.Normal, s => s.AdvanceAction(shi, 1));
                Check("실제 만종6타 분배 표지", log.Dealt.Any(h => h.Attacker == shi && h.DamageContext.IsDistributed && h.DamageContext.HasTag(DamageTag.UltAttack)));
            }
            Equip(astolfo, 736);
            Check("개전 보호막 운영은 힐러 역할보다 실제 크기 우선", OpeningShieldCampaignValue(sei) > OpeningShieldCampaignValue(astolfo),
                $"세이{ShieldCapability.StrongestBaseAmount(sei)}·아스톨포{ShieldCapability.StrongestBaseAmount(astolfo)}");
            Equip(astolfo); Equip(agni, 760, 739);
            foreach (Unit unit in AliveAllies()) unit.RemoveShield(unit.ShieldCurr);
            int lifeShield = Mathf.RoundToInt(agni.StatValue(StatType.Attack) * 4f);
            agni.Invoke(UnitEventType.OnPassiveActivates, new EventContext(agni));
            Check("생명선 보호막 능력·우수한 방어 연동·소모 없음", ShieldCapability.StrongestBaseAmount(agni) == lifeShield &&
                AliveAllies().All(u => u.ShieldCurr == lifeShield) && agni.ActiveRewardInstance(760) != null, lifeShield.ToString());
            Equip(agni, 760); agni.RemoveShield(agni.ShieldCurr); agni.ModifyHp(Mathf.RoundToInt(agni.HpMax * .4f));
            yield return Extra(enemy, () => Hit(enemy, agni, Mathf.RoundToInt(agni.HpMax * .2f)));
            Check("생명선 공격력400%·실제 인스턴스 소모", agni.ShieldCurr == Mathf.RoundToInt(agni.StatValue(StatType.Attack) * 4f) && agni.ActiveRewardInstance(760) == null && agni.HpCurr > 0,
                $"보호막{agni.ShieldCurr}·원공격력{agni.StatValue(StatType.Attack)}");
            Equip(agni, 761); ControlStatuses.ApplyFixedStun(agni, enemy, 2, true);
            Check("은 식기 즉시 제어·타이머 정화·소모", !agni.isControlled && !agni.HasStatusKey(ControlStatuses.StunKey) && agni.ActiveRewardInstance(761) == null);
            // 보존 분기만 제어된 난수로 확인한다. 장기 자연 진행 측정에는 이 탐침을 쓰지 않는다.
            void PreserveSeed(EventContext _)
            {
                for (int i = 0; i < 100; i++)
                {
                    var state = UnityEngine.Random.state;
                    if (UnityEngine.Random.value >= .25f) continue;
                    UnityEngine.Random.state = state; break;
                }
            }
            astolfo.ActivePassiveCodes.OfType<AstolfoTravelKit>().Single().CastCode();
            agni.AddListener<EventContext>(UnitEventType.OnAfterDamageTaken, PreserveSeed);
            Equip(agni, 760); string keptGuid = agni.ActiveRewardInstance(760).instanceId;
            agni.RemoveShield(agni.ShieldCurr); agni.ModifyHp(Mathf.RoundToInt(agni.HpMax * .4f));
            try { yield return Extra(enemy, () => Hit(enemy, agni, Mathf.RoundToInt(agni.HpMax * .2f))); }
            finally { agni.RemoveListener<EventContext>(UnitEventType.OnAfterDamageTaken, PreserveSeed); }
            Check("아스톨포 생명선 소모 보존·GUID 유지", agni.ActiveRewardInstance(760)?.instanceId == keptGuid && agni.ShieldCurr > 0);
            astolfo.ActivePassiveCodes.OfType<AstolfoTravelKit>().Single().StopCode();
            // 인벤토리와 GUID는 기존 v16 포맷에서 새 ID도 그대로 보존한다.
            var ids = SingleAttackRewardCode.Ids; Equip(agni);
            agni.ApplyRewardCodeInventory(ids.Take(7).ToList(), ids.Skip(7).ToList());
            var saved = RunManager.Instance.CaptureDebugSnapshot();
            RunManager.Instance.RestoreDebugSnapshot(JsonUtility.FromJson<RunSaveData>(JsonUtility.ToJson(saved)));
            agni = CfAlly(81); shi = CfAlly(2); sei = CfAlly(1); astolfo = CfAlly(162); himiko = CfAlly(44); enemy = AliveEnemies().First();
            Check("v17 활성7·보관6 저장 복원", RunSaveData.CurrentVersion == 17 && agni.RewardCodeSlots.SequenceEqual(ids.Take(7)) && agni.StoredRewardCodeIds.SequenceEqual(ids.Skip(7)));
            foreach (Unit u in AliveAllies()) Equip(u);
            foreach (Unit u in AliveAllies().Concat(AliveEnemies())) { CfNoCrit(u); if (u.IsEnemy) YvBoostEnemy(u); }
            Equip(shi, 758, 661); Equip(himiko, 758, 661); agni.AddShield(100, agni);
            yield return Extra(agni, () => dealt = Hit(agni, enemy, 100).ResolvedDamage);
            Check("성역 두 보유자 비중첩15%", dealt == 115, dealt.ToString());
            agni.RemoveShield(agni.ShieldCurr);
            yield return Extra(agni, () => dealt = Hit(agni, enemy, 100).ResolvedDamage);
            Check("성역 보호막 없는 아군 제외", dealt == 100);
            agni.RestoreHpOutsideCombat(agni.HpMax); agni.RemoveShield(agni.ShieldCurr);
            yield return Extra(enemy, () => dealt = Hit(enemy, agni, 100).ResolvedDamage);
            Check("우상 두 보유자 비중첩10%", dealt == 90, dealt.ToString());
            Equip(shi); agni.AddShield(100, agni);
            yield return Extra(agni, () => dealt = Hit(agni, enemy, 100).ResolvedDamage);
            Check("오라 한 보유자 종료 뒤 유지", dealt == 115);
            Equip(himiko); agni.RemoveShield(agni.ShieldCurr);
            yield return Extra(enemy, () => dealt = Hit(enemy, agni, 100).ResolvedDamage);
            Check("마지막 보유자 종료 뒤 우상 해제", dealt == 100);
            // 마지막 적 처치에서도 사망 경제·마나 반환이 전투 종료 정리에 앞서 한 번만 발동해야 한다.
            Equip(agni, 916); foreach (Unit foe in AliveEnemies()) { foe.RemoveShield(foe.ShieldCurr); foe.ModifyHp(1); }
            int spent = 0, killed = 0; void Paid(Unit u, int n) { if (u == agni) spent += n; }
            void Killed(Unit u, Unit by) { if (by == agni && u.IsEnemy) killed++; }
            Unit.AnyUltimateManaSpent += Paid; Unit.AnyUnitDied += Killed;
            try { yield return NordAction(agni, ActionScheduler.ActionKind.Ultimate, _ => agni.FillUltimateResource()); }
            finally { Unit.AnyUltimateManaSpent -= Paid; Unit.AnyUnitDied -= Killed; }
            Check("실제 공격 초월기 소비·다중 처치 반환1회", spent == agni.ManaMax && killed > 0 && agni.ManaCurr == Mathf.Min(agni.ManaMax, Mathf.RoundToInt(spent * .30f) + killed * Unit.ManaPerKill), $"소비{spent}·처치{killed}·마나{agni.ManaCurr}");
            // 새 전장: 실제 처형·일반 사망·가상 처치의 경제를 구분한다.
            yield return YvPrepareScene(config, _ => { });
            shi = CfAlly(2); agni = CfAlly(81); enemy = AliveEnemies().FirstOrDefault(u => u.UnitTier == "normal") ?? AliveEnemies().First();
            foreach (Unit u in AliveAllies()) Equip(u);
            foreach (Unit u in AliveEnemies()) { foreach (var code in u.ActivePassiveCodes.ToList()) code.StopCode(); u.ActiveUltimateCode?.StopCode(); }
            if (enemy.UnitTier != "normal")
            {
                Cell cell = GridManager.Instance.Field.GetComponentsInChildren<Cell>(true).First(c => c.xPos > 0 && !c.isOccupied);
                enemy = GridManager.Instance.SpawnUnit(cell.xPos, cell.yPos, true, 1110);
                YvBoostEnemy(enemy);
            }
            Check("천살성 일반 등급 대상 준비", enemy != null && enemy.UnitTier == "normal", enemy?.UnitTier);
            Equip(shi, 762, 805); enemy.ModifyHp(30); float gold = GameManager.Instance.BattleGoldBonus;
            yield return Extra(shi, () => Hit(shi, enemy, 1));
            Check("천살성 실제 처형 골드10%", !enemy.isActive && Near(GameManager.Instance.BattleGoldBonus - gold, .10f), $"등급{enemy.UnitTier}·HP{enemy.HpCurr}·골드{GameManager.Instance.BattleGoldBonus - gold}");
            Unit other = AliveEnemies().FirstOrDefault();
            if (other != null)
            {
                gold = GameManager.Instance.BattleGoldBonus; Toughness.Publish(other, shi, true);
                Check("가상 처치 경제 제외", Near(GameManager.Instance.BattleGoldBonus, gold));
                Equip(shi, 762); other.ModifyHp(1);
                shi.ActivePassiveCodes.OfType<ShiFinalVerse>().Single().CastCode();
                gold = GameManager.Instance.BattleGoldBonus;
                yield return NordAction(shi, ActionScheduler.ActionKind.Special, s => ((ShiFinalVerse)shi.ActivePassiveCodes.First(c => c is ShiFinalVerse)).DebugRequestExecute());
                Check("시 실제 특성 처형 골드10%", !other.isActive && Near(GameManager.Instance.BattleGoldBonus - gold, .10f), $"HP{other.HpCurr}·골드{GameManager.Instance.BattleGoldBonus - gold}");
            }
            OsFinish(enabled);
        }
    }
}
#endif
