using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using NeverTheLast.Simulation;

internal static class Program
{
    internal static readonly JsonSerializerOptions Json=new() { PropertyNamingPolicy=JsonNamingPolicy.CamelCase,PropertyNameCaseInsensitive=true,WriteIndented=true,IncludeFields=true,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,Encoder=JavaScriptEncoder.Create(UnicodeRanges.All) };
    internal static readonly JsonSerializerOptions Line=new(Json){WriteIndented=false};
    private static int Main(string[] args)
    {
        Console.OutputEncoding=Encoding.UTF8;
        try
        {
            string root=FindRoot(),command=args.Length==0?"help":args[0];
            var options=new Dictionary<string,string>();
            for(int i=1;i<args.Length;i++)
            {
                if(!args[i].StartsWith("--")||i+1>=args.Length) throw new ArgumentException("옵션은 --이름 값 형식입니다.");
                string key=args[i++];if(!options.TryAdd(key,args[i]))throw new ArgumentException("중복 옵션: "+key);
            }
            foreach(var option in options.Keys) if(!new[]{"--data","--config","--out","--runs","--seed","--traces","--unity","--sim","--unity-traces","--sim-traces"}.Contains(option)) throw new ArgumentException("알 수 없는 옵션: "+option);
            string data=Path.GetFullPath(options.GetValueOrDefault("--data",Path.Combine(root,"Assets/Resources/Data")));
            if(command is "help" or "--help")
            {
                Console.WriteLine("NeverTheLast 밸런스 시뮬레이터 (.NET 9, Unity 불필요)\n"+
                    "catalog [--data 폴더]\ncopy-data --out 새폴더\nsample --out 새파일.json\n"+
                    "run --config 실험.json [--data 폴더] [--out 새폴더] [--runs 횟수] [--seed 정수]\n"+
                    "self-test [--data 폴더] [--out 새폴더]\n"+
                    "compare --unity Unity결과.jsonl --sim 콘솔결과.jsonl --out 새폴더");return 0;
            }
            if(command=="copy-data")
            {
                string destination=Required(options,"--out");EnsureNewDirectory(destination);
                foreach(string file in Directory.GetFiles(data,"*.yaml"))File.Copy(file,Path.Combine(destination,Path.GetFileName(file)));
                Console.WriteLine(Path.GetFullPath(destination));return 0;
            }
            if(command=="sample")
            {
                using var output=new FileStream(Required(options,"--out"),FileMode.CreateNew);JsonSerializer.Serialize(output,Sample(),Json);return 0;
            }
            if(command=="compare")return ParityComparison.Run(Required(options,"--unity"),Required(options,"--sim"),Required(options,"--out"),options.GetValueOrDefault("--unity-traces"),options.GetValueOrDefault("--sim-traces"));
            var simulator=new Simulator(data);
            if(options.TryGetValue("--traces",out string traceDirectory))
            {
                EnsureNewDirectory(traceDirectory);int traceIndex=0;
                simulator.TraceCompleted=(result,events)=>File.WriteAllLines(Path.Combine(traceDirectory,(traceIndex++).ToString("D5")+".txt"),events);
            }
            if(command=="catalog")
            {
                object Stats(Managers.CombatantData u)=>Enum.GetValues<BaseClasses.BaseEnums.StatType>()
                    .ToDictionary(s=>s.ToString(),s=>new {baseValue=u.BaseOf(s),growth=u.GrowthOf(s)});
                Console.WriteLine(JsonSerializer.Serialize(new {
                    units=simulator.Data.FetchUnitDataList().units.Select(u=>new {u.id,u.name,u.element,u.codes,stats=Stats(u)}),
                    enemies=simulator.Data.FetchEnemyDataList().enemies.Select(u=>new {u.id,u.name,u.tier,u.archetype,u.codes,stats=Stats(u)}),
                    rewards=Codes.Base.RewardCodeCatalog.All().Select(e=>new {e.id,e.verbalName,e.codeName,e.description,e.tier,e.codeTags,e.flatStats,e.percentStats,e.supersededBy,e.shopFixed,e.shopPrice}),
                    combatBudget=new[]{16,34,68,102,136,170,200}.Select(stage=>new {stage,
                        normalDamage=Combat.EnemyCombatBudget.DamageMultiplier(Managers.StageKind.Normal,stage),
                        eliteDamage=Combat.EnemyCombatBudget.DamageMultiplier(Managers.StageKind.Elite,stage),
                        bossDamage=Combat.EnemyCombatBudget.DamageMultiplier(Managers.StageKind.Boss,stage),
                        normalSpeed=Combat.EnemyCombatBudget.SpeedMultiplier(Managers.StageKind.Normal,stage),
                        bossSpeed=Combat.EnemyCombatBudget.SpeedMultiplier(Managers.StageKind.Boss,stage)})
                },Json));return 0;
            }
            string directory=options.GetValueOrDefault("--out",Path.Combine(root,"Logs","BalanceSim",DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ",CultureInfo.InvariantCulture)));
            if(command=="self-test")return Regression.Run(simulator,directory);
            if(command!="run")throw new ArgumentException("알 수 없는 명령: "+command);
            var experiment=JsonSerializer.Deserialize<Experiment>(File.ReadAllText(Required(options,"--config")),Json)??throw new ArgumentException("빈 실험");
            if(options.TryGetValue("--runs",out string runs))experiment.Runs=int.Parse(runs,CultureInfo.InvariantCulture);
            if(options.TryGetValue("--seed",out string seed))experiment.Seed=int.Parse(seed,CultureInfo.InvariantCulture);
            simulator.Validate(experiment);EnsureNewDirectory(directory);
            File.WriteAllText(Path.Combine(directory,"experiment.json"),JsonSerializer.Serialize(experiment,Json));
            File.WriteAllText(Path.Combine(directory,"manifest.json"),JsonSerializer.Serialize(new
            {
                createdUtc=DateTime.UtcNow,dataDirectory=data,runtime=Environment.Version.ToString(),dataHashes=Hashes(data,"*.yaml"),
                sourceHashes=Hashes(Path.Combine(root,"Assets/Scripts"),"*.cs"),hostHashes=Hashes(Path.Combine(root,"Tools/BalanceSim"),"*.cs"),
                limitations=new[]{"고정 전투 비교이며 런 보상 선택·경제 진행은 실행하지 않음","Unity 화면·동시 투사체 시점과의 동등성 검증은 별도"}
            },Json));
            var all=new List<BattleResult>();
            using(var writer=new StreamWriter(Path.Combine(directory,"battles.jsonl"),false,new UTF8Encoding(false)))
            foreach(var team in experiment.Teams)
            foreach(var encounter in experiment.Encounters)
            {
                var group=new List<BattleResult>();
                for(int i=0;i<experiment.Runs;i++)
                {
                    var battle=simulator.Run(team,encounter,unchecked(experiment.Seed+i),experiment.MaxActions,experiment.MaxCycles);
                    group.Add(battle);all.Add(battle);writer.WriteLine(JsonSerializer.Serialize(battle,Line));writer.Flush();
                }
                Console.WriteLine($"{team.Name} / {encounter.Name}: 승 {group.Count(b=>b.Outcome=="win")}, 패 {group.Count(b=>b.Outcome=="loss")}, 무 {group.Count(b=>b.Outcome=="draw")}, 오류 {group.Count(b=>b.Outcome=="error")}");
            }
            WriteSummary(all,directory);Console.WriteLine(Path.GetFullPath(directory));return all.Any(b=>b.Outcome=="error")?2:0;
        }
        catch(Exception error){Console.Error.WriteLine(error.Message);return 1;}
    }
    internal static void EnsureNewDirectory(string path){if(Directory.Exists(path)||File.Exists(path))throw new IOException("기존 경로를 덮어쓰지 않습니다: "+path);Directory.CreateDirectory(path);}
    private static string Required(Dictionary<string,string> options,string key)=>options.GetValueOrDefault(key)??throw new ArgumentException(key+" 옵션이 필요합니다.");
    internal static string FindRoot()
    {
        foreach(string start in new[]{Environment.CurrentDirectory,AppContext.BaseDirectory})
        for(var directory=new DirectoryInfo(start);directory!=null;directory=directory.Parent)
            if(File.Exists(Path.Combine(directory.FullName,"ProjectSettings/ProjectVersion.txt")))return directory.FullName;
        throw new DirectoryNotFoundException("저장소 안에서 실행하세요.");
    }
    internal static Dictionary<string,string> Hashes(string directory,string pattern)=>Directory.GetFiles(directory,pattern,SearchOption.AllDirectories)
        .Where(p=>!p.Contains(Path.DirectorySeparatorChar+"obj"+Path.DirectorySeparatorChar)&&!p.Contains(Path.DirectorySeparatorChar+"bin"+Path.DirectorySeparatorChar))
        .OrderBy(p=>p,StringComparer.Ordinal).ToDictionary(p=>Path.GetRelativePath(directory,p).Replace('\\','/'),p=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));
    internal static Experiment Sample()=>new()
    {
        Runs=100,Teams=new()
        {
            new Team{Name="기본",Members=new(){new Member{Id=7,Column=1,Row=2},new Member{Id=1,Column=1,Row=3},new Member{Id=3,Column=2,Row=2},new Member{Id=44,Column=2,Row=3},new Member{Id=82,Column=2,Row=1}}},
            new Team{Name="오리온",Members=new(){new Member{Id=7,Column=1,Row=2},new Member{Id=1,Column=1,Row=3},new Member{Id=22,Column=2,Row=2},new Member{Id=83,Column=2,Row=3},new Member{Id=82,Column=2,Row=1}}}
        },Encounters=new(){new Encounter{Name="늑대 셋",Enemies=new(){new Member{Id=1001,Column=1,Row=1},new Member{Id=1001,Column=1,Row=2},new Member{Id=1001,Column=1,Row=3}}}}
    };
    private static void WriteSummary(List<BattleResult> battles,string directory)
    {
        var summaries=battles.GroupBy(b=>(b.Team,b.Encounter)).Select(group=>
        {
            int wins=group.Count(b=>b.Outcome=="win"),losses=group.Count(b=>b.Outcome=="loss"),draws=group.Count(b=>b.Outcome=="draw"),errors=group.Count(b=>b.Outcome=="error"),valid=wins+losses+draws;
            double p=valid==0?0:wins/(double)valid,z=1.959963984540054,n=Math.Max(1,valid),denominator=1+z*z/n,center=(p+z*z/(2*n))/denominator,half=z*Math.Sqrt(p*(1-p)/n+z*z/(4*n*n))/denominator;
            var completed=group.Where(b=>b.Outcome is "win" or "loss").ToArray();
            return new {team=group.Key.Team,encounter=group.Key.Encounter,runs=group.Count(),wins,losses,draws,errors,valid,
                winRate=valid==0?(double?)null:p,ci95Low=valid==0?(double?)null:Math.Max(0,center-half),ci95High=valid==0?(double?)null:Math.Min(1,center+half),
                meanActions=completed.Length==0?(double?)null:completed.Average(b=>b.Actions),meanCycles=completed.Length==0?(double?)null:completed.Average(b=>b.ActionValue/100d),meanSurvivors=completed.Length==0?(double?)null:completed.Average(b=>b.Units.Count(u=>!u.Enemy&&u.Hp>0))};
        }).ToArray();
        File.WriteAllText(Path.Combine(directory,"summary.json"),JsonSerializer.Serialize(summaries,Json));
        using var writer=new StreamWriter(Path.Combine(directory,"summary.csv"),false,new UTF8Encoding(true));
        writer.WriteLine("team,encounter,runs,wins,losses,draws,errors,winRate,ci95Low,ci95High,meanActions,meanCycles,meanSurvivors");
        foreach(var row in summaries)writer.WriteLine(string.Join(",",new object[]{row.team,row.encounter,row.runs,row.wins,row.losses,row.draws,row.errors,row.winRate,row.ci95Low,row.ci95High,row.meanActions,row.meanCycles,row.meanSurvivors}.Select(Csv)));
        using var units=new StreamWriter(Path.Combine(directory,"units.csv"),false,new UTF8Encoding(true));
        units.WriteLine("team,encounter,id,name,enemy,column,row,samples,meanDamage,meanHealing,meanShielding,survivalRate,meanTurns");
        foreach(var group in battles.Where(b=>b.Outcome!="error").SelectMany(b=>b.Units.Select(u=>new{b.Team,b.Encounter,Unit=u})).GroupBy(x=>(x.Team,x.Encounter,x.Unit.Id,x.Unit.Name,x.Unit.Enemy,x.Unit.Column,x.Unit.Row)))
            units.WriteLine(string.Join(",",new object[]{group.Key.Team,group.Key.Encounter,group.Key.Id,group.Key.Name,group.Key.Enemy,group.Key.Column,group.Key.Row,group.Count(),group.Average(x=>x.Unit.Damage),group.Average(x=>x.Unit.Healing),group.Average(x=>x.Unit.Shielding),group.Average(x=>x.Unit.Hp>0?1d:0d),group.Average(x=>x.Unit.Turns)}.Select(Csv)));
    }
    private static string Csv(object value)
    {
        string text=Convert.ToString(value,CultureInfo.InvariantCulture)??"";
        if(text.Length>0&&"=+-@".Contains(text[0])&&value is string)text="'"+text;
        return "\""+text.Replace("\"","\"\"")+"\"";
    }
}
