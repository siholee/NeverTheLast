using System.Collections.Generic;
using System.Linq;
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
    /// 우하단 아군 카드. 붕괴: 스타레일의 좌하단 캐릭터 정보를 옮겨 온 형태다.
    ///
    /// 한 장에 담기는 것:
    ///   · 초상화
    ///   · 궁극기 충전(초상화 테두리 링)
    ///   · 고유 게이지(있는 유닛만)
    ///   · HP 바 — 적색. 방어막이 있으면 같은 바 위에 청색이 덧씌워진다.
    ///   · Action Bar — HP 바 바로 아래, 절반 높이·같은 너비의 노란 바.
    ///     DEX 기반 행동 예약까지 얼마나 남았는지를 좌→우로 채워 보여준다.
    ///   · Modifier — 걸려 있는 버프/디버프 표식
    /// </summary>
    public class PartyPanel
    {
        private const int MaxCards = 6;
        private const float CardWidth = 132f;
        private const float CardHeight = 128f;
        private const float CardGap = 8f;
        private const float PortraitSize = 56f;
        private const float HpBarHeight = 10f;
        private const float ActionBarHeight = HpBarHeight * 0.5f;
        private const int MaxModifierDots = 5;

        private readonly RectTransform _root;
        private readonly List<Card> _cards = new();
        private readonly List<Unit> _bound = new();

        public PartyPanel(Transform parent)
        {
            _root = UIBuild.Container("PartyPanel", parent);
            // 우하단 고정. 카드가 오른쪽에서 왼쪽으로 늘어선다.
            UIBuild.Pin(_root, new Vector2(1f, 0f),
                new Vector2(MaxCards * (CardWidth + CardGap), CardHeight),
                new Vector2(-20f, 20f));

            for (int i = 0; i < MaxCards; i++)
            {
                _cards.Add(new Card(_root, i));
            }
        }

        public void Tick()
        {
            List<Unit> units = CollectAllies();

            if (!SameUnits(units))
            {
                _bound.Clear();
                _bound.AddRange(units);
                for (int i = 0; i < _cards.Count; i++)
                {
                    _cards[i].Bind(i < units.Count ? units[i] : null);
                }
            }

            foreach (Card card in _cards) card.Refresh();
        }

        private bool SameUnits(List<Unit> units)
        {
            if (units.Count != _bound.Count) return false;
            for (int i = 0; i < units.Count; i++)
            {
                if (!ReferenceEquals(units[i], _bound[i])) return false;
            }

            return true;
        }

        private static List<Unit> CollectAllies()
        {
            GridManager grid = GridManager.Instance;
            if (grid?.heroList == null) return new List<Unit>();

            return grid.heroList
                .Where(unit => unit != null && !unit.IsEnemy && unit.currentCell != null && unit.currentCell.yPos > 0)
                .OrderBy(unit => unit.currentCell.yPos)
                .ThenBy(unit => unit.currentCell.xPos)
                .Take(MaxCards)
                .ToList();
        }

        private sealed class Card
        {
            private readonly Image _frame;
            private readonly Image _portrait;
            private readonly Image _ultimateRing;
            private readonly Image _actingGlow;
            private readonly TextMeshProUGUI _name;
            private readonly TextMeshProUGUI _hpText;

            private readonly Image _hpFill;
            private readonly Image _shieldFill;
            private readonly Image _actionFill;

            private readonly RectTransform _uniqueGaugeRoot;
            private readonly Image _uniqueFill;
            private readonly TextMeshProUGUI _uniqueLabel;

            private readonly Image[] _modifierDots = new Image[MaxModifierDots];

            private Unit _unit;
            private string _portraitPath;
            private string _uniqueResourceId;

            public Card(Transform parent, int index)
            {
                _frame = UIBuild.Panel($"Card{index}", parent, UITheme.HudBar,
                    UIShapes.Corner.Diagonal, 8);
                // 오른쪽 끝부터 왼쪽으로 채운다.
                UIBuild.Pin(_frame.rectTransform, new Vector2(1f, 0f),
                    new Vector2(CardWidth, CardHeight),
                    new Vector2(-index * (CardWidth + CardGap), 0f));

                // 지금 행동 중인 유닛을 알리는 앰버 테두리.
                _actingGlow = UIBuild.Panel("ActingGlow", _frame.transform, Color.clear,
                    UIShapes.Corner.Diagonal, 8, UITheme.Accent, 2);
                UIBuild.Stretch(_actingGlow.rectTransform);
                _actingGlow.raycastTarget = false;
                _actingGlow.enabled = false;

                // ── 초상화 + 궁극기 충전 링 ──
                // 링이 한 바퀴 차면 궁극기 준비 완료다.
                _ultimateRing = UIBuild.RadialBar("UltRing", _frame.transform, UITheme.Mana,
                    new Color(1f, 1f, 1f, 0.10f), 96, 0.82f);
                RectTransform ringTrack = _ultimateRing.rectTransform.parent as RectTransform;
                UIBuild.Pin(ringTrack, new Vector2(0.5f, 1f),
                    new Vector2(PortraitSize + 10f, PortraitSize + 10f), new Vector2(0f, -8f));

                var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
                portraitGo.transform.SetParent(ringTrack, false);
                _portrait = portraitGo.GetComponent<Image>();
                _portrait.preserveAspect = true;
                _portrait.raycastTarget = false;
                UIBuild.Stretch(_portrait.rectTransform, 7f, 7f);

                // ── 이름 ──
                _name = UIBuild.Text("Name", _frame.transform, "", UITheme.FontMicro,
                    UITheme.TextPrimary, TextAlignmentOptions.Center);
                UIBuild.Pin(_name.rectTransform, new Vector2(0.5f, 1f), new Vector2(CardWidth - 10f, 14f),
                    new Vector2(0f, -(PortraitSize + 20f)));
                _name.overflowMode = TextOverflowModes.Ellipsis;

                // ── 고유 게이지(있는 유닛만 켜진다) ──
                _uniqueGaugeRoot = UIBuild.Container("UniqueGauge", _frame.transform);
                UIBuild.Pin(_uniqueGaugeRoot, new Vector2(0.5f, 0f), new Vector2(CardWidth - 16f, 12f),
                    new Vector2(0f, 46f));

                _uniqueFill = UIBuild.Bar("Unique", _uniqueGaugeRoot, UITheme.Accent, UITheme.SurfaceSunken);
                UIBuild.Stretch((RectTransform)_uniqueFill.rectTransform.parent);

                _uniqueLabel = UIBuild.Text("UniqueLabel", _uniqueGaugeRoot, "", UITheme.FontMicro,
                    UITheme.TextOnAccent, TextAlignmentOptions.Center);
                UIBuild.Stretch(_uniqueLabel.rectTransform);
                _uniqueGaugeRoot.gameObject.SetActive(false);

                // ── HP 바 (적색) + 방어막(청색 덧씌움) ──
                _hpFill = UIBuild.Bar("Hp", _frame.transform, UITheme.HpRed, UITheme.SurfaceSunken);
                RectTransform hpTrack = (RectTransform)_hpFill.rectTransform.parent;
                UIBuild.Pin(hpTrack, new Vector2(0.5f, 0f), new Vector2(CardWidth - 16f, HpBarHeight),
                    new Vector2(0f, 30f));

                // 방어막은 같은 트랙 위에 겹쳐 왼쪽부터 파랗게 덮는다.
                var shieldGo = new GameObject("ShieldFill", typeof(RectTransform), typeof(Image));
                shieldGo.transform.SetParent(hpTrack, false);
                _shieldFill = shieldGo.GetComponent<Image>();
                _shieldFill.sprite = UIShapes.Solid(Color.white);
                _shieldFill.type = Image.Type.Filled;
                _shieldFill.fillMethod = Image.FillMethod.Horizontal;
                _shieldFill.color = UITheme.ShieldBlue;
                _shieldFill.raycastTarget = false;
                UIBuild.Stretch(_shieldFill.rectTransform);

                _hpText = UIBuild.Text("HpText", _frame.transform, "", UITheme.FontMicro,
                    UITheme.TextSecondary, TextAlignmentOptions.MidlineRight);
                UIBuild.Pin(_hpText.rectTransform, new Vector2(0.5f, 0f), new Vector2(CardWidth - 16f, 12f),
                    new Vector2(0f, 41f));

                // ── Action Bar: HP 바 바로 아래, 절반 높이·같은 너비 ──
                _actionFill = UIBuild.Bar("Action", _frame.transform, UITheme.ActionYellow,
                    UITheme.SurfaceSunken);
                RectTransform actionTrack = (RectTransform)_actionFill.rectTransform.parent;
                UIBuild.Pin(actionTrack, new Vector2(0.5f, 0f),
                    new Vector2(CardWidth - 16f, ActionBarHeight),
                    new Vector2(0f, 30f - ActionBarHeight - 2f));

                // ── Modifier 점 ──
                for (int i = 0; i < MaxModifierDots; i++)
                {
                    Image dot = UIBuild.Solid($"Mod{i}", _frame.transform, UITheme.TextMuted);
                    UIBuild.Pin(dot.rectTransform, new Vector2(0f, 0f), new Vector2(8f, 8f),
                        new Vector2(9f + i * 11f, 10f));
                    dot.raycastTarget = false;
                    dot.enabled = false;
                    _modifierDots[i] = dot;
                }

                UIBuild.OnClick(_frame.gameObject, () =>
                {
                    if (_unit != null) GameManager.Instance?.uiManager?.ShowUnitDetail(_unit);
                });

                _frame.gameObject.SetActive(false);
            }

            public void Bind(Unit unit)
            {
                _unit = unit;
                _frame.gameObject.SetActive(unit != null);
                if (unit == null) return;

                _name.text = unit.UnitName;
                LoadPortrait(unit);

                // 고유 게이지는 전투 자원을 가진 유닛만 보여준다.
                _uniqueResourceId = unit.CombatResourceIds.FirstOrDefault();
                _uniqueGaugeRoot.gameObject.SetActive(!string.IsNullOrEmpty(_uniqueResourceId));
            }

            private void LoadPortrait(Unit unit)
            {
                if (_portraitPath == unit.PortraitPath) return;
                _portraitPath = unit.PortraitPath;

                Sprite sprite = string.IsNullOrEmpty(unit.PortraitPath)
                    ? null
                    : Resources.Load<Sprite>(unit.PortraitPath);
                _portrait.sprite = sprite;
                _portrait.enabled = sprite != null;
            }

            public void Refresh()
            {
                if (_unit == null) return;

                bool alive = _unit.isActive && _unit.HpCurr > 0;
                ActionScheduler scheduler = GameManager.Instance?.ActionScheduler;

                // ── HP (적색) ──
                float hpRatio = _unit.HpMax > 0 ? Mathf.Clamp01(_unit.HpCurr / (float)_unit.HpMax) : 0f;
                _hpFill.fillAmount = hpRatio;
                _hpText.text = $"{Mathf.Max(0, _unit.HpCurr)}";

                // ── 방어막 (청색, HP 위에 덧씌움) ──
                float shieldRatio = _unit.HpMax > 0
                    ? Mathf.Clamp01(_unit.ShieldCurr / (float)_unit.HpMax)
                    : 0f;
                _shieldFill.fillAmount = shieldRatio;
                _shieldFill.enabled = shieldRatio > 0f;

                // ── 궁극기 충전 링 ──
                int resourceMax = _unit.UltimateResourceMax > 0 ? _unit.UltimateResourceMax : _unit.ManaMax;
                float ultRatio = resourceMax > 0 ? Mathf.Clamp01(_unit.ManaCurr / (float)resourceMax) : 0f;
                _ultimateRing.fillAmount = ultRatio;

                bool ultReserved = scheduler != null && scheduler.HasUltimateReserved(_unit);
                _ultimateRing.color = ultRatio >= 1f || ultReserved ? UITheme.ManaFull : UITheme.Mana;

                // ── Action Bar ──
                // 스케줄러가 도는 동안에만 의미가 있다. 아니면 비워 둔다.
                _actionFill.fillAmount = scheduler?.ActionProgress(_unit) ?? 0f;

                bool isActing = scheduler != null && ReferenceEquals(scheduler.ActingUnit, _unit);
                _actingGlow.enabled = isActing;

                // ── 고유 게이지 ──
                if (!string.IsNullOrEmpty(_uniqueResourceId))
                {
                    int current = _unit.GetCombatResource(_uniqueResourceId);
                    int maximum = _unit.GetCombatResourceMaximum(_uniqueResourceId);
                    _uniqueFill.fillAmount = maximum > 0 ? Mathf.Clamp01(current / (float)maximum) : 0f;
                    _uniqueLabel.text = maximum > 0 ? $"{_uniqueResourceId} {current}/{maximum}" : _uniqueResourceId;
                }

                RefreshModifiers();

                UIBuild.Group(_frame.gameObject).alpha = alive ? 1f : 0.38f;
            }

            /// <summary>걸린 상태를 색 점으로 표시한다. 이로운 것은 초록, 해로운 것은 붉은색.</summary>
            private void RefreshModifiers()
            {
                IReadOnlyList<UnitStatus> statuses = _unit.ActiveStatuses;
                for (int i = 0; i < _modifierDots.Length; i++)
                {
                    if (statuses != null && i < statuses.Count && statuses[i] != null)
                    {
                        _modifierDots[i].enabled = true;
                        _modifierDots[i].color = statuses[i].IsBeneficial ? UITheme.Positive : UITheme.Danger;
                    }
                    else
                    {
                        _modifierDots[i].enabled = false;
                    }
                }
            }
        }
    }
}
