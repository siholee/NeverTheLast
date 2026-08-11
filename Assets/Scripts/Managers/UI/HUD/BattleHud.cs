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
    ///   좌상단 — 행동 로그(궁극기만)
    ///   상단중앙 — 생명력 / 골드 / 현재 단계
    ///   우상단 — 라운드 + 진행 게이지 · 진행 예고 · 배속/인벤/설정
    ///   좌하단 — 아군 파티 카드
    ///   우하단 — 의도적으로 비워둠(전투 연출과 드래그 조작을 가리지 않기 위해)
    /// </summary>
    public class BattleHud : MonoBehaviour
    {
        /// <summary>배속 단계. 버튼을 누를 때마다 순환한다.</summary>
        private static readonly float[] SpeedSteps = { 1f, 2f, 3f };

        private TopStatusBar _topBar;
        private PartyPanel _party;
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

            _queue = new ActionQueuePanel(canvas.transform);
            _party = new PartyPanel(canvas.transform);
            _topBar = new TopStatusBar(canvas.transform, CycleSpeed, ToggleCodex, OpenSettings);
            BuildResourceStrip(canvas.transform);

            _codex = new CodexScreen();
            _topBar.SetSpeedLabel(SpeedSteps[_speedIndex]);
        }

        /// <summary>상단 중앙의 생명력/골드/단계 표시.</summary>
        private void BuildResourceStrip(Transform parent)
        {
            Image strip = UIBuild.Panel("ResourceStrip", parent, UITheme.HudBar,
                UIShapes.Corner.Diagonal, 8);
            UIBuild.Pin(strip.rectTransform, new Vector2(0.5f, 1f), new Vector2(420f, 40f),
                new Vector2(0f, -20f));

            _lifeLabel = UIBuild.Text("Life", strip.transform, "♥ 20", UITheme.FontHeading,
                UITheme.TextPrimary, TextAlignmentOptions.Center);
            UIBuild.Anchor(_lifeLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0.30f, 1f), 8f, 0f);

            UIBuild.Divider("Div1", strip.transform).rectTransform.pivot = new Vector2(0.5f, 0.5f);
            Image div1 = strip.transform.Find("Div1").GetComponent<Image>();
            UIBuild.Pin(div1.rectTransform, new Vector2(0.30f, 0.5f), new Vector2(1f, 20f), Vector2.zero);

            _goldLabel = UIBuild.Text("Gold", strip.transform, "◈ 0", UITheme.FontHeading,
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
            _party?.Tick();
            _queue?.Tick();

            if (Input.GetKeyDown(KeyCode.Tab)) ToggleCodex();
            if (Input.GetKeyDown(KeyCode.Escape) && _codex != null && _codex.IsOpen) _codex.Hide();
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
            _goldLabel.text = $"◈ {gold}";
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
            float speed = SpeedSteps[_speedIndex];
            Time.timeScale = speed;
            _topBar.SetSpeedLabel(speed);
        }

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

        private static void OpenSettings()
        {
            GameManager.Instance?.uiManager?.ShowSettings();
        }
    }
}
