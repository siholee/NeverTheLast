#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Core;
using Entities;
using UnityEngine;

namespace Managers.UI.DevTools
{
    /// <summary>
    /// 12차 기획 구현(보상 코드 · 영구 중첩 · 소환수 · 로카팔라 4인 · 아누비스 · 우치)의 <b>메커니즘</b> 검증 시나리오.
    /// 연출이 아니라 수치 · 순서 · 상태를 확인하고, 결과는 `[R12검증]` 로그 줄과 실패 메시지로 남긴다.
    /// 하네스 규칙상 시나리오마다 정지 캡처 한 장은 남긴다.
    /// </summary>
    public sealed partial class VerifyHarnessRunner
    {
        private const int IndraId = 63;
        private const int VayuId = 64;
        private const int KuberaId = 65;
        private const int AnubisId = 121;
        private const int UchiId = 180;
        private static readonly int[] PartyFiveLokapalaIds = { IndraId, VayuId, KuberaId, ChandraId, ShiId };
        private static readonly int[] PartyFiveSummonIds = { AnubisId, UchiId, KuberaId, PygmalionId, SeiId };

        private readonly List<string> _r12Failures = new();

        private void R12Log(string message) => Debug.Log($"[R12검증] {message}");

        private void R12Check(bool ok, string label)
        {
            R12Log((ok ? "통과 — " : "실패 — ") + label);
            if (!ok) _r12Failures.Add(label);
        }

        private void R12Finish()
        {
            if (_r12Failures.Count > 0) FailCurrent($"{_r12Failures.Count}건 실패: " + string.Join(" / ", _r12Failures));
            _r12Failures.Clear();
        }

        // ── 기반 · 보상 코드 ───────────────────────────────────────

