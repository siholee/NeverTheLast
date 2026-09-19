using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseClasses;
using Codes.Base;
using Core;
using Helpers;
using Managers.UI.Core;
using static Managers.UI.Core.CodeText;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.Screens
{
    /// <summary>
    /// 자료실 — ESC 메뉴에서 여는 게임 내 위키.
    ///
    /// TAB 창(<see cref="CodexScreen"/>)이 <b>지금 내 파티</b>를 보여 준다면, 여기는 런과 무관한
    /// <b>게임 전체의 도감</b>이다. 캐릭터 · 코드 · 장비 · 적 · 용어 다섯 갈래를 왼쪽 목록과
    /// 오른쪽 본문으로 편다.
    ///
    /// 원본은 전부 데이터 파일이다(10_units · 20_codes · 30_synergies · 40_items · 60_enemies ·
    /// 80_stages). 따로 적어 둔 문장은 '용어'의 기본 규칙뿐이고, 나머지는 파일이 바뀌면
    /// 그대로 따라 바뀐다. 표에서 빠진 항목은 조용히 비우지 않고 무엇이 없는지 적는다.
    ///
    /// ESC 메뉴 위에 뜨므로 시간은 메뉴가 이미 멈춰 두었다. 여기서 따로 멈추지 않는다.
    /// </summary>
    public class WikiScreen : ModalScreen
    {
        private enum Category
        {
            Characters,
            Codes,
            Equipment,
            Enemies,
            Glossary,
        }

        private static readonly (Category Category, string Label)[] Categories =
        {
            (Category.Characters, "캐릭터"),
            (Category.Codes, "코드"),
            (Category.Equipment, "장비"),
            (Category.Enemies, "적"),
            (Category.Glossary, "용어"),
        };

        /// <summary>유닛 ID는 진영마다 20칸 블록이다(Detail_03 §7.4).</summary>
        private static readonly string[] FactionNames =
        {
            "아카샤", "그리스", "다카마가하라", "베다", "노르드", "로마", "이집트", "메히코", "갈리아",
        };

        private static readonly string[] StatOrder = { "STR", "DEX", "CON", "INT", "LUK" };

        private const float TabRowHeight = 36f;
        private const float ListWidth = 0.27f;
        private const float RowHeight = 30f;
        private const float PortraitSize = 132f;

        /// <summary>목록 한 줄. 본문은 누를 때 만든다 — 수백 항목의 문장을 미리 짜 둘 이유가 없다.</summary>
        private sealed class Entry
        {
            public string Group;
            public string Title;
            public string Sub;
            public Color Tint = UITheme.TextPrimary;
            public string Portrait;
            public Func<string> Body;
            public string Search;
        }

        protected override string CanvasName => "WikiCanvas";

        /// <summary>ESC 메뉴보다 위, 설정·확인 창보다 아래.</summary>
        protected override int SortingOrder => UITheme.LayerWiki;

        protected override string Title => "자료실";
        protected override string Caption => "ARCHIVE";
        protected override Vector2 AnchorMin => new(0.05f, 0.05f);
        protected override Vector2 AnchorMax => new(0.95f, 0.95f);
        protected override bool CloseOnBackdrop => true;

        private readonly Dictionary<Category, Button> _tabs = new();
        private readonly Dictionary<Category, List<Entry>> _cache = new();

        /// <summary>지금 목록에 그려진 줄. 선택 강조를 다시 칠할 때 쓴다.</summary>
        private readonly Dictionary<Button, Entry> _rows = new();

        private Category _category = Category.Characters;
        private Entry _selected;
        private string _filter = "";

        private TMP_InputField _search;
        private RectTransform _listContent;
        private ScrollRect _listScroll;
        private RectTransform _bodyContent;
        private ScrollRect _bodyScroll;
        private TextMeshProUGUI _bodyText;
        private Image _portrait;

        // 본문 위에 고정되는 머리. 굴려도 사라지지 않는다.
        private TextMeshProUGUI _breadcrumb;
        private CanvasGroup _stickyIdentity;
        private Image _stickyPortrait;
        private TextMeshProUGUI _stickyTitle;

        /// <summary>고정 머리의 높이. 본문 스크롤 영역이 이만큼 아래에서 시작한다.</summary>
        private const float StickyHeight = 58f;

        /// <summary>본문을 이만큼 굴리면 큰 제목이 가려진 것으로 보고 고정 머리에 이름을 띄운다.</summary>
        private const float StickyRevealScroll = 70f;

        // ── 조립 ─────────────────────────────────────────────────────

        protected override void Build()
        {
            BuildTabs();
            BuildSearch();
            BuildList();
            BuildBody();
        }

        private void BuildTabs()
        {
            for (int i = 0; i < Categories.Length; i++)
            {
                (Category category, string label) = Categories[i];
                Button tab = UIBuild.Button($"Tab_{category}", Body, label, () => SetCategory(category),
                    false, UITheme.FontCaption);
                UIBuild.Pin(tab.image.rectTransform, new Vector2(0f, 1f), new Vector2(110f, TabRowHeight - 4f),
                    new Vector2(i * 116f, 0f));
                _tabs[category] = tab;
            }
        }

        /// <summary>이름 검색. 코드만 수백 개라 목록을 눈으로 훑어서는 찾을 수 없다.</summary>
        private void BuildSearch()
        {
            Image background = UIBuild.Panel("Search", Body, UITheme.SurfaceSunken,
                UIShapes.Corner.Diagonal, 6, UITheme.Outline, 1);
            UIBuild.Pin(background.rectTransform, new Vector2(1f, 1f), new Vector2(320f, TabRowHeight - 4f),
                Vector2.zero);

            RectTransform viewport = UIBuild.Container("Viewport", background.transform);
            UIBuild.Stretch(viewport, 12f, 4f);
            viewport.gameObject.AddComponent<RectMask2D>();

            TextMeshProUGUI placeholder = UIBuild.Text("Placeholder", viewport, "이름 · 설명 검색",
                UITheme.FontCaption, UITheme.TextMuted);
            UIBuild.Stretch(placeholder.rectTransform);

            TextMeshProUGUI text = UIBuild.Text("Text", viewport, "", UITheme.FontCaption, UITheme.TextPrimary);
            UIBuild.Stretch(text.rectTransform);
            text.richText = false;

            _search = background.gameObject.AddComponent<TMP_InputField>();
            _search.textViewport = viewport;
            _search.textComponent = text;
            _search.placeholder = placeholder;
            _search.targetGraphic = background;
            _search.lineType = TMP_InputField.LineType.SingleLine;
            _search.onValueChanged.AddListener(value =>
            {
                _filter = value?.Trim() ?? "";
                RebuildList(keepSelection: true);
            });
        }

        private void BuildList()
        {
            Image frame = UIBuild.Panel("ListFrame", Body, UITheme.SurfaceSunken,
                UIShapes.Corner.Diagonal, 10, UITheme.Outline, 1);
            UIBuild.Anchor(frame.rectTransform, new Vector2(0f, 0f), new Vector2(ListWidth, 1f));
            frame.rectTransform.offsetMax = new Vector2(0f, -(TabRowHeight + 8f));

            _listContent = UIBuild.ScrollArea("List", frame.transform, out _listScroll);
            UIBuild.Stretch((RectTransform)_listScroll.transform, 6f, 8f);
        }

        private void BuildBody()
        {
            Image frame = UIBuild.Panel("BodyFrame", Body, UITheme.SurfaceSunken,
                UIShapes.Corner.Diagonal, 10, UITheme.Outline, 1);
            UIBuild.Anchor(frame.rectTransform, new Vector2(ListWidth, 0f), new Vector2(1f, 1f));
            frame.rectTransform.offsetMin = new Vector2(12f, 0f);
            frame.rectTransform.offsetMax = new Vector2(0f, -(TabRowHeight + 8f));

            BuildStickyHeader(frame.transform);

            _bodyContent = UIBuild.ScrollArea("Body", frame.transform, out _bodyScroll);
            UIBuild.Stretch((RectTransform)_bodyScroll.transform, 20f, 14f);
            ((RectTransform)_bodyScroll.transform).offsetMax = new Vector2(-20f, -StickyHeight - 6f);
            _bodyScroll.onValueChanged.AddListener(_ => RefreshStickyIdentity());

            var portraitObject = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitObject.transform.SetParent(_bodyContent, false);
            _portrait = portraitObject.GetComponent<Image>();
            _portrait.preserveAspect = true;
            _portrait.raycastTarget = false;
            UIBuild.Pin(_portrait.rectTransform, new Vector2(1f, 1f), new Vector2(PortraitSize, PortraitSize),
                Vector2.zero);

            _bodyText = UIBuild.Text("Text", _bodyContent, "", UITheme.FontBody, UITheme.TextPrimary,
                TextAlignmentOptions.TopLeft, true);
            _bodyText.richText = true;
            _bodyText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _bodyText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _bodyText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _bodyText.rectTransform.anchoredPosition = Vector2.zero;
        }

        /// <summary>
        /// 본문 위 고정 머리 — 지금 어디를 보고 있는가(자료실 › 분류 › 무리 · 몇 번째)와,
        /// 본문을 굴려 큰 제목이 사라진 뒤에는 대상의 작은 초상화와 이름.
        ///
        /// 예전에는 본문 안에만 이름과 초상화가 있어, 긴 캐릭터 문서를 굴리면 누구의 문서인지와
        /// 현재 위치가 함께 사라졌다(QA).
        /// </summary>
        private void BuildStickyHeader(Transform frame)
        {
            Image bar = UIBuild.Panel("Sticky", frame, UITheme.Surface, UIShapes.Corner.Diagonal, 8);
            bar.raycastTarget = false;
            RectTransform rect = bar.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(-12f, StickyHeight);
            rect.anchoredPosition = new Vector2(0f, -6f);

            Image rule = UIBuild.Divider("Rule", bar.transform);
            UIBuild.Anchor(rule.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), 14f, 0f);
            rule.rectTransform.sizeDelta = new Vector2(rule.rectTransform.sizeDelta.x, 1f);

            _breadcrumb = UIBuild.Text("Breadcrumb", bar.transform, "", UITheme.FontCaption, UITheme.TextMuted,
                TextAlignmentOptions.MidlineRight);
            _breadcrumb.richText = true;
            UIBuild.Anchor(_breadcrumb.rectTransform, new Vector2(0.45f, 0f), new Vector2(1f, 1f), 16f, 0f);

            RectTransform identity = UIBuild.Container("Identity", bar.transform);
            UIBuild.Anchor(identity, new Vector2(0f, 0f), new Vector2(0.55f, 1f), 12f, 0f);
            _stickyIdentity = UIBuild.Group(identity.gameObject);
            _stickyIdentity.blocksRaycasts = false;
            _stickyIdentity.alpha = 0f;

            var portraitObject = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitObject.transform.SetParent(identity, false);
            _stickyPortrait = portraitObject.GetComponent<Image>();
            _stickyPortrait.preserveAspect = true;
            _stickyPortrait.raycastTarget = false;
            UIBuild.Pin(_stickyPortrait.rectTransform, new Vector2(0f, 0.5f), new Vector2(44f, 44f), Vector2.zero);

            _stickyTitle = UIBuild.Text("Title", identity, "", UITheme.FontHeading, UITheme.TextPrimary);
            UIBuild.Stretch(_stickyTitle.rectTransform);
            _stickyTitle.rectTransform.offsetMin = new Vector2(54f, 0f);
            _stickyTitle.overflowMode = TextOverflowModes.Ellipsis;
        }

        private void RefreshStickyHeader(Entry entry)
        {
            if (_breadcrumb == null) return;

            string category = Categories.FirstOrDefault(pair => pair.Category == _category).Label ?? "";
            string location = $"자료실  ›  {category}";
            if (!string.IsNullOrEmpty(entry?.Group)) location += $"  ›  {entry.Group}";

            List<Entry> visible = VisibleEntries();
            int index = entry == null ? -1 : visible.IndexOf(entry);
            if (index >= 0)
            {
                location += $"   <color=#{ColorUtility.ToHtmlStringRGB(UITheme.TextSecondary)}>{index + 1} / {visible.Count}</color>";
            }
            _breadcrumb.text = location;

            Sprite portrait = entry == null ? null : SpriteResource.LoadPortrait(entry.Portrait);
            _stickyPortrait.sprite = portrait;
            _stickyPortrait.enabled = portrait != null;
            _stickyTitle.text = entry?.Title ?? "";
            _stickyTitle.color = entry?.Tint ?? UITheme.TextPrimary;
            _stickyTitle.rectTransform.offsetMin = new Vector2(portrait != null ? 54f : 0f, 0f);
            RefreshStickyIdentity();
        }

        /// <summary>큰 제목이 보이는 동안에는 숨겨 둔다. 같은 이름이 두 번 나란히 보이지 않게.</summary>
        private void RefreshStickyIdentity()
        {
            if (_stickyIdentity == null || _bodyContent == null) return;
            _stickyIdentity.alpha = _bodyContent.anchoredPosition.y > StickyRevealScroll ? 1f : 0f;
        }

        // ── 열고 닫기 ────────────────────────────────────────────────

        public override void Show()
        {
            base.Show();
            SetCategory(_category);
        }

        public void Toggle()
        {
            if (IsVisible) Hide();
            else Show();
        }

        private void SetCategory(Category category)
        {
            bool changed = category != _category;
            _category = category;
            if (changed) _selected = null;

            foreach ((Category key, Button tab) in _tabs) Tint(tab, key == _category);
            RebuildList(keepSelection: !changed);
        }

        private static void Tint(Button button, bool active)
        {
            button.image.sprite = UIShapes.CutCorner(8,
                active ? UITheme.Accent : UITheme.SurfaceRaised, UIShapes.Corner.Diagonal);
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.color = active ? UITheme.TextOnAccent : UITheme.TextSecondary;
        }

        // ── 목록 ─────────────────────────────────────────────────────

        private List<Entry> EntriesOf(Category category)
        {
            if (_cache.TryGetValue(category, out List<Entry> cached)) return cached;

            List<Entry> built = category switch
            {
                Category.Characters => CharacterEntries(),
                Category.Codes => CodeEntries(),
                Category.Equipment => EquipmentEntries(),
                Category.Enemies => EnemyEntries(),
                _ => GlossaryEntries(),
            };
            _cache[category] = built;
            return built;
        }

        private void RebuildList(bool keepSelection)
        {
            UIBuild.Clear(_listContent);
            _rows.Clear();

            List<Entry> entries = VisibleEntries();

            float y = 0f;
            string group = null;
            foreach (Entry entry in entries)
            {
                if (entry.Group != group)
                {
                    group = entry.Group;
                    AddGroupHeading(group, ref y);
                }

                AddRow(entry, ref y);
            }

            if (entries.Count == 0)
            {
                TextMeshProUGUI empty = UIBuild.Text("Empty", _listContent,
                    _filter.Length > 0 ? $"'{_filter}'에 맞는 항목이 없다." : "항목이 없다 — 데이터 파일을 읽지 못했다.",
                    UITheme.FontCaption, UITheme.TextMuted, TextAlignmentOptions.TopLeft, true);
                Place(empty.rectTransform, y, 40f, 8f);
                y += 40f;
            }

            _listContent.sizeDelta = new Vector2(0f, y + 8f);
            _listContent.anchoredPosition = Vector2.zero;

            if (!keepSelection || _selected == null || !entries.Contains(_selected))
            {
                _selected = entries.FirstOrDefault();
            }

            ShowEntry(_selected);
            HighlightSelection();
        }

        /// <summary>지금 분류에서 검색어에 맞는 항목. 목록과 고정 머리의 "몇 번째"가 같은 목록을 센다.</summary>
        private List<Entry> VisibleEntries() => EntriesOf(_category)
            .Where(entry => _filter.Length == 0 ||
                            entry.Search.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();

        private void AddGroupHeading(string text, ref float y)
        {
            if (string.IsNullOrEmpty(text)) return;

            y += y > 0f ? 8f : 2f;
            TextMeshProUGUI label = UIBuild.Label("Group", _listContent, text, UITheme.FontMicro, UITheme.Accent);
            Place(label.rectTransform, y, 18f, 8f);
            y += 20f;
        }

        /// <summary>
        /// 목록 한 줄. 클릭은 Button으로 받는다 — EventTrigger는 휠까지 먹어 목록이 굴러가지 않는다.
        /// </summary>
        private void AddRow(Entry entry, ref float y)
        {
            Button row = UIBuild.Button("Row", _listContent, null, () => Select(entry), false);
            Place(row.image.rectTransform, y, RowHeight, 2f);
            row.image.sprite = UIShapes.CutCorner(6, new Color(1f, 1f, 1f, 0f), UIShapes.Corner.Diagonal);

            TextMeshProUGUI title = UIBuild.Text("Title", row.transform, entry.Title, UITheme.FontCaption,
                entry.Tint);
            title.overflowMode = TextOverflowModes.Ellipsis;
            UIBuild.Anchor(title.rectTransform, new Vector2(0f, 0f), new Vector2(0.64f, 1f), 10f, 0f);

            if (!string.IsNullOrEmpty(entry.Sub))
            {
                TextMeshProUGUI sub = UIBuild.Text("Sub", row.transform, entry.Sub, UITheme.FontMicro,
                    UITheme.TextMuted, TextAlignmentOptions.MidlineRight);
                sub.overflowMode = TextOverflowModes.Ellipsis;
                UIBuild.Anchor(sub.rectTransform, new Vector2(0.64f, 0f), new Vector2(1f, 1f), 10f, 0f);
            }

            row.gameObject.name = $"Row_{entry.Title}";
            _rows[row] = entry;
            y += RowHeight + 2f;
        }

        private void Select(Entry entry)
        {
            _selected = entry;
            ShowEntry(entry);
            HighlightSelection();
        }

        private void HighlightSelection()
        {
            foreach ((Button row, Entry entry) in _rows)
            {
                if (row == null) continue;
                bool active = entry == _selected;
                row.image.sprite = UIShapes.CutCorner(6,
                    active ? UITheme.AccentFaint : new Color(1f, 1f, 1f, 0f), UIShapes.Corner.Diagonal,
                    active ? UITheme.Accent : default, active ? 1 : 0);
            }
        }

        private static void Place(RectTransform rect, float y, float height, float padX)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(-padX * 2f, height);
            rect.anchoredPosition = new Vector2(0f, -y);
        }

        // ── 본문 ─────────────────────────────────────────────────────

        private void ShowEntry(Entry entry)
        {
            string text = entry?.Body?.Invoke() ?? "";
            Sprite portrait = entry == null ? null : SpriteResource.LoadPortrait(entry.Portrait);

            _portrait.sprite = portrait;
            _portrait.gameObject.SetActive(portrait != null);

            // 처음 여는 프레임에는 캔버스 크기가 아직 잡히지 않아 폭이 틀리게 읽힌다.
            // 폭이 틀리면 줄 수를 적게 재서 본문 끝이 잘린다.
            Canvas.ForceUpdateCanvases();

            // 초상화가 있으면 오른쪽 위를 비워 둔다. 머리글이 그림 밑으로 파고들지 않게.
            float width = ((RectTransform)_bodyScroll.transform).rect.width;
            _bodyText.text = text;
            _bodyText.rectTransform.sizeDelta = new Vector2(0f, 0f);
            _bodyText.margin = new Vector4(0f, 0f, portrait != null ? PortraitSize + 16f : 0f, 0f);

            float height = _bodyText.GetPreferredValues(text, Mathf.Max(200f, width - _bodyText.margin.z), 0f).y;
            height = Mathf.Max(height, portrait != null ? PortraitSize : 0f);
            _bodyText.rectTransform.sizeDelta = new Vector2(0f, height);
            _bodyContent.sizeDelta = new Vector2(0f, height + 24f);
            _bodyContent.anchoredPosition = Vector2.zero;
            RefreshStickyHeader(entry);
        }

        // ── 본문 조판 ────────────────────────────────────────────────
        // TMP 서식 문자열을 직접 짠다. Plain · Paint · 코드 덩어리는 캐릭터 선택과 같이 쓰는 CodeText에 있다.

        private static void CodeBlock(StringBuilder sb, CodeCatalog.Slot slot, int codeId, string kind) =>
            CodeText.AppendBlock(sb, slot, codeId, kind);

        private static void Headline(StringBuilder sb, string title, string sub, Color tint)
        {
            sb.Append($"<size={UITheme.FontDisplay}><b>{Paint(Plain(title), tint)}</b></size>\n");
            if (!string.IsNullOrWhiteSpace(sub))
                sb.Append($"<size={UITheme.FontBody}>{Paint(Plain(sub.Trim()), UITheme.TextSecondary)}</size>\n");
            sb.Append('\n');
        }

        private static void Section(StringBuilder sb, string title)
        {
            sb.Append($"\n<size={UITheme.FontHeading}><b>{Paint(Plain(title), UITheme.Accent)}</b></size>\n");
        }

        private static void Field(StringBuilder sb, string label, string value, Color? tint = null)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            sb.Append($"{Paint(Plain(label), UITheme.TextMuted)}   {Paint(Plain(value), tint ?? UITheme.TextPrimary)}\n");
        }

        private static void Paragraph(StringBuilder sb, string text, Color? tint = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            sb.Append(Paint(Plain(text.Trim()), tint ?? UITheme.TextSecondary)).Append('\n');
        }

        private static string ElementName(string element) => element switch
        {
            "Pyro" => "불",
            "Hydro" => "물",
            "Anemo" => "바람",
            "Electro" => "번개",
            "Dendro" => "풀",
            "Cryo" => "얼음",
            "Geo" => "바위",
            _ => string.IsNullOrWhiteSpace(element) ? "무속성" : element,
        };

        private static string ProficiencyName(string key) =>
            Enum.TryParse(key, true, out EquipmentProficiency proficiency)
                ? ItemTooltip.ProficiencyName(proficiency)
                : key;

        // ── 캐릭터 ───────────────────────────────────────────────────

        private static List<UnitData> Units() =>
            GameManager.Instance?.unitDataList?.units ?? new List<UnitData>();

        private List<Entry> CharacterEntries()
        {
            return Units()
                .Where(unit => unit != null)
                .OrderBy(unit => unit.id / 20)
                .ThenBy(unit => unit.id)
                .Select(unit =>
                {
                    bool available = SynergyCatalog.IsAvailable(unit.id);
                    return new Entry
                    {
                        Group = FactionOf(unit.id),
                        Title = unit.name,
                        Sub = available ? TypeName(unit.characterType) : "미해금",
                        Tint = available ? UITheme.TextPrimary : UITheme.TextMuted,
                        Portrait = unit.portrait,
                        Body = () => CharacterBody(unit),
                        Search = $"{unit.name} {unit.tagline} {FactionOf(unit.id)} {ElementName(unit.element)}",
                    };
                })
                .ToList();
        }

        private static string FactionOf(int unitId)
        {
            int block = unitId / 20;
            return block >= 0 && block < FactionNames.Length ? FactionNames[block] : "기타";
        }

        private static string TypeName(string characterType) => characterType switch
        {
            "Starter" => "메인",
            "Support" => "서포트",
            "Locked" => "합류",
            _ => characterType,
        };

        private static string CharacterBody(UnitData unit)
        {
            var sb = new StringBuilder();
            Headline(sb, unit.name, unit.tagline, UITheme.TextPrimary);

            Section(sb, "기본 정보");
            Field(sb, "진영", FactionOf(unit.id));
            Field(sb, "분류", unit.characterType switch
            {
                "Starter" => "메인으로 시작할 수 있다",
                "Support" => "서포트 카드 전용",
                "Locked" => "런 중에 아군으로 합류하면 영구 해금",
                _ => unit.characterType,
            });
            Field(sb, "해금", SynergyCatalog.IsAvailable(unit.id) ? "편성 가능" : "아직 손에 넣지 못했다",
                SynergyCatalog.IsAvailable(unit.id) ? UITheme.Positive : UITheme.TextMuted);
            Field(sb, "원소", ElementName(unit.element));

            List<string> subs = unit.subStats is { Count: > 0 } ? unit.subStats
                : string.IsNullOrWhiteSpace(unit.subStat) ? new List<string>() : new List<string> { unit.subStat };
            Field(sb, "주스탯", unit.mainStat, UITheme.Accent);
            Field(sb, "부스탯", subs.Count > 0 ? string.Join(" · ", subs) : "없음");
            Field(sb, "숙련", unit.startingProficiencies is { Count: > 0 }
                ? string.Join(" · ", unit.startingProficiencies.Select(ProficiencyName))
                : "없음 — 맨손");
            Field(sb, "궁극기 자원", string.IsNullOrWhiteSpace(unit.ultimateResourceType)
                ? $"마나 {(unit.manaMax > 0 ? unit.manaMax : Entities.Unit.DefaultManaMax)}"
                : $"{unit.ultimateResourceName ?? unit.ultimateResourceType} {unit.ultimateResourceMax}");

            Section(sb, "기초 스탯");
            AppendStats(sb, unit.mainStat, subs,
                (unit.strBase, unit.strIncrementLvl), (unit.dexBase, unit.dexIncrementLvl),
                (unit.conBase, unit.conIncrementLvl), (unit.intBase, unit.intIncrementLvl),
                (unit.lukBase, unit.lukIncrementLvl));

            Section(sb, "코드");
            AppendUnitCodes(sb, unit.codes, unit.levelPassives);

            AppendSynergy(sb, unit);
            return sb.ToString();
        }

        /// <summary>Lv.1 값과 레벨당 성장. 주스탯은 ★·앰버, 부스탯은 ◆·흰색, 나머지는 흐리게.</summary>
        private static void AppendStats(StringBuilder sb, string mainStat, List<string> subStats,
            params (int Base, int PerLevel)[] values)
        {
            for (int i = 0; i < StatOrder.Length && i < values.Length; i++)
            {
                string key = StatOrder[i];
                bool isMain = string.Equals(mainStat, key, StringComparison.OrdinalIgnoreCase);
                bool isSub = subStats.Any(sub => string.Equals(sub, key, StringComparison.OrdinalIgnoreCase));
                Color tint = isMain ? UITheme.Accent : isSub ? UITheme.TextPrimary : UITheme.TextSecondary;

                sb.Append($"<mspace=0.62em>{Paint(key + (isMain ? "★" : isSub ? "◆" : " "), tint)}</mspace>  ");
                sb.Append(Paint($"{values[i].Base}", tint));
                sb.Append($"<size={UITheme.FontMicro}>{Paint($"   레벨당 +{values[i].PerLevel}", UITheme.TextMuted)}</size>\n");
            }
        }

        private static void AppendUnitCodes(StringBuilder sb, Dictionary<string, int> codes,
            List<LevelPassiveData> levelPassives)
        {
            if (codes != null)
            {
                if (codes.TryGetValue("passive", out int passive)) CodeBlock(sb, CodeCatalog.Slot.Passive, passive, "고유 패시브");
                if (codes.TryGetValue("normal", out int normal)) CodeBlock(sb, CodeCatalog.Slot.Normal, normal, "일반행동");
                if (codes.TryGetValue("special", out int special)) CodeBlock(sb, CodeCatalog.Slot.Special, special, "특수행동");
                if (codes.TryGetValue("ultimate", out int ultimate)) CodeBlock(sb, CodeCatalog.Slot.Ultimate, ultimate, "궁극기");
            }

            if (levelPassives is not { Count: > 0 }) return;

            Section(sb, "해금 패시브");
            foreach (LevelPassiveData passive in levelPassives.Where(p => p != null).OrderBy(p => p.unlockLevel))
            {
                CodeBlock(sb, CodeCatalog.Slot.Passive, passive.codeId, $"Lv.{passive.unlockLevel}");
            }
        }

        /// <summary>
        /// 이 캐릭터를 어떻게 쓰는가. `30_synergies.yaml`이 원본이다.
        /// 메인이 될 수 있으면 고점·대체 두 벌의 편성을, 서포트 전용이면 이 캐릭터를 부르는 메인을 적는다.
        /// 아직 손에 넣지 못한 이름은 흐리게 찍어 "지금 짤 수 있는 조합"이 보이게 한다.
        /// </summary>
        private static void AppendSynergy(StringBuilder sb, UnitData unit)
        {
            SynergyUnitData profile = SynergyCatalog.UnitOf(unit.id);
            Section(sb, "추천 조합");

            if (profile == null)
            {
                // 표가 통째로 안 읽힌 것과 이 캐릭터만 빠진 것은 원인이 전혀 다르다. 갈라서 적는다.
                Paragraph(sb, SynergyCatalog.IsLoaded
                    ? $"30_synergies.yaml의 units에 ID {unit.id}({unit.name})가 없다."
                    : "30_synergies.yaml을 읽지 못했다. 콘솔 로그를 확인한다.", UITheme.Danger);
                return;
            }

            if (profile.roles is { Count: > 0 })
                Field(sb, "역할", string.Join(" · ", profile.roles.Select(SynergyCatalog.RoleName)));
            if (profile.supportValue != null)
            {
                Field(sb, "전투 기여", Pips(profile.supportValue.combat), UITheme.Accent);
                Field(sb, "육성 기여", Pips(profile.supportValue.training), UITheme.Accent);
            }

            Field(sb, "권장 열", profile.row == "Front" ? "전열" : "후열");

            if (profile.provides is { Count: > 0 })
            {
                sb.Append('\n').Append(Paint("강점", UITheme.TextMuted)).Append('\n');
                foreach (SynergyProvideData provide in profile.provides.Where(p => !string.IsNullOrWhiteSpace(p?.text)))
                    Paragraph(sb, $"· {provide.text}");
            }

            if (!string.IsNullOrWhiteSpace(profile.caution))
            {
                sb.Append('\n').Append(Paint("주의", UITheme.TextMuted)).Append('\n');
                Paragraph(sb, profile.caution, UITheme.Danger);
            }

            SynergyRecommendationData recommendation = SynergyCatalog.RecommendationFor(unit.id);
            if (recommendation != null)
            {
                if (!string.IsNullOrWhiteSpace(recommendation.axis))
                {
                    sb.Append('\n').Append(Paint("축", UITheme.TextMuted)).Append('\n');
                    Paragraph(sb, recommendation.axis, UITheme.TextPrimary);
                }

                AppendLineup(sb, "고점 조합", recommendation.best, unit);
                AppendLineup(sb, "대체 조합", recommendation.basic, unit);
                if (!string.IsNullOrWhiteSpace(recommendation.warning))
                    Paragraph(sb, $"⚠ {recommendation.warning.Trim()}", UITheme.Danger);
                return;
            }

            List<(string MainName, bool IsBest)> callers = SynergyCatalog.RecommendedFor(unit.id);
            sb.Append('\n').Append(Paint($"추천되는 메인 {callers.Count}명", UITheme.TextMuted)).Append('\n');
            if (callers.Count == 0)
            {
                Paragraph(sb, "아직 어떤 메인의 추천 조합에도 들어가지 않는다.", UITheme.TextMuted);
                return;
            }

            foreach ((string mainName, bool isBest) in callers)
                Field(sb, mainName, isBest ? "고점" : "대체", isBest ? UITheme.Accent : UITheme.TextSecondary);
        }

        private static void AppendLineup(StringBuilder sb, string title, SynergyLineupData lineup, UnitData main)
        {
            if (lineup?.members == null || lineup.members.Count == 0) return;

            string archetype = SynergyCatalog.ArchetypeName(lineup.archetype);
            sb.Append('\n').Append(Paint(Plain(string.IsNullOrWhiteSpace(archetype) ? title : $"{title} — {archetype}"),
                UITheme.Accent)).Append('\n');

            var names = new List<string> { Paint(Plain($"{main.name}(메인·{RowLabel(lineup, main.id)})"), UITheme.Accent) };
            foreach (int memberId in lineup.members)
            {
                bool owned = SynergyCatalog.IsAvailable(memberId);
                names.Add(Paint(Plain($"{SynergyCatalog.NameOf(memberId)}({RowLabel(lineup, memberId)})"),
                    owned ? UITheme.TextPrimary : UITheme.TextMuted));
            }

            sb.Append(string.Join(Paint("  ·  ", UITheme.TextMuted), names)).Append('\n');
            if (!string.IsNullOrWhiteSpace(lineup.reason))
                sb.Append($"<size={UITheme.FontCaption}>{Paint(Plain(lineup.reason.Trim()), UITheme.TextMuted)}</size>\n");
        }

        private static string RowLabel(SynergyLineupData lineup, int unitId)
        {
            if (lineup.rows != null && lineup.rows.TryGetValue(unitId, out string row))
                return row == "Front" ? "전열" : "후열";
            return SynergyCatalog.UnitOf(unitId)?.row == "Front" ? "전열" : "후열";
        }

        /// <summary>0~5를 눈금으로. 숫자보다 한눈에 들어온다.</summary>
        private static string Pips(int value)
        {
            int filled = Mathf.Clamp(value, 0, 5);
            return new string('●', filled) + new string('○', 5 - filled);
        }

        // ── 코드 ─────────────────────────────────────────────────────

        /// <summary>
        /// 아군이 쓰는 패시브만 싣는다. 일반행동·궁극기·특수행동은 주인이 하나뿐이라
        /// 캐릭터 본문에서 보는 편이 낫고, 적 코드(1000+)는 적 본문에 있다.
        /// ID 구간은 Detail_11 §1.5를 따른다.
        /// </summary>
        private List<Entry> CodeEntries()
        {
            Dictionary<int, string> owners = UniqueOwners();

            return CodeCatalog.All(CodeCatalog.Slot.Passive)
                .Where(entry => entry.id < 500)
                .Select(entry =>
                {
                    (int order, string group) = CodeFamily(entry.id);
                    bool unique = owners.ContainsKey(entry.id);
                    return (Order: order, Entry: new Entry
                    {
                        Group = group,
                        Title = entry.verbalName ?? $"패시브 #{entry.id}",
                        Sub = unique ? owners[entry.id] : $"#{entry.id}",
                        Tint = unique ? UITheme.CodeUnique : UITheme.TextPrimary,
                        Body = () => CodeBody(entry, owners.GetValueOrDefault(entry.id)),
                        Search = $"{entry.verbalName} {CodeCatalog.Summary(entry.description)}",
                    });
                })
                .OrderBy(pair => pair.Order)
                .ThenBy(pair => pair.Entry.Title)
                .Select(pair => pair.Entry)
                .ToList();
        }

        private static (int Order, string Group) CodeFamily(int codeId) => codeId switch
        {
            >= 1 and <= 179 => (0, "공용 해금 패시브"),
            >= 180 and <= 199 => (1, "공용 · 특수"),
            >= 200 and <= 399 => (2, "고유 패시브"),
            >= 400 and <= 499 => (3, "장비 부여 코드"),
            _ => (4, "기타"),
        };

        /// <summary>고유 패시브 ID → 주인 이름. 고유 패시브는 유닛 ID + 200이지만 표를 직접 읽는 편이 확실하다.</summary>
        private static Dictionary<int, string> UniqueOwners()
        {
            var owners = new Dictionary<int, string>();
            foreach (UnitData unit in Units())
            {
                if (unit?.codes != null && unit.codes.TryGetValue("passive", out int passive))
                    owners.TryAdd(passive, unit.name);
            }

            return owners;
        }

        private static string CodeBody(CodeCatalog.Entry entry, string owner)
        {
            var sb = new StringBuilder();
            Headline(sb, entry.verbalName ?? $"패시브 #{entry.id}", CodeFamily(entry.id).Group,
                owner != null ? UITheme.CodeUnique : UITheme.TextPrimary);

            Section(sb, "효과");
            string summary = CodeCatalog.Summary(entry.description);
            Paragraph(sb, summary.Length > 0 ? summary : "설명이 비어 있다.", UITheme.TextPrimary);

            List<string> markers = CodeCatalog.Markers(entry.description);
            if (markers.Count > 0) Field(sb, "꼬리표", string.Join(" · ", markers));

            Section(sb, "보유");
            if (owner != null) Field(sb, "고유 주인", owner, UITheme.CodeUnique);

            List<string> learners = Units()
                .Where(unit => unit?.levelPassives?.Any(p => p?.codeId == entry.id) == true)
                .Select(unit => $"{unit.name} Lv.{unit.levelPassives.First(p => p.codeId == entry.id).unlockLevel}")
                .ToList();
            if (learners.Count > 0) Paragraph(sb, "레벨로 배우는 캐릭터 — " + string.Join(" · ", learners));

            List<string> items = (GameManager.Instance?.itemDataList?.items ?? new List<ItemData>())
                .Where(item => item?.codeGrants?.Any(grant => grant?.codeId == entry.id &&
                    string.Equals(grant.slot, "passive", StringComparison.OrdinalIgnoreCase)) == true)
                .Select(item => item.name)
                .ToList();
            if (items.Count > 0) Paragraph(sb, "부여하는 장비 — " + string.Join(" · ", items));

            if (owner == null && learners.Count == 0 && items.Count == 0)
                Paragraph(sb, "레벨 해금으로 배우는 캐릭터가 없다. 사건·보상·서포트 전수로만 얻는다.", UITheme.TextMuted);

            Field(sb, "ID", $"passive {entry.id}", UITheme.TextMuted);
            return sb.ToString();
        }

        // ── 장비 ─────────────────────────────────────────────────────

        private List<Entry> EquipmentEntries()
        {
            return (GameManager.Instance?.itemDataList?.items ?? new List<ItemData>())
                .Where(item => item != null && !item.enemyOnly)
                .OrderBy(item => item.IsValuable)
                .ThenBy(item => ItemTooltip.SlotName(item.slot))
                .ThenBy(item => item.category)
                .ThenBy(item => item.rarity)
                .ThenBy(item => item.id)
                .Select(item => new Entry
                {
                    Group = item.IsValuable ? "귀중품" : ItemTooltip.SlotName(item.slot),
                    Title = item.name,
                    Sub = item.IsValuable ? "귀중품" : $"T{Mathf.Max(1, item.rarity)} · {CategoryName(item)}",
                    Tint = UITheme.Rarity(Mathf.Max(1, item.rarity)),
                    Body = () => ItemBody(item),
                    Search = $"{item.name} {item.category} {CategoryName(item)} {ItemTooltip.SlotName(item.slot)}",
                })
                .ToList();
        }

        private static string CategoryName(ItemData item) => ItemTooltip.CategoryName(item.category);

        private static string ItemBody(ItemData item)
        {
            var sb = new StringBuilder();
            Headline(sb, item.name, ItemTooltip.HeadLine(item), UITheme.Rarity(Mathf.Max(1, item.rarity)));

            if (item.IsValuable)
            {
                Section(sb, "귀중품");
                Paragraph(sb, "입을 수 없고 상점에 팔기만 하는 물건이다. 값은 주운 스테이지에서 정해진다.");
                Field(sb, "기본 판매가", $"{item.sellPrice}");
                return sb.ToString();
            }

            Section(sb, "제원");
            foreach (UITooltip.Line line in ItemTooltip.Lines(item).Skip(1))
            {
                if (line.Label == null) Paragraph(sb, line.Value, line.Color);
                else Field(sb, line.Label, line.Value, line.Color);
            }

            if (item.requiredUnitIds is { Count: > 0 })
            {
                Field(sb, "전용", string.Join(" · ", item.requiredUnitIds.Select(SynergyCatalog.NameOf)));
            }

            if (item.shopPrice > 0) Field(sb, "상점 기본가", $"{item.shopPrice}");
            if (item.eventOnly) Field(sb, "획득", "사건에서만 얻는다");

            if (item.codeGrants is { Count: > 0 })
            {
                Section(sb, "부여 코드");
                foreach (EquipmentCodeGrant grant in item.codeGrants.Where(grant => grant != null))
                    {
                    CodeCatalog.Slot slot = CodeCatalog.ParseSlot(grant.slot);
                    CodeBlock(sb, slot, grant.codeId, SlotName(slot));
                }
            }

            List<string> droppers = Enemies()
                .Where(enemy => enemy?.drops?.Contains(item.id) == true)
                .Select(enemy => enemy.name)
                .Distinct()
                .ToList();
            if (droppers.Count > 0)
            {
                Section(sb, "떨구는 적");
                Paragraph(sb, string.Join(" · ", droppers));
            }

            return sb.ToString();
        }

        // ── 적 ───────────────────────────────────────────────────────

        private static List<EnemyData> _enemies;
        private static Dictionary<int, string> _themeNames;

        private static List<EnemyData> Enemies() =>
            _enemies ??= GameManager.Instance?.dataManager?.FetchEnemyDataList()?.enemies ?? new List<EnemyData>();

        /// <summary>적 테마 ID → 스테이지 테마 이름. 여러 스테이지 테마가 같은 적을 쓰면 첫 이름을 쓴다.</summary>
        private static string ThemeName(int enemyThemeId)
        {
            if (_themeNames == null)
            {
                _themeNames = new Dictionary<int, string>();
                StageThemeDataList stages = GameManager.Instance?.dataManager?.FetchStageThemeDataList();
                foreach (StageThemeData theme in stages?.stageThemes ?? new List<StageThemeData>())
                {
                    if (theme != null) _themeNames.TryAdd(theme.enemyThemeId, theme.name);
                }
            }

            if (enemyThemeId == 0) return "공용";
            return _themeNames.TryGetValue(enemyThemeId, out string name) ? name : $"테마 {enemyThemeId}";
        }

        private static int TierOrder(string tier) => tier switch
        {
            "boss" => 0,
            "elite" => 1,
            _ => 2,
        };

        private static string TierName(string tier) => tier switch
        {
            "boss" => "보스",
            "elite" => "정예",
            _ => "일반",
        };

        private static Color TierColor(string tier) => tier switch
        {
            "boss" => UITheme.Danger,
            "elite" => UITheme.Accent,
            _ => UITheme.TextPrimary,
        };

        private List<Entry> EnemyEntries()
        {
            return Enemies()
                .Where(enemy => enemy != null)
                .OrderBy(enemy => enemy.themeId)
                .ThenBy(enemy => TierOrder(enemy.tier))
                .ThenBy(enemy => enemy.id)
                .Select(enemy => new Entry
                {
                    Group = ThemeName(enemy.themeId),
                    Title = enemy.name,
                    Sub = TierName(enemy.tier),
                    Tint = TierColor(enemy.tier),
                    Portrait = enemy.portrait,
                    Body = () => EnemyBody(enemy),
                    Search = $"{enemy.name} {ThemeName(enemy.themeId)} {TierName(enemy.tier)} {ElementName(enemy.element)}",
                })
                .ToList();
        }

        private static string EnemyBody(EnemyData enemy)
        {
            var sb = new StringBuilder();
            Headline(sb, enemy.name, $"{ThemeName(enemy.themeId)} · {TierName(enemy.tier)}", TierColor(enemy.tier));

            Section(sb, "기본 정보");
            Field(sb, "원소", ElementName(enemy.element));
            List<string> subs = enemy.subStats is { Count: > 0 } ? enemy.subStats
                : string.IsNullOrWhiteSpace(enemy.subStat) ? new List<string>() : new List<string> { enemy.subStat };
            Field(sb, "주스탯", enemy.mainStat, UITheme.Accent);
            if (subs.Count > 0) Field(sb, "부스탯", string.Join(" · ", subs));
            if (enemy.tags is { Count: > 0 }) Field(sb, "분류", string.Join(" · ", enemy.tags));

            Section(sb, "기초 스탯");
            Paragraph(sb, "적의 레벨은 스테이지와 같다. 아래는 Lv.1 값과 레벨당 성장이다.", UITheme.TextMuted);
            AppendStats(sb, enemy.mainStat, subs,
                (enemy.strBase, enemy.strIncrementLvl), (enemy.dexBase, enemy.dexIncrementLvl),
                (enemy.conBase, enemy.conIncrementLvl), (enemy.intBase, enemy.intIncrementLvl),
                (enemy.lukBase, enemy.lukIncrementLvl));

            Section(sb, "코드");
            if (enemy.codes != null)
            {
                if (enemy.codes.TryGetValue("passive", out int passive)) CodeBlock(sb, CodeCatalog.Slot.Passive, passive, "패시브");
                if (enemy.codes.TryGetValue("normal", out int normal)) CodeBlock(sb, CodeCatalog.Slot.Normal, normal, "일반행동");
                if (enemy.codes.TryGetValue("ultimate", out int ultimate)) CodeBlock(sb, CodeCatalog.Slot.Ultimate, ultimate, "궁극기");
            }

            foreach (LevelPassiveData passive in (enemy.levelPassives ?? new List<LevelPassiveData>())
                         .Where(p => p != null).OrderBy(p => p.unlockLevel))
            {
                CodeBlock(sb, CodeCatalog.Slot.Passive, passive.codeId,
                    passive.unlockLevel > 1 ? $"스테이지 {passive.unlockLevel}부터" : "패시브");
            }

            List<ItemData> items = GameManager.Instance?.itemDataList?.items ?? new List<ItemData>();
            List<string> drops = (enemy.drops ?? new List<int>())
                .Select(id => items.FirstOrDefault(item => item?.id == id)?.name)
                .Where(name => name != null)
                .ToList();
            if (drops.Count > 0)
            {
                Section(sb, "전리품");
                Paragraph(sb, string.Join(" · ", drops));
            }

            return sb.ToString();
        }

        // ── 용어 ─────────────────────────────────────────────────────

        private List<Entry> GlossaryEntries()
        {
            var entries = new List<Entry>();
            foreach ((string title, string body) in Rules)
            {
                entries.Add(new Entry
                {
                    Group = "기본 규칙",
                    Title = title,
                    Body = () =>
                    {
                        var sb = new StringBuilder();
                        Headline(sb, title, null, UITheme.TextPrimary);
                        Paragraph(sb, body, UITheme.TextPrimary);
                        return sb.ToString();
                    },
                    Search = $"{title} {body}",
                });
            }

            foreach (SynergyRoleData role in SynergyCatalog.Roles.Where(role => role != null))
            {
                entries.Add(new Entry
                {
                    Group = "역할군",
                    Title = role.name ?? role.id,
                    Body = () => RoleBody(role),
                    Search = $"{role.name} {role.description}",
                });
            }

            foreach (SynergyArchetypeData archetype in SynergyCatalog.Archetypes.Where(a => a != null))
            {
                entries.Add(new Entry
                {
                    Group = "조합 유형",
                    Title = archetype.name ?? archetype.id,
                    Body = () => ArchetypeBody(archetype),
                    Search = $"{archetype.name} {archetype.description}",
                });
            }

            return entries;
        }

        private static string RoleBody(SynergyRoleData role)
        {
            var sb = new StringBuilder();
            Headline(sb, role.name ?? role.id, "역할군", UITheme.TextPrimary);
            Paragraph(sb, role.description, UITheme.TextPrimary);

            List<string> members = Units()
                .Where(unit => unit != null && SynergyCatalog.UnitOf(unit.id)?.roles?.Contains(role.id) == true)
                .Select(unit => unit.name)
                .ToList();
            if (members.Count > 0)
            {
                Section(sb, $"이 역할의 캐릭터 {members.Count}명");
                Paragraph(sb, string.Join(" · ", members));
            }

            return sb.ToString();
        }

        private static string ArchetypeBody(SynergyArchetypeData archetype)
        {
            var sb = new StringBuilder();
            Headline(sb, archetype.name ?? archetype.id, "조합 유형", UITheme.TextPrimary);
            Paragraph(sb, archetype.description, UITheme.TextPrimary);

            if (archetype.core is { Count: > 0 })
            {
                Section(sb, "핵심");
                Paragraph(sb, string.Join(" · ", archetype.core.Select(SynergyCatalog.NameOf)));
            }

            if (archetype.partners is { Count: > 0 })
            {
                Section(sb, "함께 쓰는 캐릭터");
                Paragraph(sb, string.Join(" · ", archetype.partners.Select(SynergyCatalog.NameOf)));
            }

            if (!string.IsNullOrWhiteSpace(archetype.counterplay))
            {
                Section(sb, "약점");
                Paragraph(sb, archetype.counterplay, UITheme.Danger);
            }

            return sb.ToString();
        }

        /// <summary>
        /// 데이터 파일에 없는 유일한 문장. 수치의 원본은 Detail_02 · Detail_03이며
        /// 공식이 바뀌면 여기도 함께 고친다.
        /// </summary>
        private static readonly (string Title, string Body)[] Rules =
        {
            ("자동 전투",
                "전투는 턴제로 저절로 진행된다. 플레이어의 결정은 전투 전에 끝난다 — 편성, 배치, 장비, 육성.\n" +
                "행동 순서는 DEX가 정하는 행동 속도로 정해지고, 행동서열 막대에 다음 차례가 보인다."),
            ("다섯 스탯",
                "공격력·방어력 스탯은 없다. 모든 전투 수치는 다섯 스탯에서 나온다.\n" +
                "STR — 방어력, 장비 중량 한도\nDEX — 행동 속도\nCON — 최대 체력 (CON × 100)\n" +
                "INT — 마나 획득 효율\nLUK — 치명타 확률\n\n" +
                "스탯 = 기본값 + 레벨당 성장 × (레벨 − 1) + 강화량. 주스탯은 ×1.2, 부스탯은 ×1.1을 받는다."),
            ("피해 공식",
                "피해 = 스킬 위력 × 주스탯 × 0.2\n" +
                "위력은 스킬마다 정해져 있다. 같은 스킬이라도 주스탯이 높을수록 세게 친다."),
            ("방어력과 내구",
                "방어력 = STR. 받는 피해 배율 = 기준값 / (기준값 + 방어력), 기준값 = 100 + 10 × (레벨 − 1).\n" +
                "기준값이 레벨을 따라 커지므로 감소율은 레벨과 무관하게 STR 비중으로 정해진다.\n\n" +
                "내구는 받는 피해에서 고정으로 깎는 양이다. 방어 무시·관통의 영향을 받지 않는다. 주로 방어구가 준다."),
            ("행동의 종류",
                "일반행동 — 차례가 오면 하는 기본 행동.\n" +
                "대체행동 — 조건이 맞으면 일반행동 자리를 대신한다. 일반행동 한 번으로 센다.\n" +
                "추가행동 — 패시브가 끼워 넣는 행동. 차례를 쓰지 않는다.\n" +
                "특수행동 — 인드라의 궁극기 '신들의 왕'만 연다. 로카팔라가 저마다의 특수행동을 한다.\n" +
                "궁극기 — 자원(마나·중첩)이 차는 즉시 발동한다. 차례를 쓰지 않고 재사용 대기시간도 없다."),
            ("코드 등급",
                "일반(은색) — 기본 코드.\n" +
                "강화(금색) — 같은 계열의 은색 코드를 대체한다. 둘 다 배우면 은색은 발동하지 않는다.\n" +
                "고유(보라) — 캐릭터마다 하나뿐인 패시브. 은·금 사다리 밖이며 서포트 카드로 전수되지 않는다."),
            ("배치",
                "아군과 적은 각각 전열·후열 두 줄, 줄마다 네 칸이다. 대기석은 필드 밖이다.\n" +
                "전열에 선 유닛은 진영과 관계없이 우선도 +1을 얻는다. 튼튼한 캐릭터를 앞에, 사거리가 긴 캐릭터를 뒤에 둔다."),
            ("피해 태그",
                "설명 끝의 #태그는 그 공격의 성질이다. 대상 범위(단일·광역), 공격 종류(물리·특수), " +
                "접촉 여부(접촉·비접촉), 무기 계열(베기·찌르기·화살 등)이 있다.\n" +
                "'접촉 공격을 받으면' 같은 조건은 이 태그를 본다. 지속피해에는 태그가 없다."),
        };
    }
}
