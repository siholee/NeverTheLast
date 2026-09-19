using System.Collections.Generic;
using Entities;
using Entities.Status;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.HUD
{
    /// <summary>
    /// 엘리트·보스가 한둘만 서는 판에서 상단 중앙에 띄우는 체력 띠.
    ///
    /// 카드 위의 작은 바는 잡졸 다섯을 한눈에 훑기 위한 것이라, 한 마리를 오래 때리는 판에서는
    /// 정보가 너무 멀리 있다. 2인조면 좌우로 반씩 나눠 <b>둘 중 누구를 먼저 깎고 있는지</b>가
    /// 화면 한가운데에서 읽히게 한다.
    ///
    /// <b>엘리트가 하나라도 있으면 뜬다.</b> 잡졸을 달고 나오는 중간 보스에서도 보여야 하므로
    /// 카드 확대(<see cref="Combat.FeatureEnemies.SoloStageTargets"/>)보다 조건이 느슨하다.
    /// </summary>
    public sealed class FeatureEnemyBanner
    {
        /// <summary>띠 하나의 최대 너비. 2인이면 이 폭을 반씩 나눠 쓴다.</summary>
        private const float TotalWidth = 560f;

        private const float SlotHeight = 46f;
        private const float SlotGap = 10f;

        /// <summary>자원 표시줄(상단 중앙) 아래로 내려 앉히는 거리.</summary>
        private const float TopOffset = -66f;

        private const int MaxDots = 6;
        private const float DotSize = 20f;

        private readonly RectTransform _root;
        private readonly List<Slot> _slots = new();

        public FeatureEnemyBanner(Transform parent)
        {
            _root = UIBuild.Container("FeatureEnemyBanner", parent);
            UIBuild.Pin(_root, new Vector2(0.5f, 1f),
                new Vector2(TotalWidth, SlotHeight), new Vector2(0f, TopOffset));
            _root.gameObject.SetActive(false);

            for (int i = 0; i < Combat.FeatureEnemies.MaxCount; i++)
            {
                _slots.Add(new Slot(_root, i));
            }
        }

        /// <summary>매 프레임 갱신한다. 조건이 어긋나면 통째로 숨는다.</summary>
        public void Refresh()
        {
            List<Unit> featured = Combat.FeatureEnemies.BannerTargets();
            if (featured.Count == 0)
            {
                if (_root.gameObject.activeSelf) _root.gameObject.SetActive(false);
                return;
            }

            if (!_root.gameObject.activeSelf) _root.gameObject.SetActive(true);

            // 한 명이면 폭을 다 쓰고, 둘이면 가운데 틈을 두고 반씩 나눈다.
            float slotWidth = featured.Count == 1
                ? TotalWidth
                : (TotalWidth - SlotGap) * 0.5f;

            for (int i = 0; i < _slots.Count; i++)
            {
                if (i >= featured.Count)
                {
                    _slots[i].Hide();
                    continue;
                }

                float x = featured.Count == 1
                    ? 0f
                    : (i == 0 ? -(slotWidth + SlotGap) * 0.5f : (slotWidth + SlotGap) * 0.5f);

                _slots[i].Show(featured[i], slotWidth, x);
            }
        }

        /// <summary>유닛 하나를 담는 칸. 이름 · 체력 바 · 상태 아이콘 한 줄.</summary>
        private sealed class Slot
        {
            private readonly RectTransform _rect;
            private readonly TextMeshProUGUI _name;
            private readonly TextMeshProUGUI _hpText;
            private readonly Image _hpFill;
            private readonly Image _shieldFill;
            private readonly List<Image> _dots = new();

            public Slot(Transform parent, int index)
            {
                Image panel = UIBuild.Panel($"Slot{index}", parent, UITheme.HudBar,
                    UIShapes.Corner.Diagonal, 6);
                _rect = panel.rectTransform;

                _name = UIBuild.Text($"Name{index}", panel.transform, "", UITheme.FontCaption,
                    UITheme.TextPrimary, TextAlignmentOptions.Left);
                UIBuild.Anchor(_name.rectTransform, new Vector2(0f, 0.52f), new Vector2(0.62f, 1f), 10f, 2f);

                _hpText = UIBuild.Text($"Hp{index}", panel.transform, "", UITheme.FontCaption,
                    UITheme.TextSecondary, TextAlignmentOptions.Right);
                UIBuild.Anchor(_hpText.rectTransform, new Vector2(0.62f, 0.52f), new Vector2(1f, 1f), 10f, 2f);

                // 방어막은 체력 바 위에 겹쳐 그린다. 체력이 줄어도 방어막 폭은 그대로라
                // "아직 껍질이 남았다"가 한눈에 보인다.
                _hpFill = UIBuild.Bar($"HpBar{index}", panel.transform, UITheme.Hp, UITheme.SurfaceSunken);
                RectTransform track = (RectTransform)_hpFill.transform.parent;
                UIBuild.Anchor(track, new Vector2(0f, 0f), new Vector2(1f, 0.46f), 10f, 6f);

                _shieldFill = UIBuild.Bar($"ShieldBar{index}", track, UITheme.Shield, Color.clear);
                UIBuild.Stretch(((RectTransform)_shieldFill.transform.parent));

                for (int i = 0; i < MaxDots; i++)
                {
                    var go = new GameObject($"Dot{index}_{i}", typeof(RectTransform), typeof(Image));
                    go.transform.SetParent(panel.transform, false);
                    var dot = go.GetComponent<Image>();
                    dot.raycastTarget = false;
                    var dotRect = (RectTransform)go.transform;
                    dotRect.sizeDelta = new Vector2(DotSize, DotSize);
                    dotRect.anchorMin = dotRect.anchorMax = new Vector2(1f, 1f);
                    dotRect.pivot = new Vector2(1f, 1f);
                    dotRect.anchoredPosition = new Vector2(-10f - i * (DotSize + 3f), -2f);
                    go.SetActive(false);
                    _dots.Add(dot);
                }
            }

            public void Hide()
            {
                if (_rect.gameObject.activeSelf) _rect.gameObject.SetActive(false);
            }

            public void Show(Unit unit, float width, float x)
            {
                if (!_rect.gameObject.activeSelf) _rect.gameObject.SetActive(true);

                _rect.sizeDelta = new Vector2(width, SlotHeight);
                _rect.anchoredPosition = new Vector2(x, 0f);

                _name.text = unit.UnitName;

                float ratio = unit.HpMax > 0 ? Mathf.Clamp01((float)unit.HpCurr / unit.HpMax) : 0f;
                _hpFill.fillAmount = ratio;
                _hpFill.color = UITheme.HpColor(ratio);
                // 준비 화면에서는 체력 배수 패시브가 아직 걸리지 않았다. 전투에서 마주할 값으로 보여 준다.
                int shownMax = unit.ProjectedHpMax;
                int shownCurr = shownMax == unit.HpMax || unit.HpMax <= 0
                    ? unit.HpCurr
                    : Mathf.RoundToInt(unit.HpCurr * (shownMax / (float)unit.HpMax));
                _hpText.text = $"{shownCurr:N0} / {shownMax:N0}";

                float shieldRatio = unit.HpMax > 0
                    ? Mathf.Clamp01((float)unit.ShieldCurr / unit.HpMax)
                    : 0f;
                _shieldFill.fillAmount = shieldRatio;
                _shieldFill.enabled = shieldRatio > 0f;

                RefreshDots(unit);
            }

            private void RefreshDots(Unit unit)
            {
                IReadOnlyList<UnitStatus> statuses = unit.ActiveStatuses;
                int shown = 0;

                for (int i = 0; i < statuses.Count && shown < _dots.Count; i++)
                {
                    UnitStatus status = statuses[i];
                    if (status == null) continue;

                    Image dot = _dots[shown];
                    dot.sprite = StatusIcons.For(status);
                    dot.color = StatusIcons.Tint(status);
                    if (!dot.gameObject.activeSelf) dot.gameObject.SetActive(true);
                    shown++;
                }

                for (int i = shown; i < _dots.Count; i++)
                {
                    if (_dots[i].gameObject.activeSelf) _dots[i].gameObject.SetActive(false);
                }
            }
        }
    }
}
