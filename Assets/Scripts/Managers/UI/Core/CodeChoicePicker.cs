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

            public Option(int id, string title, string subtitle, int tier)
            {
                Id = id;
                Title = title;
                Subtitle = subtitle;
                Tier = tier;
            }
        }

        private readonly RectTransform _root;
        private readonly TextMeshProUGUI _guide;
        private readonly List<Slot> _slots = new();
        private Action<int> _onPick;

        public CodeChoicePicker(Transform parent, Action onBack, string backLabel)
        {
            _root = UIBuild.Container("CodeChoicePicker", parent);
            UIBuild.Stretch(_root);

            _guide = UIBuild.Text("Guide", _root, "", UITheme.FontReadBody, UITheme.TextSecondary,
                TextAlignmentOptions.Center, wrap: true);
            UIBuild.Anchor(_guide.rectTransform, new Vector2(0f, 0.86f), Vector2.one, 12f, 0f);

            for (int i = 0; i < MaxOptions; i++) _slots.Add(new Slot(_root, i, id => _onPick?.Invoke(id)));

            Button back = UIBuild.Button("Back", _root, backLabel, () => onBack?.Invoke(), false, UITheme.FontReadSmall);
            UIBuild.Anchor(back.GetComponent<RectTransform>(), new Vector2(0.36f, 0.02f), new Vector2(0.64f, 0.11f));
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
            // 2열 × 4행. 7칸 전부를 한눈에 비교하게 같은 크기로 세운다.
            for (int i = 0; i < _slots.Count; i++) _slots[i].Bind(i < options.Count ? options[i] : (Option?)null);
            _root.gameObject.SetActive(true);
            return true;
        }

        private sealed class Slot
        {
            private readonly Image _root;
            private readonly Image _strip;
            private readonly TextMeshProUGUI _title;
            private readonly TextMeshProUGUI _subtitle;
            private int _id;

            public Slot(Transform parent, int index, Action<int> onPick)
            {
                _root = UIBuild.Panel($"Option{index}", parent, UITheme.SurfaceSunken,
                    UIShapes.Corner.Diagonal, 8, UITheme.Outline, 1);
                UIBuild.OnClick(_root.gameObject, () => onPick?.Invoke(_id));
                int column = index % 2;
                int row = index / 2;
                float top = 0.84f - row * 0.18f;
                UIBuild.Anchor(_root.rectTransform, new Vector2(column * 0.5f, top - 0.17f),
                    new Vector2((column + 1) * 0.5f, top), 10f, 0f);

                _strip = UIBuild.Solid("Tier", _root.transform, UITheme.Accent);
                UIBuild.Anchor(_strip.rectTransform, new Vector2(0f, 0f), new Vector2(0.02f, 1f));

                _title = UIBuild.Text("Title", _root.transform, "", UITheme.FontReadHeading, UITheme.TextPrimary,
                    TextAlignmentOptions.MidlineLeft);
                _title.fontStyle = FontStyles.Bold;
                _title.overflowMode = TextOverflowModes.Ellipsis;
                UIBuild.Anchor(_title.rectTransform, new Vector2(0.05f, 0.60f), new Vector2(0.98f, 0.96f));
                // 버릴 코드를 고를 때 지워질 코드의 효과를 비교할 수 있어야 한다. 두 줄까지 싣고 넘치면 툴팁이 전문을 보인다.
                _subtitle = UIBuild.Text("Subtitle", _root.transform, "", UITheme.FontReadSmall, UITheme.TextSecondary,
                    TextAlignmentOptions.TopLeft, wrap: true);
                _subtitle.overflowMode = TextOverflowModes.Ellipsis;
                UIBuild.Anchor(_subtitle.rectTransform, new Vector2(0.05f, 0.04f), new Vector2(0.98f, 0.60f));
            }

            public void Bind(Option? option)
            {
                _root.gameObject.SetActive(option.HasValue);
                if (!option.HasValue) return;
                _id = option.Value.Id;
                _title.text = option.Value.Title;
                _subtitle.text = option.Value.Subtitle;
                Color tint = UITheme.Rarity(Mathf.Clamp(option.Value.Tier, 1, 5));
                _strip.color = tint;
                int id = option.Value.Id;
                string title = option.Value.Title;
                TooltipTrigger.Attach(_root.gameObject, () => title, () => RewardCodeFlow.TooltipLines(id), tint);
            }
        }
    }
}
