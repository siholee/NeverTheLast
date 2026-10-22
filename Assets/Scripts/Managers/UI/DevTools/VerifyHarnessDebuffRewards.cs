#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive.Reward;
using Core;
using Entities.Status;
using Effects.Base;
using Effects.Buffs;
using Entities;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioDebuffRewardsCore(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); _cfActionIdSequence = 0;
            config.lineup = CampaignBuildCatalog().teams.Single(x => x.aliases.Contains("점검/사바흐_히미코")).key;
            yield return EnsureLineup(config);
            bool enabled = true;
            yield return YvPrepareScene(config, value => enabled = value);
            Unit a = CfAlly(3), tank = CfAlly(4), other = CfAlly(44), enemy = AliveEnemies().First();
            foreach (Unit unit in AliveAllies().Concat(AliveEnemies()))
            {
                foreach (var code in unit.ActivePassiveCodes.ToList()) code.StopCode();
                unit.ActiveUltimateCode?.StopCode();
                unit.ApplyRewardCodeInventory(Enumerable.Repeat(0, 7).ToList(), new List<int>());
                CfNoCrit(unit);
            }
            void Check(string name, bool ok, string actual = "") => YvCheck("디버프 보상 " + name, "현행 사양", actual, ok);
            void Equip(Unit holder, params int[] ids)
            {
                holder.ApplyRewardCodeInventory(ids.Concat(Enumerable.Repeat(0, 7 - ids.Length)).ToList(), new List<int>());
                foreach (int id in ids) YvCode(holder, id)?.CastCode();
            }
            BaseEffect Effect(Unit holder) => holder.CombatEffects().Single(e => e.GetType().DeclaringType == typeof(DebuffRewardCode));
            UnitStatus Grant(Unit source, Unit target, string key, BaseEffect effect = null, StatusCategory category = StatusCategory.Negative)
            {
                var status = BuffStatus.Create(99850, key, "검증 상태", source, target, effect ?? new DebuffProbe(),
                    duration: 2, category: category, stackPolicy: StatusStackPolicy.RefreshDuration);
                status.SkipEffectHitRoll = true;
                status.AffectedVfxPartId = Effects.Vfx.StatusAffectedVfx.SuppressPartId;
                target.AddStatus(status);
                return target.GetAllStatuses().SingleOrDefault(s => s.Key == key);
            }
            void Clear() => enemy.RemoveAllNegativeStatuses();
            int Hit()
            {
                var hit = new DamageContext(a, 1000, CodeType.Normal, new List<int> { DamageTag.NormalAttack, DamageTag.SpecialDamage });
                enemy.TakeDamage(hit); return hit.ResolvedDamage;
            }
            Check("운영 정책·집요함은 비지속피해 디버퍼만", CampaignCodeValue(CfAlly(167), 668) <= 2 && CampaignCodeValue(CfAlly(1), 668) >= 70);
            foreach (int id in DebuffRewardCode.Ids)
            {
                Equip(a, id);
                Check(id + " YAML·팩토리·등급", YvCode(a, id) is DebuffRewardCode &&
                    RewardCodeCatalog.TierOf(id) == (id >= 900 ? 5 : id >= 800 ? 4 : id >= 700 ? 3 : 2));
            }
            Equip(a);
            var pool = RewardManager.Instance.BuildCodePool();
            var stock = (List<RewardDef>)typeof(Managers.UI.Screens.ShopScreen).GetMethod("BuildCodeStock",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, null);
            Check("8종 실제 추첨·상점", DebuffRewardCode.Ids.All(id => pool.Any(e => e.codeId == id) && stock.Any(e => e.codeId == id)));
            a.TryGrantRewardCode(667); a.TryGrantRewardCode(840); a.TryGrantRewardCode(918);
            Check("3단계 승급·하위 보관·역순 금지", a.RewardCodeIds.Contains(918) && !a.RewardCodeIds.Contains(667) &&
                !a.RewardCodeIds.Contains(840) && a.StoredRewardCodeIds.Contains(667) && a.StoredRewardCodeIds.Contains(840) && !a.CanReceiveRewardCode(667, out _, out _, out _));
            string instance = a.ActiveRewardInstance(918).instanceId;
            var saved = new UnitSaveData { currentHP = a.HpCurr, ultimateResource = a.ManaCurr };
            a.WriteRunState(saved); Equip(a);
            a.RestoreRunState(JsonUtility.FromJson<UnitSaveData>(JsonUtility.ToJson(saved)));
            Check("저장 복원·활성/보관·인스턴스", a.RewardCodeIds.Contains(918) && a.StoredRewardCodeIds.Contains(667) && a.ActiveRewardInstance(918).instanceId == instance);
            Equip(a); a.TryGrantRewardCode(769); a.TryGrantRewardCode(841);
            Check("방호 승급·하위 비활성", a.RewardCodeIds.Contains(841) && !a.RewardCodeIds.Contains(769));
            Equip(a); Clear();
            Grant(a, enemy, "debuff_probe_mark", category: StatusCategory.Neutral);
            Equip(a, 667);
            var direct = new DamageContext(a, 1000, CodeType.Normal, new List<int> { DamageTag.NormalAttack, DamageTag.SpecialDamage });
            Check("중립 표식 제외", DebuffRewardCode.Debuffs(enemy) == 0 && Near(Effect(a).OutgoingDamageModifier(a, enemy, direct), 1));
            Grant(a, enemy, "debuff_probe_0");
            Check("T2 직접 피해18%", Near(Effect(a).OutgoingDamageModifier(a, enemy, direct), 1.18f));
            Equip(a); int normal = Hit(); Equip(a, 667); int boosted = Hit();
            Check("실제 TakeDamage 경로18%", Mathf.Abs(boosted - normal * 1.18f) <= 2, normal + "/" + boosted);
            var dot = new DamageContext(a, 1000, CodeType.Effect, new List<int>()) { IsDamageOverTime = true };
            Check("DoT·타인 공격 제외", Near(Effect(a).OutgoingDamageModifier(a, enemy, dot), 1) &&
                Near(Effect(a).OutgoingDamageModifier(other, enemy, direct), 1));
            for (int i = 1; i < 9; i++) Grant(a, enemy, "debuff_probe_" + i);
            Equip(a); int normalAtCap = Hit();
            Equip(a, 840);
            Check("T4 상한42%·방어스탯12%무시", Near(Effect(a).OutgoingDamageModifier(a, enemy, direct), 1.42f) && Near(Effect(a).DefenseStatMultiplierModifier(a, enemy, direct), .88f));
            float armor = UnitStats.ArmorConstant(a.Level), defense = enemy.SpDefCurr;
            int ignored = Hit();
            Check("실제 특수피해·특방 무시 경로", Mathf.Abs(ignored - normalAtCap * 1.42f * (armor + defense) / (armor + defense * .88f)) <= 3, normalAtCap + "/" + ignored);
            Equip(a, 918);
            Check("T5 상한60%·방어스탯24%무시", Near(Effect(a).OutgoingDamageModifier(a, enemy, direct), 1.60f) && Near(Effect(a).DefenseStatMultiplierModifier(a, enemy, direct), .76f));
            direct.IsSharedDamage = true;
            Check("분담 피해 제외", Near(Effect(a).OutgoingDamageModifier(a, enemy, direct), 1)); direct.IsSharedDamage = false;
            Clear();
            Check("정화 즉시 피증·무시 해제", Near(Effect(a).OutgoingDamageModifier(a, enemy, direct), 1) && Near(Effect(a).DefenseStatMultiplierModifier(a, enemy, direct), 1));
            Equip(a, 668);
            Check("비제어 디버프2→3", Grant(a, enemy, "debuff_duration").Duration == 3);
            Check("재부여 비누적3", Grant(a, enemy, "debuff_duration").Duration == 3);
            Check("제어·DoT·중립·타인·아군 강화 제외",
                Grant(a, enemy, "debuff_control", new DebuffControlProbe()).Duration == 2 &&
                Grant(a, enemy, "debuff_dot", new DebuffDotProbe()).Duration == 2 &&
                Grant(a, enemy, "debuff_neutral", category: StatusCategory.Neutral).Duration == 2 &&
                Grant(other, enemy, "debuff_foreign").Duration == 2 &&
                Grant(a, a, "debuff_friendly", category: StatusCategory.Positive).Duration == 2);
            Equip(a); Clear();
            Equip(tank, 769); tank.RemoveShield(tank.ShieldCurr);
            Grant(tank, enemy, "debuff_shield"); int shield = tank.ShieldCurr;
            Grant(tank, enemy, "debuff_shield"); Grant(tank, enemy, "debuff_shield2");
            Check("자기 방100%·광역/갱신 재사용 대기", shield == Mathf.RoundToInt(tank.DefCurr) && tank.ShieldCurr == shield);
            tank.BeginTurn(); Grant(tank, enemy, "debuff_shield");
            Check("소유자 차례 뒤 재개", tank.ShieldCurr == shield * 2);
            Equip(tank, 841); foreach (Unit unit in AliveAllies()) unit.RemoveShield(unit.ShieldCurr);
            Grant(other, enemy, "debuff_foreign");
            Check("타인 부여로 방호 미발동", AliveAllies().All(u => u.ShieldCurr == 0));
            Grant(tank, enemy, "debuff_party");
            Check("방80% 전원 방호", AliveAllies().All(u => u.ShieldCurr == Mathf.RoundToInt(tank.DefCurr * .8f)));
            Equip(tank); Equip(a, 771); float before = a.SpAtkCurr;
            Equip(other, 841);
            Check("보상 보호막 우수한 방어 능력 조회", Combat.ShieldCapability.StrongestBaseAmount(other) == YvCode(other, 841).BaseShieldAmount && YvCode(other, 841).BaseShieldAmount > 0);
            Equip(other);
            Check("보관/해제 뒤 보호막 능력 없음", Combat.ShieldCapability.StrongestBaseAmount(other) == 0);
            Grant(a, enemy, "debuff_insight"); float after = a.SpAtkCurr;
            Grant(a, enemy, "debuff_insight");
            Check("통찰 특공20%·갱신 비중첩", after > before && Near(a.SpAtkCurr, after) && a.GetAllStatuses().Any(s => s.StatusName == "잔혹한 통찰" && s.Duration == 2));
            Equip(a); Check("통찰 정지 스탯 원복", Near(a.SpAtkCurr, before));
            Clear(); Equip(tank, 770);
            var incoming = new DamageContext(enemy, 1000, CodeType.Normal, new List<int>());
            Check("포위망 무디버프1", Near(Effect(tank).ReceivingDamageModifier(tank, incoming), 1));
            for (int i = 0; i < 8; i++) Grant(other, enemy, "debuff_incoming" + i);
            Check("포위망5개상한20%", Near(Effect(tank).ReceivingDamageModifier(tank, incoming), .8f));
            Clear(); Check("포위망 정화 즉시 해제", Near(Effect(tank).ReceivingDamageModifier(tank, incoming), 1));
            Equip(tank, 769); tank.RemoveShield(tank.ShieldCurr);
            YvAddTestStatus(enemy, 99871, "debuff_resist", new YvResistAllEffect());
            Grant(tank, enemy, "debuff_rejected");
            Check("저항 성공 부여 없음·보호막 없음", !enemy.HasStatusKey("debuff_rejected") && tank.ShieldCurr == 0);
            enemy.RemoveStatusByKey("debuff_resist"); Equip(tank); Grant(tank, enemy, "debuff_after_stop");
            Check("정지 구독 정리", tank.ShieldCurr == 0);
            foreach (Unit unit in AliveAllies()) Equip(unit);
            DebugMode.AllyInvincible = false;
            OsFinish(enabled);
        }
        private sealed class DebuffProbe : BaseEffect { public DebuffProbe() : base(0) { } }
        private sealed class DebuffControlProbe : BaseEffect { public DebuffControlProbe() : base(0) { } public override bool IsControl => true; }
        private sealed class DebuffDotProbe : BaseEffect { public DebuffDotProbe() : base(0) { } public override bool IsDamageOverTime => true; }
    }
}
#endif