        private IEnumerator ScenarioR12Core(VerifyScenarioRequest config)
        {
            _r12Failures.Clear();
            yield return PrepareStage(config, CombatStage(config, 6, 1), false, true, true);
            Unit sei = AliveAllies().FirstOrDefault(unit => unit.ID == SeiId);
            if (sei == null) { FailCurrent("세이를 찾지 못했다."); yield break; }

            // 영구 중첩: 0.25 × 8 = 2, 소수 몫 0
            const string key = "r12.test";
            for (int i = 0; i < 8; i++) sei.GainPermanentStacks(key, 0.25f);
            R12Check(sei.GetPermanentStacks(key) == 2 && sei.GetPermanentStackFraction(key) < 0.01f,
                $"영구 중첩 0.25×8 = {sei.GetPermanentStacks(key)} (+{sei.GetPermanentStackFraction(key):0.00})");
            var snapshot = new UnitSaveData();
            sei.WriteRunState(snapshot);
            R12Check(snapshot.permanentStacks.Any(stack => stack.key == key && stack.value == 2), "영구 중첩이 저장 칸에 실린다");

            // T1 깡 · T2 퍼센트 · 치명타 깡
            float atk0 = sei.StatValue(BaseEnums.StatType.Attack);
            R12Check(sei.TryGrantRewardCode(501), "501 장착");
            float atk1 = sei.StatValue(BaseEnums.StatType.Attack);
            R12Check(Mathf.Approximately(atk1 - atk0, 12f), $"전사의 투지 공격력 +12 ({atk0:0.#} → {atk1:0.#})");
            float baseAtk = sei.BaseStat(BaseEnums.StatType.Attack);
            R12Check(sei.TryGrantRewardCode(601), "601 장착");
            float atk2 = sei.StatValue(BaseEnums.StatType.Attack);
            R12Check(Mathf.Abs(atk2 - (baseAtk * 1.12f + 12f)) < 0.05f, $"거인의 완력 +12% ({atk2:0.##} = {baseAtk:0.#}×1.12+12)");
            float crit0 = sei.StatValue(BaseEnums.StatType.CritRate);
            R12Check(sei.TryGrantRewardCode(607), "607 장착");
            R12Check(Mathf.Abs(sei.StatValue(BaseEnums.StatType.CritRate) - crit0 - 0.10f) < 0.001f, "포르투나의 미소 치확 +10%p");

            // 상위 대체: 기수(629) → 도발의 문장(726)이 하위를 보관하고 활성 칸을 그대로 쓴다
            R12Check(sei.TryGrantRewardCode(629), "629 장착");
            int before = sei.RewardCodeIds.Count;
            R12Check(sei.TryGrantRewardCode(726) && !sei.RewardCodeIds.Contains(629) && sei.RewardCodeIds.Count == before,
                "도발의 문장이 기수를 대체(칸 수 유지)");
            R12Check(!sei.TryGrantRewardCode(629), "상위 보유 중에는 하위를 받지 않는다");

            // 7활성 칸 · 초과분 보관
            foreach (int id in new[] { 502, 503, 504 }) sei.TryGrantRewardCode(id);
            R12Check(sei.RewardCodeIds.Count == Unit.RewardCodeSlotCount, $"7칸이 찼다({sei.RewardCodeIds.Count})");
            sei.CanReceiveRewardCode(505, out _, out bool needsOverwrite, out _);
            R12Check(!needsOverwrite && sei.TryGrantRewardCode(505) && sei.StoredRewardCodeIds.Contains(505), "칸이 차면 초과 코드는 보관한다");
            R12Check(sei.RewardCodeIds.Contains(504) && !sei.RewardCodeIds.Contains(505) && sei.RewardCodeIds.Count == 7,
                "504 활성 유지 · 보관 505 효과 미적용");

            // 승급의 인장(청동): 502 → 602 같은 칸
            int slot = sei.RewardCodeIds.ToList().IndexOf(502);
            R12Check(sei.TryReplaceRewardCode(502, 602) && sei.RewardCodeIds.ToList().IndexOf(602) == slot, "청동 인장: 502 → 602 같은 칸");

            // 저장 칸에 보상 코드가 실린다
            sei.WriteRunState(snapshot);
            R12Check(snapshot.grantedPassiveCodeIds.SequenceEqual(sei.RewardCodeIds), "보상 코드가 칸 순서대로 저장 칸에 실린다");

            // 공격 형태
            R12Check(AttackForms.PartyHas(DamageTag.Slash), "파티에 #베기(시)가 있다");
            R12Check(!AttackForms.PartyHas(DamageTag.Shot), "기본 편성에는 #사격이 없다");

            // 구현 수
            var catalog = RewardCodeCatalog.All().ToList();
            var missing = catalog.Where(entry => !Codes.Passive.Reward.RewardCodeFactory.IsImplemented(entry.id)).Select(entry => entry.id).ToList();
            R12Check(missing.Count == 0, $"보상 코드 구현 {catalog.Count - missing.Count}/{catalog.Count} · 누락 {string.Join(",", missing)}");

            // 보상 풀 분포(라운드 5, 300회)
            var rewards = GameManager.Instance.rewardManager;
            int code = 0, consumable = 0, drop = 0, sets = 300, setsWithCode = 0;
            for (int i = 0; i < sets; i++)
            {
                List<RewardDef> cards = rewards.GenerateRewards(3, 5, new[] { 1001, 1002 });
                if (cards.Any(card => card.IsCodeReward)) setsWithCode++;
                foreach (RewardDef card in cards)
                {
                    if (card.IsCodeReward) code++;
                    else if (card.item != null && !card.item.rewardPool) drop++;
                    else consumable++;
                }
            }
            int total = Mathf.Max(1, code + consumable + drop);
            R12Log($"보상 풀 {sets}세트: 코드 {code * 100 / total}% · 소모품 {consumable * 100 / total}% · 드랍 {drop * 100 / total}%");
            R12Check(setsWithCode == sets, $"모든 세트에 코드 1장 이상({setsWithCode}/{sets})");
            R12Check(code > consumable && consumable > 0 && drop > 0, "세 분류가 모두 나오고 코드가 가장 많다");

            yield return CaptureStill("r12_core");

            // 코드 카드가 실제 보상 화면에서 어떻게 보이는지 — 상위 표시(▲)가 뜨도록 세이가 가진 607의 사다리 대신
            // 하위를 가진 726의 상위가 없으니, 하위 614를 세이에게 주고 상위 723 카드를 함께 띄운다.
            sei.TryGrantRewardCode(614, sei.RewardCodeIds.First());
            var preview = new List<RewardDef>
            {
                RewardManager.BuildCodeReward(RewardCodeCatalog.Find(723)),
                RewardManager.BuildCodeReward(RewardCodeCatalog.Find(705)),
                RewardManager.BuildCodeReward(RewardCodeCatalog.Find(609)),
            };
            GameManager.Instance.uiManager.ShowRewardPanel(preview);
            yield return WaitUnscaled(0.8f);
            yield return CaptureStill("r12_reward_codes");
            GameManager.Instance.uiManager.HideRewardPanel();

            // TAB 캐릭터 창 — 세이의 보상 코드 7칸
            UIManager ui = GameManager.Instance.uiManager;
            ui.ToggleCodex();
            yield return WaitUnscaled(0.8f);
            yield return CaptureStill("r12_codex_slots");
            ui.ToggleCodex();

            // 상점 코드 탭 — 탭 전환은 화면 안의 private 경로라 검증에서만 리플렉션으로 연다.
            ui.ShowShopPanel();
            yield return WaitUnscaled(0.5f);
            const System.Reflection.BindingFlags Hidden = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            object shop = typeof(UIManager).GetField("_shop", Hidden)?.GetValue(ui);
            System.Type tabType = shop?.GetType().GetNestedType("Tab", System.Reflection.BindingFlags.NonPublic);
            shop?.GetType().GetMethod("SwitchTo", Hidden)?.Invoke(shop, new[] { System.Enum.ToObject(tabType, 2) });
            yield return WaitUnscaled(0.5f);
            yield return CaptureStill("r12_shop_codes");
            ui.HideShopPanel();
            R12Finish();
        }

