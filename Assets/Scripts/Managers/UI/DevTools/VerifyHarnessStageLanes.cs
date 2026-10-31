#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Core;
using Entities.View;
using Managers.Stage;
using UnityEngine;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        // S1과 같은 20입력. index.html v1.2의 ROW·rowDist·place(rows)·slotPos를 JS로 실행한 독립 기대값이다.
        private IEnumerator ScenarioS7Layout()
        {
            double[,] reference =
            {
                {-1.000000000,0.000000000,0.000000000,1.000000000,175.750000000,784.250000000,715.000000000,0.500000000,0.950000000},
                {1.000000000,1.000000000,0.000000000,1.000000000,499.500000000,1459.500000000,715.000000000,0.500000000,0.950000000},
                {-1.000000000,0.000000000,0.000000000,2.000000000,152.500000000,807.500000000,622.000000000,0.200000000,0.884000000},
                {-1.000000000,0.000000000,1.000000000,2.000000000,199.000000000,761.000000000,808.000000000,0.800000000,1.016000000},
                {1.000000000,1.000000000,0.000000000,2.000000000,450.000000000,1410.000000000,622.000000000,0.200000000,0.884000000},
                {1.000000000,1.000000000,1.000000000,2.000000000,549.000000000,1509.000000000,808.000000000,0.800000000,1.016000000},
                {-1.000000000,1.000000000,0.000000000,3.000000000,417.000000000,543.000000000,560.000000000,0.000000000,0.840000000},
                {-1.000000000,1.000000000,1.000000000,3.000000000,499.500000000,460.500000000,715.000000000,0.500000000,0.950000000},
                {-1.000000000,1.000000000,2.000000000,3.000000000,582.000000000,378.000000000,870.000000000,1.000000000,1.060000000},
                {1.000000000,0.000000000,0.000000000,4.000000000,137.000000000,1097.000000000,560.000000000,0.000000000,0.840000000},
                {1.000000000,0.000000000,1.000000000,4.000000000,162.833333333,1122.833333333,663.333333333,0.333333333,0.913333333},
                {1.000000000,0.000000000,2.000000000,4.000000000,188.666666667,1148.666666667,766.666666667,0.666666667,0.986666667},
                {1.000000000,0.000000000,3.000000000,4.000000000,214.500000000,1174.500000000,870.000000000,1.000000000,1.060000000},
                {-1.000000000,0.000000000,0.000000000,0.000000000,137.000000000,823.000000000,560.000000000,0.000000000,0.840000000},
                {-1.000000000,0.000000000,1.000000000,0.000000000,162.833333333,797.166666667,663.333333333,0.333333333,0.913333333},
                {-1.000000000,0.000000000,2.000000000,0.000000000,188.666666667,771.333333333,766.666666667,0.666666667,0.986666667},
                {-1.000000000,0.000000000,3.000000000,0.000000000,214.500000000,745.500000000,870.000000000,1.000000000,1.060000000},
                {1.000000000,1.000000000,0.000000000,0.000000000,417.000000000,1377.000000000,560.000000000,0.000000000,0.840000000},
                {1.000000000,1.000000000,2.000000000,0.000000000,527.000000000,1487.000000000,766.666666667,0.666666667,0.986666667},
                {1.000000000,1.000000000,3.000000000,0.000000000,582.000000000,1542.000000000,870.000000000,1.000000000,1.060000000}
            };
            for (int i = 0; i < reference.GetLength(0); i++)
            {
                int side=(int)reference[i,0], column=(int)reference[i,1], index=(int)reference[i,2], count=(int)reference[i,3];
                StageLayout.Slot slot = count == 0 ? StageLayout.ForPreparation(side,column,index)
                    : StageLayout.ForBattle(side,column,index,count);
                float distance=StageLayout.ColumnDistance(column,slot.Depth01,index);
                double error=Math.Max(Math.Abs(distance-reference[i,4]),
                    Math.Max(Math.Abs(slot.Pixel.x-reference[i,5]),Math.Abs(slot.Pixel.y-reference[i,6])));
                RecordMechanic("s7_reference_"+i,"시안 d/x/y ±0.5px·t/배율 ±0.000001",
                    FormattableString.Invariant($"input={side},{column},{index},{count}; d={distance:F9}; x={slot.Pixel.x:F9}; y={slot.Pixel.y:F9}; t={slot.Depth01:F9}; scale={slot.Scale:F9}; error={error:F9}"),
                    error<=.5 && Math.Abs(slot.Depth01-reference[i,7])<=.000001 && Math.Abs(slot.Scale-reference[i,8])<=.000001);
            }
            yield return CaptureStill("s7_layout");
        }

        // S6의 사건 비교는 그대로 둔다. 가림은 이전 수동 시전의 정렬 부스트가 없는 새 5:5에서 잰다.
        private IEnumerator ScenarioS7Battle(VerifyScenarioRequest config)
        {
            if (VisualModeState.Current != VisualMode.Stage) { FailCurrent("S7 무대 렌더 전용 검사다."); yield break; }
            yield return PrepareStage(config,6,false,false,false);
            ConfigureStageHudEnemies(config);
            var game=GameManager.Instance; bool prior=game.enabled; game.enabled=false;
            try
            {
                game.StartRound();
                yield return WaitForState(BaseEnums.GameState.RoundInProgress,5f);
                RecordMechanic("s7_five_lineup","실제 RoundInProgress·새 5:5 기본 자세",
                    string.Join("/",AliveAllies().Concat(AliveEnemies()).Select(u=>$"{u.ID}:{u.UnitName}({u.currentCell.xPos},{u.currentCell.yPos})")),
                    game.gameState==BaseEnums.GameState.RoundInProgress && AliveAllies().Count==5 && AliveEnemies().Count==5);
                GridManager.Instance.RefreshFieldLayout();
                game.uiManager?.UpdateGameStatusWithEnemyCount(BaseEnums.GameState.RoundInProgress,0,AliveEnemies().Count);
                yield return WaitUnscaled(.5f);
                RecordStageLayoutChecks("s7_five_party");
                RecordS7StagePixels("battle");
                yield return null;
                yield return CaptureStill("s7_battle_5v5");
            }
            finally { game.ActionScheduler.EndRound(); game.enabled=prior; }
        }

        private sealed class S7Silhouette
        {
            public UnitCardView Owner;
            public SpriteRenderer Renderer;
            public bool[] Pixels;
            public int Count;
        }

        // 셰이더·SpriteRenderer 색/뒤집기/마스크/실제 화면 투영을 그대로 쓴다.
        // Blend SrcAlpha의 RT 알파는 a²이므로 알파 채널을 읽지 않는다.
        // 선형 RT에서 흑/백 배경의 RGB 차 = 1-a. 각 렌더러 a>0.35의 합집합을 센다.
        // 원화 텍스처의 CPU 추정이나 bounds 면적을 가림 픽셀로 취급하지 않는다.
        private void RecordS7StagePixels(string tag)
        {
            if (VisualModeState.Current != VisualMode.Stage) return;
            RecordS1RowBands(tag);
            Camera camera=Camera.main;
            var views=AliveAllies().Concat(AliveEnemies()).Select(u=>u.currentCell?.Card)
                .Where(v=>v?.StageProxy!=null).ToArray();
            if (camera==null || views.Length==0) { FailCurrent("S7 렌더 측정 대상이 없다."); return; }
            Canvas.ForceUpdateCanvases();
            int width=Screen.width, height=Screen.height;
            var renderers=FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            var canvases=FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            bool[] enabled=renderers.Select(r=>r.enabled).ToArray(), canvasEnabled=canvases.Select(c=>c.enabled).ToArray();
            var target=camera.targetTexture; var active=RenderTexture.active;
            CameraClearFlags clear=camera.clearFlags; Color background=camera.backgroundColor;
            var output=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            var read=new Texture2D(width,height,TextureFormat.RGBA32,false,true);
            var masks=new List<S7Silhouette>();
            try
            {
                foreach (var r in renderers) r.enabled=false;
                foreach (var c in canvases) c.enabled=false;
                camera.targetTexture=output; camera.clearFlags=CameraClearFlags.SolidColor;
                Color32[] Render(Color color)
                {
                    camera.backgroundColor=color; camera.Render(); RenderTexture.active=output;
                    read.ReadPixels(new Rect(0,0,width,height),0,0); return read.GetPixels32();
                }
                foreach (var view in views)
                    foreach (var renderer in new[]{view.StageProxy.Lower,view.StageProxy.Head})
                    {
                        int ri=Array.IndexOf(renderers,renderer);
                        if (renderer==null || ri<0 || !enabled[ri] || !renderer.gameObject.activeInHierarchy) continue;
                        renderer.enabled=true;
                        Color32[] black=Render(Color.black), white=Render(Color.white);
                        renderer.enabled=false;
                        bool[] pixels=new bool[width*height]; int count=0;
                        for (int p=0;p<pixels.Length;p++)
                        {
                            float alpha=1f-((white[p].r-black[p].r)+(white[p].g-black[p].g)+(white[p].b-black[p].b))/(3f*255f);
                            if (alpha>.35f) { pixels[p]=true; count++; }
                        }
                        masks.Add(new S7Silhouette{Owner=view,Renderer=renderer,Pixels=pixels,Count=count});
                        RecordMechanic("s7_mask_"+tag+"_"+view.BoundUnit.ID+"_"+renderer.name,
                            "실제 렌더 alpha>0.35 픽셀 존재",count.ToString(),count>0);
                    }
            }
            finally
            {
                for (int i=0;i<renderers.Length;i++) if(renderers[i]!=null) renderers[i].enabled=enabled[i];
                for (int i=0;i<canvases.Length;i++) if(canvases[i]!=null) canvases[i].enabled=canvasEnabled[i];
                camera.targetTexture=target; camera.clearFlags=clear; camera.backgroundColor=background;
                RenderTexture.active=active; RenderTexture.ReleaseTemporary(output); Destroy(read);
            }
            int CountPixels(Rect rect, Func<S7Silhouette,bool> select)
            {
                var chosen=masks.Where(select).ToArray();
                int total=0;
                for (int y=Mathf.Max(0,Mathf.CeilToInt(rect.yMin-.5f));y<Mathf.Min(height,Mathf.CeilToInt(rect.yMax-.5f));y++)
                    for (int x=Mathf.Max(0,Mathf.CeilToInt(rect.xMin-.5f));x<Mathf.Min(width,Mathf.CeilToInt(rect.xMax-.5f));x++)
                        for (int i=0;i<chosen.Length;i++)
                            if(chosen[i].Pixels[y*width+x]) { total++; break; }
                return total;
            }
            foreach (var view in views)
            {
                Rect rect=view.StageHudHitRect(camera);
                Canvas hud=view.GetComponentsInChildren<Canvas>(true).First(c=>c.name=="Hud");
                int layer=SortingLayer.GetLayerValueFromID(hud.sortingLayerID);
                bool Front(S7Silhouette m) => m.Owner!=view &&
                    (SortingLayer.GetLayerValueFromID(m.Renderer.sortingLayerID)>layer ||
                    (m.Renderer.sortingLayerID==hud.sortingLayerID && m.Renderer.sortingOrder>hud.sortingOrder));
                int area=0;
                for(int y=Mathf.Max(0,Mathf.CeilToInt(rect.yMin-.5f));y<Mathf.Min(height,Mathf.CeilToInt(rect.yMax-.5f));y++)
                    for(int x=Mathf.Max(0,Mathf.CeilToInt(rect.xMin-.5f));x<Mathf.Min(width,Mathf.CeilToInt(rect.xMax-.5f));x++) area++;
                int covered=CountPixels(rect,Front);
                float ratio=area>0?covered/(float)area:0f;
                string sources=string.Join(",",masks.Where(Front).Where(m=>CountPixels(rect,n=>n==m)>0)
                    .Select(m=>m.Owner.BoundUnit.ID+":"+m.Renderer.name));
                RecordMechanic("s7_hud_"+tag+"_"+view.BoundUnit.ID,
                    "P2 S2 하단 HUD의 전장 실루엣 가림0(준비는 숨김)",
                    FormattableString.Invariant($"unit={view.BoundUnit.UnitName}; resolution={width}x{height}; rect={rect}; layer={layer}; order={hud.sortingOrder}; covered={covered}; area={area}; ratio={ratio:F8}; sources={sources}"),
                    covered==0 && (tag=="preparation" ? area==0 : area>0));
            }
            var ui=FindObjectsByType<RectTransform>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            foreach(string name in new[]{"ActionQueue","DamageMeter","PreparationPanel"})
            {
                var panel=ui.FirstOrDefault(r=>r.name==name);
                if (panel==null) { RecordMechanic("s7_boundary_"+tag+"_"+name,"패널 사각형 존재","없음",false); continue; }
                Canvas canvas=panel.GetComponentInParent<Canvas>();
                Camera uiCamera=canvas!=null && canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
                var corners=new Vector3[4];panel.GetWorldCorners(corners);
                var points=corners.Select(p=>RectTransformUtility.WorldToScreenPoint(uiCamera,p)).ToArray();
                Rect rect=Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
                bool visible=panel.gameObject.activeInHierarchy &&
                    panel.GetComponentsInParent<CanvasGroup>(true).All(g=>g.alpha>.01f);
                int overlap=CountPixels(rect,m=>true);
                string units=string.Join(",",views.Where(v=>CountPixels(rect,m=>m.Owner==v)>0).Select(v=>v.BoundUnit.ID));
                RecordMechanic("s7_boundary_"+tag+"_"+name,"표시 중 패널과 유닛 alpha>0.35 영역 중첩0",
                    $"resolution={width}x{height}; rect={rect}; visible={visible}; overlapPixels={overlap}; units={units}",
                    !visible || overlap==0);
            }
            RecordMechanic("s7_render_environment_"+tag,"실제 에디터 Play·선형 RT·화면 크기 동일",
                $"dataPath={Application.dataPath}; resolution={width}x{height}; masks={masks.Count}; alphaThreshold=0.35; frame={Time.frameCount}",
                !Application.isBatchMode && masks.Count>=views.Length);
        }

        private void RecordS1RowBands(string tag)
        {
            Camera camera = Camera.main;
            var rows = camera.GetComponent<StageRowBands>();
            if (rows?.Bands == null) { FailCurrent("진형 바닥 띠가 없다."); return; }
            bool prep = tag == "preparation";
            var mesh = rows.Bands.GetComponent<MeshFilter>().sharedMesh;
            var vertices = mesh.vertices;
            var colors = mesh.colors;
            // index.html rowBandsHTML을 JS로 실행해 얻은 독립 16점. 띠 외곽도 ±0.5px로 비교한다.
            Vector2[] reference =
            {
                new(855.984845f,532.246211f), new(790.015155f,515.753789f),
                new(712.515155f,891.753789f), new(778.484845f,908.246211f),
                new(573.013390f,539.974869f), new(512.986610f,508.025131f),
                new(347.986610f,884.025131f), new(408.013390f,915.974869f),
                new(1129.984845f,515.753789f), new(1064.015155f,532.246211f),
                new(1141.515155f,908.246211f), new(1207.484845f,891.753789f),
                new(1407.013390f,508.025131f), new(1346.986610f,539.974869f),
                new(1511.986610f,915.974869f), new(1572.013390f,884.025131f)
            };
            int count = 0; float maxError = 0f;
            for (int i = 0; i < vertices.Length; i++)
            {
                if (Mathf.Abs(colors[i].a - .1f) > .001f) continue;
                Vector3 screen = camera.WorldToScreenPoint(rows.Bands.transform.TransformPoint(vertices[i]));
                Vector2 actual = new((screen.x-camera.pixelRect.center.x)*1080f/camera.pixelHeight+960f,
                    (camera.pixelRect.yMax-screen.y)*1080f/camera.pixelHeight);
                if (count < reference.Length) maxError = Mathf.Max(maxError, Vector2.Distance(actual, reference[count]));
                count++;
            }
            RecordMechanic("s1_bands_geometry_"+tag,"아군·적 전후열 4띠, 시안 외곽16점 ±0.5px",
                $"vertices={count}; maxErrorPx={maxError:F6}",count==16 && maxError<=.5f);
            float alpha = rows.Bands.sharedMaterial.color.a;
            RecordMechanic("s1_bands_style_"+tag,"띠 전투10%/준비20%·점선35%/70%·이름 준비만·색보정 뒤/유닛 앞 바닥",
                $"bandAlpha={.1f*alpha:F3}; lineAlpha={.35f*alpha:F3}; labels={rows.Labels.Count(l=>l.gameObject.activeInHierarchy)}; order={rows.Bands.sortingOrder}",
                rows.Bands.enabled && rows.Bands.gameObject.activeInHierarchy && Mathf.Abs(alpha-(prep?2f:1f))<.001f &&
                rows.Labels.Count==4 && rows.Labels.All(l=>l.gameObject.activeInHierarchy==prep) &&
                rows.Bands.sortingOrder==-898 && colors.Any(c=>Mathf.Abs(c.a-.35f)<.001f));
        }
    }
}
#endif

