#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using Core;
using Effects.Buffs;
using Entities;
using UnityEngine;
using Kind = Managers.ActionScheduler.ActionKind;
using static BaseClasses.BaseEnums;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioDotFrontlineCore(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); bool enabled = true;
            // 고정 선택 순서 대신 자연 진행과 같은 추천 배치를 사용한다.
            var catalog = CampaignBuildCatalog();
            config.lineup = catalog.teams.Single(spec => spec.aliases.Contains("검증 편성/b48_dot_poison")).key;
            yield return EnsureLineup(config);
            yield return YvPrepareScene(config, value => enabled = value);
            Unit moon = CfAlly(45), healer = CfAlly(167), ally = CfAlly(164), enemy = AliveEnemies().First();
            foreach (Unit u in AliveAllies().Concat(AliveEnemies()))
            { foreach (var code in u.ActivePassiveCodes) code.StopCode(); u.ActiveUltimateCode?.StopCode(); CfNoCrit(u); }
            void Check(string label, bool okay, string actual = "") => YvCheck("지딜 전열 " + label, "현행 YAML·선형 성장", actual, okay);
            Check("츠쿠요미 추천 전열·방벽 역할", SynergyCatalog.UnitOf(45).row == "Front" && SynergyCatalog.UnitOf(45).roles.Contains("BULWARK"));
            Check("두 유닛 실제 전열 배치", moon.IsOnField && healer.IsOnField && moon.currentCell.xPos == GridManager.Instance.GetFrontColumn(false) && healer.currentCell.xPos == GridManager.Instance.GetFrontColumn(false), $"츠쿠요미 {moon.currentCell.xPos},{moon.currentCell.yPos} / 에레쉬 {healer.currentCell.xPos},{healer.currentCell.yPos}");
            foreach (int level in new[] { 1, 14, 50, 500 })
            {
                moon.DebugSetLevel(level); healer.DebugSetLevel(level);
                Check($"Lv{level} 에레쉬 체력·방어 기본/성장", Near(healer.BaseStat(StatType.MaxHp), 2400 + 215 * (level - 1)) && Near(healer.BaseStat(StatType.Defense), 155 + 11.5f * (level - 1)), $"{healer.HpMax}/{healer.StatValue(StatType.Defense)}");
                Check($"Lv{level} 츠쿠요미 체력·방어 기본/성장", Near(moon.BaseStat(StatType.MaxHp), 2400 + 215 * (level - 1)) && Near(moon.BaseStat(StatType.Defense), 170 + 12.5f * (level - 1)), $"{moon.HpMax}/{moon.StatValue(StatType.Defense)}");
                Check($"Lv{level} 특공 유지·특방 성장 반영", Near(moon.BaseStat(StatType.SpecialAttack), 104 + 6.8f * (level - 1), .1f) && Near(moon.BaseStat(StatType.SpecialDefense), 110 + 8.2f * (level - 1), .1f) &&
                    Near(healer.BaseStat(StatType.SpecialAttack), 78 + 5 * (level - 1), .1f) && Near(healer.BaseStat(StatType.SpecialDefense), 120 + 9f * (level - 1), .1f));
            }
            moon.DebugSetLevel(14); healer.DebugSetLevel(14);
            healer.ModifyHp(healer.HpMax / 2); float ratio = (float)healer.HpCurr / healer.HpMax;
            YvAddTestStatus(healer, 99879, "frontline_hp_ratio", new StatModifierEffect(StatType.MaxHp, 1000));
            bool held = Near((float)healer.HpCurr / healer.HpMax, ratio, .001f);
            healer.RemoveStatusByKey("frontline_hp_ratio");
            Check("체력 갱신·해제 HP비율 보존", held && Near((float)healer.HpCurr / healer.HpMax, ratio, .001f));
            DebugMode.AllyInvincible = false;
            foreach (Unit u in new[] { healer, moon })
            {
                u.RestoreHpOutsideCombat(u.HpMax);
                var hit = new DamageContext(enemy, 1000, CodeType.Normal, new List<int> { DamageTag.NormalAttack, DamageTag.NonContactAttack });
                u.TakeDamage(hit);
                int expected = Mathf.RoundToInt(1000 * UnitStats.MitigationMultiplier(u.StatValue(StatType.Defense), UnitStats.ArmorConstant(enemy.Level)));
                Check(u.UnitName + " 실제 방어 경감", hit.ResolvedDamage == expected, $"{hit.ResolvedDamage}/{expected}");
            }
            DebugMode.AllyInvincible = true;
            var feast = healer.ActivePassiveCodes.OfType<EreshkigalUnderworldFeast>().Single(); feast.CastCode();
            foreach (Unit u in AliveAllies()) u.ModifyHp(u.HpMax / 4);
            int before = ally.HpCurr, expectedHeal = Mathf.RoundToInt(healer.HpMax * .020f);
            enemy.TakeDamage(new DamageContext(moon, 10, CodeType.Effect, new List<int>()) { IsDamageOverTime = true, DotRootId = ActionDot.NextRootId() });
            Check("최대체력 버프가 기본 치유에 반영", ally.HpCurr - before == expectedHeal && healer.HpMax == 5195, $"치유 {ally.HpCurr-before}/{expectedHeal}");
            feast.StopCode();
            using (var log = new YvRecorder())
            {
                yield return NordAction(healer, Kind.Normal, s => s.AdvanceAction(healer, 1));
                Check("일반행동 최대체력6% 계수 보존", log.Dealt.Any(h => h.Attacker == healer && !h.DamageContext.IsDamageOverTime && h.DamageContext.Damage == Mathf.RoundToInt(healer.HpMax * .06f)));
            }
            foreach (Unit u in AliveAllies()) u.ModifyHp(u.HpMax / 4);
            before = ally.HpCurr; int self = healer.HpCurr;
            yield return NordAction(healer, Kind.Ultimate, _ => healer.FillUltimateResource());
            Check("초월 치유18%·자신 추가30% 치유", ally.HpCurr - before == Mathf.RoundToInt(healer.HpMax * .18f) && healer.HpCurr - self == Mathf.RoundToInt(healer.HpMax * .18f) + Mathf.RoundToInt(healer.HpMax * .30f));
            DebugMode.AllyInvincible = false; OsFinish(enabled);
        }
    }
}
#endif