        // ── 로카팔라 ────────────────────────────────────────────────

        private IEnumerator ScenarioR12Lokapala(VerifyScenarioRequest config)
        {
            _r12Failures.Clear();
            int stage = CombatStage(config, 6, 1);
            yield return PrepareStage(config, stage, false, true, true);

            // 금력 배율: 보유 골드 = 스테이지 × 100 × 5 → ×1.75
            InventoryManager inventory = GameManager.Instance.inventoryManager;
            inventory.RestoreGold(stage * 100 * 5);
            float multiplier = Codes.Special.KuberaGoldRush.GoldMultiplier();
            R12Check(Mathf.Abs(multiplier - 1.75f) < 0.001f, $"금력 5 → 골드 러쉬 ×{multiplier:0.00}");
            inventory.RestoreGold(stage * 100 * 40);
            R12Check(Mathf.Abs(Codes.Special.KuberaGoldRush.GoldMultiplier() - 4f) < 0.001f, "금력 상한 ×4");

            yield return PrepareStage(config, stage, true, true, true);
            Unit indra = AliveAllies().FirstOrDefault(unit => unit.ID == IndraId);
            Unit chandra = AliveAllies().FirstOrDefault(unit => unit.ID == ChandraId);
            if (indra == null || chandra == null) { FailCurrent("인드라 · 찬드라를 찾지 못했다."); yield break; }

            var specials = new List<int>();
            int moonHeals = 0;
            void OnStarted(Unit unit, ActionScheduler.ActionKind kind, string label)
            {
                if (kind == ActionScheduler.ActionKind.Special && unit != null && !unit.IsEnemy) specials.Add(unit.ID);
            }
            void OnHealed(Unit target, Unit source, int effective, int overflow)
            {
                if (source == chandra && target != null && !target.IsEnemy) moonHeals++;
            }
            ActionScheduler.AnyActionStarted += OnStarted;
            Unit.AnyHealed += OnHealed;
            // 아군에게 상처를 내 두어야 회복이 실제로 들어간다.
            foreach (Unit ally in AliveAllies()) ally.ModifyHp(Mathf.Max(1, ally.HpMax / 2));

            indra.FillUltimateResource();
            float deadline = Time.realtimeSinceStartup + 20f;
            while (specials.Count < 4 && Time.realtimeSinceStartup < deadline) yield return null;
            yield return WaitUnscaled(0.6f);
            ActionScheduler.AnyActionStarted -= OnStarted;
            Unit.AnyHealed -= OnHealed;

            string order = string.Join(" → ", specials.Select(id => id switch
            {
                VayuId => "바유", KuberaId => "쿠베라", IndraId => "인드라", ChandraId => "찬드라", _ => id.ToString(),
            }));
            R12Log($"묶음 순서: {order}");
            R12Check(specials.Take(4).SequenceEqual(new[] { VayuId, KuberaId, IndraId, ChandraId }), "묶음 순서 바유 → 쿠베라 → 인드라 → 찬드라");
            // 2026-10-05 바유 교체 — 태풍의 눈은 방깍 대신 일곱 원소 취약(+25%)을 건다(Detail_08 §14.5).
            R12Check(AliveEnemies().Any(enemy => Codes.Passive.VayuVulnerability.AllElements.Any(element =>
                enemy.HasStatusKey(Codes.Passive.VayuVulnerability.GaleKey(element)))), "태풍의 눈 이후 적에게 원소 취약");
            R12Check(moonHeals > 0, $"찬드라 회복 이벤트 {moonHeals}회(달빛 순환 · 창야 분배)");

            yield return CaptureStill("r12_lokapala");
            R12Finish();
        }

