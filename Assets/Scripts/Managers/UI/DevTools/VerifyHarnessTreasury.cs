#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Passive;
using Codes.Ultimate;
using Combat;
using Core;
using Effects.Buffs;
using Entities;
using Managers.UI.Core;
using UnityEngine;
namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator TreasuryCast(Unit actor, Action start)
        {
            start(); float deadline=Time.realtimeSinceStartup+20;
            while(actor.isCasting && Time.realtimeSinceStartup<deadline)yield return null;
            RuiCheck("재보 행동 완료", "제한시간 내 종료",actor.isCasting.ToString(),!actor.isCasting);
            yield return WaitUnscaled(.1f);
        }
        private IEnumerator ScenarioTreasuryCore(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear(); bool enabled=true;yield return YvPrepareScene(config,x=>enabled=x);
            var game=GameManager.Instance;var kb=AliveAllies().Single(u=>u.ID==65);var m=AliveAllies().Single(u=>u.ID==200);var ish=AliveAllies().Single(u=>u.ID==220);var enemy=AliveEnemies().First();
            var passive=ish.ActivePassiveCodes.OfType<IshtarGoldenMagazine>().Single();
            foreach(var unit in AliveAllies().Where(u=>u!=kb&&u!=m&&u!=ish))foreach(var code in unit.ActivePassiveCodes)code.StopCode();
            RuiCheck("캐릭터 등록", "마나120/100/140·풀·공격형 차이",$"{kb.ManaMax}/{m.ManaMax}/{ish.ManaMax} {ish.Element}",
                kb.ManaMax==120 && m.ManaMax==100 && ish.ManaMax==140 && ish.Element=="Dendro" && !m.ActiveNormalCode.CanJoinCommand && ish.ActiveNormalCode.CanJoinCommand);
            var barrageDefinition=Effects.Vfx.VfxDefinitionCatalog.Skill("ishtar.barrage");
            RuiCheck("이슈타르 지정 연출 등록", "조합/3부품 모두 유효",(barrageDefinition?.slots?.Count??0).ToString(),barrageDefinition?.slots?.Count==3&&barrageDefinition.slots.All(slot=>Effects.Vfx.VfxDefinitionCatalog.Part(slot.partId)!=null));
            PartyTreasury.BeginBattle();
            RuiCheck("아군 공용 초기·구매권", "0·시작 편성 구매권",PartyTreasury.Total.ToString(),PartyTreasury.Total==0&&PartyTreasury.RoyalPurchaseEntitled);
            RuiCheck("적 재보 접근 차단","충전0·소비실패",PartyTreasury.Charge(enemy,99).ToString(),PartyTreasury.Charge(enemy,1)==0&&!PartyTreasury.Spend(enemy,1));
            PartyTreasury.Charge(kb,99);RuiCheck("공유 상한", "12",PartyTreasury.Total.ToString(),PartyTreasury.Available==12);
            using(var magazine=PartyTreasury.Reserve(ish))
            {
                RuiCheck("예약은 상한 점유", "사용0 예약12·충전0",$"{PartyTreasury.Available}/{PartyTreasury.Reserved}",PartyTreasury.Available==0&&PartyTreasury.Reserved==12&&PartyTreasury.Charge(m,6)==0);
                magazine.SpendOne();RuiCheck("실제 소비 스택", "1",passive.Stacks.ToString(),passive.Stacks==1);
                PartyTreasury.Charge(m,1);RuiCheck("발사만 상한 공간 개방", "합12·별도신규1",PartyTreasury.Total.ToString(),PartyTreasury.Total==12&&PartyTreasury.Available==1);
            }
            RuiCheck("미발사 반환은 소비 아님", "합12·스택1",$"{PartyTreasury.Total}/{passive.Stacks}",PartyTreasury.Available==12&&PartyTreasury.Reserved==0&&passive.Stacks==1);
            ish.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved,new EventContext(ish));
            RuiCheck("자기 일반 종료 만료", "스택0·상태없음",passive.Stacks.ToString(),passive.Stacks==0&&!ish.HasStatusKey("ishtar_magazine"));
            PartyTreasury.BeginBattle();using(var old=PartyTreasury.Reserve(ish)){} // 0 예약은 null 허용
            foreach(int amount in new[]{0,3,4,8,12})
            {
                PartyTreasury.BeginBattle();PartyTreasury.Charge(kb,amount);kb.FillUltimateResource();
                yield return TreasuryCast(kb,kb.CastUltimateCode);
                int expected=Math.Min(12,amount+1+amount/4);
                RuiCheck("이자 경계 "+amount,expected.ToString(),PartyTreasury.Available.ToString(),PartyTreasury.Available==expected&&kb.ManaCurr==0);
            }
            PartyTreasury.BeginBattle();using(var recorder=new YvRecorder())
            {
                yield return TreasuryCast(kb,kb.CastNormalCode);
                RuiCheck("금화 생산과 공격 거래","재보1·피해표지",PartyTreasury.Available.ToString(),PartyTreasury.Available==1&&recorder.Incoming.Any(x=>x.Attacker==kb&&x.IsTreasuryAttack));
            }
            int before=PartyTreasury.Available;
            yield return kb.ActiveNormalCode.PerformCommandedAttack(enemy,ish);
            RuiCheck("지휘 사격 생산 금지",before.ToString(),PartyTreasury.Available.ToString(),PartyTreasury.Available==before);
            PartyTreasury.BeginBattle();int mana=m.ManaCurr;int shield=m.ShieldCurr;
            yield return TreasuryCast(m,m.CastNormalCode);
            RuiCheck("재보0 방호도 정상 마나", "마나+20·보호막불변",$"{m.ManaCurr-mana}/{m.ShieldCurr-shield}",m.ManaCurr-mana==20&&m.ShieldCurr==shield);
            m.FillUltimateResource();float atk=ish.StatValue(BaseEnums.StatType.Attack);float defense=m.StatValue(BaseEnums.StatType.Defense);
            yield return TreasuryCast(m,m.CastUltimateCode);
            RuiCheck("DEF 공급과 버프", "공급계산·ATK증가",$"{PartyTreasury.Available}/{ish.AtkCurr}",PartyTreasury.Available==MansaGoldenProcession.Income(defense)&&ish.StatValue(BaseEnums.StatType.Attack)>atk);
            int charged=PartyTreasury.Available;int oldShield=ish.ShieldCurr;
            yield return TreasuryCast(m,m.CastNormalCode);
            RuiCheck("누적 DEF 보호막·1 소비","소비1·보호막",$"{PartyTreasury.Available}/{ish.ShieldCurr-oldShield}",PartyTreasury.Available==charged-1&&ish.ShieldCurr-oldShield==Mathf.RoundToInt(defense*.8f));
            ish.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved,new EventContext(ish));
            RuiCheck("지원 대상 턴은 만사 기간 제외","ATK 유지",ish.AtkCurr.ToString(),ish.StatValue(BaseEnums.StatType.Attack)>atk);
            yield return TreasuryCast(m,m.CastNormalCode);
            RuiCheck("만사 2회 종료 만료","ATK 원복",ish.AtkCurr.ToString(),Mathf.Approximately(ish.StatValue(BaseEnums.StatType.Attack),atk));
            PartyTreasury.BeginBattle();PartyTreasury.Charge(kb,3);ish.FillUltimateResource();int ultEvents=0;
            var shotBonuses = new List<float>();
            Action<Unit,DamageContext> stackProbe=(target,ctx)=>{if(ctx.Attacker==ish && ctx.IsTreasuryAttack)shotBonuses.Add(ish.CombatEffects().Sum(e=>e.UltimateDamageBonus(ish)));};
            ish.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved,new EventContext(ish));
            Unit.AnyDamageIncoming+=stackProbe;
            Action<Unit> ult=x=>{if(x==ish)ultEvents++;};Unit.AnyUltimateCast+=ult;
            using(var record=new YvRecorder())
            {
                yield return TreasuryCast(ish,ish.CastUltimateCode);
                var hits=record.Incoming.Where(x=>x.Attacker==ish&&x.CodeType==BaseEnums.CodeType.Ultimate&&!x.HasTag(DamageTag.ElementalReaction)).ToList();
                RuiCheck("3탄 초월기 1회·원소1회", "3피해·1발동·잔고0·스택3",$"{hits.Count}/{ultEvents}/{PartyTreasury.Total}/{passive.Stacks}",hits.Count==3&&ultEvents==1&&PartyTreasury.Total==0&&passive.Stacks==3&&hits.All(x=>x.IsTreasuryAttack)&&record.Elements.Count(x=>x.Source==ish)<=1);
                RuiCheck("탄창 현재 탄부터 증가", ".04/.08/.12",string.Join(",",shotBonuses),shotBonuses.Count==3&&shotBonuses.Select((x,i)=>Mathf.Abs(x-.04f*(i+1))<.001f).All(x=>x));
            }
            Unit.AnyDamageIncoming-=stackProbe;
            ish.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved,new EventContext(ish));
            PartyTreasury.BeginBattle();PartyTreasury.Charge(kb,12);ish.FillUltimateResource();
            int barrageVisuals=0;
            Action<Effects.Vfx.VfxSlotPlaybackEvent> barrageVisualProbe=x=>{if(x.SkillKey=="ishtar.barrage"&&x.PartId=="hit.kubera.coin")barrageVisuals++;};
            Effects.Vfx.VfxComposer.SlotPlayed+=barrageVisualProbe;
            using(var record=new YvRecorder())
            {
                yield return TreasuryCast(ish,ish.CastUltimateCode);
                RuiCheck("12발 상한·초월 행동1회", "12타격·12층·1추가발동·잔고0",$"{record.Incoming.Count(x=>x.Attacker==ish&&x.IsTreasuryAttack)}/{passive.Stacks}/{ultEvents}",
                    record.Incoming.Count(x=>x.Attacker==ish&&x.IsTreasuryAttack)==12&&passive.Stacks==12&&ultEvents==2&&PartyTreasury.Total==0);
            }
            Effects.Vfx.VfxComposer.SlotPlayed-=barrageVisualProbe;
            RuiCheck("실제 포격 지정 피격 연출", "12발 지정 부품 재생",barrageVisuals.ToString(),barrageVisuals>=12);
            enemy.DebugClearStatuses();
            // 12연사 뒤의잔여 체력은 공식 검증량보다 작을 수 있다. 초과 피해를 실피해로 오인하지 않는다.
            YvBoostEnemy(enemy);
            enemy.RestoreHpOutsideCombat(enemy.HpMax);
            ish.AddStatus(BuffStatus.Create(72901,"qa.ult.additive","검증 초월 증폭",ish,ish,new UltimateDamageBonusEffect(.20f),0));
            var additiveProbe=new DamageContext(ish,1000,BaseEnums.CodeType.Ultimate,new List<int>{DamageTag.UltAttack,DamageTag.NonContactAttack,DamageTag.Shot}){DefenseStatMultiplier=0};
            enemy.TakeDamage(additiveProbe);
            RuiCheck("초월 피해 증가 분류 가산", "1000×(1+.48+.20)=1680",additiveProbe.ResolvedDamage.ToString(),additiveProbe.ResolvedDamage==1680);
            ish.RemoveStatusByKey("qa.ult.additive");
            bool receivedUltCode=ish.TryGrantRewardCode(723);
            ish.ActivePassiveCodes.OfType<Codes.Passive.Reward.ThunderStrikeCode>().Single().CastCode();
            var rewardBonusProbe=new DamageContext(ish,1000,BaseEnums.CodeType.Ultimate,new List<int>{DamageTag.UltAttack,DamageTag.NonContactAttack,DamageTag.Shot}){DefenseStatMultiplier=0};
            enemy.TakeDamage(rewardBonusProbe);
            RuiCheck("기존 T3 초월 코드와 가산", "1000×(1+.48+.25)=1730",rewardBonusProbe.ResolvedDamage.ToString(),receivedUltCode&&rewardBonusProbe.ResolvedDamage==1730);
            ish.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved,new EventContext(ish));
            PartyTreasury.BeginBattle();PartyTreasury.Charge(kb,3);ish.FillUltimateResource();ish.isControlled=true;
            yield return TreasuryCast(ish,ish.CastUltimateCode);ish.isControlled=false;
            RuiCheck("첫 탄 전 취소", "마나140·재보3·발동불변",$"{ish.ManaCurr}/{PartyTreasury.Total}/{ultEvents}",ish.ManaCurr==ish.ManaMax&&PartyTreasury.Available==3&&ultEvents==2);
            Unit.AnyUltimateCast-=ult;
            PartyTreasury.BeginBattle();PartyTreasury.Charge(kb,6);ish.FillUltimateResource();int shots=0;
            Action<Unit,DamageContext> interrupt=(target,ctx)=>{if(ctx.Attacker!=ish||ctx.CodeType!=BaseEnums.CodeType.Ultimate)return;shots++;ish.ActiveUltimateCode.StopCode();};
            Unit.AnyDamageIncoming+=interrupt;yield return TreasuryCast(ish,ish.CastUltimateCode);Unit.AnyDamageIncoming-=interrupt;
            RuiCheck("연사 중단 미발사 반환", "1탄소비·5반환·예약0",$"{shots}/{PartyTreasury.Available}/{PartyTreasury.Reserved}",shots==1&&PartyTreasury.Available==5&&PartyTreasury.Reserved==0);
            PartyTreasury.BeginBattle();PartyTreasury.Charge(kb,3);
            using(var rec=new YvRecorder())
            {
                yield return TreasuryCast(kb,kb.CastSpecialCode);
                RuiCheck("강화 특수 비용3", "0·재보공격",PartyTreasury.Available.ToString(),PartyTreasury.Available==0&&rec.Incoming.Any(x=>x.Attacker==kb&&x.IsTreasuryAttack));
            }
            PartyTreasury.Charge(kb,2);
            using(var rec=new YvRecorder())
            {
                yield return TreasuryCast(kb,kb.CastSpecialCode);
                RuiCheck("부족 특수 부분소비 금지", "2·기본공격",PartyTreasury.Available.ToString(),PartyTreasury.Available==2&&rec.Incoming.Where(x=>x.Attacker==kb).All(x=>!x.IsTreasuryAttack));
            }
            RuiCheck("치피 원본 재귀 없음", ".15",KuberaPatronage.AdditionalCritDamage(ish).ToString(),Mathf.Approximately(KuberaPatronage.AdditionalCritDamage(ish),kb.StatValue(BaseEnums.StatType.CritDamage)*.3f));
            RuiCheck("소환 후원 상태 제거", "후원/보고개방상태0",kb.GetStatuses().Count.ToString(),!AliveAllies().Any(x=>x.HasStatusKey("kubera_patronage")||x.HasStatusKey("kubera_treasury")));
            PartyTreasury.BeginBattle();PartyTreasury.Charge(kb,4);
            bool mansaAlive=m.isActive;m.isActive=false;
            RuiCheck("공급자 사망 잔고·자격 보존", "잔고4·구매권·추가충전0",PartyTreasury.Total.ToString(),PartyTreasury.Available==4&&PartyTreasury.RoyalPurchaseEntitled&&PartyTreasury.Charge(m,1)==0);
            m.isActive=mansaAlive;
            var stale=PartyTreasury.Reserve(ish);PartyTreasury.EndBattle();stale.Dispose();
            RuiCheck("종료·오래된예약 격리", "0·사용불가",PartyTreasury.Total.ToString(),PartyTreasury.Total==0&&!PartyTreasury.Spend(kb,1));
            // 실제 보상 화면과 동일한 권한/카드/대상 최종 확정 경로.
            game.ActionScheduler.EndRound();game.gameState=BaseEnums.GameState.RewardSelection;
            var rewards=game.rewardManager;rewards.ResetRewardSelection();var inventory=game.inventoryManager;inventory.RestoreGold(100000);
            var cards=new List<RewardDef>{new RewardDef{id="qa.free",displayName="무료",tier=1,rerollTicketBonus=1},new RewardDef{id="qa.buy",displayName="구매",tier=2,rerollTicketBonus=1},new RewardDef{id="qa.code",displayName="코드",tier=3,codeId=540},new RewardDef{id="qa.limit",displayName="추가",tier=4,rerollTicketBonus=1}};
            rewards.BeginRewardSelection(cards);
            var itemPriceProbe=new RewardDef{tier=5,item=new ItemData{shopPrice=20}};
            RuiCheck("동일 아이템 상점 가격 우선", "20×스테이지",RewardManager.ShopPrice(itemPriceProbe,game.RoundManager.Stage).ToString(),RewardManager.ShopPrice(itemPriceProbe,game.RoundManager.Stage)==20*game.RoundManager.Stage);
            RuiCheck("무료1·골드유지", "무료성공",inventory.Gold.ToString(),rewards.TryClaimReward(cards[0],kb)&&inventory.Gold==100000);
            RuiCheck("리롤/중복차단", "둘다차단",rewards.CanRerollSelection.ToString(),!rewards.CanRerollSelection&&!rewards.TryClaimReward(cards[0],kb));
            int gold=inventory.Gold;int price=RewardManager.ShopPrice(cards[1],game.RoundManager.Stage);
            RuiCheck("티어가격 구매1",price.ToString(),inventory.Gold.ToString(),rewards.TryClaimReward(cards[1],kb)&&inventory.Gold==gold-price);
            gold=inventory.Gold;
            RuiCheck("대상실패 결제복구", "골드·횟수불변",inventory.Gold.ToString(),!rewards.TryClaimReward(cards[2],enemy)&&inventory.Gold==gold&&rewards.PaidRewardCount==1);
            for(int id=501;id<=507;id++)kb.TryGrantRewardCode(id);
            RuiCheck("구매2·활성7포화 보관 수령","540보관",kb.RewardCodeIds.Count.ToString(),kb.RewardCodeIds.Count==7&&rewards.TryClaimReward(cards[2],kb)&&kb.StoredRewardCodeIds.Contains(540)&&rewards.PaidRewardCount==2);
            RuiCheck("구매 상한2", "3번째거절",rewards.CanClaimReward(cards[3]).ToString(),!rewards.TryClaimReward(cards[3],kb));
            game.uiManager.ShowRewardPanel(cards);yield return WaitUnscaled(.4f);
            _deferCaptureEncoding=true;yield return CaptureStill("treasury_paid_rewards");FlushDeferredCaptures();
            inventory.RestoreGold(0);RuiCheck("빈잔고 차단", "구매불가",rewards.CanClaimReward(cards[3]).ToString(),!rewards.CanClaimReward(cards[3]));
            game.uiManager.HideRewardPanel();rewards.ResetRewardSelection();
            inventory.RestoreGold(10000);
            game.uiManager.ShowRewardPanel(cards);yield return WaitUnscaled(.4f);
            var root=GameObject.Find("RewardScreen");
            RuiClick(RuiFind(root,"Reward0"));RuiClick(RuiFind(root,"ConfirmReward"));yield return WaitUnscaled(.2f);
            RuiCheck("실제 UI 무료 이후 목록 유지","무료선택·완료버튼·리롤숨김",root.activeSelf.ToString(),rewards.FreeRewardClaimed&&root.activeSelf&&RuiFind(root,"FinishRoyalReward")!=null&&RuiFind(root,"Reroll")==null);
            RuiClick(RuiFind(root,"Reward1"));RuiClick(RuiFind(root,"ConfirmReward"));yield return WaitUnscaled(.2f);
            RuiCheck("실제 UI 추가 구매","구매1·가격표시",rewards.PaidRewardCount.ToString(),rewards.PaidRewardCount==1);
            _deferCaptureEncoding=true;yield return CaptureStill("treasury_reward_ui");FlushDeferredCaptures();
            int previousStage=game.RoundManager.Stage;
            var finish=RuiFind(root,"FinishRoyalReward").GetComponent<UnityEngine.UI.Button>();finish.onClick.Invoke();finish.onClick.Invoke();
            yield return WaitUnscaled(.3f);
            RuiCheck("완료 중복 진행 차단","스테이지+1",game.RoundManager.Stage.ToString(),game.RoundManager.Stage==previousStage+1);
            PartyTreasury.Clear();
            rewards.ResetRewardSelection();game.gameState=BaseEnums.GameState.RewardSelection;
            int regularStage=game.RoundManager.Stage;
            game.uiManager.ShowRewardPanel(cards);yield return WaitUnscaled(.25f);
            root=GameObject.Find("RewardScreen");
            RuiClick(RuiFind(root,"Reward0"));RuiClick(RuiFind(root,"ConfirmReward"));yield return WaitUnscaled(.25f);
            RuiCheck("구매권 없는 기존 무료 보상 진행","무료1·즉시+1·상점권없음",game.RoundManager.Stage.ToString(),!PartyTreasury.RoyalPurchaseEntitled&&game.RoundManager.Stage==regularStage+1);
            _deferCaptureEncoding=true;yield return CaptureStill("treasury_roster");FlushDeferredCaptures();game.enabled=enabled;RuiFinish();
        }
        private IEnumerator ScenarioTreasuryNatural(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();yield return PrepareStage(config,6,true,true,false);
            var game=GameManager.Instance;float deadline=Time.realtimeSinceStartup+55;int spent=0,gained=0,casts=0;
            Action<Unit,int> txn=(u,d)=>{if(d<0)spent-=d;else gained+=d;};
            Action<Unit> ult=u=>{if(u!=null&&u.ID==220)casts++;};PartyTreasury.Changed+=txn;Unit.AnyUltimateCast+=ult;
            while(Time.realtimeSinceStartup<deadline&&game.gameState==BaseEnums.GameState.RoundInProgress && (casts==0 || gained==0 || spent==0))yield return null;
            PartyTreasury.Changed-=txn;Unit.AnyUltimateCast-=ult;
            RuiCheck("자연 자동전투 재보 순환", "공급·소비·이슈타르 초월",$"gain={gained},spend={spent},ult={casts},mana={AliveAllies().FirstOrDefault(u=>u.ID==220)?.ManaCurr},state={game.gameState}",gained>0&&spent>0&&casts>0);
            yield return CaptureStill("treasury_natural_battle");game.ActionScheduler.EndRound();RuiFinish();
        }
    }
}
#endif
