#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using BaseClasses;
using Codes.Passive;
using Combat;
using Core;
using Entities;
using UnityEngine;
namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioVedicTrioNatural(VerifyScenarioRequest config)
        {
            bool enabled=true;
            yield return YvPrepareScene(config,x=>enabled=x);
            bool rewards=config.name.Contains("reward");
            Unit vayu=AliveAllies().First(x=>x.ID==64),agni=AliveAllies().First(x=>x.ID==62),surya=AliveAllies().First(x=>x.ID==67);
            foreach(Unit unit in AliveAllies())YvBoostEnemy(unit); // 생존만 고정한 회전 측정, 승률 검사가 아니다.
            foreach(Unit unit in new[]{vayu,agni,surya})
            {
                foreach(var code in unit.ActivePassiveCodes.Where(x=>x.CatalogId>=500).ToList())unit.RemoveRewardCode(code.CatalogId);
            }
            if(rewards)
            {
                foreach(int id in new[]{724,803,804,723})agni.TryGrantRewardCode(id);
                foreach(int id in new[]{724,805})surya.TryGrantRewardCode(id);
                vayu.TryGrantRewardCode(712);
            }
            foreach(Unit unit in AliveAllies())
            {
                unit.ClearUltimateResource();
                foreach(var code in unit.ActivePassiveCodes.ToList()){code.StopCode();code.CastCode();}
            }
            using var recorder=new YvRecorder();
            ActionScheduler scheduler=GameManager.Instance.ActionScheduler;
            scheduler.BeginRound();
            int maxQueued=0;
            float deadline=Time.realtimeSinceStartup+90f;
            while(recorder.Finished.Count<100 && AliveEnemies().Count>0 && Time.realtimeSinceStartup<deadline)
            {
                scheduler.Tick(Time.deltaTime);
                maxQueued=Mathf.Max(maxQueued,scheduler.DebugQueuedCount(ActionScheduler.ActionKind.Additional));
                yield return null;
            }
            RecordMechanic("자연 단일라운드 유한100행동 Lv"+agni.Level+" "+rewards,"100완료 또는 적 전멸",recorder.Finished.Count+"/queuePeak="+maxQueued,recorder.Finished.Count>=100 || AliveEnemies().Count==0);
            foreach(Unit unit in new[]{vayu,agni,surya})
            {
                int normal=recorder.Finished.Count(x=>x.Unit==unit&&x.Kind==ActionScheduler.ActionKind.Normal);
                int ultimate=recorder.Finished.Count(x=>x.Unit==unit&&x.Kind==ActionScheduler.ActionKind.Ultimate);
                int additional=recorder.Finished.Count(x=>x.Unit==unit&&ActionScheduler.IsAdditional(x.Kind));
                var dealt=recorder.Dealt.Where(x=>x.Attacker==unit).ToList();
                RecordMechanic("자연 회전 측정 "+unit.ID+" Lv"+unit.Level+" reward="+rewards,"실제행동/실피해 기록","N="+normal+" U="+ultimate+" P="+additional+" total="+dealt.Sum(x=>(long)x.DamageDealt)+" hits="+dealt.Count,true);
                foreach(var group in dealt.GroupBy(x=>x.DamageContext.CodeType))
                    RecordMechanic("자연 피해형태 "+unit.ID+" "+group.Key,"실피해 기록",group.Sum(x=>(long)x.DamageDealt).ToString(),true);
            }
            scheduler.EndRound();
            RecordMechanic("자연 측정 종료 예약 정리","0",scheduler.DebugQueuedCount(ActionScheduler.ActionKind.Additional).ToString(),scheduler.DebugQueuedCount(ActionScheduler.ActionKind.Additional)==0);
            foreach(Unit unit in AliveAllies().Concat(AliveEnemies())){unit.StopAllCoroutines();unit.isCasting=false;}
            yield return EndOfFrame;
            Capture("trio_natural_L"+agni.Level+"_"+rewards);
            GameManager.Instance.gameState=BaseEnums.GameState.Preparation;
            GameManager.Instance.enabled=enabled;
        }
    }
}
#endif

