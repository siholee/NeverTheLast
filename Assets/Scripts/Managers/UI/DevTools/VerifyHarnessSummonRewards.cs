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
using Effects.Buffs;
using Entities;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private static bool _campaignSummonRewardStudy;
        private static float? SummonStudyCodeValue(Unit unit, int id)
        {
            bool summon = unit.ID is 180 or 125 or 124;
            bool healer = unit.ID == 124;
            bool front = unit.currentCell != null && unit.currentCell.xPos == -1;
            bool shield = unit.ID == 125;
            bool army = GridManager.Instance.heroList.Any(u => u != null && u.ID is 180 or 125 or 124);
            bool ideas = unit.RewardCodeIds.Any(i => i is 751 or 829 or 911) || GridManager.Instance.heroList.Any(u => u != null && u.RewardCodeIds.Any(i => i is 827 or 830));
            if (id is 752 or 753 or 828 && !ideas) return 12;
            if (id == 655 && GridManager.Instance.heroList.Any(u => u != null && u != unit && u.RewardCodeIds.Contains(655))) return 1;
            if (SummonIdeaRewardCode.Ids.Contains(id)) return id switch
            {
                911 => unit.ID == 180 ? 102 : unit.ID == 125 ? 96 : 1,
                912 => summon ? 102 : 1,
                826 => army ? (front ? 106 : 80) : 1,
                827 => summon ? (unit.ID == 125 ? 108 : 99) : 1,
                828 => unit.ID is 180 or 125 or 20 ? 102 : 1,
                829 => unit.ID == 180 ? 104 : unit.ID == 124 ? 92 : unit.ID == 125 ? 65 : 1,
                830 => summon ? (unit.ID == 125 ? 112 : 99) : 1,
                831 => summon ? 101 : 1,
                748 => front && GridManager.Instance.heroList.Count(u => u != null && u.IsOnField && u.currentCell?.xPos == -1) == 1 ? 106 : 1,
                749 => shield && summon ? 106 : 1,
                750 => unit.ID == 20 ? 92 : 1,
                751 => army ? (unit.ID is 20 or 121 ? 116 : healer ? 113 : 96) : 1,
                752 => army ? (front ? 112 : healer ? 90 : 72) : 1,
                753 => healer ? 110 : 1,
                652 => healer ? 108 : 1,
                653 => healer ? 114 : unit.ID == 121 ? 86 : 1,
                654 => unit.ID is 180 or 125 or 20 ? 108 : 1,
                655 => army ? (front ? 102 : 78) : 1,
                _ => 1
            };
            if (id is 648 or 650 or 649 or 734 or 736) return healer ? (id == 736 ? 110 : id == 648 ? 105 : 99) : 1;
            if (id == 623) return healer ? 102 : unit.ID == 121 ? 75 : 1;
            if (id == 738) return unit.ID == 20 ? 100 : 1;
            if (id == 739) return shield ? 109 : 1;
            return null;
        }

        private IEnumerator ScenarioSummonRewardsCore(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); _cfActionIdSequence = 0;
            bool enabled = true;
            yield return YvPrepareScene(config, value => enabled = value);
            DebugMode.AllyInvincible = DebugMode.EnemyInvincible = false;
            Unit uchi = CfAlly(180), thoth = CfAlly(124), pyg = CfAlly(20), author = CfAlly(10), enemy = AliveEnemies().First();
            void Check(string label, bool ok, string actual = "") => YvCheck("소환 발상 보상 " + label, "설계 사양", actual, ok);
            foreach (Unit unit in AliveAllies().Concat(AliveEnemies()))
            {
                foreach (var code in unit.ActivePassiveCodes.ToList()) code.StopCode();
                unit.ActiveUltimateCode?.StopCode(); CfNoCrit(unit);
                unit.ApplyRewardCodeInventory(Enumerable.Repeat(0, 7).ToList(), new List<int>());
                unit.RemoveShield(unit.ShieldCurr); unit.RestoreHpOutsideCombat(unit.HpMax);
            }
            void Equip(Unit unit, params int[] ids)
            {
                unit.ApplyRewardCodeInventory(ids.Concat(Enumerable.Repeat(0, 7 - ids.Length)).ToList(), new List<int>());
                foreach (int id in ids) YvCode(unit, id)?.CastCode();
            }
            int Hit(Unit source, Unit target, bool summon = false, int raw = 100)
            {
                var tags = new List<int> { DamageTag.NormalAttack, DamageTag.NonContactAttack };
                if (summon) tags.Add(DamageTag.SummonAttack);
                var ctx = new DamageContext(source, raw, CodeType.Normal, tags); target.TakeDamage(ctx); return ctx.ResolvedDamage;
            }
            foreach (int id in SummonIdeaRewardCode.Ids)
                Check("등록·팩토리·상점 " + id, RewardCodeCatalog.Find(id)?.shopFixed == true && RewardCodeFactory.IsImplemented(id));
            var pool = RewardManager.Instance.BuildCodePool();
            foreach (int id in new[] { 751, 752, 753 }) Check("상점 전용 추첨 제외 " + id, RewardCodeCatalog.Find(id).shopOnly && pool.All(e => e.codeId != id));
            Equip(author, 751); Equip(uchi, 830, 829, 912, 828, 654, 911);
            uchi.ActivePassiveCodes.First(p => p is Codes.Passive.UchiTaoist).CastCode();
            int before = SummonIdeaRewardCode.Ideas(author);
            Unit clone = Summons.Create(uchi);
            Check("아군 생성·신종 특허", SummonIdeaRewardCode.Ideas(author) == before + 3 && SummonIdeaRewardCode.Ideas(uchi) == 2);
            Summons.Create(uchi);
            Check("동종 재소환 특허 제외", SummonIdeaRewardCode.Ideas(uchi) == 2 && SummonIdeaRewardCode.Ideas(author) == before + 4);
            clone.Die(null);
            Check("실제 소환 사망 보상", SummonIdeaRewardCode.Ideas(uchi) == 3);
            uchi.RemoveAllNegativeStatuses(author);
            YvDispelBeneficial(uchi);
            Check("발상 정화·강화 해제 보존", SummonIdeaRewardCode.Ideas(uchi) == 3);
            foreach (Unit e in AliveEnemies())
            { YvAddTestStatus(e, 99891, "idea_test_hp", new StatModifierEffect(StatType.MaxHp, 200000f), StatusCategory.Neutral); e.RestoreHpOutsideCombat(e.HpMax); }
            int enhanced = Hit(uchi, enemy, true, 1000), body = Hit(uchi, enemy, false, 1000);
            Check("일장일단 소환·본체 분리", enhanced > body * 1.6f, enhanced + "/" + body);
            Equip(uchi, 911);
            Unit victim = AliveEnemies().LastOrDefault(u => u != enemy);
            if (victim == null)
            {
                var cell = FindObjectsByType<Cell>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .First(c => c.xPos > 0 && c.xPos <= 2 && !c.isOccupied && c.yPos >= 1 && c.yPos <= 4);
                victim = GridManager.Instance.SpawnUnit(cell.xPos, cell.yPos, true, enemy.ID, playEntry: false);
            }
            victim.RemoveShield(victim.ShieldCurr);
            int idea = SummonIdeaRewardCode.Ideas(uchi);
            Hit(uchi, victim, true, 10000000);
            Check("소환 실제 적 처치 +3", !victim.isActive && SummonIdeaRewardCode.Ideas(uchi) == idea + 3);
            idea = SummonIdeaRewardCode.Ideas(uchi);
            Toughness.Publish(enemy, uchi, true);
            Check("가상 처치 발상 제외", SummonIdeaRewardCode.Ideas(uchi) == idea);
            Equip(thoth, 652, 653, 753);
            thoth.RemoveStatusByKey(SummonIdeaRewardCode.IdeaKey);
            thoth.ActivePassiveCodes.First(p => p is Codes.Passive.ThothLivingRecords).CastCode();
            var records = thoth.ActivePassiveCodes.OfType<Codes.Passive.ThothLivingRecords>().Single(); records.SummonScribe();
            author.RemoveShield(author.ShieldCurr); author.RestoreHpOutsideCombat(author.HpMax);
            int shieldBefore = author.ShieldCurr;
            author.Heal(100, thoth);
            Check("반려·초과 치유 30% 보호막", author.ShieldCurr - shieldBefore == 38, (author.ShieldCurr - shieldBefore).ToString());
            SummonIdeaRewardCode.GainIdea(thoth, 4); shieldBefore = author.ShieldCurr;
            author.Heal(100, thoth);
            Check("발상 치유 보너스", author.ShieldCurr - shieldBefore == 41, (author.ShieldCurr - shieldBefore).ToString());
            Equip(thoth, 827); int total = SummonIdeaRewardCode.Ideas(author);
            thoth.CastUltimateCode(); yield return new WaitForSeconds(.8f);
            Check("교류 세미나 소유 개체당 +1", SummonIdeaRewardCode.Ideas(author) == total + 1);
            Equip(uchi, 831);
            YvAddTestStatus(author, 99892, "idea_test_bad", new StatModifierEffect(StatType.Defense, -.1f, true), StatusCategory.Negative);
            Summons.Create(uchi);
            Check("신선한 바람 광역 정화", !author.HasStatusKey("idea_test_bad"));
            YvAddTestStatus(author, 99892, "idea_test_bad", new StatModifierEffect(StatType.Defense, -.1f, true), StatusCategory.Negative);
            Summons.Create(uchi);
            Check("신선한 바람 같은 턴 재사용 불가", author.HasStatusKey("idea_test_bad"));
            uchi.BeginTurn(); Summons.Create(uchi);
            Check("2턴 재사용 대기 1턴 후 유지", author.HasStatusKey("idea_test_bad"));
            uchi.BeginTurn(); Summons.Create(uchi);
            Check("2턴 후 정화 재사용", !author.HasStatusKey("idea_test_bad"));
            int finalIdeas = SummonIdeaRewardCode.Ideas(author);
            Equip(uchi); Equip(thoth); Equip(author); Summons.Create(uchi);
            Check("정지 후 생성 중첩 없음", SummonIdeaRewardCode.Ideas(author) == finalIdeas);
            foreach (Unit unit in AliveAllies()) unit.RestoreHpOutsideCombat(unit.HpMax);
            int baseHit = Hit(enemy, pyg, false, 300);
            pyg.RestoreHpOutsideCombat(pyg.HpMax); Equip(pyg, 826);
            int reduced = Hit(enemy, pyg, false, 300);
            Check("필드 소환 수에 비례한 경감", reduced < baseHit, reduced + "/" + baseHit);
            pyg.RestoreHpOutsideCombat(pyg.HpMax); Equip(pyg, 752);
            SummonIdeaRewardCode.GainIdea(pyg, 100);
            reduced = Hit(enemy, pyg, false, 300);
            Check("과학 승리 경감 상한25%", Mathf.Abs(reduced - Mathf.RoundToInt(baseHit * .75f)) <= 1);
            Equip(pyg); pyg.ActivePassiveCodes.OfType<Codes.Passive.PygmalionGalatea>().Single().CastCode();
            float def = pyg.StatValue(StatType.Defense);
            float scribeDefense = records.Scribe.StatValue(StatType.Defense);
            Equip(author, 655);
            Check("소환 간주 아군 보호 장비", pyg.StatValue(StatType.Defense) > def, pyg.StatValue(StatType.Defense) + "/" + def);
            Check("실제 서기관 상속 방어에도30%", Mathf.Abs(records.Scribe.StatValue(StatType.Defense) - scribeDefense * 1.30f) < .01f,
                records.Scribe.StatValue(StatType.Defense) + "/" + scribeDefense);
            Equip(author);
            Check("보호 장비 정지 방어력 복원", Mathf.Abs(pyg.StatValue(StatType.Defense) - def) < .01f);
            Check("서기관 상속 방어 정지 복원", Mathf.Abs(records.Scribe.StatValue(StatType.Defense) - scribeDefense) < .01f);
            pyg.ActivePassiveCodes.OfType<Codes.Passive.PygmalionGalatea>().Single().StopCode();
            Equip(author, 655);
            float ordinaryDef = pyg.StatValue(StatType.Defense);
            pyg.ActivePassiveCodes.OfType<Codes.Passive.PygmalionGalatea>().Single().CastCode();
            Check("개전 특성 순서와 무관한 보호 장비", pyg.StatValue(StatType.Defense) > ordinaryDef);
            float singleAura = pyg.StatValue(StatType.Defense);
            Equip(thoth, 655);
            Check("동일 보호 장비 오라 중복 없음", Mathf.Abs(pyg.StatValue(StatType.Defense) - singleAura) < .01f);
            Equip(author);
            Check("한 제공자 정지 후 다른 오라 유지", Mathf.Abs(pyg.StatValue(StatType.Defense) - singleAura) < .01f);
            Equip(thoth);
            Check("마지막 오라 정지 후 방어력 복원", Mathf.Abs(pyg.StatValue(StatType.Defense) - ordinaryDef) < .01f);
            author.RemoveShield(author.ShieldCurr); Equip(thoth, 749); author.AddShield(100, thoth);
            Check("소환 보유 보호막 보너스", author.ShieldCurr == 130, author.ShieldCurr.ToString());
            Equip(thoth); Equip(pyg, 750);
            int hp = enemy.HpCurr, triggers = 0;
            Action<EventContext> retaliate = hit =>
            {
                if (hit.DmgCtx?.ResolvedDamage <= 0) return;
                var status = BuffStatus.Create(99893, "idea_counter_bad", "검증 디버프", pyg, enemy,
                    new StatModifierEffect(StatType.SpecialDefense, -.1f, true), category: StatusCategory.Negative);
                status.SkipEffectHitRoll = true; enemy.AddStatus(status); enemy.AddStatus(status); triggers++;
            };
            pyg.AddListener(UnitEventType.OnAfterDamageTaken, retaliate);
            pyg.RestoreHpOutsideCombat(pyg.HpMax);
            using (var log = new YvRecorder())
            {
                Hit(enemy, pyg, false, 300);
                Check("피격 중 성공 디버프 고정 피해·중복 없음", triggers == 1 && enemy.HpCurr < hp &&
                    log.Dealt.Count(h => h.Attacker == pyg && h.DamageContext.HasTag(DamageTag.TrueDamage) && h.DamageContext.HasTag(DamageTag.BonusDamage)) == 1,
                    (hp-enemy.HpCurr).ToString());
            }
            pyg.RemoveListener(UnitEventType.OnAfterDamageTaken, retaliate);
            hp = enemy.HpCurr;
            ActionDot.Apply(pyg, enemy, ActionDotKind.Poison, inheritedHit: true);
            Check("피격 범위 밖 디버프 추가 피해 없음", enemy.HpCurr == hp);
            var grid = GridManager.Instance;
            // 대기석도 같은 x 좌표를 쓰므로 실제 전열/후열의 양수 y 칸만 선택한다.
            var cells = FindObjectsByType<Cell>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => c.yPos > 0).ToArray();
            int front = grid.GetFrontColumn(false), rear = grid.GetRearColumn(false);
            // 피그말리온이 후열이면 먼저 전열과 교환해 후열4칸 중 하나를 비운다.
            if (pyg.currentCell.xPos != front)
                Check("검증 피그말리온 전열 이동", grid.RelocateUnit(pyg, cells.First(c => c.xPos == front)));
            foreach (Unit unit in AliveAllies().Where(u => u != pyg && u.currentCell.xPos == front).ToList())
                Check("검증 후열 이동 " + unit.ID, grid.RelocateUnit(unit, cells.First(c => c.xPos == rear && !c.isOccupied)));
            Equip(pyg); def = pyg.StatValue(StatType.Defense); pyg.RestoreHpOutsideCombat(pyg.HpMax);
            baseHit = Hit(enemy, pyg, false, 300); pyg.RestoreHpOutsideCombat(pyg.HpMax); Equip(pyg, 748);
            reduced = Hit(enemy, pyg, false, 300);
            Check("전열 단독 방어력·경감", pyg.StatValue(StatType.Defense) > def && reduced < baseHit);
            bool moved = grid.RelocateUnit(author, cells.First(c => c.xPos == front && !c.isOccupied));
            Check("전열 동료 이동 즉시 단독 보너스 해제", moved && author.IsOnField && Mathf.Abs(pyg.StatValue(StatType.Defense) - def) < .01f,
                $"이동 {moved}, 필드 {author.IsOnField}, 칸 {author.currentCell.xPos}/{author.currentCell.yPos}, 기준 {def}, 현재 {pyg.StatValue(StatType.Defense)}");
            Check("피격 컨텍스트 범위 복원", pyg.ReceivingDamageContext == null && enemy.ReceivingDamageContext == null);
            Equip(thoth, 652, 653, 751, 753);
            var saved = new UnitSaveData(); thoth.WriteRunState(saved);
            string instance = thoth.ActiveRewardInstance(653).instanceId;
            Equip(thoth); thoth.RestoreRunState(JsonUtility.FromJson<UnitSaveData>(JsonUtility.ToJson(saved)));
            Check("신규 코드 저장 인스턴스 복원", thoth.ActiveRewardInstance(653)?.instanceId == instance && thoth.RewardCodeIds.Contains(753));
            foreach (Unit unit in AliveAllies()) unit.Invoke(UnitEventType.OnRoundEnd, new EventContext(unit));
            Check("전투 종료 발상 초기화", AliveAllies().All(u => SummonIdeaRewardCode.Ideas(u) == 0));
            OsFinish(enabled);
        }
    }
}
#endif
