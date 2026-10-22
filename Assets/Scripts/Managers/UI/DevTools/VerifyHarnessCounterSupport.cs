#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BaseClasses;
using Codes.Base;
using Codes.Normal;
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
using Kind = Managers.ActionScheduler.ActionKind;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioBastetSustainCore(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); _cfActionIdSequence = 0; bool enabled = true;
            yield return YvPrepareScene(config, v => enabled = v);
            DebugMode.AllyInvincible = DebugMode.EnemyInvincible = false;
            Unit jean = CfAlly(7), bastet = CfAlly(122), orion = CfAlly(22), enemy = AliveEnemies().First();
            foreach (Unit unit in AliveAllies().Concat(AliveEnemies()))
            {
                foreach (var code in unit.ActivePassiveCodes.ToList()) code.StopCode();
                unit.ActiveUltimateCode?.StopCode(); CfNoCrit(unit); unit.RemoveShield(unit.ShieldCurr);
                if (!unit.IsEnemy) unit.ApplyRewardCodeInventory(Enumerable.Repeat(0, 7).ToList(), new List<int>());
            }
            jean.ActivePassiveCodes.OfType<JeanIronSanction>().Single().CastCode();
            bastet.ActiveUltimateCode.CastCode();
            YvAddTestStatus(jean, 99850, "counter_priority", new TargetPriorityModifierEffect(9999));
            CfEnsureEffectHit(jean, AliveEnemies());
            var heals = new List<(Unit target, int total)>();
            void Healing(Unit target, Unit source, int effective, int overflow)
            { if (source == bastet) heals.Add((target, effective + overflow)); }
            void Check(string name, bool ok, string actual = "") => YvCheck("바스테트 유지력 " + name, "실제 후속 본타 피해50%·원 반격자", actual, ok);
            Unit.AnyHealed += Healing;
            try
            {
                Check("전열2인 이상 슈퍼 히어로 제외", CampaignCodeValue(jean, 748) == 1);
                jean.ModifyHp(jean.HpMax / 2);
                using (var log = new YvRecorder())
                {
                    yield return NordAction(enemy, Kind.Normal, s => s.AdvanceAction(enemy, 1));
                    var hit = log.Dealt.Single(x => x.Attacker == bastet && x.DamageContext.HasTag(DamageTag.AdditionalAttack) && !x.DamageContext.HasTag(DamageTag.BonusDamage));
                    Check("자연 반격 후속 치유1회", heals.Count == 1 && heals[0].target == jean && heals[0].total == Mathf.RoundToInt(hit.DamageDealt * .5f));
                }
                heals.Clear();
                yield return NordAction(bastet, Kind.Normal, s => s.AdvanceAction(bastet, 1));
                Check("일반공격에는 치유 없음", heals.Count == 0);
                jean.ActivePassiveCodes.OfType<JeanIronSanction>().Single().StopCode();
                orion.ModifyHp(orion.HpMax / 2);
                using (var log = new YvRecorder())
                {
                    yield return NordAction(orion, Kind.PriorityAdditional, s => s.EnqueuePriorityAdditional(orion, "qa_counter_source", "반격 출처 검증", () =>
                    {
                        for (int i = 0; i < 3; i++) enemy.TakeDamage(new DamageContext(orion, 10, BaseEnums.CodeType.Normal,
                            new List<int> { DamageTag.NormalAttack, DamageTag.AdditionalAttack, DamageTag.CounterAttack }));
                    }));
                    Check("다단 반격1회·오리온 귀속", heals.Count == 1 && heals[0].target == orion &&
                        log.Dealt.Count(h => h.Attacker == bastet) == 1);
                }
                heals.Clear();
                bastet.ApplyRewardCodeInventory(new[] { 756, 0, 0, 0, 0, 0, 0 }.ToList(), new List<int>());
                YvCode(bastet, 756)?.CastCode();
                using (var log = new YvRecorder())
                {
                    yield return NordAction(bastet, Kind.PriorityAdditional, s => s.EnqueuePriorityAdditional(bastet, "qa_bonus_exclude", "후속 피해 검증",
                        () => ((BastetCatPunch)bastet.ActiveNormalCode).CastAdditional(enemy, orion)));
                    var hit = log.Dealt.Single(h => h.Attacker == bastet && !h.DamageContext.HasTag(DamageTag.BonusDamage));
                    Check("파생 고정 피해 중복 회복 제외", log.Dealt.Count(h => h.Attacker == bastet) > 1 && heals.Count == 1 &&
                        heals[0].total == Mathf.RoundToInt(hit.DamageDealt * .5f));
                }
                bastet.ApplyRewardCodeInventory(Enumerable.Repeat(0, 7).ToList(), new List<int>());
                heals.Clear(); orion.Die(enemy);
                yield return NordAction(bastet, Kind.PriorityAdditional, s => s.EnqueuePriorityAdditional(bastet, "qa_no_revive", "사망 반격자 검증",
                    () => ((BastetCatPunch)bastet.ActiveNormalCode).CastAdditional(enemy, orion)));
                Check("사망한 반격자 치유·부활 없음", heals.Count == 0 && !orion.isActive);
                heals.Clear(); enemy.ModifyHp(4);
                yield return NordAction(bastet, Kind.PriorityAdditional, s => s.EnqueuePriorityAdditional(bastet, "qa_kill_heal", "처치 후 회복 검증",
                    () => ((BastetCatPunch)bastet.ActiveNormalCode).CastAdditional(enemy, jean)));
                Check("후속 처치 후에도 실제 피해만 회복", !enemy.isActive && heals.Count == 1 && heals[0].total == 2);
                var grid = GridManager.Instance;
                foreach (Unit unit in AliveAllies().Where(u => u != jean && u.currentCell?.xPos == grid.GetFrontColumn(false)).ToList())
                    grid.RelocateUnit(unit, grid.Field.GetComponentsInChildren<Cell>(true)
                        .First(cell => cell.xPos == grid.GetRearColumn(false) && !cell.isOccupied));
                Check("단독 전열 슈퍼 히어로 평가", CampaignCodeValue(jean, 748) == 106);
            }
            finally { Unit.AnyHealed -= Healing; OsFinish(enabled); }
        }

        private IEnumerator ScenarioSeiShield68Core(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); bool enabled = true;
            foreach (bool legacy in new[] { true, false })
            {
                yield return YvPrepareScene(config, v => enabled = v);
                DebugMode.AllyInvincible = DebugMode.EnemyInvincible = false;
                var levels = new Dictionary<int, int> { [1] = 33, [7] = 34, [122] = 36, [162] = 36, [168] = 36 };
                foreach (Unit unit in AliveAllies().Concat(AliveEnemies()))
                {
                    foreach (var code in unit.ActivePassiveCodes.ToList()) code.StopCode();
                    unit.ActiveUltimateCode?.StopCode(); CfNoCrit(unit); unit.RemoveShield(unit.ShieldCurr);
                    if (levels.TryGetValue(unit.ID, out int level))
                    {
                        unit.ApplyRewardCodeInventory(Enumerable.Repeat(0, 7).ToList(), new List<int>());
                        unit.DebugSetLevel(level);
                    }
                }
                Unit sei = CfAlly(1), astolfo = CfAlly(162), nicol = CfAlly(168), boss = AliveEnemies().Single(u => u.ID == 3202);
                sei.ApplyRewardCodeInventory(new[] { 603, 624, 0, 0, 0, 0, 0 }.ToList(), new List<int>());
                foreach (int id in new[] { 603, 624 }) YvCode(sei, id)?.CastCode();
                EnemyCombatBudget.Apply(boss);
                foreach (Unit unit in AliveAllies()) unit.RestoreHpOutsideCombat(unit.HpMax);
                yield return NordAction(sei, Kind.Ultimate, _ => sei.FillUltimateResource());
                if (legacy)
                {
                    int oldShield = Mathf.RoundToInt(300 + sei.Level * 12 + sei.DefCurr * 1.8f);
                    foreach (Unit unit in AliveAllies()) { unit.RemoveShield(unit.ShieldCurr); unit.AddShield(oldShield, sei); }
                }
                int shield = astolfo.ShieldCurr;
                int hpA = astolfo.HpCurr, hpN = nicol.HpCurr;
                var finalDamage = typeof(Unit).GetMethod("CalculateFinalDamage", BindingFlags.Instance | BindingFlags.NonPublic);
                var probe = new DamageContext(boss, Mathf.RoundToInt(boss.AtkCurr * .8f), BaseEnums.CodeType.Ultimate,
                    new List<int> { DamageTag.AllTarget, DamageTag.UltAttack, DamageTag.NonContactAttack, DamageTag.Spell });
                int incomingA = (int)finalDamage.Invoke(astolfo, new object[] { probe });
                int incomingN = (int)finalDamage.Invoke(nicol, new object[] { probe });
                YvCheck("세이68층 집중 경감", "집중 활성·광역기 피해 사전 측정",
                    $"아스톨포{incomingA} · 니콜{incomingN}", Concentration.IsActive(sei, SeiSongOfGenesis.ConcentrationKey));
                using (var log = new YvRecorder())
                {
                    yield return NordAction(boss, Kind.Ultimate, _ => boss.FillUltimateResource());
                    bool bothHit = log.Dealt.Any(h => h.Target == astolfo) && log.Dealt.Any(h => h.Target == nicol);
                    YvCheck("세이68층 " + (legacy ? "종전 단일 시전 광역 즉사 대조" : "강화 단일 시전 광역 생존"),
                        legacy ? "후열 즉사" : "두 후열 모두 생존", $"보호막{shield} · 아스톨포{hpA}→{astolfo.HpCurr}(피해{incomingA}) · 니콜{hpN}→{nicol.HpCurr}(피해{incomingN})",
                        bothHit && (legacy ? !astolfo.isActive && !nicol.isActive : astolfo.isActive && nicol.isActive));
                    YvCheck("세이68층 실제 보스 예산", "공820·68층 예산·조건부 경감 없이 단일 초월기 보호막", boss.AtkCurr.ToString(),
                        boss.AtkCurr == 820 && boss.HasStatusKey(EnemyCombatBudget.StatusKey));
                }
            }
            OsFinish(enabled);
        }

        private IEnumerator ScenarioCounterIceCore(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); _cfActionIdSequence = 0;
            bool enabled = true;
            yield return YvPrepareScene(config, v => enabled = v);
            DebugMode.AllyInvincible = false; DebugMode.EnemyInvincible = false;
            Unit jean = CfAlly(7), bastet = CfAlly(122), sun = CfAlly(40), enemy = AliveEnemies().First();
            void Check(string label, bool ok, string actual = "") => YvCheck("반격 조정 " + label, "현행 사양", actual, ok);
            foreach (var unit in AliveAllies().Concat(AliveEnemies())) { CfNoCrit(unit); unit.ClearUltimateResource(); }
            YvAddTestStatus(jean, 99850, "counter_priority", new TargetPriorityModifierEffect(9999));
            CfEnsureEffectHit(jean, AliveEnemies());
            using (var log = new YvRecorder())
            {
                yield return NordAction(enemy, Kind.Normal, s => s.AdvanceAction(enemy, 1));
                var counters = log.Dealt.Where(x => x.Attacker == jean && x.DamageContext.HasTag(DamageTag.CounterAttack)).ToList();
                var follows = log.Dealt.Where(x => x.Attacker == bastet && x.DamageContext.HasTag(DamageTag.AdditionalAttack)).ToList();
                Check("잔160%·복합 태그", counters.Count == 1 && counters[0].DamageContext.Damage == Mathf.RoundToInt(jean.AtkCurr * 1.6f) && counters[0].DamageContext.HasTag(DamageTag.NormalAttack) && counters[0].DamageContext.HasTag(DamageTag.AdditionalAttack));
                Check("반격 얼음 부착1회", log.Elements.Count(x => x.Source == jean && x.Element == BaseEnums.UnitElement.Cryo) == 1);
                Check("바스테트 후속100%", follows.Count == 1 && follows[0].DamageContext.Damage == Mathf.RoundToInt(bastet.AtkCurr));
                Check("두 실제 추공 일광4", sun.GetCombatResource(AmaterasuRisingSun.ResourceKey) == 4);
            }
            using (var log = new YvRecorder())
            {
                yield return NordAction(jean, Kind.Normal, s => s.AdvanceAction(jean, 1));
                Check("일반125%·얼음 부착 없음", log.Dealt.Any(x => x.Attacker == jean && x.DamageContext.Damage == Mathf.RoundToInt(jean.AtkCurr * 1.25f)) && !log.Elements.Any(x => x.Source == jean));
            }
            enemy.ResetCombatElements(); enemy.GrantCombatElement(BaseEnums.UnitElement.Pyro, source: enemy); enemy.ClearUltimateResource();
            using (var log = new YvRecorder())
            {
                yield return NordAction(enemy, Kind.Normal, s => s.AdvanceAction(enemy, 1));
                Check("잔이 유발하는 융해", log.Reactions.Count(x => x.Source == jean && x.Name == "융해") == 1);
            }
            Check("얼음 명중 성장", Near(jean.Stats.BaseValue(BaseEnums.StatType.SpecialAttack), 78) && Near(jean.Stats.Growth(BaseEnums.StatType.SpecialAttack), 5f));
            OsFinish(enabled);
        }

        private IEnumerator ScenarioSupportCleanseCore(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); _cfActionIdSequence = 0;
            bool enabled = true;
            yield return YvPrepareScene(config, v => enabled = v);
            DebugMode.AllyInvincible = false; DebugMode.EnemyInvincible = false;
            Unit healer = CfAlly(82), shielder = CfAlly(83), a = CfAlly(7), b = CfAlly(122), enemy = AliveEnemies().First();
            foreach (var unit in AliveAllies())
            {
                foreach (var code in unit.ActivePassiveCodes) code.StopCode();
                if (unit.ActiveUltimateCode?.IsPersistent == true) unit.ActiveUltimateCode.StopCode();
                unit.ClearUltimateResource();
            }
            void Check(string label, bool ok, string actual = "") => YvCheck("T2 정화 " + label, "요청 사양", actual, ok);
            foreach (int id in new[] { 634, 635 }) Check("등록·T2·구현·상점 " + id, RewardCodeCatalog.TierOf(id) == 2 && RewardCodeFactory.IsImplemented(id) && RewardCodeCatalog.Find(id).shopFixed && RewardCodeCatalog.Find(id).shopPrice == 150);
            Check("보상 후보 두 코드", new[] { 634, 635 }.All(id => RewardManager.Instance.BuildCodePool().Any(x => x.codeId == id)));
            var stock = (List<RewardDef>)typeof(Managers.UI.Screens.ShopScreen).GetMethod("BuildCodeStock", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            Check("실제 상점 매대 생성", new[] { 634, 635 }.All(id => stock.Any(x => x.codeId == id)));
            var rng = UnityEngine.Random.state;
            var drawn = new HashSet<int>();
            UnityEngine.Random.InitState(16345);
            for (int i = 0; i < 1500 && drawn.Count < 2; i++)
                foreach (var reward in RewardManager.Instance.GenerateRewards(3, 2, Array.Empty<int>()))
                    if (reward.codeId is 634 or 635) drawn.Add(reward.codeId);
            UnityEngine.Random.state = rng;
            Check("실제 보상 추첨 출현", drawn.SetEquals(new[] { 634, 635 }));
            foreach (var pair in new[] { (id: 634, target: a), (id: 635, target: b) })
            {
                var reward = stock.Single(x => x.codeId == pair.id);
                int price = RewardManager.ShopPrice(reward, GameManager.Instance.RoundManager.Stage);
                GameManager.Instance.inventoryManager.AddGold(price);
                int left = GameManager.Instance.ShopPurchasesLeft;
                bool paid = GameManager.Instance.inventoryManager.TrySpendGold(price);
                bool granted = paid && RewardManager.Instance.ApplyRewardEffect(reward, pair.target);
                if (granted) GameManager.Instance.NotifyShopPurchase();
                Check("실제 구매·활성 칸 지급 " + pair.id, granted && pair.target.RewardCodeSlots.Contains(pair.id) && GameManager.Instance.ShopPurchasesLeft == left - 1);
                foreach (var code in pair.target.ActivePassiveCodes.OfType<SupportCleanseCode>()) code.StopCode();
            }
            Check("지원자 실제 코드 지급", healer.TryGrantRewardCode(634) && shielder.TryGrantRewardCode(635));
            var wind = healer.ActivePassiveCodes.OfType<SupportCleanseCode>().Single();
            var fortress = shielder.ActivePassiveCodes.OfType<SupportCleanseCode>().Single();
            wind.CastCode(); fortress.CastCode();
            int serial = 0;
            void Debuffs(Unit target)
            {
                target.RemoveAllNegativeStatuses(); target.ControlAppliedCount = 0;
                ControlStatuses.ApplyFreeze(target, enemy, 3, effectHitResolved: true);
                target.ControlAppliedCount = 0;
                ControlStatuses.ApplyFixedStun(target, enemy, 2, effectHitResolved: true);
                YvAddTestStatus(target, 99851, "support_slow", new StatModifierEffect(BaseEnums.StatType.Speed, -10), BaseEnums.StatusCategory.Negative);
                YvReactionDot(enemy, target);
            }
            bool HasControl(Unit target) => target.isControlled || target.GetAllStatuses().Any(x => x.CountsAsDebuff && x.IsControl);
            bool HasOther(Unit target) => target.GetAllStatuses().Any(x => x.CountsAsDebuff && !x.IsControl);
            void Hurt(Unit target) => target.ModifyHp(target.HpMax - 200);
            IEnumerator Support(Unit actor, Action action, Kind kind = Kind.Special)
            {
                yield return NordAction(actor, kind, s =>
                {
                    if (kind == Kind.Additional) s.EnqueueAdditional(actor, "cleanse_" + (++serial), "정화 검증", action);
                    else s.EnqueueSpecial(actor, "cleanse_" + (++serial), "정화 검증", action);
                });
            }
            Debuffs(a); Debuffs(b); Hurt(a); Hurt(b);
            yield return Support(healer, () => { a.Heal(100, healer); b.Heal(100, healer); });
            Check("서풍 광역 제어 전부·타이머 해제", !HasControl(a) && !HasControl(b) && HasOther(a) && HasOther(b));
            Debuffs(a); Hurt(a);
            yield return Support(healer, () => a.Heal(100, healer), Kind.Additional);
            Check("추가/특수행동 재사용 불가", HasControl(a));
            b.BeginTurn(); Hurt(a);
            yield return Support(healer, () => a.Heal(100, healer));
            Check("수혜자 턴은 재사용을 열지 않음", HasControl(a));
            healer.BeginTurn(); Hurt(a);
            yield return Support(healer, () => a.Heal(100, healer));
            Check("소유자 다음 턴 재사용", !HasControl(a) && HasOther(a));
            Debuffs(a); Debuffs(b);
            yield return Support(shielder, () => { a.AddShield(100, shielder); b.AddShield(100, shielder); });
            Check("보루 광역 비제어 전부·제어 보존", !HasOther(a) && !HasOther(b) && HasControl(a) && HasControl(b));
            Debuffs(a);
            yield return Support(shielder, () => a.AddShield(100, shielder));
            Check("보루 같은 턴 다른 행동 차단", HasOther(a));
            shielder.BeginTurn();
            yield return Support(shielder, () => a.AddShield(100, shielder));
            Check("보루 다음 소유자 턴 재사용", !HasOther(a) && HasControl(a));
            // 빈 정화는 사용권을 남기며, 체력이 가득 찬 아군도 치유가 도달하면 정화한다.
            healer.BeginTurn(); a.RemoveAllNegativeStatuses(); Hurt(a);
            yield return Support(healer, () => a.Heal(100, healer));
            Debuffs(a); a.RestoreHpOutsideCombat(a.HpMax);
            yield return Support(healer, () => a.Heal(100, healer));
            Check("체력 가득 찬 아군도 정화", !HasControl(a));
            healer.BeginTurn(); Debuffs(a);
            Hurt(a);
            yield return Support(shielder, () => a.Heal(100, shielder));
            Check("다른 시전자 치유 제외", HasControl(a));
            Hurt(a);
            yield return Support(healer, () => a.Heal(100, healer));
            Check("다른 시전자 지원은 사용권 보존", !HasControl(a));
            // 두 코드를 같은 보유자에게 장착해도 회복/보호막의 사용권은 독립이다.
            wind.StopCode(); fortress.StopCode();
            var sameOwnerFortress = new SupportCleanseCode(new PassiveCodeContext { Caster = healer }, control: false);
            wind.CastCode(); sameOwnerFortress.CastCode(); Debuffs(a); Hurt(a);
            yield return Support(healer, () => a.Heal(100, healer));
            yield return Support(healer, () => a.AddShield(100, healer));
            Check("두 코드 제한 독립", !HasControl(a) && !HasOther(a));
            wind.StopCode(); sameOwnerFortress.StopCode();
            Debuffs(a); Hurt(a);
            yield return Support(healer, () => { a.Heal(100, healer); a.AddShield(100, healer); });
            Check("정지 구독 정리", HasControl(a) && HasOther(a));
            wind.CastCode(); wind.CastCode(); Hurt(a);
            yield return Support(healer, () => a.Heal(100, healer));
            Check("재시작 정화·중복 구독 방지", !HasControl(a));
            wind.StopCode();
            OsFinish(enabled);
        }

        private IEnumerator ScenarioSupportCleanseSave(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();
            yield return BuildFreshRun("party5_amaterasu");
            yield return PrepareStage(config, 24, false, false, false, 5);
            Unit healer = CfAlly(82), shielder = CfAlly(83);
            bool granted = new[] { 634, 601, 602, 603, 604, 605, 606, 635 }.All(id => healer.TryGrantRewardCode(id)) && shielder.TryGrantRewardCode(635);
            YvCheck("정화 저장 fixture", "서풍 활성·보루 보관·별도 보루 활성", "", granted && healer.StoredRewardCodeInstances.Any(x => x.codeId == 635));
            string windId = healer.RewardCodeSlotInstances.First(x => x.codeId == 634).instanceId;
            string storedId = healer.StoredRewardCodeInstances.First(x => x.codeId == 635).instanceId;
            string shieldId = shielder.RewardCodeSlotInstances.First(x => x.codeId == 635).instanceId;
            RunSaveData save = JsonUtility.FromJson<RunSaveData>(JsonUtility.ToJson(RunManager.Instance.CaptureDebugSnapshot()));
            RunManager.Instance.RestoreDebugSnapshot(save);
            healer = CfAlly(82); shielder = CfAlly(83);
            YvCheck("정화 저장 복원 ID·인스턴스", "활성·보관 위치와 GUID 유지", "",
                healer.RewardCodeSlotInstances.Any(x => x.codeId == 634 && x.instanceId == windId) &&
                healer.StoredRewardCodeInstances.Any(x => x.codeId == 635 && x.instanceId == storedId) &&
                shielder.RewardCodeSlotInstances.Any(x => x.codeId == 635 && x.instanceId == shieldId));
            YvCheck("보관 정화 비활성", "활성 코드만 생성", "", healer.ActivePassiveCodes.OfType<SupportCleanseCode>().Count() == 1 && shielder.ActivePassiveCodes.OfType<SupportCleanseCode>().Count() == 1);
            OsFinish(true);
        }

        private IEnumerator ScenarioReferenceCycleCore(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); bool enabled = true;
            yield return YvPrepareScene(config, value => enabled = value);
            YvCheck("잔 이중 추천 데이터", "반격 연계/얼음 지휘 캐리", "", SynergyCatalog.RecommendationFor(7)?.best?.members?.Contains(122) == true && SynergyCatalog.RecommendationFor(7)?.basic?.members?.Contains(161) == true);
            var scheduler = GameManager.Instance.ActionScheduler;
            foreach (Unit unit in AliveAllies().Concat(AliveEnemies()))
            {
                foreach (var code in unit.ActivePassiveCodes) code.StopCode();
                unit.ActiveUltimateCode?.StopCode(); unit.ClearUltimateResource();
                YvAddTestStatus(unit, 99855, "reference_speed", new StatModifierEffect(BaseEnums.StatType.Speed, 100 - unit.StatValue(BaseEnums.StatType.Speed)));
            }
            int normal = 0, extras = 0; bool extraClockStable = true; float first = -1;
            void StartAction(Unit actor, Kind kind, string label)
            {
                if (kind == Kind.Normal && first < 0)
                {
                    first = scheduler.CombatActionValueElapsed;
                    scheduler.EnqueuePriorityAdditional(actor, "reference_priority", "측정 검증", () => { });
                    scheduler.EnqueueAdditional(actor, "reference_additional", "측정 검증", () => { });
                    scheduler.EnqueueSpecial(actor, "reference_special", "측정 검증", () => { });
                }
                else if (kind != Kind.Normal) extraClockStable &= Mathf.Approximately(scheduler.CombatActionValueElapsed, first);
            }
            void Finished(Unit actor, Kind kind) { if (kind == Kind.Normal) normal++; else extras++; }
            ActionScheduler.AnyActionStarted += StartAction; ActionScheduler.AnyActionFinished += Finished;
            try { yield return YvRunScheduled(null, () => normal >= 1 && extras >= 3); }
            finally { ActionScheduler.AnyActionStarted -= StartAction; ActionScheduler.AnyActionFinished -= Finished; }
            YvCheck("기준 라운드 속도100 첫 행동", "100AV=1라운드", first.ToString(), Mathf.Approximately(first, 100f) && Mathf.Approximately(scheduler.ReferenceCyclesElapsed, 1f));
            YvCheck("기준 라운드 추가·특수행동 불변", "일반1·별도3·AV100", normal + "/" + extras, normal == 1 && extras == 3 && extraClockStable && scheduler.TurnsTaken == 1);
            float before = scheduler.CombatActionValueElapsed;
            yield return WaitUnscaled(.25f);
            YvCheck("기준 라운드 벽시계 독립", "Tick 없는 대기에는 불변", "", scheduler.CombatActionValueElapsed == before);
            YvCheck("기준 라운드 목표 경계", "일반300·보스1000 허용·오차 초과 판정", "",
                !ActionScheduler.ExceedsReferenceCycleBudget(300.00003f, StageKind.Normal) && ActionScheduler.ExceedsReferenceCycleBudget(300.01f, StageKind.Normal) &&
                !ActionScheduler.ExceedsReferenceCycleBudget(1000, StageKind.Boss) && ActionScheduler.ExceedsReferenceCycleBudget(1000.01f, StageKind.Boss));
            scheduler.BeginRound();
            YvCheck("기준 라운드 새 전투 초기화", "AV·라운드0", "", scheduler.CombatActionValueElapsed == 0 && scheduler.ReferenceCyclesElapsed == 0);
            scheduler.EndRound(); OsFinish(enabled);
        }

        [Serializable]
        private sealed class CounterBalanceRecord
        {
            public int stage, seed, level, turns, sunDamage, jeanDamage, bastetDamage, teamDamage, sunUlts, sunHits, reactions, allyDeaths, overflow, remainder, manaMax;
            public string profile, variant, stageKind, enemyIds, partyIds;
            public bool won, finished, overCycleBudget;
            public int cycleBudget;
            public float seconds, averageStacks, actionValue, referenceCycles, enemyPressure;
        }
        private sealed class OldCounterCoefficient : BaseEffect
        {
            private readonly bool _jean;
            public OldCounterCoefficient(bool jean) : base(0) => _jean = jean;
            public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
                => _jean ? context.HasTag(DamageTag.CounterAttack) ? 1f / JeanHolyStrike.CounterRatio : 1f
                    : context.HasTag(DamageTag.AdditionalAttack) ? 1f / BastetCatPunch.FollowUpRatio : 1f;
        }
        private static int[] CounterBuild(Unit unit, string profile)
        {
            if (unit.ID == 40) return profile switch
            {
                "attack" => new[] { 601, 607, 608, 611, 614, 620 },
                "reaction" => new[] { 601, 605, 607, 608, 614, 618 },
                "charge" => new[] { 601, 607, 608, 614, 622, 606 },
                _ => new[] { 601, 605, 607, 608, 614, 622 },
            };
            return unit.ID switch
            {
                7 when profile == "jean" => new[] { 601, 607, 612, 615, 616, 629 },
                7 => new[] { 601, 602, 607, 608, 616, 605 },
                122 => new[] { 601, 607, 608, 612, 615, 606 },
                83 => new[] { 602, 603, 605, 606, 624, 635 },
                82 => new[] { 602, 604, 606, 623, 625, 634 },
                60 => new[] { 602, 603, 604, 606, 623, 634 },
                160 => new[] { 602, 604, 608, 606, 623, 634 },
                66 => new[] { 602, 603, 604, 605, 624, 635 },
                200 => new[] { 602, 603, 604, 606, 624, 635 },
                161 => new[] { 602, 603, 604, 606, 608, 622 },
                44 => new[] { 602, 604, 605, 606, 613, 614 },
                64 => new[] { 602, 604, 605, 606, 613, 614 },
                _ => new[] { 601, 602, 603, 604, 605, 606 },
            };
        }
        private IEnumerator ScenarioCounterSupportBalance(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();
            int stage = Stage(config, 24);
            bool old = config.trigger == "old_coefficients";
            bool jeanCarry = config.trigger == "jean_carry";
            bool manaTrial = config.trigger == "mana120";
            float enemyPressure = config.trigger == "pressure2" ? 2f : config.trigger == "pressure4" ? 4f : 1f;
            string[] profiles = old || jeanCarry || manaTrial || enemyPressure > 1 || config.trigger == "balanced" ? new[] { "balanced" } : new[] { "balanced", "attack", "reaction", "charge" };
            foreach (string profile in profiles)
            foreach (int seed in new[] { 1611, 1612 })
            {
                UnityEngine.Random.InitState(seed); // 런 생성·테마/적 편성부터 고정한다.
                yield return BuildFreshRun(jeanCarry ? "party5_jean_carry" : "party5_amaterasu");
                yield return PrepareStage(config, stage, false, false, false, 5);
                var game = GameManager.Instance;
                foreach (Unit unit in AliveAllies())
                {
                    bool granted = CounterBuild(unit, jeanCarry ? "jean" : profile).All(id => unit.TryGrantRewardCode(id));
                    int active = unit.RewardCodeSlots.Count(id => id > 0);
                    YvCheck("밸런스 실제 T2 여섯 칸 " + unit.ID, "6개 지급", active.ToString(), granted && active == 6);
                    unit.ClearUltimateResource(); unit.RestoreHpOutsideCombat(unit.HpMax);
                }
                Unit sun = CfAlly(40), jean = CfAlly(7), bastet = CfAlly(122);
                var kind = game.RoundManager.GetStageKind(stage);
                var record = new CounterBalanceRecord { stage = stage, seed = seed, level = jean.Level, profile = profile, variant = old ? "old_coefficients_same_elements" : jeanCarry ? "jean_carry" : manaTrial ? "mana120" : "current", manaMax = manaTrial ? 120 : sun?.ManaMax ?? 0, stageKind = kind.ToString(), cycleBudget = ActionScheduler.ReferenceCycleBudget(kind) };
                record.enemyIds = string.Join(",", AliveEnemies().Select(unit => unit.ID).OrderBy(id => id));
                record.partyIds = string.Join(",", AliveAllies().Select(unit => unit.ID).OrderBy(id => id));
                if (jeanCarry) { record.variant = "jean_command_carry"; record.profile = "counter"; }
                record.enemyPressure = enemyPressure;
                if (enemyPressure > 1) record.variant = config.trigger;
                if (manaTrial)
                {
                    typeof(Unit).GetField("manaMaxBase", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sun, 120);
                    sun.RefreshAttributes();
                }
                var gains = new HashSet<int>(); var bursts = new HashSet<int>(); int spent = 0;
                void Damage(DamageResolvedContext hit)
                {
                    var scheduler = game.ActionScheduler;
                    if (hit.Attacker == null || hit.Attacker.IsEnemy || hit.Target == null || !hit.Target.IsEnemy) return;
                    record.teamDamage += hit.DamageDealt;
                    if (hit.Attacker == sun)
                    {
                        record.sunDamage += hit.DamageDealt;
                        if (hit.DamageContext.CodeType == BaseEnums.CodeType.Ultimate)
                        {
                            record.sunHits++;
                            if (bursts.Add(scheduler.CurrentActionId))
                            {
                                int stacks = ((AmaterasuDaybreak)sun.ActiveUltimateCode).LastSpentStacks;
                                spent += stacks; record.remainder += stacks % 3; record.sunUlts++;
                            }
                        }
                    }
                    if (hit.Attacker == jean) record.jeanDamage += hit.DamageDealt;
                    if (hit.Attacker == bastet) record.bastetDamage += hit.DamageDealt;
                    if (sun != null && sun.isActive && hit.Attacker != sun && DirectAttack.IsDirect(hit.DamageContext) &&
                        hit.DamageContext.HasTag(DamageTag.AdditionalAttack) && scheduler.ExecutingUnit != null &&
                        ActionScheduler.IsAdditional(scheduler.ActingKind) && gains.Add(scheduler.CurrentActionId))
                        record.overflow += Mathf.Max(0, sun.GetCombatResource(AmaterasuRisingSun.ResourceKey) + 2 - 12);
                }
                void Reaction(Unit source, Unit target, string name) { if (source == sun && (name == "융해" || name == "증발")) record.reactions++; }
                void Death(Unit unit, Unit source) { if (!unit.IsEnemy && !unit.IsSummon) record.allyDeaths++; }
                Unit.AnyDamageDealt += Damage; Unit.AnyElementalReaction += Reaction; Unit.AnyUnitDied += Death;
                UnityEngine.Random.InitState(seed);
                float start = Time.realtimeSinceStartup;
                try
                {
                    DebugMode.SuiteRunning = false; game.StartRound();
                    if (enemyPressure > 1)
                        foreach (Unit enemy in AliveEnemies())
                            YvAddTestStatus(enemy, 99856, "enemy_pressure_probe", new ConditionalOutgoingDamageMultiplierEffect(_ => true, enemyPressure));
                    if (old)
                    {
                        YvAddTestStatus(jean, 99853, "counter_old_ratio", new OldCounterCoefficient(true));
                        YvAddTestStatus(bastet, 99854, "counter_old_ratio", new OldCounterCoefficient(false));
                    }
                    while (game.gameState == BaseEnums.GameState.RoundInProgress && Time.realtimeSinceStartup - start < 120)
                    { record.turns = game.ActionScheduler.TurnsTaken; yield return null; }
                }
                finally { Unit.AnyDamageDealt -= Damage; Unit.AnyElementalReaction -= Reaction; Unit.AnyUnitDied -= Death; DebugMode.SuiteRunning = true; }
                record.seconds = Time.realtimeSinceStartup - start;
                record.actionValue = game.ActionScheduler.CombatActionValueElapsed;
                record.referenceCycles = game.ActionScheduler.ReferenceCyclesElapsed;
                record.overCycleBudget = ActionScheduler.ExceedsReferenceCycleBudget(record.actionValue, kind);
                YvCheck("속도100 기준 라운드 환산", "누적AV/100·기존 통계 시간과 동일 축", record.referenceCycles.ToString("F3"),
                    Mathf.Approximately(record.referenceCycles, record.actionValue / 100f) && Mathf.Approximately(record.actionValue, game.ActionScheduler.CombatSecondsElapsed * ActionScheduler.ActionValuePerSecond));
                record.finished = game.gameState != BaseEnums.GameState.RoundInProgress;
                record.won = CombatLog.Current.LastOrDefault(e => e.Text.StartsWith("전투 종료 — ")).Text?.Contains("전투 종료 — 승리") == true;
                record.averageStacks = record.sunUlts > 0 ? spent / (float)record.sunUlts : 0;
                File.AppendAllText(Path.Combine(_requestDirectory, "counter-balance.jsonl"), JsonUtility.ToJson(record) + Environment.NewLine);
                YvCheck("자연 밸런스 실행 " + stage + "/" + profile + "/" + seed, "정상 종료·실측 기록", JsonUtility.ToJson(record), record.finished && record.teamDamage > 0);
                game.DebugResetBattle();
            }
            OsFinish(true);
        }
    }
}
#endif
