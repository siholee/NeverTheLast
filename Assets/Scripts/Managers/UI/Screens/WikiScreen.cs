using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseClasses;
using Codes.Base;
using Core;
using Effects.Negative;
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
    /// <b>게임 전체의 도감</b>이다. 캐릭터 · 코드 · 아이템 · 적 · 용어 다섯 갈래를 왼쪽 목록과
    /// 오른쪽 본문으로 편다.
    ///
    /// 원본은 전부 데이터 파일이다(10_units · 20_codes · 30_synergies · 40_items · 60_enemies ·
    /// 80_stages). 따로 적어 둔 문장은 '용어'의 기본 규칙뿐이고, 나머지는 파일이 바뀌면
    /// 그대로 따라 바뀐다. 표에서 빠진 항목은 조용히 비우지 않고 무엇이 없는지 적는다.
    ///
    /// ESC 메뉴 위에 뜨므로 시간은 메뉴가 이미 멈춰 두었다. 여기서 따로 멈추지 않는다.
    /// </summary>
    public partial class WikiScreen : ModalScreen
    {
        private enum Category
        {
            Guide,
            Characters,
            Codes,
            Items,
            Enemies,
            Glossary,
        }

        private static readonly (Category Category, string Label)[] Categories =
        {
            (Category.Guide, "가이드"),
            (Category.Characters, "캐릭터"),
            (Category.Codes, "코드"),
            (Category.Items, "보급품"),
            (Category.Enemies, "적 도감"),
            (Category.Glossary, "용어 사전"),
        };

        /// <summary>유닛 ID는 진영마다 20칸 블록이다(Detail_03 §7.4).</summary>
        private static readonly string[] FactionNames =
        {
            "아카샤", "그리스", "다카마가하라", "베다", "노르드", "로마", "이집트", "메히코", "갈리아",
        };


        private const float TabRowHeight = 52f;
        private const float ListWidth = 0.32f;
        private const float RowHeight = 76f;
        private const float PortraitSize = 188f;

        /// <summary>목록 한 줄. 본문은 누를 때 만든다 — 수백 항목의 문장을 미리 짜 둘 이유가 없다.</summary>
        private sealed class Entry
        {
            public string Key;
            public string Group;
            public string Title;
            public string Sub;
            public Color Tint = UITheme.TextPrimary;
            public string Portrait;
            public Func<string> Body;
            public string Search;
        }

        protected override Color BackdropTint => new Color(.77f,.84f,.92f,.98f);
        protected override string CanvasName => "WikiCanvas";

        /// <summary>ESC 메뉴보다 위, 설정·확인 창보다 아래.</summary>
        protected override int SortingOrder => UITheme.LayerWiki;

        protected override string Title => "자료실";
        protected override string Caption => "ARCHIVE / GAME GUIDE";
        protected override Vector2 AnchorMin => new(0.05f, 0.05f);
        protected override Vector2 AnchorMax => new(0.95f, 0.95f);
        protected override bool CloseOnBackdrop => true;

        private readonly Dictionary<Category, Button> _tabs = new();
        private readonly Dictionary<Category, List<Entry>> _cache = new();

        /// <summary>지금 목록에 그려진 줄. 선택 강조를 다시 칠할 때 쓴다.</summary>
        private readonly Dictionary<Button, Entry> _rows = new();

        private Category _category = Category.Guide;
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
            BuildGuideNavigation();
        }

        private void BuildTabs()
        {
            UIIcon[] icons={UIIcon.Guide,UIIcon.Party,UIIcon.Support,UIIcon.Supply,UIIcon.Counter,UIIcon.Lexicon};
            for(int i=0;i<Categories.Length;i++)
            {
                (Category category,string label)=Categories[i];
                Button tab=UIBuild.Button($"Tab_{category}",Body,"",()=>{RememberLocation();SetCategory(category);},false,18);
                float top=.80f-i*.10f;
                UIBuild.Anchor(tab.image.rectTransform,new Vector2(0,top-.075f),new Vector2(.078f,top),2,4);
                Image icon=UIBuild.Icon("CategoryIcon",tab.transform,icons[i],UITheme.AccentDim);
                UIBuild.Anchor(icon.rectTransform,new Vector2(.02f,.26f),new Vector2(.22f,.73f));
                var title=UIBuild.Text("Label",tab.transform,label,20,UITheme.TextSecondary,TextAlignmentOptions.MidlineLeft);
                UIBuild.Anchor(title.rectTransform,new Vector2(.28f,0),new Vector2(1,1));
                _tabs[category]=tab;
            }
        }

        /// <summary>이름 검색. 코드만 수백 개라 목록을 눈으로 훑어서는 찾을 수 없다.</summary>
        private void BuildSearch()
        {
            // 검색은 다른 입력 · 버튼과 같은 알약 — 흰 면 + 군청 헤어라인.
            Image background = UIBuild.Panel("Search", Body, UITheme.SurfaceSunken,
                UIShapes.Corner.Diagonal, 6, UITheme.Outline, 1);
            background.sprite = UIShapes.RoundedRect(22, Color.white,
                new Color(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, 0.24f), 1);
            UIBuild.Pin(background.rectTransform, new Vector2(1f, 1f), new Vector2(420f, TabRowHeight - 4f),
                Vector2.zero);

            RectTransform viewport = UIBuild.Container("Viewport", background.transform);
            UIBuild.Stretch(viewport, 20f, 4f);
            viewport.gameObject.AddComponent<RectMask2D>();

            TextMeshProUGUI placeholder = UIBuild.Text("Placeholder", viewport, "전체 자료실 검색",
                UITheme.FontReadSmall, UITheme.TextMuted);
            UIBuild.Stretch(placeholder.rectTransform);

            TextMeshProUGUI text = UIBuild.Text("Text", viewport, "", UITheme.FontReadSmall, UITheme.TextPrimary);
            UIBuild.Stretch(text.rectTransform);
            text.richText = false;

            _search = background.gameObject.AddComponent<TMP_InputField>();
            _search.textViewport = viewport;
            _search.textComponent = text;
            _search.placeholder = placeholder;
            _search.targetGraphic = background;
            _search.lineType = TMP_InputField.LineType.SingleLine;
            _search.textComponent.fontSize = UITheme.FontReadSmall;
            placeholder.fontSize = UITheme.FontReadSmall;
            _search.onValueChanged.AddListener(value =>
            {
                if (_filter.Length == 0 && !string.IsNullOrWhiteSpace(value)) RememberLocation();
                _filter = value?.Trim() ?? "";
                RebuildList(keepSelection: true);
            });
        }

        private void BuildList()
        {
            Image frame = UIBuild.Solid("ListFrame", Body, Color.clear);
            UIBuild.Anchor(frame.rectTransform, new Vector2(.095f, 0f), new Vector2(ListWidth, 1f));
            frame.rectTransform.offsetMax = new Vector2(0f, -(TabRowHeight + 8f));

            _listContent = UIBuild.ScrollArea("List", frame.transform, out _listScroll);
            UIBuild.Stretch((RectTransform)_listScroll.transform, 6f, 8f);
        }

        private void BuildBody()
        {
            Image frame = UIBuild.Solid("BodyFrame", Body, Color.clear);
            UIBuild.Anchor(frame.rectTransform, new Vector2(ListWidth+.015f, 0f), new Vector2(1f, 1f));
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

            _bodyText = UIBuild.Text("Text", _bodyContent, "", UITheme.FontReadBody, UITheme.TextPrimary,
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
            Image bar = UIBuild.Solid("Sticky", frame, Color.clear);
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
            if (!IsVisible)
            {
                _history.Clear();
                _resumeScale = Time.timeScale;
                _ownsPause = true;
                Time.timeScale = 0;
                _cache.Clear();
                _selected = null;
            }
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

        /// <summary>
        /// 분류 레일은 세로 목록이다. 선택은 옅은 군청 면과 굵은 이름으로 표시하며 별도 막대는 두지 않는다.
        /// </summary>
        private static void SelectWikiRow(Image row, bool active)
        {
            row.sprite=UIShapes.RoundedRect(8,active?UIArtDirection.SelectedFill:Color.clear);
            row.type=Image.Type.Sliced; row.color=Color.white;
        }

        private static void Tint(Button button, bool active)
        {
            if (button == null) return;
            SelectWikiRow(button.image, active);
            TextMeshProUGUI label = button.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (label != null)
            {
                label.color = active ? UITheme.AccentDim : UITheme.TextSecondary;
                label.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
            }
        }

        // ── 목록 ─────────────────────────────────────────────────────

        private List<Entry> EntriesOf(Category category)
        {
            if (_cache.TryGetValue(category, out List<Entry> cached)) return cached;

            List<Entry> built = category switch
            {
                Category.Guide => GuideEntries(),
                Category.Characters => CharacterEntries(),
                Category.Codes => CodeEntries(),
                Category.Items => ItemEntries(),
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

            Canvas.ForceUpdateCanvases();
            float available=_listScroll.viewport.rect.height;
            float shift=Mathf.Max(0,(available-y-8f)*.5f);
            foreach (RectTransform child in _listContent) child.anchoredPosition-=new Vector2(0,shift);
            _listContent.sizeDelta = new Vector2(0f,Mathf.Max(available,y+8f));
            _listContent.anchoredPosition = Vector2.zero;

            if (!keepSelection || _selected == null || !entries.Contains(_selected))
            {
                _selected = entries.FirstOrDefault();
            }

            ShowEntry(_selected);
            HighlightSelection();
        }

        /// <summary>지금 분류에서 검색어에 맞는 항목. 목록과 고정 머리의 "몇 번째"가 같은 목록을 센다.</summary>
        private List<Entry> VisibleEntries() => (_filter.Length > 0
                ? Categories.SelectMany(pair => EntriesOf(pair.Category)) : EntriesOf(_category))
            .Where(entry => _filter.Length == 0 ||
                            entry.Search.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();

        private void AddGroupHeading(string text, ref float y)
        {
            if (string.IsNullOrEmpty(text)) return;

            y += y > 0f ? 8f : 2f;
            TextMeshProUGUI label = UIBuild.Label("Group", _listContent, text, UITheme.FontReadSmall, UITheme.TextMuted);
            Place(label.rectTransform, y, 28f, 8f);
            y += 32f;
        }

        /// <summary>
        /// 목록 한 줄. 클릭은 Button으로 받는다 — EventTrigger는 휠까지 먹어 목록이 굴러가지 않는다.
        /// </summary>
        private void AddRow(Entry entry, ref float y)
        {
            Button row = UIBuild.Button("Row", _listContent, null, () => Select(entry), false);
            bool hasSub=!string.IsNullOrEmpty(entry.Sub)&&entry.Sub!=entry.Group;
            float height=hasSub?RowHeight:44f;
            Place(row.image.rectTransform, y, height, 2f);
            row.image.sprite = UIShapes.Surface(6, new Color(1f, 1f, 1f, 0f), UIShapes.Corner.Diagonal);

            TextMeshProUGUI title = UIBuild.Text("Title", row.transform, entry.Title, UITheme.FontReadSmall, entry.Tint);
            title.overflowMode = TextOverflowModes.Ellipsis;
            // 제목과 부제를 하나의 덩어리로 배치한다. 중복 부제가 없으면 한 줄을 중앙에 둔다.
            UIBuild.Anchor(title.rectTransform, new Vector2(0f, hasSub ? .44f : 0f), new Vector2(1f,hasSub ? .96f : 1f), 14f, 0f);
            if (hasSub)
            {
                var sub = UIBuild.Text("Sub", row.transform, entry.Sub, 18f, UITheme.TextMuted);
                sub.overflowMode = TextOverflowModes.Ellipsis;
                UIBuild.Anchor(sub.rectTransform, new Vector2(0,.04f), new Vector2(1f,.42f),14f,0f);
            }

            row.gameObject.name = $"Row_{entry.Title}";
            _rows[row] = entry;
            y += height + 4f;
        }

        private void Select(Entry entry)
        {
            if (_selected != entry) RememberLocation();
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
                // 선택 면을 유지하고 행 전체가 목록 안에 보이도록 한다.
                SelectWikiRow(row.image, active);
                if(active)
                {
                    Canvas.ForceUpdateCanvases();
                    float top=-row.image.rectTransform.anchoredPosition.y;
                    float bottom=top+row.image.rectTransform.rect.height;
                    float current=_listContent.anchoredPosition.y;
                    float available=_listScroll.viewport.rect.height;
                    if(top<current+8)current=top-8;
                    else if(bottom>current+available-8)current=bottom-available+8;
                    _listContent.anchoredPosition=new Vector2(0,Mathf.Clamp(current,0,Mathf.Max(0,_listContent.rect.height-available)));
                }
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
            GuideTermTooltip.CloseAll();
            if (_backButton != null) _backButton.interactable = _history.Count > 0;
            if (_homeRoot != null) _homeRoot.gameObject.SetActive(false);
            _bodyText.gameObject.SetActive(true);
            if (entry?.Key == "guide:home") { ShowGuideHome(entry); return; }
            string text = entry?.Body?.Invoke() ?? "";
            Sprite portrait = entry == null ? null : SpriteResource.LoadPortrait(entry.Portrait);

            _portrait.sprite = portrait;
            _portrait.gameObject.SetActive(portrait != null);

            // 처음 여는 프레임에는 캔버스 크기가 아직 잡히지 않아 폭이 틀리게 읽힌다.
            // 폭이 틀리면 줄 수를 적게 재서 본문 끝이 잘린다.
            Canvas.ForceUpdateCanvases();

            // 초상화가 있으면 오른쪽 위를 비워 둔다. 머리글이 그림 밑으로 파고들지 않게.
            float width = ((RectTransform)_bodyScroll.transform).rect.width;
            text = GameGuideCatalog.LinkTerms(text);
            _bodyText.text = text;
            _bodyText.rectTransform.sizeDelta = new Vector2(0f, 0f);
            _bodyText.margin = new Vector4(0f, 0f, portrait != null ? PortraitSize + 16f : 0f, 0f);

            float height = _bodyText.GetPreferredValues(text, Mathf.Max(200f, width - _bodyText.margin.z), 0f).y;
            height = Mathf.Max(height, portrait != null ? PortraitSize : 0f);
            _bodyText.rectTransform.sizeDelta = new Vector2(0f, height);
            float available=_bodyScroll.viewport.rect.height;
            float inset=Mathf.Max(12f,(available-height)*.5f);
            _bodyText.rectTransform.anchoredPosition=new Vector2(0,-inset);
            _portrait.rectTransform.anchoredPosition=new Vector2(_portrait.rectTransform.anchoredPosition.x,-inset);
            _bodyContent.sizeDelta = new Vector2(0f,Mathf.Max(available,height+24f));
            _bodyContent.anchoredPosition = Vector2.zero;
            RefreshStickyHeader(entry);
        }

        // ── 본문 조판 ────────────────────────────────────────────────
        // TMP 서식 문자열을 직접 짠다. Plain · Paint · 코드 덩어리는 캐릭터 선택과 같이 쓰는 CodeText에 있다.

        private static void CodeBlock(StringBuilder sb, CodeCatalog.Slot slot, int codeId, string kind) =>
            CodeText.AppendBlock(sb, slot, codeId, kind, UITheme.FontReadSmall, UITheme.FontReadSmall);

        private static void Headline(StringBuilder sb, string title, string sub, Color tint)
        {
            sb.Append($"<size={UITheme.FontDisplay}><b>{Paint(Plain(title), tint)}</b></size>\n");
            if (!string.IsNullOrWhiteSpace(sub))
                sb.Append($"<size={UITheme.FontReadBody}>{Paint(Plain(sub.Trim()), UITheme.TextSecondary)}</size>\n");
            sb.Append('\n');
        }

        private static void Section(StringBuilder sb, string title)
        {
            sb.Append($"\n<size={UITheme.FontReadHeading}><b>{Paint(Plain(title), UITheme.Accent)}</b></size>\n");
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
                    string archetypes = string.Join(" ", UnitTagCatalog.Resolve(unit.archetypeTags)
                        .Select(tag => tag.Name));
                    return new Entry
                    {
                        Key = $"unit:{unit.id}",
                        Group = FactionOf(unit.id),
                        Title = unit.name,
                        Sub = available ? string.Join(" · ",new[]{ElementName(unit.element)}.Concat(UnitTagCatalog.Resolve(unit.archetypeTags).Take(2).Select(tag=>tag.Name))) : "미해금",
                        Tint = available ? UITheme.TextPrimary : UITheme.TextMuted,
                        Portrait = unit.portrait,
                        Body = () => CharacterBody(unit),
                        Search = $"{unit.name} {unit.tagline} {FactionOf(unit.id)} {ElementName(unit.element)} {archetypes}",
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
            "Unlocked" => "기본 해금",
            "Locked" => "합류 해금",
            _ => characterType,
        };

        private static string CharacterBody(UnitData unit)
        {
            var sb = new StringBuilder();
            Headline(sb, unit.name, unit.tagline, UITheme.TextPrimary);

            Section(sb, "기본 정보");
            Field(sb, "진영", FactionOf(unit.id));
            Field(sb, "해금", SynergyCatalog.IsAvailable(unit.id) ? "편성 가능" : "아직 손에 넣지 못했다",
                SynergyCatalog.IsAvailable(unit.id) ? UITheme.Positive : UITheme.TextMuted);
            Field(sb, "원소", ElementName(unit.element));
            IReadOnlyList<UnitTagCatalog.Definition> archetypes = UnitTagCatalog.Resolve(unit.archetypeTags);
            if (archetypes.Count > 0)
                Field(sb, "전투 성향", string.Join(" · ", archetypes.Select(tag => tag.Name)), UITheme.Accent);

            Field(sb, "초월기 자원", unit.ultimateResourceType == "Passive" ? "상시 효과" :
                $"{unit.ultimateResourceName} {(unit.manaMax > 0 ? unit.manaMax : Entities.Unit.DefaultManaMax)}");

            Section(sb, "캐릭터 스킬");
            AppendUnitCodes(sb, unit.codes);
            AppendSynergy(sb, unit);
            Section(sb, "성장 스탯");
            AppendStats(sb, unit);
            return sb.ToString();
        }

        /// <summary>Lv.1 값과 레벨당 성장(롤식 선형). 치명타 두 스탯은 퍼센트로 적는다.</summary>
        private static void AppendStats(StringBuilder sb, CombatantData data)
        {
            for (int i = 0; i < Entities.UnitStats.StatCount; i++)
            {
                var stat = (BaseClasses.BaseEnums.StatType)i;
                float baseValue = data.BaseOf(stat);
                float growth = data.GrowthOf(stat);
                Color tint = UITheme.Stat(stat);
                sb.Append($"<mspace=0.62em>{Paint(Entities.UnitStats.ShortName(stat), tint)}</mspace>  ");
                sb.Append(Paint(Entities.UnitStats.Format(stat, baseValue), UITheme.TextPrimary));
                if (!Mathf.Approximately(growth, 0f))
                    sb.Append($"<size={UITheme.FontReadSmall}>{Paint($"   레벨당 +{Entities.UnitStats.Format(stat, growth)}", UITheme.TextMuted)}</size>");
                sb.Append('\n');
            }
        }

        private static void AppendUnitCodes(StringBuilder sb, Dictionary<string, int> codes)
        {
            if (codes == null) return;
            if (codes.TryGetValue("passive", out int passive)) CodeBlock(sb, CodeCatalog.Slot.Passive, passive, "특성");
            if (codes.TryGetValue("normal", out int normal)) CodeBlock(sb, CodeCatalog.Slot.Normal, normal, "일반행동");
            if (codes.TryGetValue("ultimate", out int ultimate)) CodeBlock(sb, CodeCatalog.Slot.Ultimate, ultimate, "초월기");
            if (codes.TryGetValue("special", out int special)) CodeBlock(sb, CodeCatalog.Slot.Special, special, "특수행동");
        }
        /// <summary>
        /// 이 캐릭터를 어떻게 쓰는가. `30_synergies.yaml`이 원본이다.
        /// 이 캐릭터가 대표(편성 첫 자리)일 때의 고점·대체 두 벌을, 추천이 없으면 이 캐릭터를 부르는 대표를 적는다.
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
            // 훈련이 사라져 육성 기여는 더 이상 찍지 않는다.
            if (profile.supportValue != null)
                Field(sb, "전투 기여", Pips(profile.supportValue.combat), UITheme.Accent);

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
            sb.Append('\n').Append(Paint($"이 캐릭터를 부르는 대표 {callers.Count}명", UITheme.TextMuted)).Append('\n');
            if (callers.Count == 0)
            {
                Paragraph(sb, "아직 어떤 대표의 추천 조합에도 들어가지 않는다.", UITheme.TextMuted);
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

            var names = new List<string> { Paint(Plain($"{main.name}(대표·{RowLabel(lineup, main.id)})"), UITheme.Accent) };
            foreach (int memberId in lineup.members)
            {
                bool owned = SynergyCatalog.IsAvailable(memberId);
                names.Add(Paint(Plain($"{SynergyCatalog.NameOf(memberId)}({RowLabel(lineup, memberId)})"),
                    owned ? UITheme.TextPrimary : UITheme.TextMuted));
            }

            sb.Append(string.Join(Paint("  ·  ", UITheme.TextMuted), names)).Append('\n');
            if (!string.IsNullOrWhiteSpace(lineup.reason))
                sb.Append($"<size={UITheme.FontReadSmall}>{Paint(Plain(lineup.reason.Trim()), UITheme.TextMuted)}</size>\n");
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
            var result = RewardCodeCatalog.All().OrderBy(e => e.tier).ThenBy(e => e.id)
                .Select(e => new Entry {
                    Key = $"code:Passive:{e.id}", Group = $"보상 코드 · T{e.tier}", Title = e.verbalName,
                    Sub = "보상 · 상점", Body = () => CodeBody(e, null),
                    Search = $"{e.verbalName} {CodeCatalog.Summary(e.description)} 보상 T{e.tier}"
                }).ToList();
            foreach (UnitData unit in Units())
            {
                if (unit?.codes == null) continue;
                foreach (var pair in unit.codes)
                {
                    var slot = pair.Key switch { "normal" => CodeCatalog.Slot.Normal, "ultimate" => CodeCatalog.Slot.Ultimate,
                        "special" => CodeCatalog.Slot.Special, _ => CodeCatalog.Slot.Passive };
                    var e = CodeCatalog.Find(slot, pair.Value); if (e == null) continue;
                    string key = $"code:{slot}:{e.id}";
                    if (result.Any(r => r.Key == key)) continue;
                    result.Add(new Entry { Key = key, Group = "캐릭터 스킬", Title = e.verbalName, Sub = unit.name + " · " + SlotName(slot),
                        Body = () => SkillBody(e, slot, unit), Search = $"{unit.name} {e.verbalName} {CodeCatalog.Summary(e.description)}" });
                }
            }
            return result;
        }

        private static (int Order, string Group) CodeFamily(int codeId) => codeId switch
        {
            >= 1 and <= 179 => (0, "공용 해금 패시브"),
            >= 180 and <= 199 => (1, "공용 · 특수"),
            >= 200 and <= 399 => (2, "고유 패시브"),
            >= 400 and <= 499 => (3, "폐기된 코드 영역"),
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
            Headline(sb, entry.verbalName ?? "보상 코드", owner == null ? "보상 코드" : "캐릭터 특성",
                owner != null ? UITheme.CodeUnique : UITheme.TextPrimary);

            Section(sb, "효과");
            string summary = CodeCatalog.Summary(entry.description);
            Paragraph(sb, summary.Length > 0 ? summary : "설명이 비어 있다.", UITheme.TextPrimary);

            List<string> markers = CodeCatalog.Markers(entry.description);
            if (markers.Count > 0) Field(sb, "꼬리표", string.Join(" · ", markers));

            Section(sb, "보유");
            if (owner != null) Field(sb, "고유 주인", owner, UITheme.CodeUnique);

            if (owner == null)
                Paragraph(sb, "보상 선택과 상점에서 획득해 활성 칸에 장착하는 코드입니다.", UITheme.TextMuted);

            if (RewardCodeCatalog.IsRewardCode(entry.id))
            {
                Field(sb, "등급", $"T{RewardCodeCatalog.TierOf(entry.id)}");
                int upper = RewardCodeCatalog.UpperOf(entry.id);
                if (upper > 0) Paragraph(sb, "상위 코드  ›  " + GameGuideCatalog.ArticleLink($"code:Passive:{upper}", RewardCodeCatalog.Find(upper)?.verbalName));
                Paragraph(sb, "활성 7칸 · 추가 개인 보관 · 준비 중 무료 드래그 교환");
            }
            return sb.ToString();
        }

        // ── 아이템 ───────────────────────────────────────────────────

        private List<Entry> ItemEntries()
        {
            return (GameManager.Instance?.itemDataList?.items ?? new List<ItemData>())
                .Where(item => item != null && (item.IsValuable || item.category == "Food" || item.category == "Tonic" || item.category == "CodeTool"))
                .OrderBy(item => item.IsValuable)
                .ThenBy(item => item.category)
                .ThenBy(item => item.rarity)
                .ThenBy(item => item.id)
                .Select(item => new Entry
                {
                    Key = $"item:{item.id}",
                    Group = ItemTooltip.CategoryName(item.category),
                    Title = item.name,
                    Sub = item.IsValuable ? "귀중품" : $"T{Mathf.Max(1, item.rarity)} · {CategoryName(item)}",
                    Tint = UITheme.Rarity(Mathf.Max(1, item.rarity)),
                    Body = () => ItemBody(item),
                    Search = $"{item.name} {item.category} {CategoryName(item)}",
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
                Paragraph(sb, "상점에서 골드로 판매하는 물건입니다. 값은 주운 스테이지에서 정해집니다.");
                Field(sb, "기본 판매가", $"{item.sellPrice}");
                return sb.ToString();
            }

            Section(sb, "제원");
            foreach (UITooltip.Line line in ItemTooltip.Lines(item).Skip(1))
            {
                if (line.Label == null) Paragraph(sb, line.Value, line.Color);
                else Field(sb, line.Label, line.Value, line.Color);
            }

            if (item.shopPrice > 0) Field(sb, "상점 기본가", $"{item.shopPrice}");
            if (item.eventOnly) Field(sb, "획득", "사건에서만 얻는다");

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
                    Key = $"enemy:{enemy.id}",
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
            if (enemy.tags is { Count: > 0 }) Field(sb, "분류", string.Join(" · ", enemy.tags));

            Section(sb, "기초 스탯");
            Paragraph(sb, "적의 레벨은 스테이지와 같다. 아래는 Lv.1 값과 레벨당 성장이다.", UITheme.TextMuted);
            AppendStats(sb, enemy);

            Section(sb, "코드");
            if (enemy.codes != null)
            {
                if (enemy.codes.TryGetValue("passive", out int passive)) CodeBlock(sb, CodeCatalog.Slot.Passive, passive, "패시브");
                if (enemy.codes.TryGetValue("normal", out int normal)) CodeBlock(sb, CodeCatalog.Slot.Normal, normal, "일반행동");
                if (enemy.codes.TryGetValue("ultimate", out int ultimate)) CodeBlock(sb, CodeCatalog.Slot.Ultimate, ultimate, "궁극기");
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
            foreach (var article in GameGuideCatalog.Terms)
                entries.Add(ArticleEntry(article, "term:"));

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

        // ── 원소 반응 ─────────────────────────────────────────────

        /// <summary>표에 적힌 순서대로. 이름이 겹치는 확산은 한 번만 센다.</summary>
        private static IEnumerable<string> ReactionNames()
            => ElementalReaction.AllReactions().Select(reaction => reaction.Name).Distinct();

        private static string ElementName(BaseEnums.UnitElement element) => element switch
        {
            BaseEnums.UnitElement.Pyro => "불",
            BaseEnums.UnitElement.Hydro => "물",
            BaseEnums.UnitElement.Dendro => "풀",
            BaseEnums.UnitElement.Electro => "전기",
            BaseEnums.UnitElement.Cryo => "얼음",
            BaseEnums.UnitElement.Geo => "바위",
            BaseEnums.UnitElement.Anemo => "바람",
            _ => element.ToString(),
        };

        private static string KindName(ReactionKind kind) => kind switch
        {
            ReactionKind.Amplify => "증폭 — 방금 그 공격의 피해를 키운다",
            ReactionKind.Spread => "확산 — 원소를 주변으로 퍼뜨린다",
            ReactionKind.Control => "제어 — 상대의 행동을 막거나 늦춘다",
            ReactionKind.Burst => "즉발 — 그 자리에서 피해를 준다",
            ReactionKind.Dot => "지속피해 — 턴마다 깎는다",
            ReactionKind.Buff => "강화 — 스탯을 올린다",
            ReactionKind.Debuff => "약화 — 대상의 스탯을 내린다",
            _ => "무효 — 붙은 원소를 지운다",
        };

        private static string ReactionOverviewBody()
        {
            var sb = new StringBuilder();
            Headline(sb, "원소 반응", "28쌍", UITheme.TextPrimary);
            Paragraph(sb,
                "캐릭터마다 타고난 원소가 있고, 공격은 대상에게 원소를 부착한다." + "\n" +
                "이미 다른 원소가 붙어 있는 대상에게 원소를 부착하면 두 원소가 반응한다.",
                UITheme.TextPrimary);

            Section(sb, "세기");
            Paragraph(sb,
                "반응의 세기는 반응을 일으킨 쪽의 특수공격력을 따른다. " +
                "레벨이 올라도 묻히지 않는다.");

            Section(sb, "겹칠 때");
            Paragraph(sb,
                "성립하는 쌍이 둘 이상이면 하나만 터진다. 증폭 · 확산 · 제어 · 즉발 · 지속피해 순으로 고른다. " +
                "증폭이 가장 앞인 것은 방금 들어간 그 공격에만 얹을 수 있어 놓치면 사라지기 때문이다.");

            Section(sb, "바람");
            Paragraph(sb, "바람이 낀 여섯 쌍은 모두 같은 확산이다. 바람 + 바람만 광역 피해가 된다.");

            Section(sb, $"전체 {ElementalReaction.AllReactions().Count()}쌍");
            foreach (ElementalReaction.Listing reaction in ElementalReaction.AllReactions())
            {
                Paragraph(sb, $"{ElementName(reaction.A)} + {ElementName(reaction.B)} = {reaction.Name}");
            }
            return sb.ToString();
        }

        private static string ReactionBody(string name)
        {
            var sb = new StringBuilder();
            List<ElementalReaction.Listing> pairs = ElementalReaction.AllReactions()
                .Where(reaction => reaction.Name == name)
                .ToList();

            Headline(sb, name, "원소 반응", UITheme.TextPrimary);
            if (pairs.Count == 0) return sb.ToString();

            Section(sb, "계열");
            Paragraph(sb, KindName(pairs[0].Kind), UITheme.TextPrimary);

            if (pairs[0].Kind == ReactionKind.Buff || pairs[0].Kind == ReactionKind.Debuff)
            {
                Section(sb, "건드리는 스탯");
                Paragraph(sb, pairs[0].Stat.ToString());
            }

            Section(sb, pairs.Count > 1 ? $"성립하는 조합 {pairs.Count}쌍" : "성립하는 조합");
            foreach (ElementalReaction.Listing pair in pairs)
            {
                Paragraph(sb, $"{ElementName(pair.A)} + {ElementName(pair.B)}");
            }
            return sb.ToString();
        }

    }
}