        // ── 소환 · 영혼 ─────────────────────────────────────────────

        private IEnumerator ScenarioR12Summon(VerifyScenarioRequest config)
        {
            _r12Failures.Clear();
            // 분신은 속도 70이라 우치 행동 두세 번 뒤에야 제 차례가 온다. 연출 시간을 줄이려 4배속으로 돌린다.
            yield return PrepareStage(config, CombatStage(config, 6, 1), true, true, true, 4f);
            Unit anubis = AliveAllies().FirstOrDefault(unit => unit.ID == AnubisId);
            Unit uchi = AliveAllies().FirstOrDefault(unit => unit.ID == UchiId);
            Unit kubera = AliveAllies().FirstOrDefault(unit => unit.ID == KuberaId);
            if (anubis == null || uchi == null || kubera == null) { FailCurrent("아누비스 · 우치 · 쿠베라를 찾지 못했다."); yield break; }

            int created = 0, summonDeaths = 0, maxAlive = 0, uchiTurns = 0;
            void OnCreated(Unit owner, Unit summon, SummonSource source, object tag) { if (owner == uchi) created++; }
            void OnDied(Unit dead, Unit attacker) { if (dead != null && dead.IsSummon) summonDeaths++; }
            void OnStarted(Unit unit, ActionScheduler.ActionKind kind, string label)
            {
                if (unit == uchi && kind == ActionScheduler.ActionKind.Normal) uchiTurns++;
            }
            Summons.AnySummonCreated += OnCreated;
            Unit.AnyUnitDied += OnDied;
            ActionScheduler.AnyActionStarted += OnStarted;

            float soulStart = anubis.GetPermanentStacks(Codes.Passive.AnubisSoulHarvest.SoulKey) +
                              anubis.GetPermanentStackFraction(Codes.Passive.AnubisSoulHarvest.SoulKey);
            int invalidSummonIncome = 0;
            void TreasuryChanged(Unit source, int delta) { if (delta > 0 && (source == uchi || source.IsSummon)) invalidSummonIncome += delta; }
            PartyTreasury.Changed += TreasuryChanged;
            float deadline = Time.realtimeSinceStartup + 50f;
            while (Time.realtimeSinceStartup < deadline && uchiTurns < 10)
            {
                maxAlive = Mathf.Max(maxAlive, Summons.Of(uchi).Count);
                yield return null;
            }
            yield return WaitUnscaled(0.5f);
            Summons.AnySummonCreated -= OnCreated;
            Unit.AnyUnitDied -= OnDied;
            ActionScheduler.AnyActionStarted -= OnStarted;

            float soulEnd = anubis.GetPermanentStacks(Codes.Passive.AnubisSoulHarvest.SoulKey) +
                            anubis.GetPermanentStackFraction(Codes.Passive.AnubisSoulHarvest.SoulKey);
            R12Log($"우치 행동 {uchiTurns} · 분신 생성 {created} · 분신 소멸 {summonDeaths} · 동시 최대 {maxAlive} · 영혼 {soulStart:0.00} → {soulEnd:0.00} · 소환 출처 재보 {invalidSummonIncome}");
            R12Check(created > 0 && summonDeaths > 0, "분신이 생기고 사라진다");
            R12Check(maxAlive <= 3, $"분신 상한 3 유지(최대 {maxAlive})");
            R12Check(Mathf.Abs(soulEnd - soulStart - summonDeaths * 0.25f) < 0.01f, "분신 소멸 1기당 영혼 0.25");
            PartyTreasury.Changed -= TreasuryChanged;
            R12Check(invalidSummonIncome == 0, "분신 생성/소멸이 재보를 생산하지 않는다");
            R12Check(anubis.StatValue(BaseEnums.StatType.MaxHp) >= anubis.BaseStat(BaseEnums.StatType.MaxHp) +
                     8f * anubis.GetPermanentStacks(Codes.Passive.AnubisSoulHarvest.SoulKey) - 1f, "영혼 1당 최대 체력 +8");

            yield return CaptureStill("r12_summon");
            R12Finish();
        }
    }
}
#endif
