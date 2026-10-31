#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BaseClasses;
using Core;
using Entities;
using UnityEngine;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private CampaignSpec _shi200Spec;
        private static bool _shi200CodeFocus;
        private readonly Dictionary<UnitData, (float attack, float hp)> _shi200Original = new();
        private Shi200Metrics _shi200Metrics;
        private RunSaveData _shi200Preparation;
        private void Shi200Restore()
        {
            _shi200Metrics?.Dispose(); _shi200Metrics = null;
            foreach (var pair in _shi200Original) { pair.Key.atkGrowth = pair.Value.attack; pair.Key.hpGrowth = pair.Value.hp; }
            _shi200Original.Clear(); _shi200Spec = null; _shi200Preparation = null; _shi200CodeFocus=false;
        }
        private void Shi200Configure(CampaignSpec spec)
        {
            Shi200Restore();
            if (string.IsNullOrEmpty(spec.study)) return;
            if (spec.study != "shi200" || spec.policy != "shi" || !spec.ids.OrderBy(x => x).SequenceEqual(new[] {1,2,25,44,60}))
                throw new InvalidOperationException("200층 실험 조건 오류");
            if (spec.shiAttackGrowthScale == 0) spec.shiAttackGrowthScale = 1;
            if (spec.partyHpGrowthScale == 0) spec.partyHpGrowthScale = 1;
            if (spec.shiAttackGrowthScale < 1 || spec.shiAttackGrowthScale > 4 || spec.partyHpGrowthScale < 1 || spec.partyHpGrowthScale > 4)
                throw new InvalidOperationException("성장 진단 배율은 1~4");
            _shi200Spec = spec;
            _shi200CodeFocus=spec.studyCodeFocus;
            foreach (var data in GameManager.Instance.unitDataList.units.Where(x => spec.ids.Contains(x.id)))
            {
                _shi200Original[data] = (data.atkGrowth, data.hpGrowth);
                if (data.id == 2) data.atkGrowth *= spec.shiAttackGrowthScale;
                data.hpGrowth *= spec.partyHpGrowthScale;
            }
        }
        private static float Shi200CodeBonus(Unit unit,int id)
        {
            if(!_shi200CodeFocus)return 0;
            if(unit.ID==60&&id==634)return 50;
            if(unit.ID==60&&id==823)return 30;
            // 시의 재행동은 Special이다. 이 진단에서는 추공 전용 코드의 기존 +22를 상쇄한다.
            if(unit.ID==2&&id==724)return -22;
            return 0;
        }
        [Serializable] private sealed class Shi200RestRow
        { public string run,team,phase;public int stage,foodBefore,foodAfter,manaBefore,manaAfter;public bool success; }
        private bool Shi200DefersPreparationRest()
        {
            if(_shi200Spec?.studyResourceRest!=true)return false;
            var game=GameManager.Instance;
            if(game.RoundManager.CurrentCombatKind is not (StageKind.Boss or StageKind.Elite or StageKind.MidBoss))return false;
            var sei=GridManager.Instance.heroList.FirstOrDefault(x=>x!=null&&x.ID==1);
            return sei!=null&&sei.ManaCurr<sei.ManaMax;
        }
        private void Shi200BeforePreparation(string phase="before_shop")
        {
            if(_shi200Spec?.studyResourceRest!=true)return;
            var game=GameManager.Instance;
            if(!game.CanRest)return;
            var kind=game.RoundManager.CurrentCombatKind;
            if(kind is not (StageKind.Boss or StageKind.Elite or StageKind.MidBoss))return;
            var sei=GridManager.Instance.heroList.FirstOrDefault(x=>x!=null&&x.ID==1);
            if(sei==null||sei.ManaCurr>=sei.ManaMax)return;
            var row=new Shi200RestRow{run=_baselineRun.id,team=_baselineRun.team,phase=phase,stage=game.RoundManager.Stage,
                foodBefore=game.inventoryManager.FoodUnits,manaBefore=sei.ManaCurr};
            row.success=game.TryLongRest();row.foodAfter=game.inventoryManager.FoodUnits;row.manaAfter=sei.ManaCurr;
            CampaignWrite("resource_rests",row);
        }
        private void Shi200AfterPreparation()
        {
            if(_shi200Spec?.studyResourceRest!=true)return;
            Shi200BeforePreparation("after_shop");
            var game=GameManager.Instance;
            if(!game.CanRest)return;
            var party=GridManager.Instance.heroList.Where(u=>u!=null&&(u.ID>0||u.LastActiveId>0)).ToList();
            if(party.Any(u=>!u.IsOnField))game.TryLongRest();
            else if(party.Any(u=>u.HpCurr<u.HpMax*.65f))
            {if(game.ShortRestCharges>0)game.TryShortRest();else game.TryLongRest();}
        }
        private void Shi200VerifyGrowth()
        {
            if (_shi200Spec == null) return;
            bool correct = GridManager.Instance.heroList.Where(x => x != null && _shi200Spec.ids.Contains(x.ID)).All(u =>
            {
                var data = _shi200Original.Keys.First(d => d.id == u.ID);
                return Mathf.Approximately(u.Stats.Growth(BaseEnums.StatType.Attack), data.atkGrowth) &&
                       Mathf.Approximately(u.Stats.Growth(BaseEnums.StatType.MaxHp), data.hpGrowth);
            });
            YvCheck("200층 진단 성장 데이터 반영", "해당 런의 지정 성장값", correct.ToString(), correct);
        }
        private void Shi200BeforeFight()
        {
            if (_shi200Spec == null) return;
            _shi200Metrics?.Dispose(); _shi200Metrics = new Shi200Metrics();
            _shi200Preparation = RunManager.Instance.CaptureDebugSnapshot();
        }
        [Serializable] private sealed class Shi200UnitRow
        {
            public int id, level, hp, maxHp, attack, specialAttack, defense, specialDefense, mana, x, y;
            public float speed, attackGrowth, hpGrowth;
            public bool enemy;
            public long incoming, outgoing;
            public int actions, deaths;
        }
        [Serializable] private sealed class Shi200FightRow
        {
            public string run, team, category;
            public int seed, attempt, stage;
            public bool won;
            public float enemyHpFraction;
            public List<Shi200UnitRow> units;
            public string deathOrder;
        }
        [Serializable] private sealed class Shi200LastInput
        { public string run, team; public int seed, stage; public RunSaveData preparation; public List<Shi200UnitRow> units; }
        private void Shi200BattleStarted(CampaignBattle battle)
        {
            if (_shi200Spec == null) return;
            Shi200VerifyGrowth();
            _shi200Metrics.Seal(GridManager.Instance.heroList.Concat(GridManager.Instance.enemyList));
            File.WriteAllText(Path.Combine(_requestDirectory, "last-fight-input.json"), JsonUtility.ToJson(new Shi200LastInput
            { run = battle.run, team = battle.team, seed = battle.seed, stage = battle.stage, preparation = _shi200Preparation, units = _shi200Metrics.Rows }));
        }
        private void Shi200BattleFinished(CampaignBattle battle)
        {
            if (_shi200Spec == null) return;
            CampaignWrite("fight_diagnostics", new Shi200FightRow { run=battle.run, team=battle.team, seed=battle.seed,
                attempt=battle.attempt, stage=battle.stage, category=battle.category, won=battle.won,
                enemyHpFraction=battle.won?0:_shi200Metrics.EnemyHpFraction, units=_shi200Metrics.Rows,
                deathOrder=string.Join(";",_shi200Metrics.Deaths) });
            _shi200Metrics.Dispose(); _shi200Metrics=null;
        }
        private sealed class Shi200Metrics : IDisposable
        {
            private readonly Dictionary<Unit,Shi200UnitRow> _rows = new();
            private readonly Dictionary<Unit,long> _incoming = new(), _outgoing = new();
            private readonly Dictionary<Unit,int> _actions = new(), _deaths = new();
            private float _endEnemyFraction=1;
            public readonly List<int> Deaths = new();
            public List<Shi200UnitRow> Rows => _rows.Select(pair => { var row=pair.Value;
                row.incoming=_incoming.GetValueOrDefault(pair.Key);row.outgoing=_outgoing.GetValueOrDefault(pair.Key);
                row.actions=_actions.GetValueOrDefault(pair.Key);row.deaths=_deaths.GetValueOrDefault(pair.Key);return row; }).ToList();
            public float EnemyHpFraction => _endEnemyFraction;
            private void CaptureLive() { if(GameManager.Instance.gameState!=BaseEnums.GameState.RoundInProgress||_rows.Count==0)return;
                _endEnemyFraction=_rows.Where(x=>x.Value.enemy).Sum(x=>x.Key!=null&&x.Key.isActive?Mathf.Max(0,x.Key.HpCurr):0f) /
                    Mathf.Max(1,_rows.Values.Where(x=>x.enemy).Sum(x=>(float)x.maxHp)); }
            public Shi200Metrics() { Unit.AnyDamageDealt+=Damage;Unit.AnyUnitDied+=Died;ActionScheduler.AnyActionStarted+=Action; }
            public void Seal(IEnumerable<Unit> units)
            {
                foreach(var u in units.Where(x=>x!=null&&x.ID>0))_rows[u]=new Shi200UnitRow {id=u.ID,enemy=u.IsEnemy,level=u.Level,
                    hp=u.HpCurr,maxHp=u.HpMax,attack=u.AtkCurr,specialAttack=u.SpAtkCurr,defense=u.DefCurr,specialDefense=u.SpDefCurr,
                    mana=u.ManaCurr,speed=u.SpeedCurr,x=u.currentCell?.xPos??0,y=u.currentCell?.yPos??0,
                    attackGrowth=u.Stats.Growth(BaseEnums.StatType.Attack),hpGrowth=u.Stats.Growth(BaseEnums.StatType.MaxHp)};
                CaptureLive();
            }
            private void Damage(DamageResolvedContext c) { if(c.Target!=null)_incoming[c.Target]=_incoming.GetValueOrDefault(c.Target)+c.DamageDealt;
                if(c.Attacker!=null)_outgoing[c.Attacker]=_outgoing.GetValueOrDefault(c.Attacker)+c.DamageDealt;CaptureLive(); }
            private void Died(Unit u,Unit killer) { if(u==null)return;Deaths.Add(u.ID);_deaths[u]=_deaths.GetValueOrDefault(u)+1;CaptureLive(); }
            private void Action(Unit u,ActionScheduler.ActionKind kind,string label) { if(u!=null)_actions[u]=_actions.GetValueOrDefault(u)+1; }
            public void Dispose(){Unit.AnyDamageDealt-=Damage;Unit.AnyUnitDied-=Died;ActionScheduler.AnyActionStarted-=Action;}
        }
    }
}
#endif
