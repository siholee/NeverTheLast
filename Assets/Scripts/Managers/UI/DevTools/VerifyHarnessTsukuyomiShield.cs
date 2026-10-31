#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using BaseClasses;
using Codes.Passive;
using Codes.Ultimate;
using Core;
using Effects.Buffs;
using Entities;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioTsukuyomiShieldCore(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); bool enabled = true;
            config.lineup = CampaignBuildCatalog().teams.Single(s => s.aliases.Contains("검증 편성/b48_dot_active")).key;
            yield return EnsureLineup(config); yield return YvPrepareScene(config, v => enabled = v);
            var moon = CfAlly(45); var varuna = CfAlly(66); var friend = CfAlly(61); var enemy = AliveEnemies().First();
            foreach (var u in AliveAllies().Concat(AliveEnemies()))
            { foreach (var p in u.ActivePassiveCodes) p.StopCode(); u.ActiveUltimateCode?.StopCode(); u.RemoveShield(u.ShieldCurr); CfNoCrit(u); }
            var night = moon.ActivePassiveCodes.OfType<TsukuyomiNight>().Single();
            void Check(string label, bool ok, string actual = "") => YvCheck("츠쿠요미 보호막 " + label, "공통 보호막·정규 방호", actual, ok);
            foreach (int level in new[] { 1, 14, 50, 500 })
            {
                moon.DebugSetLevel(level); varuna.DebugSetLevel(level);
                int opening = VarunaLawLevee.ShieldAmount(moon, TsukuyomiNight.OpeningShieldRatio);
                int ultimate = VarunaLawLevee.ShieldAmount(moon, TsukuyomiMoonlitPact.PartyShieldRatio);
                int dedicated = VarunaLawLevee.ShieldAmount(varuna, VarunaUnderSameLaw.PartyShieldRatio);
                Check($"Lv{level} 정규 방호 개전300%·초월550%", opening >= dedicated && ultimate > opening,
                    $"개전{opening}/초월{ultimate}/바루나{dedicated}");
            }
            moon.DebugSetLevel(14); varuna.DebugSetLevel(14);
            var allies = AliveAllies().ToList();
            using (var log = new YvRecorder())
            {
                night.CastCode(); int opening = VarunaLawLevee.ShieldAmount(moon, TsukuyomiNight.OpeningShieldRatio);
                Check("개전 전원 자신 포함·적 제외", allies.All(u => u.ShieldCurr == opening) && enemy.ShieldCurr == 0, opening.ToString());
                Check("생존 필드 대상당 공통 부여 이벤트 1회", log.Shields.Count(s => s.Source == moon) == allies.Count);
                night.CastCode(); moon.Invoke(UnitEventType.OnRoundStart, new EventContext(moon));
                Check("중복 활성·개전 알림 재발행 중복 방지", log.Shields.Count(s => s.Source == moon) == allies.Count);
                night.StopCode();
                Check("오라 종료", Near(TsukuyomiNight.DotMultiplier(friend), 1f));
                foreach (var u in allies) u.RemoveShield(u.ShieldCurr);
                YvAddTestStatus(moon, 99876, "moon_ward_def", new StatModifierEffect(StatType.Defense, 100));
                TsukuyomiNight.ShieldParty(moon, .5f);
                Check("현재 방어력 증가 반영", friend.ShieldCurr == VarunaLawLevee.ShieldAmount(moon, .5f));
                moon.RemoveStatusByKey("moon_ward_def");
                foreach (var u in allies) u.RemoveShield(u.ShieldCurr);
                float sp = moon.StatValue(StatType.SpecialAttack), allySp = friend.StatValue(StatType.SpecialAttack);
                yield return NordAction(moon, ActionScheduler.ActionKind.Ultimate, _ => moon.FillUltimateResource());
                int ward = VarunaLawLevee.ShieldAmount(moon, TsukuyomiMoonlitPact.PartyShieldRatio);
                Check("실제 초월기 전원 방어력550%", allies.All(u => u.ShieldCurr == ward), ward.ToString());
                Check("기존 특공20% 지원 유지", Near(friend.StatValue(StatType.SpecialAttack) - allySp, sp * .2f, .1f));
                yield return NordAction(moon, ActionScheduler.ActionKind.Ultimate, _ => moon.FillUltimateResource());
                Check("재시전 공통 누적·특공 자가증폭 방지", allies.All(u => u.ShieldCurr == ward * 2) && Near(moon.StatValue(StatType.SpecialAttack), sp * 1.2f, .1f));
                foreach (var u in allies) u.RemoveShield(u.ShieldCurr);
                friend.AddShield(1000, moon); int hp = friend.HpCurr, shield = friend.ShieldCurr;
                DebugMode.AllyInvincible = false;
                friend.TakeDamage(new DamageContext(enemy, 100, CodeType.Normal, new() { DamageTag.NormalAttack, DamageTag.NonContactAttack }));
                Check("직접 피해 흡수", friend.HpCurr == hp && friend.ShieldCurr < shield);
                shield = friend.ShieldCurr; hp = friend.HpCurr;
                friend.TakeDamage(new DamageContext(enemy, 10, CodeType.Effect, new()) { IsDamageOverTime = true });
                Check("지속피해 우회 공통 규칙 유지", friend.ShieldCurr == shield && friend.HpCurr < hp);
                DebugMode.AllyInvincible = true;
                night.CastCode(); int count = log.Shields.Count;
                moon.Invoke(UnitEventType.OnDeath, new EventContext(moon));
                night.StopCode();
                Check("사망 정리 뒤 개전 구독 없음", log.Shields.Count == count && Near(TsukuyomiNight.DotMultiplier(friend), 1f));
                night.StopCode();
            }
            DebugMode.AllyInvincible = false; OsFinish(enabled);
        }
    }
}
#endif
