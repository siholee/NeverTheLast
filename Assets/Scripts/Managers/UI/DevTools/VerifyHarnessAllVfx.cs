#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Core;
using Effects;
using Effects.Vfx;
using Entities;
using Entities.View;
using UnityEngine;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator AllVfxSample(string tag, float seconds=.72f)
        {
            for(int i=0;i<6;i++)
            {
                yield return new WaitForSeconds(seconds/6);
                yield return EndOfFrame;
                Capture(tag+"_phase"+i);
            }
        }

        private IEnumerator AllVfxActionSample(string tag)
        {
            // 시전뿐 아니라 피격·후속 발톱까지 연속 기록하여 띄엄띄엄 찍은 정점으로 시간을 판단하지 않는다.
            for(float age=0f;age<3f;age+=Time.deltaTime)
            {
                yield return EndOfFrame;
                Capture(tag+"_opening");
            }
        }

        private static bool VfxBeneficialSlot(VfxSlotData slot) => slot.partId.StartsWith("hit.support") ||
            slot.partId.StartsWith("affected.buff") || slot.partId is "affected.lokapala.hud" or "affected.varuna.shield" ||
            slot.partId.StartsWith("affected.agni.ultimate");

        private IEnumerator ScenarioAllVfxCatalog(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();
            bool enabled=true;
            yield return YvPrepareScene(config,value=>enabled=value);
            // 진입 시 오라 알림이 부품 사진에 섞이지 않게 표시 수명이 끝난 뒤 촬영한다.
            yield return WaitFeedbackTime(1.6f);
            Unit caster=AliveAllies().First(),target=AliveEnemies().First();
            if(config.vfxCasterEnemyId>0)
            {
                caster=AliveEnemies().FirstOrDefault(x=>x.ID==1002)??AliveEnemies().First();caster.InitializeUnit(true,config.vfxCasterEnemyId);
                target=AliveAllies().First();
                YvCheck("집중 VFX 실제 적 시전자","적ID "+config.vfxCasterEnemyId,
                    caster.ID+"/"+caster.IsEnemy,caster.ID==config.vfxCasterEnemyId&&caster.IsEnemy);
                yield return WaitUnscaled(.65f);
            }
            else if(config.vfxCasterAllyId>0)
            {
                caster=AliveAllies().FirstOrDefault(x=>x.ID==config.vfxCasterAllyId)??AliveAllies().First();
                if(caster.ID!=config.vfxCasterAllyId)caster.InitializeUnit(false,config.vfxCasterAllyId);
                YvCheck("집중 VFX 실제 아군 시전자","아군ID "+config.vfxCasterAllyId,
                    caster.ID+"/"+caster.IsEnemy,caster.ID==config.vfxCasterAllyId&&!caster.IsEnemy);
                yield return WaitFeedbackTime(.65f);
            }
            if(config.vfxTargetAllyId>0)
            {
                target=AliveAllies().FirstOrDefault(x=>x.ID==config.vfxTargetAllyId)
                    ??AliveAllies().First(x=>x!=caster);
                if(target.ID!=config.vfxTargetAllyId)target.InitializeUnit(false,config.vfxTargetAllyId);
                YvCheck("집중 VFX 실제 아군 대상","아군ID "+config.vfxTargetAllyId,
                    target.ID+"/"+target.IsEnemy,target.ID==config.vfxTargetAllyId&&!target.IsEnemy);
                yield return WaitFeedbackTime(.65f);
            }
            // 시전자 재초기화로 다시 붙은 오라의 실제 알림도 끝난 뒤 독립 부품을 촬영한다.
            yield return WaitFeedbackTime(2f);
            VfxSkillData probe=VfxDefinitionCatalog.Skills.First();
            List<VfxSlotData> original=probe.slots;
            float previousDelta=Time.captureDeltaTime;Time.captureDeltaTime=1f/60f;
            try
            {
                bool focused=config.vfxPartIds!=null&&config.vfxPartIds.Length>0;
                if(focused)
                    foreach(string id in config.vfxPartIds)
                        YvCheck("집중 VFX 등록 "+id,"등록 부품",id,VfxDefinitionCatalog.Part(id)!=null);
                foreach(VfxPartData part in VfxDefinitionCatalog.Parts.Where(p=>!focused||config.vfxPartIds.Contains(p.id)))
                {
                    string slot=part.category=="screen"?"screen":part.category=="affected"?"affected":part.category=="hit"?"hit":part.category=="prepare"?"prepare":"cast";
                    probe.slots=new List<VfxSlotData>{new() {slot=slot,partId=part.id,triggersImpact=slot=="hit",portraitAnchor=slot=="hit",direction="casterToTarget"}};
                    bool resource=string.IsNullOrWhiteSpace(part.sheet) || part.renderer=="clawTriple" || Resources.Load<Texture2D>("VfxTex/Handpaint/"+part.sheet)!=null;
                    YvCheck("VFX 리소스 "+part.id,"시트/전용 렌더러 존재",resource.ToString(),resource);
                    int callbacks=0; bool done=false;
                    Vector3 cameraBefore=Camera.main!=null?Camera.main.transform.position:Vector3.zero;
                    float cameraTravel=0f;
                    float gatePulse=0f;
                    UnitCardView gateView=part.id=="affected.lokapala.hud"?CardMotion.ViewOf(target):null;
                    int statusCount=target.GetAllStatuses().Count();
                    bool automaticEvent=false;
                    void Peak(VfxSlotPlaybackEvent e)
                    {
                        if(e.PartId==part.id&&e.Phase=="start"&&e.SkillKey=="status.auto")automaticEvent=true;
                        if(e.PartId==part.id && e.Phase=="peak")StartCoroutine(CaptureStill("part_"+part.id+"_exact_peak"));
                    }
                    VfxComposer.SlotPlayed+=Peak;
                    IEnumerator Hit()
                    {
                        yield return VfxComposer.PlayHitSlot(probe.key,0,caster,target,()=>callbacks++);
                        done=true;
                    }
                    if(slot=="screen") VfxComposer.PlayScreen(probe.key,caster,target);
                    else if(slot=="affected"&&config.vfxAutomaticStatusRoute)VfxComposer.PlayAffectedPart(target,part.id,caster);
                    else if(slot=="affected") VfxComposer.PlaySlot(probe.key,slot,0,caster,target,VfxSpace.WorldPoint(target),VfxSpace.SortingLayer(target));
                    else if(slot=="hit" && (part.renderer=="clawTriple" || !string.IsNullOrWhiteSpace(part.sheet) && part.renderer!="supportBuff" && part.renderer!="supportHeal" && part.renderer!="supportShield" && part.renderer!="supportCleanse"))
                        StartCoroutine(Hit());
                    else VfxComposer.PlaySlot(probe.key,slot,0,caster,target,slot is "cast" or "prepare"?VfxSpace.WorldPoint(caster):VfxSpace.PortraitPoint(target),VfxSpace.SortingLayer(target));
                    if(focused)
                    {
                        int frames=Mathf.Clamp(config.vfxPartCaptureFrames,60,180);
                        for(int frame=0;frame<frames;frame++)
                        {
                            yield return EndOfFrame;
                            if(slot=="screen" && Camera.main!=null)
                                cameraTravel=Mathf.Max(cameraTravel,Vector3.Distance(cameraBefore,Camera.main.transform.position));
                            if(gateView!=null)gatePulse=Mathf.Max(gatePulse,gateView.ActionGatePulseStrength);
                            Capture("part_"+part.id+"_live"+frames);
                        }
                    }
                    else yield return AllVfxSample("part_"+part.id);
                    VfxComposer.SlotPlayed-=Peak;
                    if(part.id=="affected.lokapala.hud")
                    {
                        YvCheck("로카팔라 HUD 실제 표시", "정보판 펄스 정점>0.5",gatePulse.ToString("F3"),gateView!=null&&gatePulse>.5f);
                        YvCheck("로카팔라 HUD 종료", "60프레임 후 완전 소멸",gateView?.ActionGatePulseStrength.ToString("F3")??"뷰 없음",
                            gateView!=null&&gateView.ActionGatePulseStrength<.001f);
                        YvCheck("로카팔라 HUD 상태 불변", "가짜 지속 버프 추가 없음",target.GetAllStatuses().Count().ToString(),target.GetAllStatuses().Count()==statusCount);
                    }
                    if(focused && (part.id is "screen.shake.medium" or "screen.flash.front"))
                    {
                        YvCheck("화면 진동 실행 "+part.id,"카메라 실제 변위 발생",cameraTravel.ToString("F5"),cameraTravel>0.00001f);
                        float residual=Camera.main!=null?Vector3.Distance(cameraBefore,Camera.main.transform.position):float.PositiveInfinity;
                        YvCheck("화면 진동 복귀 "+part.id,"60프레임 후 위치 복귀",residual.ToString("F5"),residual<0.003f);
                    }
                    if(slot=="hit" && (part.renderer=="clawTriple" || !string.IsNullOrWhiteSpace(part.sheet) && part.renderer.StartsWith("support")==false))
                    {
                        float guard=2;
                        while(!done && guard>0) {guard-=Time.deltaTime;yield return null;}
                        YvCheck("VFX 정점 "+part.id,"완료·콜백1회",callbacks+"/"+done,done&&callbacks==1);
                    }
                    bool finite=FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).All(f=>f.sharedMesh==null || f.sharedMesh.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)));
                    YvCheck("VFX 좌표 "+part.id,"유한 좌표",finite.ToString(),finite);
                    if(slot=="affected"&&config.vfxAutomaticStatusRoute)
                        YvCheck("자동 상태 부여 VFX 경로 "+part.id,"status.auto 시작 사건",automaticEvent.ToString(),automaticEvent);
                    BattlefieldBackdrop.ClearFocus();
                    if(focused && slot=="screen")
                    {
                        // 유지형 초월기 화면은 종료 사건 전후를 별도 촬영한다. f59 유지가 잔류 오류는 아니다.
                        YvCheck("화면 강조 해제 "+part.id,"해제 후 강도 0",BattlefieldBackdrop.CurrentFocusStrength.ToString(),
                            Mathf.Approximately(BattlefieldBackdrop.CurrentFocusStrength,0f));
                        for(int frame=0;frame<8;frame++)
                        {
                            yield return EndOfFrame;
                            Capture("part_"+part.id+"_release8");
                        }
                    }
                    yield return new WaitForSeconds(.38f);
                }
            }
            finally {probe.slots=original;Time.captureDeltaTime=previousDelta;GameManager.Instance.enabled=enabled;}
            if(_yvFailures.Count>0)throw new InvalidOperationException(string.Join(" / ",_yvFailures));
        }

        private IEnumerator ScenarioAllVfxSkills(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();bool enabled=true;
            yield return YvPrepareScene(config,value=>enabled=value);
            Unit caster=AliveAllies().First(),target=AliveEnemies().First();
            float previousDelta=Time.captureDeltaTime;Time.captureDeltaTime=1f/60f;
            Action<VfxSlotPlaybackEvent> activePeak=null;
            try
            {
                bool focused=config.vfxSkillKeys!=null&&config.vfxSkillKeys.Length>0;
                if(focused)
                    foreach(string key in config.vfxSkillKeys)
                        YvCheck("집중 VFX 조합 등록 "+key,"등록 조합",key,VfxDefinitionCatalog.Skill(key)!=null);
                foreach(VfxSkillData skill in VfxDefinitionCatalog.Skills.Where(s=>!focused||config.vfxSkillKeys.Contains(s.key)))
                {
                    int callback=0;bool done=false;
                    float visible=0f, peakVisible=0f;
                    var priorRenderers=FindObjectsByType<Renderer>(FindObjectsSortMode.None).Select(r=>r.GetInstanceID()).ToHashSet();
                    bool attack=skill.slots.Any(slot=>slot.slot is "hit" or "affected" && !VfxBeneficialSlot(slot));
                    bool support=!attack&&skill.slots.Any(VfxBeneficialSlot);
                    Unit[] receivers={attack?target:caster};
                    bool receivingSlots=skill.slots.Any(slot=>slot.slot is "hit" or "affected");
                    IEnumerator PeakPixels(VfxSlotPlaybackEvent e)
                    {
                        yield return EndOfFrame;
                        float pixels=VfxPixelCoverage(priorRenderers,receivers,support,!receivingSlots);
                        if(e.SlotName is "hit" or "affected" || !receivingSlots)peakVisible=Mathf.Max(peakVisible,pixels);
                        Capture("skill_"+e.SkillKey+"_"+e.PartId+"_event_peak");
                    }
                    void Peak(VfxSlotPlaybackEvent e)
                    { if(e.SkillKey==skill.key&&e.Phase=="peak")StartCoroutine(PeakPixels(e)); }
                    VfxComposer.SlotPlayed+=Peak;
                    activePeak=Peak;
                    IEnumerator Play()
                    {
                        yield return VfxComposer.PlaySingleTarget(skill.key,caster,target,()=>callback++);
                        foreach(VfxSlotData slot in skill.slots.Where(x=>x.slot=="affected"))
                            VfxComposer.PlayAffectedSlot(skill.key,skill.slots.Where(x=>x.slot=="affected").ToList().IndexOf(slot),caster,VfxBeneficialSlot(slot) ? caster : target);
                        done=true;
                    }
                    StartCoroutine(Play());
                    yield return VerifiedVfxSamples("skill_"+skill.key,priorRenderers,receivers,support,v=>visible=Mathf.Max(visible,v),!receivingSlots);
                    float guard=3;
                    while(!done && guard>0){guard-=Time.deltaTime;yield return null;}
                    YvCheck("VFX 조합 "+skill.key,"완료·콜백1회",callback+"/"+done,done&&callback==1);
                    bool hasVisual=skill.slots.Any(slot=>slot.slot!="screen" &&
                        (VfxDefinitionCatalog.Part(slot.partId)?.renderer!="cardAdvance"||slot.partId is "cast.anubis.book" or "cast.uchi.guard"));
                    if(hasVisual)
                    {
                        float minimum=receivingSlots ? .02f : .03f;
                        string region=receivingSlots?"수신자 원화 2%":"시전자 손·가슴 띠 3%";
                        YvCheck("VFX 실제 가시 픽셀 "+skill.key,"새 효과·"+region+" 이상",visible.ToString("P3"),visible>=minimum);
                        YvCheck("VFX 정점 가시 픽셀 "+skill.key,"정점 사건 렌더 후 "+region+" 이상",peakVisible.ToString("P3"),peakVisible>=minimum);
                    }
                    VfxComposer.SlotPlayed-=Peak;
                    activePeak=null;
                    BattlefieldBackdrop.ClearFocus();yield return new WaitForSeconds(.35f);
                }
            }
            finally{if(activePeak!=null)VfxComposer.SlotPlayed-=activePeak;Time.captureDeltaTime=previousDelta;GameManager.Instance.enabled=enabled;}
            if(_yvFailures.Count>0)throw new InvalidOperationException(string.Join(" / ",_yvFailures));
        }

        private IEnumerator ScenarioAllVfxUnits(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();bool enabled=true;
            yield return YvPrepareScene(config,value=>enabled=value);
            float previousDelta=Time.captureDeltaTime;Time.captureDeltaTime=1f/60f;
            foreach(Unit unit in AliveAllies())CfNoCrit(unit);
            void Peak(VfxSlotPlaybackEvent e)
            {
                if(e.Phase=="peak")StartCoroutine(CaptureStill("actual_"+(e.Caster?.ID??0)+"_"+e.SkillKey+"_"+e.PartId));
            }
            VfxComposer.SlotPlayed+=Peak;
            try
            {
                Unit[] actors=AliveAllies().Where(unit=>config.vfxCasterAllyId<=0 || unit.ID==config.vfxCasterAllyId).ToArray();
                if(config.vfxCasterAllyId>0)
                    YvCheck("실제 캐릭터 집중 촬영 대상", "지정 ID 전장에 1명",actors.Length.ToString(),actors.Length==1);
                foreach(Unit unit in actors)
                {
                    bool finished=false;
                    void End(Unit actor,ActionScheduler.ActionKind kind){if(actor==unit&&kind==ActionScheduler.ActionKind.Normal)finished=true;}
                    ActionScheduler.AnyActionFinished+=End;
                    try
                    {
                        // 연속 PNG 기록은 벽시계 비용이 있다. 실제 행동·후속 큐 완료까지 하나의 기록기만 기다린다.
                        int vfxStart=_result.vfxSlots.Count;
                        yield return NordAction(unit,ActionScheduler.ActionKind.Normal,
                            s=>s.AdvanceAction(unit,1),"unit_"+unit.ID+"_normal");
                        YvCheck("실제 일반행동 "+unit.UnitName,"행동 완료",finished.ToString(),finished);
                        if(unit.ID==64)
                        {
                            var normalFx=_result.vfxSlots.Skip(vfxStart).Where(e=>e.casterInstanceId==unit.GetInstanceID()&&e.skillKey=="vayu.gust").ToArray();
                            int casts=normalFx.Count(e=>e.slot=="cast"&&e.phase=="start");
                            int hits=normalFx.Count(e=>e.slot=="hit"&&e.phase=="peak");
                            YvCheck("바유 실제 일반 시전/피격 키 연결","cast 1·hit 정점 1",casts+"/"+hits,casts==1&&hits==1);
                        }
                    }
                    finally {ActionScheduler.AnyActionFinished-=End;}
                    if(unit.ActiveUltimateCode==null || !unit.ActiveUltimateCode.IsAutoCast)continue;
                    finished=false;
                    void Ult(Unit actor,ActionScheduler.ActionKind kind){if(actor==unit&&kind==ActionScheduler.ActionKind.Ultimate)finished=true;}
                    ActionScheduler.AnyActionFinished+=Ult;
                    try
                    {
                        yield return NordAction(unit,ActionScheduler.ActionKind.Ultimate,
                            s=>unit.FillUltimateResource(),"unit_"+unit.ID+"_ultimate");
                        YvCheck("실제 초월기 "+unit.UnitName,"행동 완료",finished.ToString(),finished);
                    }
                    finally {ActionScheduler.AnyActionFinished-=Ult;}
                }
            }
            finally{VfxComposer.SlotPlayed-=Peak;Time.captureDeltaTime=previousDelta;GameManager.Instance.enabled=enabled;}
            if(_yvFailures.Count>0)throw new InvalidOperationException(string.Join(" / ",_yvFailures));
        }

        private IEnumerator ScenarioAllVfxSystems(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); bool enabled=true;
            yield return YvPrepareScene(config,value=>enabled=value);
            Unit source=AliveAllies().First(),target=AliveEnemies().First();
            try
            {
                foreach(BaseEnums.UnitElement element in Enum.GetValues(typeof(BaseEnums.UnitElement)))
                {
                    if(element==BaseEnums.UnitElement.None)continue;
                    bool granted=target.GrantCombatElement(element,2,source,true,true);
                    yield return new WaitForSeconds(.14f);
                    yield return CaptureStill("element_"+element);
                    YvCheck("실제 원소 부착 "+element,"부착·표시",granted.ToString(),granted&&target.HasAttachedElement(element));
                    target.RemoveCombatElement(element);yield return new WaitForSeconds(.48f);
                }
                foreach(string name in Effects.Negative.ElementalReaction.AllReactions().Select(x=>x.Name).Distinct())
                {
                    ElementReactionFx.Reaction(target,name);
                    yield return new WaitForSeconds(.14f);yield return CaptureStill("reaction_visual_"+name);
                    yield return new WaitForSeconds(.48f);
                }
                foreach(string kind in StatusMotifFx.Kinds)
                {
                    StatusMotifFx.Play(target,kind);
                    yield return new WaitForSeconds(.14f);yield return CaptureStill("state_"+kind);
                    yield return new WaitForSeconds(.48f);
                }
                UnitCardView pulseView=CardMotion.ViewOf(source);
                pulseView?.PulseActionGateHud(new Color(.80f,.85f,.95f));
                pulseView?.PulseSupportHud(false,true,new Color(.80f,.90f,.88f));
                yield return new WaitForSeconds(.06f);
                bool done=false;int hits=0;
                IEnumerator Hit(){yield return VfxComposer.PlayHitSlot("enemy.ultimate.smash",0,source,target,()=>hits++);done=true;}
                StartCoroutine(Hit());yield return new WaitForSeconds(.05f);
                StatusMotifFx.Play(target,"curse");yield return null;
                SpriteFx[] sprites=FindObjectsByType<SpriteFx>(FindObjectsSortMode.None);
                var positions=sprites.Select(x=>x.transform.localScale).ToArray();
                float pulseBefore=pulseView?.ActionGatePulseStrength??0f;
                RectTransform hpPulse=pulseView?.GetComponentsInChildren<RectTransform>(true)
                    .FirstOrDefault(r=>r.name=="HpFill");
                Vector3 hpPulseBefore=hpPulse!=null?hpPulse.localScale:Vector3.one;
                var pause=GameObject.Find("BattlePauseButton").GetComponent<UnityEngine.UI.Button>();
                pause.onClick.Invoke();yield return WaitUnscaled(1.40f);
                bool paused=Managers.UI.HUD.BattleHud.IsPaused && Time.timeScale==0 && !done && hits==0 && sprites.Length>0 && sprites.Select((x,i)=>x!=null&&x.transform.localScale==positions[i]).All(x=>x);
                YvCheck("공용 시트·상태 VFX 일시정지","1.4초 동안 변형/타격 진행 없음",paused.ToString(),paused);
                YvCheck("가호 HUD 펄스 일시정지","표시 중인 펄스가 정지 전 값 유지",
                    pulseBefore.ToString("F3")+"/"+(pulseView?.ActionGatePulseStrength??0f).ToString("F3"),
                    pulseView!=null&&pulseBefore>.1f&&Mathf.Abs(pulseView.ActionGatePulseStrength-pulseBefore)<.001f);
                YvCheck("치유 HUD 펄스 일시정지","체력 막대의 팝 배율 유지",
                    hpPulseBefore.ToString()+"/"+(hpPulse!=null?hpPulse.localScale.ToString():"뷰 없음"),
                    hpPulse!=null&&hpPulseBefore.x>1.01f&&Vector3.Distance(hpPulse.localScale,hpPulseBefore)<.001f);
                yield return CaptureStill("pause_effects");pause.onClick.Invoke();
                float guard=3;while(!done&&guard>0){guard-=Time.deltaTime;yield return null;}
                YvCheck("일시정지 해제 타격","콜백 1회",hits+"/"+done,done&&hits==1);
                yield return new WaitForSeconds(2);
                YvCheck("공용 VFX 잔상 정리","활성 시트·스프라이트 0",FindObjectsByType<SpriteFx>(FindObjectsSortMode.None).Length.ToString(),FindObjectsByType<SpriteFx>(FindObjectsSortMode.None).Length==0&&FindObjectsByType<FlipbookFx>(FindObjectsSortMode.None).Length==0);
            }
            finally {if(Managers.UI.HUD.BattleHud.IsPaused)GameObject.Find("BattlePauseButton")?.GetComponent<UnityEngine.UI.Button>()?.onClick.Invoke();GameManager.Instance.enabled=enabled;}
            if(_yvFailures.Count>0)throw new InvalidOperationException(string.Join(" / ",_yvFailures));
        }

        private IEnumerator ScenarioAllVfxFormation(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();bool enabled=true;
            yield return YvPrepareScene(config,value=>enabled=value);
            float previousDelta=Time.captureDeltaTime;Time.captureDeltaTime=1f/60f;
            var events=new List<VfxSlotPlaybackEvent>();
            void Slot(VfxSlotPlaybackEvent e){events.Add(e);}
            VfxComposer.SlotPlayed+=Slot;
            try
            {
                foreach(Unit ally in AliveAllies())CfNoCrit(ally);
                Unit shi=CfAlly(ShiId),sabah=CfAlly(SabahId),sei=CfAlly(SeiId);
                var bell=shi.ActiveUltimateCode as Codes.Ultimate.ShiFinalBell;
                foreach(int tier in new[]{6,9})
                {
                    Unit[] enemies=AliveEnemies().ToArray();
                    bool done=false;events.Clear();bell.DebugSetStack(tier);
                    using(var log=new CfLog(AliveAllies()))
                    {
                        IEnumerator Play(){yield return bell.ResolveBell(tier,enemies[0]);done=true;}
                        StartCoroutine(Play());
                        yield return NordCapture("formation_final_bell_"+tier,()=>done);
                        var hits=log.Dealt.Where(d=>d.Attacker==shi&&d.DamageContext.CodeType==BaseEnums.CodeType.Ultimate).ToList();
                        bool once=hits.Count==enemies.Length&&enemies.All(e=>hits.Count(d=>d.Target==e)==1);
                        YvCheck("실전 만종 "+tier,"모든 적 정확히 한 번",hits.Count+"/"+enemies.Length,once);
                        YvCheck("실전 만종 정점 "+tier,"연기 정점 사건 한 번",events.Count(e=>e.Phase=="peak"&&e.PartId=="hit.smoke.horizontal").ToString(),
                            events.Count(e=>e.Phase=="peak"&&e.PartId=="hit.smoke.horizontal")==1);
                    }
                    yield return new WaitForSeconds(.8f);
                }
                YvCheck("실전 만종 자원","9단계 이후 0",shi.GetCombatResource(Codes.Ultimate.ShiFinalBell.ResourceId).ToString(),
                    shi.GetCombatResource(Codes.Ultimate.ShiFinalBell.ResourceId)==0);
                events.Clear();
                using(var log=new CfLog(AliveAllies()))
                {
                    Unit[] enemies=AliveEnemies().ToArray();
                    yield return NordAction(sabah,ActionScheduler.ActionKind.Ultimate,s=>sabah.FillUltimateResource(),"formation_actual_azrael");
                    var hits=log.Dealt.Where(d=>d.Attacker==sabah&&d.DamageContext.CodeType==BaseEnums.CodeType.Ultimate).ToList();
                    YvCheck("실전 아즈라엘 타격","모든 적 정확히 한 번",hits.Count+"/"+enemies.Length,
                        hits.Count==enemies.Length&&enemies.All(e=>hits.Count(d=>d.Target==e)==1));
                    int lightning=events.FindIndex(e=>e.SkillKey=="sabah.azrael"&&e.SlotName=="hit"&&e.SlotIndex==0&&e.Phase=="peak");
                    int smoke=events.FindIndex(e=>e.SkillKey=="sabah.azrael"&&e.PartId=="hit.smoke.horizontal"&&e.Phase=="peak");
                    YvCheck("실전 아즈라엘 연출 순서","번개 정점 이후 연기 정점",lightning+" → "+smoke,lightning>=0&&smoke>lightning);
                }
                yield return PresentationFrames("formation_azrael_tail",1.1f);
                var bigbang=sei.ActivePassiveCodes.OfType<Codes.Passive.SeiBigBang>().FirstOrDefault();
                using(var log=new CfLog(AliveAllies()))
                {
                    bool started=bigbang!=null&&bigbang.DebugReleaseExecutable();
                    YvCheck("실전 빅뱅 시전","실제 본체 시작",started.ToString(),started);
                    if(started)yield return NordCapture("formation_actual_bigbang",()=>!sei.isCasting);
                    // 행동 분류는 추가행동, 피해 분류는 패시브 공격이다. 실제 빅뱅 피해 컨텍스트로 센다.
                    int hits=log.Dealt.Count(d=>d.Attacker==sei&&d.DamageContext.CodeType==BaseEnums.CodeType.Passive&&d.DamageContext.HasTag(DamageTag.PassiveAttack));
                    YvCheck("실전 빅뱅 타수","결정 7회",hits.ToString(),hits==7);
                }
            }
            finally{VfxComposer.SlotPlayed-=Slot;Time.captureDeltaTime=previousDelta;GameManager.Instance.enabled=enabled;}
            if(_yvFailures.Count>0)throw new InvalidOperationException(string.Join(" / ",_yvFailures));
        }

        private IEnumerator ScenarioSoulReadability(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();bool enabled=true;
            yield return YvPrepareScene(config,value=>enabled=value);
            float previousDelta=Time.captureDeltaTime;Time.captureDeltaTime=1f/60f;
            try
            {
                Unit anubis=CfAlly(121),enemy=AliveEnemies().First();
                string key=Codes.Passive.AnubisSoulHarvest.SoulKey;
                float before=anubis.GetPermanentStacks(key)+anubis.GetPermanentStackFraction(key);
                yield return CaptureStill("soul_before");
                bool previousInvincible=DebugMode.EnemyInvincible;DebugMode.EnemyInvincible=false;
                enemy.TakeDamage(new BaseClasses.DamageContext(anubis,enemy.HpCurr+1,BaseEnums.CodeType.Normal,
                    new List<int>{BaseClasses.DamageTag.SingleTarget,BaseClasses.DamageTag.TrueDamage},false));
                DebugMode.EnemyInvincible=previousInvincible;
                YvCheck("실제 피해 사망","HP0·사망",enemy.HpCurr+"/"+enemy.isActive,enemy.HpCurr<=0&&!enemy.isActive);
                bool erosion=FindObjectsByType<ErosionFx>(FindObjectsInactive.Exclude,FindObjectsSortMode.None)
                    .Any(fx=>fx.transform.root.name.StartsWith("CardDeparture_"));
                YvCheck("사망 실제 원화 침식 연결","원화 복제본에 ErosionFx 존재",erosion.ToString(),erosion);
                yield return PresentationFrames("soul_whole_gain",.72f);
                float after=anubis.GetPermanentStacks(key)+anubis.GetPermanentStackFraction(key);
                string label=CardMotion.ViewOf(anubis).GetComponentsInChildren<TMPro.TMP_Text>(true)
                    .Select(text=>text.text).FirstOrDefault(text=>text.StartsWith("영혼 "));
                YvCheck("영혼 실제 획득 표시","사망 1회·누적 실수 표시",$"{before}->{after}/{label}",
                    after>before&&label==$"영혼 {after:0.0}");
                // 원래 고유 특성의 사망 이벤트를 실행하며, 표시용 Soul 호출로 게임 값을 만들지 않는다.
                YvCheck("영혼 백분율 오표시 제거","무한 상한 비율 사용 안 함",label??"없음",label!=null&&!label.Contains("%"));
            }
            finally{Time.captureDeltaTime=previousDelta;GameManager.Instance.enabled=enabled;}
            if(_yvFailures.Count>0)throw new InvalidOperationException(string.Join(" / ",_yvFailures));
        }

        private IEnumerator ScenarioAllVfxShowcase(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();bool enabled=true;
            yield return YvPrepareScene(config,value=>enabled=value);
            float previousDelta=Time.captureDeltaTime;
            Time.captureDeltaTime=1f/60f;
            try
            {
                foreach(Unit ally in AliveAllies()){CfNoCrit(ally);ally.ModifyHp(Mathf.CeilToInt(ally.HpMax*.65f));}
                foreach(Unit unit in AliveAllies().ToArray())
                {
                    foreach(Unit ally in AliveAllies())ally.ClearUltimateResource();
                    bool ultimate=unit.ActiveUltimateCode!=null&&unit.ActiveUltimateCode.IsAutoCast;
                    yield return NordAction(unit,ultimate?ActionScheduler.ActionKind.Ultimate:ActionScheduler.ActionKind.Normal,
                        s=>{if(ultimate)unit.FillUltimateResource();else s.AdvanceAction(unit,1);},"show_"+unit.ID+"_"+(ultimate?"ultimate":"normal"));
                }
                YvCheck("60fps 실제 행동 쇼케이스","행동 완료 / 무적 해제",DebugMode.AllyInvincible+"/"+DebugMode.EnemyInvincible,!DebugMode.AllyInvincible&&!DebugMode.EnemyInvincible);
            }
            finally{Time.captureDeltaTime=previousDelta;GameManager.Instance.enabled=enabled;}
            if(_yvFailures.Count>0)throw new InvalidOperationException(string.Join(" / ",_yvFailures));
        }
    }
}
#endif
