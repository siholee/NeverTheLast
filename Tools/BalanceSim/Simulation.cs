using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BaseClasses;
using Codes.Base;
using Combat;
using Core;
using Entities;
using Managers;
using UnityEngine;

namespace NeverTheLast.Simulation
{

    /// <summary>한 프로세스에서 순차 실행한다. 게임의 정적 이벤트·재보·소환 상태를 병렬 공유하지 않는다.</summary>
    public sealed class Simulator
    {
        public Action<BattleResult,IReadOnlyList<string>> TraceCompleted { get; set; }
        public DataManager Data { get; }
        public Simulator(string dataDirectory)
        {
            if(GameManager.Instance!=null) throw new InvalidOperationException("진행 중인 전투의 데이터 원본을 바꿀 수 없습니다.");
            DataManager.DirectoryPath=System.IO.Path.GetFullPath(dataDirectory);
            UnityEngine.Resources.DataDirectory=DataManager.DirectoryPath;
            CodeCatalog.Invalidate();
            Data=new DataManager();
            ValidateCatalog(Data.FetchUnitDataList().units);
            ValidateCatalog(Data.FetchEnemyDataList().enemies);
        }
        private static void ValidateCatalog<T>(IReadOnlyList<T> list) where T:CombatantData
        {
            if(list==null||list.Count==0) throw new ArgumentException("비어 있는 유닛 데이터");
            if(list.Select(u=>u.id).Distinct().Count()!=list.Count) throw new ArgumentException("중복 유닛 ID");
            foreach(var unit in list)
            {
                if(unit.id<=0||string.IsNullOrWhiteSpace(unit.name)||unit.hpBase<=0||unit.speedBase<=0||unit.manaMax<0)
                    throw new ArgumentException($"유닛 데이터 오류: {unit.id}");
                foreach(BaseEnums.StatType stat in Enum.GetValues(typeof(BaseEnums.StatType)))
                    if(!float.IsFinite(unit.BaseOf(stat))||!float.IsFinite(unit.GrowthOf(stat))) throw new ArgumentException($"비정상 스탯: {unit.id}/{stat}");
            }
        }
        public void Validate(Experiment experiment)
        {
            if(experiment.Runs<1||experiment.Runs>1000000||experiment.MaxActions<1||!float.IsFinite(experiment.MaxCycles)||experiment.MaxCycles<=0)
                throw new ArgumentException("runs/maxActions/maxCycles 범위 오류");
            if(experiment.Teams.Count==0||experiment.Encounters.Count==0) throw new ArgumentException("편성과 적 조합이 필요합니다.");
            if(experiment.Teams.Select(t=>t.Name).Distinct().Count()!=experiment.Teams.Count||experiment.Encounters.Select(t=>t.Name).Distinct().Count()!=experiment.Encounters.Count)
                throw new ArgumentException("편성/적 조합 이름이 중복됩니다.");
            foreach(var team in experiment.Teams)
            {
                if(string.IsNullOrWhiteSpace(team.Name)||team.Gold<0||team.Members.Count>5) throw new ArgumentException("편성 이름·골드·인원 오류");
                ValidateMembers(team.Members,false);
                if(team.Members.Select(m=>m.Id).Distinct().Count()!=team.Members.Count) throw new ArgumentException("아군 중복 편성");
            }
            foreach(var encounter in experiment.Encounters)
            {
                if(string.IsNullOrWhiteSpace(encounter.Name)||encounter.Stage<1||!Enum.TryParse<StageKind>(encounter.Kind,true,out var kind)||!Enum.IsDefined(kind)||kind==StageKind.Rest)
                    throw new ArgumentException("적 조합 이름·스테이지·종류 오류");
                ValidateMembers(encounter.Enemies,true);
            }
        }
        private void ValidateMembers(List<Member> members,bool enemy)
        {
            if(members==null||members.Count==0||members.Count>8) throw new ArgumentException("진영 인원은 1~8명이어야 합니다.");
            var occupied=new HashSet<(int,int)>();
            foreach(var m in members)
            {
                if(m==null||m.Column<1||m.Column>2||m.Row<1||m.Row>4||!occupied.Add((m.Column,m.Row))||m.Level<1||!float.IsFinite(m.HpRatio)||m.HpRatio<=0||m.HpRatio>1||m.Resource<0)
                    throw new ArgumentException("배치·레벨·HP·자원 오류");
                CombatantData definition=enemy?Data.FetchEnemyDataList().enemies.FirstOrDefault(u=>u.id==m.Id):Data.FetchUnitDataList().units.FirstOrDefault(u=>u.id==m.Id);
                if(definition==null) throw new ArgumentException($"없는 {(enemy?"적":"아군")} ID: {m.Id}");
                if(definition.CodeId("normal")==0) throw new ArgumentException($"일반행동 정의 없음: {m.Id}");
                if(m.Rewards.Count>Unit.RewardCodeSlotCount||m.Rewards.Any(id=>!RewardCodeCatalog.IsRewardCode(id))||m.Rewards.Distinct().Count()!=m.Rewards.Count)
                    throw new ArgumentException($"보상 코드 오류: {m.Id}");
                if(m.Rewards.Any(a=>m.Rewards.Any(b=>a!=b&&RewardCodeCatalog.IsUpperOf(a,b)))) throw new ArgumentException("하위·상위 보상 코드를 동시에 활성화할 수 없습니다.");
                if(m.Stacks.Any(s=>s==null||string.IsNullOrWhiteSpace(s.key)||s.value<0||!float.IsFinite(s.fraction)||s.fraction<0||s.fraction>=1)) throw new ArgumentException("영구 중첩 오류");
                if(m.Nectar.Any(n=>n==null||!Enum.TryParse<BaseEnums.StatType>(n.stat,out var stat)||!Enum.IsDefined(stat)||!float.IsFinite(n.amount)||n.amount<0)) throw new ArgumentException("넥타르 스탯 오류");
                if(m.RewardInstances.Select(i=>i.codeId).Distinct().Count()!=m.RewardInstances.Count||m.RewardInstances.Any(i=>!m.Rewards.Contains(i.codeId)||!float.IsFinite(i.charge)||i.charge<0||i.charge>(RewardCodeCatalog.Find(i.codeId)?.capacitorCap??0))) throw new ArgumentException("축전 상태 오류");
                if(enemy&&(m.Rewards.Count>0||m.Stacks.Count>0||m.Nectar.Count>0)) throw new ArgumentException("적의 보상·런 성장 입력은 지원하지 않습니다.");
            }
        }
        public BattleResult Run(Team team,Encounter encounter,int seed,int maxActions=3000,float maxCycles=100)
        {
            var result=new BattleResult { Team=team.Name,Encounter=encounter.Name,Seed=seed,TeamDefinition=team,EncounterDefinition=encounter,MaxActions=maxActions,MaxCycles=maxCycles };
            using var trace=new CombatParityTrace(result,TraceCompleted!=null);
            var game=new GameManager {dataManager=Data,unitDataList=Data.FetchUnitDataList()};
            var grid=new GridManager();
            GameManager.Instance=game; GridManager.Instance=grid;
            game.RoundManager.Stage=encounter.Stage; game.RoundManager.EnemyLevel=encounter.Stage;
            game.RoundManager.CurrentStageKind=Enum.Parse<StageKind>(encounter.Kind,true);
            game.IsEventBattleInProgress=game.RoundManager.CurrentStageKind==StageKind.Event;
            game.inventoryManager.Gold=team.Gold;
            using var randomSession=CombatRandom.BeginSeed(seed);
            Time.time=0; Time.deltaTime=.02f;
            try
            {
                foreach(var member in team.Members) Spawn(member,false);
                foreach(var member in encounter.Enemies) Spawn(member,true);
                grid.RequestFieldLayoutRefresh();
                trace.SyncRoster();
                PartyTreasury.BeginBattle();
                CombatRoundLifecycle.Begin(grid.heroList,grid.enemyList);
                game.ActionScheduler.BeginRound();
                const float delta=.02f;
                int ticks=0;
                while(true)
                {
                    bool allies=grid.heroList.Any(u=>u.IsOnField),enemies=grid.enemyList.Any(u=>u.IsOnField);
                    if(!allies||!enemies) {result.Outcome=allies?"win":enemies?"loss":"draw";result.Reason="annihilation";break;}
                    if(game.ActionScheduler.ActionCount>=maxActions||game.ActionScheduler.ReferenceCyclesElapsed>=maxCycles)
                    {result.Outcome="draw";result.Reason="limit";break;}
                    if(++ticks>maxActions*5000L+10000) throw new InvalidOperationException("전투 진행 정지");
                    grid.RequestFieldLayoutRefresh();
                    game.ActionScheduler.Tick(delta);
                    CoroutineLoop.Tick(delta);
                }
            }
            catch(Exception error) {result.Outcome="error";result.Reason=error.ToString();}
            finally
            {
                trace.Capture();
                trace.Dispose();
                try
                {
                    game.ActionScheduler.EndRound();
                    CombatRoundLifecycle.End(grid.heroList,grid.enemyList);
                    foreach(var unit in grid.heroList.Concat(grid.enemyList).ToArray()) unit.ClearCombatStateForRemoval();
                }
                catch(Exception error) {result.Outcome="error";result.Reason+="\n정리 실패: "+error;}
                finally
                {
                    CoroutineLoop.Clear();PartyTreasury.Clear();GameManager.Instance=null;GridManager.Instance=null;
                }
            }
            TraceCompleted?.Invoke(result,trace.Events);
            return result;

            void Spawn(Member member,bool enemy)
            {
                game.RoundManager.EnemyLevel=member.Level??encounter.Stage;
                var unit=grid.SpawnUnit(member.Column*(enemy?1:-1),member.Row,enemy,member.Id);
                game.RoundManager.EnemyLevel=encounter.Stage;
                if(unit==null) throw new InvalidOperationException("유닛 생성 실패");
                if(unit.ActiveNormalCode==null) throw new InvalidOperationException($"일반행동 구현 없음: {member.Id}");
                if(!enemy)
                {
                    unit.GrantLevels((member.Level??1)-1);
                    foreach(int id in member.Rewards) if(!unit.TryGrantRewardCode(id)) throw new InvalidOperationException($"활성 제한에 걸린 보상 코드: {id}");
                    foreach(var stack in member.Stacks) unit.SetPermanentStacks(stack.key,stack.value,stack.fraction);
                    if(member.Nectar.Count>0||member.RewardInstances.Count>0)
                    {
                        var save=new UnitSaveData();unit.WriteRunState(save);
                        save.nectarStats=member.Nectar;
                        foreach(var instance in member.RewardInstances)
                        {
                            int slot=save.activeRewardCodeSlots.IndexOf(instance.codeId);
                            if(slot<0) throw new ArgumentException("축전 상태에 대응하는 활성 코드가 없습니다.");
                            save.activeRewardCodeInstances[slot].charge=instance.charge;
                        }
                        unit.RestoreRunState(save);
                    }
                }
                unit.AddUltimateResource(member.Resource);
                unit.ModifyHp(Mathf.Max(1,Mathf.RoundToInt(unit.HpMax*member.HpRatio)));
                trace.Register(unit);
            }
        }
    }
}
