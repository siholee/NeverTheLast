#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Core;
using Unity.Profiling;
using UnityEngine;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        [Serializable]
        private sealed class WholePerformanceReport
        {
            public string scope="Unity Editor Play Mode · 캡처 없는 전투 스트레스 · 실기기/Player FPS 인증 아님";
            public string lineup;
            public float playbackSpeed;
            public int width, height, samplesFocused;
            public string graphicsDevice;
            public bool developmentBuild;
            public bool mainThreadCounterValid,gcCounterValid,drawCounterValid;
            public long memoryWarm,memorySettled;
            public List<float> frameMs=new();
            public List<long> mainThreadNs=new(),gcBytes=new(),drawCalls=new();
        }
        private IEnumerator ScenarioWholePerformance(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();
            yield return PrepareStage(config,Stage(config,7),true,true,true,config.timeScale>0?config.timeScale:1);
            var game=GameManager.Instance;
            int sync=QualitySettings.vSyncCount,limit=Application.targetFrameRate;
            float captureDelta=Time.captureDeltaTime;
            var report=new WholePerformanceReport{
                scope=Application.isEditor ? "Unity Editor stress measurement; not Player FPS" : "Windows Development Player stress measurement; not release Player certification",
                lineup=ResolveLineup(config),playbackSpeed=config.timeScale,width=Screen.width,height=Screen.height,
                graphicsDevice=SystemInfo.graphicsDeviceName,developmentBuild=Debug.isDebugBuild
            };
            try
            {
                // PNG 기록과 프레임 고정을 완전히 끈 별도 측정. 에디터/검증 이벤트 비용은 남는다.
                QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;Time.captureDeltaTime=0;
                yield return WaitUnscaled(2);
                report.memoryWarm=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                using(var main=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",1))
                using(var gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1))
                using(var draw=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count",1))
                {
                    report.mainThreadCounterValid=main.Valid;report.gcCounterValid=gc.Valid;report.drawCounterValid=draw.Valid;
                    float until=Time.realtimeSinceStartup+CaptureSeconds(config,12);
                    while(Time.realtimeSinceStartup<until&&game.gameState==BaseClasses.BaseEnums.GameState.RoundInProgress)
                    {
                        yield return null;
                        report.frameMs.Add(Time.unscaledDeltaTime*1000);
                        if (Application.isFocused) report.samplesFocused++;
                        report.mainThreadNs.Add(main.Valid?main.LastValue:0);
                        report.gcBytes.Add(gc.Valid?gc.LastValue:0);
                        report.drawCalls.Add(draw.Valid?draw.LastValue:0);
                    }
                }
                game.DebugResetBattle();
                yield return WaitUnscaled(2);
                report.memorySettled=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                string directory=Path.Combine(_requestDirectory,"performance");Directory.CreateDirectory(directory);
                string path=Path.Combine(directory,SafeName(report.lineup+"_"+report.playbackSpeed)+".json");
                File.WriteAllText(path,JsonUtility.ToJson(report,true));
                YvCheck("실측 성능 표본","120프레임 이상",report.frameMs.Count.ToString(),report.frameMs.Count>=120);
                YvCheck("실제 GC 계측기","사용 가능",report.gcCounterValid.ToString(),report.gcCounterValid);
                yield return CaptureStill("whole_performance_settled");
            }
            finally{QualitySettings.vSyncCount=sync;Application.targetFrameRate=limit;Time.captureDeltaTime=captureDelta;}
            if(_yvFailures.Count>0)throw new InvalidOperationException(string.Join(" / ",_yvFailures));
        }
    }
}
#endif
