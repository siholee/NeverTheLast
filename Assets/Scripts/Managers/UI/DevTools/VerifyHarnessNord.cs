#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Normal;
using Codes.Passive;
using Codes.Ultimate;
using Combat;
using Core;
using Effects.Base;
using Effects.Buffs;
using Effects.Vfx;
using Entities;
using UnityEngine;
using UnityEngine.UI;
using Kind = Managers.ActionScheduler.ActionKind;
namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator NordAction(Unit actor, Kind kind, Action<ActionScheduler> setup, string capture = null)
        {
            bool finished = false;
            void End(Unit unit, Kind type) { if (unit == actor && type == kind) finished = true; }
            ActionScheduler.AnyActionFinished += End;
            bool settled=false;
            Coroutine recorder=capture != null ? StartCoroutine(NordCapture(capture, () => settled)) : null;
            yield return CfRun(setup, () => finished, capture == null ? 20f : 180f);
            settled=true;
            if(recorder!=null)yield return recorder;
            ActionScheduler.AnyActionFinished -= End;
        }
        private bool _nordFinite;
        private int _nordSeenMeshes, _nordFrameCount;
        private int _actionCaptureStride=1;
        private IEnumerator NordCapture(string tag, Func<bool> finished)
        {
            do
            {
                yield return EndOfFrame;
                foreach(MeshFilter filter in FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
                {
                    Mesh mesh=filter.sharedMesh;
                    if(mesh==null || mesh.name!="NordRibbon48")continue;
                    _nordSeenMeshes++;
                    _nordFinite &= mesh.vertices.All(v=>!float.IsNaN(v.x)&&!float.IsNaN(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.x)&&!float.IsInfinity(v.y)&&!float.IsInfinity(v.z));
                }
                _nordFrameCount++; if(_nordFrameCount%_actionCaptureStride==0)Capture(tag);
            }
            while (!finished() || GameManager.Instance.ActionScheduler.IsPresenting);
        }
        private IEnumerator ScenarioNordCore(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); _cfActionIdSequence = 0;
            bool wasEnabled = true;
            yield return YvPrepareScene(config, value => wasEnabled = value);
            Unit surtr = CfAlly(81), freya = CfAlly(82), napoleon = CfAlly(161), ally = CfAlly(67);
            List<Unit> foes = AliveEnemies(); Unit enemy = foes.First();
            var furnace = surtr.ActivePassiveCodes.OfType<SurtrLastEmber>().Single();
            var hands = freya.ActivePassiveCodes.OfType<FreyaGentleHands>().Single();
            var ult = (SurtrRagnarok)surtr.ActiveUltimateCode;
            var reservoir = (FreyaBrisingamen)freya.ActiveUltimateCode;
            YvCheck("신규 2인 코드 등록", "P281/N81/U81 · P282/N82/U82", surtr.ActiveNormalCode.GetType().Name + "/" + freya.ActiveNormalCode.GetType().Name,
                surtr.ActiveNormalCode is SurtrEmberBlade && freya.ActiveNormalCode is FreyaRestoringLight && !freya.ActiveNormalCode.CanJoinCommand && reservoir.IsPersistent && !reservoir.IsAutoCast);
            CfStats(surtr,"수르트",1320,112,82,5.2f,70,4.4f,76,5.5f,64,4.5f,90,120);
            CfStats(freya,"프레이아",1180,106,50,3,74,4.8f,64,4.8f,78,5.8f,94,100);
            YvCheck("원화 · 고유 원소", "SURTR/Pyro · FREYA/Dendro", surtr.Element + "/" + freya.Element, surtr.Element == "Pyro" && freya.Element == "Dendro");
            foreach (Unit unit in AliveAllies()) CfNoCrit(unit);
            CfEnsureEffectHit(surtr, foes);
            hands.StopCode(); reservoir.StopCode();
            foreach (Unit unit in AliveAllies()) { unit.RestoreHpOutsideCombat(unit.HpMax); unit.RemoveShield(unit.ShieldCurr); unit.ClearUltimateResource(); }
            // 비용은 보호막/피격/반격 이벤트와 분리되며 죽이지 않는다.
            int costEvents = 0, damageEvents = 0;
            void Paid(Unit unit,int amount) { if(unit == surtr) costEvents++; }
            void Hurt(DamageResolvedContext context) { if(context.Target == surtr) damageEvents++; }
            HealthCost.AnyPaid += Paid; Unit.AnyDamageDealt += Hurt;
            surtr.AddShield(900,surtr); int shield = surtr.ShieldCurr, hp = surtr.HpCurr, mana = surtr.ManaCurr;
            int paid = HealthCost.Pay(surtr, 100);
            YvCheck("체력 비용 보호막 우회", "체력 -100 / 보호막 동일", $"{hp-surtr.HpCurr}/{surtr.ShieldCurr}", paid==100 && hp-surtr.HpCurr==100 && surtr.ShieldCurr==shield);
            YvCheck("체력 비용 피해·피격마나 없음", "비용 1 / 피해 0 / 마나 동일", $"{costEvents}/{damageEvents}/{surtr.ManaCurr}",costEvents==1 && damageEvents==0 && surtr.ManaCurr==mana);
            surtr.ModifyHp(2); YvCheck("자해 사망 방지", "1 지불 / HP1", HealthCost.Pay(surtr,9999)+"/"+surtr.HpCurr,surtr.HpCurr==1);
            HealthCost.AnyPaid -= Paid; Unit.AnyDamageDealt -= Hurt;
            surtr.RestoreHpOutsideCombat(surtr.HpMax); surtr.RemoveShield(surtr.ShieldCurr);
            using(var log=new CfLog(AliveAllies()))
            {
                yield return NordAction(surtr,Kind.Normal,s=>s.AdvanceAction(surtr,1));
                var hit=log.Dealt.First(x=>x.Attacker==surtr);
                YvCheck("수르트 단일 일반공격", "피해 1 / 일반·접촉·베기",log.Dealt.Count(x=>x.Attacker==surtr).ToString(),log.Dealt.Count(x=>x.Attacker==surtr)==1 && hit.DamageContext.HasTag(DamageTag.NormalAttack) && hit.DamageContext.HasTag(DamageTag.ContactAttack) && hit.DamageContext.HasTag(DamageTag.Slash));
                YvCheck("일반공격 원소 부착 없음", "부착 0",log.Elements.Count(x=>x.Source==surtr).ToString(),log.Elements.All(x=>x.Source!=surtr));
            }
            int smallShield=Mathf.RoundToInt(surtr.StatValue(BaseEnums.StatType.Defense)*.12f);
            YvCheck("첫 공격 전체 방어력 보호막", "전원 "+smallShield,string.Join(",",AliveAllies().Select(x=>x.ShieldCurr)),AliveAllies().All(x=>x.ShieldCurr==smallShield));
            int before=ally.ShieldCurr;
            using(var log=new CfLog(AliveAllies()))
                yield return NordAction(surtr,Kind.Additional,s=>s.EnqueueAdditional(surtr,"nord_dot","지속피해 검증",()=>CfHit(surtr,enemy,new List<int>(),BaseEnums.CodeType.Effect)));
            YvCheck("지속피해 보호막 재발동 제외",before.ToString(),ally.ShieldCurr.ToString(),ally.ShieldCurr==before);
            foreach(float ratio in new[]{.59f,.60f,1f})
            {
                foreach(Unit foe in foes) YvResetEnemy(foe);
                foreach(Unit unit in AliveAllies()) unit.ClearUltimateResource();
                int startHp=Mathf.CeilToInt(surtr.HpMax*ratio); surtr.ModifyHp(startHp);
                before=ally.ShieldCurr;
                using(var log=new CfLog(AliveAllies()))
                {
                    yield return NordAction(surtr,Kind.Ultimate,s=>surtr.FillUltimateResource());
                    bool empowered=ratio>=.60f;
                    int expectedPay=empowered?Mathf.RoundToInt(surtr.HpMax*.20f):0;
                    int expectedRaw=Mathf.RoundToInt(surtr.StatValue(BaseEnums.StatType.Attack)*1.80f+(empowered?surtr.HpMax*.14f:0));
                    YvCheck("수르트 U 체력 경계 "+ratio,"비용 "+expectedPay,ult.LastHealthPaid.ToString(),ult.LastHealthPaid==expectedPay && surtr.HpCurr==startHp-expectedPay);
                    YvCheck("수르트 U 두 계수 "+ratio,expectedRaw.ToString(),ult.LastRawDamage.ToString(),ult.LastRawDamage==expectedRaw);
                    YvCheck("수르트 U 적 전원 1회 "+ratio,foes.Count.ToString(),log.Dealt.Count(x=>x.Attacker==surtr && x.DamageContext.CodeType==BaseEnums.CodeType.Ultimate).ToString(),log.Dealt.Count(x=>x.Attacker==surtr && x.DamageContext.CodeType==BaseEnums.CodeType.Ultimate)==foes.Count);
                    YvCheck("수르트 U 불 부착 "+ratio,foes.Count.ToString(),log.Elements.Count(x=>x.Source==surtr).ToString(),log.Elements.Count(x=>x.Source==surtr && x.Element==BaseEnums.UnitElement.Pyro)==foes.Count);
                    YvCheck("수르트 광역 보호막 1회 "+ratio,smallShield.ToString(),(ally.ShieldCurr-before).ToString(),ally.ShieldCurr-before==smallShield);
                }
            }
            // 두 계수 전체에 치명이 한 번만 적용된다.
            surtr.RemoveStatusByKey("verify_cf_nocrit");
            YvAddTestStatus(surtr,99983,"verify_nord_crit",new StatModifierEffect(BaseEnums.StatType.CritRate,1));
            surtr.RestoreHpOutsideCombat(surtr.HpMax);
            using(var log=new CfLog(AliveAllies()))
            {
                yield return NordAction(surtr,Kind.Ultimate,s=>surtr.FillUltimateResource());
                int expected=Mathf.RoundToInt(ult.LastRawDamage*(1f+surtr.StatValue(BaseEnums.StatType.CritDamage)));
                YvCheck("HP 계수 포함 전체 치명",expected.ToString(),string.Join(",",log.Dealt.Where(x=>x.Attacker==surtr).Select(x=>x.DamageContext.Damage)),
                    log.Dealt.Where(x=>x.Attacker==surtr && x.DamageContext.CodeType==BaseEnums.CodeType.Ultimate).All(x=>x.DamageContext.IsCrit && x.DamageContext.Damage==expected));
            }
            surtr.RemoveStatusByKey("verify_nord_crit"); CfNoCrit(surtr);
            // 방어력만 보호막에 기여한다. 체력/공격력은 보호막을 올리지 않는다.
            int defenseShield=ally.ShieldCurr;
            YvAddTestStatus(surtr,99981,"verify_nord_hp_atk",new StatModifierEffect(BaseEnums.StatType.MaxHp,2000));
            YvAddTestStatus(surtr,99982,"verify_nord_atk",new StatModifierEffect(BaseEnums.StatType.Attack,100));
            using(var log=new CfLog(AliveAllies())) yield return NordAction(surtr,Kind.Normal,s=>s.AdvanceAction(surtr,1));
            YvCheck("보호막 HP·공격 독립",smallShield.ToString(),(ally.ShieldCurr-defenseShield).ToString(),ally.ShieldCurr-defenseShield==smallShield);
            surtr.RemoveStatusByKey("verify_nord_hp_atk"); surtr.RemoveStatusByKey("verify_nord_atk");
            // 프레이아의 실제 회복/과잉 회복/다른 치유사와 분리.
            reservoir.CastCode(); hands.CastCode();
            foreach(Unit unit in AliveAllies()) { unit.RestoreHpOutsideCombat(unit.HpMax); unit.ClearUltimateResource(); }
            surtr.ModifyHp(surtr.HpMax-100); int energy=reservoir.Energy;
            surtr.Heal(300,freya);
            YvCheck("실치유만 저장", "100",(reservoir.Energy-energy).ToString(),reservoir.Energy-energy==100);
            energy=reservoir.Energy; surtr.Heal(300,freya);
            YvCheck("과잉 치유 저장 없음",energy.ToString(),reservoir.Energy.ToString(),reservoir.Energy==energy);
            surtr.ModifyHp(surtr.HpMax-100); surtr.Heal(100,napoleon);
            YvCheck("다른 치유사 저장 없음",energy.ToString(),reservoir.Energy.ToString(),reservoir.Energy==energy);
            surtr.ModifyHp(Mathf.CeilToInt(surtr.HpMax*.35f)); surtr.Heal(surtr.HpMax,freya);
            ally.ModifyHp(ally.HpMax/2); ally.Heal(ally.HpMax,freya);
            int cap=Mathf.RoundToInt(freya.HpMax*.75f);
            YvCheck("온기 상한",cap.ToString(),reservoir.Energy.ToString(),reservoir.Energy==cap);
            YvCheck("오라 최대 30%", ".30",reservoir.DamageBonus.ToString(),Near(reservoir.DamageBonus,.30f));
            YvCheck("아군 전체 오라 1개", "전원 1",string.Join(",",AliveAllies().Select(x=>x.GetAllStatuses().Count(y=>y.Key==reservoir.AuraKey))),AliveAllies().All(x=>x.GetAllStatuses().Count(y=>y.Key==reservoir.AuraKey)==1));
            float aura=CfModifier(ally,reservoir.AuraKey,fx=>fx.OutgoingDamageModifier(ally,enemy,CfNormalProbe(ally)));
            YvCheck("동적 오라 일반공격", "1.30",aura.ToString(),Near(aura,1.30f));
            float dot=CfModifier(ally,reservoir.AuraKey,fx=>fx.OutgoingDamageModifier(ally,enemy,new DamageContext(ally,100,BaseEnums.CodeType.Effect,new List<int>())));
            YvCheck("동적 오라 지속피해", "1.30",dot.ToString(),Near(dot,1.30f));
            // 만피 파티에서는 자신의 일반행동이 온기를 감소시키고 다시 채우지 못한다.
            foreach(Unit unit in AliveAllies()) unit.RestoreHpOutsideCombat(unit.HpMax);
            using(var log=new CfLog(AliveAllies())) yield return NordAction(freya,Kind.Normal,s=>s.AdvanceAction(freya,1));
            int decayed=cap-Mathf.CeilToInt(cap*.25f);
            YvCheck("자신 일반행동 25% 감소",decayed.ToString(),reservoir.Energy.ToString(),reservoir.Energy==decayed);
            // 전체 치유는 정확한 부족분을 채우고 감쇠 후 저장한다.
            foreach(Unit unit in AliveAllies()) unit.ModifyHp(unit.HpMax-70);
            energy=reservoir.Energy;
            using(var log=new CfLog(AliveAllies())) yield return NordAction(freya,Kind.Normal,s=>s.AdvanceAction(freya,1));
            int expectedEnergy=Mathf.Min(cap,energy-Mathf.CeilToInt(energy*.25f)+70*AliveAllies().Count);
            YvCheck("전체 치유 후 부족분 저장",expectedEnergy.ToString(),reservoir.Energy.ToString(),reservoir.Energy==expectedEnergy && AliveAllies().All(x=>x.HpCurr==x.HpMax));
            // 행동 후 짤힐: 일반 제외, 세 종류 포함. 회복량 그대로 저장.
            foreach(Kind kind in new[]{Kind.Additional,Kind.Special,Kind.Ultimate})
            {
                freya.AddCombatResource(FreyaBrisingamen.Resource,-reservoir.Energy);
                surtr.ModifyHp(surtr.HpMax-100); int oldHp=surtr.HpCurr;
                if(kind==Kind.Ultimate)
                {
                    using(var log=new CfLog(AliveAllies())) yield return NordAction(surtr,kind,s=>surtr.FillUltimateResource());
                    int tiny=Mathf.RoundToInt(freya.HpMax*.02f);
                    YvCheck("자해 뒤 초월기 짤힐",tiny.ToString(),reservoir.Energy.ToString(),reservoir.Energy==tiny && surtr.HpCurr==oldHp-ult.LastHealthPaid+tiny);
                }
                else
                {
                    using(var log=new CfLog(AliveAllies()))
                        yield return NordAction(surtr,kind,s=> { if(kind==Kind.Special) s.EnqueueSpecial(surtr,"nord_small","짤힐 특수",()=>{}); else s.EnqueueAdditional(surtr,"nord_small","짤힐 추가",()=>{}); });
                    int tiny=Mathf.RoundToInt(freya.HpMax*.02f);
                    YvCheck(kind+" 짤힐 2%",tiny.ToString(),(surtr.HpCurr-oldHp).ToString(),surtr.HpCurr-oldHp==tiny && reservoir.Energy==tiny);
                }
            }
            // 긴급치유는 선소모, 자기 충전/자기 특성 루프 없음.
            foreach(Unit unit in AliveAllies()) { unit.RestoreHpOutsideCombat(unit.HpMax); unit.ClearUltimateResource(); }
            freya.AddCombatResource(FreyaBrisingamen.Resource,cap);
            ally.RemoveShield(ally.ShieldCurr); ally.ModifyHp(Mathf.CeilToInt(ally.HpMax*.30f));
            freya.ModifyHp(freya.HpMax-100); int freyaBefore=freya.HpCurr;
            hp=ally.HpCurr; energy=reservoir.Energy;
            using(var log=new CfLog(AliveAllies()))
                yield return NordAction(enemy,Kind.Additional,s=>s.EnqueueAdditional(enemy,"nord_hurt","긴급 판정",()=>CfHit(enemy,ally,new List<int>(),BaseEnums.CodeType.Effect)));
            int emergencyCost=Mathf.RoundToInt(freya.HpMax*.18f);
            YvCheck("긴급 온기 18% 소모",(energy-emergencyCost).ToString(),reservoir.Energy.ToString(),reservoir.Energy==energy-emergencyCost);
            YvCheck("긴급 단일 치유", "HP 증가",hp+"→"+ally.HpCurr,ally.HpCurr>hp && freya.HpCurr==freyaBefore);
            // 실행 전 다른 치유사가 구조했다면 예약 비용 반환.
            freya.AddCombatResource(FreyaBrisingamen.Resource,-reservoir.Energy); freya.AddCombatResource(FreyaBrisingamen.Resource,cap);
            ally.ModifyHp(Mathf.CeilToInt(ally.HpMax*.30f)); ally.RemoveShield(ally.ShieldCurr); energy=reservoir.Energy;
            using(var log=new CfLog(AliveAllies()))
                yield return NordAction(enemy,Kind.Additional,s=>s.EnqueueAdditional(enemy,"nord_refund","구조 경쟁",()=> { CfHit(enemy,ally,new List<int>(),BaseEnums.CodeType.Effect); ally.Heal(ally.HpMax,napoleon); }));
            YvCheck("긴급 예약 취소 환불",energy.ToString(),reservoir.Energy.ToString(),reservoir.Energy==energy);
            freya.AddCombatResource(FreyaBrisingamen.Resource,-reservoir.Energy); ally.ModifyHp(Mathf.CeilToInt(ally.HpMax*.30f));
            using(var log=new CfLog(AliveAllies())) yield return NordAction(enemy,Kind.Additional,s=>s.EnqueueAdditional(enemy,"nord_empty","빈 온기",()=>CfHit(enemy,ally,new List<int>(),BaseEnums.CodeType.Effect)));
            YvCheck("온기 부족 긴급치유 없음", "0 / HP ≤30%",reservoir.Energy+"/"+ally.HpCurr,reservoir.Energy==0 && ally.HpCurr<=ally.HpMax*.30f);
            reservoir.StopCode(); energy=reservoir.Energy;
            surtr.ModifyHp(surtr.HpMax-100); surtr.Heal(100,freya);
            YvCheck("오라 정지 구독·자원 정리", "0 / 상태 없음",reservoir.Energy.ToString(),reservoir.Energy==0 && AliveAllies().All(x=>!x.HasStatusKey(reservoir.AuraKey)));
            reservoir.CastCode(); hands.CastCode();
            foreach(Unit unit in AliveAllies()) { unit.RestoreHpOutsideCombat(unit.HpMax); unit.ClearUltimateResource(); }
            // 일반행동으로 참여하는 지휘는 보호막을 주되 프레이아의 치유 일반행동은 발사하지 않는다.
            hands.StopCode();
            using(var log=new CfLog(AliveAllies()))
            {
                before=ally.ShieldCurr;
                yield return NordAction(napoleon,Kind.Special,s=>s.AdvanceAction(napoleon,1));
                YvCheck("지휘 사격 수르트 참여", "수르트 피해 ≥1 / 보호막 1회", log.Dealt.Count(x=>x.Attacker==surtr).ToString(),log.Dealt.Any(x=>x.Attacker==surtr) && ally.ShieldCurr-before==smallShield);
                YvCheck("지휘 사격 프레이아 제외", "피해0 / 온기0",log.Dealt.Count(x=>x.Attacker==freya)+"/"+reservoir.Energy,log.Dealt.All(x=>x.Attacker!=freya) && reservoir.Energy==0);
            }
            hands.CastCode();
            // 충분한 에너지/부족 체력에서 직접 사망시켜 지속 오라의 수명도 검증한다.
            freya.AddCombatResource(FreyaBrisingamen.Resource,cap);
            freya.RemoveShield(freya.ShieldCurr);
            freya.TakeDamage(new DamageContext(enemy,freya.HpMax*100,BaseEnums.CodeType.Effect,new List<int>()));
            YvCheck("프레이아 사망 오라·자원 정리", "비활성 / 온기0 / 전원 오라 없음",freya.isActive+"/"+reservoir.Energy,
                !freya.isActive && reservoir.Energy==0 && AliveAllies().All(x=>!x.HasStatusKey(reservoir.AuraKey)));
            ally.ModifyHp(ally.HpMax-100); ally.Heal(100,freya);
            YvCheck("사망 뒤 치유 이벤트 구독 없음", "0",reservoir.Energy.ToString(),reservoir.Energy==0);
            yield return CaptureStill("nord_core_complete");
            CfFinish(wasEnabled);
        }
        private IEnumerator ScenarioNordNatural(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();
            yield return PrepareStage(config,6,false,false,false);
            Unit surtr=CfAlly(81), freya=CfAlly(82);
            int cost=0, healed=0, attacks=0, heals=0, peakEnergy=0;
            float peakBonus=0;
            void Paid(Unit unit,int amount){if(unit==surtr)cost+=amount;}
            void Healed(Unit target,Unit source,int actual,int overheal){if(source==freya){healed+=actual;heals++;}}
            void Damage(DamageResolvedContext ctx){if(ctx.Attacker==surtr)attacks++;}
            HealthCost.AnyPaid+=Paid; Unit.AnyHealed+=Healed; Unit.AnyDamageDealt+=Damage;
            GameManager.Instance.StartRound();
            float deadline=Time.realtimeSinceStartup+90f, nextCapture=0;
            while(GameManager.Instance.gameState==BaseEnums.GameState.RoundInProgress && Time.realtimeSinceStartup<deadline)
            {
                if(freya.ActiveUltimateCode is FreyaBrisingamen warmth){peakEnergy=Mathf.Max(peakEnergy,warmth.Energy);peakBonus=Mathf.Max(peakBonus,warmth.DamageBonus);}
                if(Time.realtimeSinceStartup>=nextCapture){yield return EndOfFrame;Capture("nord_natural");nextCapture=Time.realtimeSinceStartup+2f;}
                yield return null;
            }
            HealthCost.AnyPaid-=Paid;Unit.AnyHealed-=Healed;Unit.AnyDamageDealt-=Damage;
            YvCheck("자연 전투 무적 미사용", "양 진영 무적 없음",DebugMode.AllyInvincible+"/"+DebugMode.EnemyInvincible,!DebugMode.AllyInvincible && !DebugMode.EnemyInvincible);
            YvCheck("자연 전투 종료", "전투 상태 탈출",GameManager.Instance.gameState.ToString(),GameManager.Instance.gameState!=BaseEnums.GameState.RoundInProgress);
            YvCheck("자연 전투 자해·실치유·온기", "비용/치유/저장 >0",$"비용{cost} 치유{healed} 저장{peakEnergy} 최대오라{peakBonus:P1} / 공격{attacks} 치유이벤트{heals}",cost>0 && healed>0 && peakEnergy>0 && attacks>0 && peakBonus<=.30001f);
            yield return CaptureStill("nord_natural_complete");
            if(_yvFailures.Count>0)FailCurrent(string.Join(" / ",_yvFailures));_yvFailures.Clear();
        }
        private IEnumerator ScenarioNordVisual(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); _cfActionIdSequence=0; bool wasEnabled=true;
            yield return YvPrepareScene(config,value=>wasEnabled=value);
            Unit surtr=CfAlly(81),freya=CfAlly(82);
            FindAnyObjectByType<Managers.UI.HUD.BattleHud>()?.SetPhase(BaseEnums.GameState.RoundInProgress,AliveEnemies().Count);
            float previousCaptureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f/60f;
            _nordFinite=true; _nordSeenMeshes=0; _nordFrameCount=0;
            foreach(Unit unit in AliveAllies()) {unit.ModifyHp(Mathf.CeilToInt(unit.HpMax*.70f)); unit.ClearUltimateResource();}
            foreach(Unit unit in AliveEnemies()) YvResetEnemy(unit);
            yield return NordAction(surtr,Kind.Ultimate,s=>surtr.FillUltimateResource(),"surtr_ultimate");
            yield return NordAction(freya,Kind.Normal,s=>s.AdvanceAction(freya,1),"freya_normal");
            var resource=CardMotion.ViewOf(freya)?.GetComponentsInChildren<TMPro.TextMeshPro>().FirstOrDefault(t=>t.name=="ResourceValue" && t.enabled);
            resource?.ForceMeshUpdate();
            float fraction=freya.GetCombatResource(FreyaBrisingamen.Resource)/(float)freya.GetCombatResourceMaximum(FreyaBrisingamen.Resource);
            YvCheck("온기 실제 HUD 표시",$"온기 {fraction*100:0}%",resource?.text??"없음",resource!=null && resource.text==$"온기 {fraction*100:0}%" && !resource.isTextTruncated && resource.textInfo.characterInfo.Take(resource.textInfo.characterCount).Any(c=>c.isVisible));
            yield return NordAction(surtr,Kind.Normal,s=>s.AdvanceAction(surtr,1),"surtr_normal");
            var warmth=(FreyaBrisingamen)freya.ActiveUltimateCode;
            freya.AddCombatResource(FreyaBrisingamen.Resource,freya.GetCombatResourceMaximum(FreyaBrisingamen.Resource));
            Unit foe=AliveEnemies().First();
            surtr.RemoveShield(surtr.ShieldCurr); surtr.ModifyHp(Mathf.FloorToInt(surtr.HpMax*.30f));
            yield return NordAction(foe,Kind.Additional,s=>s.EnqueueAdditional(foe,"nord_visual_emergency","긴급 구조 트리거",()=>
                surtr.TakeDamage(new DamageContext(foe,10,BaseEnums.CodeType.Effect,new List<int>()))),"freya_emergency");
            YvCheck("연속 메시 전체 프레임 유효", "유한 좌표 / 실제 메시 >0", $"finite={_nordFinite} probes={_nordSeenMeshes} frames={_nordFrameCount}",_nordFinite && _nordSeenMeshes>0);
            yield return new WaitForSeconds(1f);
            YvCheck("잔광 메시 수명 정리", "0",FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Count(x=>x.sharedMesh!=null && x.sharedMesh.name=="NordRibbon48").ToString(),
                !FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Any(x=>x.sharedMesh!=null && x.sharedMesh.name=="NordRibbon48"));
            NordFx.FreyaEmergency(freya); yield return new WaitForSeconds(.15f);
            var petals=FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Where(x=>x.sharedMesh!=null && x.sharedMesh.name=="NordRibbon48").ToArray();
            var snapshot=petals.Select(x=>x.sharedMesh.vertices).ToArray();
            var pause=GameObject.Find("BattlePauseButton").GetComponent<Button>(); pause.onClick.Invoke();
            yield return WaitUnscaled(.20f);
            YvCheck("꽃잎 실제 일시정지", "메시 정지 / pause=true / scale0",petals.Length.ToString(),Managers.UI.HUD.BattleHud.IsPaused && Time.timeScale==0 && petals.Length>0 && petals.Select((x,i)=>x!=null && x.sharedMesh.vertices.SequenceEqual(snapshot[i])).All(x=>x));
            pause.onClick.Invoke();
            yield return new WaitForSeconds(1f);
            Time.captureDeltaTime = previousCaptureDelta;
            yield return CaptureStill("nord_visual_complete");
            CfFinish(wasEnabled);
        }
    }
}
#endif
