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
using Combat;
using Entities.View;
using UnityEngine;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator AllVfxSample(string tag, float seconds=.72f, Action sample=null)
        {
            for(int i=0;i<6;i++)
            {
                yield return new WaitForSeconds(seconds/6);
                yield return EndOfFrame;
                sample?.Invoke();
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

        private void AuditPortraitContact(Unit target)
        {
            var renderer=CardMotion.ViewOf(target)?.PortraitRenderer;
            if(renderer?.sprite==null)return;
            Sprite sprite=renderer.sprite;Texture2D texture=sprite.texture;
            Vector3 point=VfxSpace.ContactPoint(target),local=renderer.transform.InverseTransformPoint(point);
            if(renderer.flipX)local.x=-local.x;
            if(renderer.flipY)local.y=-local.y;
            int x=Mathf.Clamp(Mathf.FloorToInt(sprite.rect.x+sprite.pivot.x+local.x*sprite.pixelsPerUnit),0,texture.width-1);
            int y=Mathf.Clamp(Mathf.FloorToInt(sprite.rect.y+sprite.pivot.y+local.y*sprite.pixelsPerUnit),0,texture.height-1);
            RenderTexture previous=RenderTexture.active;
            RenderTexture temporary=RenderTexture.GetTemporary(texture.width,texture.height,0,RenderTextureFormat.ARGB32);
            Texture2D probe=new(1,1,TextureFormat.RGBA32,false);
            try
            {
                Graphics.Blit(texture,temporary);RenderTexture.active=temporary;
                probe.ReadPixels(new Rect(x,y,1,1),0,0,false);probe.Apply();
                float alpha=probe.GetPixel(0,0).a;
                float hudGap=(point.y-VfxSpace.PortraitHudTop(target))/VfxSpace.CardSize;
                var profile=VfxDefinitionCatalog.PortraitContact(sprite.name);
                if(profile!=null)
                {
                    var view=CardMotion.ViewOf(target);
                    float worldLift=view.Body.TransformVector(Vector3.up*profile.presentationLiftInCards*view.CardSize).y/VfxSpace.CardSize;
                    YvCheck("등록 원화 몸통 여백 "+target.ID,"원본 표시 lift0.16·접점도 HUD에서 함께 분리",
                        $"configured={profile.presentationLiftInCards:F3},worldLift={worldLift:F3},hudGap={hudGap:F3}",
                        Mathf.Approximately(profile.presentationLiftInCards,.16f)&&hudGap>=worldLift+.02f);
                }
                Vector4 face=VfxSpace.PortraitFaceWorldRect(target);
                bool insideFace=face.z>face.x&&face.w>face.y&&point.x>=face.x&&point.x<=face.z&&point.y>=face.y&&point.y<=face.w;
                YvCheck("실제 원화 접점 "+target.ID,"alpha>=0.5·HUD 위·얼굴 제외·유한 좌표",
                    $"sprite={sprite.name},world={point},pixel={x}/{y},alpha={alpha:F3},hudGap={hudGap:F3},insideFace={insideFace},face={face}",
                    alpha>=.5f&&hudGap>=.02f&&!insideFace&&float.IsFinite(point.x)&&float.IsFinite(point.y));
            }
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(temporary);Destroy(probe);}
        }

        private void DiagnosePortraitContact(Unit target,string part)
        {
            if(target==null)return;
            Vector3 contact=VfxSpace.ContactPoint(target);Camera camera=Camera.main;
            Vector4 face=VfxSpace.PortraitFaceWorldRect(target);
            var points=new List<string>();
            foreach(var fx in FindObjectsByType<FlipbookFx>(FindObjectsSortMode.None).Where(f=>f.Recipient==target&&f.name.StartsWith("FlipbookFx_")))
            {
                var body=fx.GetComponentsInChildren<SpriteRenderer>().FirstOrDefault(r=>r.name=="Body");
                if(body==null)continue;
                var properties=new MaterialPropertyBlock();body.GetPropertyBlock(properties);
                points.Add($"{fx.name}:screen={camera.WorldToScreenPoint(fx.transform.position)},pivot={body.sprite.pivot},frame={body.sprite.name},faceUniform={properties.GetVector("_WorldFaceExcludeRect")}");
            }
            RecordMechanic("접점 진단 "+part,"실제 앵커/합성층/얼굴 영역 기록",
                $"target={target.ID},contactScreen={camera.WorldToScreenPoint(contact)},faceWorld={face};"+string.Join(";",points),points.Count>0);
            // 검수 전용 주석: 청록 점=실제 접점, 빨간 사각=얼굴 제외. 최종 미술 증거로 사용하지 않는다.
            SpriteFx.Play(VfxSpace.HardDot,contact,Color.cyan,false,.035f*VfxSpace.CardSize,.035f*VfxSpace.CardSize,.15f,
                fadeIn:0f,fadeOutFrom:.9f,sortingLayer:VfxSpace.SortingLayer(target),order:VfxSpace.OverlaySortingOrder+1900,preserveInk:true);
            if(face.z<=face.x||face.w<=face.y)return;
            void Edge(Vector3 a,Vector3 b)
            {
                Vector3 delta=b-a;
                SpriteFx.Play(VfxSpace.GlyphLine,(a+b)*.5f,Color.red,false,new Vector2(delta.magnitude,.007f*VfxSpace.CardSize),
                    new Vector2(delta.magnitude,.007f*VfxSpace.CardSize),.15f,rotation:Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,
                    fadeIn:0f,fadeOutFrom:.9f,sortingLayer:VfxSpace.SortingLayer(target),order:VfxSpace.OverlaySortingOrder+1900,preserveInk:true);
            }
            Edge(new(face.x,face.y),new(face.z,face.y));Edge(new(face.x,face.w),new(face.z,face.w));
            Edge(new(face.x,face.y),new(face.x,face.w));Edge(new(face.z,face.y),new(face.z,face.w));
        }

        private IEnumerator AuditMovingContact(Unit target,HashSet<int> renderedCrests)
        {
            int samples=0,crestSamples=0,nativeSamples=0;float maximum=0f,nativeMaximum=0f;bool sawCrest=false;
            var crestIds=new HashSet<int>();
            for(int i=0;i<5;i++)
            {
                yield return EndOfFrame;
                foreach(var anchor in FindObjectsByType<FlipbookFx>(FindObjectsSortMode.None).Where(a=>a.Recipient==target&&a.name=="FlipbookFx_Hit"))
                {nativeSamples++;nativeMaximum=Mathf.Max(nativeMaximum,Vector3.Distance(anchor.transform.position,VfxSpace.ContactPoint(target))/VfxSpace.CardSize);}
                foreach(var fx in FindObjectsByType<FlipbookFx>(FindObjectsSortMode.None)
                    .Where(f=>f.ContactLocked&&f.Recipient==target&&!f.name.Contains("Drops")))
                {
                    maximum=Mathf.Max(maximum,Vector3.Distance(fx.transform.position,VfxSpace.ContactPoint(target))/VfxSpace.CardSize);
                    samples++;
                    if(fx.RequiredVisibleFrame>=0)
                    {
                        crestSamples++;
                        crestIds.Add(fx.GetInstanceID());
                        sawCrest|=renderedCrests.Contains(fx.GetInstanceID());
                    }
                }
            }
            // 고FPS의 5프레임은 핵심 셀에 도달하기 전일 수 있다. 접점 샘플 뒤 실제 연출 시간까지 관측한다.
            float crestDeadline=Time.time+.35f;
            while(crestSamples>0&&!sawCrest&&Time.time<crestDeadline)
            {
                yield return EndOfFrame;
                sawCrest=crestIds.Any(renderedCrests.Contains);
            }
            YvCheck("반동 중 실제 원화 접점 추종 "+target.ID,"샘플>0·UV 접점 오차<=0.01카드",
                $"samples={samples},maxError={maximum:F5}",samples>0&&maximum<=.01f);
            if(nativeSamples>0)YvCheck("구매팩 실제 원화 접점 추종 "+target.ID,"반동 뒤 원본 베이크 접점 오차<=0.01카드",
                $"samples={nativeSamples},maxError={nativeMaximum:F5}",nativeMaximum<=.01f);
            if(crestSamples>0)YvCheck("과장 정점 실제 렌더 "+target.ID,"핵심 셀을 최소 한 렌더 프레임 관측",
                $"samples={crestSamples},seen={sawCrest}",sawCrest);
        }

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
            else if(config.vfxTargetEnemyId>0)
            {
                target=AliveEnemies().First();target.InitializeUnit(true,config.vfxTargetEnemyId);
                YvCheck("집중 VFX 실제 적 대상","적ID "+config.vfxTargetEnemyId,
                    target.ID+"/"+target.IsEnemy,target.ID==config.vfxTargetEnemyId&&target.IsEnemy);
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
                            if(frame%_actionCaptureStride==0)Capture("part_"+part.id+"_live"+frames);
                        }
                    }
                    else yield return AllVfxSample("part_"+part.id,sample:()=>
                    {if(gateView!=null)gatePulse=Mathf.Max(gatePulse,gateView.ActionGatePulseStrength);});
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
            float previousDelta=Time.captureDeltaTime;
            bool previousDeferred=_deferCaptureEncoding;
            bool realtime=config.trigger=="realtime";
            Time.captureDeltaTime=realtime?0f:1f/60f;
            _deferCaptureEncoding=realtime;
            foreach(Unit unit in AliveAllies())CfNoCrit(unit);
            foreach(Unit unit in AliveAllies())
            {YvBoostEnemy(unit);unit.ModifyHp(Mathf.RoundToInt(unit.HpMax*.65f));}
            // 상태 연출 촬영은 효과 명중을 보장한 fixture다. 실제 저항 확률 검증은 별도 시나리오가 맡는다.
            foreach(Unit unit in AliveAllies().Where(u=>u.ID is 164 or 165 or 166 or 167))
                CfEnsureEffectHit(unit,AliveEnemies());
            foreach(Unit unit in AliveAllies().Concat(AliveEnemies()))AuditPortraitContact(unit);
            DebugMode.AllyInvincible=DebugMode.EnemyInvincible=false;
            var renderedCrests=new HashSet<int>();
            void ObserveCrests(Camera camera)
            {
                if(camera!=Camera.main)return;
                foreach(var fx in FindObjectsByType<FlipbookFx>(FindObjectsSortMode.None))
                    if(fx.RequiredVisibleFrame>=0&&fx.DisplayedFrame==fx.RequiredVisibleFrame&&
                        fx.GetComponentsInChildren<SpriteRenderer>().Any(r=>r.enabled&&r.isVisible&&r.color.a>.05f))
                        renderedCrests.Add(fx.GetInstanceID());
            }
            // 피격 알림보다 먼저 정점을 그린 기존 잔광도 실제 카메라 렌더 이력으로 확인한다.
            if(realtime)Camera.onPostRender+=ObserveCrests;
            void Peak(VfxSlotPlaybackEvent e)
            {
                if(realtime&&e.Phase=="peak"&&e.SlotName=="hit"&&e.Target!=null&&
                    e.Caster?.ID==config.vfxCasterAllyId&&
                    VfxDefinitionCatalog.Part(e.PartId)?.contactMotif is "poison" or "wound" or "pierce" or "arc" or "field")
                    StartCoroutine(AuditMovingContact(e.Target,renderedCrests));
                if(config.trigger=="contactdiag"&&e.Phase=="peak"&&e.SlotName=="hit"&&e.Caster?.ID==config.vfxCasterAllyId)
                    DiagnosePortraitContact(e.Target,e.PartId);
                if(!config.vfxMechanicsOnly&&e.Phase=="peak")StartCoroutine(CaptureStill("actual_"+(e.Caster?.ID??0)+"_"+e.SkillKey+"_"+e.PartId));
            }
            VfxComposer.SlotPlayed+=Peak;
            try
            {
                Unit[] actors=AliveAllies().Where(unit=>config.vfxCasterAllyId<=0 || unit.ID==config.vfxCasterAllyId).ToArray();
                if(config.trigger=="anchors")
                {
                    if(_yvFailures.Count>0)throw new InvalidOperationException(string.Join(" / ",_yvFailures));
                    yield break;
                }
                if(config.vfxCasterAllyId>0)
                    YvCheck("실제 캐릭터 집중 촬영 대상", "지정 ID 전장에 1명",actors.Length.ToString(),actors.Length==1);
                foreach(Unit unit in actors)
                {
                    // 앞 캐릭터의 실제 행동이 fixture 상태를 해제할 수 있으므로 촬영 직전에 다시 확보한다.
                    if(unit.ID is 164 or 165 or 166 or 167)
                    {
                        // Replace형 보정을 기존 보정이 더해진 값으로 재계산하면 0으로 덮어쓸 수 있다.
                        unit.RemoveStatusByKey("verify_cf_hit");
                        CfEnsureEffectHit(unit,AliveEnemies());
                    }
                    int damageCount=0,damageFrame=-1,hitFrame=-1,primaryActionId=-1,primaryHitCount=0;
                    string damageSummary="";
                    var numberTotals=new Dictionary<Unit,(int amount,int hits)>();
                    var primaryTargets=new HashSet<Unit>();
                    var existingBleed=AliveEnemies().Where(e=>e.HasStatusKey(ActionDot.Key(ActionDotKind.Bleed))).ToHashSet();
                    void Damage(DamageResolvedContext e)
                    {
                        if(e.Attacker!=unit)return;
                        var context=e.DamageContext;
                        bool primary=context.CodeType==BaseEnums.CodeType.Normal&&!context.IsSharedDamage&&!context.IsDistributed&&
                            !context.IsDamageOverTime&&!context.HasTag(DamageTag.BonusDamage)&&!context.HasTag(DamageTag.ElementalReaction)&&
                            GameManager.Instance.ActionScheduler.CurrentActionId==primaryActionId;
                        damageSummary+=$"target={e.Target.ID}/{e.Target.GetInstanceID()},damage={e.DamageDealt},type={context.CodeType},shared={context.IsSharedDamage},tags={string.Join(",",context.DamageTags)},primary={primary};";
                        if(!primary)return;
                        primaryTargets.Add(e.Target);
                        damageCount++;damageFrame=Time.frameCount;
                        if(unit.ID==168&&!config.vfxMechanicsOnly)
                        {
                            numberTotals.TryGetValue(e.Target,out var prior);
                            numberTotals[e.Target]=(prior.amount+e.DamageDealt,prior.hits+1);
                            var numbers=FindObjectsByType<DamagePopup>(FindObjectsSortMode.None)
                                .Where(p=>p.Recipient==e.Target&&p.ActionId==primaryActionId).ToArray();
                            var expected=numberTotals[e.Target];
                            YvCheck("니콜 연타 실제 피해 누계 "+damageCount,
                                $"target={e.Target.ID}/{e.Target.GetInstanceID()},sum={expected.amount},hits={expected.hits}",
                                $"sum={numbers.Sum(p=>p.DisplayedAmount)},hits={numbers.Sum(p=>p.HitCount)},popups={numbers.Length}",
                                numbers.Sum(p=>p.DisplayedAmount)==expected.amount&&numbers.Sum(p=>p.HitCount)==expected.hits);
                            YvCheck("니콜 연타 숫자 대상 귀속 "+damageCount,"숫자 중심이 자기 대상에 가장 가까움",
                                string.Join(";",numbers.Select(p=>((RectTransform)p.transform).anchoredPosition.ToString())),
                                numbers.Length>0&&numbers.All(p=>CombatFeedbackSurface.BelongsTo(e.Target,((RectTransform)p.transform).anchoredPosition)));
                            YvCheck("니콜 연타 숫자 실제 얼굴 제외 "+damageCount,"등록 원화 얼굴과 숫자/누계 영역 겹침0",
                                string.Join(";",numbers.Select(p=>p.OccupiedRect.ToString())),
                                numbers.Length>0&&numbers.All(p=>!CombatFeedbackSurface.OverlapsAuthoredFace(e.Target,p.OccupiedRect)));
                        }
                    }
                    void Hit(VfxSlotPlaybackEvent e)
                    {
                        if(e.Caster==unit&&e.Phase=="peak"&&e.SlotName=="hit"&&GameManager.Instance.ActionScheduler.CurrentActionId==primaryActionId)
                            primaryHitCount++;
                        if(e.Caster==unit&&(e.SkillKey is "shi.destruction_slash" or "himiko.ember")&&e.Phase=="peak"&&e.SlotName=="hit"&&
                            GameManager.Instance.ActionScheduler.CurrentActionId==primaryActionId&&hitFrame<0)hitFrame=Time.frameCount;
                    }
                    void Begin(Unit actor,ActionScheduler.ActionKind kind,string label)
                    {if(actor==unit&&kind==ActionScheduler.ActionKind.Normal&&primaryActionId<0)primaryActionId=GameManager.Instance.ActionScheduler.CurrentActionId;}
                    Unit.AnyDamageDealt+=Damage;VfxComposer.SlotPlayed+=Hit;
                    ActionScheduler.AnyActionStarted+=Begin;
                    bool finished=false;
                    void End(Unit actor,ActionScheduler.ActionKind kind){if(actor==unit&&kind==ActionScheduler.ActionKind.Normal)finished=true;}
                    ActionScheduler.AnyActionFinished+=End;
                    try
                    {
                        // 연속 PNG 기록은 벽시계 비용이 있다. 실제 행동·후속 큐 완료까지 하나의 기록기만 기다린다.
                        int vfxStart=_result.vfxSlots.Count;
                        int arrowsBefore=PurchasedArcherFx.EmittedArrows,contactsBefore=PurchasedArcherFx.ResolvedContacts;
                        yield return NordAction(unit,ActionScheduler.ActionKind.Normal,
                            s=>s.AdvanceAction(unit,1),config.vfxMechanicsOnly?null:"unit_"+unit.ID+"_normal");
                        YvCheck("실제 일반행동 "+unit.UnitName,"행동 완료",finished.ToString(),finished);
                        if(unit.ID==165)
                        {
                            int arrows=PurchasedArcherFx.EmittedArrows-arrowsBefore;
                            int contacts=PurchasedArcherFx.ResolvedContacts-contactsBefore;
                            YvCheck("구매팩 화살 묶음과 실제 단일 판정","화살3·실제 피해1·명중1",
                                $"arrows={arrows},damage={damageCount},contacts={contacts}",arrows==3&&damageCount==1&&contacts==1);
                        }
                        string roster=unit.ID switch{164=>"nephthys",165=>"atalante",166=>"neith",167=>"ereshkigal",168=>"nicol",162=>"astolfo",_=>null};
                        if(roster!=null)
                        {
                            var normalFx=_result.vfxSlots.Skip(vfxStart).Where(e=>e.casterInstanceId==unit.GetInstanceID()).ToArray();
                            string expected="hit."+roster+".contact";
                            YvCheck("로스터 고유 피격 연결 "+unit.UnitName,expected+"·차용 달문양 없음",
                                string.Join(",",normalFx.Select(e=>e.partId).Distinct()),
                                normalFx.Any(e=>e.partId==expected&&e.phase=="peak")&&normalFx.All(e=>e.partId!="hit.tsukuyomi.normal"));
                            if(unit.ID==165)
                            {
                                var contact=normalFx.FirstOrDefault(e=>e.partId==expected&&e.phase=="peak");
                                var poison=normalFx.FirstOrDefault(e=>e.partId=="affected.dot.poison"&&e.phase=="start");
                                YvCheck("풀 화살 착탄 뒤 실제 맹독 표시","착탄 이후 맹독 표시·상태 판정 지연 없음",
                                    $"hit={contact?.frame},poison={poison?.frame},delay={VfxDefinitionCatalog.Part("affected.dot.poison").affectedDelay}",
                                    contact!=null&&poison!=null&&poison.frame>contact.frame&&VfxDefinitionCatalog.Part("affected.dot.poison").affectedDelay>=.24f);
                            }
                            if(unit.ID==167)
                            {
                                // RefreshDuration은 신규 상태 사건을 다시 발행하지 않는다. 실제 대상의 적용 행동ID로 갱신을 확인한다.
                                bool refreshed=primaryTargets.Any(t=>existingBleed.Contains(t)&&t.GetAllStatuses().Any(
                                    s=>s.Key==ActionDot.Key(ActionDotKind.Bleed)&&s.AppliedActionId==primaryActionId));
                                YvCheck("에레쉬키갈 실제 출혈 전용 경로","신규 bleed VFX 또는 기존 출혈의 실제 행동ID 갱신·burn 표시0",
                                    string.Join(",",normalFx.Select(e=>e.partId).Distinct())+$";refresh={refreshed},action={primaryActionId}",
                                    (normalFx.Any(e=>e.partId=="affected.dot.bleed")||refreshed)&&normalFx.All(e=>e.partId!="affected.dot.burn"));
                            }
                        }
                        if(unit.ID==168)YvCheck("니콜 실제 일반 6탄 정점","기존 6탄·본 행동의 피격6회",primaryHitCount.ToString(),primaryHitCount==6);
                        if(unit.ID==162)YvCheck("아스톨포 비공격 표시","본 일반 피해0",damageCount.ToString(),damageCount==0);
                        if(unit.ID==2)
                        {
                            YvCheck("멸참 피해 1회·피격 정점 동기화","피해1회·같은프레임",
                                $"count={damageCount},peakFrame={hitFrame},damageFrame={damageFrame};{damageSummary}",
                                damageCount==1&&hitFrame>=0&&hitFrame==damageFrame);
                        }
                        if(unit.ID==44)
                        {
                            YvCheck("불씨 피해 1회·피격 정점 동기화","피해1회·같은프레임",
                                $"count={damageCount},peakFrame={hitFrame},damageFrame={damageFrame};{damageSummary}",
                                damageCount==1&&hitFrame>=0&&hitFrame==damageFrame);
                            int marks=AliveEnemies().Sum(e=>e.ActiveStatuses.Count(s=>s.Key=="himiko_blaze"));
                            YvCheck("불씨 맹화 중복 부여 없음","실제 표식1개",marks.ToString(),marks==1);
                        }
                        if(unit.ID==64)
                        {
                            var normalFx=_result.vfxSlots.Skip(vfxStart).Where(e=>e.casterInstanceId==unit.GetInstanceID()&&e.skillKey=="vayu.gust").ToArray();
                            int casts=normalFx.Count(e=>e.slot=="cast"&&e.phase=="start");
                            int hits=normalFx.Count(e=>e.slot=="hit"&&e.phase=="peak");
                            YvCheck("바유 실제 일반 시전/피격 키 연결","cast 1·hit 정점 1",casts+"/"+hits,casts==1&&hits==1);
                        }
                    }
                    finally
                    {
                        ActionScheduler.AnyActionFinished-=End;
                        ActionScheduler.AnyActionStarted-=Begin;
                        Unit.AnyDamageDealt-=Damage;VfxComposer.SlotPlayed-=Hit;
                        if(realtime){FlushDeferredCaptures();_deferCaptureEncoding=true;}
                    }
                    if(realtime)yield return null;
                    if(unit.ID==44&&config.trigger=="emberpause")
                        yield return AuditHimikoContactLifetime(AliveEnemies().First());
                    if(unit.ActiveUltimateCode==null || !unit.ActiveUltimateCode.IsAutoCast)continue;
                    if(!unit.ActiveUltimateCode.HasValidTarget())
                    {
                        _result.warnings.Add("초월기 촬영 보류 "+unit.UnitName+": 이 편성/시점에 유효 대상 없음 — 미실행");
                        continue;
                    }
                    finished=false;
                    var ultDamage=new List<(Unit Target,int Frame,int Amount)>();
                    var impactFrames=new Dictionary<Unit,int>();
                    var ultTargets=AliveEnemies().ToArray();
                    var auditedPaint=new HashSet<int>();
                    IEnumerator AuditContactPaint()
                    {
                        yield return EndOfFrame;
                        foreach(var fx in FindObjectsByType<FlipbookFx>(FindObjectsSortMode.None))
                        {
                            var body=fx.GetComponentsInChildren<SpriteRenderer>().FirstOrDefault(r=>r.name=="Body");
                            if(body==null||body.sortingOrder<VfxSpace.OverlaySortingOrder+171||!auditedPaint.Add(fx.GetInstanceID()))continue;
                            Bounds b=body.bounds;Camera camera=Camera.main;
                            Vector3 min=camera.WorldToScreenPoint(b.min),max=camera.WorldToScreenPoint(b.max);
                            float visible=Mathf.Clamp01((Mathf.Min(camera.pixelHeight,max.y)-Mathf.Max(min.y,0f))/Mathf.Max(1f,max.y-min.y));
                            YvCheck("아즈라엘 접점 렌더 영역","화면 안 가시높이25% 이상·카드 마스크 없음",
                                $"order={body.sortingOrder},frame={body.sprite?.name},color={body.color},screen={min}/{max},visible={visible:F2}",
                                visible>=.25f&&!body.sharedMaterial.HasProperty("_WorldClipMinY"));
                            if(body.sprite!=null&&body.sprite.name.EndsWith("_02",StringComparison.Ordinal))
                            {
                                Bounds local=body.sprite.bounds;
                                Vector3 crest=body.transform.TransformPoint(new Vector3(
                                    Mathf.Lerp(local.min.x,local.max.x,.51868f),Mathf.Lerp(local.min.y,local.max.y,.31534f),0f));
                                float crestAlpha=body.color.a;
                                YvCheck("아즈라엘 정점 심 형상 보존","실제 밝은 심의 표시알파95% 이상",
                                    $"order={body.sortingOrder},crestAlpha={crestAlpha:F3}",crestAlpha>=.95f);
                            }
                        }
                    }
                    void UltDamage(DamageResolvedContext e)
                    {
                        if(e.Attacker==unit&&e.DamageContext.CodeType==BaseEnums.CodeType.Ultimate)
                            ultDamage.Add((e.Target,Time.frameCount,e.DamageDealt));
                    }
                    void UltImpact(VfxSlotPlaybackEvent e)
                    {
                        if(e.Caster==unit&&e.SkillKey=="sabah.azrael"&&e.Phase=="impact")impactFrames[e.Target]=Time.frameCount;
                        if(config.trigger=="contactaudit"&&e.Caster==unit&&e.SkillKey=="sabah.azrael"&&e.Phase=="impact")StartCoroutine(AuditContactPaint());
                    }
                    Unit.AnyDamageDealt+=UltDamage;VfxComposer.SlotPlayed+=UltImpact;
                    void Ult(Unit actor,ActionScheduler.ActionKind kind){if(actor==unit&&kind==ActionScheduler.ActionKind.Ultimate)finished=true;}
                    ActionScheduler.AnyActionFinished+=Ult;
                    try
                    {
                        int arrowsBefore=PurchasedArcherFx.EmittedArrows;
                        yield return NordAction(unit,ActionScheduler.ActionKind.Ultimate,
                            s=>unit.FillUltimateResource(),config.vfxMechanicsOnly?null:"unit_"+unit.ID+"_ultimate");
                        YvCheck("실제 초월기 "+unit.UnitName,"행동 완료",finished.ToString(),finished);
                        if(unit.ID==165)
                        {
                            int arrows=PurchasedArcherFx.EmittedArrows-arrowsBefore;
                            YvCheck("구매팩 광역 화살과 대상별 단일 판정","대상당 화살5·실제 피해1",
                                $"arrows={arrows},targets={ultTargets.Length},damage={ultDamage.Count}",
                                arrows==ultTargets.Length*5&&ultDamage.Count==ultTargets.Length&&ultTargets.All(t=>ultDamage.Count(d=>d.Target==t)==1));
                        }
                        if(unit.ID==3)
                            YvCheck("아즈라엘 실제 대상별 피해 정점","각 대상 1회·접점과 같은프레임",
                                string.Join(";",ultDamage.Select(hit=>$"target={hit.Target.ID},damage={hit.Amount},frame={hit.Frame},impact={impactFrames.GetValueOrDefault(hit.Target,-1)}")),
                                ultDamage.Count==ultTargets.Length&&ultTargets.All(target=>ultDamage.Count(hit=>hit.Target==target)==1)&&
                                ultDamage.All(hit=>impactFrames.TryGetValue(hit.Target,out int frame)&&frame==hit.Frame));
                    }
                    finally
                    {
                        ActionScheduler.AnyActionFinished-=Ult;Unit.AnyDamageDealt-=UltDamage;VfxComposer.SlotPlayed-=UltImpact;
                        if(realtime){FlushDeferredCaptures();_deferCaptureEncoding=true;}
                    }
                    if(unit.ID==165&&realtime)
                    {
                        yield return AuditPurchasedArcherPursuit(unit);
                        FlushDeferredCaptures();_deferCaptureEncoding=true;
                    }
                }
            }
            finally{VfxComposer.SlotPlayed-=Peak;if(realtime){Camera.onPostRender-=ObserveCrests;FlushDeferredCaptures();}_deferCaptureEncoding=previousDeferred;Time.captureDeltaTime=previousDelta;GameManager.Instance.enabled=enabled;}
            if(_yvFailures.Count>0)throw new InvalidOperationException(string.Join(" / ",_yvFailures));
        }

        private IEnumerator ScenarioAllVfxEnemies(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();bool enabled=true;
            yield return YvPrepareScene(config,value=>enabled=value);
            float previousDelta=Time.captureDeltaTime;bool previousDeferred=_deferCaptureEncoding;
            bool realtime=config.trigger=="realtime";Time.captureDeltaTime=realtime?0f:1f/60f;
            _deferCaptureEncoding=realtime;
            Unit caster=AliveEnemies().First();
            DebugMode.EnemyInvincible=false;
            DebugMode.AllyInvincible=false;
            int[] ids=config.vfxEnemyIds?.Length>0?config.vfxEnemyIds:new[]{config.vfxCasterEnemyId};
            bool underlay=config.trigger=="underlay";
            var hidden=new List<SpriteRenderer>();
            void HideVfx(Camera camera)
            {
                if(!underlay||camera!=Camera.main)return;
                foreach(var renderer in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                    if(renderer.enabled&&renderer.sortingLayerName=="CombatVfx")
                    {hidden.Add(renderer);renderer.enabled=false;}
            }
            if(underlay)Camera.onPreCull+=HideVfx;
            void Peak(VfxSlotPlaybackEvent e)
            {
                if(!config.vfxMechanicsOnly&&e.Caster==caster&&e.Phase=="peak")
                    StartCoroutine(CaptureStill("enemy_"+caster.ID+"_"+e.SkillKey+"_"+e.PartId));
            }
            VfxComposer.SlotPlayed+=Peak;
            try
            {
                foreach(int id in ids.Distinct())
                {
                    caster.DebugResetCombatState();caster.InitializeUnit(true,id);
                    caster.RemoveStatusByKey(Combat.EnemyCombatBudget.StatusKey);
                    YvBoostEnemy(caster);CfNoCrit(caster);
                    if(config.trigger!="anchors")caster.CastPassiveCode();
                    yield return null;
                    AuditPortraitContact(caster);
                    if(config.trigger=="anchors")continue;
                    YvCheck("테마 적 실제 로드 "+id,"지정 적·전장 활성",caster.ID+"/"+caster.IsOnField,
                        caster.ID==id&&caster.IsEnemy&&caster.IsOnField&&caster.isActive);
                    foreach(var kind in new[]{ActionScheduler.ActionKind.Normal,ActionScheduler.ActionKind.Ultimate})
                    {
                        if(kind==ActionScheduler.ActionKind.Ultimate && (caster.ActiveUltimateCode==null||!caster.ActiveUltimateCode.IsAutoCast))continue;
                        foreach(Unit unit in AliveAllies().Concat(AliveEnemies()))
                        {
                            YvBoostEnemy(unit);unit.ClearUltimateResource();
                            unit.ModifyHp(Mathf.RoundToInt(unit.HpMax*.65f));
                        }
                        bool finished=false;int start=_result.vfxSlots.Count,damage=0;
                        void End(Unit actor,ActionScheduler.ActionKind action){if(actor==caster&&action==kind)finished=true;}
                        void Damage(DamageResolvedContext e){if(e.Attacker==caster)damage++;}
                        ActionScheduler.AnyActionFinished+=End;Unit.AnyDamageDealt+=Damage;
                        try
                        {
                            yield return NordAction(caster,kind,s=>
                            {
                                if(kind==ActionScheduler.ActionKind.Normal)s.AdvanceAction(caster,1);
                                else caster.FillUltimateResource();
                            },config.vfxMechanicsOnly?null:"enemy_"+id+"_"+kind);
                            YvCheck("테마 적 행동 "+id+"/"+kind,"실제 스케줄러 완료",finished+"/damage="+damage,finished);
                            var played=_result.vfxSlots.Skip(start).Where(e=>e.casterInstanceId==caster.GetInstanceID()).ToArray();
                            string mode=kind==ActionScheduler.ActionKind.Normal?Codes.Restored.RestoredEnemyKit.Find(caster)?.normalMode:
                                Codes.Restored.RestoredEnemyKit.Find(caster)?.ultimateMode;
                            if(Codes.Restored.RestoredEnemyKit.Support(mode))
                            {
                                string expected=Codes.Restored.RestoredEnemyKit.SupportFx(mode);
                                YvCheck("적 지원 연출 분리 "+id+"/"+kind,expected+"·장미 키 없음",
                                    string.Join(",",played.Select(e=>e.skillKey).Distinct()),
                                    played.Any(e=>e.skillKey==expected)&&played.All(e=>e.skillKey!="orpheus.lament"));
                                var kit=Codes.Restored.RestoredEnemyKit.Find(caster);
                                bool all=kind==ActionScheduler.ActionKind.Ultimate||kit.normalTargets>1;
                                string affected=mode=="Shield"?"hit.support.shield":mode=="Heal"?"hit.support.heal":"hit.support.buff";
                                int actual=played.Where(e=>e.skillKey=="status.auto"&&e.partId==affected&&e.phase=="start")
                                    .Select(e=>e.targetInstanceId).Distinct().Count();
                                int expectedRecipients=mode=="Charge"?1:all?AliveEnemies().Count():1;
                                YvCheck("적 지원 실제 수신자 "+id+"/"+kind,mode+" 대상"+expectedRecipients,
                                    actual.ToString(),actual==expectedRecipients);
                            }
                            bool finite=FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).All(f=>f.sharedMesh==null||
                                f.sharedMesh.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)));
                            YvCheck("적 연출 좌표 "+id+"/"+kind,"유한 좌표",finite.ToString(),finite);
                        }
                        finally
                        {
                            ActionScheduler.AnyActionFinished-=End;Unit.AnyDamageDealt-=Damage;
                            if(realtime){FlushDeferredCaptures();_deferCaptureEncoding=true;}
                        }
                        if(realtime)yield return null;
                    }
                }
            }
            finally
            {
                caster?.DebugResetCombatState();VfxComposer.SlotPlayed-=Peak;
                if(underlay){Camera.onPreCull-=HideVfx;foreach(var renderer in hidden)if(renderer!=null)renderer.enabled=true;}
                if(realtime)FlushDeferredCaptures();_deferCaptureEncoding=previousDeferred;
                Time.captureDeltaTime=previousDelta;GameManager.Instance.enabled=enabled;
            }
            if(_yvFailures.Count>0)throw new InvalidOperationException(string.Join(" / ",_yvFailures));
        }

        private IEnumerator AuditHimikoContactLifetime(Unit target)
        {
            // 실제 일반기 검사는 위에서 수행한다. 여기서는 새 국소 레이어의 정지/해제 수명만 검사한다.
            PortraitAccentFx.HimikoContact(target,VfxSpace.PortraitPoint(target));
            var paint=FindObjectsByType<FlipbookFx>(FindObjectsSortMode.None)
                .Where(f=>f.GetComponentsInChildren<SpriteRenderer>().Any(r=>r.name=="Body"&&
                    (r.sortingOrder==VfxSpace.OverlaySortingOrder+73||r.sortingOrder==VfxSpace.OverlaySortingOrder+74))).ToArray();
            var bodies=paint.Select(f=>f.GetComponentsInChildren<SpriteRenderer>().First(r=>r.name=="Body")).ToArray();
            var sprites=bodies.Select(r=>r.sprite).ToArray();
            var scales=paint.Select(f=>f.transform.localScale).ToArray();
            var colors=bodies.Select(r=>r.color).ToArray();
            var pause=GameObject.Find("BattlePauseButton").GetComponent<UnityEngine.UI.Button>();
            pause.onClick.Invoke();
            try
            {
                yield return WaitUnscaled(.25f);
                bool held=Managers.UI.HUD.BattleHud.IsPaused&&Time.timeScale==0f&&paint.Length==2&&
                    paint.Select((f,i)=>f!=null&&bodies[i]!=null&&bodies[i].sprite==sprites[i]&&
                        f.transform.localScale==scales[i]&&bodies[i].color==colors[i]).All(x=>x);
                YvCheck("불씨 국소 피격 일시정지","0.25초 동안 두 겹의 셀·크기·알파 유지",held.ToString(),held);
            }
            finally {if(Managers.UI.HUD.BattleHud.IsPaused)pause.onClick.Invoke();}
            // 고배속에서는 셀마다 최소 표시 프레임이 있다. 0.6 게임초는 5배에서 8셀 소진보다 짧다.
            yield return WaitUnscaled(.35f);
            YvCheck("불씨 국소 피격 수명 정리","재개 후 잔류0",paint.Count(f=>f!=null).ToString(),paint.All(f=>f==null));
        }

        private IEnumerator ScenarioHpDamageTrail(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();bool enabled=true;
            yield return YvPrepareScene(config,value=>enabled=value);
            Unit target=AliveEnemies().First();var view=CardMotion.ViewOf(target);
            var images=view.GetComponentsInChildren<UnityEngine.UI.Image>(true);
            var trail=images.Single(image=>image.name=="HpDamageTrail");
            var fill=images.Single(image=>image.name=="HpFill");
            float previousScale=Time.timeScale;
            try
            {
                target.ModifyHp(target.HpMax);view.Tick();
                target.ModifyHp(Mathf.RoundToInt(target.HpMax*.65f));view.Tick();
                float actual=target.HpCurr/(float)target.HpMax;
                YvCheck("피해 HUD 실제 값 즉시 적용","실체력 비율·잔량1",
                    $"hp={fill.rectTransform.anchorMax.x},trail={trail.rectTransform.anchorMax.x}",
                    Mathf.Abs(fill.rectTransform.anchorMax.x-actual)<.001f&&trail.enabled&&trail.rectTransform.anchorMax.x>.99f);
                Time.timeScale=0f;float paused=trail.rectTransform.anchorMax.x;
                yield return WaitUnscaled(.20f);
                YvCheck("피해 잔량 일시정지","잔량 불변",trail.rectTransform.anchorMax.x.ToString(),
                    Mathf.Approximately(paused,trail.rectTransform.anchorMax.x));
                Time.timeScale=previousScale;
                yield return WaitFeedbackTime(.4f);
                YvCheck("피해 잔량 수렴","실체력까지 정리",trail.rectTransform.anchorMax.x.ToString(),
                    !trail.enabled&&Mathf.Abs(trail.rectTransform.anchorMax.x-actual)<.001f);
                target.ModifyHp(Mathf.RoundToInt(target.HpMax*.45f));view.Tick();
                target.ModifyHp(Mathf.RoundToInt(target.HpMax*.85f));view.Tick();
                YvCheck("치유 때 가짜 피해 잔량 제거","잔량 숨김",trail.enabled.ToString(),!trail.enabled);
                target.ModifyHp(Mathf.RoundToInt(target.HpMax*.60f));view.Tick();
                view.Bind(target,true);
                YvCheck("카드 재연결 때 잔량 초기화","잔량 숨김",trail.enabled.ToString(),!trail.enabled);
                yield return CaptureStill("hp_trail_rebound");
                yield return WaitFeedbackTime(1.4f);
                DamagePopup.Show(target,205,false,Color.magenta);
                yield return WaitFeedbackTime(.65f);
                DamagePopup.Show(target,238,false,Color.magenta);
                yield return EndOfFrame;
                var labels=FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None)
                    .Where(label=>label.name=="Number"&&(label.text=="205"||label.text=="238")).ToArray();
                Rect GlyphRect(TMPro.TextMeshProUGUI label)
                {
                    label.ForceMeshUpdate();Bounds bounds=label.textBounds;
                    Vector2 min=RectTransformUtility.WorldToScreenPoint(null,label.transform.TransformPoint(bounds.min));
                    Vector2 max=RectTransformUtility.WorldToScreenPoint(null,label.transform.TransformPoint(bounds.max));
                    return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
                }
                var boxes=labels.Select(GlyphRect).ToArray();
                YvCheck("연속 피해 숫자 세로 분리","205·238 글자 비중첩·가로폭 절반 이내 세로 정렬",
                    string.Join(";",boxes.Select(box=>box.ToString())),
                    boxes.Length==2&&!boxes[0].Overlaps(boxes[1])&&
                    Mathf.Abs(boxes[0].center.x-boxes[1].center.x)<Mathf.Min(boxes[0].width,boxes[1].width)*.5f&&
                    Mathf.Abs(boxes[0].center.y-boxes[1].center.y)>Mathf.Max(boxes[0].height,boxes[1].height));
                Rect nearby=view.PortraitFocusScreenRect(Camera.main);
                float margin=120f*Screen.width/1920f;
                nearby=Rect.MinMaxRect(nearby.xMin-margin,nearby.yMin-margin,nearby.xMax+margin,nearby.yMax+margin);
                YvCheck("연속 피해 숫자 대상 인접","자기 원화에서 120px 이내",nearby.ToString(),boxes.All(box=>nearby.Contains(box.center)));
                yield return CaptureStill("consecutive_number_vertical");
                yield return WaitFeedbackTime(1.4f);
                Unit[] healTargets=AliveEnemies().ToArray();
                foreach(Unit recipient in healTargets)
                    DamagePopup.Show(recipient,12345,false,Color.green,CombatNumberKind.Heal);
                yield return EndOfFrame;
                var healLabels=FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None)
                    .Where(label=>label.name=="Number"&&label.text=="+12,345").Select(GlyphRect).ToArray();
                var cardViews=FindObjectsByType<UnitCardView>(FindObjectsSortMode.None);
                bool clear=healLabels.All(box=>cardViews.All(card=>!box.Overlaps(card.HudScreenRect(Camera.main))&&
                    !box.Overlaps(card.ResourceBadgeScreenRect(Camera.main))));
                string collisions=string.Join(";",healLabels.SelectMany(box=>cardViews.Where(card=>box.Overlaps(card.HudScreenRect(Camera.main))||
                    box.Overlaps(card.ResourceBadgeScreenRect(Camera.main))).Select(card=>$"glyph={box},unit={card.BoundUnit?.ID},hud={card.HudScreenRect(Camera.main)},badge={card.ResourceBadgeScreenRect(Camera.main)}")));
                YvCheck("동시 회복 숫자 HUD 분리","모든 글자와 체력·원소 HUD 비중첩",
                    healLabels.Length+"/"+healTargets.Length+"/"+clear+"/"+collisions,healLabels.Length==healTargets.Length&&clear);
                yield return CaptureStill("simultaneous_heal_number_layout_probe");
            }
            finally{Time.timeScale=previousScale;GameManager.Instance.enabled=enabled;}
            if(_yvFailures.Count>0)throw new InvalidOperationException(string.Join(" / ",_yvFailures));
        }

        private IEnumerator AuditSignatureFrames(Unit target)
        {
            foreach(string name in new[]{"Ward","Miasma","Resonance","Blessing","Arc","Pierce","Bleed","Seed","Drops","Pulse","Coil","Thunder","Cleave","Impact"})
            {
                var texture=Resources.Load<Texture2D>("VfxTex/Signature/"+name+"_16");
                YvCheck("연속 원화 실제 임포트 "+name,"16셀·긴 축1024px",
                    texture!=null?$"{texture.width}x{texture.height}":"리소스 없음",
                    texture!=null&&Mathf.Max(texture.width,texture.height)==1024);
            }
            PortraitAccentFx.ShieldWard(target);yield return null;yield return null;
            var sheets=FindObjectsByType<FlipbookFx>(FindObjectsSortMode.None).Where(f=>f.name=="FlipbookFx_Ward_16").ToArray();
            var sprites=sheets.SelectMany(f=>f.GetComponentsInChildren<SpriteRenderer>()).ToArray();
            var frames=sprites.Select(r=>r.sprite).ToArray();var colors=sprites.Select(r=>r.color).ToArray();
            var positions=sheets.Select(f=>f.transform.position).ToArray();
            var pause=GameObject.Find("BattlePauseButton")?.GetComponent<UnityEngine.UI.Button>();
            pause?.onClick.Invoke();yield return WaitUnscaled(.25f);
            bool stable=sheets.Length==2&&Managers.UI.HUD.BattleHud.IsPaused&&
                sprites.Select((r,i)=>r!=null&&r.sprite==frames[i]&&r.color==colors[i]).All(x=>x)&&
                sheets.Select((f,i)=>f!=null&&f.transform.position==positions[i]).All(x=>x);
            YvCheck("16프레임 결계 일시정지","0.25초 동안 두 합성층의 셀·알파·위치 유지",stable.ToString(),stable);
            if(Managers.UI.HUD.BattleHud.IsPaused)pause?.onClick.Invoke();
            yield return WaitUnscaled(.65f);
            YvCheck("16프레임 결계 소진","재개 후 두 합성층 잔류0",sheets.Count(f=>f!=null).ToString(),sheets.All(f=>f==null));
        }

        private IEnumerator ScenarioAllVfxSystems(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); bool enabled=true;
            yield return YvPrepareScene(config,value=>enabled=value);
            Unit source=AliveAllies().First(),target=AliveEnemies().First();
            try
            {
                if(config.trigger=="purchasedpause")
                {
                    yield return AuditPurchasedArcherPause(source,target);
                    if(_yvFailures.Count>0)throw new InvalidOperationException(string.Join(" / ",_yvFailures));
                    yield break;
                }
                yield return AuditSignatureFrames(target);
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
                RectTransform hpPulse=pulseView?.GetComponentsInChildren<RectTransform>(true)
                    .FirstOrDefault(r=>r.name=="HpFill");
                float pulseGuard=1f;
                while(pulseGuard>0f&&((pulseView?.ActionGatePulseStrength??0f)<.30f||hpPulse==null||hpPulse.localScale.y<1.02f))
                {pulseGuard-=Time.unscaledDeltaTime;yield return null;}
                bool done=false;int hits=0;
                IEnumerator Hit(){yield return VfxComposer.PlayHitSlot("enemy.ultimate.smash",0,source,target,()=>hits++);done=true;}
                StatusMotifFx.Play(target,"curse");
                // 5배속의 한 프레임이 .05초 대기를 넘어 정점까지 통과할 수 있다.
                // 예약한 프레임에 멈춰 실제 정지 구간에 콜백과 표시가 진행되는지 검사한다.
                StartCoroutine(Hit());
                SpriteFx[] sprites=FindObjectsByType<SpriteFx>(FindObjectsSortMode.None);
                var positions=sprites.Select(x=>x.transform.localScale).ToArray();
                float pulseBefore=pulseView?.ActionGatePulseStrength??0f;
                Vector3 hpPulseBefore=hpPulse!=null?hpPulse.localScale:Vector3.one;
                var pause=GameObject.Find("BattlePauseButton").GetComponent<UnityEngine.UI.Button>();
                pause.onClick.Invoke();yield return WaitUnscaled(1.40f);
                bool paused=Managers.UI.HUD.BattleHud.IsPaused && Time.timeScale==0 && !done && hits==0 && sprites.Length>0 && sprites.Select((x,i)=>x!=null&&x.transform.localScale==positions[i]).All(x=>x);
                YvCheck("공용 시트·상태 VFX 일시정지","1.4초 동안 변형/타격 진행 없음",
                    $"paused={Managers.UI.HUD.BattleHud.IsPaused},scale={Time.timeScale},done={done},hits={hits},sprites={sprites.Length},stable={paused}",paused);
                YvCheck("가호 HUD 펄스 일시정지","표시 중인 펄스가 정지 전 값 유지",
                    pulseBefore.ToString("F3")+"/"+(pulseView?.ActionGatePulseStrength??0f).ToString("F3"),
                    pulseView!=null&&pulseBefore>.1f&&Mathf.Abs(pulseView.ActionGatePulseStrength-pulseBefore)<.001f);
                YvCheck("치유 HUD 펄스 일시정지","체력 막대의 팝 배율 유지",
                    hpPulseBefore.ToString()+"/"+(hpPulse!=null?hpPulse.localScale.ToString():"뷰 없음"),
                    hpPulse!=null&&Mathf.Abs(hpPulseBefore.x-1f)<.001f&&hpPulseBefore.y>1.01f&&Vector3.Distance(hpPulse.localScale,hpPulseBefore)<.001f);
                yield return CaptureStill("pause_effects");pause.onClick.Invoke();
                float guard=3;while(!done&&guard>0){guard-=Time.deltaTime;yield return null;}
                YvCheck("일시정지 해제 타격","콜백 1회",hits+"/"+done,done&&hits==1);
                yield return new WaitForSeconds(2);
                YvCheck("공용 VFX 잔상 정리","활성 시트·스프라이트 0",FindObjectsByType<SpriteFx>(FindObjectsSortMode.None).Length.ToString(),FindObjectsByType<SpriteFx>(FindObjectsSortMode.None).Length==0&&FindObjectsByType<FlipbookFx>(FindObjectsSortMode.None).Length==0);
            }
            finally {if(Managers.UI.HUD.BattleHud.IsPaused)GameObject.Find("BattlePauseButton")?.GetComponent<UnityEngine.UI.Button>()?.onClick.Invoke();GameManager.Instance.enabled=enabled;}
            if(_yvFailures.Count>0)throw new InvalidOperationException(string.Join(" / ",_yvFailures));
        }

        private IEnumerator AuditPurchasedArcherPause(Unit source,Unit target)
        {
            int front=SortingLayer.GetLayerValueFromID(VfxSpace.SortingLayer(target));
            int card=FindObjectsByType<UnitCardView>(FindObjectsSortMode.None)
                .SelectMany(v=>v.GetComponentsInChildren<Renderer>()).Select(r=>SortingLayer.GetLayerValueFromID(r.sortingLayerID)).DefaultIfEmpty(0).Max();
            YvCheck("전투 VFX 공통 앞 레이어","CombatVfx가 카드·HUD보다 앞",$"vfx={front},card={card}",front>card);
            YvCheck("공통 합성 형상 보존","몸체·광 셰이더에 카드/얼굴/HUD 잘림 프로퍼티 없음",
                "VfxCompositeAlpha/VfxLuminanceGlow",new[]{VfxSpace.CompositeAlpha,VfxSpace.LuminanceGlow}.All(m=>
                    !m.HasProperty("_WorldClipMinY")&&!m.HasProperty("_WorldExcludeRect")&&!m.HasProperty("_WorldFaceExcludeRect")));
            float previousScale=Time.timeScale;
            var pause=GameObject.Find("BattlePauseButton").GetComponent<UnityEngine.UI.Button>();
            int hp=target.HpCurr;
            PurchasedArcherFx.Cast(source,new[]{target},"atalante.normal");
            yield return new WaitForSeconds(.10f);
            var arrows=FindObjectsByType<FlipbookFx>(FindObjectsSortMode.None).Where(p=>p.name=="FlipbookFx_Flight").ToArray();
            var expectedColor=ElementPalette.ColorFor(BaseEnums.UnitElement.Dendro);
            var bodies=arrows.Select(p=>p.GetComponentsInChildren<SpriteRenderer>().First(r=>r.name=="Body")).ToArray();
            YvCheck("범용 풀 화살 원소색","비행체 몸체가 공통 Dendro 팔레트와 일치",
                string.Join(";",bodies.Select(r=>r.color.ToString())),bodies.Length==3&&bodies.All(r=>
                    Mathf.Abs(r.color.r-expectedColor.r)<.001f&&Mathf.Abs(r.color.g-expectedColor.g)<.001f&&Mathf.Abs(r.color.b-expectedColor.b)<.001f));
            try
            {
                pause.onClick.Invoke();
                // 정지 입력이 들어온 프레임의 이미 계산된 엔진 시뮬레이션 뒤, 실제 표시된 정지 화면부터 비교한다.
                yield return EndOfFrame;
                var positions=arrows.Select(p=>p.transform.position).ToArray();
                var frames=arrows.Select(p=>p.DisplayedFrame).ToArray();
                var scales=arrows.Select(p=>p.transform.localScale).ToArray();
                var rotations=arrows.Select(p=>p.transform.rotation).ToArray();
                yield return WaitUnscaled(.22f);
                float positionError=arrows.Select((p,i)=>Vector3.Distance(p.transform.position,positions[i])).DefaultIfEmpty().Max();
                bool held=arrows.Select((p,i)=>p.DisplayedFrame==frames[i]&&p.transform.localScale==scales[i]&&p.transform.rotation==rotations[i]).All(x=>x);
                YvCheck("구매팩 화살 베이크 일시정지","화살3·위치/셀/크기/회전 불변",
                    $"arrows={arrows.Length},positionError={positionError:F6},held={held}",
                    Managers.UI.HUD.BattleHud.IsPaused&&Time.timeScale==0f&&arrows.Length==3&&positionError<.0001f&&held);
            }
            finally{if(Managers.UI.HUD.BattleHud.IsPaused)pause.onClick.Invoke();Time.timeScale=previousScale;}
            PurchasedArcherFx.Impact(source,target);
            yield return WaitFeedbackTime(.70f);
            YvCheck("구매팩 화살 회수·장식 판정 분리","비행체 잔류0·대상 HP 불변",
                $"active={arrows.Count(p=>p!=null&&p.gameObject.activeInHierarchy)},hp={hp}/{target.HpCurr}",
                arrows.All(p=>p==null||!p.gameObject.activeInHierarchy)&&hp==target.HpCurr);
            YvCheck("구매팩 원본 시트 연결","비행/명중 2종 원본 베이크·시전은 명중 핵심 셀 재사용",
                "PurchasedArcher",new[]{"Flight","Hit"}.All(n=>Resources.Load<Texture2D>("VfxTex/PurchasedArcher/"+n)!=null));
            var sheets=new[]{"Flight","Hit"}.Select(n=>Resources.Load<Texture2D>("VfxTex/PurchasedArcher/"+n)).ToArray();
            long sheetBytes=sheets.Where(t=>t!=null).Sum(t=>UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t));
            long gpuBytes=sheets.Where(t=>t!=null).Sum(t=>(long)UnityEngine.Experimental.Rendering.GraphicsFormatUtility.ComputeMipmapSize(t.width,t.height,t.graphicsFormat));
            YvCheck("공용 구매 시트 메모리","GPU 2종 합계32MiB 이하·원소색별 복제 없음",
                $"gpuBytes={gpuBytes},editorReportedBytes={sheetBytes},formats={string.Join(",",sheets.Where(t=>t!=null).Select(t=>t.graphicsFormat.ToString()))}",
                sheets.All(t=>t!=null)&&gpuBytes<=32L*1024*1024);
            yield return CaptureStill("atalante_purchased_pause_resume");
        }

        private IEnumerator AuditPurchasedArcherPursuit(Unit caster)
        {
            Unit target=AliveEnemies().FirstOrDefault(t=>t.HasStatusKey(ActionDot.Key(ActionDotKind.Poison)));
            var hunt=caster.ActivePassiveCodes.OfType<Codes.Passive.AtalantePoisonHunt>().FirstOrDefault();
            var poison=target?.GetAllStatuses().SelectMany(s=>s.Effects).Select(e=>e.EffectObject)
                .OfType<ActionDotEffect>().FirstOrDefault(e=>e.Kind==ActionDotKind.Poison);
            if(target==null||hunt==null||poison==null)
            {YvCheck("아탈란테 추격 준비","실제 맹독과 사냥독","누락",false);yield break;}
            hunt.RefreshPursuits();
            int mana=caster.ManaCurr,arrows=PurchasedArcherFx.EmittedArrows,contacts=PurchasedArcherFx.ResolvedContacts,hits=0;
            void Damage(DamageResolvedContext e)
            {if(e.Attacker==caster&&e.DamageContext.HasTag(DamageTag.AdditionalAttack))hits++;}
            Unit.AnyDamageDealt+=Damage;
            try
            {
                yield return NordAction(caster,ActionScheduler.ActionKind.Additional,
                    _=>ActionDot.ResolveNaturalTick(target,poison),"unit_165_pursuit");
                YvCheck("아탈란테 실제 추격·구매팩 연결","추가행동 피해1·유도 화살1·명중1·예산3·마나 불변",
                    $"hits={hits},arrows={PurchasedArcherFx.EmittedArrows-arrows},contacts={PurchasedArcherFx.ResolvedContacts-contacts},remaining={hunt.Remaining},mana={mana}/{caster.ManaCurr}",
                    hits==1&&PurchasedArcherFx.EmittedArrows-arrows==1&&PurchasedArcherFx.ResolvedContacts-contacts==1&&hunt.Remaining==3&&mana==caster.ManaCurr);
            }
            finally{Unit.AnyDamageDealt-=Damage;}
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
