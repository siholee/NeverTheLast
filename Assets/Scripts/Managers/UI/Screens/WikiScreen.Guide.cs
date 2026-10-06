using System.Collections.Generic;
using System.Linq;
using System.Text;
using Codes.Base;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Managers.UI.Core.CodeText;

namespace Managers.UI.Screens
{
    public partial class WikiScreen
    {
        private readonly Stack<(Category Category, Entry Entry, string Filter, float Scroll)> _history = new();
        private RectTransform _homeRoot;
        private Button _backButton;
        private float _resumeScale;
        private bool _ownsPause;
        internal string SelectedKey => _selected?.Key;
        internal string ArticleText => _bodyText?.text;
        internal int SearchResultCount => VisibleEntries().Count;

        private void BuildGuideNavigation()
        {
            // 목록만 굴리고 탐색 버튼은 항상 같은 자리에서 사용할 수 있다.
            _listScroll.GetComponent<RectTransform>().offsetMin += new Vector2(0, 52);
            var rail = UIBuild.Container("GuideNavigation", Body);
            UIBuild.Anchor(rail, Vector2.zero, new Vector2(ListWidth, 0));
            rail.sizeDelta = new Vector2(0, 44); rail.pivot = new Vector2(.5f, 0);
            _backButton = UIBuild.Button("GuideBack", rail, "‹ 돌아가기", GoBack, false);
            UIBuild.Anchor(_backButton.image.rectTransform, Vector2.zero, new Vector2(.38f, 1), 3, 0);
            var home = UIBuild.Button("GuideHome", rail, "첫 화면", () => ShowArticle("home"), false);
            UIBuild.Anchor(home.image.rectTransform, new Vector2(.38f, 0), new Vector2(.69f, 1), 3, 0);
            var close = UIBuild.Button("GuideClose", rail, "닫기", Hide, false);
            UIBuild.Anchor(close.image.rectTransform, new Vector2(.69f, 0), Vector2.one, 3, 0);
            foreach (var text in rail.GetComponentsInChildren<TextMeshProUGUI>()) text.fontSize = UITheme.FontReadSmall;
        }
        private void RememberLocation()
        {
            if (_selected != null) _history.Push((_category, _selected, _filter, _bodyContent.anchoredPosition.y));
        }
        public void GoBack()
        {
            if (_history.Count == 0) return;
            var location = _history.Pop();
            _category = location.Category; _selected = location.Entry; _filter = location.Filter;
            _search.SetTextWithoutNotify(_filter);
            SetCategory(_category);
            _bodyContent.anchoredPosition = new Vector2(0, location.Scroll);
        }
        public void ShowArticle(string id)
        {
            string key = id.Contains(":") ? id : id == "home" || GameGuideCatalog.Guides.Any(a => a.Id == id) ? "guide:" + id : "term:" + id;
            bool visible = IsVisible;
            if (!visible) Show();
            Entry target = null; Category category = Category.Guide;
            foreach (var pair in Categories)
            {
                target = EntriesOf(pair.Category).FirstOrDefault(e => e.Key == key);
                if (target != null) { category = pair.Category; break; }
            }
            if (target == null) return;
            if (visible && _selected != target) RememberLocation();
            _filter = ""; _search.SetTextWithoutNotify(""); _category = category; _selected = target;
            SetCategory(category);
        }
        public override void Hide()
        {
            if (!IsVisible) return;
            GuideTermTooltip.CloseAll(); UITooltip.HideAny();
            base.Hide();
            if (_ownsPause) Time.timeScale = _resumeScale;
            _ownsPause = false;
        }
        private List<Entry> GuideEntries()
        {
            var entries = new List<Entry> { new() { Key = "guide:home", Group = "게임 가이드", Title = "여정의 길잡이", Sub = "처음부터 차근차근", Search = "게임 가이드 시작 도움말 여정" } };
            entries.AddRange(GameGuideCatalog.Guides.Select(a => ArticleEntry(a, "guide:")));
            return entries;
        }
        private static Entry ArticleEntry(GameGuideCatalog.Article article, string prefix) => new()
        {
            Key = prefix + article.Id, Group = prefix == "guide:" ? "플레이 가이드" : article.Chapter,
            Title = article.Title, Sub = prefix == "guide:" ? article.Chapter.Split('·')[0].Trim() : article.Chapter,
            Search = $"{article.Title} {article.Chapter} {article.Summary} {article.Detail}", Body = () => GuideArticleBody(article)
        };
        private static string GuideArticleBody(GameGuideCatalog.Article a)
        {
            var sb = new StringBuilder();
            Headline(sb, a.Title, a.Chapter, UITheme.TextPrimary);
            Paragraph(sb, a.Summary, UITheme.TextPrimary);
            Section(sb, "이렇게 이해하세요"); Paragraph(sb, a.Detail);
            if (!string.IsNullOrEmpty(a.Example)) { Section(sb, "편성에 적용하기"); Paragraph(sb, a.Example); }
            if (a.Related.Length > 0)
            {
                Section(sb, "함께 읽기");
                foreach (string id in a.Related) sb.Append("›  ").Append(GameGuideCatalog.ArticleLink(id)).Append("\n");
            }
            return sb.ToString();
        }
        private static string SkillBody(CodeCatalog.Entry e, CodeCatalog.Slot slot, UnitData unit)
        {
            var sb = new StringBuilder();
            Headline(sb, e.verbalName, unit.name + " · " + SlotName(slot), UITheme.TextPrimary);
            Section(sb, "스킬 효과"); Paragraph(sb, CodeCatalog.Summary(e.description), UITheme.TextPrimary);
            if (slot == CodeCatalog.Slot.Special)
                Paragraph(sb, "인드라가 여는 특수행동으로 사용합니다. 일반행동의 차례를 소모하지 않습니다.");
            Section(sb, "사용 캐릭터"); sb.Append(GameGuideCatalog.ArticleLink($"unit:{unit.id}", unit.name));
            return sb.ToString();
        }
        private void ShowGuideHome(Entry entry)
        {
            _bodyText.gameObject.SetActive(false); _portrait.gameObject.SetActive(false);
            if (_homeRoot == null) _homeRoot = UIBuild.Container("GuideHomeCards", _bodyContent);
            UIBuild.Clear(_homeRoot); _homeRoot.gameObject.SetActive(true);
            _homeRoot.anchorMin = new Vector2(0, 1); _homeRoot.anchorMax = Vector2.one;
            _homeRoot.pivot = new Vector2(.5f, 1); _homeRoot.sizeDelta = new Vector2(0, 750);
            var hero = UIBuild.Panel("GuideWelcome", _homeRoot, UITheme.SurfaceSunken, UIShapes.Corner.Diagonal, 10, UITheme.Outline, 1);
            Place(hero.rectTransform, 0, 180, 0);
            var label = UIBuild.Text("WelcomeCaption", hero.transform, "NEVER THE LAST  /  GUIDE", UITheme.FontReadSmall, UITheme.TextMuted);
            UIBuild.Pin(label.rectTransform, new Vector2(0, 1), new Vector2(600, 30), new Vector2(26, -24));
            var title = UIBuild.Text("WelcomeTitle", hero.transform, "다음 승리를 위한 한 페이지", 34, UITheme.TextPrimary);
            title.fontStyle = FontStyles.Bold;
            UIBuild.Pin(title.rectTransform, new Vector2(0, 1), new Vector2(740, 48), new Vector2(26, -62));
            var note = UIBuild.Text("WelcomeNote", hero.transform, "편성과 전투의 원리를 익히고, 나만의 파티를 완성하세요.\n밑줄 친 용어에 머무르면 짧은 설명을 읽을 수 있습니다.", UITheme.FontReadSmall,
                UITheme.TextSecondary, TextAlignmentOptions.TopLeft, true);
            UIBuild.Pin(note.rectTransform, new Vector2(0, 1), new Vector2(850, 60), new Vector2(26, -118));
            float y = 204;
            for (int i = 0; i < GameGuideCatalog.Guides.Count; i++)
            {
                var a = GameGuideCatalog.Guides[i];
                Button card = UIBuild.Button("Chapter_" + a.Id, _homeRoot, null, () => ShowArticle(a.Id), false);
                card.image.sprite = UIShapes.CutCorner(10, UITheme.SurfaceRaised, UIShapes.Corner.Diagonal, UITheme.Outline, 1);
                RectTransform rect = card.image.rectTransform;
                rect.anchorMin = new Vector2(i % 2 * .5f, 1); rect.anchorMax = new Vector2((i % 2 + 1) * .5f, 1);
                rect.pivot = new Vector2(.5f, 1); rect.sizeDelta = new Vector2(-12, 154); rect.anchoredPosition = new Vector2(i % 2 == 0 ? -6 : 6, -(y + i / 2 * 174));
                var chapter = UIBuild.Text("Chapter", rect, a.Chapter, UITheme.FontReadSmall, UITheme.TextMuted);
                UIBuild.Stretch(chapter.rectTransform, 22, 0); chapter.rectTransform.offsetMin = new Vector2(22, 108); chapter.rectTransform.offsetMax = new Vector2(-22, -12);
                var heading = UIBuild.Text("Heading", rect, a.Title + "  ›", UITheme.FontReadHeading, UITheme.TextPrimary);
                heading.fontStyle = FontStyles.Bold;
                UIBuild.Stretch(heading.rectTransform, 22, 0); heading.rectTransform.offsetMin = new Vector2(22, 68); heading.rectTransform.offsetMax = new Vector2(-22, -48);
                var summary = UIBuild.Text("Summary", rect, a.Summary, UITheme.FontReadSmall, UITheme.TextSecondary, TextAlignmentOptions.TopLeft, true);
                UIBuild.Stretch(summary.rectTransform, 22, 0); summary.rectTransform.offsetMin = new Vector2(22, 14); summary.rectTransform.offsetMax = new Vector2(-22, -100);
            }
            _bodyContent.sizeDelta = new Vector2(0, 750); _bodyContent.anchoredPosition = Vector2.zero;
            RefreshStickyHeader(entry);
        }
    }
}
