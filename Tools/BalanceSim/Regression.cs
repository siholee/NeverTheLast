using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Combat;
using NeverTheLast.Simulation;

internal static class Regression
{
    internal static int Run(Simulator simulator,string directory)
    {
        Program.EnsureNewDirectory(directory);
        var checks=new List<object>();var battles=new List<BattleResult>();int failed=0;
        void Check(string name,bool pass,string detail=""){checks.Add(new{name,pass,detail});if(!pass)failed++;}
        var experiment=Program.Sample();simulator.Validate(experiment);
        Check("Unity 바이너리 참조 없음",typeof(Entities.Unit).Assembly.GetReferencedAssemblies().All(a=>!a.Name.StartsWith("Unity",StringComparison.OrdinalIgnoreCase)));
        // 서로 독립적으로 올린 공격/특공이 실제 맹화 입력 피해에 반영되는지 확인한다.
        {
            var game=new Managers.GameManager {dataManager=simulator.Data,unitDataList=simulator.Data.FetchUnitDataList()};
            var grid=new Managers.GridManager();Managers.GameManager.Instance=game;Managers.GridManager.Instance=grid;
            var himiko=grid.SpawnUnit(-2,2,false,44);var victim=grid.SpawnUnit(1,2,true,1001);
            victim.AddShield(1000000);
            var blaze=new Codes.Passive.HimikoBlaze(new BaseClasses.PassiveCodeContext {Caster=himiko});
            var inputs=new List<int>();
            void Capture(BaseClasses.DamageResolvedContext c){if(c.Attacker==himiko&&c.DamageContext.CodeType==BaseClasses.BaseEnums.CodeType.Passive)inputs.Add(c.DamageContext.Damage);}
            Entities.Unit.AnyDamageDealt+=Capture;
            try
            {
                void Detonate(){blaze.ApplyBlaze(victim);blaze.ApplyBlaze(victim);blaze.ApplyBlaze(victim);}
                int initial=himiko.SpAtkCurr;Detonate();
                var state=new Core.UnitSaveData();himiko.WriteRunState(state);
                state.nectarStats.Add(new Core.PermanentStatSaveData {stat="Attack",amount=500});himiko.RestoreRunState(state);Detonate();
                state.nectarStats.Add(new Core.PermanentStatSaveData {stat="SpecialAttack",amount=200});himiko.RestoreRunState(state);Detonate();
                Check("히미코 맹화: 공격 증가 독립·특공 계수",inputs.SequenceEqual(new[]{initial,initial,initial+200}),string.Join(",",inputs));
                Check("맹화 3중첩 소비와 취약 유지",!victim.HasStatusKey("himiko_blaze")&&victim.HasStatusKey("himiko_blaze_vulnerability"));
            }
            finally
            {
                Entities.Unit.AnyDamageDealt-=Capture;
                CombatRoundLifecycle.End(grid.heroList,grid.enemyList);
                foreach(var u in grid.heroList.Concat(grid.enemyList))
                { foreach(var code in u.ActivePassiveCodes)code.StopCode();foreach(var status in u.ActiveStatuses.ToList())u.RemoveStatus(status);u.StopAllCoroutines(); }
                UnityEngine.CoroutineLoop.Clear();PartyTreasury.Clear();Managers.GameManager.Instance=null;Managers.GridManager.Instance=null;
            }
        }
        var team=experiment.Teams[0];var enemy=experiment.Encounters[0];
        var first=simulator.Run(team,enemy,42);battles.Add(first);
        battles.Add(simulator.Run(experiment.Teams[1],enemy,91));
        var repeat=simulator.Run(team,enemy,42);battles.Add(repeat);
        Check("반복 전투 상태 격리와 동일 시드 재현",JsonSerializer.Serialize(first,Program.Line)==JsonSerializer.Serialize(repeat,Program.Line));
        Check("기본 전투 정상 종료",first.Outcome is "win" or "loss",first.Reason);
        var limited=simulator.Run(team,enemy,42,1);battles.Add(limited);
        Check("행동 상한은 무승부",limited.Outcome=="draw"&&limited.Reason=="limit");
        Check("종료 후 재보·코루틴 정리",!PartyTreasury.Active&&PartyTreasury.Total==0&&UnityEngine.CoroutineLoop.Count==0&&Managers.GridManager.Instance==null);
        foreach(var definition in simulator.Data.FetchUnitDataList().units)
        {
            var rosterTeam=new Team{Name="roster/"+definition.id,Members=new(){new Member{Id=definition.id,Row=2,Resource=definition.manaMax>0?definition.manaMax:100}}};
            var result=simulator.Run(rosterTeam,enemy,17,150,20);battles.Add(result);
            Check("아군 전투 경로/"+definition.id,result.Outcome!="error",result.Outcome=="error"?result.Reason:result.Outcome);
        }
        foreach(var definition in simulator.Data.FetchEnemyDataList().enemies)
        {
            var encounter=new Encounter{Name="enemy/"+definition.id,Kind=definition.tier=="boss"?"Boss":definition.tier=="elite"?"Elite":"Normal",Enemies=new(){new Member{Id=definition.id,Row=2,Resource=definition.manaMax>0?definition.manaMax:100}}};
            var result=simulator.Run(team,encounter,22,100,20);battles.Add(result);
            Check("적 전투 경로/"+definition.id,result.Outcome!="error",result.Outcome=="error"?result.Reason:result.Outcome);
        }
        foreach(var entry in Codes.Base.RewardCodeCatalog.All().Where(e=>Codes.Passive.Reward.RewardCodeFactory.IsImplemented(e.id)))
        {
            var rewardTeam=Program.Sample().Teams[0];rewardTeam.Name="reward/"+entry.id;rewardTeam.Members[0].Rewards.Add(entry.id);
            var result=simulator.Run(rewardTeam,enemy,55,150,20);battles.Add(result);
            Check("보상 코드 실행/"+entry.id,result.Outcome!="error",result.Outcome=="error"?result.Reason:result.Outcome);
        }
        foreach(var ids in new[]{new[]{63,60,61,64,66},new[]{65,200,220,7,82},new[]{180,80,125,121,20},new[]{164,165,166,167,168},new[]{161,160,120,124,82}})
        {
            var mixed=new Team{Name="mixed/"+string.Join("-",ids),Gold=20000};
            for(int i=0;i<ids.Length;i++)mixed.Members.Add(new Member{Id=ids[i],Column=i<2?1:2,Row=i<2?i+1:i-1,Level=15,Resource=1000});
            var hard=new Encounter{Name="혼합 회귀",Stage=15,Enemies=new(){new Member{Id=1001,Row=1,Resource=100},new Member{Id=1001,Row=2,Resource=100},new Member{Id=1001,Column=2,Row=2,Resource=100}}};
            var result=simulator.Run(mixed,hard,1234,400,30);battles.Add(result);
            Check(mixed.Name,result.Outcome!="error",result.Outcome=="error"?result.Reason:string.Join(",",result.ActionsByKind.Select(p=>p.Key+"="+p.Value)));
        }
        Check("전수 실행 뒤 동일 시드 재현",JsonSerializer.Serialize(first,Program.Line)==JsonSerializer.Serialize(simulator.Run(team,enemy,42),Program.Line));
        bool rejected=false;try{var invalid=Program.Sample();invalid.Teams[0].Members[1].Row=2;simulator.Validate(invalid);}catch(ArgumentException){rejected=true;}
        Check("겹치는 배치 거부",rejected);
        Check("모든 전투 종료 후 정리",!PartyTreasury.Active&&PartyTreasury.Total==0&&UnityEngine.CoroutineLoop.Count==0&&Managers.GridManager.Instance==null);
        File.WriteAllText(Path.Combine(directory,"checks.json"),JsonSerializer.Serialize(new{passed=checks.Count-failed,failed,checks},Program.Json));
        File.WriteAllLines(Path.Combine(directory,"battles.jsonl"),battles.Select(b=>JsonSerializer.Serialize(b,Program.Line)));
        string root=Program.FindRoot();
        File.WriteAllText(Path.Combine(directory,"manifest.json"),JsonSerializer.Serialize(new{createdUtc=DateTime.UtcNow,dataHashes=Program.Hashes(Managers.DataManager.DirectoryPath,"*.yaml"),sourceHashes=Program.Hashes(Path.Combine(root,"Assets/Scripts"),"*.cs"),hostHashes=Program.Hashes(Path.Combine(root,"Tools/BalanceSim"),"*.cs")},Program.Json));
        Console.WriteLine($"검사 {checks.Count-failed}/{checks.Count} 통과. {Path.GetFullPath(directory)}");return failed==0?0:2;
    }
}
