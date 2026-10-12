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

        /// <summary>
        /// 후보 카드의 기준 크기와 간격(기준 해상도 px). 예전에는 판 폭을 후보 수로 나눈 비율 칸에 그림 · 이름 · 미리보기를
        /// 띄워 두어 칸 사이가 비고 미리보기가 바닥에 떨어져 보였다(2026-10-08 1920 검수). 지금은 같은 크기의 흰 카드를
        /// 가운데 모아 세우고, 남는 높이는 위아래에 똑같이 나눈다.
        /// </summary>
        private const float CardWidth = 236f;
        private const float CardHeight = 340f;
        private const float CardGap = 22f;
        private const float GuideBand = 92f;
        private const float BackBand = 72f;

        private readonly RectTransform _root;
        private readonly RectTransform _stage;
        private readonly TextMeshProUGUI _guide;
        private readonly List<Card> _cards = new();

        public UnitTargetPicker(Transform parent, Action<Unit> onPick, Action onBack, string backLabel)
        {
            _root = UIBuild.Container("TargetPicker", parent);
            UIBuild.Stretch(_root);

            _guide = UIBuild.Text("Guide", _root, "", UITheme.FontReadBody, UITheme.TextSecondary,
                TextAlignmentOptions.Midline, wrap: true);
            _guide.rectTransform.anchorMin = new Vector2(0f, 1f);
            _guide.rectTransform.anchorMax = Vector2.one;
            _guide.rectTransform.offsetMin = new Vector2(12f, -GuideBand + 8f);
            _guide.rectTransform.offsetMax = new Vector2(-12f, 0f);

            // 카드 무대 — 안내 줄과 돌아가기 줄 사이. 카드는 이 무대의 정중앙을 기준으로 좌우 대칭 배치한다.
            _stage = UIBuild.Container("Cards", _root);
            UIBuild.Stretch(_stage);
            _stage.offsetMin = new Vector2(0f, BackBand);
            _stage.offsetMax = new Vector2(0f, -GuideBand);

            for (int i = 0; i < MaxCards; i++)
            {
                _cards.Add(new Card(_stage, onPick));
            }

            Button back = UIBuild.Button("Back", _root, backLabel, () => onBack?.Invoke(), false, UITheme.FontReadSmall);
            RectTransform backRect = back.GetComponent<RectTransform>();
            backRect.anchorMin = backRect.anchorMax = backRect.pivot = new Vector2(0.5f, 0f);
            backRect.sizeDelta = new Vector2(320f, 48f);
            backRect.anchoredPosition = new Vector2(0f, 4f);

            // 보상 · 상점 · 공용 보급이 함께 쓰는 선택 판이라 판 안의 22 이상 글자에만 가독 하한을 건다.
            UIScaledText.KeepReadable(_root);
            _root.gameObject.SetActive(false);
        }

        public bool IsActive => _root.gameObject.activeSelf;

        public void SetActive(bool active) => _root.gameObject.SetActive(active);

        /// <summary>후보를 세우고 판을 연다. 후보가 없으면 열지 않고 false를 돌려준다.</summary>
        public bool Show(IReadOnlyList<Unit> candidates, string guide, Func<Unit, string> preview)
        {
            if (candidates == null || candidates.Count == 0) return false;

            _guide.text = guide;
            Canvas.ForceUpdateCanvases();
            Vector2 stage = _stage.rect.size;
            int count = Mathf.Min(candidates.Count, _cards.Count);
            float width = stage.x > 100f
                ? Mathf.Min(CardWidth, (stage.x - CardGap * (count - 1)) / Mathf.Max(1, count))
                : CardWidth;
            float height = stage.y > 100f ? Mathf.Min(CardHeight, stage.y - 12f) : CardHeight;
            for (int i = 0; i < _cards.Count; i++)
            {
                _cards[i].Bind(i < candidates.Count ? candidates[i] : null, preview, i, count, new Vector2(width, height));
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
                // 흰 카드 한 장 — 위 62%는 초상 창(카드 폭에 맞춰 머리 위부터 잘라 낸 원화), 아래는 이름과 결과 미리보기.
                _root = UIBuild.Solid("Target", parent, Color.white);
                _root.sprite = UIArtDirection.Sheet(14);
                _root.type = Image.Type.Sliced;
                UIBuild.OnClick(_root.gameObject, () => onPick?.Invoke(_unit));

                Image well = UIBuild.Solid("PortraitWell", _root.transform, Color.white);
                well.sprite = UIShapes.RoundedRect(10, new Color(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, 0.05f));
                well.type = Image.Type.Sliced;
                well.raycastTarget = false;
                UIBuild.Anchor(well.rectTransform, new Vector2(0f, 0.38f), Vector2.one);
                well.rectTransform.offsetMin = new Vector2(8f, 0f);
                well.rectTransform.offsetMax = new Vector2(-8f, -8f);

                var portraitObject = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
                portraitObject.transform.SetParent(well.transform, false);
                _portrait = portraitObject.GetComponent<Image>();
                _portrait.preserveAspect = true;
                _portrait.raycastTarget = false;
                UIBuild.Stretch(_portrait.rectTransform, 2f, 2f);

                _name = UIBuild.Text("Name", _root.transform, "", 24f,
                    UITheme.TextPrimary, TextAlignmentOptions.Midline, wrap: true);
                _name.fontStyle = FontStyles.Bold;
                UIBuild.Anchor(_name.rectTransform, new Vector2(0f, 0.26f), new Vector2(1f, 0.37f), 10f, 0f);

                Image rule = UIBuild.Divider("Rule", _root.transform);
                rule.raycastTarget = false;
                UIBuild.Anchor(rule.rectTransform, new Vector2(0.2f, 0.25f), new Vector2(0.8f, 0.25f));
                rule.rectTransform.sizeDelta = new Vector2(0f, 1f);

                _preview = UIBuild.Text("Preview", _root.transform, "", UITheme.FontReadSmall,
                    UITheme.AccentDim, TextAlignmentOptions.Midline, wrap: true);
                UIBuild.Anchor(_preview.rectTransform, new Vector2(0f, 0.03f), new Vector2(1f, 0.24f), 10f, 0f);
            }

            public void Bind(Unit unit, Func<Unit, string> preview, int index, int count, Vector2 size)
            {
                _unit = unit;
                _root.gameObject.SetActive(unit != null);
                if (unit == null) return;

                RectTransform rect = _root.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = size;
                rect.anchoredPosition = new Vector2((index - (count - 1) * 0.5f) * (size.x + CardGap), 0f);

                Sprite portrait = SpriteResource.LoadPortrait(unit.PortraitPath);
                float aspect = Mathf.Max(0.4f, (size.x - 20f) / Mathf.Max(1f, size.y * 0.62f - 12f));
                Sprite window = portrait != null ? Managers.UI.Screens.ScreenKit.PortraitWindow(portrait, aspect) : null;
                _portrait.sprite = window != null ? window : portrait;
                _portrait.enabled = portrait != null;
                _name.text = unit.UnitName;
                // 결과를 두 줄로 구분해 한글 음절 중간 줄바꿈을 피한다.
                _preview.text = (preview?.Invoke(unit) ?? "").Replace(" — ", "\n");
            }
        }
    }
}
