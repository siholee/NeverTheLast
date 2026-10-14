using System;
using System.Collections.Generic;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.Core
{
    /// <summary>
    /// "어느 코드를?"을 묻는 선택 판. 칸이 찼을 때 덮어쓸 코드, 승급의 인장으로 올릴 코드,
    /// 전수의 두루마리로 옮길 코드를 같은 모양으로 고른다(Detail_17 §2).
    /// 유닛을 고르는 <see cref="UnitTargetPicker"/>와 짝을 이룬다.
    /// </summary>
    public class CodeChoicePicker
    {
        /// <summary>한 판에 세우는 최대 선택지. 보상 코드 칸(7) + 여유.</summary>
        private const int MaxOptions = 8;

        public readonly struct Option
        {
            public readonly int Id;
            public readonly string Title;
            public readonly string Subtitle;
            public readonly int Tier;
            public readonly Action OnPick;

            public Option(int id, string title, string subtitle, int tier, Action onPick = null)
            {
                Id = id;
                Title = title;
                Subtitle = subtitle;
                Tier = tier;
                OnPick = onPick;
            }
        }

        private readonly RectTransform _root;
        private readonly TextMeshProUGUI _guide;
        private readonly List<Slot> _slots = new();
        private Action<int> _onPick;
        private IReadOnlyList<Option> _options;
        private int _page;
        private readonly Button _previous;
        private readonly Button _next;
        private readonly TextMeshProUGUI _pageLabel;

        public CodeChoicePicker(Transform parent, Action onBack, string backLabel)
        {
            _root = UIBuild.Container("CodeChoicePicker", parent);
            UIBuild.Stretch(_root);

            _guide = UIBuild.Text("Guide", _root, "", UITheme.FontReadBody, UITheme.TextSecondary,
                TextAlignmentOptions.Center, wrap: true);
            UIBuild.Anchor(_guide.rectTransform, new Vector2(0f, 0.86f), Vector2.one, 12f, 0f);

            for (int i = 0; i < MaxOptions; i++) _slots.Add(new Slot(_root, i, id => _onPick?.Invoke(id)));

            Button back = UIBuild.Button("Back", _root, backLabel, () => onBack?.Invoke(), false, UITheme.FontReadSmall);
            RectTransform backRect = back.GetComponent<RectTransform>();
            backRect.anchorMin = backRect.anchorMax = backRect.pivot = new Vector2(0.5f, 0f);
            backRect.sizeDelta = new Vector2(320f, 48f);
            backRect.anchoredPosition = new Vector2(0f, 4f);
            _previous = UIBuild.Button("PreviousPage", _root, "이전", () => ChangePage(-1), false, UITheme.FontReadSmall);
            _next = UIBuild.Button("NextPage", _root, "다음", () => ChangePage(1), false, UITheme.FontReadSmall);
            foreach (var entry in new[] { (_previous, -280f), (_next, 280f) })
            {
                var rect = entry.Item1.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 0f);
                rect.sizeDelta = new Vector2(120f, 48f);
                rect.anchoredPosition = new Vector2(entry.Item2, 4f);
            }
            _pageLabel = UIBuild.Text("PageNumber", _root, "", UITheme.FontReadSmall, UITheme.TextSecondary, TextAlignmentOptions.Center);
            UIBuild.Anchor(_pageLabel.rectTransform, new Vector2(.35f, .86f), new Vector2(.65f, .90f));
            // 보상 · 상점 · 공용 보급이 함께 쓰는 선택 판이라 판 안의 22 이상 글자에만 가독 하한을 건다.
            UIScaledText.KeepReadable(_root);
            _root.gameObject.SetActive(false);
        }

        public bool IsActive => _root.gameObject.activeSelf;
        public void SetActive(bool active) => _root.gameObject.SetActive(active);

        public bool Show(IReadOnlyList<Option> options, string guide, Action<int> onPick)
        {
            if (options == null || options.Count == 0) return false;
            _onPick = onPick;
            _guide.text = guide;
            _options = options;
            _page = 0;
            BindPage();
            _root.gameObject.SetActive(true);
            return true;
        }

        private void ChangePage(int delta)
        {
            _page = Mathf.Clamp(_page + delta, 0, (_options.Count - 1) / MaxOptions);
            BindPage();
        }

        private void BindPage()
        {
            int pages = (_options.Count + MaxOptions - 1) / MaxOptions;
            for (int i = 0; i < _slots.Count; i++)
            {
                int index = _page * MaxOptions + i;
                _slots[i].Bind(index < _options.Count ? _options[index] : (Option?)null);
            }
            _previous.gameObject.SetActive(pages > 1);
            _next.gameObject.SetActive(pages > 1);
            _previous.interactable = _page > 0;
            _next.interactable = _page + 1 < pages;
            _pageLabel.gameObject.SetActive(pages > 1);
            _pageLabel.text = $"{_page + 1} / {pages}";
            UIBuild.Anchor(_guide.rectTransform, new Vector2(0f, pages > 1 ? .91f : .86f), Vector2.one, 12f, 0f);
        }

        private sealed class Slot
        {
            private readonly Image _root;
            private readonly Image _strip;
            private readonly TextMeshProUGUI _title;
            private readonly TextMeshProUGUI _subtitle;
            private int _id;
            private Action _pick;

            public Slot(Transform parent, int index, Action<int> onPick)
            {
                // 선택지 하나 = 흰 카드 한 장. 상점 · 보상 · 받을 아군 판과 같은 재질이다.
                _root = UIBuild.Solid($"Option{index}", parent, Color.white);
                _root.sprite = UIArtDirection.Sheet(12);
                UIBuild.OnClick(_root.gameObject, () => { if (_pick != null) _pick(); else onPick?.Invoke(_id); });
                int column = index % 2;
                int row = index / 2;
                float top = 0.84f - row * 0.18f;
                UIBuild.Anchor(_root.rectTransform, new Vector2(column * 0.5f, top - 0.17f),
                    new Vector2((column + 1) * 0.5f, top), 10f, 0f);

                _strip = UIBuild.Solid("Tier", _root.transform, UITheme.Accent);
                UIBuild.Anchor(_strip.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f));

                _title = UIBuild.Text("Title", _root.transform, "", 22f, UITheme.TextPrimary,
                    TextAlignmentOptions.MidlineLeft);
                _title.fontStyle = FontStyles.Bold;
                _title.overflowMode = TextOverflowModes.Ellipsis;
                UIBuild.Anchor(_title.rectTransform, new Vector2(0f, 0.57f), new Vector2(1f, .96f), 20f, 0f);
                // 버릴 코드를 고를 때 지워질 코드의 효과를 비교할 수 있어야 한다. 두 줄까지 싣고 넘치면 툴팁이 전문을 보인다.
                _subtitle = UIBuild.Text("Subtitle", _root.transform, "", UITheme.FontReadSmall, UITheme.TextSecondary,
                    TextAlignmentOptions.TopLeft, wrap: true);
                _subtitle.overflowMode = TextOverflowModes.Ellipsis;
                UIBuild.Anchor(_subtitle.rectTransform, new Vector2(0f, .06f), new Vector2(1f, .57f), 20f, 0f);
            }

            public void Bind(Option? option)
            {
                _root.gameObject.SetActive(option.HasValue);
                if (!option.HasValue) return;
                _id = option.Value.Id;
                _pick = option.Value.OnPick;
                _title.text = option.Value.Title;
                _subtitle.text = option.Value.Subtitle;
                Color tint = UITheme.Rarity(Mathf.Clamp(option.Value.Tier, 1, 5));
                _strip.color = UITheme.Divider;
                _strip.rectTransform.sizeDelta = new Vector2(0, 1);
                _strip.gameObject.SetActive(false);
                int id = option.Value.Id;
                string title = option.Value.Title;
                TooltipTrigger.Attach(_root.gameObject, () => title, () => RewardCodeFlow.TooltipLines(id), tint);
            }
        }
    }
}
