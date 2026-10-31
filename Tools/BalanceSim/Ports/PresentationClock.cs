using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using YamlDotNet.Serialization;

namespace Effects.Vfx
{
    /// <summary>픽셀을 만들지 않고 YAML의 판정 정점만 가상 코루틴 시간으로 진행한다.</summary>
    internal static class PresentationClock
    {
        private static string _directory;
        private static VfxDataList _data;
        private static VfxDataList Data
        {
            get
            {
                string directory=Managers.DataManager.DirectoryPath;
                if(_directory!=directory||_data==null)
                {
                    _data=new DeserializerBuilder().IgnoreUnmatchedProperties().Build().Deserialize<VfxDataList>(File.ReadAllText(Path.Combine(directory,"96_vfx.yaml")));
                    _directory=directory;
                }
                return _data;
            }
        }
        public static IEnumerator Hit(string key,Action impact,bool offset=false,float minimumDuration=0)
        {
            var hits=Data.skills.FirstOrDefault(s=>s.key==key)?.slots.Where(s=>s.slot=="hit").ToArray();
            var slot=hits?.FirstOrDefault(s=>s.triggersImpact)??hits?.FirstOrDefault();
            var part=Data.parts.FirstOrDefault(p=>p.id==slot?.partId);
            float peak=0,end=minimumDuration,lead=0;
            if(slot!=null&&part!=null)
            {
                float fps=Math.Max(1,part.fps);
                int frame=slot.peakFrame>=0?slot.peakFrame:part.peakFrame>=0?part.peakFrame:Math.Max(0,part.columns*part.rows/2);
                peak=slot.impactDelay>=0?slot.impactDelay:frame/fps;
                if(string.IsNullOrWhiteSpace(part.sheet)) peak=0;
                if(part.renderer=="clawTriple")
                {
                    float draw=Math.Max(.10f,part.drawDuration);
                    peak=draw*Math.Clamp(part.centerCrossFraction,.2f,.8f)+Math.Max(0,part.lineDelay);
                    end=Math.Max(end,draw+2*Math.Max(0,part.lineDelay)+Math.Max(0,part.lingerDuration)+Math.Max(.06f,part.fadeDuration));
                }
                if(offset)lead=Math.Max(0,slot.timeOffset);
                end=Math.Max(end,Math.Max(peak,part.columns*part.rows/fps+part.peakHoldSeconds+part.tailHoldSeconds));
                // 단일 조합은 판정 슬롯뿐 아니라 늦게 시작하는 연기·잔상 슬롯까지 기다린다.
                // 이 대기를 빼면 마지막 선제 반격의 완료 카운트가 사망 판정보다 먼저 올라간다.
                if(offset)
                    foreach(var hit in hits)
                    {
                        var visual=Data.parts.FirstOrDefault(p=>p.id==hit.partId);
                        if(visual!=null)end=Math.Max(end,Math.Max(0,hit.timeOffset)+visual.columns*visual.rows/Math.Max(1,visual.fps)+visual.peakHoldSeconds+visual.tailHoldSeconds);
                    }
                if(offset){float guard=Math.Max(3,minimumDuration+1);peak=Math.Min(peak,Math.Max(0,guard-lead));end=Math.Min(end,guard);}
            }
            // Unity는 timeOffset 대기 뒤 별도 코루틴으로 정점을 진행한다.
            // 합산 대기는 두 구간의 프레임 반올림을 없애 동시 타격 순서를 바꾼다.
            if(lead>0)yield return Combat.CombatPresentationWait.Delay(lead);
            if(peak>0)yield return new WaitForSeconds(peak);
            impact?.Invoke();
            if(end>lead+peak)yield return new WaitForSeconds(end-lead-peak);
        }
    }
}
