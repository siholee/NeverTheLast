#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Entities;
using UnityEngine;
using Effects.Vfx;
namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        // 해당 조합 직전에 존재했던 렌더러는 제외한다. 이전 잔광·상시 오라는 통과 근거가 아니다.
        // 같은 시각 두 렌더에서 수신자의 원화 영역만 비교하는 빈 효과 방지 검사다(미술 합격 아님).
        private float VfxPixelCoverage(ISet<int> previousRenderers, IReadOnlyList<Unit> receivers, bool support=false, bool casterSignal=false)
        {
            Camera camera=Camera.main;
            if(camera==null)return 0;
            Renderer[] effects=FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(r=>r.enabled&&r.sortingLayerID==VfxSpace.SortingLayer(null)&&!previousRenderers.Contains(r.GetInstanceID())).ToArray();
            if(effects.Length==0)return 0;
            Rect[] regions=receivers.Where(u=>u!=null).Select(u=>CardMotion.ViewOf(u))
                .Where(view=>view!=null).Select(view=>
                {
                    Rect rect=view.PortraitFocusScreenRect(camera);
                    // 지원은 얼굴을 덮지 않고 머리 위에서 모인다. 그 빈 공간까지 수신자 영역으로 센다.
                    if(support)rect.yMax+=rect.height*.45f;
                    // 시전은 얼굴 가림을 피하기 위해 손 옆 공간에도 놓인다.
                    // 피격 영역은 그대로 유지하며 시전자만 양옆의 가까운 공간을 포함한다.
                    if(casterSignal)
                    {
                        Rect hud=view.HudScreenRect(camera);
                        rect.yMin=Mathf.Max(rect.yMin,hud.yMax);
                        rect.yMax=Mathf.Lerp(rect.yMin,rect.yMax,.50f);
                        float margin=rect.width*.15f;rect.xMin-=margin;rect.xMax+=margin;
                    }
                    return rect;
                }).ToArray();
            if(regions.Length==0)return 0;
            var previousTarget=camera.targetTexture;var previousActive=RenderTexture.active;
            var output=RenderTexture.GetTemporary(640,360,24,RenderTextureFormat.ARGB32);
            var read=new Texture2D(640,360,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=output;camera.Render();RenderTexture.active=output;
                read.ReadPixels(new Rect(0,0,640,360),0,0);read.Apply();Color32[] present=read.GetPixels32();
                foreach(Renderer r in effects)if(r!=null)r.enabled=false;
                camera.Render();RenderTexture.active=output;
                read.ReadPixels(new Rect(0,0,640,360),0,0);read.Apply();Color32[] absent=read.GetPixels32();
                int changed=0, tested=0;
                for(int i=0;i<present.Length;i++)
                {
                    Vector2 point=new((i%640+.5f)*Screen.width/640f,(i/640+.5f)*Screen.height/360f);
                    if(!regions.Any(region=>region.Contains(point)))continue;
                    tested++;
                    if(Math.Max(Math.Abs(present[i].r-absent[i].r),Math.Max(Math.Abs(present[i].g-absent[i].g),Math.Abs(present[i].b-absent[i].b)))>=18)changed++;
                }
                return tested>0?changed/(float)tested:0f;
            }
            finally
            {
                foreach(Renderer r in effects)if(r!=null)r.enabled=true;
                camera.targetTexture=previousTarget;RenderTexture.active=previousActive;
                RenderTexture.ReleaseTemporary(output);Destroy(read);
            }
        }
        private IEnumerator VerifiedVfxSamples(string tag,ISet<int> previousRenderers,IReadOnlyList<Unit> receivers,bool support,Action<float> coverage,bool casterSignal=false)
        {
            float last=0f;
            foreach(float at in new[]{.03f,.06f,.10f,.18f,.30f,.45f,.70f,1.05f})
            {
                yield return new WaitForSeconds(at-last);last=at;
                yield return EndOfFrame;
                coverage(VfxPixelCoverage(previousRenderers,receivers,support,casterSignal));Capture(tag+"_visible_"+at.ToString("0.00"));
            }
        }
    }
}
#endif
