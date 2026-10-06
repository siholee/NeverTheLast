using System.Collections.Generic;
using System.Linq;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Managers.UI.Core
{
    /// <summary>출처를 남긴 채 용어를 연속 탐색하는 작은 사전. 카드와 출처 사이 이동에 유예를 둔다.</summary>
    public sealed class GuideTermTooltip : MonoBehaviour
    {
        private sealed class Card
        {
            public object Owner;
            public string Id;
            public RectTransform Root;
            public CanvasGroup Group;
            public float Born, Release = -1;
            public bool Pinned;
        }
        private static GuideTermTooltip _instance;
        private readonly List<Card> _cards = new();
        private RectTransform _canvas;
        internal static int Depth => _instance != null ? _instance._cards.Count : 0;
        internal static string CurrentId => _instance?._cards.LastOrDefault()?.Id;
        public static void Show(object owner, string id, Vector2 point, bool immediatePin = false)
        {
            var article = GameGuideCatalog.Find(id); if (article == null) return;
            if (_instance == null)
            {
                Canvas canvas = UIBuild.Canvas("GuideTooltipCanvas", 920);
                _instance = canvas.gameObject.AddComponent<GuideTermTooltip>();
                _instance._canvas = (RectTransform)canvas.transform;
            }
            _instance.ShowCard(owner, article, point, immediatePin);
        }
        private void ShowCard(object owner, GameGuideCatalog.Article article, Vector2 point, bool pinned)
        {
            int existing = _cards.FindIndex(c => c.Owner == owner && c.Id == article.Id);
            if (existing >= 0) { _cards[existing].Release = -1; return; }
            Component source = owner as Component;
            int parent = source == null ? -1 : _cards.FindLastIndex(c => source.transform.IsChildOf(c.Root));
            Trim(parent + 1);
            if (_cards.Count >= 4) return;
            Image panel = UIBuild.Panel("Term_" + article.Id, _canvas, UITheme.SurfaceRaised,
                UIShapes.Corner.Diagonal, 8, UITheme.Outline, 1);
            RectTransform root = panel.rectTransform;
            root.anchorMin = root.anchorMax = new Vector2(.5f, .5f); root.pivot = new Vector2(0f, 1f);
            var card = new Card { Owner = owner, Id = article.Id, Root = root, Group = UIBuild.Group(root.gameObject), Born = Time.unscaledTime, Pinned = pinned };
            _cards.Add(card);
            var stripe = UIBuild.Solid("TermLine", root, UITheme.Accent);
            UIBuild.Anchor(stripe.rectTransform, new Vector2(0, 1), Vector2.one);
            stripe.rectTransform.sizeDelta = new Vector2(-32, 3); stripe.rectTransform.anchoredPosition = new Vector2(0, -2);
            var category = UIBuild.Text("Chapter", root, $"용어  /  {article.Chapter}", UITheme.FontReadSmall, UITheme.TextMuted);
            UIBuild.Pin(category.rectTransform, new Vector2(0, 1), new Vector2(364, 30), new Vector2(18, -16));
            var title = UIBuild.Text("Title", root, article.Title, UITheme.FontReadHeading, UITheme.TextPrimary);
            title.fontStyle = FontStyles.Bold;
            UIBuild.Pin(title.rectTransform, new Vector2(0, 1), new Vector2(364, 38), new Vector2(18, -48));
            var body = UIBuild.Text("Explanation", root, article.Summary, UITheme.FontReadSmall, UITheme.TextSecondary, TextAlignmentOptions.TopLeft, true);
            body.text = GameGuideCatalog.LinkTerms(body.text);
            float bodyHeight = body.GetPreferredValues(body.text, 364, 0).y + 6;
            UIBuild.Pin(body.rectTransform, new Vector2(0, 1), new Vector2(364, bodyHeight), new Vector2(18, -94));
            float y = 100 + bodyHeight;
            var related = UIBuild.Text("Related", root,
                string.Join("  ·  ", article.Related.Take(3).Select(k => $"<link=\"term:{k}\"><color=#177DA5><u>{GameGuideCatalog.Find(k)?.Title}</u></color></link>")),
                UITheme.FontReadSmall, UITheme.TextSecondary, TextAlignmentOptions.TopLeft, true);
            float relatedHeight = related.GetPreferredValues(related.text, 364, 0).y;
            UIBuild.Pin(related.rectTransform, new Vector2(0, 1), new Vector2(364, relatedHeight), new Vector2(18, -y));
            y += relatedHeight + 14;
            Button read = UIBuild.Button("ReadGuide", root, "자료실에서 자세히 읽기  ›", () => GameTermLinks.OpenArticle(article.Id), true);
            read.GetComponentInChildren<TextMeshProUGUI>().text = "자료실에서 자세히 읽기  ›";
            read.GetComponentInChildren<TextMeshProUGUI>().fontSize = UITheme.FontReadSmall;
            UIBuild.Pin(read.image.rectTransform, new Vector2(0, 1), new Vector2(364, 42), new Vector2(18, -y));
            y += 52;
            var hint = UIBuild.Text("PinHint", root, "잠시 머무르면 고정 · ESC로 닫기", UITheme.FontReadSmall, UITheme.TextMuted);
            hint.enableAutoSizing = true; hint.fontSizeMin = UITheme.FontReadFloor; hint.fontSizeMax = UITheme.FontReadSmall;
            UIBuild.Pin(hint.rectTransform, new Vector2(0, 1), new Vector2(364, 28), new Vector2(18, -y));
            root.sizeDelta = new Vector2(400, y + 42);
            card.Group.blocksRaycasts = pinned;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, point, null, out Vector2 local);
            Rect bounds = _canvas.rect; float x = local.x + 14, top = local.y + 16;
            if (parent >= 0)
            {
                Card prior = _cards[parent]; x = prior.Root.anchoredPosition.x + 390; top = prior.Root.anchoredPosition.y - 36;
                if (x + 400 > bounds.xMax) x = prior.Root.anchoredPosition.x - 390;
            }
            else if (x + 400 > bounds.xMax) x = local.x - 414;
            root.anchoredPosition = new Vector2(Mathf.Clamp(x, bounds.xMin + 12, bounds.xMax - 412),
                Mathf.Clamp(top, bounds.yMin + root.sizeDelta.y + 12, bounds.yMax - 12));
        }
        public static void Release(object owner)
        {
            if (_instance == null) return;
            foreach (Card card in _instance._cards.Where(c => c.Owner == owner))
                if (card.Release < 0) card.Release = Time.unscaledTime;
        }
        public static void CloseAll() { if (_instance != null) _instance.Trim(0); }
        public static bool TryDismiss()
        {
            if (Depth == 0) return false;
            _instance.Trim(Depth - 1);
            return true;
        }
        private void Trim(int count)
        {
            for (int i = _cards.Count - 1; i >= count; i--) { Destroy(_cards[i].Root.gameObject); _cards.RemoveAt(i); }
        }
        private void Update()
        {
            if (_cards.Count == 0) return;
            Vector2 point = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null) point = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
#else
            point = Input.mousePosition;
#endif
            for (int i = 0; i < _cards.Count; i++)
            {
                Card card = _cards[i];
                if (card.Owner is Component source && (source == null || !source.gameObject.activeInHierarchy)) { Trim(i); break; }
                if (!card.Pinned && Time.unscaledTime - card.Born > .65f) { card.Pinned = true; card.Group.blocksRaycasts = true; }
                bool inBranch = _cards.Skip(i).Any(c => RectTransformUtility.RectangleContainsScreenPoint(c.Root, point));
                if (inBranch) card.Release = -1;
                else if (card.Release < 0 && card.Pinned) card.Release = Time.unscaledTime;
                if (card.Release >= 0 && Time.unscaledTime - card.Release > .7f) { Trim(i); break; }
            }
        }
    }
}
