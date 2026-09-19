using System.Collections.Generic;
using System.Linq;
using System.Text;
using Combat;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.Screens
{
    /// <summary>
    /// 전투 로그 창. 결과 화면의 [전투 로그]가 연다.
    ///
    /// 위쪽 <b>원인 요약</b>이 먼저 답한다 — 어떻게 끝났는가, 누가 언제 누구에게 쓰러졌는가,
    /// 어느 적이 아군에게 가장 많이 넣었는가, 누가 가장 많이 맞았는가. 그 아래 전체 기록을 턴 순서로 굴려 본다.
    /// 기록이 길어 한 번에 다 보면 찾을 수 없으므로 탭으로 거른다(주요 · 행동 · 피해 · 쓰러짐/반응/상태).
    ///
    /// 한 글자 상자에 수천 줄을 넣으면 TMP의 정점 한도를 넘는다. 줄을 묶음 단위로 나눠 여러 상자에 담는다.
    /// </summary>
    public class CombatLogScreen : ModalScreen
    {
        protected override string CanvasName => "CombatLogCanvas";

        /// <summary>결과 화면(75)보다 위.</summary>
        protected override int SortingOrder => 78;

        protected override string Title => "전투 로그";
        protected override string Caption => "COMBAT LOG";
        protected override Vector2 AnchorMin => new(0.12f, 0.06f);
        protected override Vector2 AnchorMax => new(0.88f, 0.94f);
        protected override bool CloseOnBackdrop => true;

        private enum Filter { All, Important, Actions, Damage, Events }

        private static readonly (Filter Filter, string Label)[] Tabs =
        {
            (Filter.Important, "주요"),
            (Filter.All, "전체"),
            (Filter.Actions, "행동"),
            (Filter.Damage, "피해"),
            (Filter.Events, "쓰러짐 · 반응 · 상태"),
        };

        /// <summary>글자 상자 하나에 담는 줄 수. TMP 한 메시의 정점 한도 안에 넉넉히 들어온다.</summary>
        private const int LinesPerChunk = 80;

        private TextMeshProUGUI _summary;
        private RectTransform _listContent;
        private ScrollRect _listScroll;
        private TextMeshProUGUI _count;
        private readonly List<Button> _tabButtons = new();
        private Filter _filter = Filter.Important;

        protected override void Build()
        {
            // ── 원인 요약 ──
            Image summaryPanel = UIBuild.Panel("Summary", Body, UITheme.SurfaceSunken, UIShapes.Corner.Diagonal, 10,
                UITheme.Outline, 1);
            UIBuild.Anchor(summaryPanel.rectTransform, new Vector2(0f, 0.74f), new Vector2(1f, 1f));

            _summary = UIBuild.Text("SummaryText", summaryPanel.transform, "", UITheme.FontCaption,
                UITheme.TextPrimary, TextAlignmentOptions.TopLeft, wrap: true);
            _summary.richText = true;
            UIBuild.Stretch(_summary.rectTransform, 16f, 10f);

            // ── 탭 ──
            RectTransform tabRow = UIBuild.Container("Tabs", Body);
            UIBuild.Anchor(tabRow, new Vector2(0f, 0.665f), new Vector2(1f, 0.725f));
            float x = 0f;
            foreach ((Filter filter, string label) in Tabs)
            {
                Filter captured = filter;
                float width = 40f + label.Length * 16f;
                Button tab = UIBuild.Button($"Tab{filter}", tabRow, label, () => SetFilter(captured),
                    false, UITheme.FontCaption);
                UIBuild.Pin(tab.image.rectTransform, new Vector2(0f, 0.5f), new Vector2(width, 34f), new Vector2(x, 0f));
                x += width + 8f;
                _tabButtons.Add(tab);
            }

            _count = UIBuild.Text("Count", tabRow, "", UITheme.FontMicro, UITheme.TextMuted,
                TextAlignmentOptions.MidlineRight);
            UIBuild.Anchor(_count.rectTransform, new Vector2(0.6f, 0f), new Vector2(1f, 1f));

            // ── 기록 ──
            _listContent = UIBuild.ScrollArea("LogScroll", Body, out _listScroll);
            var viewport = (RectTransform)_listScroll.transform;
            UIBuild.Anchor(viewport, new Vector2(0f, 0f), new Vector2(1f, 0.65f));
            _listScroll.scrollSensitivity = 60f;

            var layout = _listContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.padding = new RectOffset(10, 18, 6, 10);
            layout.spacing = 0f;
            var fitter = _listContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        public override void Show()
        {
            base.Show();
            _summary.text = ComposeSummary();
            SetFilter(_filter);
        }

        private void SetFilter(Filter filter)
        {
            _filter = filter;
            for (int i = 0; i < _tabButtons.Count; i++)
            {
                bool on = Tabs[i].Filter == filter;
                _tabButtons[i].image.sprite = UIShapes.CutCorner(8, on ? UITheme.AccentFaint : UITheme.SurfaceRaised,
                    UIShapes.Corner.Diagonal, on ? UITheme.Accent : UITheme.Outline, on ? 2 : 1);
            }
            RebuildList();
        }

        private void RebuildList()
        {
            UIBuild.Clear(_listContent);

            List<CombatLog.Entry> entries = CombatLog.Current.Where(Matches).ToList();
            _count.text = CombatLog.Dropped > 0
                ? $"{entries.Count:N0}줄 · 앞부분 {CombatLog.Dropped:N0}줄은 한도로 생략"
                : $"{entries.Count:N0}줄";

            if (entries.Count == 0)
            {
                AddChunk(Paint("표시할 기록이 없습니다.", UITheme.TextMuted));
            }

            var builder = new StringBuilder();
            int lines = 0;
            int lastTurn = int.MinValue;
            foreach (CombatLog.Entry entry in entries)
            {
                if (lines > 0) builder.Append('\n');
                builder.Append(FormatLine(entry, entry.Turn != lastTurn));
                lastTurn = entry.Turn;
                if (++lines < LinesPerChunk) continue;

                AddChunk(builder.ToString());
                builder.Clear();
                lines = 0;
            }
            if (lines > 0) AddChunk(builder.ToString());

            // 원인은 대개 끝에 있다. 패배 직전이 먼저 보이도록 맨 아래에서 연다.
            Canvas.ForceUpdateCanvases();
            _listScroll.verticalNormalizedPosition = _filter == Filter.Important ? 1f : 0f;
        }

        private void AddChunk(string text)
        {
            TextMeshProUGUI chunk = UIBuild.Text("Chunk", _listContent, text, UITheme.FontCaption,
                UITheme.TextPrimary, TextAlignmentOptions.TopLeft, wrap: true);
            chunk.richText = true;
            chunk.lineSpacing = 6f;
            // 목록 안의 글자는 자동 맞춤을 끈다. 켜 두면 칸 높이를 정할 수 없어 레이아웃이 0으로 접힌다.
            Object.Destroy(chunk.GetComponent<UIScaledText>());
            chunk.enableAutoSizing = false;
            chunk.fontSize = UITheme.FontCaption * UITheme.TextScale;
        }

        private bool Matches(CombatLog.Entry entry) => _filter switch
        {
            Filter.Important => entry.Important || entry.Kind is CombatLog.Kind.Defeat or CombatLog.Kind.Info,
            Filter.Actions => entry.Kind is CombatLog.Kind.Action or CombatLog.Kind.Info,
            Filter.Damage => entry.Kind is CombatLog.Kind.Damage or CombatLog.Kind.Evade,
            Filter.Events => entry.Kind is CombatLog.Kind.Defeat or CombatLog.Kind.Reaction
                or CombatLog.Kind.Status or CombatLog.Kind.Info,
            _ => true,
        };

        private static string FormatLine(CombatLog.Entry entry, bool showTurn)
        {
            string turn = Paint(showTurn ? $"T{entry.Turn,-4}" : "     ", UITheme.TextMuted);
            Color side = entry.Ally ? UITheme.Accent : UITheme.Enemy;
            string marker = Paint(entry.Kind switch
            {
                CombatLog.Kind.Action => "▶",
                CombatLog.Kind.Damage => "·",
                CombatLog.Kind.Defeat => "×",
                CombatLog.Kind.Reaction => "◆",
                CombatLog.Kind.Evade => "○",
                CombatLog.Kind.Status => "▼",
                _ => "■",
            }, side);

            Color ink = entry.Kind switch
            {
                CombatLog.Kind.Defeat => entry.Ally ? UITheme.Accent : UITheme.Danger,
                CombatLog.Kind.Info => UITheme.TextPrimary,
                CombatLog.Kind.Damage => UITheme.TextSecondary,
                _ => UITheme.TextPrimary,
            };
            string text = Paint(Escape(entry.Text), ink);
            if (entry.Important) text = $"<b>{text}</b>";
            return $"<mspace=0.62em>{turn}</mspace> {marker}  {text}";
        }

        /// <summary>원인 요약. 가장 먼저 읽혀야 할 네 가지만 적는다.</summary>
        private static string ComposeSummary()
        {
            IReadOnlyList<CombatLog.Entry> all = CombatLog.Current;
            var lines = new List<string>();

            CombatLog.Entry? last = all.Count > 0 ? all[all.Count - 1] : null;
            if (last.HasValue && last.Value.Kind == CombatLog.Kind.Info)
                lines.Add($"<b>{Paint(Escape(last.Value.Text), last.Value.Ally ? UITheme.Accent : UITheme.Danger)}</b>");

            List<CombatLog.Entry> allyDowns = all.Where(e => e.Kind == CombatLog.Kind.Defeat && !e.Ally).ToList();
            lines.Add(allyDowns.Count == 0
                ? $"{Label("쓰러진 아군")} 없음"
                : $"{Label("쓰러진 아군")} " + string.Join("   ",
                    allyDowns.Select(e => Paint($"T{e.Turn} {Escape(e.Text.Replace("아군 ", ""))}", UITheme.Danger))));

            if (CombatLog.EnemyThreat.Count > 0)
            {
                long total = CombatLog.EnemyThreat.Values.Sum();
                string threats = string.Join("   ", CombatLog.EnemyThreat.OrderByDescending(p => p.Value).Take(3)
                    .Select(p => $"{Escape(p.Key)} {p.Value:N0}" +
                                 Paint($" ({(total > 0 ? 100 * p.Value / total : 0)}%)", UITheme.TextMuted)));
                lines.Add($"{Label("아군에게 가장 많이 넣은 적")} {threats}");
            }

            if (CombatLog.AllyDamageTaken.Count > 0)
            {
                string taken = string.Join("   ", CombatLog.AllyDamageTaken.OrderByDescending(p => p.Value).Take(3)
                    .Select(p => $"{Escape(p.Key)} {p.Value:N0}"));
                lines.Add($"{Label("가장 많이 맞은 아군")} {taken}");
            }

            if (all.Count == 0) lines.Add(Paint("이번 전투의 기록이 없습니다.", UITheme.TextMuted));
            return string.Join("\n", lines);
        }

        private static string Label(string text) => Paint(text, UITheme.TextMuted) + "  ";

        private static string Paint(string text, Color color) =>
            $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";

        /// <summary>이름에 꺾쇠가 들어가도 태그로 읽히지 않게 한다.</summary>
        private static string Escape(string text) => string.IsNullOrEmpty(text) ? "" : text.Replace("<", "<​");
    }
}
