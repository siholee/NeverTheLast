using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NeverTheLast.Simulation;

internal static class ParityComparison
{
    internal static int Run(string unityPath,string consolePath,string directory,string unityTraces,string consoleTraces)
    {
        Program.EnsureNewDirectory(directory);
        var unity=Read(unityPath);var console=Read(consolePath);
        if(unity.Length==0||console.Length==0)throw new InvalidDataException("비교할 전투 결과가 비어 있습니다.");
        var comparisons=new List<object>();int failures=0;
        for(int index=0;index<Math.Max(unity.Length,console.Length);index++)
        {
            BattleResult a=index<unity.Length?unity[index]:null,b=index<console.Length?console[index]:null;
            var differences=new List<string>();
            if(a==null||b==null)differences.Add("전투 개수 불일치");
            else
            {
                if(a.Outcome=="error"||b.Outcome=="error")differences.Add("오류 전투는 동등성 합격으로 처리하지 않습니다.");
                if(a.Team!=b.Team||a.Encounter!=b.Encounter||a.Seed!=b.Seed)differences.Add("실험 순서/조건 키 불일치");
                if(a.MaxActions!=b.MaxActions||a.MaxCycles!=b.MaxCycles)differences.Add("행동/AV 실험 상한 불일치");
                if(JsonSerializer.Serialize(a.TeamDefinition,Program.Line)!=JsonSerializer.Serialize(b.TeamDefinition,Program.Line)||JsonSerializer.Serialize(a.EncounterDefinition,Program.Line)!=JsonSerializer.Serialize(b.EncounterDefinition,Program.Line))differences.Add("입력 정의 불일치");
                if(a.Outcome!=b.Outcome||a.Reason!=b.Reason)differences.Add($"결과 {a.Outcome}/{a.Reason} != {b.Outcome}/{b.Reason}");
                if(a.Actions!=b.Actions||a.Turns!=b.Turns)differences.Add($"행동/턴 {a.Actions}/{a.Turns} != {b.Actions}/{b.Turns}");
                // Mono와 .NET JIT의 float 중간 정밀도 차이만 허용한다. 정수 피해/HP와 사건 순서는 정확 비교다.
                if(Math.Abs((long)BitConverter.SingleToInt32Bits(a.ActionValue)-BitConverter.SingleToInt32Bits(b.ActionValue))>4)differences.Add($"AV {a.ActionValue:R} != {b.ActionValue:R}");
                if(a.EventDigest!=b.EventDigest)differences.Add("행동/피해 사건 해시 불일치");
                string Stats(BattleResult r)=>JsonSerializer.Serialize(r.Units.OrderBy(u=>u.Enemy).ThenBy(u=>u.Column).ThenBy(u=>u.Row).ThenBy(u=>u.Id),Program.Line);
                if(Stats(a)!=Stats(b))differences.Add("유닛별 HP/피해/회복/보호막/턴 불일치");
            }
            object firstEvent=null;
            if(differences.Count>0)
            {
                failures++;
                string ua=unityTraces==null?null:Path.Combine(unityTraces,index.ToString("D5")+".trace.txt");
                string cb=consoleTraces==null?null:Path.Combine(consoleTraces,index.ToString("D5")+".txt");
                if(File.Exists(ua)&&File.Exists(cb))
                {
                    var left=File.ReadAllLines(ua);var right=File.ReadAllLines(cb);
                    int mismatch=Enumerable.Range(0,Math.Max(left.Length,right.Length)).FirstOrDefault(i=>i>=left.Length||i>=right.Length||left[i]!=right[i],-1);
                    if(mismatch>=0)firstEvent=new {index=mismatch,unity=left.Skip(Math.Max(0,mismatch-1)).Take(4),console=right.Skip(Math.Max(0,mismatch-1)).Take(4)};
                }
            }
            comparisons.Add(new {index,team=a?.Team??b?.Team,encounter=a?.Encounter??b?.Encounter,seed=a?.Seed??b?.Seed,passed=differences.Count==0,differences,firstEvent});
        }
        File.WriteAllText(Path.Combine(directory,"comparison.json"),JsonSerializer.Serialize(new {total=comparisons.Count,passed=comparisons.Count-failures,failed=failures,comparisons},Program.Json));
        File.Copy(unityPath,Path.Combine(directory,"unity-battles.jsonl"));File.Copy(consolePath,Path.Combine(directory,"console-battles.jsonl"));
        Console.WriteLine($"동등성 {comparisons.Count-failures}/{comparisons.Count}: {Path.GetFullPath(directory)}");return failures==0?0:2;
    }
    private static BattleResult[] Read(string path)=>File.ReadLines(path).Where(s=>!string.IsNullOrWhiteSpace(s)).Select(s=>JsonSerializer.Deserialize<BattleResult>(s,Program.Json)??throw new InvalidDataException(path)).ToArray();
}
