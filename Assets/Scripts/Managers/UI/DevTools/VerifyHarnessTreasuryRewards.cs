#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive.Reward;
using Combat;
using Core;
using Effects.Buffs;
using Entities;
using Entities.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private TreasuryRewardCode GrantTreasuryReward(Unit owner, int id)
        {
            bool granted = owner.TryGrantRewardCode(id);
            var code = owner.ActivePassiveCodes.OfType<TreasuryRewardCode>().SingleOrDefault(c => c.CodeName == RewardCodeCatalog.Find(id)?.verbalName);
            RuiCheck("보상 코드 장착 " + id, "활성 슬롯/팩토리", granted + "/" + (code != null), granted && code != null);
            code?.CastCode(); return code;
        }
        private IEnumerator ScenarioTreasuryRewards(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear(); bool enabled = true; yield return YvPrepareScene(config, x => enabled = x);
            var game = GameManager.Instance; var kb = AliveAllies().Single(u => u.ID == 65);
            var m = AliveAllies().Single(u => u.ID == 200); var ish = AliveAllies().Single(u => u.ID == 220); var enemy = AliveEnemies().First();
            var ids = new[] {631,632,728,729,730,819,820,908,909};
            RuiCheck("재보 코드9종 티어/보상풀", "T2×2/T3×3/T4×2/T5×2", string.Join(",", ids.Select(i => RewardCodeCatalog.TierOf(i))),
                ids.Select(i => RewardCodeCatalog.TierOf(i)).SequenceEqual(new[]{2,2,3,3,3,4,4,5,5}) && ids.All(i=>game.rewardManager.BuildCodePool().Any(c=>c.codeId==i)));
            foreach(var ally in AliveAllies()) foreach(var code in ally.ActivePassiveCodes.ToList()) code.StopCode();
            PartyTreasury.BeginBattle(); GrantTreasuryReward(kb,728); GrantTreasuryReward(m,728);
            RuiCheck("부유한 시작 파티 중복", "재보4",PartyTreasury.Available.ToString(),PartyTreasury.Available==4);
            kb.ActivePassiveCodes.OfType<TreasuryRewardCode>().Single().CastCode();
            RuiCheck("같은 시작 코드 재시전 중복 차단", "4",PartyTreasury.Total.ToString(),PartyTreasury.Total==4);
            PartyTreasury.BeginBattle(); kb.ActivePassiveCodes.OfType<TreasuryRewardCode>().Single().CastCode();
            RuiCheck("다음 전투 시작 재충전", "2",PartyTreasury.Total.ToString(),PartyTreasury.Total==2);
            kb.RemoveRewardCode(728);m.RemoveRewardCode(728);
            PartyTreasury.BeginBattle();GrantTreasuryReward(kb,730);
            for(int i=0;i<3;i++) { yield return TreasuryCast(kb,kb.CastNormalCode); PartyTreasury.ResolveAction(kb,ActionScheduler.ActionKind.Normal); }
            RuiCheck("장기 적금 실제 일반3회", "기본3+적금1",PartyTreasury.Total.ToString(),PartyTreasury.Total==4);
            int before = PartyTreasury.Total; yield return kb.ActiveNormalCode.PerformCommandedAttack(enemy,ish);
            RuiCheck("지휘사격 적금 제외",before.ToString(),PartyTreasury.Total.ToString(),PartyTreasury.Total==before);
            for(int i=0;i<12;i++){ kb.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved,new EventContext(kb)); }
            RuiCheck("장기 적금 임의 전투당 상한 없음","추가4",PartyTreasury.Total.ToString(),PartyTreasury.Total==8);
            kb.RemoveRewardCode(730);PartyTreasury.BeginBattle();float defense=m.StatValue(BaseEnums.StatType.Defense),sdef=m.StatValue(BaseEnums.StatType.SpecialDefense);
            GrantTreasuryReward(m,819);PartyTreasury.Charge(kb,4);
            RuiCheck("안전 보험 4 경계","DEF/SDEF×1.12",$"{m.DefCurr}/{m.StatValue(BaseEnums.StatType.SpecialDefense)}",Near(m.StatValue(BaseEnums.StatType.Defense),defense*1.12f)&&Near(m.StatValue(BaseEnums.StatType.SpecialDefense),sdef*1.12f));
            using(var reserved=PartyTreasury.Reserve(ish))
                RuiCheck("보험 예약 잔고 제외","원복",m.DefCurr.ToString(),Near(m.StatValue(BaseEnums.StatType.Defense),defense));
            RuiCheck("보험 반환 즉시 복원","×1.12",m.DefCurr.ToString(),Near(m.StatValue(BaseEnums.StatType.Defense),defense*1.12f));
            PartyTreasury.Spend(kb,1);RuiCheck("보험3 즉시 해제","원복",m.DefCurr.ToString(),Near(m.StatValue(BaseEnums.StatType.Defense),defense));m.RemoveRewardCode(819);
            PartyTreasury.BeginBattle();GrantTreasuryReward(m,729);PartyTreasury.Charge(kb,1);
            int shield=ish.ShieldCurr;yield return TreasuryCast(m,m.CastNormalCode);
            RuiCheck("유전무죄 실제 소비 보호막","DEF×.8×1.3",(ish.ShieldCurr-shield).ToString(),ish.ShieldCurr-shield==Mathf.RoundToInt(defense*.8f*1.3f));
            shield=ish.ShieldCurr;ish.AddShield(100,m);RuiCheck("일반 보호막에 소비 보너스 금지","100",(ish.ShieldCurr-shield).ToString(),ish.ShieldCurr-shield==100);
            ish.ModifyHp(ish.HpMax-1000);int hp=ish.HpCurr;ish.Heal(100,m,treasurySpent:true);RuiCheck("재보 소비 치유 보너스","130",(ish.HpCurr-hp).ToString(),ish.HpCurr-hp==130);
            hp=ish.HpCurr;ish.Heal(100,m);RuiCheck("일반 치유 소비 보너스 제외","100",(ish.HpCurr-hp).ToString(),ish.HpCurr-hp==100);m.RemoveRewardCode(729);
            PartyTreasury.BeginBattle();GrantTreasuryReward(ish,631);GrantTreasuryReward(ish,632);GrantTreasuryReward(ish,820);GrantTreasuryReward(ish,909);
            DamageContext Probe(bool trade,bool spend,int shot=0,BaseEnums.CodeType kind=BaseEnums.CodeType.Normal)
            {
                var hit=new DamageContext(ish,1000,kind,new List<int>{DamageTag.NonContactAttack}){IsTreasuryAttack=trade,IsTreasurySpendAttack=spend,TreasuryShotIndex=shot};enemy.TakeDamage(hit);return hit;
            }
            var plain=Probe(false,false);var charging=Probe(true,false);var spending=Probe(true,true);
            float armor=enemy.StatValue(BaseEnums.StatType.Defense),k=UnitStats.ArmorConstant(ish.Level);
            RuiCheck("확실한 거래 소비/충전 분리","기본 대비1.1·소비방무",$"{plain.ResolvedDamage}/{charging.ResolvedDamage}/{spending.ResolvedDamage}",
                Math.Abs(charging.ResolvedDamage-Mathf.RoundToInt(1100*UnitStats.MitigationMultiplier(armor,k)))<=1&&Math.Abs(spending.ResolvedDamage-Mathf.RoundToInt(1100*UnitStats.MitigationMultiplier(armor*.7f,k)))<=1);
            var dot=Probe(true,true,0,BaseEnums.CodeType.Effect);
            RuiCheck("금권 특수방어 제외","방무1",TreasuryRewardCode.DefenseMultiplier(dot).ToString(),Near(TreasuryRewardCode.DefenseMultiplier(dot),1));
            RuiCheck("황금 탄환 조건부 치피","+.4·기초치피 불변",TreasuryRewardCode.CritDamageBonus(ish).ToString(),Near(TreasuryRewardCode.CritDamageBonus(ish),.4f)&&Near(ish.StatValue(BaseEnums.StatType.CritDamage),.5f));
            for(int shot=5;shot<=6;shot++) { var hit=Probe(true,true,shot,BaseEnums.CodeType.Ultimate);RuiCheck("천공의 황소 경계"+shot,shot==5?".1":".9",TreasuryRewardCode.DamageBonus(hit).ToString(),Near(TreasuryRewardCode.DamageBonus(hit),shot==5?.1f:.9f)); }
            GrantTreasuryReward(ish,908);PartyTreasury.BeginBattle();PartyTreasury.Charge(kb,7);
            // 앞선 일반행동 탐침으로 찬 쿠베라 초월기가 이자를 먼저 주지 않도록 입력을 격리한다.
            foreach(var ally in AliveAllies())ally.ClearUltimateResource();ish.FillUltimateResource();
            int finished=0,shots=0;var indices=new List<int>();var bonuses=new List<float>();int minimumBalance=99;
            Action<Unit,DamageContext> probe=(u,c)=>{if(c.Attacker!=ish||!c.IsTreasuryAttack)return;shots++;indices.Add(c.TreasuryShotIndex);bonuses.Add(TreasuryRewardCode.DamageBonus(c));minimumBalance=Math.Min(minimumBalance,PartyTreasury.Available);};
            Action<Unit,ActionScheduler.ActionKind> done=(u,kind)=>{if(u==ish&&kind==ActionScheduler.ActionKind.Ultimate)finished++;};
            Unit.AnyDamageIncoming+=probe;ActionScheduler.AnyActionFinished+=done;
            yield return YvRunScheduled(s=>{},()=>finished>0,35);
            Unit.AnyDamageIncoming-=probe;ActionScheduler.AnyActionFinished-=done;
            RuiCheck("실제 연사6탄 경계·행동 종료1회", "7발/1회/.1×5+.9×2",string.Join(",",indices)+"/"+finished,shots==7&&finished==1&&indices.SequenceEqual(Enumerable.Range(1,7))&&bonuses.Take(5).All(v=>Near(v,.1f))&&bonuses.Skip(5).All(v=>Near(v,.9f)));
            RuiCheck("풍요 환급은 연사 끝에1","발사중0·종료1",$"min={minimumBalance},available={PartyTreasury.Available},reserved={PartyTreasury.Reserved}",minimumBalance==0&&PartyTreasury.Available==1&&PartyTreasury.Reserved==0);
            PartyTreasury.ResolveAction(ish,ActionScheduler.ActionKind.Ultimate);RuiCheck("중복 종료 환급 차단","1",PartyTreasury.Total.ToString(),PartyTreasury.Total==1);
            PartyTreasury.BeginBattle();PartyTreasury.Charge(kb,7);using(var cancel=PartyTreasury.Reserve(ish)){}
            PartyTreasury.ResolveAction(ish,ActionScheduler.ActionKind.Ultimate);RuiCheck("미발사 반환 환급 제외","7",PartyTreasury.Total.ToString(),PartyTreasury.Total==7);
            YvAddTestStatus(ish,72913,"qa.treasury.critical",new StatModifierEffect(BaseEnums.StatType.CritRate,1f));
            PartyTreasury.BeginBattle();PartyTreasury.Charge(kb,1);ish.FillUltimateResource();
            using(var actual=new YvRecorder())
            {
                yield return TreasuryCast(ish,ish.CastUltimateCode);
                var hit=actual.Incoming.Single(c=>c.Attacker==ish&&c.IsTreasuryAttack);
                int expected=Mathf.RoundToInt(Mathf.RoundToInt(ish.StatValue(BaseEnums.StatType.Attack)*1.2f)*1.9f);
                RuiCheck("황금 탄환 실제 치명 연사","기초50%+조건40%p",hit.Damage+"/"+expected,hit.IsCrit&&hit.Damage==expected&&hit.TreasuryShotIndex==1);
            }
            ish.RemoveStatusByKey("qa.treasury.critical");
            var saved=new UnitSaveData{currentHP=ish.HpCurr,ultimateResource=ish.ManaCurr};ish.WriteRunState(saved);
            foreach(int id in new[]{631,632,820,908,909})ish.RemoveRewardCode(id);
            ish.RestoreRunState(saved);
            RuiCheck("신규 코드 저장/복원 슬롯·인스턴스","5코드·동일7칸/인스턴스",string.Join(",",ish.RewardCodeSlots),
                ish.RewardCodeSlots.SequenceEqual(saved.activeRewardCodeSlots)&&ish.RewardCodeSlotInstances.Where(x=>x!=null).Select(x=>x.instanceId).SequenceEqual(saved.activeRewardCodeInstances.Where(x=>x!=null).Select(x=>x.instanceId))&&new[]{631,632,820,908,909}.All(id=>ish.ActivePassiveCodes.Any(c=>c.CatalogId==id)));
            foreach(int id in new[]{631,632,820,908,909})ish.RemoveRewardCode(id);
            PartyTreasury.EndBattle();game.enabled=enabled;yield return CaptureStill("treasury_reward_codes");RuiFinish();
        }
        private IEnumerator ScenarioCardHudContainment(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();bool enabled=true;yield return YvPrepareScene(config,x=>enabled=x);
            if(!AliveAllies().Any(u=>u.HasArchetypeTag("Treasury")))RuiCheck("재보 없는 파티 전용 코드 후보 제외","9종 없음",GameManager.Instance.rewardManager.BuildCodePool().Count.ToString(),!GameManager.Instance.rewardManager.BuildCodePool().Any(c=>TreasuryRewardCode.Supports(c.codeId)));
            foreach(var unit in AliveAllies().Concat(AliveEnemies()))
            {
                YvAddTestStatus(unit,72910,"qa.hud.1",new StatModifierEffect(BaseEnums.StatType.Attack,1));
                YvAddTestStatus(unit,72911,"qa.hud.2",new StatModifierEffect(BaseEnums.StatType.Attack,1));
                YvAddTestStatus(unit,72912,"qa.hud.3",new StatModifierEffect(BaseEnums.StatType.Defense,1));
                unit.RefreshAttributes();
            }
            yield return WaitUnscaled(.3f);Canvas.ForceUpdateCanvases();
            foreach(var view in UnityEngine.Object.FindObjectsByType<UnitCardView>(FindObjectsSortMode.None).Where(v=>v.gameObject.activeInHierarchy))
            {
                var glass=view.GetComponentsInChildren<RectTransform>(true).SingleOrDefault(x=>x.name=="BottomGlass");if(glass==null)continue;
                var images=glass.GetComponentsInChildren<Image>(true).Where(x=>x.enabled&&(x.name.StartsWith("StatusBack")||x.name.StartsWith("StatusCountBack")||x.name is "HpTrack" or "ActionTrack"));
                foreach(var image in images)
                {
                    var corners=new Vector3[4];image.rectTransform.GetWorldCorners(corners);var local=corners.Select(c=>glass.InverseTransformPoint(c)).ToArray();
                    bool inside=local.All(c=>c.x>=glass.rect.xMin+4.5f&&c.x<=glass.rect.xMax-4.5f&&c.y>=glass.rect.yMin+4.5f&&c.y<=glass.rect.yMax-4.5f);
                    RuiCheck(view.name+"/"+image.name+" 정보판 여백","모든 모서리≥4.5",string.Join(";",local.Select(c=>$"{c.x:F1},{c.y:F1}")),inside);
                }
                foreach(var text in view.GetComponentsInChildren<TextMeshPro>(true).Where(t=>t.enabled&&t.name is "Name" or "HpValue" or "ResourceValue"))
                {
                    text.ForceMeshUpdate();var corners=new Vector3[4];text.rectTransform.GetWorldCorners(corners);
                    bool inside=corners.Select(c=>glass.InverseTransformPoint(c)).All(c=>c.x>=glass.rect.xMin+4.5f&&c.x<=glass.rect.xMax-4.5f&&c.y>=glass.rect.yMin+1&&c.y<=glass.rect.yMax-1);
                    RuiCheck(view.name+"/"+text.name+" 글자 컨테이너","내부·잘림 없음",text.text,inside&&!text.isTextTruncated);
                }
            }
            yield return WaitUnscaled(1.5f);
            _deferCaptureEncoding=true;yield return CaptureStill("card_hud_status_inside");FlushDeferredCaptures();GameManager.Instance.enabled=enabled;RuiFinish();
        }
    }
}
#endif
