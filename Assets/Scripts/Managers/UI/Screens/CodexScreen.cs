using System.Collections.Generic;
using System.Linq;
using Codes.Base;
using Entities;
using Helpers;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.Screens
{
    /// <summary>
    /// TAB으로 여는 캐릭터 시트 · 장비 창. 발더스 게이트 3의 파티 인벤토리를 따른다.
    ///
    /// <b>지금 파티의 상태만 다룬다.</b> 캐릭터·코드·장비·적의 전체 목록처럼 런과 무관한
    /// 참고 자료는 ESC 메뉴의 자료실(<see cref="WikiScreen"/>)이 맡는다. 예전엔 여기에
    /// 추천 조합 탭이 있어 "지금 내 파티"와 "게임 전체 도감"이 한 창에 섞여 있었다.
    ///
    /// <b>한 번에 한 명이 아니라 파티 전원을 나란히 편다.</b> 캐릭터마다 세로 칸이 하나씩이고,
    /// 그 안에 장착 인형 · 5스탯 · 보관함 격자 · 중량 막대가 들어간다. 장비는 칸에서 집어
    /// 다른 캐릭터의 칸이나 장착 슬롯 위에 놓으면 그대로 넘어간다.
    ///
    /// 예전에는 왼쪽에서 캐릭터를 고르고 가운데에 그 한 명만 폈다. 보상 장비가 누구에게
    /// 들어갔는지 찾으려면 다섯 번을 눌러 봐야 했다.
    ///
    /// 영웅이 생기기 전에 지급된 장비(공용 보관함)가 남아 있으면 맨 끝에 칸 하나로 붙는다.
    /// </summary>
    public class CodexScreen
    {
        private enum Tab
        {
            Equipment,
            Skills,
            Sheet,
        }

        // ── 치수(1920x1080 기준) ─────────────────────────────────────
        private const float PanelPad = 18f;
        private const float ColumnGap = 10f;

        /// <summary>칸 하나의 최대 폭. 인원이 적을 때 한 칸이 너무 넓어지지 않게 잡는다.</summary>
        private const float ColumnMaxWidth = 400f;

        private const float HeaderHeight = 58f;

        /// <summary>
        /// 경험치 막대와 장비 인형 사이의 여백. 헤더 바로 밑에 슬롯과 그림을 붙였더니
        /// 막대에 닿을 듯 답답하게 읽혔다.
        /// </summary>
        private const float DollTopGap = 16f;

        private const float DollHeight = 198f;
        private const float EquipSlot = 46f;
        private const float EquipGap = 4f;
        private const float StatStripHeight = 38f;
        private const float WeightBarHeight = 26f;
        private const int StoreColumns = 5;
        private const float StoreGap = 5f;

        /// <summary>장착 인형의 왼쪽 줄 · 오른쪽 줄 · 아래 무기 쌍.</summary>
        private static readonly (BaseClasses.EquipmentSlot Slot, string Label)[] LeftSlots =
        {
            (BaseClasses.EquipmentSlot.Head, "머리"),
            (BaseClasses.EquipmentSlot.Necklace, "목걸이"),
            (BaseClasses.EquipmentSlot.Armor, "갑옷"),
        };

        private static readonly (BaseClasses.EquipmentSlot Slot, string Label)[] RightSlots =
        {
            (BaseClasses.EquipmentSlot.Ring, "반지"),
            (BaseClasses.EquipmentSlot.Shoes, "신발"),
        };

        private static readonly (BaseClasses.EquipmentSlot Slot, string Label)[] WeaponSlots =
        {
            (BaseClasses.EquipmentSlot.MainHand, "주무기"),
            (BaseClasses.EquipmentSlot.OffHand, "보조"),
        };

        /// <summary>
        /// 아무것도 없는 칸의 테두리. <b>짙은 선</b>이라 채워진 칸과 한눈에 갈린다 — 안쪽은 칠하지 않고 비운다.
        /// 밝은 테마 위에서 15% 농도의 옅은 선이던 예전 빈 칸은 '꺼진 칸'인지 '빈 칸'인지 구분되지 않았다.
        /// </summary>
        private static readonly Color EmptySlotBorder = new(0.010f, 0.012f, 0.016f, 1f);

        private const int EmptySlotBorderWidth = 2;

        private static readonly BaseClasses.BaseEnums.PrimaryStat[] StatOrder =
        {
            BaseClasses.BaseEnums.PrimaryStat.STR,
            BaseClasses.BaseEnums.PrimaryStat.DEX,
            BaseClasses.BaseEnums.PrimaryStat.CON,
            BaseClasses.BaseEnums.PrimaryStat.INT,
            BaseClasses.BaseEnums.PrimaryStat.LUK,
        };

        private GameObject _rootObject;
        private RectTransform _root;
        private RectTransform _columnArea;
        private RectTransform _detailCard;
        private Button _equipmentTab;
        private Button _skillTab;
        private Button _sheetTab;

        private Tab _tab = Tab.Equipment;

        /// <summary>끌고 다니는 장비 하나. 어디서 집었는지까지 들고 있어야 되돌릴 수 있다.</summary>
        private sealed class ItemDrag
        {
            public ItemData Item;

            /// <summary>들고 있던 캐릭터. 공용 보관함에서 집었으면 null.</summary>
            public Unit From;

            public bool Equipped;
        }

        public bool IsOpen => _rootObject != null && _rootObject.activeSelf;

        public void Show(Unit focus = null)
        {
            EnsureBuilt();
            _rootObject.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            if (_rootObject != null) _rootObject.SetActive(false);
        }

        // ── 조립 ─────────────────────────────────────────────────────

        private void EnsureBuilt()
        {
            if (_rootObject != null) return;

            Canvas canvas = UIBuild.Canvas("CodexCanvas", UITheme.LayerCharacterSheet);
            _rootObject = new GameObject("CodexScreen", typeof(RectTransform));
            _rootObject.transform.SetParent(canvas.transform, false);
            _root = _rootObject.GetComponent<RectTransform>();
            UIBuild.Stretch(_root);

            Image backdrop = UIBuild.Backdrop("Backdrop", _rootObject.transform);
            UIBuild.OnClick(backdrop.gameObject, Hide);

            Image panel = UIBuild.Panel("Panel", _rootObject.transform, UITheme.Surface,
                UIShapes.Corner.Diagonal, 16, UITheme.Outline, 1);
            UIBuild.Anchor(panel.rectTransform, new Vector2(0.03f, 0.05f), new Vector2(0.97f, 0.95f));
            UIBuild.Elevate(panel, 40, 0.6f, 14f);
            _rootObject.AddComponent<UIPopIn>().Panel = panel.rectTransform;

            BuildHeader(panel.transform);

            _columnArea = UIBuild.Container("Columns", panel.transform);
            UIBuild.Anchor(_columnArea, new Vector2(0f, 0f), new Vector2(1f, 0.90f),
                PanelPad, PanelPad);

            BuildDetailCard();

            _rootObject.SetActive(false);
        }

        private void BuildHeader(Transform panel)
        {
            // 예전 사선 빗금 장식은 둥근 면으로 바꾸며 걷어 냈다(ModalScreen과 같은 이유).

            Image bar = UIBuild.Solid("HeaderBar", panel, UITheme.Accent);
            UIBuild.Pin(bar.rectTransform, new Vector2(0f, 1f), new Vector2(3f, 26f),
                new Vector2(PanelPad, -20f));

            TextMeshProUGUI caption = UIBuild.Label("Caption", panel, "CHARACTER SHEET",
                UITheme.FontMicro, UITheme.TextMuted);
            caption.characterSpacing = 20f;
            UIBuild.Pin(caption.rectTransform, new Vector2(0f, 1f), new Vector2(320f, 13f),
                new Vector2(PanelPad + 12f, -16f));

            TextMeshProUGUI title = UIBuild.Label("Title", panel, "캐릭터", UITheme.FontTitle,
                UITheme.TextPrimary);
            title.characterSpacing = 5f;
            UIBuild.Pin(title.rectTransform, new Vector2(0f, 1f), new Vector2(320f, 30f),
                new Vector2(PanelPad + 12f, -32f));

            BuildTabs(panel);

            Button close = UIBuild.Button("Close", panel, "×", Hide);
            UIBuild.Pin(close.image.rectTransform, new Vector2(1f, 1f), new Vector2(40f, 40f),
                new Vector2(-PanelPad, -PanelPad));

            Image rule = UIBuild.Divider("HeaderRule", panel);
            UIBuild.Anchor(rule.rectTransform, new Vector2(0f, 0.90f), new Vector2(1f, 0.90f),
                PanelPad, 0f);
            rule.rectTransform.sizeDelta = new Vector2(rule.rectTransform.sizeDelta.x, 1f);
        }

        private void BuildTabs(Transform panel)
        {
            _equipmentTab = MakeTab(panel, "장비", 0, () => SetTab(Tab.Equipment));
            _sheetTab = MakeTab(panel, "캐릭터 시트", 1, () => SetTab(Tab.Sheet));
            _skillTab = MakeTab(panel, "코드", 2, () => SetTab(Tab.Skills));
        }

        private static Button MakeTab(Transform panel, string text, int index, System.Action onClick)
        {
            Button button = UIBuild.Button($"Tab{index}", panel, text, onClick, false, UITheme.FontCaption);
            UIBuild.Pin(button.image.rectTransform, new Vector2(0.5f, 1f), new Vector2(150f, 34f),
                new Vector2(-157f + index * 157f, -18f));
            return button;
        }

        private void SetTab(Tab tab)
        {
            _tab = tab;
            Refresh();
        }

        // ── 갱신 ─────────────────────────────────────────────────────

        private void Refresh()
        {
            if (_rootObject == null) return;

            HideDetail();
            RefreshTabVisuals();
            UIBuild.Clear(_columnArea);

            List<Unit> allies = Allies();
            List<int> shared = SharedStorage();

            int columns = allies.Count + (shared.Count > 0 ? 1 : 0);
            if (columns == 0)
            {
                // 그냥 비워 두면 "화면이 깨졌다"와 구별이 안 된다. 왜 빈지 적어 준다.
                TextMeshProUGUI empty = UIBuild.Text("Empty", _columnArea,
                    "표시할 아군이 없다.\n캐릭터 창은 필드에 선 파티를 그린다 — " +
                    "편성 전이거나 전투 사이라면 비어 있다.",
                    UITheme.FontCaption, UITheme.TextMuted, TextAlignmentOptions.Center, true);
                UIBuild.Anchor(empty.rectTransform, new Vector2(0.1f, 0.4f), new Vector2(0.9f, 0.6f));
                return;
            }

            float available = UITheme.ReferenceResolution.x * (0.97f - 0.03f) - PanelPad * 2f;
            float width = Mathf.Min(ColumnMaxWidth,
                (available - (columns - 1) * ColumnGap) / columns);
            float total = width * columns + (columns - 1) * ColumnGap;
            float startX = (available - total) * 0.5f;

            for (int i = 0; i < allies.Count; i++)
            {
                BuildColumn(allies[i], startX + i * (width + ColumnGap), width);
            }

            if (shared.Count > 0)
            {
                BuildSharedColumn(shared, startX + allies.Count * (width + ColumnGap), width);
            }
        }

        private void RefreshTabVisuals()
        {
            Tint(_equipmentTab, _tab == Tab.Equipment);
            Tint(_skillTab, _tab == Tab.Skills);
            Tint(_sheetTab, _tab == Tab.Sheet);
        }

        private static void Tint(Button button, bool active)
        {
            if (button == null) return;
            button.image.sprite = UIShapes.CutCorner(8,
                active ? UITheme.Accent : UITheme.SurfaceRaised, UIShapes.Corner.Diagonal);
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.color = active ? UITheme.TextOnAccent : UITheme.TextSecondary;
        }

        // ── 캐릭터 칸 ────────────────────────────────────────────────

        private void BuildColumn(Unit unit, float x, float width)
        {
            Image column = UIBuild.Panel($"Col_{unit.UnitName}", _columnArea, UITheme.SurfaceSunken,
                UIShapes.Corner.Diagonal, 10, UITheme.Outline, 1);
            column.rectTransform.anchorMin = new Vector2(0f, 0f);
            column.rectTransform.anchorMax = new Vector2(0f, 1f);
            column.rectTransform.pivot = new Vector2(0f, 1f);
            column.rectTransform.sizeDelta = new Vector2(width, 0f);
            column.rectTransform.anchoredPosition = new Vector2(x, 0f);

            // 칸 어디에 놓아도 이 캐릭터가 받는다. 작은 슬롯을 정확히 겨냥하지 않아도 되게.
            MakeDropTarget(column, drag => drag.From != unit || drag.Equipped,
                drag => TransferTo(drag, unit), cut: 10, highlightFill: UITheme.SurfaceSunken);

            BuildColumnHeader(column.transform, unit, width);

            float y = HeaderHeight;
            if (_tab == Tab.Equipment)
            {
                y += DollTopGap;
                BuildDoll(column.transform, unit, width, y);
                y += DollHeight + 10f;

                BuildStatStrip(column.transform, unit, y);
                y += StatStripHeight + 10f;

                BuildStore(column.transform, unit, width, y);
                BuildWeightBar(column.transform, unit);
            }
            else if (_tab == Tab.Skills)
            {
                BuildCodeList(column.transform, unit, width, y);
            }
            else
            {
                BuildSheet(column.transform, unit, y);
            }
        }

        private void BuildColumnHeader(Transform column, Unit unit, float width)
        {
            TextMeshProUGUI name = UIBuild.Text("Name", column, unit.UnitName, UITheme.FontHeading,
                UITheme.TextPrimary, TextAlignmentOptions.Center);
            UIBuild.Pin(name.rectTransform, new Vector2(0f, 1f), new Vector2(width, 24f),
                new Vector2(0f, -10f));

            bool onBench = unit.currentCell == null || unit.currentCell.yPos <= 0;
            TextMeshProUGUI meta = UIBuild.Text("Meta", column,
                $"Lv.{unit.Level}{(onBench ? "   ·   대기석" : "")}",
                UITheme.FontMicro, UITheme.TextMuted, TextAlignmentOptions.Center);
            UIBuild.Pin(meta.rectTransform, new Vector2(0f, 1f), new Vector2(width, 14f),
                new Vector2(0f, -36f));

            // 경험치 막대. 발더스 게이트가 이름 밑에 두는 그 줄이다.
            Image track = UIBuild.Solid("ExpTrack", column, UITheme.Track);
            track.raycastTarget = false;
            UIBuild.Pin(track.rectTransform, new Vector2(0f, 1f), new Vector2(width - 24f, 3f),
                new Vector2(12f, -52f));

            float ratio = unit.ExpToNextLevel > 0
                ? Mathf.Clamp01(unit.Exp / (float)unit.ExpToNextLevel)
                : 0f;
            Image fill = UIBuild.Solid("ExpFill", track.transform, UITheme.Accent);
            fill.raycastTarget = false;
            UIBuild.Anchor(fill.rectTransform, new Vector2(0f, 0f), new Vector2(ratio, 1f));
        }

        /// <summary>장착 인형. 왼쪽 세 칸 · 오른쪽 두 칸 · 아래 무기 쌍, 가운데는 초상화.</summary>
        private void BuildDoll(Transform column, Unit unit, float width, float top)
        {
            var bySlot = new Dictionary<BaseClasses.EquipmentSlot, ItemData>();
            foreach (ItemData item in unit.EquippedItems)
            {
                if (item != null &&
                    BaseClasses.EquipmentLoadout.TryParseSlot(item.slot, out BaseClasses.EquipmentSlot slot))
                {
                    bySlot[slot] = item;
                }
            }

            for (int i = 0; i < LeftSlots.Length; i++)
            {
                (BaseClasses.EquipmentSlot slot, string label) = LeftSlots[i];
                bySlot.TryGetValue(slot, out ItemData item);
                CreateSlot(column, unit, item, 10f, top + i * (EquipSlot + EquipGap), EquipSlot,
                    label, true, slot);
            }

            for (int i = 0; i < RightSlots.Length; i++)
            {
                (BaseClasses.EquipmentSlot slot, string label) = RightSlots[i];
                bySlot.TryGetValue(slot, out ItemData item);
                CreateSlot(column, unit, item, width - EquipSlot - 10f, top + i * (EquipSlot + EquipGap),
                    EquipSlot, label, true, slot);
            }

            // 초상화. 슬롯 두 줄 사이의 남은 자리를 채운다.
            // 높이를 왼쪽 슬롯 줄 전체와 같게 잡아, 그림이 두 줄의 한가운데에 서게 한다.
            float portraitLeft = 10f + EquipSlot + 8f;
            float portraitWidth = width - portraitLeft * 2f;
            Sprite portrait = SpriteResource.LoadPortrait(unit.PortraitPath);
            float stackHeight = LeftSlots.Length * EquipSlot + (LeftSlots.Length - 1) * EquipGap;
            var portraitSize = new Vector2(portraitWidth, stackHeight);
            var portraitAt = new Vector2(portraitLeft, -top);

            if (portraitWidth > 20f && portrait != null)
            {
                var go = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(column, false);
                var image = go.GetComponent<Image>();
                image.sprite = portrait;
                image.preserveAspect = true;
                image.raycastTarget = false;
                UIBuild.Pin(image.rectTransform, new Vector2(0f, 1f), portraitSize, portraitAt);
            }
            else if (portraitWidth > 20f)
            {
                // 초상화가 없으면 조용히 비우지 않는다. 어떤 키를 못 찾았는지 그 자리에 적어야
                // "왜 안 보이지"가 곧바로 "이 키가 없다"로 좁혀진다.
                Image frame = UIBuild.Panel("PortraitMissing", column, UITheme.SurfaceSunken,
                    UIShapes.Corner.Diagonal, 6, UITheme.Danger, 1);
                UIBuild.Pin(frame.rectTransform, new Vector2(0f, 1f), portraitSize, portraitAt);

                string key = string.IsNullOrWhiteSpace(unit.PortraitPath) ? "(경로 없음)" : unit.PortraitPath;
                TextMeshProUGUI note = UIBuild.Text("Key", frame.transform, $"초상화 없음\n{key}",
                    UITheme.FontMicro, UITheme.Danger, TextAlignmentOptions.Center, true);
                UIBuild.Stretch(note.rectTransform, 4f, 4f);
                Debug.LogWarning($"[캐릭터 창] {unit.UnitName} 초상화를 찾지 못했다: {key}");
            }

            // 무기 쌍은 인형 아래 가운데. 발더스 게이트의 '근접' 칸 자리다.
            float weaponsWidth = EquipSlot * 2f + EquipGap;
            float weaponsX = (width - weaponsWidth) * 0.5f;
            float weaponsY = top + DollHeight - EquipSlot;
            // 양손 무기(둔기·장창·대검 등)를 들면 보조 슬롯은 쓸 수 없다. 빈 칸(검정)과 달리
            // 회색으로 꺼 두어 "비었다"가 아니라 "막혔다"로 읽히게 하고, 무엇도 놓이지 않게 한다.
            bool twoHandedMain = bySlot.TryGetValue(BaseClasses.EquipmentSlot.MainHand, out ItemData mainHand)
                                 && mainHand.twoHanded;
            for (int i = 0; i < WeaponSlots.Length; i++)
            {
                (BaseClasses.EquipmentSlot slot, string label) = WeaponSlots[i];
                bySlot.TryGetValue(slot, out ItemData item);
                bool blocked = slot == BaseClasses.EquipmentSlot.OffHand && twoHandedMain;
                CreateSlot(column, unit, item, weaponsX + i * (EquipSlot + EquipGap), weaponsY,
                    EquipSlot, blocked ? "양손" : label, true, slot, blocked);
            }
        }

        private void BuildStatStrip(Transform column, Unit unit, float top)
        {
            Image strip = UIBuild.Panel("Stats", column, UITheme.Surface, UIShapes.Corner.None, 4);
            strip.raycastTarget = false;
            strip.rectTransform.anchorMin = new Vector2(0f, 1f);
            strip.rectTransform.anchorMax = new Vector2(1f, 1f);
            strip.rectTransform.pivot = new Vector2(0.5f, 1f);
            strip.rectTransform.sizeDelta = new Vector2(-20f, StatStripHeight);
            strip.rectTransform.anchoredPosition = new Vector2(0f, -top);

            float step = 1f / StatOrder.Length;
            for (int i = 0; i < StatOrder.Length; i++)
            {
                BaseClasses.BaseEnums.PrimaryStat stat = StatOrder[i];
                bool isMain = unit.MainPrimaryStat == stat;

                var cell = UIBuild.Container($"Stat{stat}", strip.transform);
                UIBuild.Anchor(cell, new Vector2(i * step, 0f), new Vector2((i + 1) * step, 1f));

                TextMeshProUGUI key = UIBuild.Text("Key", cell, stat + (isMain ? " ★" : ""),
                    UITheme.FontMicro, isMain ? UITheme.Accent : UITheme.TextMuted,
                    TextAlignmentOptions.Center);
                UIBuild.Anchor(key.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 1f));

                TextMeshProUGUI value = UIBuild.Text("Value", cell,
                    unit.GetBasePrimaryStat(stat).ToString(), UITheme.FontBody,
                    isMain ? UITheme.Accent : UITheme.TextPrimary, TextAlignmentOptions.Center);
                UIBuild.Anchor(value.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.5f));
            }
        }

        private void BuildStore(Transform column, Unit unit, float width, float top)
        {
            ItemDataList itemData = GameManager.Instance?.itemDataList;

            var items = new List<ItemData>();
            foreach (int itemId in unit.CarriedItemIds)
            {
                ItemData item = itemData?.items?.FirstOrDefault(entry => entry.id == itemId);
                if (item != null) items.Add(item);
            }

            TextMeshProUGUI caption = UIBuild.Label("StoreCaption", column,
                $"보관함  {items.Count}", UITheme.FontMicro, UITheme.TextMuted);
            UIBuild.Pin(caption.rectTransform, new Vector2(0f, 1f), new Vector2(width - 24f, 13f),
                new Vector2(12f, -top));

            float slot = Mathf.Clamp((width - 24f - (StoreColumns - 1) * StoreGap) / StoreColumns, 34f, 62f);
            float gridWidth = slot * StoreColumns + StoreGap * (StoreColumns - 1);
            float gridX = (width - gridWidth) * 0.5f;
            float gridTop = top + 18f;

            // 중량 막대에 닿기 전까지 남은 자리를 <b>끝까지</b> 빈 칸으로 채운다.
            // 발더스 게이트처럼 가방이 칸 아래를 다 쓰고 있어야, 새로 얻은 장비가
            // 어디로 들어오는지가 보이고 칸 안에 빈 공백이 남지 않는다.
            float columnHeight = UITheme.ReferenceResolution.y * (0.95f - 0.05f) * 0.90f - PanelPad * 2f;
            float room = columnHeight - gridTop - WeightBarHeight - 16f;
            int rows = Mathf.Max(1, Mathf.FloorToInt((room + StoreGap) / (slot + StoreGap)));

            for (int index = 0; index < rows * StoreColumns; index++)
            {
                int row = index / StoreColumns;
                int col = index % StoreColumns;
                ItemData item = index < items.Count ? items[index] : null;

                CreateSlot(column, unit, item,
                    gridX + col * (slot + StoreGap),
                    gridTop + row * (slot + StoreGap),
                    slot, null, false, null);
            }
        }

        private void BuildWeightBar(Transform column, Unit unit)
        {
            var row = UIBuild.Container("Weight", column);
            row.anchorMin = new Vector2(0f, 0f);
            row.anchorMax = new Vector2(1f, 0f);
            row.pivot = new Vector2(0.5f, 0f);
            row.sizeDelta = new Vector2(-20f, WeightBarHeight);
            row.anchoredPosition = new Vector2(0f, 8f);

            Image track = UIBuild.Solid("Track", row, UITheme.Track);
            track.raycastTarget = false;
            UIBuild.Anchor(track.rectTransform, new Vector2(0f, 0.62f), new Vector2(1f, 1f));

            float ratio = unit.CarryWeightMax > 0
                ? Mathf.Clamp01(unit.CarryWeightCurrent / (float)unit.CarryWeightMax)
                : 0f;
            Image fill = UIBuild.Solid("Fill", track.transform,
                unit.IsOverCarryWeightMax ? UITheme.Danger
                : unit.EncumbranceTier > 0 ? new Color(0.878f, 0.647f, 0.290f)
                : UITheme.Positive);
            fill.raycastTarget = false;
            UIBuild.Anchor(fill.rectTransform, new Vector2(0f, 0f), new Vector2(ratio, 1f));

            TextMeshProUGUI label = UIBuild.Text("Label", row,
                $"중량  {unit.CarryWeightCurrent} / {unit.CarryWeightMax}", UITheme.FontMicro,
                unit.IsOverCarryWeightMax ? UITheme.Danger : UITheme.TextMuted,
                TextAlignmentOptions.Center);
            UIBuild.Anchor(label.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.60f));
        }

        // ── 공용 보관함 칸 ───────────────────────────────────────────

        private void BuildSharedColumn(List<int> itemIds, float x, float width)
        {
            Image column = UIBuild.Panel("Col_Shared", _columnArea, UITheme.SurfaceSunken,
                UIShapes.Corner.Diagonal, 10, UITheme.Outline, 1);
            column.rectTransform.anchorMin = new Vector2(0f, 0f);
            column.rectTransform.anchorMax = new Vector2(0f, 1f);
            column.rectTransform.pivot = new Vector2(0f, 1f);
            column.rectTransform.sizeDelta = new Vector2(width, 0f);
            column.rectTransform.anchoredPosition = new Vector2(x, 0f);

            TextMeshProUGUI name = UIBuild.Text("Name", column.transform, "공용 보관함",
                UITheme.FontHeading, UITheme.TextSecondary, TextAlignmentOptions.Center);
            UIBuild.Pin(name.rectTransform, new Vector2(0f, 1f), new Vector2(width, 24f),
                new Vector2(0f, -10f));

            TextMeshProUGUI meta = UIBuild.Text("Meta", column.transform,
                "영웅이 생기기 전에 받은 장비", UITheme.FontMicro, UITheme.TextMuted,
                TextAlignmentOptions.Center);
            UIBuild.Pin(meta.rectTransform, new Vector2(0f, 1f), new Vector2(width, 14f),
                new Vector2(0f, -36f));

            ItemDataList itemData = GameManager.Instance?.itemDataList;
            float slot = Mathf.Clamp((width - 24f - (StoreColumns - 1) * StoreGap) / StoreColumns, 34f, 62f);
            float gridWidth = slot * StoreColumns + StoreGap * (StoreColumns - 1);
            float gridX = (width - gridWidth) * 0.5f;

            for (int index = 0; index < itemIds.Count; index++)
            {
                ItemData item = itemData?.items?.FirstOrDefault(entry => entry.id == itemIds[index]);
                if (item == null) continue;

                int row = index / StoreColumns;
                int col = index % StoreColumns;
                CreateSlot(column.transform, null, item,
                    gridX + col * (slot + StoreGap),
                    HeaderHeight + row * (slot + StoreGap),
                    slot, null, false, null);
            }
        }

        // ── 칸 하나 ──────────────────────────────────────────────────

        /// <summary>
        /// 슬롯 하나. <paramref name="item"/>이 null이면 빈 칸이다.
        /// <paramref name="equipSlot"/>이 있으면 장착 슬롯이라 그 부위만 받는다.
        /// <paramref name="blocked"/>는 양손 무기에 막힌 보조 슬롯이다 — 회색으로 꺼지고 아무것도 받지 않는다.
        /// </summary>
        private void CreateSlot(Transform column, Unit owner, ItemData item, float x, float y, float size,
            string emptyLabel, bool isEquipped, BaseClasses.EquipmentSlot? equipSlot, bool blocked = false)
        {
            bool filled = item != null;

            // 빈 칸은 안쪽이 투명하고 짙은 테두리만 둘렀다. 막힌 칸은 예전 그대로의 회색 면,
            // 채워진 칸은 흰 면이다.
            bool empty = !filled && !blocked;
            Color fill = filled ? UITheme.SurfaceRaised : blocked ? UITheme.SurfaceSunken : Color.clear;
            Color border = empty ? EmptySlotBorder
                : !filled ? UITheme.Outline
                : isEquipped ? UITheme.Accent
                : UITheme.Rarity(item.rarity);
            int borderWidth = empty ? EmptySlotBorderWidth : filled && isEquipped ? 2 : 1;

            Image slot = UIBuild.Panel(filled ? $"Slot_{item.name}" : "Slot", column, fill,
                UIShapes.Corner.Diagonal, 5, border, borderWidth);
            UIBuild.Pin(slot.rectTransform, new Vector2(0f, 1f), new Vector2(size, size),
                new Vector2(x, -y));

            if (equipSlot.HasValue && owner != null && !blocked)
            {
                BaseClasses.EquipmentSlot target = equipSlot.Value;
                MakeDropTarget(slot,
                    drag => FitsSlot(drag.Item, target) && !(drag.Equipped && drag.From == owner),
                    drag => EquipFromDrag(drag, owner), cut: 5, highlightFill: fill);
            }
            else if (blocked)
            {
                // 받는 자리가 아니다. 드롭 대상을 아예 달지 않으니 강조도 켜지지 않는다.
            }
            else if (owner != null)
            {
                MakeDropTarget(slot,
                    drag => drag.From != owner || drag.Equipped,
                    drag => TransferTo(drag, owner), cut: 5);
            }

            if (!filled)
            {
                if (!string.IsNullOrEmpty(emptyLabel))
                {
                    TextMeshProUGUI hint = UIBuild.Text("Hint", slot.transform, emptyLabel,
                        UITheme.FontMicro, UITheme.TextDisabled, TextAlignmentOptions.Center);
                    UIBuild.Stretch(hint.rectTransform, 2f, 2f);
                }

                return;
            }

            Sprite itemSprite = ItemTooltip.LoadArt(item);
            if (itemSprite != null)
            {
                var artObject = new GameObject("ItemArt", typeof(RectTransform), typeof(Image));
                artObject.transform.SetParent(slot.transform, false);
                Image art = artObject.GetComponent<Image>();
                art.sprite = itemSprite;
                art.preserveAspect = true;
                art.raycastTarget = false;
                art.color = new Color(1f, 1f, 1f, 0.9f);
                UIBuild.Stretch(art.rectTransform, 5f, 5f);
            }

            TextMeshProUGUI label = UIBuild.Text("Name", slot.transform, item.name,
                UITheme.FontMicro, UITheme.TextPrimary, TextAlignmentOptions.Center, wrap: true);
            UIBuild.Stretch(label.rectTransform, 3f, 10f);
            label.overflowMode = TextOverflowModes.Ellipsis;
            if (itemSprite != null)
            {
                label.alignment = TextAlignmentOptions.Bottom;
                label.outlineColor = new Color32(10, 12, 15, 230);
                label.outlineWidth = 0.22f;
            }

            TextMeshProUGUI weight = UIBuild.Text("Weight", slot.transform, $"{item.weight}",
                UITheme.FontMicro, UITheme.TextMuted, TextAlignmentOptions.BottomRight);
            UIBuild.Pin(weight.rectTransform, new Vector2(1f, 0f), new Vector2(18f, 12f),
                new Vector2(-3f, 2f));

            // 슬롯에는 그림과 이름만 남긴다. 제원은 마우스를 올렸을 때 툴팁으로 보인다.
            // 누르면 뜨는 상세 카드는 장착·해제 버튼을 들고 있어 툴팁과 역할이 다르다.
            ItemTooltip.Attach(slot.gameObject, item, owner);

            RectTransform slotRect = slot.rectTransform;
            UIBuild.OnClick(slot.gameObject, () => ShowItemDetail(item, owner, isEquipped, slotRect));

            UIDragSource source = slot.gameObject.GetComponent<UIDragSource>();
            if (source == null) source = slot.gameObject.AddComponent<UIDragSource>();
            source.Payload = new ItemDrag { Item = item, From = owner, Equipped = isEquipped };
            source.Label = item.name;
            source.Icon = itemSprite;
            source.Tint = UITheme.Rarity(item.rarity);
        }

        /// <summary>
        /// 놓을 자리 하나.
        ///
        /// <paramref name="highlightFill"/>을 준 자리만 끌기 중에 앰버 테두리가 켜진다.
        /// 보관함 빈 칸까지 전부 켜면 화면이 통째로 앰버가 되어 아무것도 가리키지 못한다.
        /// 그래서 <b>캐릭터 칸과 부위가 맞는 장착 슬롯만</b> 켜고, 보관함 칸은 조용히 받는다.
        /// 켤 때도 면을 칠하지 않고 테두리만 바꾼다.
        /// </summary>
        private void MakeDropTarget(Image frame, System.Func<ItemDrag, bool> accepts,
            System.Action<ItemDrag> onDrop, int cut, Color? highlightFill = null)
        {
            UIDropTarget target = frame.gameObject.GetComponent<UIDropTarget>();
            if (target == null) target = frame.gameObject.AddComponent<UIDropTarget>();

            target.IdleSprite = frame.sprite;
            target.HighlightSprite = highlightFill.HasValue
                ? UIShapes.CutCorner(cut, highlightFill.Value, UIShapes.Corner.Diagonal, UITheme.Accent, 2)
                : null;
            target.CanAccept = payload => payload is ItemDrag drag && drag.Item != null && accepts(drag);
            target.Accept = payload =>
            {
                if (payload is ItemDrag drag) onDrop(drag);
            };
        }

        // ── 코드 / 시트 탭 ───────────────────────────────────────────

        /// <summary>
        /// 코드 목록. 이름 아래에 <b>무엇을 하는 코드인가</b>를 한두 줄로 붙인다.
        /// 이름만 늘어놓으면 '질량 보존'이 무엇인지 알려고 자료를 뒤져야 했다.
        ///
        /// 설명은 `20_codes.yaml`에서 온다(<see cref="CodeCatalog"/>). 두 줄을 넘으면 말줄임하고,
        /// 마우스를 올리면 전문과 꼬리표(고유·전수 불가·피해 태그)가 툴팁으로 뜬다.
        ///
        /// <b>등급은 이름 색으로 보인다</b> — 일반은 은색, 강화는 금색, 고유는 보라색이다.
        /// 강화 등급에 대체되어 발동하지 않는 코드는 흐리게 깔고 이유를 붙인다.
        /// 코드가 많은 캐릭터는 칸 높이를 넘으므로 칸 안에서 굴린다.
        /// </summary>
        private void BuildCodeList(Transform column, Unit unit, float width, float top)
        {
            var entries = new List<(string Kind, Code Code, bool Superseded)>();

            foreach (PassiveCode passive in unit.ActivePassiveCodes)
            {
                if (passive is { IsUniquePassive: true }) entries.Add(("고유", passive, false));
            }

            if (unit.ActiveNormalCode != null) entries.Add(("일반행동", unit.ActiveNormalCode, false));
            if (unit.ActiveSpecialCode != null) entries.Add(("특수행동", unit.ActiveSpecialCode, false));
            if (unit.ActiveUltimateCode != null) entries.Add(("궁극기", unit.ActiveUltimateCode, false));

            foreach (PassiveCode passive in unit.ActivePassiveCodes)
            {
                if (passive == null || passive.IsUniquePassive) continue;
                entries.Add(("패시브", passive, IsSuperseded(passive, unit)));
            }

            foreach (PassiveCode passive in unit.ActiveItemPassiveCodes)
            {
                if (passive != null) entries.Add(("장비", passive, IsSuperseded(passive, unit)));
            }

            RectTransform content = UIBuild.ScrollArea("CodeScroll", column, out ScrollRect scroll);
            var viewport = (RectTransform)scroll.transform;
            viewport.anchorMin = new Vector2(0f, 0f);
            viewport.anchorMax = new Vector2(1f, 1f);
            viewport.offsetMin = new Vector2(0f, 8f);
            viewport.offsetMax = new Vector2(0f, -top);

            float y = 0f;
            AddListHeading(content, $"코드 {unit.LearnedCodeCount}개", ref y);
            foreach ((string kind, Code code, bool superseded) in entries)
            {
                AddCodeEntry(content, kind, code, superseded, width, ref y);
            }

            if (entries.Count == 0) AddListRow(content, "—", "없음", ref y);
            content.sizeDelta = new Vector2(0f, y + 8f);
        }

        private static bool IsSuperseded(PassiveCode passive, Unit unit) =>
            passive.SupersededByCodeId > 0 && unit.HasLearnedPassiveCode(passive.SupersededByCodeId);

        private static Color GradeColor(Code code) => code?.Grade switch
        {
            BaseClasses.BaseEnums.CodeGrade.Enhanced => UITheme.CodeEnhanced,
            BaseClasses.BaseEnums.CodeGrade.Unique => UITheme.CodeUnique,
            _ => UITheme.CodeNormal,
        };

        /// <summary>
        /// 코드 한 항목: 윗줄에 이름과 종류, 아랫줄에 한 줄 소개.
        /// 항목 전체가 툴팁 영역이라 이름이 아닌 설명 위에 올려도 전문이 뜬다.
        /// </summary>
        private static void AddCodeEntry(Transform parent, string kind, Code code, bool superseded,
            float columnWidth, ref float y)
        {
            CodeCatalog.Entry entry = CodeCatalog.Find(code);
            string name = !string.IsNullOrWhiteSpace(code.CodeName) ? code.CodeName
                : entry?.verbalName ?? "이름 없음";
            string description = entry?.description;
            string summary = CodeCatalog.Summary(description);
            if (summary.Length == 0)
            {
                // 설명이 비면 표에서 빠진 것이다. 조용히 비우지 않고 무엇이 빠졌는지 적는다.
                summary = $"설명 없음 — 20_codes.yaml {CodeCatalog.SlotOf(code)} #{code.CatalogId}";
            }

            Color tint = superseded ? UITheme.TextMuted : GradeColor(code);
            float entryTop = y;
            float inner = Mathf.Max(80f, columnWidth - 32f);

            var head = UIBuild.Container("Code", parent);
            head.anchorMin = new Vector2(0f, 1f);
            head.anchorMax = new Vector2(1f, 1f);
            head.pivot = new Vector2(0.5f, 1f);
            head.sizeDelta = new Vector2(-24f, 26f);
            head.anchoredPosition = new Vector2(0f, -y);

            TextMeshProUGUI nameLabel = UIBuild.Text("Name", head, superseded ? $"{name} (대체됨)" : name,
                UITheme.FontBody, tint);
            nameLabel.overflowMode = TextOverflowModes.Ellipsis;
            UIBuild.Anchor(nameLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0.72f, 1f), 4f, 0f);

            // 일반행동은 이름도 "일반행동"이라 종류를 한 번 더 적으면 같은 말이 두 번 나온다.
            TextMeshProUGUI kindLabel = UIBuild.Text("Kind", head, name == kind ? "" : kind, UITheme.FontMicro, UITheme.TextMuted,
                TextAlignmentOptions.MidlineRight);
            UIBuild.Anchor(kindLabel.rectTransform, new Vector2(0.72f, 0f), new Vector2(1f, 1f), 4f, 0f);
            y += 26f;

            // 예전에는 두 줄에서 말줄임했다. 칸 아래가 텅 비어 있는데 설명은 '...'으로 잘린다는 QA가 있었다.
            // 목록은 칸 안에서 굴러가므로 자리를 아낄 이유가 없다 — 전문을 다 싣는다.
            TextMeshProUGUI line = UIBuild.Text("Summary", parent, summary, UITheme.FontCaption,
                superseded ? UITheme.TextMuted : UITheme.TextSecondary, TextAlignmentOptions.TopLeft, true);
            line.richText = false;
            line.overflowMode = TextOverflowModes.Overflow;
            line.rectTransform.anchorMin = new Vector2(0f, 1f);
            line.rectTransform.anchorMax = new Vector2(1f, 1f);
            line.rectTransform.pivot = new Vector2(0.5f, 1f);

            // 높이는 실제 칸 폭으로 재야 한다. 파티가 다섯이면 칸이 좁아져 줄 수가 늘어난다.
            float height = Mathf.Max(16f, line.GetPreferredValues(summary, inner, 0f).y);
            line.rectTransform.sizeDelta = new Vector2(-32f, height);
            line.rectTransform.anchoredPosition = new Vector2(0f, -y);
            y += height + 8f;

            // 항목 전체를 덮는 투명 판. 툴팁은 여기서 받는다 — 휠은 막지 않으므로 목록도 굴러간다.
            Image hover = UIBuild.Solid("Hover", parent, new Color(1f, 1f, 1f, 0f));
            hover.rectTransform.anchorMin = new Vector2(0f, 1f);
            hover.rectTransform.anchorMax = new Vector2(1f, 1f);
            hover.rectTransform.pivot = new Vector2(0.5f, 1f);
            hover.rectTransform.sizeDelta = new Vector2(-12f, y - entryTop - 4f);
            hover.rectTransform.anchoredPosition = new Vector2(0f, -entryTop);

            TooltipTrigger.Attach(hover.gameObject, () => name,
                () => CodeTooltipLines(kind, code, description, superseded), tint);
        }

        private static IReadOnlyList<UITooltip.Line> CodeTooltipLines(string kind, Code code,
            string description, bool superseded)
        {
            var lines = new List<UITooltip.Line>
            {
                new("종류", kind, UITheme.TextSecondary),
                new("등급", GradeName(code), GradeColor(code)),
            };

            string summary = CodeCatalog.Summary(description);
            if (summary.Length > 0) lines.Add(UITooltip.Line.Note(summary, UITheme.TextPrimary));

            List<string> markers = CodeCatalog.Markers(description);
            if (markers.Count > 0) lines.Add(UITooltip.Line.Note(string.Join(" · ", markers), UITheme.TextMuted));

            if (superseded)
            {
                lines.Add(UITooltip.Line.Note("상위 등급 코드를 배워 발동하지 않는다.", UITheme.Danger));
            }

            return lines;
        }

        private static string GradeName(Code code) => code?.Grade switch
        {
            BaseClasses.BaseEnums.CodeGrade.Enhanced => "강화 (금색)",
            BaseClasses.BaseEnums.CodeGrade.Unique => "고유 (보라)",
            _ => "일반 (은색)",
        };

        private void BuildSheet(Transform column, Unit unit, float top)
        {
            float y = top;

            AddListHeading(column, "기초 스탯", ref y);
            foreach (BaseClasses.BaseEnums.PrimaryStat stat in StatOrder)
            {
                AddListRow(column, stat.ToString(), $"{unit.GetBasePrimaryStat(stat)}", ref y);
            }

            y += 8f;
            AddListHeading(column, "상세", ref y);
            int shownMax = unit.ProjectedHpMax;
            int shownCurr = shownMax == unit.HpMax || unit.HpMax <= 0
                ? unit.HpCurr
                : Mathf.RoundToInt(unit.HpCurr * (shownMax / (float)unit.HpMax));
            AddListRow(column, "체력", shownMax == unit.HpMax
                ? $"{shownCurr} / {shownMax}"
                : $"{shownCurr} / {shownMax} (전투 시)", ref y);
            AddListRow(column, "방어력", $"{unit.DefCurr}", ref y);
            AddListRow(column, "내구", $"{unit.DurabilityCurr}", ref y);
            AddListRow(column, "위력 100 피해", $"{unit.SkillDamage(100)}", ref y);
            AddListRow(column, "치명타", $"{unit.CritChanceCurr * 100f:0.#}%  ×{unit.CritMultiplierCurr:0.##}", ref y);
            AddListRow(column, "행동 속도", $"×{unit.ActionSpeedCurr:0.##}", ref y);
            AddListRow(column, "마나 효율", $"×{unit.ManaEfficiencyCurr:0.##}", ref y);

            y += 8f;
            AddListHeading(column, "육성", ref y);
            AddListRow(column, "육성 Lv", $"{unit.TrainingLevel}", ref y);
            AddListRow(column, "중량", $"{unit.CarryWeightCurrent} / {unit.CarryWeightMax}", ref y);
            AddListRow(column, "코드", $"{unit.LearnedCodeCount}개", ref y);

            BuildStatusList(column, unit, ref y);
        }

        /// <summary>
        /// 지금 걸려 있는 상태. 카드 위 아이콘과 <b>같은 그림</b>을 이름과 나란히 놓는다.
        /// 전투 중에는 아이콘만 보이므로, 그 그림이 무엇인지 배우는 자리가 여기다.
        /// </summary>
        private static void BuildStatusList(Transform column, Unit unit, ref float y)
        {
            IReadOnlyList<Entities.Status.UnitStatus> statuses = unit.ActiveStatuses;
            if (statuses == null || statuses.Count == 0) return;

            y += 8f;
            AddListHeading(column, "상태", ref y);

            foreach (Entities.Status.UnitStatus status in statuses)
            {
                if (status == null) continue;

                var row = UIBuild.Container("Status", column);
                row.anchorMin = new Vector2(0f, 1f);
                row.anchorMax = new Vector2(1f, 1f);
                row.pivot = new Vector2(0.5f, 1f);
                row.sizeDelta = new Vector2(-24f, 19f);
                row.anchoredPosition = new Vector2(0f, -y);

                var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconObject.transform.SetParent(row, false);
                var icon = iconObject.GetComponent<Image>();
                icon.sprite = StatusIcons.For(status);
                icon.color = StatusIcons.Tint(status);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                UIBuild.Pin(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(15f, 15f),
                    new Vector2(4f, 0f));

                TextMeshProUGUI name = UIBuild.Text("Name", row, status.StatusName,
                    UITheme.FontMicro, UITheme.TextSecondary);
                UIBuild.Anchor(name.rectTransform, new Vector2(0f, 0f), new Vector2(0.72f, 1f), 24f, 0f);

                TextMeshProUGUI turns = UIBuild.Text("Turns", row,
                    status.Duration > 0 ? $"{status.RemainingTurns}턴" : "지속",
                    UITheme.FontMicro, UITheme.TextMuted, TextAlignmentOptions.MidlineRight);
                UIBuild.Anchor(turns.rectTransform, new Vector2(0.72f, 0f), new Vector2(1f, 1f), 4f, 0f);

                y += 20f;
            }
        }

        private static void AddListHeading(Transform column, string text, ref float y)
        {
            TextMeshProUGUI label = UIBuild.Text("Heading", column, text, UITheme.FontCaption,
                UITheme.Accent);
            label.rectTransform.anchorMin = new Vector2(0f, 1f);
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.pivot = new Vector2(0.5f, 1f);
            label.rectTransform.sizeDelta = new Vector2(-24f, 20f);
            label.rectTransform.anchoredPosition = new Vector2(0f, -y);
            y += 22f;
        }

        private static void AddListRow(Transform column, string left, string right, ref float y,
            Color? rightTint = null)
        {
            var row = UIBuild.Container("Row", column);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.sizeDelta = new Vector2(-24f, 19f);
            row.anchoredPosition = new Vector2(0f, -y);

            TextMeshProUGUI l = UIBuild.Text("L", row, left, UITheme.FontMicro, UITheme.TextSecondary);
            UIBuild.Anchor(l.rectTransform, new Vector2(0f, 0f), new Vector2(0.55f, 1f), 4f, 0f);

            TextMeshProUGUI r = UIBuild.Text("R", row, right, UITheme.FontMicro,
                rightTint ?? UITheme.TextPrimary, TextAlignmentOptions.MidlineRight);
            UIBuild.Anchor(r.rectTransform, new Vector2(0.55f, 0f), new Vector2(1f, 1f), 4f, 0f);
            r.overflowMode = TextOverflowModes.Ellipsis;

            y += 20f;
        }

        // ── 아이템 상세(떠 있는 카드) ────────────────────────────────

        private void BuildDetailCard()
        {
            Image card = UIBuild.Panel("ItemDetail", _rootObject.transform, UITheme.SurfaceRaised,
                UIShapes.Corner.Diagonal, 10, UITheme.Accent, 1);
            _detailCard = card.rectTransform;
            _detailCard.anchorMin = new Vector2(0.5f, 0.5f);
            _detailCard.anchorMax = new Vector2(0.5f, 0.5f);
            _detailCard.pivot = new Vector2(0f, 1f);
            _detailCard.sizeDelta = new Vector2(280f, 240f);
            // 바깥을 누르면 닫힌다. 예전에는 탭을 옮기거나 창을 닫아야만 사라졌다.
            card.gameObject.AddComponent<UIDismissOnOutsideClick>();
            _detailCard.gameObject.SetActive(false);
        }

        private void HideDetail()
        {
            if (_detailCard != null) _detailCard.gameObject.SetActive(false);
        }

        /// <summary>
        /// 누른 칸 옆에 상세를 띄운다. 오른쪽 전용 패널을 두면 캐릭터 칸이 그만큼 좁아지므로,
        /// 필요할 때만 나타나는 떠 있는 카드로 둔다.
        /// </summary>
        private void ShowItemDetail(ItemData item, Unit owner, bool isEquipped, RectTransform slotRect)
        {
            if (_detailCard == null || item == null) return;

            // 호버 툴팁이 상세 카드 위에 겹쳐 [장착] 글자를 가렸다. 카드가 뜨면 툴팁은 접는다.
            UITooltip.HideAny();
            UIBuild.Clear(_detailCard);
            _detailCard.gameObject.SetActive(true);
            _detailCard.SetAsLastSibling();

            float y = 12f;
            AddDetailTitle(item.name, ref y, UITheme.Rarity(item.rarity));
            AddDetailLine($"{ItemTooltip.SlotName(item.slot)} · {ItemTooltip.CategoryName(item.category)} · 중량 {item.weight}",
                ref y, UITheme.TextSecondary);

            if (owner != null && !owner.CanUseEquipmentEffects(item))
            {
                AddDetailLine("숙련 없음 — 중량만 적용", ref y, UITheme.Danger);
            }

            if (item.statBonuses != null && item.statBonuses.Count > 0)
            {
                y += 6f;
                AddDetailLine("스탯 보정", ref y, UITheme.Accent);
                foreach (var bonus in item.statBonuses)
                {
                    AddDetailLine(
                        $"{BaseClasses.EquipmentStatKeys.DisplayName(bonus.stat)}  " +
                        $"{(bonus.amount >= 0 ? "+" : "")}{bonus.amount}" +
                        BaseClasses.EquipmentStatKeys.DisplaySuffix(bonus.stat), ref y);
                }
            }

            if (item.codeGrants != null && item.codeGrants.Count > 0)
            {
                y += 6f;
                AddDetailLine($"부여 코드 {item.codeGrants.Count}개", ref y, UITheme.Accent);
            }

            y += 8f;
            if (isEquipped && owner != null)
            {
                AddDetailButton("해제", ref y, () =>
                {
                    owner.TryUnequip(item.id, storeToCarried: true);
                    AfterInventoryChange();
                });
            }
            else if (owner != null)
            {
                AddDetailButton("장착", ref y, () => EquipStored(item, owner), primary: true);
                AddDetailButton("폐기", ref y, () =>
                {
                    if (owner.RemoveCarriedItem(item.id)) AfterInventoryChange();
                });
            }
            else
            {
                AddDetailLine("캐릭터 칸으로 끌어다 놓으면 넘어갑니다.", ref y, UITheme.TextMuted);
            }

            _detailCard.sizeDelta = new Vector2(280f, y + 12f);
            PlaceDetailNext(slotRect);
        }

        /// <summary>카드를 칸 오른쪽에 붙이되, 화면 밖으로 나가면 왼쪽으로 넘긴다.</summary>
        private void PlaceDetailNext(RectTransform slotRect)
        {
            if (slotRect == null) return;

            var corners = new Vector3[4];
            slotRect.GetWorldCorners(corners);

            Vector2 topRight = _root.InverseTransformPoint(corners[2]);
            Vector2 topLeft = _root.InverseTransformPoint(corners[1]);

            float halfWidth = _root.rect.width * 0.5f;
            float halfHeight = _root.rect.height * 0.5f;

            float x = topRight.x + 8f;
            if (x + _detailCard.sizeDelta.x > halfWidth) x = topLeft.x - 8f - _detailCard.sizeDelta.x;

            float top = Mathf.Min(topRight.y, halfHeight - 8f);
            if (top - _detailCard.sizeDelta.y < -halfHeight) top = -halfHeight + _detailCard.sizeDelta.y + 8f;

            _detailCard.anchoredPosition = new Vector2(x, top);
        }

        private void AddDetailTitle(string text, ref float y, Color color)
        {
            TextMeshProUGUI label = UIBuild.Text("Title", _detailCard, text, UITheme.FontBody, color);
            UIBuild.Pin(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 24f), new Vector2(12f, -y));
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.sizeDelta = new Vector2(-24f, 24f);
            y += 26f;
        }

        private void AddDetailLine(string text, ref float y, Color? color = null)
        {
            TextMeshProUGUI label = UIBuild.Text("Line", _detailCard, text, UITheme.FontMicro,
                color ?? UITheme.TextPrimary);
            UIBuild.Pin(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 17f), new Vector2(12f, -y));
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.sizeDelta = new Vector2(-24f, 17f);
            y += 18f;
        }

        private void AddDetailButton(string text, ref float y, System.Action onClick, bool primary = false)
        {
            Button button = UIBuild.Button(text, _detailCard, text, onClick, primary, UITheme.FontCaption);
            UIBuild.Pin(button.image.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 32f),
                new Vector2(12f, -y));
            button.image.rectTransform.anchorMax = new Vector2(1f, 1f);
            button.image.rectTransform.sizeDelta = new Vector2(-24f, 32f);
            y += 36f;
        }

        // ── 장비 이동 ────────────────────────────────────────────────

        private static bool FitsSlot(ItemData item, BaseClasses.EquipmentSlot slot)
        {
            return item != null
                   && BaseClasses.EquipmentLoadout.TryParseSlot(item.slot, out BaseClasses.EquipmentSlot parsed)
                   && parsed == slot;
        }

        /// <summary>장비를 다른 캐릭터에게 넘긴다. 같은 캐릭터면 장착만 푼다.</summary>
        private void TransferTo(ItemDrag drag, Unit target)
        {
            if (drag?.Item == null || target == null) return;

            if (drag.From == target)
            {
                if (drag.Equipped) target.TryUnequip(drag.Item.id, storeToCarried: true);
            }
            else
            {
                InventoryManager inventory = GameManager.Instance?.inventoryManager;
                if (inventory == null) return;
                if (!inventory.TryTransferItem(drag.From, target, drag.Item.id, out string reason))
                {
                    Debug.LogWarning($"[코덱스] 장비를 넘기지 못했습니다: {reason}");
                    return;
                }
            }

            AfterInventoryChange();
        }

        /// <summary>끌어온 장비를 그 칸의 캐릭터에게 착용시킨다. 남의 것이면 먼저 넘겨받는다.</summary>
        private void EquipFromDrag(ItemDrag drag, Unit owner)
        {
            if (owner == null || drag?.Item == null) return;

            InventoryManager inventory = GameManager.Instance?.inventoryManager;
            if (inventory == null) return;

            if (drag.From != owner)
            {
                if (!inventory.TryTransferItem(drag.From, owner, drag.Item.id, out string moveReason))
                {
                    Debug.LogWarning($"[코덱스] 장비를 넘기지 못했습니다: {moveReason}");
                    return;
                }
            }
            else if (drag.Equipped)
            {
                owner.TryUnequip(drag.Item.id, storeToCarried: true);
            }

            if (!inventory.TryEquipStoredItem(owner, drag.Item.id, out string reason))
            {
                Debug.LogWarning($"[코덱스] 장착하지 못했습니다: {reason}");
            }

            AfterInventoryChange();
        }

        private void EquipStored(ItemData item, Unit owner)
        {
            InventoryManager inventory = GameManager.Instance?.inventoryManager;
            if (inventory == null || owner == null) return;

            if (!inventory.TryEquipStoredItem(owner, item.id, out string reason))
            {
                Debug.LogWarning($"[코덱스] 장착하지 못했습니다: {reason}");
            }

            AfterInventoryChange();
        }

        private void AfterInventoryChange()
        {
            GameManager.Instance?.runManager?.SaveCurrentRun();
            GameManager.Instance?.RefreshPreparationForCarryWeight();
            Refresh();
        }

        // ── 데이터 ───────────────────────────────────────────────────

        private static List<Unit> Allies()
        {
            GridManager grid = GridManager.Instance;
            if (grid?.heroList == null) return new List<Unit>();

            // isActive를 반드시 본다. 물러난 유닛이 리스트에 남아 있으면 같은 캐릭터가 여러 번 나온다.
            return grid.heroList
                .Where(unit => unit != null && unit.isActive && !unit.IsEnemy)
                .OrderByDescending(unit => unit.currentCell != null && unit.currentCell.yPos > 0)
                .ThenBy(unit => unit.UnitName)
                .ToList();
        }

        private static List<int> SharedStorage()
        {
            return GameManager.Instance?.inventoryManager?.ItemIdsInHand ?? new List<int>();
        }
    }
}
