using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    /// TAB으로 여는 파티 창. 장비 · 중량은 없고 코드 보관함과 <b>한 명의 빌드</b>를 크게 읽는다.
    ///
    /// 예전에는 다섯 명을 좁은 다섯 열에 나란히 세워 보상 코드 7칸과 스탯이 모두 작게 눌렸다(2026-10-06 QA).
    /// 지금은 왼쪽 레일에서 한 명을 고르고, 오른쪽 전체를 그 유닛에 쓴다.
    ///   · 빌드 탭 — 원화 · 레벨 · 자원 머리, 가운데 활성 코드 7칸 · 아래 코드 보관함, 고른 칸의 상세, 8스탯
    ///   · 코드 탭 — 고유 특성 · 일반행동 · (있으면) 특수행동 · 초월기 · 보상 코드 · 그 밖의 패시브를 넓은 스크롤로
    /// 준비 중 드래그로 활성 · 보관 · 아군 간 이동과 점유 칸 교환을 한다(Detail_17 §2).
    /// </summary>
    public sealed class CodexScreen
    {
        private enum Tab { Sheet, Codes }

        private static readonly BaseClasses.BaseEnums.StatType[] StatOrder =
        {
            BaseClasses.BaseEnums.StatType.MaxHp,
            BaseClasses.BaseEnums.StatType.Attack,
            BaseClasses.BaseEnums.StatType.SpecialAttack,
            BaseClasses.BaseEnums.StatType.Defense,
            BaseClasses.BaseEnums.StatType.SpecialDefense,
            BaseClasses.BaseEnums.StatType.Speed,
            BaseClasses.BaseEnums.StatType.CritRate,
            BaseClasses.BaseEnums.StatType.CritDamage,
        };

        /// <summary>
        /// 비어 있는 칸의 테두리. 안쪽은 칠하지 않고 <b>짙은 선</b>만 둘러 채워진 칸과 한눈에 갈리게 한다.
        /// </summary>
        private static readonly Color EmptySlotBorder = new(.42f,.57f,.70f,.32f);

        private const int EmptySlotBorderWidth = 1;

        /// <summary>빈 칸을 눌렀을 때의 안내. 코드를 얻는 길과 칸 규칙을 한 번에 적는다.</summary>
        /// <summary>표에 설명이 없을 때 플레이어에게 보이는 문구. 파일 이름 · ID는 로그에만 남긴다.</summary>
        internal const string MissingEffectText = "효과 정보를 불러오지 못했습니다.";

        internal const string EmptySlotGuide =
            "활성 코드 칸입니다.\n\n" +
            "· 활성 코드만 전투에 적용됩니다(최대 7개).\n" +
            "· 보관함에서 끌어 이 칸에 놓으면 활성화합니다.\n" +
            "· 활성 코드를 보관함으로 끌면 해제합니다.\n" +
            "· 왼쪽 아군 초상화로 끌면 그 아군의 보관함으로 이동합니다.\n" +
            "· 편집은 준비 중에만 가능합니다.";

        private GameObject _rootObject;
        private RectTransform _rail;
        private RectTransform _main;
        private Button _sheetTab;
        private Button _codeTab;
        private Button _supplyButton;
        private TextMeshProUGUI _supplyHint;
        private Tab _tab = Tab.Sheet;

        /// <summary>보고 있는 유닛. 다시 열어도, 탭을 바꿔도 유지한다. 파티에서 빠지면 첫 유닛으로 바꾼다.</summary>
        private Unit _selected;

        /// <summary>빌드 탭에서 고른 보상 코드 칸(0~6). 유닛을 바꾸면 처음 찬 칸으로 돌아간다.</summary>
        private int _selectedSlot = -1;

        /// <summary>
        /// 창이 열려 있는 동안 바뀌는 값 — 쿠베라의 금력 배율, 고른 유닛의 체력 · 자원 · 레벨 · 스탯, 레일의 전투불능 · 코드 칸.
        /// 전투 중에도 열 수 있으므로 상단 바(<see cref="HUD.TopStatusBar"/>)처럼 매 프레임 읽고 글자 · 색이 달라졌을 때만 고친다.
        /// 계층은 다시 만들지 않아 스크롤 · 선택 · 툴팁이 그대로다.
        /// </summary>
        private sealed class LiveEntry
        {
            public TextMeshProUGUI Label;
            public System.Func<string> Text;
            public System.Func<Color> Color;
            public TooltipTrigger Trigger;
            public string Shown;
            public Color ShownColor;
        }

        private readonly List<LiveEntry> _live = new();

        /// <summary>마지막으로 그린 구조(파티 구성 · 고른 유닛의 코드). 달라지면 값 줄만 고쳐서는 안 되므로 다시 그린다.</summary>
        private int _structureHash;

        /// <summary>구조 검사 간격(프레임). 값 줄은 매 프레임, 파티 목록을 다시 모으는 검사는 띄엄띄엄 한다.</summary>
        private const int StructureCheckFrames = 6;

        public bool IsOpen => _rootObject != null && _rootObject.activeSelf;

        /// <summary>검증용 — 지금 보고 있는 유닛과 탭.</summary>
        internal Unit SelectedUnit => _selected;
        internal string ActiveTabName => _tab.ToString();

        /// <summary>
        /// HUD가 매 프레임 부른다. 파티가 바뀌었거나 고른 유닛의 코드 칸이 바뀌었으면 스크롤을 지킨 채 다시 그리고,
        /// 아니면 값 줄의 글자 · 색만 고친다. 그 항목의 툴팁이 떠 있으면 툴팁까지 고친다.
        /// </summary>
        public void Tick()
        {
            if (!IsOpen) return;
            if (Time.frameCount % StructureCheckFrames == 0 && StructureHash() != _structureHash)
            {
                RefreshKeepingScroll();
                return;
            }

            foreach (LiveEntry entry in _live)
            {
                if (entry.Label == null) continue;
                if (entry.Color != null)
                {
                    Color color = entry.Color();
                    if (color != entry.ShownColor)
                    {
                        entry.ShownColor = color;
                        entry.Label.color = color;
                    }
                }
                string text = entry.Text();
                if (text == entry.Shown) continue;
                entry.Shown = text;
                entry.Label.text = text;
                if (entry.Trigger != null) entry.Trigger.RefreshIfShown();
            }
        }

        /// <summary>글자(와 색)가 실행 중에 바뀌는 줄을 등록한다. 지금 값으로 한 번 맞춰 둔다.</summary>
        private void Live(TextMeshProUGUI label, System.Func<string> text, System.Func<Color> color = null)
        {
            var entry = new LiveEntry { Label = label, Text = text, Color = color, Shown = text() };
            label.text = entry.Shown;
            if (color != null)
            {
                entry.ShownColor = color();
                label.color = entry.ShownColor;
            }
            _live.Add(entry);
        }

        /// <summary>파티 구성(인스턴스) · 고른 유닛 · 그 유닛의 코드 칸 · 패시브 수. 값(체력 · 레벨)은 넣지 않는다.</summary>
        private int StructureHash()
        {
            unchecked
            {
                int hash = 17;
                foreach (Unit unit in PartyMembers()) hash = hash * 31 + unit.GetHashCode();
                if (_selected == null) return hash;
                hash = hash * 31 + _selected.GetHashCode();
                foreach (int id in _selected.RewardCodeSlots) hash = hash * 31 + id;
                foreach (int id in _selected.StoredRewardCodeIds) hash = hash * 31 + id;
                hash = hash * 31 + _selected.ActivePassiveCodes.Count;
                hash = hash * 31 + (_selected.HasSpecialAction ? 1 : 0);
                return hash;
            }
        }

        /// <summary>
        /// 구조가 바뀌어 다시 그릴 때 목록 · 상세의 스크롤 위치를 지킨다. 내용이 짧아졌으면 끝까지로 맞춘다.
        /// 선택 유닛 · 탭 · 고른 칸은 필드라 그대로 남는다(사라졌으면 Refresh가 유효한 유닛으로 바꾼다).
        /// </summary>
        private void RefreshKeepingScroll()
        {
            var saved = new Dictionary<string, float>();
            foreach (ScrollRect scroll in _main.GetComponentsInChildren<ScrollRect>(false))
                saved[scroll.name] = scroll.content.anchoredPosition.y;

            Refresh();
            if (saved.Count == 0) return;
            Canvas.ForceUpdateCanvases();
            foreach (ScrollRect scroll in _main.GetComponentsInChildren<ScrollRect>(false))
            {
                if (!saved.TryGetValue(scroll.name, out float y)) continue;
                float max = Mathf.Max(0f, scroll.content.rect.height - ((RectTransform)scroll.transform).rect.height);
                scroll.content.anchoredPosition = new Vector2(scroll.content.anchoredPosition.x, Mathf.Clamp(y, 0f, max));
            }
        }

        /// <summary>
        /// 창을 연다. <paramref name="focus"/>가 파티원이면 그 유닛을 고르고(소환수면 소환자),
        /// 없으면 지난번에 보던 유닛을 그대로 보여 준다.
        /// </summary>
        public void Show(Unit focus = null)
        {
            EnsureBuilt();
            Unit target = focus != null && focus.IsSummon ? focus.SummonOwner : focus;
            if (target != null && !target.IsEnemy && target != _selected)
            {
                _selected = target;
                _selectedSlot = -1;
            }
            _rootObject.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            if (_rootObject != null) _rootObject.SetActive(false);
        }

        private void EnsureBuilt()
        {
            if (_rootObject != null) return;
            Canvas canvas = UIBuild.Canvas("CodexCanvas", UITheme.LayerCharacterSheet);
            _rootObject = new GameObject("CodexScreen", typeof(RectTransform));
            _rootObject.transform.SetParent(canvas.transform, false);
            UIBuild.Stretch(_rootObject.GetComponent<RectTransform>());

            Image backdrop = UIBuild.Backdrop("Backdrop", _rootObject.transform);
            UIBuild.OnClick(backdrop.gameObject, Hide);

            Image panel = UIBuild.Panel("Panel", _rootObject.transform, UITheme.Surface,
                UIShapes.Corner.Diagonal, 16, UITheme.Outline, 1);
            // 모달과 같은 창 문법 — 위 모서리 16의 종이 한 장 + 남색 제목 띠. 굵은 윤곽 대신 얕은 그림자로 뜬다.
            panel.sprite = UIShapes.RoundedRect(16, UITheme.Surface);
            UIBuild.Anchor(panel.rectTransform, new Vector2(0.03f, 0.04f), new Vector2(0.97f, 0.96f));
            UIBuild.Elevate(panel, 24, .14f, 6f);
            _rootObject.AddComponent<UIPopIn>().Panel = panel.rectTransform;

            BuildHeader(panel.transform);

            _rail = UIBuild.Container("PartyRail", panel.transform);
            UIBuild.Anchor(_rail, new Vector2(0f, 0f), new Vector2(.09f,1f), 18f, 18f);
            _rail.offsetMax = new Vector2(-6f, -76f);

            _main = UIBuild.Container("Main", panel.transform);
            UIBuild.Anchor(_main, new Vector2(.09f,0f), Vector2.one, 18f, 18f);
            _main.offsetMin = new Vector2(6f, 18f);
            _main.offsetMax = new Vector2(-24f, -76f);
            _rootObject.SetActive(false);
        }

        /// <summary>
        /// 남색 제목 띠 — 왼쪽 기호 · 제목, 가운데 빌드/코드 탭, 오른쪽 공용 보급 · 닫기.
        /// 모든 요소의 세로 중심이 띠 중심선(32px)에 온다. 탭은 띠 위의 흰 알약으로 선택을 표시한다.
        /// </summary>
        private void BuildHeader(Transform panel)
        {
            UIArtDirection.Header(panel, UIArtDirection.HeaderHeight);
            UIArtDirection.TitleGlyph(panel, UIIcon.Party, 24);
            UIArtDirection.HeaderTitle(panel, "Title", "파티 빌드", 62f, 320f);

            _sheetTab = MakeTab(panel, "빌드", -70f, () => SetTab(Tab.Sheet));
            _codeTab = MakeTab(panel, "코드", 70f, () => SetTab(Tab.Codes));

            // 공용 보급은 정식 경로(GameManager → UIManager)로 연다. 이 창을 먼저 닫아 두 모달이 겹치지 않게 한다.
            _supplyButton = UIBuild.Button("OpenSupply", panel, "공용 보급 열기", OpenSupply, false, UITheme.FontReadSmall);
            UIArtDirection.HeaderButton(_supplyButton);
            UIBuild.Pin(_supplyButton.image.rectTransform, new Vector2(1f, 1f), new Vector2(196f, 40f),
                new Vector2(-80f, -(UIArtDirection.HeaderMidline - 20f)));
            _supplyHint = UIBuild.Text("SupplyHint", panel, "", UITheme.FontCaption, UITheme.TextSecondary,
                TextAlignmentOptions.MidlineRight);
            UIBuild.Pin(_supplyHint.rectTransform, new Vector2(1f, 1f), new Vector2(360f, 22f), new Vector2(-76f, -10f));

            _supplyHint.gameObject.SetActive(false);
            UIArtDirection.HeaderClose("Close", panel, Hide, 24f);
        }

        private static Button MakeTab(Transform panel, string text, float x, System.Action action)
        {
            Button button = UIBuild.Button($"Tab{text}", panel, text, action, false, UITheme.FontReadSmall);
            UIBuild.Pin(button.image.rectTransform, new Vector2(0.5f, 1f), new Vector2(128f, 40f),
                new Vector2(x, -(UIArtDirection.HeaderMidline - 20f)));
            return button;
        }

        private void SetTab(Tab tab)
        {
            _tab = tab;
            Refresh();
        }

        private void OpenSupply()
        {
            GameManager game = GameManager.Instance;
            if (game == null || game.gameState != BaseClasses.BaseEnums.GameState.Preparation) return;
            Hide();
            game.OpenInventoryFromPreparation();
        }

        // ── 파티 · 선택 ─────────────────────────────────────────────

        /// <summary>창에 세울 파티. 소환수와 같은 영웅의 중복은 뺀다. 쓰러진 아군 · 대기석도 넣는다.</summary>
        internal static List<Unit> PartyMembers()
        {
            return GridManager.Instance?.heroList?
                .Where(unit => unit != null && !unit.IsEnemy && !unit.IsSummon && (unit.ID > 0 || unit.LastActiveId > 0))
                .GroupBy(unit => unit.ID > 0 ? unit.ID : unit.LastActiveId)
                .Select(group => group.First())
                .Take(5)
                .ToList() ?? new List<Unit>();
        }

        private void Select(Unit unit)
        {
            if (unit == null || unit == _selected) return;
            _selected = unit;
            _selectedSlot = -1;
            Refresh();
        }

        private void SelectSlot(int slot)
        {
            _selectedSlot = slot;
            Refresh();
        }

        /// <summary>
        /// 자리 표시. 유닛 카드의 위치와 같은 말을 쓴다. 쓰러진 아군은 칸이 남아 있어도 전투불능이 먼저다.
        /// </summary>
        internal static string PlacementText(Unit unit)
        {
            if (unit == null) return "";
            if (!unit.isActive) return "전투불능";
            if (unit.IsBench) return "대기석";
            GridManager grid = GridManager.Instance;
            if (unit.currentCell != null && grid != null)
            {
                if (unit.currentCell.xPos == grid.GetFrontColumn(false)) return "전열";
                if (unit.currentCell.xPos == grid.GetRearColumn(false)) return "후열";
            }
            return "미배치";
        }

        private void Refresh()
        {
            if (_main == null) return;
            _live.Clear();
            ClearNow(_rail);
            ClearNow(_main);
            Tint(_sheetTab, _tab == Tab.Sheet);
            Tint(_codeTab, _tab == Tab.Codes);

            bool preparation = GameManager.Instance?.gameState == BaseClasses.BaseEnums.GameState.Preparation;
            _supplyButton.interactable = preparation;
            _supplyHint.text = preparation ? "인장 · 비약 사용" : "준비 중에만 열 수 있습니다";

            List<Unit> party = PartyMembers();
            if (party.Count == 0)
            {
                _selected = null;
                TextMeshProUGUI empty = UIBuild.Text("Empty", _main, "표시할 아군이 없습니다.",
                    UITheme.FontReadBody, UITheme.TextMuted, TextAlignmentOptions.Center);
                UIBuild.Stretch(empty.rectTransform);
                _structureHash = StructureHash();
                UIScaledText.KeepReadable(_rootObject.transform);
                return;
            }

            // 파티가 바뀌어 보던 유닛이 사라졌으면 같은 영웅(새 인스턴스)을, 그마저 없으면 첫 유닛을 고른다.
            if (_selected == null || !party.Contains(_selected))
            {
                int id = _selected == null ? 0 : _selected.ID > 0 ? _selected.ID : _selected.LastActiveId;
                Unit same = id > 0 ? party.FirstOrDefault(unit => (unit.ID > 0 ? unit.ID : unit.LastActiveId) == id) : null;
                _selected = same ?? party[0];
                _selectedSlot = -1;
            }

            BuildRail(party);

            RectTransform column = UIBuild.Container($"Col_{_selected.UnitName}", _main);
            UIBuild.Stretch(column);
            if (_tab == Tab.Sheet) BuildSheet(column, _selected);
            else
            {
                // 코드 탭에는 왼쪽 아래 능력치 판이 없다. 원화 머리가 왼쪽 열 전체를 써서 빈 종이를 남기지 않는다.
                Image identity = BuildIdentity(column, _selected);
                identity.rectTransform.anchorMin = new Vector2(0f, .12f);
                RectTransform skills=UIBuild.Container("SkillWorkspace",column); UIBuild.Anchor(skills,new Vector2(.285f,0),Vector2.one); BuildCodes(skills,_selected,_live);
            }
            _structureHash = StructureHash();
            // 이 창의 22 이상 글자에만 가독 하한을 건다(다른 화면의 자동 맞춤 규칙은 그대로).
            UIScaledText.KeepReadable(_rootObject.transform);
        }

        /// <summary>
        /// 다시 그릴 때 옛 자식을 먼저 끈다. <see cref="UIBuild.Clear"/>의 Destroy는 프레임 끝까지 미뤄지므로
        /// 같은 프레임에 새로 만든 같은 이름의 항목과 겹쳐 잡히지 않게 한다.
        /// </summary>
        private static void ClearNow(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--) parent.GetChild(i).gameObject.SetActive(false);
            UIBuild.Clear(parent);
        }

        private static void Tint(Button button, bool active)
        {
            if (button != null) UIArtDirection.HeaderTab(button, active);
        }

        // ── 왼쪽 레일 ───────────────────────────────────────────────

        private void BuildRail(List<Unit> party)
        {
            for (int i=0;i<party.Count;i++)
            {
                Unit unit=party[i]; bool selected=unit==_selected;
                Image row=UIBuild.Panel($"Member_{unit.UnitName}",_rail,Color.clear,cut:16);
                float top=1f-i*.19f;
                UIBuild.Anchor(row.rectTransform,new Vector2(0,top-.17f),new Vector2(1,top),8,4);
                // 선택은 옅은 군청 면으로 구분한다. 드롭 대상의 평상 모양을 잡기 전에 칠한다.
                UIArtDirection.SelectRow(row,selected,false,10);
                UIBuild.OnClick(row.gameObject,()=>Select(unit));
                Image face=PortraitImage("Portrait",row.transform,unit);
                face.sprite=UIPortraitCrop.Face(face.sprite);
                // 선택은 행의 면과 막대로 구분한다. 비활성 아군도 덱 편집 대상이므로 초상은 읽히게 유지한다.
                face.color = Color.white;
                UIBuild.Anchor(face.rectTransform,new Vector2(.12f,.24f),new Vector2(.88f,.94f));
                var name=UIBuild.Text("Name",row.transform,unit.UnitName,18,selected ? UITheme.AccentDim : UITheme.TextSecondary,TextAlignmentOptions.Midline);
                name.fontStyle=selected ? FontStyles.Bold : FontStyles.Normal;
                UIBuild.Anchor(name.rectTransform,new Vector2(0,.03f),new Vector2(1,.23f));
                AttachCodeDrop(row,unit,true);
                TooltipTrigger.Attach(row.gameObject,()=>unit.UnitName,()=>new List<UITooltip.Line>{UITooltip.Line.Note(VitalsText(unit),UITheme.TextPrimary)},UITheme.Accent);
            }
        }

        private static Image PortraitImage(string name, Transform parent, Unit unit)
        {
            var artObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            artObject.transform.SetParent(parent, false);
            Image image = artObject.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            Sprite sprite = SpriteResource.LoadPortrait(unit.PortraitPath);
            image.sprite = sprite;
            image.enabled = sprite != null;
            return image;
        }

        // ── 빌드 탭 ─────────────────────────────────────────────────

        private void BuildSheet(RectTransform parent, Unit unit)
        {
            BuildIdentity(parent,unit);
            IReadOnlyList<int> owned=unit.RewardCodeSlots;
            if(_selectedSlot<0 || _selectedSlot>=Unit.RewardCodeSlotCount)_selectedSlot=0;
            RectTransform slots=UIBuild.Container("RewardSlots",parent);
            UIBuild.Anchor(slots,new Vector2(.27f,.31f),new Vector2(.70f,1));
            BuildRewardSlots(slots,owned);
            BuildCodeStorage(parent,unit);
            // 고른 칸의 상세는 종이 위 흰 카드 한 장이다. 목록(가운데)과 상세(오른쪽)가 재질로 갈려 계층이 읽힌다.
            Image detail=UIBuild.Solid("CodeDetail",parent,Color.white);
            detail.sprite=UIShapes.Solid(Color.white);
            UIArtDirection.ReadingSurface(detail.transform, UIIcon.Support, false);
            detail.type=Image.Type.Sliced;
            UIBuild.Anchor(detail.rectTransform,new Vector2(.715f,.31f),Vector2.one);
            detail.rectTransform.offsetMin=new Vector2(0f,4f);
            BuildSlotDetail(detail.transform,unit,owned);
            Image stats=UIBuild.Panel("Stats",parent,Color.clear,cut:16);
            stats.sprite=UIShapes.Solid(Color.clear);
            UIBuild.Anchor(stats.rectTransform,new Vector2(.0f,.0f),new Vector2(.255f,.39f));
            BuildStats(stats.transform,unit);
        }

        /// <summary>머리 — 원화 · 이름 · 레벨/자리 · 체력/자원 · 전투 성향 태그.</summary>
        private Image BuildIdentity(RectTransform parent, Unit unit)
        {
            Image strip=UIBuild.Panel("Identity",parent,Color.clear,cut:18);
            UIBuild.Anchor(strip.rectTransform,new Vector2(0,.405f),new Vector2(.255f,1));

            // 원화 뒤의 원소 빛. 원소는 의미색이므로 이 자리에만 옅게(28%) 깐다 — 흰 종이에 그림이 떠 보이지 않고 서 있게 한다.
            Color element=Effects.Vfx.ElementPalette.PastelColorFor(Effects.Vfx.ElementPalette.Parse(unit.Element));
            Image glow=UIBuild.Solid("ElementGlow",strip.transform,Color.white);
            glow.sprite=UIShapes.Glow(128,new Color(element.r,element.g,element.b,.30f));
            glow.type=Image.Type.Simple; glow.raycastTarget=false;
            UIBuild.Anchor(glow.rectTransform,new Vector2(-.08f,.30f),new Vector2(1.08f,1.04f));
            Image floor=UIBuild.Solid("ElementFloor",strip.transform,Color.white);
            floor.sprite=UIShapes.Glow(128,new Color(UITheme.Accent.r,UITheme.Accent.g,UITheme.Accent.b,.10f));
            floor.type=Image.Type.Simple; floor.raycastTarget=false;
            UIBuild.Anchor(floor.rectTransform,new Vector2(.12f,.25f),new Vector2(.88f,.33f));

            Image portrait=PortraitImage("Portrait",strip.transform,unit);
            Sprite standing=SpriteResource.LoadStanding(unit.PortraitPath.Replace("_PORTRAIT","_STANDING"));
            if(standing!=null)portrait.sprite=standing;
            UIBuild.Anchor(portrait.rectTransform,new Vector2(0f,.28f),new Vector2(1f,1f));
            TextMeshProUGUI name=UIBuild.Text("SelectedName",strip.transform,unit.UnitName,30,UITheme.TextPrimary);
            name.fontStyle=FontStyles.Bold;
            UIBuild.Anchor(name.rectTransform,new Vector2(.04f,.18f),new Vector2(.96f,.28f));
            var meta=UIBuild.Text("Meta",strip.transform,"",18,UITheme.TextSecondary);
            UIBuild.Anchor(meta.rectTransform,new Vector2(.04f,.115f),new Vector2(.96f,.18f));
            Live(meta,()=> $"Lv.{unit.Level}  ·  {UnitInfoText.ElementName(unit.Element)}  ·  {PlacementText(unit)}");
            var vitals=UIBuild.Text("Vitals",strip.transform,"",16,UITheme.TextSecondary);
            vitals.overflowMode=TextOverflowModes.Ellipsis;
            UIBuild.Anchor(vitals.rectTransform,new Vector2(.04f,.045f),new Vector2(.96f,.11f));
            Live(vitals,()=>VitalsText(unit));
            TooltipTrigger.Attach(strip.gameObject,()=>unit.UnitName,()=>new List<UITooltip.Line>{UITooltip.Line.Note(VitalsText(unit),UITheme.TextPrimary),UITooltip.Line.Note(UnitInfoText.Tagline(unit),UITheme.TextSecondary)},UITheme.Accent);
            return strip;
        }

        /// <summary>"체력 1,200 / 1,800 · 마나 40 / 100 · 창세의 결정 3/7" — 초월기 자원 방식에 맞춰 적는다.</summary>
        internal static string VitalsText(Unit unit)
        {
            var parts = new List<string> { $"체력 {unit.HpCurr:N0} / {unit.HpMax:N0}" };
            switch (unit.UltimateResourceType)
            {
                case BaseClasses.BaseEnums.UltimateResourceType.Passive:
                    parts.Add("초월기 상시 발동");
                    break;
                case BaseClasses.BaseEnums.UltimateResourceType.Stack:
                    parts.Add($"초월기 중첩 {unit.ManaCurr} / {unit.ManaMax}");
                    break;
                default:
                    parts.Add($"마나 {unit.ManaCurr} / {unit.ManaMax}");
                    break;
            }

            foreach (string resourceId in unit.CombatResourceIds)
            {
                int maximum = unit.GetCombatResourceMaximum(resourceId);
                if (maximum <= 0) continue;
                parts.Add($"{Combat.CombatResourceLabels.NameOf(resourceId)} {unit.GetCombatResource(resourceId)}/{maximum}");
            }
            return string.Join("  ·  ", parts);
        }

        // ── 보상 코드 칸 ─────────────────────────────────────────────

        /// <summary>
        /// 활성 코드 7칸. 번호 · 티어 · 이름 · 효과를 한 행에서 비교한다.
        /// 칸을 누르면 오른쪽 상세에 전문 · 태그 · 상위 경로가 뜬다. 빈 칸은 테두리만 두르고 얻는 길을 안내한다.
        /// </summary>
        private void BuildRewardSlots(RectTransform parent, IReadOnlyList<int> owned)
        {
            var heading=UIBuild.Text("Heading",parent,$"활성 코드  {_selected.RewardCodeIds.Count} / 7",24,UITheme.TextPrimary);
            heading.fontStyle=FontStyles.Bold;
            UIBuild.Anchor(heading.rectTransform,new Vector2(0,.925f),Vector2.one);
            for(int i=0;i<Unit.RewardCodeSlotCount;i++)
            {
                int id=i<owned.Count ? owned[i] : 0; bool filled=id>0; bool selected=i==_selectedSlot;
                int tier=filled ? RewardCodeCatalog.TierOf(id):0; Color tint=filled ? UITheme.Rarity(tier):UITheme.TextMuted;
                float top=.90f-i*.125f;
                Image slot=UIBuild.Panel("RewardSlot"+i,parent,Color.clear,cut:16);
                UIBuild.Anchor(slot.rectTransform,new Vector2(0f,top-.115f),new Vector2(1f,top));
                // 선택은 옅은 군청 면으로 구분한다. 빈 칸은 끼울 자리임이 보이게 헤어라인 홈으로 둔다.
                UIArtDirection.SelectRow(slot,selected,false,10);
                if(!filled && !selected) slot.sprite=UIShapes.RoundedRect(10,new Color(1f,1f,1f,.55f),EmptySlotBorder,EmptySlotBorderWidth);
                Image rule=UIBuild.Divider("RowRule",slot.transform);UIBuild.Anchor(rule.rectTransform,Vector2.zero,new Vector2(1f,0f));rule.rectTransform.offsetMin=new Vector2(12f,0f);rule.rectTransform.offsetMax=new Vector2(-12f,1f);rule.raycastTarget=false;
                rule.gameObject.SetActive(filled && !selected);
                int index=i; UIBuild.OnClick(slot.gameObject,()=>SelectSlot(index)); AttachCodeDrop(slot,_selected,false,index);
                if(filled)AttachCodeDrag(slot,_selected,false,index,id);
                // 한 줄의 세로 기준 — 번호 · 아이콘 원 · 두 줄 글자 덩어리의 시각 중심을 모두 행 중심에 맞춘다.
                // 이름(22) 상자는 중심 +2~+34px, 효과(18) 첫 줄은 중심 -2px부터 내려와 두 줄 덩어리의 중심이 행 중심 근처에 온다.
                var number=UIBuild.Text("Number",slot.transform,(i+1).ToString("00"),15,UITheme.TextMuted,TextAlignmentOptions.Midline);
                UIBuild.Pin(number.rectTransform,new Vector2(0f,.5f),new Vector2(34f,26f),new Vector2(10f,0f));
                Image tile=UIBuild.Solid("CodeTile",slot.transform,Color.white);
                tile.sprite=UIShapes.RoundedRect(20,filled ? UIArtDirection.SelectedFill : new Color(UITheme.Accent.r,UITheme.Accent.g,UITheme.Accent.b,.05f));
                tile.type=Image.Type.Sliced; tile.raycastTarget=false;
                UIBuild.Pin(tile.rectTransform,new Vector2(0f,.5f),new Vector2(40f,40f),new Vector2(50f,0f));
                Image icon=UIBuild.Icon("CodeIcon",tile.transform,filled ? UIArtDirection.CodeIcon(id):UIIcon.Arrow,filled ? tint : UITheme.TextMuted);
                UIBuild.Stretch(icon.rectTransform,10f,10f);
                if(filled) { var badge = ScreenKit.TierBadge(slot.transform, tier, 22f, showTicks: false);
                    badge.anchorMin = badge.anchorMax = new Vector2(1f,.5f); badge.pivot = new Vector2(1f,.5f);
                    badge.anchoredPosition = new Vector2(-16f, 18f); }
                var name=UIBuild.Text(filled ? "Name":"Empty",slot.transform,filled ? RewardCodeFlow.NameOf(id):"빈 코드 칸",22,filled ? UITheme.TextPrimary : UITheme.TextMuted);
                name.fontStyle=filled ? FontStyles.Bold : FontStyles.Normal;name.overflowMode=TextOverflowModes.Ellipsis;
                UIBuild.Anchor(name.rectTransform,new Vector2(0f,.5f),new Vector2(1f,.5f));
                name.rectTransform.offsetMin=new Vector2(106f,2f); name.rectTransform.offsetMax=new Vector2(filled ? -112f : -16f,34f);
                var effect=UIBuild.Text(filled ? "Effect":"How",slot.transform,filled ? (RewardCodeCatalog.Find(id)?.capacitorCap>0 ? Combat.RewardCodeInventory.ChargeBrief(_selected.RewardCodeSlotInstances[index]) : CodeCatalog.Summary(RewardCodeCatalog.Find(id)?.description)):"보관 코드를 끌어 활성화",18,UITheme.TextSecondary,TextAlignmentOptions.TopLeft,true);
                effect.richText=false;effect.overflowMode=TextOverflowModes.Ellipsis;
                UIBuild.Anchor(effect.rectTransform,new Vector2(0f,0f),new Vector2(1f,.5f));
                effect.rectTransform.offsetMin=new Vector2(106f,4f); effect.rectTransform.offsetMax=new Vector2(-24f,-2f);
                if(filled)TooltipTrigger.Attach(slot.gameObject,()=>RewardCodeFlow.NameOf(id),()=>RewardCodeLines(id,_selected.RewardCodeSlotInstances[index]),tint);
            }
        }

        /// <summary>main의 공통 드래그 런타임에 코드 위치를 전달한다.</summary>
        private void AttachCodeDrag(Image card, Unit owner, bool stored, int index, int id)
        {
            var source = card.gameObject.AddComponent<UIDragSource>();
            source.Payload = new Combat.RewardCodeInventory.Drag { Owner = owner, Stored = stored, Index = index, CodeId = id, InstanceId=(stored?owner.StoredRewardCodeInstances[index]:owner.RewardCodeSlotInstances[index])?.instanceId };
            source.Label = RewardCodeFlow.NameOf(id);
            source.Icon = UILineIcons.Get(UIArtDirection.CodeIcon(id));
            source.ShowIconAndLabel = true;
            source.Tint = UITheme.Rarity(RewardCodeCatalog.TierOf(id));
        }

        private void AttachCodeDrop(Image card, Unit owner, bool stored, int index = -1)
        {
            var target = card.gameObject.AddComponent<UIDropTarget>();
            target.IdleSprite = card.sprite;
            target.Preview = payload => stored ? $"{owner.UnitName} · 보관" :
                $"{owner.UnitName} · {(index >= 0 && index < owner.RewardCodeSlotInstances.Count && owner.RewardCodeSlotInstances[index] != null ? "코드 교환" : "코드 활성화")}";
            // 일반 선택의 군청 회색 면과 구분되는 드롭 전용 청색 면이다.
            target.HighlightSprite = UIShapes.Surface(8, new Color(.78f,.87f,.98f,1f),
                UIShapes.Corner.Diagonal, new Color(.20f,.35f,.57f,.80f), 1);
            target.CanAccept = payload => payload is Combat.RewardCodeInventory.Drag drag &&
                Combat.RewardCodeInventory.CanMove(drag, owner, stored, index);
            target.Accept = payload =>
            {
                if (payload is Combat.RewardCodeInventory.Drag drag && Combat.RewardCodeInventory.TryMove(drag, owner, stored, index))
                {
                    UITooltip.HideAny(); RefreshKeepingScroll();
                    Transform arrival;
                    if(owner==_selected)
                    {
                        arrival=stored ? _main.GetComponentsInChildren<RectTransform>().FirstOrDefault(t=>t.name=="StoredCode"+(owner.StoredRewardCodeIds.Count-1)) :
                            _main.GetComponentsInChildren<RectTransform>().FirstOrDefault(t=>t.name=="RewardSlot"+index);
                        if(stored)
                        {
                            var scroll=_main.GetComponentsInChildren<ScrollRect>().FirstOrDefault(s=>s.name=="StorageScroll");
                            if(scroll!=null){Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=0f;}
                        }
                    }
                    // 같은 프레임에 Refresh한 이전 레일은 Destroy 대기 중이다. Find는 꺼진 옛 행도 잡으므로 새 활성 행을 찾는다.
                    else arrival=_rail.GetComponentsInChildren<RectTransform>()
                        .FirstOrDefault(t=>t.name=="Member_"+owner.UnitName);
                    UIDropArrival.Play(arrival as RectTransform,owner!=_selected?"+1 보관":null);
                }
            };
        }

        private void BuildCodeStorage(RectTransform parent, Unit unit)
        {
            // 보관함은 코드를 끌어다 놓는 쟁반이다. 종이보다 한 단계 짙은 군청 4.5% 면으로 "놓는 자리"를 보이고,
            // 그 위의 코드는 흰 칩으로 떠 있다(칩 한 겹 — 중첩 카드 없음).
            Image box = UIBuild.Solid("CodeStorage", parent, Color.white);
            box.sprite = UIShapes.RoundedRect(12, new Color(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, .045f));
            box.type = Image.Type.Sliced;
            UIBuild.Anchor(box.rectTransform, new Vector2(.27f,0), new Vector2(1f,.285f));
            AttachCodeDrop(box, unit, true);
            TextMeshProUGUI heading = UIBuild.Text("StorageTitle", box.transform,
                $"보관함  {unit.StoredRewardCodeIds.Count}", UITheme.FontReadSmall, UITheme.TextPrimary);
            heading.fontStyle = FontStyles.Bold;
            ScreenKit.TopBand(heading.rectTransform, 10f, 36f, 18f, 0f);
            heading.rectTransform.anchorMax = new Vector2(.46f, 1f);
            TextMeshProUGUI hint = UIBuild.Text("StorageHint", box.transform,
                GameManager.Instance?.gameState == BaseClasses.BaseEnums.GameState.Preparation ? "준비 중 자유 이동 · 효과 비활성" : "전투 중 편집 잠금",
                UITheme.FontCaption, UITheme.TextMuted, TextAlignmentOptions.MidlineRight);
            ScreenKit.TopBand(hint.rectTransform, 10f, 36f, 0f, 18f);
            hint.rectTransform.anchorMin = new Vector2(.43f, 1f);
            RectTransform content = UIBuild.ScrollArea("StorageScroll", box.transform, out ScrollRect scroll);
            UIBuild.Stretch((RectTransform)scroll.transform);
            ((RectTransform)scroll.transform).offsetMin = new Vector2(12f, 10f);
            ((RectTransform)scroll.transform).offsetMax = new Vector2(-8f, -52f);
            const int columns = 4; const float height = 84f;
            for (int i = 0; i < unit.StoredRewardCodeIds.Count; i++)
            {
                int id = unit.StoredRewardCodeIds[i]; int tier = RewardCodeCatalog.TierOf(id);
                Image card = UIBuild.Solid($"StoredCode{i}", content, Color.white);
                card.sprite=UIArtDirection.Sheet(10);
                float left = (i % columns) / (float)columns;
                UIBuild.Anchor(card.rectTransform, new Vector2(left, 1f), new Vector2(left + 1f / columns, 1f));
                card.rectTransform.pivot = new Vector2(0.5f, 1f);
                card.rectTransform.offsetMin = new Vector2(4f, -(i / columns + 1) * height + 4f);
                card.rectTransform.offsetMax = new Vector2(-6f, -(i / columns) * height - 4f);
                Image chipIcon = UIBuild.Icon("CodeIcon", card.transform, UIArtDirection.CodeIcon(id), UITheme.Accent);
                // 기호는 20px로 작게 — 가장 긴 코드 이름(약 225px)이 22px 그대로 칩 안에 들어가야 한다.
                UIBuild.Pin(chipIcon.rectTransform, new Vector2(0f, .5f), new Vector2(20f, 20f), new Vector2(12f, 0f));
                // 보관 카드의 왼쪽 군청 띠는 티어마다 같은 색이라 활성 카드와 다른 굵은 외곽처럼만 보였다. 티어는 T 표기로 읽는다.
                TextMeshProUGUI name = UIBuild.Text("Name", card.transform, RewardCodeFlow.NameOf(id), UITheme.FontReadSmall, UITheme.TextPrimary);
                name.fontStyle = FontStyles.Bold; name.overflowMode = TextOverflowModes.Ellipsis;
                UIBuild.Anchor(name.rectTransform, new Vector2(0f, .5f), new Vector2(1f, 1f));
                name.rectTransform.offsetMin = new Vector2(40f, 0f); name.rectTransform.offsetMax = new Vector2(-8f, -4f);
                TextMeshProUGUI meta = UIBuild.Text("Meta", card.transform, $"T{tier}  ·  "+(RewardCodeCatalog.Find(id)?.capacitorCap>0 ? Combat.RewardCodeInventory.ChargeBrief(unit.StoredRewardCodeInstances[i]) : TagText(id)), 18f, UITheme.TextMuted);
                meta.overflowMode = TextOverflowModes.Ellipsis;
                UIBuild.Anchor(meta.rectTransform, Vector2.zero, new Vector2(1f, .5f));
                meta.rectTransform.offsetMin = new Vector2(40f, 6f); meta.rectTransform.offsetMax = new Vector2(-8f, 0f);
                int storageIndex=i;
                AttachCodeDrag(card, unit, true, i, id);
                TooltipTrigger.Attach(card.gameObject, () => RewardCodeFlow.NameOf(id), () => RewardCodeLines(id,unit.StoredRewardCodeInstances[storageIndex]), UITheme.Rarity(tier));
            }
            if (unit.StoredRewardCodeIds.Count == 0)
            {
                var empty = UIBuild.Text("EmptyStorage", content,
                    "활성 코드를 이곳으로 끌어 보관하세요.\n다른 아군의 초상화에 놓으면 그 아군에게 이동합니다.",
                    UITheme.FontReadSmall, UITheme.TextMuted, TextAlignmentOptions.Center, true);
                UIBuild.Pin(empty.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 110f), Vector2.zero);
                empty.rectTransform.anchorMin = new Vector2(0f, 1f); empty.rectTransform.anchorMax = Vector2.one;
            }
            ScreenKit.ScrollFade(scroll, new Color(.93f, .945f, .962f, 1f), 18f);
            content.sizeDelta = new Vector2(0f, Mathf.Max(110f, ((unit.StoredRewardCodeIds.Count + columns - 1) / columns) * height));
        }

        private static string SlotMetaLine(int codeId)
        {
            string tags = TagText(codeId);
            int upper = RewardCodeCatalog.UpperOf(codeId);
            string upperText = upper > 0 ? $"▲ {RewardCodeFlow.NameOf(upper)}" : "";
            if (tags.Length > 0 && upperText.Length > 0) return $"{tags}  ·  {upperText}";
            return tags.Length > 0 ? tags : upperText;
        }

        /// <summary>
        /// 고른 칸의 상세 카드. 목록 행이 이름 · 한 줄 효과로 "고르는" 자리라면, 이 카드는 그 코드를 "읽는" 자리다.
        /// 위에 아이콘 원 · 큰 이름 · 티어 눈금 · 칸 번호를 한 덩어리로 세우고, 가는 선 아래로 효과 전문 · 태그 · 상위 경로를 굴린다.
        /// 같은 효과 문장 아래 이름 줄을 또 늘어놓던 예전 판(2026-10-08 root 지적)과 달리 이름 · 티어는 머리에만 있다.
        /// </summary>
        private void BuildSlotDetail(Transform parent, Unit unit, IReadOnlyList<int> owned)
        {
            int codeId = _selectedSlot < owned.Count ? owned[_selectedSlot] : 0;
            const float pad = 22f;

            // 머리 — "칸 N 상세"는 맨 위 작은 캡션. 검증이 이 글자를 읽는다.
            TextMeshProUGUI heading = UIBuild.Text("Heading", parent, $"칸 {_selectedSlot + 1} 상세",
                UITheme.FontCaption, UITheme.TextMuted);
            ScreenKit.TopBand(heading.rectTransform, 14f, 26f, pad, pad);

            Image tile = UIArtDirection.Emblem("DetailTile", parent, codeId > 0 ? UIArtDirection.CodeIcon(codeId) : UIIcon.Arrow,
                codeId > 0 ? RewardCodeCatalog.TierOf(codeId) : 1);
            ScreenKit.PinTopLeft(tile.rectTransform, pad - 4f, 38f);
            tile.rectTransform.sizeDelta = new Vector2(76f, 76f);

            // 이름(26) 상자 44~80은 원(48~104)의 위쪽, 티어 줄 80~108은 아래쪽 — 두 줄 덩어리가 원의 중심(76)을 공유한다.
            TextMeshProUGUI detailName = UIBuild.Text("DetailName", parent,
                codeId > 0 ? RewardCodeFlow.NameOf(codeId) : "빈 코드 칸", 26f, codeId > 0 ? UITheme.TextPrimary : UITheme.TextMuted);
            detailName.fontStyle = FontStyles.Bold;
            detailName.overflowMode = TextOverflowModes.Ellipsis;
            ScreenKit.TopBand(detailName.rectTransform, 44f, 36f, pad + 84f, pad);
            if (codeId > 0)
            {
                int tier = RewardCodeCatalog.TierOf(codeId);
                RectTransform badge = ScreenKit.TierBadge(parent, tier, 20f);
                ScreenKit.PinTopLeft(badge, pad + 84f, 84f);
                TextMeshProUGUI kind = UIBuild.Text("DetailKind", parent, $"보상 코드  ·  {_selectedSlot + 1}번 칸",
                    UITheme.FontCaption, UITheme.TextMuted);
                ScreenKit.TopBand(kind.rectTransform, 80f, 28f, pad + 84f + ScreenKit.TierBadgeWidth(20f) + 12f, pad);
            }
            else
            {
                TextMeshProUGUI kind = UIBuild.Text("DetailKind", parent, $"{_selectedSlot + 1}번 칸  ·  비어 있음",
                    UITheme.FontCaption, UITheme.TextMuted);
                ScreenKit.TopBand(kind.rectTransform, 80f, 28f, pad + 84f, pad);
            }

            Image rule = UIBuild.Divider("DetailRule", parent);
            rule.raycastTarget = false;
            ScreenKit.TopBand(rule.rectTransform, 122f, 1f, pad, pad);

            RectTransform content = UIBuild.ScrollArea("DetailScroll", parent, out ScrollRect scroll);
            var viewport = (RectTransform)scroll.transform;
            UIBuild.Stretch(viewport);
            viewport.offsetMin = new Vector2(pad, 14f);
            viewport.offsetMax = new Vector2(-pad + 10f, -136f);

            string text = codeId > 0 ? DetailText(codeId, _selectedSlot)+"\n"+CodeText.Plain(Combat.RewardCodeInventory.ChargeSummary(unit.RewardCodeSlotInstances[_selectedSlot])) : CodeText.Plain(EmptySlotGuide);

            TextMeshProUGUI body = UIBuild.Text("DetailBody", content, text, UITheme.FontReadSmall,
                UITheme.TextPrimary, TextAlignmentOptions.TopLeft, true);
            body.richText = true;
            body.overflowMode = TextOverflowModes.Overflow;
            body.rectTransform.anchorMin = new Vector2(0f, 1f);
            body.rectTransform.anchorMax = new Vector2(1f, 1f);
            body.rectTransform.pivot = new Vector2(0.5f, 1f);
            float width = Mathf.Max(200f, MeasuredWidth(viewport, ReferenceMainWidth * 0.285f - 34f) - 12f);
            float height = Mathf.Max(24f, body.GetPreferredValues(text, width, 0f).y + 8f);
            body.rectTransform.sizeDelta = new Vector2(-12f, height);
            body.rectTransform.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, height + 8f);
            // 짧은 효과를 큰 빈 흰 판으로 확대하지 않는다. 장문은 최대 높이에서 스크롤한다.
            RectTransform detailPanel = (RectTransform)parent;
            float available = detailPanel.rect.height > 180f ? detailPanel.rect.height : 610f;
            float fitted = Mathf.Clamp(height + 166f, Mathf.Min(320f, available), available);
            detailPanel.anchorMin = new Vector2(detailPanel.anchorMin.x, 1f);
            detailPanel.anchorMax = new Vector2(detailPanel.anchorMax.x, 1f);
            detailPanel.pivot = new Vector2(.5f, 1f);
            detailPanel.sizeDelta = new Vector2(0f, fitted);
            detailPanel.anchoredPosition = new Vector2(0f, -4f);
        }

        /// <summary>상세 판의 서식 문자열. 데이터 문장은 <see cref="CodeText.Plain"/>으로 감싼다.</summary>
        internal static string DetailText(int codeId, int slotIndex)
        {
            CodeCatalog.Entry entry = RewardCodeCatalog.Find(codeId);
            int tier = RewardCodeCatalog.TierOf(codeId);
            Color tierColor = UITheme.Rarity(Mathf.Max(1, tier));
            var sb = new StringBuilder();

            sb.Append(CodeText.Paint("효과", UITheme.TextMuted)).Append('\n');
            string summary = CodeCatalog.Summary(entry?.description);
            if (summary.Length == 0) Debug.LogWarning($"[파티 창] 보상 코드 설명 없음 — 패시브 #{codeId}");
            sb.Append(CodeText.Plain(summary.Length > 0 ? summary : MissingEffectText)).Append("\n\n");
            // 이름 · 티어 · 칸 번호는 상세 카드 머리가 크게 보인다. 본문에서는 되풀이하지 않는다(tier · slotIndex는 호출 호환용).
            _ = tierColor;
            _ = slotIndex;

            string tags = TagText(codeId);
            if (tags.Length > 0) sb.Append(CodeText.Paint("태그  ", UITheme.TextMuted)).Append(CodeText.Plain(tags)).Append('\n');

            string ladder = LadderText(codeId);
            if (ladder.Length > 0) sb.Append(CodeText.Paint("상위 경로  ", UITheme.TextMuted)).Append(ladder).Append('\n');

            List<string> markers = CodeCatalog.Markers(entry?.description);
            if (markers.Count > 0)
                sb.Append(CodeText.Paint(CodeText.Plain(string.Join(" · ", markers)), UITheme.TextMuted)).Append('\n');
            return sb.ToString();
        }

        /// <summary>"하위 → [이 코드] → 상위(T4) → 그 위" 사다리. 사다리가 없으면 빈 문자열.</summary>
        private static string LadderText(int codeId)
        {
            List<int> lowers = RewardCodeCatalog.LowersOf(codeId).ToList();
            var uppers = new List<int>();
            int current = RewardCodeCatalog.UpperOf(codeId);
            for (int guard = 0; current > 0 && guard < 8; guard++)
            {
                uppers.Add(current);
                current = RewardCodeCatalog.UpperOf(current);
            }
            if (lowers.Count == 0 && uppers.Count == 0) return "";

            var parts = new List<string>();
            if (lowers.Count > 0)
                parts.Add(CodeText.Plain(string.Join(" / ", lowers.Select(id => $"{RewardCodeFlow.NameOf(id)}(T{RewardCodeCatalog.TierOf(id)})"))));
            parts.Add($"<b>{CodeText.Plain($"[{RewardCodeFlow.NameOf(codeId)}]")}</b>");
            parts.AddRange(uppers.Select(id => CodeText.Plain($"{RewardCodeFlow.NameOf(id)}(T{RewardCodeCatalog.TierOf(id)})")));
            string chain = string.Join(" → ", parts);
            return uppers.Count > 0 ? chain + CodeText.Paint("\n상위 획득 시 이 칸에서 바뀌고 하위는 보관됩니다.", UITheme.TextMuted) : chain;
        }

        private void BuildStats(Transform parent, Unit unit)
        {
            TextMeshProUGUI heading = UIBuild.Text("Heading", parent, "능력치",
                UITheme.FontReadSmall, UITheme.TextPrimary);
            heading.fontStyle = FontStyles.Bold;
            UIBuild.Anchor(heading.rectTransform, new Vector2(0f, 0.88f), new Vector2(1f, 0.985f), 20f, 0f);
            TextMeshProUGUI legend = UIBuild.Text("Legend", parent, "기초 → 최종", UITheme.FontCaption, UITheme.TextMuted,
                TextAlignmentOptions.MidlineRight);
            UIBuild.Anchor(legend.rectTransform, new Vector2(0.4f, 0.88f), new Vector2(1f, 0.985f), 20f, 0f);

            float step = 0.86f / StatOrder.Length;
            for (int i = 0; i < StatOrder.Length; i++)
            {
                BaseClasses.BaseEnums.StatType stat = StatOrder[i];
                float rowTop = 0.875f - i * step;

                RectTransform row = UIBuild.Container($"Stat_{stat}", parent);
                UIBuild.Anchor(row, new Vector2(0f, rowTop - step), new Vector2(1f, rowTop), 20f, 0f);
                // 한 줄 건너 아주 옅은 띠 — 긴 숫자 열을 눈으로 따라갈 때 줄을 잃지 않는다.
                if (i % 2 == 0)
                {
                    Image band = UIBuild.Solid("Band", row, new Color(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, .035f));
                    band.raycastTarget = false;
                    UIBuild.Stretch(band.rectTransform);
                    band.rectTransform.offsetMin = new Vector2(-8f, 0f);
                    band.rectTransform.offsetMax = new Vector2(8f, 0f);
                }
                Image swatch = UIBuild.Solid("Swatch", row, UITheme.Stat(stat));
                swatch.raycastTarget = false;
                UIBuild.Anchor(swatch.rectTransform, new Vector2(0f, 0.25f), new Vector2(0f, 0.75f));
                swatch.rectTransform.offsetMax = new Vector2(4f, 0f);
                // 스탯 색은 전부 군청으로 통일되어 줄마다 붙은 막대가 "|" 기호처럼 반복될 뿐이었다. 정렬은 라벨 끝선이 맡는다.
                swatch.gameObject.SetActive(false);
                TextMeshProUGUI label = UIBuild.Text("Label", row, UnitStats.DisplayName(stat), UITheme.FontReadSmall,
                    UITheme.TextSecondary);
                UIBuild.Anchor(label.rectTransform, new Vector2(0.03f, 0f), new Vector2(0.52f, 1f));
                TextMeshProUGUI valueLabel = UIBuild.Text("Value", row, "", UITheme.FontReadSmall, UITheme.TextPrimary,
                    TextAlignmentOptions.MidlineRight);
                UIBuild.Anchor(valueLabel.rectTransform, new Vector2(0.53f, 0f), new Vector2(1f, 1f));
                Live(valueLabel, () => StatText(unit, stat),
                    () => Mathf.Approximately(unit.BaseStat(stat), unit.StatValue(stat)) ? UITheme.TextPrimary : UITheme.Positive);
            }
        }

        /// <summary>"기초 → 최종". 같으면 값 하나.</summary>
        private static string StatText(Unit unit, BaseClasses.BaseEnums.StatType stat)
        {
            float baseValue = unit.BaseStat(stat);
            float finalValue = unit.StatValue(stat);
            return Mathf.Approximately(baseValue, finalValue)
                ? UnitStats.Format(stat, finalValue)
                : $"{UnitStats.Format(stat, baseValue)} → {UnitStats.Format(stat, finalValue)}";
        }

        private static List<UITooltip.Line> RewardCodeLines(int codeId,global::Core.RewardCodeInstanceData holding=null)
        {
            var lines=RewardCodeFlow.TooltipLines(codeId);
            if(holding!=null && RewardCodeCatalog.Find(codeId)?.capacitorCap>0)
                lines.Add(UITooltip.Line.Note(Combat.RewardCodeInventory.ChargeSummary(holding),UITheme.TextPrimary));
            return lines;
        }

        private static string TagText(int codeId)
            => string.Join(" · ", UnitTagCatalog.Resolve(RewardCodeCatalog.TagsOf(codeId)).Select(tag => tag.Name));

        // ── 코드 탭 ──────────────────────────────────────────────────

        /// <summary>
        /// 패널 폭을 기준 해상도로 어림한 오른쪽 본문 폭. 첫 프레임처럼 실제 폭을 아직 모를 때만 쓴다.
        /// 패널 0.94 · 레일 0.20 · 좌우 여백을 뺀 값이다.
        /// </summary>
        private static float ReferenceMainWidth => UITheme.ReferenceResolution.x * 0.94f * 0.80f - 42f;

        /// <summary>실제 배치 폭. 캔버스가 아직 한 번도 그려지지 않았으면 어림값을 쓴다.</summary>
        private static float MeasuredWidth(RectTransform rect, float fallback)
        {
            float width = rect != null ? rect.rect.width : 0f;
            return width > 120f ? width : fallback;
        }

        /// <summary>
        /// 코드 목록. 고유 특성 · 일반행동 · (있으면) 특수행동 · 초월기 아래에 보상 코드를 칸 순서대로,
        /// 그 아래 사건이 준 패시브를 붙인다. 한 유닛에 본문 폭 전체를 쓰므로 문구를 줄이지 않고 다 적는다.
        /// </summary>
        private static void BuildCodes(RectTransform parent, Unit unit, List<LiveEntry> live)
        {
            RectTransform content = UIBuild.ScrollArea("CodeScroll", parent, out ScrollRect scroll);
            var viewport = (RectTransform)scroll.transform;
            UIBuild.Stretch(viewport);
            float columnWidth = MeasuredWidth(viewport, ReferenceMainWidth);

            float y = 0f;
            AddListHeading(content, unit.HasSpecialAction
                ? $"{unit.UnitName} — 고유 특성 · 일반행동 · 특수행동 · 초월기"
                : $"{unit.UnitName} — 고유 특성 · 일반행동 · 초월기", ref y);
            foreach (PassiveCode passive in unit.ActivePassiveCodes)
            {
                if (passive is { IsUniquePassive: true }) AddCodeEntry(content, "고유 특성", passive, columnWidth, ref y);
            }

            AddCodeEntry(content, "일반행동", unit.ActiveNormalCode, columnWidth, ref y);
            // 특수행동은 일부 유닛만 가진 슬롯이다(로카팔라 묶음 참여자 등). 없는 유닛에는 항목을 만들지 않는다.
            AddCodeEntry(content, "특수행동", unit.ActiveSpecialCode, columnWidth, ref y, live);
            AddCodeEntry(content, "초월기", unit.ActiveUltimateCode, columnWidth, ref y);

            IReadOnlyList<int> owned = unit.RewardCodeIds;
            y += 10f;
            AddListHeading(content, $"활성 코드  {unit.RewardCodeIds.Count} / {Unit.RewardCodeSlotCount}", ref y);
            if (owned.Count == 0) AddListNote(content, "받은 보상 코드가 없다. 전투 보상과 상점의 코드 탭에서 얻는다.", ref y);
            for (int i = 0; i < owned.Count; i++) AddRewardCodeEntry(content, owned[i], i, columnWidth, ref y);

            // 사건이 준 비보상 패시브. 보상 코드 칸을 쓰지 않으니 따로 모은다.
            List<PassiveCode> others = unit.ActivePassiveCodes
                .Where(passive => passive != null && !passive.IsUniquePassive &&
                                  !RewardCodeCatalog.IsRewardCode(passive.CatalogId))
                .ToList();
            if (others.Count > 0)
            {
                y += 10f;
                AddListHeading(content, "그 밖의 패시브", ref y);
                foreach (PassiveCode passive in others) AddCodeEntry(content, "패시브", passive, columnWidth, ref y);
            }

            content.sizeDelta = new Vector2(0f, y + 12f);
        }

        private static void AddCodeEntry(Transform parent, string kind, Code code, float columnWidth, ref float y,
            List<LiveEntry> live = null)
        {
            if (code == null) return;
            CodeCatalog.Entry entry = CodeCatalog.Find(code);
            string name = !string.IsNullOrWhiteSpace(code.CodeName) ? code.CodeName : entry?.verbalName ?? "이름 없음";
            string summary = CodeCatalog.Summary(entry?.description);
            if (summary.Length == 0)
            {
                // 설명이 비면 표에서 빠진 것이다. 무엇이 빠졌는지는 로그에 남기고 화면에는 일반 문구만 보인다.
                Debug.LogWarning($"[파티 창] 코드 설명 없음 — {CodeCatalog.SlotOf(code)} #{code.CatalogId}");
                summary = MissingEffectText;
            }

            List<string> markers = CodeCatalog.Markers(entry?.description);
            System.Func<string> liveText = live != null ? LiveTextOf(code) : null;
            Color tint = kind == "패시브" ? GradeColor(code) : UITheme.CodeUnique;
            AddEntry(parent, name, kind, summary, markers.Count > 0 ? string.Join(" · ", markers) : null, tint,
                columnWidth, ref y, () =>
                {
                    var lines = new List<UITooltip.Line> { new("종류", kind, UITheme.TextSecondary) };
                    lines.Add(UITooltip.Line.Note(summary, UITheme.TextPrimary));
                    if (liveText != null) lines.Add(UITooltip.Line.Note(liveText(), UITheme.Accent));
                    if (markers.Count > 0) lines.Add(UITooltip.Line.Note(string.Join(" · ", markers), UITheme.TextMuted));
                    return lines;
                }, liveText, live);
        }

        /// <summary>
        /// 이 런의 지금 값에 따라 달라지는 코드의 현재 값 줄. 표의 설명(자료실 · 선택 화면과 공유)은 그대로 두고
        /// 실제 유닛을 보여 주는 이 창에서만 덧붙인다. 값은 코드가 쓰는 계산을 그대로 불러 식을 두 번 쓰지 않는다.
        /// </summary>
        private static System.Func<string> LiveTextOf(Code code) => code switch
        {
            Codes.Special.KuberaGoldRush => () =>
            {
                float multiplier = Codes.Special.KuberaGoldRush.GoldMultiplier(out int gold, out int stage);
                return $"현재 금력 배율 ×{multiplier:0.00}\n보유 골드 {gold:N0} · 스테이지 {stage}";
            },
            _ => null,
        };

        /// <summary>실시간 줄의 높이를 잴 때 쓰는 가장 긴 모양. 값이 바뀌어도 줄 수가 늘어 아래 항목을 덮지 않게 한다.</summary>
        private const string LiveTextWidest = "현재 금력 배율 ×4.00\n보유 골드 9,999,999 · 스테이지 9999";

        private static void AddRewardCodeEntry(Transform parent, int codeId, int slotIndex, float columnWidth, ref float y)
        {
            CodeCatalog.Entry entry = RewardCodeCatalog.Find(codeId);
            int tier = RewardCodeCatalog.TierOf(codeId);
            string summary = CodeCatalog.Summary(entry?.description);
            string tags = TagText(codeId);
            string kind = tags.Length > 0 ? $"{slotIndex + 1}번 칸 · T{tier} · {tags}" : $"{slotIndex + 1}번 칸 · T{tier}";
            int upper = RewardCodeCatalog.UpperOf(codeId);
            string note = upper > 0
                ? $"▲ 상위: {RewardCodeFlow.NameOf(upper)} (T{RewardCodeCatalog.TierOf(upper)}) — 받으면 이 칸에서 바뀐다"
                : null;
            AddEntry(parent, RewardCodeFlow.NameOf(codeId), kind, summary, note,
                UITheme.Rarity(Mathf.Max(1, tier)), columnWidth, ref y, () => RewardCodeLines(codeId));
        }

        /// <summary>
        /// 코드 한 항목: 윗줄에 이름과 종류, 그 아래 효과 전문, 있으면 보조 줄(꼬리표 · 상위 코드).
        /// 항목 전체가 툴팁 영역이라 이름이 아닌 설명 위에 올려도 전문이 뜬다.
        /// </summary>
        private static void AddEntry(Transform parent, string name, string kind, string summary, string note,
            Color tint, float columnWidth, ref float y, System.Func<List<UITooltip.Line>> tooltip,
            System.Func<string> liveText = null, List<LiveEntry> live = null)
        {
            float entryTop = y;
            float inner = Mathf.Max(120f, columnWidth - 48f);

            // 회색 상자 대신 종이 위 흰 카드 한 장. 굵은 윤곽 없이 헤어라인만 둔다.
            Image card = UIBuild.Panel($"Entry_{name}", parent, UITheme.SurfaceSunken, UIShapes.Corner.Diagonal, 8,
                UITheme.Outline, 1);
            card.sprite = UIArtDirection.Sheet(10);
            card.raycastTarget = false;
            card.rectTransform.anchorMin = new Vector2(0f, 1f);
            card.rectTransform.anchorMax = new Vector2(1f, 1f);
            card.rectTransform.pivot = new Vector2(0.5f, 1f);
            card.rectTransform.anchoredPosition = new Vector2(-6f, -y);
            y += 8f;

            var head = UIBuild.Container("Code", parent);
            head.anchorMin = new Vector2(0f, 1f);
            head.anchorMax = new Vector2(1f, 1f);
            head.pivot = new Vector2(0.5f, 1f);
            head.sizeDelta = new Vector2(-40f, 36f);
            head.anchoredPosition = new Vector2(-6f, -y);

            TextMeshProUGUI nameLabel = UIBuild.Text("Name", head, name, UITheme.FontReadHeading, tint);
            nameLabel.fontStyle = FontStyles.Bold;
            nameLabel.overflowMode = TextOverflowModes.Ellipsis;
            UIBuild.Anchor(nameLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 1f), 4f, 0f);

            // 일반행동은 이름도 "일반행동"이라 종류를 한 번 더 적으면 같은 말이 두 번 나온다.
            TextMeshProUGUI kindLabel = UIBuild.Text("Kind", head, name == kind ? "" : kind, UITheme.FontReadSmall,
                UITheme.TextMuted, TextAlignmentOptions.MidlineRight);
            kindLabel.overflowMode = TextOverflowModes.Ellipsis;
            UIBuild.Anchor(kindLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(1f, 1f), 4f, 0f);
            y += 40f;

            y += AddWrapped(parent, "Summary", summary, UITheme.FontReadSmall, UITheme.TextPrimary, inner, y) + 6f;
            if (!string.IsNullOrEmpty(note))
                y += AddWrapped(parent, "Note", note, UITheme.FontCaption, UITheme.TextMuted, inner, y) + 6f;

            TextMeshProUGUI liveLabel = null;
            string shown = null;
            if (liveText != null)
            {
                shown = liveText();
                liveLabel = UIBuild.Text("LiveNote", parent, shown, UITheme.FontReadSmall, UITheme.Accent,
                    TextAlignmentOptions.TopLeft, true);
                liveLabel.richText = false;
                liveLabel.overflowMode = TextOverflowModes.Overflow;
                liveLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
                liveLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
                liveLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
                float liveHeight = Mathf.Max(20f, liveLabel.GetPreferredValues(LiveTextWidest, inner, 0f).y);
                liveLabel.rectTransform.sizeDelta = new Vector2(-48f, liveHeight);
                liveLabel.rectTransform.anchoredPosition = new Vector2(-6f, -y);
                y += liveHeight + 6f;
            }

            y += 6f;
            card.rectTransform.sizeDelta = new Vector2(-20f, y - entryTop);

            // 항목 전체를 덮는 투명 판. 휠은 막지 않으므로 목록도 굴러간다.
            Image hover = UIBuild.Solid($"Hover_{name}", parent, new Color(1f, 1f, 1f, 0f));
            // 용어 글자의 레이캐스트를 덮지 않는다. 그 밖의 영역은 기존 전체 항목 툴팁을 받는다.
            hover.transform.SetAsFirstSibling();
            hover.rectTransform.anchorMin = new Vector2(0f, 1f);
            hover.rectTransform.anchorMax = new Vector2(1f, 1f);
            hover.rectTransform.pivot = new Vector2(0.5f, 1f);
            hover.rectTransform.sizeDelta = new Vector2(-20f, y - entryTop);
            hover.rectTransform.anchoredPosition = new Vector2(-6f, -entryTop);
            TooltipTrigger trigger = TooltipTrigger.Attach(hover.gameObject, () => name, () => tooltip(), tint);

            if (liveLabel != null)
                live?.Add(new LiveEntry { Label = liveLabel, Text = liveText, Trigger = trigger, Shown = shown });
            y += 10f;
        }

        /// <summary>폭에 맞춰 줄을 바꾸는 글 한 덩어리를 <paramref name="y"/>에 놓고 높이를 돌려준다.</summary>
        private static float AddWrapped(Transform parent, string objectName, string text, float size, Color color,
            float inner, float y)
        {
            TextMeshProUGUI line = UIBuild.Text(objectName, parent, text, size, color, TextAlignmentOptions.TopLeft, true);
            line.richText = false;
            line.overflowMode = TextOverflowModes.Overflow;
            line.rectTransform.anchorMin = new Vector2(0f, 1f);
            line.rectTransform.anchorMax = new Vector2(1f, 1f);
            line.rectTransform.pivot = new Vector2(0.5f, 1f);
            float height = Mathf.Max(20f, line.GetPreferredValues(text, inner, 0f).y);
            line.rectTransform.sizeDelta = new Vector2(-48f, height);
            line.rectTransform.anchoredPosition = new Vector2(-6f, -y);
            return height;
        }

        private static Color GradeColor(Code code) => code?.Grade switch
        {
            BaseClasses.BaseEnums.CodeGrade.Enhanced => UITheme.CodeEnhanced,
            BaseClasses.BaseEnums.CodeGrade.Unique => UITheme.CodeUnique,
            _ => UITheme.CodeNormal,
        };

        private static void AddListHeading(Transform parent, string text, ref float y)
        {
            TextMeshProUGUI label = UIBuild.Text("Heading", parent, text, UITheme.FontReadSmall, UITheme.TextPrimary);
            label.fontStyle = FontStyles.Bold;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.rectTransform.anchorMin = new Vector2(0f, 1f);
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.pivot = new Vector2(0.5f, 1f);
            label.rectTransform.sizeDelta = new Vector2(-32f, 34f);
            label.rectTransform.anchoredPosition = new Vector2(-6f, -y);
            y += 42f;
        }

        private static void AddListNote(Transform parent, string text, ref float y)
        {
            TextMeshProUGUI label = UIBuild.Text("Note", parent, text, UITheme.FontReadSmall, UITheme.TextMuted);
            label.rectTransform.anchorMin = new Vector2(0f, 1f);
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.pivot = new Vector2(0.5f, 1f);
            label.rectTransform.sizeDelta = new Vector2(-48f, 30f);
            label.rectTransform.anchoredPosition = new Vector2(6f, -y);
            y += 36f;
        }
    }
}
