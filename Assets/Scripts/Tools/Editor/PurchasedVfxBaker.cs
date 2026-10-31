#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tools.Editor
{
    /// <summary>구매한 완성 파티클을 결정적 2D 시트로 렌더한다. 원본 에셋·색·입자 모듈은 수정하지 않는다.</summary>
    public static class PurchasedVfxBaker
    {
        private const string Pack="Assets/Hovl Studio/AAA Projectiles Vol 1/Prefabs/";
        private const int Cell=512,Columns=8,Rows=6;
        [Serializable] private sealed class Record
        {
            public string source,sourceSha256,output;
            public int frames=48,cell=Cell,columns=Columns,rows=Rows,seed=165;
            public float fps=60,field,firstTime,motionSpeed=15,hdrMaximum;
            public string alpha="HDR black render -> (maxRGB/(1+maxRGB))^0.55 coverage + straight RGB; original prefab materials";
            public string cloneSelection;
        }

        [MenuItem("Tools/NeverTheLast/VFX/Bake Purchased Atalante Arrow")]
        public static void BakeAtalanteArrow()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Edit Mode에서 실행한다.");
            string directory="Assets/Resources/VfxTex/PurchasedArcher";
            Directory.CreateDirectory(directory);
            Bake(Pack+"Projectiles 2D/2D Projectile 21 red arrow.prefab",directory+"/Flight.png",4.8f,0f);
            BakeAtalanteImpact96();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ImportAtlases(directory);
            Debug.Log("[구매 VFX 베이크] 비행48·명중96프레임 완료");
        }

        [MenuItem("Tools/NeverTheLast/VFX/Bake Purchased Impact 96 Frames")]
        public static void BakeAtalanteImpact96()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Edit Mode에서 실행한다.");
            const string directory="Assets/Resources/VfxTex/PurchasedArcher";
            // 같은 .8초의 구매 파티클을 120Hz로 실제 재시뮬레이션한다. 복제 프레임이나 느린 정점 홀드가 아니다.
            Bake(Pack+"Hit 4 yellow arrow.prefab",directory+"/Hit.png",3.6f,0f,8,12,384,120f,true,
                "Logs/ProjectileTrioRevision20261024");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ImportAtlases(directory,"Hit.png");
        }

        public static void BakeAtalanteFlight()
        {
            const string directory="Assets/Resources/VfxTex/PurchasedArcher";
            Bake(Pack+"Projectiles 2D/2D Projectile 21 red arrow.prefab",directory+"/Flight.png",4.8f,0f);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ImportAtlases(directory);
        }

        public static void BakeAtalanteCast()
        {
            const string directory="Assets/Resources/VfxTex/PurchasedArcher";
            Bake(Pack+"Flash 4 yellow arrow.prefab",directory+"/Cast.png",2.4f,0f,3);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ImportAtlases(directory,"Cast.png");
        }

        private static void ImportAtlases(string directory,string onlyFile=null)
        {
            foreach(string path in Directory.GetFiles(directory,"*.png"))
            {
                if(onlyFile!=null&&Path.GetFileName(path)!=onlyFile)continue;
                var importer=(TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
                importer.textureType=TextureImporterType.Default;
                importer.alphaSource=TextureImporterAlphaSource.FromInput;
                bool noise=Path.GetFileName(path)=="Dissolve.png";
                importer.alphaIsTransparency=!noise;
                importer.mipmapEnabled=false;
                importer.npotScale=TextureImporterNPOTScale.None;
                importer.sRGBTexture=!noise;
                importer.wrapMode=noise?TextureWrapMode.Repeat:TextureWrapMode.Clamp;
                importer.filterMode=FilterMode.Bilinear;
                importer.maxTextureSize=Path.GetFileName(path)=="Hit.png"?8192:4096;
                importer.textureCompression=TextureImporterCompression.CompressedHQ;
                importer.compressionQuality=100;
                importer.SaveAndReimport();
            }
        }

        private static void Bake(string source,string output,float field,float firstTime,int atlasRows=Rows,
            int atlasColumns=Columns,int cell=Cell,float fps=60f,bool subframe=false,string reportDirectory="Logs/PurchasedArcher20261022")
        {
            var scene=EditorSceneManager.NewPreviewScene();
            RenderTexture rt=null;Texture2D frame=null,atlas=null;GameObject root=null,cameraObject=null;
            var previous=RenderTexture.active;
            try
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(source);
                if(prefab==null)throw new FileNotFoundException(source);
                root=UnityEngine.Object.Instantiate(prefab);
                root.SetActive(false);SceneManager.MoveGameObjectToScene(root,scene);
                // Arrow21의 전격 보조 꼬리는 풀 화살과 혼동되므로 복제본에서만 끈다. 원본 화살 재질은 유지한다.
                if(source.Contains("Projectile 21"))
                    foreach(var child in root.GetComponentsInChildren<Transform>(true))
                        if(child.name=="SubEnergy")child.gameObject.SetActive(false);
                // 시전 전에 독 거품처럼 보이는 Point12 알갱이는 복제본에서만 숨긴다.
                // 구매 원본의 방향성 Flash·Shockwave 재질과 입자 모듈은 그대로 쓴다.
                if(source.EndsWith("Flash 4 yellow arrow.prefab",StringComparison.Ordinal))
                    root.GetComponent<ParticleSystemRenderer>().enabled=false;
                root.transform.position=Vector3.zero;
                root.transform.rotation=Quaternion.Euler(0,90,0);
                // 수명·충돌 스크립트만 격리한다. 파티클 모듈과 재질은 원본 그대로 렌더한다.
                foreach(var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))behaviour.enabled=false;
                foreach(var collider in root.GetComponentsInChildren<Collider>(true))collider.enabled=false;
                foreach(var collider in root.GetComponentsInChildren<Collider2D>(true))collider.enabled=false;
                foreach(var body in root.GetComponentsInChildren<Rigidbody>(true))
                {body.interpolation=RigidbodyInterpolation.None;body.isKinematic=true;body.detectCollisions=false;}
                foreach(var body in root.GetComponentsInChildren<Rigidbody2D>(true))body.simulated=false;
                var systems=root.GetComponentsInChildren<ParticleSystem>(true);
                for(int i=0;i<systems.Length;i++)
                {
                    systems[i].Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                    systems[i].useAutoRandomSeed=false;systems[i].randomSeed=(uint)(165+i*73);
                }
                root.SetActive(true);
                cameraObject=new GameObject("Purchased VFX bake camera");SceneManager.MoveGameObjectToScene(cameraObject,scene);
                var camera=cameraObject.AddComponent<Camera>();camera.scene=scene;
                camera.transform.position=new Vector3(0,0,-20);camera.transform.rotation=Quaternion.identity;
                camera.orthographic=true;camera.orthographicSize=field*.5f;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
                camera.nearClipPlane=.01f;camera.farClipPlane=50;camera.allowHDR=true;camera.allowMSAA=true;
                rt=new RenderTexture(cell,cell,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear){antiAliasing=4};rt.Create();
                camera.targetTexture=rt;
                frame=new Texture2D(cell,cell,TextureFormat.RGBAFloat,false,true);
                atlas=new Texture2D(cell*atlasColumns,cell*atlasRows,TextureFormat.RGBA32,false,false);
                // 원본 이동 프리팹의 월드 공간 꼬리까지 기록한다. 고정된 발사체만 찍으면 머리만 남는다.
                bool moving=source.Contains("Projectile ");
                foreach(var system in systems)system.Simulate(0f,false,true,true);
                int warmup=Mathf.RoundToInt(firstTime*fps);
                for(int step=0;step<warmup;step++)
                {
                    if(moving)root.transform.position+=Vector3.right*(15f/fps);
                    foreach(var system in systems)system.Simulate(1f/fps,false,false,!subframe);
                }
                float hdrMaximum=0f;
                for(int index=0;index<atlasColumns*atlasRows;index++)
                {
                    if(moving)root.transform.position+=Vector3.right*(15f/fps);
                    foreach(var system in systems)system.Simulate(1f/fps,false,false,!subframe);
                    camera.transform.position=root.transform.position+new Vector3(0,0,-20);
                    camera.Render();RenderTexture.active=rt;
                    frame.ReadPixels(new Rect(0,0,cell,cell),0,0);frame.Apply(false);
                    var pixels=frame.GetPixels();
                    for(int p=0;p<pixels.Length;p++)
                    {
                        var c=pixels[p];float maximum=Mathf.Max(c.r,Mathf.Max(c.g,c.b));hdrMaximum=Mathf.Max(hdrMaximum,maximum);
                        // 가산광이 1을 넘는 원본을 8bit에 바로 쓰면 중심 디테일이 한 덩어리로 잘린다.
                        // HDR 명도만 표준 압축하고 색 비율·입자 형상은 보존한다.
                        float coverage=Mathf.Pow(maximum/(1f+maximum),.55f);
                        pixels[p]=maximum>.002f?new Color(c.r/maximum,c.g/maximum,c.b/maximum,coverage):Color.clear;
                    }
                    atlas.SetPixels(index%atlasColumns*cell,(atlasRows-1-index/atlasColumns)*cell,cell,cell,pixels);
                }
                atlas.Apply(false);File.WriteAllBytes(output,atlas.EncodeToPNG());
                string hash;
                using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(source))).Replace("-","").ToLowerInvariant();
                var record=new Record{source=source,output=output,field=field,firstTime=firstTime,sourceSha256=hash,hdrMaximum=hdrMaximum,rows=atlasRows,columns=atlasColumns,cell=cell,fps=fps,frames=atlasColumns*atlasRows,
                    cloneSelection=source.Contains("Projectile 21")?"SubEnergy child disabled":
                        source.EndsWith("Flash 4 yellow arrow.prefab",StringComparison.Ordinal)?"root Point12 renderer disabled; Flash and Shockwave children retained":"all source renderers retained"};
                string logs=reportDirectory;Directory.CreateDirectory(logs);
                File.WriteAllText(Path.Combine(logs,Path.GetFileNameWithoutExtension(output)+"-bake.json"),JsonUtility.ToJson(record,true));
            }
            finally
            {
                RenderTexture.active=previous;
                if(cameraObject!=null)UnityEngine.Object.DestroyImmediate(cameraObject);
                if(root!=null)UnityEngine.Object.DestroyImmediate(root);
                if(frame!=null)UnityEngine.Object.DestroyImmediate(frame);
                if(atlas!=null)UnityEngine.Object.DestroyImmediate(atlas);
                if(rt!=null){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
#endif
