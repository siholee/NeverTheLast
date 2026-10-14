using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Combat;
using Entities;
using Helpers;
using Effects.Vfx;
using Managers.UI.Core;
using Managers.UI.Screens;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.HUD
{
    /// <summary>화면 좌하단의 고정 파티 HUD. 전장의 원화와 정보 표면을 분리한다.</summary>
    public sealed class PartyVitalsPanel
    {
        public const float Height = 258f;
        /// <summary>행동 순서 열(24 + 118) 오른쪽에서 시작해, 서열이 9행까지 늘어도 첫 카드 초상화와 겹치지 않는다.</summary>
        private const float Left = UITheme.ScreenEdgeInset + 118f + 12f;
        /// <summary>준비 독과 같은 바닥선.</summary>
        private const float Bottom = 18f;
        private const float MaximumEntryWidth = 186f, Gap = 10f, Inset = 12f;
        private float _entryWidth = MaximumEntryWidth, _entryPitch = MaximumEntryWidth + Gap;
        /// <summary>
        /// Noto Sans KR 한 줄의 실제 preferredHeight는 글자 크기의 약 1.77배다(18.04 → 31.86).
        /// 22pt 이름은 40, 21pt 수치는 38 높이를 줘야 자동 맞춤이 줄이거나 말줄임으로 지우지 않는다.
        /// </summary>
        private const float NameRow = 40f, ValueRow = 38f;
        /// <summary>흰 판 위에서 읽히도록 방어막 샴페인 금색을 본문 쪽으로 38% 눌렀다.</summary>
        private static readonly string ShieldTextHex = ColorUtility.ToHtmlStringRGB(Color.Lerp(UITheme.Shield, UITheme.TextPrimary, .38f));
        private readonly RectTransform _root;
        private readonly List<Entry> _entries = new();
        private readonly System.Action<Unit> _open;
        private int _hash;
        private float _nextRosterCheck;
        private sealed class Entry
        {
            public Unit Unit;
            public RectTransform Root;
            public CanvasGroup Group;
            public Image Plate, Health, Shield, Energy, Ring, Focus;
            public TextMeshProUGUI Name, Hp, Resource, Unique, Overflow;
            public Image[] Status = new Image[4];
            
        }
        public PartyVitalsPanel(Transform parent, System.Action<Unit> open)
        {
            _open = open;
            _root = UIBuild.Container("PartyVitals", parent);
            UIBuild.Pin(_root, Vector2.zero, new Vector2(_entryPitch * 4 + _entryWidth, Height), new Vector2(Left, Bottom));
        }
        public void Tick()
        {
            GameManager game = GameManager.Instance;
            bool visible = game != null && (game.gameState == BaseEnums.GameState.Preparation ||
                game.gameState == BaseEnums.GameState.RoundInProgress || game.gameState == BaseEnums.GameState.RoundEnd);
            _root.gameObject.SetActive(visible && !Cell.PlacementModeActive);
            if (!visible) return;
            if (Time.unscaledTime >= _nextRosterCheck)
            {
                _nextRosterCheck = Time.unscaledTime + .25f;
                List<Unit> roster = CodexScreen.PartyMembers();
                int hash = 17; foreach (Unit unit in roster) hash = hash * 31 + unit.GetInstanceID();
                // HUD 크기·해상도가 바뀌면 같은 로스터도 새 가로 공간에 맞춘다.
                hash = hash * 31 + Mathf.RoundToInt(((RectTransform)_root.parent).rect.width);
                if (hash != _hash) { _hash = hash; Rebuild(roster); }
            }
            foreach (Entry e in _entries) Refresh(e, game);
        }
        private void Rebuild(List<Unit> roster)
        {
            UIBuild.Clear(_root); _entries.Clear();
            // 준비 독의 시작선(.61)과 행동서열 오른쪽 사이에서 다섯 장이 모두 들어갈 폭을 구한다.
            // 배속/전투 규칙은 그대로 두고, "큰 HUD"에서도 마지막 카드가 독을 덮지 않게 한다.
            float width = ((RectTransform)_root.parent).rect.width;
            int count = Mathf.Max(1, roster.Count);
            float available = width * .61f + UITheme.ScreenEdgeInset - Left - 24f;
            _entryWidth = Mathf.Min(MaximumEntryWidth, Mathf.Max(140f, (available - Gap * (count - 1)) / count));
            _entryPitch = _entryWidth + Gap;
            _root.sizeDelta = new Vector2(_entryPitch * (count - 1) + _entryWidth, Height);
            for (int i = 0; i < roster.Count; i++)
            {
                Unit unit = roster[i];
                var e = new Entry { Unit = unit };
                e.Root = UIBuild.Container("Vitals_" + unit.ID, _root);
                UIBuild.Pin(e.Root, Vector2.zero, new Vector2(_entryWidth, Height), new Vector2(i * _entryPitch, 0));
                e.Group = UIBuild.Group(e.Root.gameObject);
                // 세로 단차(엔트리 186×258, 판 0~172). 글자 행은 실제 폰트 높이를 그대로 받는다.
                // 상태 2~40 · 고유 자원 40~78 · HP 막대 82~88 · HP 88~126 · 이름 126~166(전체 폭) · 초상화 168~256.
                // 초월기 링과 수치는 초상화 오른쪽 열에 위아래로 따로 두어 이름 폭을 빼앗지 않는다.
                e.Plate = UIBuild.Panel("VitalsPlate", e.Root, new Color(.955f,.978f,1,.91f), cut:14);
                UIBuild.Pin(e.Plate.rectTransform, Vector2.zero, new Vector2(_entryWidth, 172), Vector2.zero);
                UIPrismMaterial.Apply(e.Plate);
                UIBuild.Elevate(e.Plate, 18, .12f, 5);
                UIBuild.OnClick(e.Plate.gameObject, () => _open(unit));
                Image portraitWindow = UIBuild.Panel("PortraitWindow", e.Root, new Color(.88f,.94f,.99f,.65f), cut:12);
                UIBuild.Pin(portraitWindow.rectTransform, Vector2.zero, new Vector2(88,88), new Vector2(8,168));
                portraitWindow.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                UIBuild.OnClick(portraitWindow.gameObject, () => _open(unit));
                Image portrait = UIBuild.Solid("Face", portraitWindow.transform, Color.white);
                portrait.sprite = UIPortraitCrop.Face(SpriteResource.LoadPortrait(unit.PortraitPath));
                portrait.preserveAspect = true; portrait.raycastTarget = false;
                UIBuild.Stretch(portrait.rectTransform);
                Image emblem = UIBuild.Icon("Element", e.Root, UIIcon.Infusion, ElementPalette.ColorFor(unit.Element));
                UIBuild.Pin(emblem.rectTransform, new Vector2(0,1), new Vector2(20,20), new Vector2(9,-12));
                e.Ring = UIBuild.RadialBar("Ultimate", e.Root, UITheme.Mana, new Color(.50f,.65f,.78f,.24f), 80, .91f);
                UIBuild.OnClick(e.Ring.transform.parent.gameObject, () => _open(unit));
                UIBuild.Pin((RectTransform)e.Ring.transform.parent, Vector2.zero, new Vector2(48,48), new Vector2(_entryWidth-68,208));
                Image burst = UIBuild.Icon("UltimateIcon", e.Ring.transform, UIIcon.Burst, UITheme.AccentDim);
                UIBuild.Stretch(burst.rectTransform, 12,12);
                // 초월기 수치는 링 바로 아래 독립 행. 이름 행과 나누어 써서 둘 다 18pt로 줄던 문제를 없앤다.
                e.Resource = Readable(UIBuild.Text("Resource", e.Root, "", 21, UITheme.AccentDim, TextAlignmentOptions.Center));
                UIBuild.Pin(e.Resource.rectTransform, Vector2.zero, new Vector2(84,ValueRow), new Vector2(_entryWidth-86,168));
                e.Name = Readable(UIBuild.Text("Name", e.Root, unit.UnitName, 22, UITheme.TextPrimary));
                e.Name.fontStyle = FontStyles.Bold;
                e.Name.overflowMode = TextOverflowModes.Ellipsis;
                UIBuild.Pin(e.Name.rectTransform, Vector2.zero, new Vector2(_entryWidth - Inset * 2,NameRow), new Vector2(Inset,126));
                e.Hp = Readable(UIBuild.Text("HealthValue", e.Root, "", 21, UITheme.TextSecondary, TextAlignmentOptions.MidlineRight));
                e.Hp.richText = true;
                UIBuild.Pin(e.Hp.rectTransform, Vector2.zero, new Vector2(_entryWidth - Inset * 2,ValueRow), new Vector2(Inset,88));
                e.Health = UIBuild.Bar("Health", e.Root, UITheme.Hp, UITheme.Track);
                UIBuild.Pin((RectTransform)e.Health.transform.parent, Vector2.zero, new Vector2(_entryWidth - Inset * 2,6), new Vector2(Inset,82));
                e.Shield = UIBuild.Solid("Shield", e.Health.transform.parent, UITheme.Shield);
                e.Shield.raycastTarget = false;
                // 궁극기 자원은 링 하나로 읽는다. HP 아래 2px 파란 줄은 같은 값을 반복해 밑줄처럼 보였으므로 숨긴다.
                e.Energy = UIBuild.Bar("Energy", e.Root, UITheme.Mana, UITheme.Track);
                e.Energy.transform.parent.gameObject.SetActive(false);
                e.Unique = Readable(UIBuild.Text("UniqueResource", e.Root, "", 21, UITheme.AccentDim, TextAlignmentOptions.MidlineLeft));
                e.Unique.overflowMode = TextOverflowModes.Ellipsis;
                UIBuild.Pin(e.Unique.rectTransform, Vector2.zero, new Vector2(_entryWidth - Inset * 2,ValueRow), new Vector2(Inset,40));
                e.Focus = UIBuild.Solid("Acting", e.Root, UITheme.Accent);
                e.Focus.raycastTarget = false;
                UIBuild.Anchor(e.Focus.rectTransform, new Vector2(.15f,0), new Vector2(.85f,0));
                e.Focus.rectTransform.sizeDelta = new Vector2(0,2);
                for (int j=0;j<e.Status.Length;j++)
                {
                    int slot = j;
                    e.Status[j] = UIBuild.Solid("Status"+j, e.Root, Color.white);
                    // 아이콘도 이름·HP와 같은 왼쪽 끝선에서 시작하고, 38px 상태 행의 가운데에 둔다.
                    UIBuild.Pin(e.Status[j].rectTransform, Vector2.zero, new Vector2(24,24), new Vector2(Inset+j*28,9));
                    TooltipTrigger.Attach(e.Status[j].gameObject,
                        () => BadgeName(unit, slot),
                        () => new List<UITooltip.Line> {UITooltip.Line.Note(BadgeDescription(unit, slot), UITheme.TextPrimary)}, UITheme.Accent);
                }
                e.Overflow = Readable(UIBuild.Text("MoreStatuses",e.Root,"",21,UITheme.TextPrimary));
                UIBuild.Pin(e.Overflow.rectTransform,Vector2.zero,new Vector2(_entryWidth-Inset*2-4*28,ValueRow),new Vector2(Inset+4*28,2));
                TooltipTrigger.Attach(e.Overflow.gameObject, () => "현재 상태", () => Enumerable.Range(0,unit.AttachedElements.Count()+unit.ActiveStatuses.Count).Select(index =>
                    UITooltip.Line.Note(BadgeName(unit,index) + " — " + BadgeDescription(unit,index),UITheme.TextPrimary)).ToList(), UITheme.Accent);
                TooltipTrigger.Attach(e.Plate.gameObject, () => unit.UnitName, () => new List<UITooltip.Line>
                {
                    UITooltip.Line.Note(CodexScreen.VitalsText(unit), UITheme.TextPrimary),
                    UITooltip.Line.Note("눌러서 캐릭터와 코드 상세 보기", UITheme.TextSecondary)
                }, UITheme.Accent);
                _entries.Add(e);
            }
        }
        /// <summary>파티 HUD 이름·수치는 960×540에서도 실제 10.5px 밑으로 자동 축소되지 않게 하한을 건다.</summary>
        private static TextMeshProUGUI Readable(TextMeshProUGUI label)
        {
            UIScaledText scaled = label.GetComponent<UIScaledText>();
            scaled.MinimumBaseSize = UITheme.FontReadFloor;
            // 행과 판의 크기는 고정한다. 최대 글자 설정도 자기 행 폭 안에서만 커지게 한다.
            scaled.ScaleMinimumWithTextSetting = false;
            scaled.Apply();
            return label;
        }
        private static string BadgeName(Unit unit,int index)
        {
            var elements=unit.AttachedElements.ToArray();
            if(index<elements.Length)return UnitInfoText.ElementName(elements[index].ToString())+" 원소 부여";
            index-=elements.Length;
            return index<unit.ActiveStatuses.Count ? unit.ActiveStatuses[index].StatusName : "상태";
        }
        private static string BadgeDescription(Unit unit,int index)
        {
            var elements=unit.AttachedElements.ToArray();
            if(index<elements.Length)return $"부착된 원소. 남은 일반행동 {unit.GetAttachedElementRemainingTurns(elements[index])}회. 다른 원소가 부착되면 반응의 재료가 됩니다.";
            index-=elements.Length;
            return index<unit.ActiveStatuses.Count ? unit.ActiveStatuses[index].StatusDescription : "";
        }
        private static void Refresh(Entry e, GameManager game)
        {
            Unit unit=e.Unit; if(unit==null)return;
            bool alive=unit.isActive;
            e.Group.alpha=alive ? unit.IsBench ? .58f : 1 : .40f;
            float hp=unit.HpMax>0 ? Mathf.Clamp01(unit.HpCurr/(float)unit.HpMax):0;
            e.Health.fillAmount=hp;
            e.Health.color=hp<=.30f ? UITheme.Danger : UITheme.Hp;
            float shield=unit.HpMax>0 ? Mathf.Clamp01(unit.ShieldCurr/(float)unit.HpMax):0;
            UIBuild.Anchor(e.Shield.rectTransform,new Vector2(0,.62f),new Vector2(shield,1));
            e.Shield.enabled=shield>0;
            // 보호막은 HP 막대 위 금색 선과 같은 의미색으로 붙인다. 같은 색이면 "+12"가 회복처럼 읽혔다.
            e.Hp.text=unit.ShieldCurr>0 ? $"{unit.HpCurr:N0} <color=#{ShieldTextHex}>+{unit.ShieldCurr:N0}</color>" : $"{unit.HpCurr:N0} / {unit.HpMax:N0}";
            float energy=unit.ManaMax>0 ? Mathf.Clamp01(unit.ManaCurr/(float)unit.ManaMax):0;
            bool passive=unit.UltimateResourceType==BaseEnums.UltimateResourceType.Passive;
            e.Energy.fillAmount=passive ? 0 : energy; e.Ring.fillAmount=passive ? 1 : energy;
            e.Resource.text=passive ? "상시" : unit.UltimateResourceType == BaseEnums.UltimateResourceType.Stack ? $"{unit.ManaCurr}/{unit.ManaMax}" : $"{energy*100:0}%";
            // 프레이아는 마나가 아닌 저장 온기가 핵심이므로 기존 자원 링에 실제 비율을 표시한다.
            if(unit.ID==82 && unit.GetCombatResourceMaximum(Codes.Ultimate.FreyaBrisingamen.Resource)>0)
            {
                int capacity=unit.GetCombatResourceMaximum(Codes.Ultimate.FreyaBrisingamen.Resource);
                float warmth=unit.GetCombatResource(Codes.Ultimate.FreyaBrisingamen.Resource)/(float)capacity;
                e.Energy.fillAmount=warmth; e.Ring.fillAmount=warmth; e.Resource.text=$"{warmth*100:0}%";
            }
            e.Ring.color=UITheme.Mana;
            bool acting=game.ActionScheduler?.ExecutingUnit==unit;
            e.Focus.enabled=acting;
            e.Root.anchoredPosition=new Vector2(e.Root.anchoredPosition.x, acting ? 7 : 0);
            var unique = new List<string>();
            foreach(string id in unit.CombatResourceIds)
            {
                int max=unit.GetCombatResourceMaximum(id); if(max<=0)continue;
                unique.Add($"{CombatResourceLabels.NameOf(id).Replace("창세의 결정","결정")} {unit.GetCombatResource(id)}/{max}");
            }
            e.Unique.text=!alive ? "전투불능" : unit.IsBench ? "대기석" : string.Join(" · ",unique);
            var statuses=unit.ActiveStatuses; var elements=unit.AttachedElements.ToArray();
            int total=statuses.Count+elements.Length;
            for(int i=0;i<e.Status.Length;i++)
            {
                e.Status[i].enabled=i<total;
                if(i<elements.Length)
                {e.Status[i].sprite=StatusIcons.Element(elements[i]);e.Status[i].color=Color.Lerp(ElementPalette.ColorFor(elements[i]),UITheme.TextPrimary,.30f);}
                else if(i<total)
                {e.Status[i].sprite=StatusIcons.For(statuses[i-elements.Length]);e.Status[i].color=UITheme.AccentDim;}
            }
            e.Overflow.text=total>4 ? "+"+(total-4):"";
        }
    }
}
