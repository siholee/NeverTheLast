using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Combat;
using Entities;
using UnityEngine;
namespace Helpers { public static class SpriteResource { public static Sprite LoadPortrait(string key,out string path) { path=key;return null; } } }
namespace Entities.View { public class SummonCardView { public void Depart() { } public void Overflow(Unit target) { } public void Bind(Unit unit) { } } }
namespace Entities
{
    public class Cell
    {
        public int xPos,yPos;
        public float DisplaySlot;
        public bool isOccupied;
        public float reservedTime;
        public Unit Occupant;
        public void SetOccupiedUnit(Unit unit) => Occupant=unit;
        public void SetPortrait(Sprite sprite) { }
        public void InitializeHpBar() { }
        public void UpdateUI() { }
    }
}
namespace Managers
{
    // 전투와 무관한 정산 DTO의 호스트 타입. 콘솔은 계정·인벤토리를 저장하지 않는다.
    public class RewardDef { }
    public class ValuableHolding { public int itemId, gold; }
    public enum StageKind { Normal, Elite, Event, MidBoss, Boss, Rest }
    public sealed class GameManager
    {
        public static GameManager Instance { get; set; }
        public DataManager dataManager = new();
        public UnitDataList unitDataList;
        public RoundManager RoundManager = new();
        public ActionScheduler ActionScheduler = new();
        public BaseEnums.GameState gameState=BaseEnums.GameState.RoundInProgress;
        public RunManager runManager;
        public InventoryManager inventoryManager=new();
        public bool IsEventBattleInProgress;
        public void OnKillEnemy(Unit unit) => inventoryManager.AddGold(CombatKillEconomy.GoldForStage(RoundManager.Stage));
        public void AddBattleGoldBonus(float value) { }
    }
    public class RunManager { public void SaveCurrentRun() => throw new InvalidOperationException("시뮬레이션은 저장할 수 없습니다."); }
    public class InventoryManager { public int Gold; public void AddGold(int amount) => Gold+=amount; }
    public sealed class RoundManager
    {
        public int Stage=1,EnemyLevel=1;
        public StageKind CurrentStageKind=StageKind.Normal;
        public StageKind CurrentCombatKind => CurrentStageKind;
    }
    public sealed class GridManager
    {
        public static GridManager Instance { get; set; }
        public List<Unit> heroList=new(),enemyList=new(),summonList=new();
        public event Action<Unit> UnitSpawned;
        public int GetFrontColumn(bool enemy) => enemy?1:-1;
        public int GetRearColumn(bool enemy) => enemy?2:-2;
        public bool IsFrontRow(Unit unit) => unit?.currentCell!=null && Math.Abs(unit.currentCell.xPos)==1;
        public bool IsRearRow(Unit unit) => unit?.currentCell!=null && Math.Abs(unit.currentCell.xPos)==2;
        public Unit GetUnitAtPosition(int x,int y) => heroList.Concat(enemyList).FirstOrDefault(u=>u.IsOnField&&u.currentCell.xPos==x&&u.currentCell.yPos==y);
        public void ForgetSummon(Unit unit) { if(unit==null)return;unit.ClearCombatStateForRemoval();summonList.Remove(unit); }
        public Unit SpawnSummon(Unit owner,SummonSpec spec)
        {
            if(owner==null||spec==null)return null;
            var unit=new Unit(); summonList.Add(unit);unit.InitializeAsSummon(owner,spec);return unit;
        }
        public void ClearSummons() { foreach(var unit in summonList.ToArray()) {unit.ClearCombatStateForRemoval();unit.StopAllCoroutines();} summonList.Clear(); }
        public Unit SpawnUnit(int x,int y,bool isEnemy,int unitId,bool playEntry=false,float entryDelay=0)
        {
            if(GetUnitAtPosition(x,y)!=null) return null;
            var unit=new Unit(); unit.currentCell=new Cell{xPos=x,yPos=y,isOccupied=true};
            (isEnemy?enemyList:heroList).Add(unit); unit.Spawn(unit.currentCell,isEnemy,unitId); UnitSpawned?.Invoke(unit); return unit;
        }
        public void BeginEnemyCombatLife(Unit enemy) => CombatRoundLifecycle.BeginEnemy(enemy);
        public void RequestFieldLayoutRefresh()
        {
            foreach(var column in heroList.Concat(enemyList).Where(u=>u.IsOnField).GroupBy(u=>u.currentCell.xPos))
            {
                var ordered=column.OrderBy(u=>u.currentCell.yPos).ToArray();
                for(int i=0;i<ordered.Length;i++) ordered[i].currentCell.DisplaySlot=CombatFieldLayout.DisplaySlot(i,ordered.Length);
            }
        }
        public bool IsBenchCell(Cell cell) => cell!=null&&cell.yPos<=0;
        public System.Collections.IEnumerator WaitForFieldLayoutSettled() { yield break; }
        public List<Unit> TargetNearestEnemy(Unit caster) => (caster.IsEnemy?heroList:enemyList).Where(u=>u.IsOnField&&!u.IsUntargetable).OrderBy(u=>Vector2.Distance(new(caster.currentCell.xPos,caster.currentCell.DisplaySlot),new(u.currentCell.xPos,u.currentCell.DisplaySlot))).Take(1).ToList();
    }
    public static class AudioManager
    {
        public static void PlayAction(Unit unit,SfxActionSlot slot,SfxCuePhase phase,string name="") { }
        public static void PlayCue(string cue,float volume=1f) { }
    }
}
