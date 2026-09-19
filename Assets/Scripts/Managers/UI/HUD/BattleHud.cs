using BaseClasses;
using Entities;
using Managers.UI.Core;
using Managers.UI.Screens;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.HUD
{
    /// <summary>
    /// 전투 화면 HUD 전체를 조립하고 매 프레임 갱신한다.
    ///
    /// 화면 배치 (세븐나이츠식 전투 화면 위에 얹힌다):
    ///   좌상단 — 행동서열
    ///   상단중앙 — 생명력 / 골드 / 현재 단계
    ///   우상단 — 라운드 + 진행 게이지 · 진행 예고 · 배속/인벤/설정
    ///   하단 — 준비 페이즈 바(PreparationScreen)만. 전투 중에는 비어 있다.
    ///
    /// 아군 파티 카드는 없다. 유닛의 체력 · 방어막 · 행동 게이지 · 궁극기 충전은
    /// 전부 전장 위 <see cref="Entities.View.UnitCardView"/>가 들고 있어 중복이었다.
    /// </summary>
    public class BattleHud : MonoBehaviour
    {
        /// <summary>배속 단계. 버튼을 누를 때마다 순환한다.</summary>
        private static readonly float[] SpeedSteps = { 1f, 2f, 3f };

        /// <summary>
        /// 화면에 보이는 배속에 곱해지는 기준 시간 배율.
        ///
        /// 예전 1배속이 눈으로 따라가기 어려울 만큼 빨랐다. 기준을 절반으로 낮춰
        /// <b>2배속이 예전 1배속과 같은 속도</b>가 되도록 맞췄다.
        /// </summary>
        private const float BaseTimeScale = 0.5f;

        private TopStatusBar _topBar;
        private FeatureEnemyBanner _featureBanner;
        private ActionQueuePanel _queue;
        private CodexScreen _codex;

        private TextMeshProUGUI _lifeLabel;
        private TextMeshProUGUI _goldLabel;
        private TextMeshProUGUI _phaseLabel;

        private int _speedIndex;

        public static BattleHud Create()
        {
            var go = new GameObject("BattleHud");
            return go.AddComponent<BattleHud>();
        }

        private void Awake()
        {
            Canvas canvas = UIBuild.Canvas("BattleHudCanvas", 10);
            transform.SetParent(canvas.transform, false);
            RectTransform safeRoot = UIBuild.SafeArea(canvas);

            _queue = new ActionQueuePanel(safeRoot);
            _topBar = new TopStatusBar(safeRoot, CycleSpeed, ToggleCodex, OpenMenu);
            BuildResourceStrip(safeRoot);
            _featureBanner = new FeatureEnemyBanner(safeRoot);

            _codex = new CodexScreen();
            ApplySpeed();
        }

        /// <summary>HUD가 사라진 뒤에도 느려진 시간 배율이 남지 않게 되돌린다.</summary>
        private void OnDestroy()
        {
            Time.timeScale = 1f;
        }

        /// <summary>상단 중앙의 생명력/골드/단계 표시.</summary>
        private void BuildResourceStrip(Transform parent)
        {
            Image strip = UIBuild.Panel("ResourceStrip", parent, UITheme.HudBar,
                UIShapes.Corner.Diagonal, 8);
            strip.rectTransform.anchorMin = new Vector2(0.29f, 1f);
            strip.rectTransform.anchorMax = new Vector2(0.61f, 1f);
            strip.rectTransform.pivot = new Vector2(0.5f, 1f);
            strip.rectTransform.sizeDelta = new Vector2(0f, 72f);
            strip.rectTransform.anchoredPosition = new Vector2(0f, -30f);

            _lifeLabel = UIBuild.Text("Life", strip.transform, "♥ 20", UITheme.FontHeading,
                UITheme.TextPrimary, TextAlignmentOptions.Center);
            UIBuild.Anchor(_lifeLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0.30f, 1f), 8f, 0f);

            UIBuild.Divider("Div1", strip.transform).rectTransform.pivot = new Vector2(0.5f, 0.5f);
            Image div1 = strip.transform.Find("Div1").GetComponent<Image>();
            UIBuild.Pin(div1.rectTransform, new Vector2(0.30f, 0.5f), new Vector2(1f, 20f), Vector2.zero);

            _goldLabel = UIBuild.Text("Gold", strip.transform, "◆ 0", UITheme.FontHeading,
                UITheme.Accent, TextAlignmentOptions.Center);
            UIBuild.Anchor(_goldLabel.rectTransform, new Vector2(0.30f, 0f), new Vector2(0.60f, 1f), 8f, 0f);

            Image div2 = UIBuild.Divider("Div2", strip.transform);
            UIBuild.Pin(div2.rectTransform, new Vector2(0.60f, 0.5f), new Vector2(1f, 20f), Vector2.zero);

            _phaseLabel = UIBuild.Text("Phase", strip.transform, "", UITheme.FontCaption,
                UITheme.TextSecondary, TextAlignmentOptions.Center);
            UIBuild.Anchor(_phaseLabel.rectTransform, new Vector2(0.60f, 0f), new Vector2(1f, 1f), 8f, 0f);
        }

        private void Update()
        {
            _topBar?.Tick();
            _queue?.Tick();
            _featureBanner?.Refresh();

            // ESC 메뉴(와 그 위의 자료실)가 떠 있는 동안에는 TAB을 받지 않는다. 캐릭터 창은
            // 메뉴보다 위 캔버스라, 받으면 자료실 검색창에 타이핑하다 창이 덮어 버린다.
            bool menuOpen = GameManager.Instance?.uiManager?.IsPauseMenuOpen == true;
            if (Input.GetKeyDown(KeyCode.Tab) && !menuOpen) ToggleCodex();
            // ESC는 캐릭터 창을 닫지 않는다. 캐릭터 창이 떠 있어도 그 위에 메뉴를 띄운다.
            // 캐릭터 창은 TAB · × · 바깥 클릭으로 닫는다.
            if (Input.GetKeyDown(KeyCode.Escape)) OpenMenu();
        }

        // ── 외부(UIManager)에서 호출하는 갱신 ────────────────────────

        public void SetLife(int life)
        {
            if (_lifeLabel == null) return;
            _lifeLabel.text = $"♥ {life}";
            // 생명력이 5 이하로 떨어지면 붉게 경고한다.
            _lifeLabel.color = life <= 5 ? UITheme.Danger : UITheme.TextPrimary;
        }

        public void SetGold(int gold)
        {
            if (_goldLabel == null) return;
            _goldLabel.text = $"◆ {gold}";
        }

        public void SetPhase(BaseEnums.GameState state, int remainingEnemies)
        {
            if (_phaseLabel == null) return;
            _phaseLabel.text = state switch
            {
                BaseEnums.GameState.Preparation => "준비 단계",
                BaseEnums.GameState.CharacterSelection => "캐릭터 선택",
                BaseEnums.GameState.RoundInProgress => remainingEnemies > 0
                    ? $"전투 중 · 적 {remainingEnemies}"
                    : "전투 중",
                BaseEnums.GameState.RoundEnd => "라운드 종료",
                BaseEnums.GameState.RewardSelection => "보상 선택",
                BaseEnums.GameState.EventStage => "사건",
                BaseEnums.GameState.TrainingPhase => "육성",
                BaseEnums.GameState.RunComplete => "런 완료",
                BaseEnums.GameState.GameOver => "게임 오버",
                _ => "",
            };
        }

        // ── 버튼 동작 ────────────────────────────────────────────────

        private void CycleSpeed()
        {
            _speedIndex = (_speedIndex + 1) % SpeedSteps.Length;
            ApplySpeed();
        }

        /// <summary>현재 단계의 배속을 시간 배율과 표시에 함께 반영한다.</summary>
        private void ApplySpeed()
        {
            float speed = SpeedSteps[_speedIndex];
            Time.timeScale = speed * BaseTimeScale;
            _topBar?.SetSpeedLabel(speed);
        }

        public bool IsCodexOpen => _codex != null && _codex.IsOpen;

        public void ToggleCodex()
        {
            if (_codex == null) return;
            if (_codex.IsOpen) _codex.Hide();
            else _codex.Show();
        }

        public void ShowUnitDetail(Unit unit)
        {
            _codex?.Show(unit);
        }

        /// <summary>ESC와 우상단 ≡ 버튼은 같은 입력이다. 둘 다 여기로 온다.</summary>
        private static void OpenMenu()
        {
            GameManager.Instance?.uiManager?.HandleEscape();
        }
    }
}
