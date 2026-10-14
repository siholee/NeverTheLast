using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Entities;
using Helpers;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.Screens
{
    /// <summary>가로 보상 카드에서 효과를 비교하고, 선택 후 하단 확정 버튼으로 받는다.</summary>
    public class RewardScreen : ModalScreen
    {
        /// <summary>선택 카드와 대상 지정에 같은 전면 공간을 사용한다.</summary>
        private const float PickerHeight = 994f;

        /// <summary>선택 카드 사이 간격(기준 해상도 px).</summary>
        private const float RowGap = 18f;

        protected override Color BackdropTint => new Color(.77f,.84f,.92f,.98f);
        protected override string CanvasName => "RewardCanvas";
        protected override int SortingOrder => 70;
        protected override bool AlignToScreenFrame => false;
        protected override string Title => "보상 선택";
        protected override string Caption => "REWARD";
        protected override Vector2 AnchorMin => new(0.03f, 0.04f);
        protected override Vector2 AnchorMax => new(0.97f, 0.96f);

        private readonly List<Card> _cards = new();
        private List<RewardDef> _rewards = new();
        private RectTransform _cardsRoot;
        private ScrollRect _cardsScroll;
        private UnitTargetPicker _targetPicker;
        private RewardCodeFlow _codeFlow;
        private Button _reroll;
        private TextMeshProUGUI _rerollLabel;
        private RewardDef _pendingTargetReward;
        private RectTransform _footer;
        private Button _confirm;
        private Button _continue;
        private TextMeshProUGUI _selectionHint;
        private int _selectedIndex=-1;

        protected override void Build()
        {
            _cardsRoot = UIBuild.ScrollArea("RewardCards", Body, out _cardsScroll);
            RectTransform viewport = (RectTransform)_cardsScroll.transform;
            UIBuild.Stretch(viewport);
            viewport.offsetMin = new Vector2(0f, 92f);
            viewport.offsetMax = new Vector2(0f, -12f);
            _cardsScroll.vertical=false;
            _cardsScroll.verticalScrollbar.gameObject.SetActive(false);
            for (int i = 0; i < RewardManager.MaxRewardCount; i++)
            {
                _cards.Add(new Card(_cardsRoot, i, SelectCard));
            }

            _footer=UIBuild.Container("RewardFooter",Body);
            UIBuild.Anchor(_footer,Vector2.zero,new Vector2(1,0));
            _footer.pivot=new Vector2(.5f,0); _footer.sizeDelta=new Vector2(0,72);
            _selectionHint=UIBuild.Text("SelectionHint",_footer,"보상을 한 가지 선택하세요.",21,UITheme.TextSecondary,TextAlignmentOptions.MidlineLeft);
            UIBuild.Anchor(_selectionHint.rectTransform,Vector2.zero,new Vector2(.55f,1),6,0);
            _reroll=UIBuild.Button("Reroll",_footer,"다시 굴리기",OnReroll,false,21);
            UIBuild.Pin(_reroll.image.rectTransform,new Vector2(1,.5f),new Vector2(260,52),new Vector2(-272,0));
            _rerollLabel=_reroll.GetComponentInChildren<TextMeshProUGUI>();
            _confirm=UIBuild.Button("ConfirmReward",_footer,"선택 확정",()=>OnPick(_selectedIndex),true,22);
            UIBuild.Pin(_confirm.image.rectTransform,new Vector2(1,.5f),new Vector2(240,52),new Vector2(-8,0));
            _confirm.interactable=false;
            _continue=UIBuild.Button("FinishRoyalReward",_footer,"보상 선택 마치기",()=>GameManager.Instance?.rewardManager?.FinishRewardSelection(),false,20);
            UIBuild.Pin(_continue.image.rectTransform,new Vector2(1,.5f),new Vector2(260,52),new Vector2(-272,0));
            _continue.gameObject.SetActive(false);

            _targetPicker = new UnitTargetPicker(Body, OnTargetPicked, ShowRewardCards, "보상 목록으로 돌아가기");
            _codeFlow = new RewardCodeFlow(Body, ShowRewardCards, SetTitle, "보상 목록으로 돌아가기");
        }

        /// <summary>리롤 티켓 1장으로 선택지를 다시 굴린다(운명의 주사위, Detail_17 §6).</summary>
        private void OnReroll()
        {
            InventoryManager inventory = GameManager.Instance?.inventoryManager;
            RewardManager rewards = GameManager.Instance?.rewardManager;
            RoundManager round = GameManager.Instance?.RoundManager;
            if (inventory == null || rewards == null || !rewards.CanRerollSelection || inventory.rerollTicketCount <= 0) return;
            var rerolled = rewards.RerollRewards(_rewards.Count, round?.Round ?? 1);
            inventory.rerollTicketCount--;
            inventory.RefreshPanel();
            Show(rerolled);
        }

        private void RefreshReroll()
        {
            int tickets = GameManager.Instance?.inventoryManager?.rerollTicketCount ?? 0;
            _reroll.gameObject.SetActive(true);
            _reroll.interactable=tickets>0;
            if(tickets>0)_reroll.image.sprite=UIShapes.RoundedRect(10,Color.white,UITheme.Accent,1);
            FitContentHeight(994f);
            if (_rerollLabel != null) _rerollLabel.text=$"다시 굴리기 · {tickets}장";
            TooltipTrigger.Attach(_reroll.gameObject,()=>"리롤 티켓",()=>new[]{UITooltip.Line.Note("티켓 1장을 사용하여 모든 보상 선택지를 다시 뽑습니다.",UITheme.TextSecondary)},UITheme.Accent);

        }

        public void Show(List<RewardDef> rewards)
        {
            EnsureBuilt();
            UITooltip.HideAny();
            GuideTermTooltip.CloseAll();
            _rewards = rewards ?? new List<RewardDef>();
            GameManager.Instance?.rewardManager?.BeginRewardSelection(_rewards);
            _selectedIndex=-1;

            int shown=Mathf.Min(_rewards.Count,_cards.Count);
            base.Show();
            FitContentHeight(994f);
            Canvas.ForceUpdateCanvases();
            float cardHeight=Mathf.Min(640f,_cardsScroll.viewport.rect.height);
            _cardsRoot.sizeDelta=new Vector2(0,cardHeight);
            _cardsRoot.anchoredPosition=new Vector2(0,-Mathf.Max(0,(_cardsScroll.viewport.rect.height-cardHeight)*.5f));
            for(int i=0;i<_cards.Count;i++)
            {
                if(i<shown)_cards[i].Layout(i,0,Mathf.Max(1,shown),1);
                _cards[i].Bind(i<shown?_rewards[i]:null);
                _cards[i].SetSelected(false);
            }

            ShowRewardCards();
            Show();
        }

        private void SelectCard(int index)
        {
            if(index<0||index>=_rewards.Count || !GameManager.Instance.rewardManager.CanClaimReward(_rewards[index]))return;
            _selectedIndex=index;
            for(int i=0;i<_cards.Count;i++)_cards[i].SetSelected(i==index);
            _selectionHint.text=_rewards[index].displayName+" · 선택됨";
            RefreshPurchaseState();
        }

        private void OnPick(int index)
        {
            if (index < 0 || index >= _rewards.Count || !GameManager.Instance.rewardManager.CanClaimReward(_rewards[index])) return;

            RewardDef reward = _rewards[index];
            UITooltip.HideAny();
            _codeFlow.Note = PurchaseNote(reward);
            if (reward.IsCodeReward)
            {
                _cardsRoot.gameObject.SetActive(false);
                _footer.gameObject.SetActive(false);
                // 보상 비교와 대상 선택은 같은 전면 공간에서 이어진다.
                FitContentHeight(PickerHeight);
                bool opened = _codeFlow.StartReceive(reward.codeId, reward.displayName,
                    (unit, overwrite) => ClaimAndRefresh(reward, unit, overwrite));
                if (!opened) ShowRewardCards();
                return;
            }

            if (reward.RequiresTargetSelection)
            {
                ShowRewardTargets(reward);
                return;
            }

            // 대상 지정이 필요 없는 보상은 기존 동작대로 필드 위 첫 아군을 문맥 대상으로 사용한다.
            Unit target = GridManager.Instance?.heroList
                ?.FirstOrDefault(hero => hero != null && hero.isActive && !hero.IsEnemy);
            ClaimAndRefresh(reward, target);
        }

        private void ShowRewardTargets(RewardDef reward)
        {
            List<Unit> candidates = TargetCandidates(reward);
            if (candidates.Count == 0) return;

            _pendingTargetReward = reward;
            _footer.gameObject.SetActive(false);
            _cardsRoot.gameObject.SetActive(false);
            FitContentHeight(PickerHeight);
            SetTitle(reward.displayName + " — 대상 선택");
            string note = PurchaseNote(reward);
            _targetPicker.Show(candidates, TargetGuide(reward) + (string.IsNullOrEmpty(note) ? "" : "\n" + note), unit => TargetPreview(reward, unit));
        }

        private string PurchaseNote(RewardDef reward)
        {
            var game=GameManager.Instance;
            return game.rewardManager.FreeRewardClaimed
                ? $"가격 {game.rewardManager.SelectionPrice(reward):N0}G · 보유 {game.inventoryManager.Gold:N0}G · 왕실 구매 {game.rewardManager.PaidRewardCount}/2"
                : "";
        }

        /// <summary>대상 선택 판의 안내문. 상점도 같은 문구를 쓴다.</summary>
        public static string TargetGuide(RewardDef reward)
        {
            if (reward.IsExpGrant)
                return "경험치를 받을 아군을 선택하세요. 받는 양을 함께 표시합니다.";
            if (reward.RequiresLevelTargetSelection)
                return "레벨을 올릴 아군을 선택하세요. 오른 뒤의 레벨을 함께 표시합니다.";

            return reward.RequiresReviveTargetSelection
                ? "부활시킬 아군을 선택하세요. 부활 후 체력을 함께 표시합니다."
                : "회복할 아군을 선택하세요. 현재 체력과 회복 후 체력을 함께 표시합니다.";
        }

        /// <summary>카드 아래 한 줄. 쓰고 나면 체력이 어떻게 되는지를 미리 보여 준다.</summary>
        public static string TargetPreview(RewardDef reward, Unit unit)
        {
            if (reward.IsExpGrant)
            {
                int exp = Mathf.Max(1, Mathf.RoundToInt(unit.ExpToNextLevel * reward.expPercentOfNext));
                return $"Lv.{unit.Level}\nEXP +{exp:N0}";
            }
            if (reward.RequiresLevelTargetSelection)
            {
                return $"Lv.{unit.Level}\n→ Lv.{unit.Level + reward.levelGrant}";
            }

            if (reward.RequiresReviveTargetSelection)
            {
                int after = RewardManager.CalculateReviveHp(reward, unit);
                return $"전투 불능\n→ HP {after:N0} / {unit.HpMax:N0}";
            }

            int amount = RewardManager.CalculateHealingAmount(reward, unit);
            int healed = Mathf.Min(unit.HpMax, unit.HpCurr + amount);
            return $"HP {unit.HpCurr:N0} / {unit.HpMax:N0}\n→ {healed:N0} / {unit.HpMax:N0}";
        }

        /// <summary>회복·부활 대상 후보. 상점도 같은 조건을 쓴다.</summary>
        public static List<Unit> TargetCandidates(RewardDef reward)
        {
            if (reward.RequiresLevelTargetSelection) return RewardManager.LevelGrantCandidates();

            return GridManager.Instance?.heroList?
                .Where(hero => reward.RequiresReviveTargetSelection
                    ? RewardManager.IsValidReviveTarget(hero)
                    : RewardManager.IsValidHealingTarget(hero) && hero.HpCurr < hero.HpMax)
                .ToList() ?? new List<Unit>();
        }

        private void OnTargetPicked(Unit target)
        {
            if (_pendingTargetReward == null) return;
            bool valid = _pendingTargetReward.RequiresLevelTargetSelection
                ? RewardManager.IsValidLevelTarget(target)
                : _pendingTargetReward.RequiresReviveTargetSelection
                    ? RewardManager.IsValidReviveTarget(target)
                    : RewardManager.IsValidHealingTarget(target);
            if (!valid) return;
            ClaimAndRefresh(_pendingTargetReward, target);
            _pendingTargetReward = null;
        }

        private void ShowRewardCards()
        {
            _pendingTargetReward = null;
            SetTitle(Title);
            if (_cardsRoot != null) _cardsRoot.gameObject.SetActive(true);
            _targetPicker?.SetActive(false);
            _codeFlow?.Hide();
            _footer.gameObject.SetActive(true);
            _confirm.interactable=_selectedIndex>=0;
            _selectionHint.text=_selectedIndex>=0?_rewards[_selectedIndex].displayName+" · 선택됨":"보상을 한 가지 선택하세요.";
            if (_reroll != null) RefreshReroll();
            RefreshPurchaseState();
        }

        private void ClaimAndRefresh(RewardDef reward, Unit target, int overwrite = 0)
        {
            var manager=GameManager.Instance?.rewardManager;
            if(manager==null)return;
            if(!manager.TryClaimReward(reward,target,overwrite)) { ShowRewardCards(); return; }
            if(!manager.RoyalSelection) { manager.FinishRewardSelection(); return; }
            _selectedIndex=-1;
            for(int i=0;i<_cards.Count;i++)_cards[i].SetSelected(false);
            ShowRewardCards();
        }
        private void RefreshPurchaseState()
        {
            var manager=GameManager.Instance?.rewardManager;if(manager==null)return;
            bool paid=manager.FreeRewardClaimed;
            for(int i=0;i<_rewards.Count && i<_cards.Count;i++)
                _cards[i].SetPurchaseState(manager.IsClaimed(_rewards[i]),paid?RewardManager.ShopPrice(_rewards[i],GameManager.Instance.RoundManager.Stage):0);
            _continue.gameObject.SetActive(paid && manager.RoyalSelection);
            _reroll.gameObject.SetActive(!paid);
            _reroll.interactable=manager.CanRerollSelection && (GameManager.Instance.inventoryManager?.rerollTicketCount??0)>0;
            bool selected=_selectedIndex>=0 && manager.CanClaimReward(_rewards[_selectedIndex]);
            _confirm.interactable=selected;
            _confirm.GetComponentInChildren<TextMeshProUGUI>().text=paid?"추가 구매":"선택 확정";
            _selectionHint.text=paid?$"왕실의 구매권 · {manager.PaidRewardCount}/2 구매 · 보유 {GameManager.Instance.inventoryManager.Gold:N0}G":
                _selectedIndex>=0?_rewards[_selectedIndex].displayName+" · 선택됨":"무료 보상을 한 가지 선택하세요.";
        }

        // ── 표시할 값 뽑기 ───────────────────────────────────────────

        private readonly struct Row
        {
            public readonly string Label;
            public readonly string Value;
            public readonly Color Color;

            public Row(string label, string value, Color color)
            {
                Label = label;
                Value = value;
                Color = color;
            }
        }

        private static List<Row> StatRows(RewardDef reward)
        {
            var rows = new List<Row>();
            if (reward == null) return rows;

            if (reward.IsCodeReward)
            {
                // 코드 카드는 태그 칩이 "누구에게 맞는가"를, 상위 표시가 "지금 사다리를 오를 수 있는가"를 먼저 말한다(Detail_17 §3 · §2.3).
                rows.Add(new Row("코드", $"T{Mathf.Max(1, reward.tier)} 보상 코드", UITheme.Rarity(Mathf.Max(1, reward.tier))));
                string tags = string.Join(" · ", UnitTagCatalog.Resolve(Codes.Base.RewardCodeCatalog.TagsOf(reward.codeId))
                    .Select(tag => tag.Name));
                if (tags.Length > 0) rows.Add(new Row("태그", tags, UITheme.Accent));
                string lower = LowerHeldByParty(reward.codeId);
                if (lower != null) rows.Add(new Row("▲ 상위", lower + " 대체", UITheme.Positive));
                int upper = Codes.Base.RewardCodeCatalog.UpperOf(reward.codeId);
                if (upper > 0) rows.Add(new Row("상위 코드", RewardCodeFlow.NameOf(upper), UITheme.TextMuted));
                return rows;
            }

            if (reward.item != null)
            {
                if (reward.item.IsFood)
                    rows.Add(new Row("공용 식량", $"+{reward.item.foodValue}", UITheme.Accent));
                else if (reward.item.IsValuable)
                    rows.Add(new Row("판매품", "공용 주머니", UITheme.Positive));
                return rows;
            }

            // 즉시 적용 보상. RewardManager.ApplyRewardEffect의 우선순위를 그대로 따라간다 —
            // 카드가 실제로 일어날 일만 약속하게 하려는 것이다.
            if (reward.fullHealParty)
            {
                rows.Add(new Row("파티 전체", "완전 회복", UITheme.Hp));
                return rows;
            }

            if (reward.fullReviveParty)
            {
                rows.Add(new Row("파티 전체", "체력 100%로 부활", UITheme.Hp));
                return rows;
            }

            if (reward.revivePercent > 0f)
            {
                rows.Add(new Row("지정 아군", $"체력 {Mathf.RoundToInt(reward.revivePercent * 100f)}%로 부활", UITheme.Hp));
                return rows;
            }

            if (reward.fullHealTarget)
            {
                rows.Add(new Row("지정 아군", "완전 회복", UITheme.Hp));
                return rows;
            }

            if (reward.healPercent > 0f)
            {
                rows.Add(new Row("지정 아군", $"+{Mathf.RoundToInt(reward.healPercent * 100f)}%", UITheme.Hp));
                return rows;
            }

            if (reward.healAmount > 0)
            {
                rows.Add(new Row("파티 회복", "+" + reward.healAmount, UITheme.Hp));
                return rows;
            }

            if (reward.randomTokenAmount > 0)
            {
                rows.Add(new Row("무작위 토큰", reward.randomTokenAmount + "개", UITheme.Accent));
                return rows;
            }

            if (reward.rerollTicketBonus > 0)
            {
                rows.Add(new Row("리롤권", "+" + reward.rerollTicketBonus, UITheme.Accent));
                return rows;
            }

            return rows;
        }

        /// <summary>파티 누군가 가진 하위 코드의 이름. 없으면 null — 상위 카드에 "▲ 상위: ○○"를 띄운다.</summary>
        private static string LowerHeldByParty(int codeId)
        {
            foreach (Unit hero in RewardCodeFlow.Party())
            {
                int lower = hero.RewardCodeIds.FirstOrDefault(owned => Codes.Base.RewardCodeCatalog.IsUpperOf(codeId, owned));
                if (lower > 0) return $"{hero.UnitName}의 {RewardCodeFlow.NameOf(lower)}";
            }
            return null;
        }

        private static string FitText(RewardDef reward)
        {
            ItemData item = reward?.item;
            if (item == null) return "";
            return item.IsFood
                ? $"긴 휴식 비용 {GameManager.LongRestFoodCost}에 보탠다."
                : "획득 즉시 공용 인벤토리에 보관된다.";
        }

        private static string Hex(Color color) => ColorUtility.ToHtmlStringRGB(color);

        /// <summary>등급 · 부위 · 분류를 한 줄로. 세 장이 같은 자리에서 비교된다.</summary>
        private static string MetaLine(RewardDef reward)
        {
            int tier = Mathf.Max(1, reward.tier);
            var parts = new List<string> { $"TIER {tier}" };

            if (!string.IsNullOrWhiteSpace(reward.item?.category)) parts.Add(ItemTooltip.CategoryName(reward.item.category));
            if (reward.isRare) parts.Add("RARE");

            return string.Join(" · ", parts);
        }

        /// <summary>한 줄에 놓인 선택 카드. 긴 설명은 카드 안에서 읽고 최종 획득은 공용 버튼으로 확정한다.</summary>
        private sealed class Card
        {
            private readonly Image _root, _art, _tile, _selection;
            private readonly TextMeshProUGUI _name, _meta, _summary;
            private readonly ScrollRect _descriptionScroll;
            public Card(Transform parent,int index,Action<int> onPick)
            {
                _root=UIBuild.Solid($"Reward{index}",parent,Color.white);
                _root.sprite=UIArtDirection.Sheet(12);
                UIBuild.OnClick(_root.gameObject,()=>onPick(index));
                UICardReveal.Attach(_root.gameObject,index);
                _tile=UIBuild.Solid("ArtTile",_root.transform,Color.white);
                _tile.sprite=UIArtDirection.Seal(1); _tile.type=Image.Type.Simple;_tile.preserveAspect=true;_tile.raycastTarget=false;
                UIBuild.Pin(_tile.rectTransform,new Vector2(.5f,1),new Vector2(112,112),new Vector2(0,-38));
                _art=UIBuild.Solid("Art",_tile.transform,UITheme.Accent);
                _art.type=Image.Type.Simple;_art.preserveAspect=true;_art.raycastTarget=false;
                UIBuild.Pin(_art.rectTransform,new Vector2(.5f,.5f),new Vector2(52,52),Vector2.zero);
                _name=UIBuild.Text("Name",_root.transform,"",25,UITheme.TextPrimary,TextAlignmentOptions.Midline,true);
                _name.fontStyle=FontStyles.Bold;
                PlaceTop(_name.rectTransform,160,82,24);
                _meta=UIBuild.Text("Meta",_root.transform,"",20,UITheme.TextMuted,TextAlignmentOptions.Midline);
                PlaceTop(_meta.rectTransform,246,34,24);
                GameTermLinks.Attach(_meta);
                Image rule=UIBuild.Divider("Rule",_root.transform);
                PlaceTop(rule.rectTransform,302,1,32); rule.raycastTarget=false;
                RectTransform content=UIBuild.ScrollArea("RewardDescription",_root.transform,out _descriptionScroll);
                UIBuild.Stretch((RectTransform)_descriptionScroll.transform,28,0);
                ((RectTransform)_descriptionScroll.transform).offsetMin=new Vector2(28,30);
                ((RectTransform)_descriptionScroll.transform).offsetMax=new Vector2(-28,-324);
                _summary=UIBuild.Text("Summary",content,"",22,UITheme.TextPrimary,TextAlignmentOptions.TopLeft,true);
                _summary.overflowMode=TextOverflowModes.Overflow;
                _summary.richText=true;
                PlaceTop(_summary.rectTransform,0,0,8);
                _selection=UIBuild.Solid("Selected",_root.transform,UITheme.Accent);
                _selection.sprite=UIShapes.RoundedRect(18,Color.white);
                UIBuild.Pin(_selection.rectTransform,new Vector2(1,1),new Vector2(36,36),new Vector2(-20,-20));
                _selection.raycastTarget=false;
                var check=UIBuild.Icon("Check",_selection.transform,UIIcon.Check,Color.white);
                UIBuild.Stretch(check.rectTransform,9,9);
                _selection.gameObject.SetActive(false);
            }
            private static void PlaceTop(RectTransform rect,float top,float height,float pad)
            {
                UIBuild.Anchor(rect,new Vector2(0,1),Vector2.one);
                rect.pivot=new Vector2(.5f,1);
                rect.offsetMin=new Vector2(pad,-top-height);rect.offsetMax=new Vector2(-pad,-top);
            }
            public void Layout(int column,int row,int columns,int rows)
            {
                RectTransform rect=_root.rectTransform;
                rect.anchorMin=new Vector2(column/(float)columns,0);rect.anchorMax=new Vector2((column+1f)/columns,1);
                rect.offsetMin=new Vector2(RowGap*.5f,4);rect.offsetMax=new Vector2(-RowGap*.5f,-4);
            }
            public void SetSelected(bool selected)
            {
                _root.sprite=selected?UIShapes.RoundedRect(12,Color.Lerp(Color.white,UITheme.Accent,.10f),UITheme.Accent,1):UIArtDirection.Sheet(12);
                _selection.gameObject.SetActive(selected);
            }
            public void SetPurchaseState(bool claimed, int price)
            {
                var click=_root.GetComponent<Button>(); if(click!=null)click.interactable=!claimed;
                _root.color=claimed?new Color(.78f,.82f,.88f,1):Color.white;
                _meta.text=claimed?"획득 완료":price>0?$"상점 가격 · {price:N0}G":_meta.text;
            }
            public void Bind(RewardDef reward)
            {
                _root.gameObject.SetActive(reward!=null);
                if(reward==null)return;
                var reveal=_root.GetComponent<UICardReveal>();
                reveal.Replay();
                _name.text=reward.displayName;
                _meta.text=reward.IsCodeReward ? $"T{reward.tier} · 보상 코드" : MetaLine(reward);
                List<Row> stats=StatRows(reward);
                _summary.text=reward.IsCodeReward ? reward.description+"\n\n"+string.Join("\n",stats.Where(r=>r.Label!="코드").Select(r=>$"<color=#{ColorUtility.ToHtmlStringRGB(UITheme.TextSecondary)}>{r.Label}</color>  {r.Value}"))+"\n"+FitText(reward) :
                    reward.description+"\n\n"+string.Join("\n",stats.Select(r=>$"{r.Label}  {r.Value}"))+"\n"+FitText(reward);
                Canvas.ForceUpdateCanvases();
                float width=Mathf.Max(100,_descriptionScroll.viewport.rect.width-16);
                float height=_summary.GetPreferredValues(_summary.text,width,0).y;
                _summary.rectTransform.sizeDelta=new Vector2(-16,height);
                // 짧은 설명은 본문 영역 중앙에 두고, 긴 설명은 위에서 시작해 내부 스크롤한다.
                _summary.rectTransform.anchoredPosition = new Vector2(0, -Mathf.Max(0, (_descriptionScroll.viewport.rect.height - height) * .5f));
                _descriptionScroll.content.sizeDelta=new Vector2(0,Mathf.Max(_descriptionScroll.viewport.rect.height,height+12));
                _descriptionScroll.verticalNormalizedPosition=1;
                _art.sprite=reward.IsCodeReward ? UILineIcons.Get(UIArtDirection.CodeIcon(reward.codeId)) :
                    reward.item!=null ? ItemTooltip.LoadArt(reward.item) :
                    string.IsNullOrWhiteSpace(reward.artPath) ? null : Resources.Load<Sprite>(reward.artPath);
                bool lineIcon = reward.IsCodeReward;
                if (_art.sprite == null)
                {
                    // 원화가 없는 소모품도 빈 각인이 아니라 기능을 읽을 수 있는 공용 기호를 쓴다.
                    UIIcon fallback = reward.item?.IsTonic == true || reward.item?.IsNectar == true ? UIIcon.Flask :
                        reward.item?.IsCodeTool == true ? UIIcon.Growth :
                        reward.IsHealingReward || reward.IsRevivalReward ? UIIcon.Heart : UIIcon.Supply;
                    _art.sprite = UILineIcons.Get(fallback);
                    lineIcon = true;
                }
                _art.enabled=_art.sprite!=null;
                _art.color=lineIcon ? UITheme.Accent : Color.white;
                // 코드는 선 기호를 각인 안에, 물건 그림은 각인을 채우게 둔다.
                _art.rectTransform.sizeDelta=lineIcon ? new Vector2(52f,52f) : new Vector2(84f,84f);
                _tile.sprite=UIArtDirection.Seal(Mathf.Max(1,reward.tier));
                _tile.raycastTarget=false;
            }

        }
    }
}
