#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive.Reward;
using Codes.Ultimate;
using Combat;
using Core;
using Effects.Buffs;
using Entities;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private static bool CampaignUsesAdditional(Unit unit)
        {
            // 시의 처치 재행동은 Special이다. 추천 역할 표기를 추공 보상 호환성으로 읽지 않는다.
            return unit != null && unit.ID != 2 &&
                ((SynergyCatalog.UnitOf(unit.ID)?.roles?.Contains("EXTRA_ACT") ?? false) || unit.ID is 1 or 7 or 22 or 122 or 165);
        }
        private static float FollowUpCampaignValue(Unit unit, int id)
        {
            bool extra = CampaignUsesAdditional(unit);
            bool counter = unit.ID is 7 or 22 or 123;
            bool source = unit.RewardCodeIds.Any(i => i is 755 or 833);
            bool payoff = unit.RewardCodeIds.Any(i => i is 658 or 913);
            return id switch
            {
                913 => extra && source ? 112 : 3,
                832 => extra ? 78 : 2,
                833 => extra && payoff ? 94 : extra ? 18 : 2,
                754 => unit.ID is 1 or 7 or 22 or 121 or 20 or 45 ? 86 : 30,
                755 => extra ? (payoff ? 110 : 55) : 2,
                756 => extra ? 76 : 2,
                757 => extra ? 75 : 2,
                656 => extra && unit.ID is not (1 or 22) ? 75 : 2,
                657 => unit.ID == 162 ? 90 : 2,
                658 => extra && source && unit.ID != 1 ? 92 : 3,
                659 => counter ? 85 : 2,
                // 등대 자체는 마나를 만들지 않는다. 연환격 등 실제 획득 원천을 갖춘 뒤에 평가한다.
                660 => extra && unit.UltimateResourceType == UltimateResourceType.Mana && unit.RewardCodeIds.Contains(724) ? 75 : 2,
                _ => 1
            };
        }
        private sealed class FollowUpCritProbe : Code
        {
            public FollowUpCritProbe(Unit unit) { Caster = unit; CodeType = BaseEnums.CodeType.Normal; }
            public float Multiplier(Unit target, params int[] tags) { RollCritical(target, tags, out float m); return m; }
        }
        private IEnumerator ScenarioSeiFollowUpCore(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); _cfActionIdSequence = 0; bool enabled = true;
            yield return YvPrepareScene(config, value => enabled = value);
            DebugMode.AllyInvincible = DebugMode.EnemyInvincible = false;
            Unit sei = CfAlly(1), dealer = CfAlly(20), enemy = AliveEnemies().First();
            var scheduler = GameManager.Instance.ActionScheduler;
            void Check(string label, bool ok, string actual = "") => YvCheck("세이·추공 " + label, "설계 및 공통 전투 경로", actual, ok);
            foreach (Unit unit in AliveAllies().Concat(AliveEnemies()))
            {
                foreach (var code in unit.ActivePassiveCodes.ToList()) code.StopCode();
                unit.ActiveUltimateCode?.StopCode(); CfNoCrit(unit); unit.RemoveShield(unit.ShieldCurr);
                unit.ApplyRewardCodeInventory(Enumerable.Repeat(0, 7).ToList(), new List<int>());
            }
            void Equip(Unit unit, params int[] ids)
            {
                foreach (var code in unit.ActivePassiveCodes.ToList()) code.StopCode();
                unit.ApplyRewardCodeInventory(ids.Concat(Enumerable.Repeat(0, 7 - ids.Length)).ToList(), new List<int>());
                foreach (int id in ids) YvCode(unit, id)?.CastCode();
            }
            IEnumerator Extra(Unit unit, Action action, bool priority = false)
                => NordAction(unit, priority ? ActionScheduler.ActionKind.PriorityAdditional : ActionScheduler.ActionKind.Additional,
                    s => { if (priority) s.EnqueuePriorityAdditional(unit, "followup_fixture", "추공 검증", action); else s.EnqueueAdditional(unit, "followup_fixture", "추공 검증", action); });
            void Hit(Unit actor, Unit victim, int raw, params int[] tags)
                => victim.TakeDamage(new DamageContext(actor, raw, BaseEnums.CodeType.Normal, tags.ToList()));
            foreach (int id in FollowUpRewardCode.Ids)
            {
                var entry = RewardCodeCatalog.Find(id);
                Check("등록·Factory " + id, entry != null && RewardCodeFactory.IsImplemented(id) && entry.shopFixed && entry.shopPrice > 0 &&
                    entry.shopOnly == (id is 755 or 757 or 833));
            }
            Check("상위 사다리", RewardCodeCatalog.IsUpperOf(913, 658) && RewardCodeCatalog.SealTargetOf(658) == 913);
            var pool = RewardManager.Instance.BuildCodePool();
            foreach (int id in FollowUpRewardCode.Ids)
                Check("보상 추첨·상점 전용 분리 " + id, pool.Any(r => r.codeId == id) == (id is not (755 or 757 or 833)));
            Check("치유 코드 ID·효과 보존", RewardCodeCatalog.Find(649).verbalName == "치유의 찬가" && RewardCodeFactory.Create(649, new PassiveCodeContext()) is SecretSupportRewardCode);
            Check("운영 등대 원천 없으면 낮은 평가", FollowUpCampaignValue(sei, 660) == 2);
            Unit shi = CfAlly(2);
            Check("운영 시 특수행동은 추공 제외", shi != null && !CampaignUsesAdditional(shi) &&
                FollowUpCampaignValue(shi, 656) == 2 && FollowUpCampaignValue(shi, 756) == 2 && FollowUpCampaignValue(shi, 757) == 2);
            Check("운영 시 기존 연환격도 제외", shi != null && CampaignCodeValue(shi, 724) == 2);
            Check("운영 세이 실제 빅뱅 추공 유지", CampaignUsesAdditional(sei) && FollowUpCampaignValue(sei, 756) == 76);
            CfEnsureEffectHit(sei, AliveEnemies());
            using (var log = new CfLog(AliveAllies()))
            {
                yield return NordAction(sei, ActionScheduler.ActionKind.Normal, s => s.AdvanceAction(sei, 1));
                enemy = log.Dealt.First(h => h.Attacker == sei).Target;
                Check("일반 방깎·바위 부착 없음", enemy.HasStatusKey("sei_ground_shred") && !log.Elements.Any(e => e.Source == sei));
                Check("일반 완료1회", log.NormalResolved.GetValueOrDefault(sei) == 1);
            }
            int expectedShield = sei.ActiveUltimateCode.BaseShieldAmount;
            int attackBefore = dealer.AtkCurr;
            yield return NordAction(sei, ActionScheduler.ActionKind.Ultimate, _ => sei.FillUltimateResource());
            Check("시전 보호막·피증·집중 오라", AliveAllies().All(u => u.ShieldCurr == expectedShield && u.HasStatusKey(SeiSongOfGenesis.CastBuffKey) && u.HasStatusKey(SeiSongOfGenesis.BuffKey)) && dealer.AtkCurr == attackBefore, expectedShield.ToString());
            using (var log = new CfLog(AliveAllies()))
            {
                Hit(dealer, enemy, 100, DamageTag.TrueDamage, DamageTag.NormalAttack);
                Check("일반 피증25%·추가 피해1회", log.Dealt.Count(h => h.Attacker == dealer && h.DamageContext.HasTag(DamageTag.BonusDamage)) == 1 &&
                    log.Dealt.Single(h => h.Attacker == dealer && !h.DamageContext.HasTag(DamageTag.BonusDamage)).DamageDealt == 125);
                int bonusCount = log.Dealt.Count;
                Hit(dealer, enemy, 100, DamageTag.TrueDamage, DamageTag.BonusDamage);
                Check("추가 피해 재귀 없음", log.Dealt.Count == bonusCount + 1);
            }
            int beforeShield = dealer.ShieldCurr;
            Hit(enemy, dealer, 100, DamageTag.TrueDamage, DamageTag.NormalAttack);
            Check("집중 경감25%", beforeShield - dealer.ShieldCurr == 75, (beforeShield - dealer.ShieldCurr).ToString());
            // 재시전은 이전 집중만 정리하고 새 시전 효과·오라를 보존한다.
            yield return NordAction(sei, ActionScheduler.ActionKind.Ultimate, _ => sei.FillUltimateResource());
            Check("재집중 새 오라 유지", AliveAllies().All(u => u.HasStatusKey(SeiSongOfGenesis.BuffKey)));
            sei.Invoke(UnitEventType.OnControlStarts, new EventContext(sei, enemy));
            Check("집중 중단·시전 버프와 보호막 유지", !Concentration.IsActive(sei, SeiSongOfGenesis.ConcentrationKey) &&
                AliveAllies().All(u => !u.HasStatusKey(SeiSongOfGenesis.BuffKey) && u.HasStatusKey(SeiSongOfGenesis.CastBuffKey) && u.ShieldCurr > 0));
            using (var log = new CfLog(AliveAllies()))
            {
                Hit(dealer, enemy, 100, DamageTag.TrueDamage, DamageTag.NormalAttack);
                Check("중단 후 피증만 유지", log.Dealt.Count(h => h.Attacker == dealer) == 1 && log.Dealt.Single(h => h.Attacker == dealer).DamageDealt == 125);
            }
            foreach (Unit unit in AliveAllies()) { unit.RemoveStatusByKey(SeiSongOfGenesis.CastBuffKey); unit.RemoveShield(unit.ShieldCurr); }
            using (var log = new CfLog(AliveAllies()))
            {
                yield return Extra(sei, () => sei.ActivePassiveCodes.OfType<Codes.Passive.SeiBigBang>().Single().DebugReleaseExecutable(), true);
                Check("실제 빅뱅7발 추가공격 태그", log.Dealt.Count(h => h.Attacker == sei) == 7 && log.Dealt.Where(h => h.Attacker == sei).All(h => h.DamageContext.HasTag(DamageTag.AdditionalAttack)));
            }
            foreach (Unit unit in AliveAllies()) unit.RemoveShield(unit.ShieldCurr);
            enemy.RemoveAllNegativeStatuses();
            float attack = dealer.StatValue(StatType.Attack);
            Equip(dealer, 833, 658);
            Check("준비6·승리 공격력12%", FollowUpRewardCode.Precision(dealer) == 6 && Near(dealer.StatValue(StatType.Attack), attack * 1.12f));
            YvCode(dealer, 833).CastCode();
            Check("준비 재시전 중복 없음", FollowUpRewardCode.Precision(dealer) == 6);
            dealer.TryGrantRewardCode(913);
            Check("승급 활성 하위 보관", dealer.RewardCodeIds.Contains(913) && !dealer.RewardCodeIds.Contains(658));
            Equip(dealer, 913, 755, 757, 660, 724);
            dealer.ModifyHp(dealer.HpMax / 2); dealer.ClearUltimateResource();
            int hpBefore = dealer.HpCurr; float attackDuring = 0;
            yield return Extra(dealer, () => { attackDuring = dealer.StatValue(StatType.Attack); dealer.RecoverMana(10); });
            Check("추가 공격력 문맥·정밀 완료+1", Near(attackDuring, attack * 1.18f) && Near(dealer.StatValue(StatType.Attack), attack) && FollowUpRewardCode.Precision(dealer) == 7);
            Check("추공 마나30%·완료 보상 포함", dealer.ManaCurr == 19, dealer.ManaCurr.ToString());
            Check("에너지바 치유3%", dealer.HpCurr - hpBefore == Mathf.RoundToInt(dealer.HpMax * .03f));
            int stacks = FollowUpRewardCode.Precision(dealer);
            yield return NordAction(dealer, ActionScheduler.ActionKind.Special, s => s.EnqueueSpecial(dealer, "precision_special", "특수 제외", () => dealer.RecoverMana(10)));
            Check("특수 정밀·추공 마나 제외", FollowUpRewardCode.Precision(dealer) == stacks && dealer.ManaCurr == 29, dealer.ManaCurr.ToString());
            yield return Extra(dealer, () => { }, true);
            Check("우선 추가행동 포함", FollowUpRewardCode.Precision(dealer) == stacks + 1);
            Equip(dealer, 660); dealer.ClearUltimateResource();
            yield return Extra(dealer, () => Hit(enemy, dealer, 1, DamageTag.NormalAttack, DamageTag.TrueDamage));
            Check("추공 중 피격 마나는 등대 제외", dealer.ManaCurr == Unit.ManaPerHitTaken, dealer.ManaCurr.ToString());
            FollowUpRewardCode.GainPrecision(dealer, 1000);
            Check("정밀 상한20", FollowUpRewardCode.Precision(dealer) == 20);
            Equip(dealer, 656);
            dealer.RemoveStatusByKey("verify_cf_nocrit");
            YvAddTestStatus(dealer, 98915, "verify_followup_crit", new StatModifierEffect(StatType.CritRate, 1f));
            var probe = new FollowUpCritProbe(dealer);
            float baseCrit = probe.Multiplier(enemy, DamageTag.NormalAttack, DamageTag.SingleTarget);
            Check("단일 추공 치피40%p", Near(probe.Multiplier(enemy, DamageTag.AdditionalAttack, DamageTag.SingleTarget), baseCrit + .4f));
            Check("광역 추공 제외", Near(probe.Multiplier(enemy, DamageTag.AdditionalAttack, DamageTag.MultiTarget), baseCrit));
            Equip(dealer, 913);
            Check("서사시환 정밀20 치피100%p", Near(probe.Multiplier(enemy, DamageTag.AdditionalAttack, DamageTag.MultiTarget), baseCrit + 1f));
            Equip(dealer, 756);
            using (var log = new CfLog(AliveAllies()))
            {
                yield return Extra(dealer, () => { Hit(dealer, enemy, 100, DamageTag.AdditionalAttack, DamageTag.TrueDamage); Hit(dealer, enemy, 100, DamageTag.AdditionalAttack, DamageTag.TrueDamage); });
                Check("날 세우기 같은 대상 다단1회", log.Dealt.Count(h => h.Attacker == dealer && h.DamageContext.HasTag(DamageTag.BonusDamage)) == 1 &&
                    log.Dealt.Single(h => h.Attacker == dealer && h.DamageContext.HasTag(DamageTag.BonusDamage)).DamageDealt == 20 + dealer.Level * 2);
            }
            Equip(dealer, 659);
            using (var log = new CfLog(AliveAllies()))
            {
                yield return Extra(dealer, () => Hit(dealer, enemy, 100, DamageTag.AdditionalAttack, DamageTag.CounterAttack, DamageTag.TrueDamage));
                Check("복수자 반격20%", log.Dealt.Single(h => h.Attacker == dealer).DamageDealt == 120);
            }
            Equip(dealer, 754); dealer.AddShield(200, dealer); beforeShield = dealer.ShieldCurr;
            Hit(enemy, dealer, 100, DamageTag.TrueDamage, DamageTag.NormalAttack);
            Check("에어백 흡수 전25%", beforeShield - dealer.ShieldCurr == 75);
            dealer.RemoveShield(dealer.ShieldCurr); hpBefore = dealer.HpCurr;
            Hit(enemy, dealer, 100, DamageTag.TrueDamage, DamageTag.NormalAttack);
            Check("보호막 없으면 에어백 종료", hpBefore - dealer.HpCurr == 100);
            Equip(sei, 657);
            // 앞선 명중 보정을 제거한 기본값에서 다시 계산해 교체 시 보정이 사라지지 않게 한다.
            sei.RemoveStatusByKey("verify_cf_hit"); CfEnsureEffectHit(sei, AliveEnemies());
            // 남은 AV가 이미 0이면 실제 감소 이벤트가 없다. 감소할 AV를 먼저 확보한다.
            yield return Extra(sei, () => { scheduler.DelayAction(enemy, 1f); scheduler.AdvanceAction(enemy, .25f); });
            Check("투우사 적 AV 감소 디버프", enemy.HasStatusKey("reward_bullfighter"));
            var bull = enemy.CombatEffects().FirstOrDefault(e => e.CritDamageBonusAgainst(enemy, dealer, BaseEnums.CodeType.Normal) < 0);
            Check("투우사 치피40%p", bull != null && Near(bull.CritDamageBonusAgainst(enemy, dealer, BaseEnums.CodeType.Normal), -.4f));
            yield return Extra(enemy, () => Hit(enemy, dealer, 10, DamageTag.NormalAttack, DamageTag.TrueDamage));
            Check("투우사 다음 실제 공격 완료 소비", !enemy.HasStatusKey("reward_bullfighter"));
            yield return Extra(dealer, () => scheduler.AdvanceAction(enemy, .25f));
            Check("다른 보유자 행동은 투우사 제외", !enemy.HasStatusKey("reward_bullfighter"));
            Equip(dealer, 832); bool advanced = false;
            void Observe(Unit target, bool ahead) { if (target == dealer && ahead) advanced = true; }
            ActionScheduler.AnyActionOrderAdjusted += Observe;
            try { yield return Extra(dealer, () => Hit(dealer, enemy, 100000000, DamageTag.AdditionalAttack, DamageTag.TrueDamage)); }
            finally { ActionScheduler.AnyActionOrderAdjusted -= Observe; }
            Check("끝없는 허기 실제 추가공격 처치", !enemy.isActive && advanced);
            dealer.Invoke(UnitEventType.OnRoundEnd, new EventContext(dealer));
            Check("전투 종료 정밀·준비 표식 초기화", FollowUpRewardCode.Precision(dealer) == 0 && !dealer.HasStatusKey("reward_precision_prepared"));
            Equip(dealer, 833); Check("다음 전투 준비6 재획득", FollowUpRewardCode.Precision(dealer) == 6);
            foreach (Unit unit in AliveAllies()) Equip(unit);
            DebugMode.AllyInvincible = DebugMode.EnemyInvincible = false; OsFinish(enabled);
        }
    }
}
#endif
