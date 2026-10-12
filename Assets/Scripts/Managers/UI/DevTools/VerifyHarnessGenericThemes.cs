#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using Core;
using Entities;
using Helpers;
using Managers.UI.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BaseClasses.BaseEnums;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioGenericThemes(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();
            yield return PrepareStage(config, 1, false, false, false);
            var game = GameManager.Instance;
            var themes = game.dataManager.FetchStageThemeDataList().stageThemes.Where(t => t.enabled && t.isGeneric).ToList();
            var enemies = game.dataManager.FetchEnemyDataList().enemies;
            RuiCheck("범용 운영 테마 4종", "4종·서사 없음", string.Join(",", themes.Select(t => t.name)),
                themes.Count == 4 && themes.All(t => t.isGeneric && t.eventStageInRound == 0));
            foreach (var theme in themes)
            {
                bool dataValid = theme.stagePatterns.Count == 8 && theme.stagePatterns.Select(p => p.stageInRound).OrderBy(n => n).SequenceEqual(Enumerable.Range(1, 8));
                foreach (var slot in theme.stagePatterns)
                    foreach (var pattern in slot.patterns)
                    {
                        var ids = (pattern.frontIds ?? new()).Concat(pattern.rearIds ?? new()).ToList();
                        dataValid &= ids.Count > 0 && (pattern.frontIds?.Count ?? 0) <= 4 && (pattern.rearIds?.Count ?? 0) <= 4;
                        dataValid &= ids.All(id => enemies.Any(e => e.id == id && e.themeId == theme.enemyThemeId && SpriteResource.LoadPortrait(e.portrait) != null));
                    }
                RuiCheck(theme.name + " 데이터·원화", "8슬롯·유효 편성·전 원화", dataValid.ToString(), dataValid);
                for (int stage = 1; stage <= 8; stage++)
                {
                    game.DebugResetBattle();
                    DebugMode.ForcedThemeId = theme.id;
                    game.DebugLoadStage(stage);
                    bool valid = AliveEnemies().Any() && AliveEnemies().All(u => enemies.Any(e => e.id == u.ID && e.themeId == theme.enemyThemeId));
                    StageKind kind = game.RoundManager.GetStageKind(stage);
                    valid &= kind == (stage == 6 ? StageKind.Elite : stage == 8 ? StageKind.Boss : StageKind.Normal);
                    valid &= game.gameState == GameState.Preparation;
                    RuiCheck(theme.name + " 실제 슬롯 " + stage, "전투·종류·테마 일치", kind + ":" + string.Join(",", AliveEnemies().Select(u => u.ID)), valid);
                    if (stage == 1) { yield return WaitUnscaled(.65f); yield return CaptureStill("theme_" + theme.id); }
                }
            }
            DebugMode.ForcedThemeId = 0;
            bool earlyOnlyGeneric = true;
            var drawn = new System.Collections.Generic.HashSet<int>();
            for (int seed = 0; seed < 64; seed++)
            {
                UnityEngine.Random.InitState(seed);
                var round = new RoundManager(game.dataManager);
                var fixture = game.dataManager.FetchStageThemeDataList();
                fixture.stageThemes.Add(new StageThemeData { id = 999, name = "검증 서사 테마", enabled = true });
                // 범용에서 서사로 향하는 치환/연작도 초반 제한을 넘지 못해야 한다.
                fixture.stageThemes.First(t=>t.id==2).rotationRedirectThemeId = 999;
                fixture.stageThemes.First(t=>t.id==3).chainNextThemeId = 999;
                typeof(RoundManager).GetField("_stageThemeDataList", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(round, fixture);
                round.InitializeStage(1);
                round.InitializeStage(9);
                drawn.Add(round.CurrentThemeId);
                earlyOnlyGeneric &= themes.Any(t => t.id == round.CurrentThemeId && t.isGeneric);
            }
            RuiCheck("초반 2스테이지 추첨", "64종자 전부 범용·4종 등장", string.Join(",", drawn), earlyOnlyGeneric && drawn.Count == 4);
            game.DebugLoadStage(8);
            RuiCheck("보스 예고 경계", "현재 보스 1칸만", game.RoundManager.GetUpcomingStageKinds(6).Count.ToString(), game.RoundManager.GetUpcomingStageKinds(6).Count == 1);
            game.DebugLoadStage(9);
            RuiCheck("스테이지 전환 1-8→2-1", "2,1", $"{game.RoundManager.Round},{game.RoundManager.StageInRound}", game.RoundManager.Round == 2 && game.RoundManager.StageInRound == 1);
            RunManager.Instance.SaveCurrentRun();
            var template = SaveSystem.LoadRun();
            foreach ((int before, int after) in new[] { (8,6), (10,8), (11,9), (20,16), (21,17) })
            {
                var save = JsonUtility.FromJson<RunSaveData>(JsonUtility.ToJson(template));
                save.version = 14; save.currentStage = before; save.currentRound = (before - 1) / 10 + 1; save.currentThemeId = 1;
                string heroesBefore = string.Join(";", save.heroUnits.Select(u => JsonUtility.ToJson(u)));
                SaveSystem.SaveRun(save);
                var loaded = SaveSystem.LoadRun();
                RuiCheck("v14 위치 복원 " + before, after + ":theme2:v15", $"{loaded?.currentStage}:{loaded?.currentThemeId}:v{loaded?.version}",
                    loaded != null && loaded.currentStage == after && loaded.currentThemeId == 2 && loaded.version == RunSaveData.CurrentVersion);
                RuiCheck("v14 유닛·코드 보존 " + before, "전체 DTO 동일", loaded.heroUnits.Count.ToString(),
                    heroesBefore == string.Join(";", loaded.heroUnits.Select(u => JsonUtility.ToJson(u))));
            }
            game.DebugResetBattle(); RuiFinish();
        }

        private IEnumerator ScenarioIdentityHud(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();
            yield return PrepareStage(config, 1, false, false, false);
            GameManager.Instance.DebugResetBattle(); DebugMode.ForcedThemeId = 3; GameManager.Instance.DebugLoadStage(Stage(config, 1));
            yield return WaitUnscaled(1f);
            var all = AliveAllies().Concat(AliveEnemies()).ToList();
            var banner = GameObject.Find("FeatureEnemyBanner");
            if(banner != null)
                RuiCheck("보스 체력 띠 이름 숨김", "이름 미표시·정보 호버", "숨김",
                    banner.GetComponentsInChildren<TextMeshProUGUI>(true).Where(t=>t.name.StartsWith("Name")).All(t=>!t.enabled));
            foreach(float ratio in new[] { 0f, .25f, .75f })
            {
                foreach(var unit in all)
                {
                    unit.ClearUltimateResource(); unit.AddUltimateResource(Mathf.RoundToInt(unit.ManaMax * ratio)); unit.currentCell?.Card?.Tick();
                }
                yield return WaitUnscaled(.15f);
                yield return CaptureStill("identity_charge_" + Mathf.RoundToInt(ratio * 100));
            }
            foreach (var unit in all)
            {
                var card = unit.currentCell?.Card;
                if (card == null) continue;
                unit.ClearUltimateResource(); unit.AddUltimateResource(unit.ManaMax / 2); card.Tick();
                yield return null;
                var name = card.GetComponentsInChildren<TextMeshPro>(true).FirstOrDefault(t => t.name == "Name");
                var gauge = card.GetComponentsInChildren<Image>(true).FirstOrDefault(t => t.name == "LiquidFront");
                var group = gauge?.GetComponentInParent<CanvasGroup>();
                bool passive = unit.UltimateResourceType == UltimateResourceType.Passive;
                RuiCheck(unit.ID + " 이름 숨김·원형 수위", "이름 비표시·하단부터 반충전·캔버스 표시", $"name={name?.enabled},fill={gauge?.fillAmount},alpha={group?.alpha}",
                    name != null && !name.enabled && gauge != null && gauge.fillMethod == Image.FillMethod.Vertical && gauge.fillOrigin == (int)Image.OriginVertical.Bottom && group.alpha > .9f &&
                    (passive || Mathf.Abs(gauge.fillAmount - unit.ManaCurr/(float)unit.ManaMax) < .01f));
                var ring = card.GetComponentsInChildren<RectTransform>(true).First(t => t.name == "UltGauge");
                var corners = new Vector3[4]; ring.GetWorldCorners(corners);
                bool inside = corners.All(c => { var p = Camera.main.WorldToViewportPoint(c); return p.z > 0 && p.x >= 0 && p.x <= 1 && p.y >= 0 && p.y <= 1; });
                var ringCanvas = ring.GetComponentInParent<Canvas>();
                var portrait = unit.currentCell.portraitRenderer;
                var expectedElement = Effects.Vfx.ElementPalette.Parse(unit.Element);
                if(expectedElement == UnitElement.None && unit.HasUnitTag("Void")) expectedElement = UnitElement.Void;
                var expectedColor = Effects.Vfx.ElementPalette.PastelColorFor(expectedElement);
                var icon = card.GetComponentsInChildren<Image>(true).First(t=>t.name == "Mark");
                RuiCheck(unit.ID + " 고유 원소색·모던 아이콘", "원소 팔레트·새 선형 세트", $"{unit.Element}:{gauge.color}:{icon.sprite.name}",
                    Vector4.Distance(gauge.color,expectedColor)<.01f && icon.sprite == Managers.UI.Theme.ModernCombatIcons.Element(expectedElement));
                RuiCheck(unit.ID + " 우상단 확대 게이지·화면 내부", "우상단·42단위 크기, 원화 뒤", $"anchor={ring.anchorMin},pivot={ring.pivot},pos={ring.anchoredPosition},size={ring.sizeDelta},inside={inside}",
                    ring.anchorMin == Vector2.one && ring.anchorMax == Vector2.one && ring.pivot == Vector2.one &&
                    ring.anchoredPosition == new Vector2(2, 2) && ring.sizeDelta == new Vector2(42, 42) && inside &&
                    SortingLayer.GetLayerValueFromID(ringCanvas.sortingLayerID) <= SortingLayer.GetLayerValueFromID(portrait.sortingLayerID) &&
                    (ringCanvas.sortingLayerID != portrait.sortingLayerID || ringCanvas.sortingOrder < portrait.sortingOrder));
                RuiCheck(unit.ID + " 한줄소개", "빈 설명 아님", UnitInfoText.Tagline(unit), !string.IsNullOrWhiteSpace(UnitInfoText.Tagline(unit)));
            }
            yield return CaptureStill("identity_half_charge");
            Unit ally = all.First(u => !u.IsEnemy);
            ally.currentCell.ShowHoverTooltip(); yield return WaitUnscaled(.25f);
            RuiCheck("아군 소개 툴팁", "소유자 일치", UITooltip.IsShowing(ally.currentCell).ToString(), UITooltip.IsShowing(ally.currentCell));
            yield return CaptureStill("identity_ally_tooltip", keepTooltip: true);
            UITooltip.HideAny();
            Unit enemy = all.First(u => u.IsEnemy);
            enemy.currentCell.ShowHoverTooltip(); yield return WaitUnscaled(.25f);
            yield return CaptureStill("identity_enemy_tooltip", keepTooltip: true);
            UITooltip.HideAny();
            foreach(var unit in all)
            {
                unit.FillUltimateResource(); unit.currentCell?.Card?.Tick();
                var gauge = unit.currentCell.Card.GetComponentsInChildren<Image>(true).First(t=>t.name == "LiquidFront");
                bool passive = unit.UltimateResourceType == UltimateResourceType.Passive;
                RuiCheck(unit.ID + " 완충 표시", "일반 자원 완충, 상시형 실제 자원 유지", gauge.fillAmount.ToString(), passive || Mathf.Abs(gauge.fillAmount-1f)<.01f);
            }
            yield return WaitUnscaled(.15f); yield return CaptureStill("identity_ready");
            // 기존 포인터 입력을 그대로 통과하는 실제 호버/이탈 확인. 물리 마우스는 잠시 격리한다.
            var priorMouse = UnityEngine.InputSystem.Mouse.current;
            bool priorEnabled = priorMouse != null && priorMouse.enabled;
            var pointer = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            if(priorEnabled) UnityEngine.InputSystem.InputSystem.DisableDevice(priorMouse);
            foreach(var input in _disabledPointerInputs) if(input != null) input.enabled = true;
            try
            {
                Vector2 position = Camera.main.WorldToScreenPoint(ally.currentCell.Card.CenterWorld);
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(pointer, new UnityEngine.InputSystem.LowLevel.MouseState { position = position });
                yield return WaitUnscaled(.35f);
                string pointerInfo = $"shown={UITooltip.IsShowing(ally.currentCell)},enabled={pointer.enabled},current={UnityEngine.InputSystem.Mouse.current == pointer},requested={position},read={pointer.position.ReadValue()},inputs={_disabledPointerInputs.Count(i=>i!=null&&i.enabled)}";
                RuiCheck("실제 마우스 호버", "카드 소개 표시", pointerInfo, UITooltip.IsShowing(ally.currentCell));
                yield return CaptureStill("identity_pointer_hover", keepTooltip: true);
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(pointer, new UnityEngine.InputSystem.LowLevel.MouseState { position = new Vector2(Screen.width-8, 8) });
                yield return WaitUnscaled(.15f);
                RuiCheck("마우스 이탈", "소개 숨김", UITooltip.IsShowing(ally.currentCell).ToString(), !UITooltip.IsShowing(ally.currentCell));
            }
            finally
            {
                foreach(var input in _disabledPointerInputs) if(input != null) input.enabled = false;
                UnityEngine.InputSystem.InputSystem.RemoveDevice(pointer);
                if(priorEnabled && priorMouse.added) UnityEngine.InputSystem.InputSystem.EnableDevice(priorMouse);
                UITooltip.HideAny();
            }
            GameManager.Instance.DebugResetBattle(); RuiFinish();
        }

        private IEnumerator ScenarioGenericCampaign(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();
            yield return BuildFreshRun(ResolveLineup(config));
            yield return PrepareStage(config, 1, false, false, false, 4f);
            GameManager game = GameManager.Instance;
            game.DebugResetBattle(); DebugMode.ForcedThemeId = Mathf.Clamp(config.stage, 2, 5); game.DebugLoadStage(1);
            DebugMode.SuiteRunning = false;
            int battles = 0, ordinaryLoss = 0, priorStage = 0;
            int stopStage = config.trigger == "first_theme" ? 9 : 17;
            float deadline = Time.realtimeSinceStartup + 540f;
            while(game.RoundManager.Stage < stopStage && game.life > 0 && game.gameState != GameState.GameOver && Time.realtimeSinceStartup < deadline)
            {
                if(game.gameState == GameState.Preparation)
                {
                    if (priorStage != game.RoundManager.Stage)
                    {
                        int stage = game.RoundManager.Stage;
                        Debug.Log($"[범용밸런스] theme={game.RoundManager.CurrentThemeId},stage={stage},life={game.life},HP=" + string.Join(",", AliveAllies().Select(u=>$"{u.ID}:L{u.Level}:{u.HpCurr}/{u.HpMax}")));
                        // 손실은 자동 회복하지 않는다. 플레이어에게 주어진 짧은 휴식만 필요 시 사용한다.
                        if(AliveAllies().Any(u=>u.HpCurr < u.HpMax*.4f)) game.TryShortRest();
                        priorStage = stage;
                    }
                    game.StartRound();
                }
                else if(game.gameState == GameState.RoundEnd)
                {
                    Button next = ReleaseResultContinue();
                    if(next != null) { battles++; next.onClick.Invoke(); yield return WaitUnscaled(.15f); }
                }
                else if(game.gameState == GameState.RewardSelection)
                {
                    // 화면에 실제 제시된 객체만 선택한다. 새 후보를 생성하면 중복 수령 방어가 거부한다.
                    var choices = (System.Collections.Generic.List<RewardDef>)typeof(RewardManager).GetField("_selection",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(RewardManager.Instance);
                    var reward = choices.FirstOrDefault(r=>r.IsCodeReward && AliveAllies().Any(u=>u.CanReceiveRewardCode(r.codeId, out _, out _, out _)))
                        ?? choices.FirstOrDefault(r=>!r.RequiresTargetSelection || Managers.UI.Screens.RewardScreen.TargetCandidates(r).Any());
                    if (reward == null) { RuiCheck("실제 보상 수령", "유효 보상 후보", "없음", false); break; }
                    Unit target = reward.IsCodeReward ? AliveAllies().FirstOrDefault(u=>u.CanReceiveRewardCode(reward.codeId, out _, out _, out _))
                        : reward.RequiresTargetSelection ? Managers.UI.Screens.RewardScreen.TargetCandidates(reward).FirstOrDefault() : null;
                    if (!RewardManager.Instance.TryClaimReward(reward, target)) { RuiCheck("실제 보상 수령", "확정 성공", reward.displayName, false); break; }
                    RewardManager.Instance.FinishRewardSelection();
                    yield return WaitUnscaled(.15f);
                }
                else if(game.gameState == GameState.EventStage) { ordinaryLoss++; break; }
                yield return null;
            }
            RuiCheck("범용 스테이지 자연 완주", $"{stopStage-1}전투·사건0·생명20", $"stage={game.RoundManager.Stage},battle={battles},event={ordinaryLoss},life={game.life}",
                game.RoundManager.Stage >= stopStage && ordinaryLoss == 0 && game.life == 20);
            yield return CaptureStill("generic_campaign_final_" + config.stage);
            game.DebugResetBattle(); DebugMode.SuiteRunning = true; RuiFinish();
        }
    }
}
#endif
