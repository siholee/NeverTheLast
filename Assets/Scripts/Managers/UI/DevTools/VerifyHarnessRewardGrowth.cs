#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Core;
using Entities;
using Managers.UI.Core;
using Managers.UI.Theme;
using UnityEngine;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioRewardGrowth(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();bool enabledBefore=true;
            yield return YvPrepareScene(config,x=>enabledBefore=x);
            var game=GameManager.Instance;var party=RewardCodeFlow.Party();var a=party.First();var b=party[1];var c=party[2];
            foreach(var u in party)u.ApplyRewardCodeInventory(Enumerable.Repeat(0,7).ToList(),new List<int>());
            float attack=a.StatValue(BaseEnums.StatType.Attack);
            a.TryGrantRewardCode(540);a.CastPassiveCode();
            RuiCheck("축전 초기 무효","초기0·공격변화없음",a.ActiveRewardInstance(540)?.charge.ToString(),Mathf.Approximately(a.StatValue(BaseEnums.StatType.Attack),attack));
            var enemy=AliveEnemies().First();DebugMode.EnemyInvincible=false;
            enemy.TakeDamage(new DamageContext(a,1000000,BaseEnums.CodeType.Normal,new List<int>{DamageTag.TrueDamage}));
            RuiCheck("필드 사망 충전","실제사망1·저장1·공격+1",$"active={enemy.isActive},charge={a.ActiveRewardInstance(540)?.charge},atk={a.AtkCurr}",!enemy.isActive && Mathf.Approximately(a.ActiveRewardInstance(540).charge,1) && Mathf.Approximately(a.StatValue(BaseEnums.StatType.Attack),attack+1));
            var summon=GridManager.Instance.SpawnSummon(a,new SummonSpec{Id=997,Kind="qa.capacitor",Name="검증 소환수",PortraitKey=a.PortraitPath,Hp=1});
            summon.Die(null);
            RuiCheck("소환수 충전 억제","사망0.25",a.ActiveRewardInstance(540).charge.ToString(),Mathf.Approximately(a.ActiveRewardInstance(540).charge,1.25f));
            var capCode=a.ActivePassiveCodes.First(x=>x.CatalogId==540);capCode.StopCode();
            summon=GridManager.Instance.SpawnSummon(a,new SummonSpec{Id=997,Kind="qa.capacitor",Name="검증 소환수",PortraitKey=a.PortraitPath,Hp=1});summon.Die(null);
            RuiCheck("종료 구독 정리","저장1.25불변",a.ActiveRewardInstance(540).charge.ToString(),Mathf.Approximately(a.ActiveRewardInstance(540).charge,1.25f));
            capCode.CastCode();
            a.TryGrantRewardCode(727);a.GainRewardCharge(540,1);
            RuiCheck("다단중첩 배율","저장2.75",a.ActiveRewardInstance(540).charge.ToString(),Mathf.Approximately(a.ActiveRewardInstance(540).charge,2.75f));
            a.GainRewardCharge(540,100);
            RuiCheck("저급 상한","12",a.ActiveRewardInstance(540).charge.ToString(),Mathf.Approximately(a.ActiveRewardInstance(540).charge,12));
            game.ActionScheduler.EndRound();GridManager.Instance.OnRoundEnd();game.gameState=BaseEnums.GameState.Preparation;
            var instance=a.ActiveRewardInstance(540);string identity=instance.instanceId;
            int basePrice=RewardCodeInventory.SalePrice(new RewardCodeInstanceData{codeId=540,instanceId="empty",acquiredStage=instance.acquiredStage});
            RuiCheck("충전 판매가","빈값의2배",RewardCodeInventory.SalePrice(instance).ToString(),RewardCodeInventory.SalePrice(instance)==basePrice*2);
            bool store=RewardCodeInventory.TryMove(CodeDrag(a,false,a.RewardCodeSlots.ToList().IndexOf(540)),a,true);
            RuiCheck("보관 비활성","저장12보존·공격원복",a.StatValue(BaseEnums.StatType.Attack).ToString(),store && a.StoredRewardCodeInstances.Any(x=>x.instanceId==identity && x.charge==12) && Mathf.Approximately(a.StatValue(BaseEnums.StatType.Attack),attack));
            int bag=a.StoredRewardCodeIds.ToList().IndexOf(540);
            bool move=RewardCodeInventory.TryMove(CodeDrag(a,true,bag),b,false,0);
            RuiCheck("아군 이동 저장 보존","동일ID·충전12",b.ActiveRewardInstance(540)?.instanceId,move && b.ActiveRewardInstance(540).instanceId==identity && b.ActiveRewardInstance(540).charge==12);
            bool upgrade=b.TryReplaceRewardCode(540,640);
            RuiCheck("승급 저장량 유지","동일ID·12/24",b.ActiveRewardInstance(640)?.charge.ToString(),upgrade && b.ActiveRewardInstance(640).instanceId==identity && b.ActiveRewardInstance(640).charge==12);
            b.GainRewardCharge(640,100);b.TryReplaceRewardCode(640,740);b.GainRewardCharge(740,100);
            RuiCheck("중급·고급 상한","24→48",b.ActiveRewardInstance(740)?.charge.ToString(),b.ActiveRewardInstance(740).charge==48);
            var saved=new UnitSaveData{currentHP=b.HpCurr,ultimateResource=b.ManaCurr};b.WriteRunState(saved);
            string json=JsonUtility.ToJson(saved);b.RemoveRewardCode(740);b.RestoreRunState(JsonUtility.FromJson<UnitSaveData>(json));
            RuiCheck("JSON 저장 복원","동일ID48",b.ActiveRewardInstance(740)?.instanceId,b.ActiveRewardInstance(740).instanceId==identity && b.ActiveRewardInstance(740).charge==48);
            c.TryGrantRewardCode(740);c.GainRewardCharge(740,7);
            RewardCodeInventory.TryMove(CodeDrag(b,false,b.RewardCodeSlots.ToList().IndexOf(740)),c,true);
            RuiCheck("같은 코드별 독립 저장","활성7·보관48",string.Join("/",c.OwnedRewardCodeInstances.Select(x=>x.charge)),c.ActiveRewardInstance(740).charge==7 && c.StoredRewardCodeInstances.Single(x=>x.instanceId==identity).charge==48);
            var stale=CodeDrag(c,true,c.StoredRewardCodeIds.ToList().IndexOf(740));stale.InstanceId=identity;
            RewardCodeInventory.TryMove(stale,a,true);
            RuiCheck("오래된 동일명 드래그 차단","거절",RewardCodeInventory.CanMove(stale,b,true).ToString(),!RewardCodeInventory.CanMove(stale,b,true));
            RewardCodeInventory.TryMove(CodeDrag(c,false,c.RewardCodeSlots.ToList().IndexOf(740)),a,false,0);
            var sale=a.StoredRewardCodeInstances.Single(x=>x.instanceId==identity);
            int gold=game.inventoryManager.Gold,expected=RewardCodeInventory.SalePrice(sale),left=game.ShopPurchasesLeft;
            bool sold=RewardCodeInventory.TrySell(a,identity,out int paid);
            RuiCheck("개별 축전기 판매","보관48만판매·활성7보존·금액·구매횟수",$"{paid},active={a.ActiveRewardInstance(740)?.charge}",sold && paid==expected && game.inventoryManager.Gold==gold+expected && a.ActiveRewardInstance(740).charge==7 && game.ShopPurchasesLeft==left);
            var pool=game.rewardManager.BuildCodePool().Select(x=>x.codeId).ToHashSet();
            RuiCheck("축전 보상풀 연결","미보유8종3티어모두 구현",pool.Count.ToString(),Enumerable.Range(0,8).All(i=>Codes.Passive.Reward.RewardCodeFactory.IsImplemented(540+i)&&Codes.Passive.Reward.RewardCodeFactory.IsImplemented(640+i)&&Codes.Passive.Reward.RewardCodeFactory.IsImplemented(740+i)) && pool.Contains(541));
            foreach(var item in game.itemDataList.items.Where(x=>x.IsNectar))game.inventoryManager.AddItem(item.id);
            game.uiManager.ShowInventoryPanel();yield return WaitUnscaled(.3f);
            yield return CaptureStill("nectar_inventory");
            var root=GameObject.Find("InventoryCanvas");
            float hp=a.StatValue(BaseEnums.StatType.MaxHp);
            RuiClick(RuiFind(root,"Item_5301"));yield return WaitUnscaled(.2f);
            yield return CaptureStill("nectar_target_selection");
            RuiClick(RuiTargetCard(root,a));yield return WaitUnscaled(.2f);
            RuiCheck("넥타르 실제UI 사용","생명+100·1개소비",a.StatValue(BaseEnums.StatType.MaxHp).ToString(),Mathf.Approximately(a.StatValue(BaseEnums.StatType.MaxHp),hp+100) && game.inventoryManager.CountItem(5301)==0);
            game.uiManager.HideInventoryPanel();
            var nectar=new[]{(5302,BaseEnums.StatType.Defense,8f),(5303,BaseEnums.StatType.Attack,8f),(5304,BaseEnums.StatType.SpecialAttack,8f),(5305,BaseEnums.StatType.SpecialDefense,8f),(5306,BaseEnums.StatType.Speed,2f),(5307,BaseEnums.StatType.CritDamage,.04f)};
            foreach(var (id,stat,amount) in nectar)
            {
                float before=a.StatValue(stat);bool used=game.TryUseNectar(id,a,out var reason);
                RuiCheck("넥타르 "+stat,"지정스탯만 증가·소비",reason+" "+a.StatValue(stat),used && Mathf.Abs(a.StatValue(stat)-before-amount)<.001f && game.inventoryManager.CountItem(id)==0);
            }
            var snap=new UnitSaveData{currentHP=a.HpCurr};a.WriteRunState(snap);
            float spAtk=a.NectarStatBonus(BaseEnums.StatType.SpecialAttack);
            a.RestoreRunState(JsonUtility.FromJson<UnitSaveData>(JsonUtility.ToJson(snap)));
            RuiCheck("넥타르 저장복원","특공+8유지",a.NectarStatBonus(BaseEnums.StatType.SpecialAttack).ToString(),a.NectarStatBonus(BaseEnums.StatType.SpecialAttack)==spAtk && spAtk==8);
            game.gameState=BaseEnums.GameState.RoundInProgress;game.inventoryManager.AddItem(5306);
            float speed=a.StatValue(BaseEnums.StatType.Speed);
            RuiCheck("전투 중 성장·판매 잠금","넥타르미소비·판매거절","API검사",!game.TryUseNectar(5306,a,out _) && !RewardCodeInventory.TrySell(a,a.ActiveRewardInstance(740).instanceId,out _) && a.StatValue(BaseEnums.StatType.Speed)==speed && game.inventoryManager.CountItem(5306)==1);
            game.gameState=BaseEnums.GameState.Preparation;
            game.uiManager.ShowCodex();yield return WaitUnscaled(.25f);yield return CaptureStill("capacitor_live_storage");
            game.uiManager.CodexForVerification.Hide();game.uiManager.ShowShopPanel();yield return WaitUnscaled(.25f);
            RuiClick(RuiFind(GameObject.Find("ShopCanvas"),"TabSell"));yield return WaitUnscaled(.25f);yield return CaptureStill("capacitor_sale_stock");
            var shopRoot=GameObject.Find("ShopCanvas");
            string sellIdentity=a.ActiveRewardInstance(740).instanceId;
            int sellExpected=RewardCodeInventory.SalePrice(a.ActiveRewardInstance(740));gold=game.inventoryManager.Gold;
            var row=shopRoot.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true).FirstOrDefault(x=>x.name=="Name" && x.text==RewardCodeFlow.NameOf(740)+" · "+a.UnitName);
            RuiClick(row?.transform.parent);yield return WaitUnscaled(.2f);
            RuiCheck("코드 실제 상점 판매UI","선택한장제거·가격지급",game.inventoryManager.Gold.ToString(),!a.OwnedRewardCodeInstances.Any(x=>x.instanceId==sellIdentity)&&game.inventoryManager.Gold==gold+sellExpected);
            // 런 저장·전체 전장 재생성까지 확인한다. JSON DTO만 왕복하는 검사와 별개다.
            a.TryGrantRewardCode(743);a.GainRewardCharge(743,11.5f);
            string persist=a.ActiveRewardInstance(743).instanceId;int unitId=a.ID;
            game.runManager.SaveCurrentRun();var full=SaveSystem.LoadRun();
            bool restored=game.runManager.LoadSavedRun();a=RewardCodeFlow.Party().First(x=>x.ID==unitId);
            RuiCheck("실제 런 복원 성장값","동일ID충전17.25(1.5배)·특공넥타르8",a.ActiveRewardInstance(743)?.charge.ToString(),restored&&a.ActiveRewardInstance(743).instanceId==persist&&a.ActiveRewardInstance(743).charge==17.25f&&a.NectarStatBonus(BaseEnums.StatType.SpecialAttack)==8);
            var old=JsonUtility.FromJson<RunSaveData>(JsonUtility.ToJson(full));old.version=13;
            foreach(var hero in old.heroUnits){hero.activeRewardCodeInstances=null;hero.storedRewardCodeInstances=null;hero.nectarStats=null;}
            SaveSystem.SaveRun(old);restored=game.runManager.LoadSavedRun();a=RewardCodeFlow.Party().First(x=>x.ID==unitId);
            RuiCheck("v13 저장 이관","코드유지·새ID0충전·진행유지",a.ActiveRewardInstance(743)?.charge.ToString(),restored&&a.ActiveRewardInstance(743)!=null&&a.ActiveRewardInstance(743).charge==0&&SaveSystem.LoadRun().version==RunSaveData.CurrentVersion);
            SaveSystem.SaveRun(full);
            game.uiManager.HideShopPanel();game.enabled=enabledBefore;RuiFinish();
        }
    }
}
#endif
