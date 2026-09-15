using System;
using System.Collections.Generic;
using Entities;
using Helpers;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.Core
{
    /// <summary>
    /// "이 효과를 누구에게 쓸 것인가"를 묻는 아군 선택 판. 초상화 · 이름 · 한 줄 미리보기로 된
    /// 카드를 최대 다섯 장 세운다.
    ///
    /// 보상 화면과 상점이 같은 물건(회복약·부활약)을 쓰므로 선택 판도 같아야 한다.
    /// 두 화면이 각자 카드를 그리던 시절에는 미리보기 문구가 서로 달라, 같은 약을 어디서
    /// 쓰느냐에 따라 회복량이 다른 것처럼 보였다.
    ///
    /// 미리보기 문구만 화면이 정한다(<c>preview</c>) — 회복 후 체력, 부활 후 체력처럼
    /// 무엇을 보여 줄지는 물건마다 다르기 때문이다.
    /// </summary>
    public class UnitTargetPicker
    {
        /// <summary>파티 정원과 같다. 이보다 많은 후보는 들어올 수 없다.</summary>
        private const int MaxCards = 5;

        private readonly RectTransform _root;
        private readonly TextMeshProUGUI _guide;
        private readonly List<Card> _cards = new();

        public UnitTargetPicker(Transform parent, Action<Unit> onPick, Action onBack, string backLabel)
        {
            _root = UIBuild.Container("TargetPicker", parent);
            UIBuild.Stretch(_root);

            _guide = UIBuild.Text("Guide", _root, "", UITheme.FontBody, UITheme.TextSecondary,
                TextAlignmentOptions.Center);
            UIBuild.Anchor(_guide.rectTransform, new Vector2(0f, 0.88f), Vector2.one);

            for (int i = 0; i < MaxCards; i++)
            {
                _cards.Add(new Card(_root, onPick));
            }

            Button back = UIBuild.Button("Back", _root, backLabel, () => onBack?.Invoke());
            UIBuild.Anchor(back.GetComponent<RectTransform>(),
                new Vector2(0.36f, 0.02f), new Vector2(0.64f, 0.11f));

            _root.gameObject.SetActive(false);
        }

        public bool IsActive => _root.gameObject.activeSelf;

        public void SetActive(bool active) => _root.gameObject.SetActive(active);

        /// <summary>후보를 세우고 판을 연다. 후보가 없으면 열지 않고 false를 돌려준다.</summary>
        public bool Show(IReadOnlyList<Unit> candidates, string guide, Func<Unit, string> preview)
        {
            if (candidates == null || candidates.Count == 0) return false;

            _guide.text = guide;
            for (int i = 0; i < _cards.Count; i++)
            {
                _cards[i].Bind(i < candidates.Count ? candidates[i] : null, preview, i, candidates.Count);
            }

            _root.gameObject.SetActive(true);
            return true;
        }

        private sealed class Card
        {
            private readonly Image _root;
            private readonly Image _portrait;
            private readonly TextMeshProUGUI _name;
            private readonly TextMeshProUGUI _preview;
            private Unit _unit;

            public Card(Transform parent, Action<Unit> onPick)
            {
                _root = UIBuild.Panel("Target", parent, UITheme.SurfaceSunken,
                    UIShapes.Corner.Diagonal, 10, UITheme.Outline, 1);
                UIBuild.OnClick(_root.gameObject, () => onPick?.Invoke(_unit));

                var portraitObject = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
                portraitObject.transform.SetParent(_root.transform, false);
                _portrait = portraitObject.GetComponent<Image>();
                _portrait.preserveAspect = true;
                _portrait.raycastTarget = false;
                UIBuild.Anchor(_portrait.rectTransform, new Vector2(0f, 0.31f), new Vector2(1f, 0.88f), 10f, 0f);

                _name = UIBuild.Text("Name", _root.transform, "", UITheme.FontHeading,
                    UITheme.TextPrimary, TextAlignmentOptions.Center, wrap: true);
                UIBuild.Anchor(_name.rectTransform, new Vector2(0f, 0.20f), new Vector2(1f, 0.31f), 8f, 0f);

                _preview = UIBuild.Text("Preview", _root.transform, "", UITheme.FontCaption,
                    UITheme.Hp, TextAlignmentOptions.Center, wrap: true);
                UIBuild.Anchor(_preview.rectTransform, new Vector2(0f, 0.04f), new Vector2(1f, 0.20f), 6f, 0f);
            }

            public void Bind(Unit unit, Func<Unit, string> preview, int index, int count)
            {
                _unit = unit;
                _root.gameObject.SetActive(unit != null);
                if (unit == null) return;

                float width = 1f / Mathf.Max(1, count);
                UIBuild.Anchor(_root.rectTransform,
                    new Vector2(index * width, 0.14f), new Vector2((index + 1) * width, 0.84f), 8f, 0f);

                Sprite portrait = SpriteResource.LoadPortrait(unit.PortraitPath);
                _portrait.sprite = portrait;
                _portrait.enabled = portrait != null;
                _name.text = unit.UnitName;
                _preview.text = preview?.Invoke(unit) ?? "";
            }
        }
    }
}
