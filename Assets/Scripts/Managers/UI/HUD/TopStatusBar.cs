using System;
using System.Collections.Generic;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.HUD
{
    /// <summary>
    /// 화면 상단을 한 줄로 묶는 탐색/상태 헤더. 왼쪽부터 순서대로:
    /// [라운드 1-1 + 진행 원형게이지] · [앞으로 나올 적 예고] · [배속] [인벤] [설정]
    ///
    /// 진행 예고는 TFT의 라운드 트랙을 따른다. 이 게임은 적이 스테이지 시작 시 한 번에
    /// 소환되므로, "다음에 나올 적 개체"가 아니라 "앞으로 올 스테이지의 성격"을 칸으로 보여준다.
    /// </summary>
    public class TopStatusBar
    {
        private const int PreviewSlots = 6;
        private const float SlotSize = 26f;
        private const float SlotGap = 5f;
        private const float BarHeight = 96f;

        private readonly RectTransform _root;
        private readonly TextMeshProUGUI _roundLabel;
        private readonly TextMeshProUGUI _themeLabel;
        private readonly TextMeshProUGUI _timerLabel;
        private readonly Image _timerRing;
        private readonly TextMeshProUGUI _speedLabel;
        private readonly List<Slot> _slots = new();

        private int _lastStage = -1;

        public TopStatusBar(Transform parent, Action onSpeed, Action onCodex, Action onSettings)
        {
            _root = UIBuild.Container("TopStatusBar", parent);
            _root.anchorMin = new Vector2(0.015f, 1f);
            _root.anchorMax = new Vector2(0.985f, 1f);
            _root.pivot = new Vector2(0.5f, 1f);
            _root.sizeDelta = new Vector2(0f, BarHeight);
            _root.anchoredPosition = new Vector2(0f, -18f);

            Image background = UIBuild.Panel("HeaderBand", _root, UITheme.HudBar,
                UIShapes.Corner.Diagonal, 10, UITheme.Outline, 1);
            UIBuild.Stretch(background.rectTransform);

            // ── 오른쪽: 진행 예고 + 손가락으로 누르기 충분한 3개 조작 ──
            RectTransform right = UIBuild.Container("Navigation", _root);
            UIBuild.Anchor(right, new Vector2(0.62f, 0f), new Vector2(0.99f, 1f), 8f, 8f);

            Button settings = UIBuild.IconButton("SettingsButton", right, "메뉴", onSettings);
            UIBuild.Anchor(settings.image.rectTransform, new Vector2(0.84f, 0.12f), new Vector2(1f, 0.88f), 3f, 3f);

            Button codex = UIBuild.IconButton("CodexButton", right, "파티", onCodex);
            UIBuild.Anchor(codex.image.rectTransform, new Vector2(0.68f, 0.12f), new Vector2(0.835f, 0.88f), 3f, 3f);
            // TAB으로도 열리는 화면이므로 버튼에 키 힌트를 겹쳐 표시한다.
            TextMeshProUGUI hint = UIBuild.Text("KeyHint", codex.transform, "TAB",
                UITheme.FontMicro, UITheme.TextMuted, TextAlignmentOptions.Center);
            UIBuild.Anchor(hint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.25f));

            Button speed = UIBuild.IconButton("SpeedButton", right, "", onSpeed);
            UIBuild.Anchor(speed.image.rectTransform, new Vector2(0.52f, 0.12f), new Vector2(0.675f, 0.88f), 3f, 3f);
            _speedLabel = UIBuild.Text("SpeedLabel", speed.transform, "1×", UITheme.FontHeading,
                UITheme.TextPrimary, TextAlignmentOptions.Center);
            UIBuild.Stretch(_speedLabel.rectTransform);

            // ── 가운데: 진행 예고 칸 ──
            var previewRoot = UIBuild.Container("StagePreview", right);
            float previewWidth = PreviewSlots * SlotSize + (PreviewSlots - 1) * SlotGap;
            UIBuild.Pin(previewRoot, new Vector2(0f, 0.5f), new Vector2(previewWidth, SlotSize),
                new Vector2(4f, -7f));

            for (int i = 0; i < PreviewSlots; i++)
            {
                _slots.Add(new Slot(previewRoot, i));
            }

            TextMeshProUGUI previewCaption = UIBuild.Label("PreviewCaption", previewRoot, "진행 예고",
                UITheme.FontMicro, UITheme.TextMuted);
            UIBuild.Pin(previewCaption.rectTransform, new Vector2(0f, 1f), new Vector2(60f, 12f),
                new Vector2(0f, 14f));

            // ── 왼쪽: 라운드 표시 + 진행 원형게이지 ──
            var roundBlock = UIBuild.Panel("RoundBlock", _root, UITheme.HudBar,
                UIShapes.Corner.Diagonal, 8);
            UIBuild.Anchor(roundBlock.rectTransform, new Vector2(0.01f, 0.08f), new Vector2(0.28f, 0.92f), 4f, 4f);

            // 원형 게이지: 남은 시간이 시계방향으로 줄어든다.
            _timerRing = UIBuild.RadialBar("Timer", roundBlock.transform, UITheme.Accent,
                UITheme.Track, 96, 0.74f);
            RectTransform ringTrack = _timerRing.rectTransform.parent as RectTransform;
            UIBuild.Pin(ringTrack, new Vector2(0f, 0.5f), new Vector2(56f, 56f), new Vector2(12f, 0f));

            _timerLabel = UIBuild.Text("TimerText", ringTrack, "", UITheme.FontCaption,
                UITheme.TextSecondary, TextAlignmentOptions.Center);
            UIBuild.Stretch(_timerLabel.rectTransform);

            _roundLabel = UIBuild.Text("RoundLabel", roundBlock.transform, "1-1",
                UITheme.FontTitle, UITheme.TextPrimary);
            UIBuild.Anchor(_roundLabel.rectTransform, new Vector2(0f, 0.46f), new Vector2(1f, 1f), 0f, 0f);
            _roundLabel.rectTransform.offsetMin = new Vector2(78f, _roundLabel.rectTransform.offsetMin.y);

            _themeLabel = UIBuild.Text("ThemeLabel", roundBlock.transform, "",
                UITheme.FontMicro, UITheme.TextMuted);
            UIBuild.Anchor(_themeLabel.rectTransform, new Vector2(0f, 0.06f), new Vector2(1f, 0.46f), 0f, 0f);
            _themeLabel.rectTransform.offsetMin = new Vector2(78f, _themeLabel.rectTransform.offsetMin.y);
            _themeLabel.overflowMode = TextOverflowModes.Ellipsis;
        }

        public void Tick()
        {
            GameManager game = GameManager.Instance;
            RoundManager round = game?.RoundManager;
            if (round == null) return;

            _roundLabel.text = $"{round.Round}-{round.StageInRound}";
            _themeLabel.text = string.IsNullOrEmpty(round.CurrentThemeName)
                ? $"STAGE {round.Stage}"
                : round.CurrentThemeName;

            // 원형 게이지 — 준비 단계는 남은 초, 전투 중에는 남은 턴이다.
            _timerRing.fillAmount = game.PhaseProgressRatio;

            int remaining = game.PhaseRemainingSeconds;
            bool turnGauge = game.IsRoundTurnGauge;
            _timerLabel.text = remaining > 0
                ? (turnGauge ? $"{remaining}T" : remaining.ToString())
                : "";

            // 얼마 안 남으면 붉게 바꿔 압박을 준다. 턴 게이지는 10턴이 기준이다.
            _timerRing.color = remaining > 0 && remaining <= 10 ? UITheme.Danger : UITheme.Accent;

            if (_lastStage != round.Stage)
            {
                _lastStage = round.Stage;
                RefreshPreview(round);
            }
        }

        private void RefreshPreview(RoundManager round)
        {
            List<StageKind> kinds = round.GetUpcomingStageKinds(PreviewSlots);
            for (int i = 0; i < _slots.Count; i++)
            {
                // 첫 칸이 현재 스테이지다.
                _slots[i].Set(kinds[i], isCurrent: i == 0);
            }
        }

        public void SetSpeedLabel(float speed)
        {
            // 정수 배속이면 소수점을 떼서 "2×"처럼 짧게 보여준다.
            _speedLabel.text = Mathf.Approximately(speed, Mathf.Round(speed))
                ? $"{Mathf.RoundToInt(speed)}×"
                : $"{speed:0.#}×";
        }

        /// <summary>진행 예고의 한 칸.</summary>
        private sealed class Slot
        {
            private readonly Image _fill;
            private readonly Image _currentMark;
            private readonly TextMeshProUGUI _glyph;

            public Slot(Transform parent, int index)
            {
                _fill = UIBuild.Panel($"Slot{index}", parent, UITheme.SurfaceRaised,
                    UIShapes.Corner.Diagonal, 5);
                UIBuild.Pin(_fill.rectTransform, new Vector2(0f, 0.5f),
                    new Vector2(SlotSize, SlotSize), new Vector2(index * (SlotSize + SlotGap), 0f));
                _fill.raycastTarget = false;

                _glyph = UIBuild.Text("Glyph", _fill.transform, "", UITheme.FontCaption,
                    UITheme.TextPrimary, TextAlignmentOptions.Center);
                UIBuild.Stretch(_glyph.rectTransform);

                // 현재 스테이지를 가리키는 아래쪽 앰버 밑줄.
                _currentMark = UIBuild.Solid("CurrentMark", _fill.transform, UITheme.Accent);
                UIBuild.Pin(_currentMark.rectTransform, new Vector2(0.5f, 0f),
                    new Vector2(SlotSize - 6f, 2f), new Vector2(0f, -4f));
                _currentMark.raycastTarget = false;
                _currentMark.enabled = false;
            }

            public void Set(StageKind kind, bool isCurrent)
            {
                (string glyph, Color tint) = Describe(kind);
                _glyph.text = glyph;
                _glyph.color = isCurrent ? UITheme.TextPrimary : tint;
                _fill.color = isCurrent ? Color.white : new Color(1f, 1f, 1f, 0.55f);
                _currentMark.enabled = isCurrent;
            }

            private static (string, Color) Describe(StageKind kind) => kind switch
            {
                StageKind.Boss => ("★", UITheme.Danger),
                StageKind.MidBoss => ("◆", new Color(1f, 0.545f, 0.259f)),
                StageKind.Event => ("?", UITheme.Mana),
                StageKind.Elite => ("▲", UITheme.Accent),
                _ => ("■", UITheme.TextMuted),
            };
        }
    }
}
