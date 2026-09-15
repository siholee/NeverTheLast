#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Core;
using Entities;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.DevTools
{
    /// <summary>
    /// 테스트용 디버그 패널. <b>F1</b>로 열고 닫는다.
    ///
    /// 전투가 완전 자동이라 확인하고 싶은 장면에 도달하는 방법이 정상 플레이뿐이다.
    /// 테마 하나를 보려면 라운드를 그만큼 지나야 하고, Lv.90 해금 코드를 보려면 런을 거의
    /// 완주해야 한다. 그래서 <b>상태를 직접 밀어 넣는 조작만</b> 모아 둔다 —
    /// 새 규칙을 만들지 않고, 이미 있는 진입점(<c>LoadRound</c>·<c>DebugSetLevel</c> 등)만 부른다.
    ///
    /// 에디터와 개발 빌드에서만 컴파일된다.
    /// </summary>
    public sealed class DebugOverlay : MonoBehaviour
    {
        /// <summary>툴팁(900)보다 위. 무엇에도 가리지 않아야 조작할 수 있다.</summary>
        private const int SortingOrder = 950;

        private const float PanelWidth = 400f;
        private const float RowHeight = 30f;
        private const float Gap = 6f;

        private static DebugOverlay _instance;

        private RectTransform _panel;
        private RectTransform _viewport;
        private TextMeshProUGUI _status;
        private TextMeshProUGUI _inspector;
        private TextMeshProUGUI _banner;
        private TMP_InputField _stageInput;

        /// <summary>관찰 대상. 원소 부착과 상태 표시가 모두 이 하나를 향한다.</summary>
        private static bool _targetIsEnemy = true;
        private static int _targetIndex;

        /// <summary>패널을 열고 닫는다. 처음 열 때 UI를 만든다.</summary>
        public static void Toggle()
        {
            EnsureInstance();
            DebugMode.PanelOpen = !DebugMode.PanelOpen;
            _instance._viewport.gameObject.SetActive(DebugMode.PanelOpen);
            if (DebugMode.PanelOpen) _instance.Refresh();
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;

            Canvas canvas = UIBuild.Canvas("DebugCanvas", SortingOrder);
            _instance = canvas.gameObject.AddComponent<DebugOverlay>();
            _instance.Build(canvas);
        }

        public static void EnsureBanner()
        {
            EnsureInstance();
        }

        // ── 조립 ─────────────────────────────────────────────────────

        private void Build(Canvas canvas)
        {
            Image panel = UIBuild.Panel("DebugPanel", canvas.transform, UITheme.Surface,
                UIShapes.Corner.Diagonal, 8, UITheme.Outline, 1);
            _panel = panel.rectTransform;
            _panel.anchorMin = new Vector2(0f, 1f);
            _panel.anchorMax = new Vector2(0f, 1f);
            _panel.pivot = new Vector2(0f, 1f);
            _panel.anchoredPosition = new Vector2(16f, -16f);

            float y = 12f;
            Header("디버그 모드  ·  F1 닫기", ref y);
            _status = Body("", ref y, 4);

            Section("테마 고정", ref y);
            BuildThemeRows(ref y);

            Section("스테이지", ref y);
            _stageInput = NumberInput("스테이지 번호", "31", ref y);
            Row(ref y, ("입력한 스테이지로 이동", () =>
            {
                if (int.TryParse(_stageInput.text, out int stage)) LoadStage(stage);
            }), ("현재 스테이지 재시작", () => LoadStage(GameManager.Instance?.RoundManager?.Stage ?? 1)));
            Row(ref y,
                ("◀◀ −10", () => JumpStage(-10)),
                ("◀ −1", () => JumpStage(-1)),
                ("+1 ▶", () => JumpStage(1)),
                ("+10 ▶▶", () => JumpStage(10)));
            Row(ref y,
                ("슬롯 1", () => JumpToSlot(1)),
                ("5 사건", () => JumpToSlot(5)),
                ("6 엘리트", () => JumpToSlot(6)),
                ("9 엘리트", () => JumpToSlot(9)),
                ("10 보스", () => JumpToSlot(10)));

            Section("전투", ref y);
            Row(ref y,
                ("적 전멸(승리)", KillAllEnemies),
                ("아군 전멸(패배)", KillAllAllies));
            Row(ref y,
                ("아군 무적", ToggleAllyInvincible),
                ("적 무적", ToggleEnemyInvincible),
                ("아군 회복", HealAllies));

            Section("회복 보상 미리보기", ref y);
            Row(ref y,
                ("T1 하급", () => PreviewHealingRewards(1)),
                ("T2 중급", () => PreviewHealingRewards(2)),
                ("T3 고급", () => PreviewHealingRewards(3)));
            Row(ref y,
                ("T4 최상급", () => PreviewHealingRewards(4)),
                ("T5 엘릭서", () => PreviewHealingRewards(5)));

            Section("부활 보상 미리보기", ref y);
            Row(ref y,
                ("T1 초급", () => PreviewRevivalRewards(1)),
                ("T2 중급", () => PreviewRevivalRewards(2)),
                ("T3 고급", () => PreviewRevivalRewards(3)));
            Row(ref y,
                ("T4 최상급", () => PreviewRevivalRewards(4)),
                ("T5 에테르", () => PreviewRevivalRewards(5)));

            Section("아군 레벨", ref y);
            Row(ref y,
                ("Lv.1", () => SetPartyLevel(1)),
                ("30", () => SetPartyLevel(30)),
                ("60", () => SetPartyLevel(60)),
                ("90", () => SetPartyLevel(90)),
                ("+10", () => SetPartyLevel(-10)));

            Section("관찰 대상", ref y);
            _inspector = Body("", ref y, 6);
            Row(ref y,
                ("진영 전환", CycleFaction),
                ("◀ 이전", () => CycleTarget(-1)),
                ("다음 ▶", () => CycleTarget(1)));

            Section("원소 부착  ·  대상에게 직접 건다", ref y);
            Row(ref y,
                ("불", () => Attach(BaseEnums.UnitElement.Pyro)),
                ("물", () => Attach(BaseEnums.UnitElement.Hydro)),
                ("풀", () => Attach(BaseEnums.UnitElement.Dendro)),
                ("전기", () => Attach(BaseEnums.UnitElement.Electro)));
            Row(ref y,
                ("얼음", () => Attach(BaseEnums.UnitElement.Cryo)),
                ("바람", () => Attach(BaseEnums.UnitElement.Anemo)),
                ("바위", () => Attach(BaseEnums.UnitElement.Geo)),
                ("부착 해제", ClearElements));
            Row(ref y,
                ("상태 전부 해제", ClearStatuses),
                ("반응 기록 저장", () => DebugReactionLog.Dump()),
                ("기록 비우기", () => DebugReactionLog.Restart()));

            Section("배속", ref y);
            Row(ref y,
                ("0.25×", () => DebugMode.SetTimeScale(0.25f)),
                ("1×", () => DebugMode.SetTimeScale(1f)),
                ("4×", () => DebugMode.SetTimeScale(4f)),
                ("8×", () => DebugMode.SetTimeScale(8f)));
            Row(ref y, ("양측 궁극기 충전", () =>
            {
                DebugMode.BeginSession();
                foreach (var unit in AliveUnits(false).Concat(AliveUnits(true))) unit.FillUltimateResource(false);
            }));

            y += Gap;
            Row(ref y, ("강제 설정 해제", () => { DebugMode.ResetAll(); Refresh(); }),
                ("시작 스냅샷 복원", () => { DebugMode.RestoreSnapshot(); Refresh(); }));

            _panel.sizeDelta = new Vector2(PanelWidth, y + 12f);
            var viewportObject=new GameObject("DebugViewport",typeof(RectTransform),typeof(Image),typeof(RectMask2D),typeof(ScrollRect));
            _viewport=viewportObject.GetComponent<RectTransform>();
            _viewport.SetParent(canvas.transform,false);
            _viewport.anchorMin=new Vector2(0,0);
            _viewport.anchorMax=new Vector2(0,1);
            _viewport.pivot=new Vector2(0,1);
            _viewport.offsetMin=new Vector2(16,16);
            _viewport.offsetMax=new Vector2(16+PanelWidth,-32);
            viewportObject.GetComponent<Image>().color=UITheme.Surface;
            _panel.SetParent(_viewport,false);
            _panel.anchoredPosition=Vector2.zero;
            var scroll=viewportObject.GetComponent<ScrollRect>();
            scroll.viewport=_viewport;
            scroll.content=_panel;
            scroll.horizontal=false;
            scroll.vertical=true;
            scroll.movementType=ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity=40;
            _viewport.gameObject.SetActive(false);

            BuildBanner(canvas);
        }

        /// <summary>
        /// 강제 설정이 하나라도 켜져 있으면 화면 위에 상시로 띄운다.
        ///
        /// 무적을 켜 둔 채 "왜 안 죽지"를 재는 일이 없어야 한다. 패널이 닫혀 있어도 보여야
        /// 하므로 패널과 형제로 두고, 값이 바뀔 때마다 <see cref="Refresh"/>가 껐다 켠다.
        /// </summary>
        private void BuildBanner(Canvas canvas)
        {
            _banner = UIBuild.Text("DebugBanner", canvas.transform, "", UITheme.FontCaption,
                UITheme.Danger, TextAlignmentOptions.Top);
            RectTransform rect = _banner.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(720f, 22f);
            rect.anchoredPosition = new Vector2(0f, -4f);
            _banner.gameObject.SetActive(false);
        }

        /// <summary>
        /// 켜져 있는 테마만이 아니라 <b>전부</b> 늘어놓는다. 리메이크로 빼 둔 테마
        /// (<c>enabled: false</c>)도 확인할 수 있어야 하기 때문이다.
        /// </summary>
        private void BuildThemeRows(ref float y)
        {
            List<StageThemeData> themes =
                GameManager.Instance?.dataManager?.FetchStageThemeDataList()?.stageThemes
                ?? new List<StageThemeData>();

            var cells = new List<(string, Action)> { ("고정 해제", () => ForceTheme(0)) };
            foreach (StageThemeData theme in themes)
            {
                int id = theme.id;
                string label = theme.enabled ? theme.name : $"{theme.name}(off)";
                cells.Add((label, () => ForceTheme(id)));
            }

            // 이름이 길어 한 줄에 셋까지만 넣는다.
            for (int i = 0; i < cells.Count; i += 3)
            {
                Row(ref y, cells.Skip(i).Take(3).ToArray());
            }
        }

        // ── 조작 ─────────────────────────────────────────────────────

        private static void ForceTheme(int themeId)
        {
            DebugMode.BeginSession();
            DebugMode.ForcedThemeId = themeId;
            Debug.Log(themeId > 0
                ? $"[디버그] 테마 {themeId}로 고정. 다음 스테이지부터 적용된다."
                : "[디버그] 테마 고정 해제.");
            _instance.Refresh();
        }

        /// <summary>현재 스테이지에서 상대 이동. 라운드가 바뀌면 테마도 따라 바뀐다.</summary>
        private static void JumpStage(int delta)
        {
            RoundManager round = GameManager.Instance?.RoundManager;
            if (round == null) return;
            LoadStage(round.Stage + delta);
        }

        /// <summary>라운드는 그대로 두고 라운드 안의 슬롯만 바꾼다.</summary>
        private static void JumpToSlot(int slot)
        {
            RoundManager round = GameManager.Instance?.RoundManager;
            if (round == null) return;
            LoadStage((round.Stage - 1) / 10 * 10 + Mathf.Clamp(slot, 1, 10));
        }

        private static void LoadStage(int stage)
        {
            GameManager game = GameManager.Instance;
            RoundManager round = game?.RoundManager;
            if (round == null) return;

            stage = Mathf.Max(1, stage);
            game.DebugLoadStage(stage);
            Debug.Log($"[디버그] {stage}스테이지로 이동했다.");
            _instance.Refresh();
        }

        private static void KillAllEnemies()
        {
            GameManager.Instance?.DebugEndBattle(true);
        }

        private static void KillAllAllies()
        {
            GameManager.Instance?.DebugEndBattle(false);
        }

        private static void HealAllies()
        {
            DebugMode.BeginSession();
            foreach (Unit ally in AliveUnits(false)) ally.ModifyHp(ally.HpMax, ally);
            Debug.Log("[디버그] 아군을 모두 회복시켰다.");
        }

        /// <summary>회복 카드와 대상 선택 화면을 정상 보상 UI 진입점으로 즉시 확인한다.</summary>
        private static void PreviewHealingRewards(int focusTier)
        {
            GameManager game = GameManager.Instance;
            List<RewardDef> healing = game?.dataManager?.FetchRewardDataList()?.rewards?
                .Where(reward => reward?.IsHealingReward == true)
                .OrderBy(reward => Mathf.Abs(reward.tier - focusTier))
                .ThenBy(reward => reward.tier)
                .Take(3)
                .ToList() ?? new List<RewardDef>();
            if (game == null || healing.Count == 0) return;

            DebugMode.BeginSession();
            game.DebugResetBattle();
            foreach (Unit ally in AliveUnits(false)) ally.ModifyHp(Mathf.Max(1, ally.HpMax / 4));
            game.gameState = BaseEnums.GameState.RewardSelection;
            game.uiManager?.ShowRewardPanel(healing);
            DebugMode.PanelOpen = false;
            if (_instance?._viewport != null) _instance._viewport.gameObject.SetActive(false);
            Debug.Log($"[디버그] T{focusTier} 중심 회복 보상 미리보기를 열었다.");
        }

        /// <summary>아군 일부를 쓰러뜨린 뒤 부활 카드와 대상 선택 화면을 즉시 확인한다.</summary>
        private static void PreviewRevivalRewards(int focusTier)
        {
            GameManager game = GameManager.Instance;
            List<RewardDef> revival = game?.dataManager?.FetchRewardDataList()?.rewards?
                .Where(reward => reward?.IsRevivalReward == true)
                .OrderBy(reward => Mathf.Abs(reward.tier - focusTier))
                .ThenBy(reward => reward.tier)
                .Take(3)
                .ToList() ?? new List<RewardDef>();
            if (game == null || revival.Count == 0) return;

            DebugMode.BeginSession();
            game.DebugResetBattle();
            List<Unit> allies = AliveUnits(false);
            if (allies.Count < 2)
            {
                Debug.LogWarning("[디버그] 부활 보상 미리보기에는 아군이 2명 이상 필요하다.");
                return;
            }

            foreach (Unit ally in allies.Skip(1)) ally.Die(null);
            game.gameState = BaseEnums.GameState.RewardSelection;
            game.uiManager?.ShowRewardPanel(revival);
            DebugMode.PanelOpen = false;
            if (_instance?._viewport != null) _instance._viewport.gameObject.SetActive(false);
            Debug.Log($"[디버그] T{focusTier} 중심 부활 보상 미리보기를 열었다.");
        }

        /// <summary><paramref name="level"/>이 음수면 그 절댓값만큼 올린다.</summary>
        private static void SetPartyLevel(int level)
        {
            DebugMode.BeginSession();
            GameManager game = GameManager.Instance;
            if (game?.gameState == BaseEnums.GameState.RoundInProgress)
                game.DebugLoadStage(game.RoundManager.Stage);
            foreach (Unit ally in AliveUnits(false))
            {
                ally.DebugSetLevel(level < 0 ? ally.Level - level : level);
            }
            _instance.Refresh();
        }

        // ── 관찰 대상 ────────────────────────────────────────────────

        /// <summary>지금 보고 있는 유닛. 목록이 비었거나 색인이 넘치면 null이다.</summary>
        private static Unit SelectedTarget()
        {
            List<Unit> units = AliveUnits(_targetIsEnemy);
            if (units.Count == 0) return null;
            _targetIndex = Mathf.Clamp(_targetIndex, 0, units.Count - 1);
            return units[_targetIndex];
        }

        private static void CycleFaction()
        {
            _targetIsEnemy = !_targetIsEnemy;
            _targetIndex = 0;
            _instance?.Refresh();
        }

        private static void CycleTarget(int delta)
        {
            List<Unit> units = AliveUnits(_targetIsEnemy);
            if (units.Count == 0) return;
            _targetIndex = (_targetIndex + delta + units.Count) % units.Count;
            _instance?.Refresh();
        }

        /// <summary>
        /// 대상에게 원소를 하나 붙인다. <b>반응 판정을 포함한 정상 경로</b>를 그대로 탄다 —
        /// 여기서 지름길을 내면 정작 확인하려는 반응을 건너뛰게 된다.
        ///
        /// 위력은 유발자의 CON에 비례하므로, 유발자는 <b>반대 진영의 첫 유닛</b>으로 잡는다.
        /// 상대가 없으면 대상 자신이 유발자가 되어 반응은 일어나되 수치가 자기 CON을 따른다.
        /// </summary>
        private static void Attach(BaseEnums.UnitElement element)
        {
            DebugMode.BeginSession();
            Unit target = SelectedTarget();
            if (target == null)
            {
                Debug.Log("[디버그] 관찰 대상이 없다.");
                return;
            }

            DebugReactionLog.Start();
            Unit source = AliveUnits(!_targetIsEnemy).FirstOrDefault() ?? target;
            int before = DebugReactionLog.Count;

            target.GrantCombatElement(element, Unit.CommonElementAuraDuration, source);

            string reaction = DebugReactionLog.Count > before ? DebugReactionLog.Last : "없음";
            Debug.Log($"[디버그] {target.UnitName}에게 {element} 부착 (유발자 {source.UnitName}) — 반응: {reaction}");
            _instance?.Refresh();
        }

        private static void ClearElements()
        {
            DebugMode.BeginSession();
            Unit target = SelectedTarget();
            if (target == null) return;
            target.ResetCombatElements();
            Debug.Log($"[디버그] {target.UnitName}의 부착 원소를 모두 걷었다.");
            _instance?.Refresh();
        }

        private static void ClearStatuses()
        {
            DebugMode.BeginSession();
            Unit target = SelectedTarget();
            if (target == null) return;
            target.DebugResetCombatState();
            Debug.Log($"[디버그] {target.UnitName}의 상태를 모두 걷었다.");
            _instance?.Refresh();
        }

        private static List<Unit> AliveUnits(bool enemy)
        {
            GridManager grid = GridManager.Instance;
            if (grid == null) return new List<Unit>();

            return (enemy ? grid.enemyList : grid.heroList)
                .Where(unit => unit != null && unit.isActive)
                .ToList();
        }

        public static void ToggleAllyInvincible()
        {
            DebugMode.BeginSession();
            EnsureInstance();
            DebugMode.AllyInvincible = !DebugMode.AllyInvincible;
            Debug.Log($"[디버그] 아군 무적 {(DebugMode.AllyInvincible ? "켬" : "끔")}");
            _instance?.Refresh();
        }

        public static void ToggleEnemyInvincible()
        {
            DebugMode.BeginSession();
            EnsureInstance();
            DebugMode.EnemyInvincible = !DebugMode.EnemyInvincible;
            Debug.Log($"[디버그] 적 무적 {(DebugMode.EnemyInvincible ? "켬" : "끔")}");
            _instance?.Refresh();
        }

        // ── 표시 ─────────────────────────────────────────────────────

        private void Update()
        {
            // 배너는 패널이 닫혀 있어도 갱신해야 한다. 4프레임에 한 번이면 충분하다.
            if (Time.frameCount % 15 == 0) Refresh();
        }

        private void Refresh()
        {
            UpdateBanner();
            if (_status == null || !DebugMode.PanelOpen) return;

            GameManager game = GameManager.Instance;
            RoundManager round = game?.RoundManager;
            int allies = AliveUnits(false).Count;
            int enemies = AliveUnits(true).Count;

            string theme = DebugMode.ForcedThemeId > 0
                ? $"{round?.CurrentThemeId} (고정)"
                : $"{round?.CurrentThemeId}";
            string flags = DebugMode.AnyOverrideActive
                ? string.Join(" ", new[]
                {
                    DebugMode.AllyInvincible ? "아군무적" : null,
                    DebugMode.EnemyInvincible ? "적무적" : null,
                    DebugMode.ForcedThemeId > 0 ? "테마고정" : null,
                }.Where(x => x != null))
                : "없음";

            _status.text =
                $"스테이지 {round?.Stage} (라운드 {round?.Round} · 슬롯 {round?.StageInRound})   테마 {theme}\n"
                + $"상태 {game?.gameState}   생명력 {game?.life}   배속 {Time.timeScale:0.##}×\n"
                + $"아군 {allies}   적 {enemies}\n"
                + $"강제 설정: {flags}" + (DebugMode.SessionActive ? " · 저장: 메모리 전용" : "");
            _status.color = DebugMode.AnyOverrideActive ? UITheme.Danger : UITheme.TextSecondary;
            UpdateInspector();
        }

        /// <summary>
        /// 관찰 대상의 부착 원소와 상태를 있는 그대로 늘어놓는다.
        ///
        /// 반응이 안 터졌을 때 <b>원소가 안 붙은 것인지 반응 쪽이 막은 것인지</b>를
        /// 화면에서 가릴 수 있어야 한다. 남은 턴까지 같이 보여야 부착이 만료된 경우도 구분된다.
        /// </summary>
        private void UpdateInspector()
        {
            if (_inspector == null) return;

            Unit target = SelectedTarget();
            if (target == null)
            {
                _inspector.text = $"관찰 대상 없음  ({(_targetIsEnemy ? "적" : "아군")} 진영)";
                _inspector.color = UITheme.TextMuted;
                return;
            }

            List<Unit> units = AliveUnits(_targetIsEnemy);
            string elements = string.Join("  ", Enum.GetValues(typeof(BaseEnums.UnitElement))
                .Cast<BaseEnums.UnitElement>()
                .Where(element => element != BaseEnums.UnitElement.None && target.HasAttachedElement(element))
                .Select(element => $"{element}({target.GetAttachedElementRemainingTurns(element)})"));

            string statuses = string.Join("  ", target.ActiveStatuses
                .Select(status => status.RemainingTurns == int.MaxValue
                    ? status.StatusName
                    : $"{status.StatusName}({status.RemainingTurns})"));

            _inspector.text = string.Join("\n", new[]
            {
                $"[{(_targetIsEnemy ? "적" : "아군")} {_targetIndex + 1}/{units.Count}] {target.UnitName}  Lv.{target.Level}",
                $"HP {target.HpCurr}/{target.HpMax}   보호막 {target.ShieldCurr}   턴 {target.TurnCount}",
                $"속성 {target.Element}   CON {target.GetBaseCon()}   제어 {(target.isControlled ? "예" : "아니오")}"
                    + $"   분쇄 {target.ControlAppliedCount}회차",
                $"부착: {(string.IsNullOrEmpty(elements) ? "없음" : elements)}",
                $"상태: {(string.IsNullOrEmpty(statuses) ? "없음" : statuses)}",
                $"반응 기록 {DebugReactionLog.Count}건   최근: {(DebugReactionLog.Count > 0 ? DebugReactionLog.Last : "없음")}",
            });
            _inspector.color = UITheme.TextSecondary;
        }

        private void UpdateBanner()
        {
            if (_banner == null) return;

            bool show = DebugMode.SessionActive || DebugMode.AnyOverrideActive || !Mathf.Approximately(Time.timeScale, 1f);
            _banner.gameObject.SetActive(show);
            if (!show) return;

            var parts = new List<string>();
            if (DebugMode.SessionActive) parts.Add("테스트 런 · 저장 보호");
            if (DebugMode.AllyInvincible) parts.Add("아군 무적");
            if (DebugMode.EnemyInvincible) parts.Add("적 무적");
            if (DebugMode.ForcedThemeId > 0) parts.Add($"테마 {DebugMode.ForcedThemeId} 고정");
            if (!Mathf.Approximately(Time.timeScale, 1f)) parts.Add($"{Time.timeScale:0.##}배속");
            _banner.text = "디버그 — " + string.Join("  ·  ", parts) + "   (F1 패널)";
        }

        // ── 레이아웃 도우미 ──────────────────────────────────────────

        private TMP_InputField NumberInput(string name, string initial, ref float y)
        {
            Image background = UIBuild.Solid(name, _panel, UITheme.SurfaceRaised);
            Place(background.rectTransform, y, RowHeight);
            var input = background.gameObject.AddComponent<TMP_InputField>();
            var viewport = UIBuild.Container("Viewport", background.transform);
            UIBuild.Stretch(viewport, 8f, 2f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = UIBuild.Text("Value", viewport, initial, UITheme.FontBody, UITheme.TextPrimary);
            UIBuild.Stretch(text.rectTransform);
            input.textViewport = viewport;
            input.textComponent = text;
            input.targetGraphic = background;
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            input.text = initial;
            y += RowHeight + Gap;
            return input;
        }

        private void Header(string text, ref float y)
        {
            TextMeshProUGUI label = UIBuild.Text("Header", _panel, text, UITheme.FontHeading,
                UITheme.Accent);
            Place(label.rectTransform, y, RowHeight);
            y += RowHeight + Gap;
        }

        private void Section(string text, ref float y)
        {
            y += Gap;
            TextMeshProUGUI label = UIBuild.Label("Section", _panel, text, UITheme.FontCaption,
                UITheme.TextMuted);
            Place(label.rectTransform, y, 20f);
            y += 20f + 2f;
        }

        private TextMeshProUGUI Body(string text, ref float y, int lines)
        {
            TextMeshProUGUI label = UIBuild.Text("Status", _panel, text, UITheme.FontCaption,
                UITheme.TextSecondary, TextAlignmentOptions.TopLeft, wrap: true);
            float height = lines * 17f;
            Place(label.rectTransform, y, height);
            y += height + Gap;
            return label;
        }

        /// <summary>한 줄에 버튼을 균등 배치한다.</summary>
        private void Row(ref float y, params (string Text, Action OnClick)[] cells)
        {
            float inner = PanelWidth - 24f;
            float width = (inner - Gap * (cells.Length - 1)) / cells.Length;

            for (int i = 0; i < cells.Length; i++)
            {
                Button button = UIBuild.Button($"Btn_{cells[i].Text}", _panel, cells[i].Text,
                    cells[i].OnClick, false, UITheme.FontCaption);
                RectTransform rect = button.image.rectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(width, RowHeight);
                rect.anchoredPosition = new Vector2(12f + i * (width + Gap), -y);
            }
            y += RowHeight + Gap;
        }

        private static void Place(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(PanelWidth - 24f, height);
            rect.anchoredPosition = new Vector2(12f, -top);
        }
    }
}
#endif
