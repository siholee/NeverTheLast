#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive.Reward;
using Combat;
using Core;
using Effects.Buffs;
using Entities;
using UnityEngine;
using static BaseClasses.BaseEnums;
namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioIndraOpening(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();bool enabled=true;
            var spec=CampaignBuildCatalog().teams.Single(x=>x.aliases.Contains("검증 편성/b48_lokapala_surya"));
            config.lineup=spec.key;yield return EnsureLineup(config);yield return YvPrepareScene(config,v=>enabled=v);
            var game=GameManager.Instance;Unit indra=CfAlly(63);
            var king=indra.ActivePassiveCodes.OfType<Codes.Passive.IndraDevaKing>().Single();
            void Check(string name,bool ok)=>YvCheck("인드라 소집 준비 "+name,"최소40%·추가 충전 없음·기존 마나 보존",indra.ManaCurr.ToString(),ok);
            game.gameState=GameState.RoundEnd;king.StopCode();indra.ClearUltimateResource();game.gameState=GameState.Preparation;
            king.CastCode();Check("준비·저장 복원 중 자동 충전 없음",indra.ManaCurr==0);
            game.gameState=GameState.RoundInProgress;king.CastCode();Check("전투 개시 최소40%",indra.ManaCurr==40);
            indra.ClearUltimateResource();king.StopCode();king.CastCode();Check("같은 전투 재시전은 충전 없음",indra.ManaCurr==0);
            game.gameState=GameState.RoundEnd;king.StopCode();
            foreach(var status in indra.GetAllStatuses())indra.RemoveStatusByKey(status.Key);
            game.gameState=GameState.Preparation;
            indra.ApplyRewardCodeInventory(new List<int>{666,0,0,0,0,0,0},new List<int>());indra.ClearUltimateResource();
            king=indra.ActivePassiveCodes.OfType<Codes.Passive.IndraDevaKing>().Single();
            game.gameState=GameState.RoundInProgress;indra.Invoke(UnitEventType.OnRoundStart,new EventContext(indra));
            Check("실제 개전 순서·빠른 집결과65%",indra.ManaCurr==65);
            indra.CastPassiveCode();Check("개전·코드 중복 시전 방지",indra.ManaCurr==65);
            game.gameState=GameState.RoundEnd;king.StopCode();indra.ClearUltimateResource();indra.AddUltimateResource(70);
            game.gameState=GameState.RoundInProgress;king.CastCode();Check("더 높은 기존 마나 유지",indra.ManaCurr==70);
            game.gameState=GameState.RoundEnd;king.StopCode();indra.ClearUltimateResource();
            game.gameState=GameState.RoundInProgress;king.CastCode();Check("다음 전투 다시 준비",indra.ManaCurr==40);
            king.StopCode();game.DebugResetBattle();OsFinish(enabled);
        }

        private IEnumerator ScenarioNormalPressure(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); bool enabled=true;
            var spec=CampaignBuildCatalog().teams.Single(x=>x.aliases.Contains("검증 편성/b48_lokapala_surya"));
            config.lineup=spec.key;yield return EnsureLineup(config);yield return YvPrepareScene(config,v=>enabled=v);
            DebugMode.AllyInvincible=DebugMode.EnemyInvincible=false;
            var game=GameManager.Instance;var scheduler=game.ActionScheduler;
            void Check(string name,bool ok,string actual="")=>YvCheck("일반전 압박 "+name,"AV 기반 완충·증가·정리",actual,ok);
            foreach(Unit u in AliveAllies().Concat(AliveEnemies()))
            {
                foreach(var passive in u.ActivePassiveCodes.ToList())passive.StopCode();
                u.ApplyRewardCodeInventory(Enumerable.Repeat(0,7).ToList(),new List<int>());
                foreach(var status in u.GetAllStatuses())u.RemoveStatusByKey(status.Key);
                YvAddTestStatus(u,99890,"pressure_probe_hp",new StatModifierEffect(StatType.MaxHp,8000000),StatusCategory.Neutral);
                u.RemoveShield(u.ShieldCurr);u.RestoreHpOutsideCombat(u.HpMax);u.ClearUltimateResource();
            }
            Unit enemy=AliveEnemies().First(),target=CfAlly(66);
            int stage=game.RoundManager.Stage;
            Check("일반 슬롯",game.RoundManager.CurrentCombatKind==StageKind.Normal,stage.ToString());
            Check("초반 범용 하한",Near(EnemyCombatBudget.EffectiveDamageMultiplier(StageKind.Normal,1,0),2.2f));
            foreach(StageKind kind in new[]{StageKind.Elite,StageKind.MidBoss,StageKind.Boss})
                Check("정예·보스 장기 압박 "+kind,Near(EnemyCombatBudget.EffectiveDamageMultiplier(kind,96,0),EnemyCombatBudget.DamageMultiplier(kind,96)) &&
                    Near(EnemyCombatBudget.EffectiveDamageMultiplier(kind,96,20),EnemyCombatBudget.DamageMultiplier(kind,96)*4f) &&
                    Near(EnemyCombatBudget.EffectiveDamageMultiplier(kind,96,10),EnemyCombatBudget.DamageMultiplier(kind,96)*1.96f) &&
                    Near(EnemyCombatBudget.EffectiveDamageMultiplier(kind,96,11),EnemyCombatBudget.DamageMultiplier(kind,96)*2.58f));
            Check("사건 압박 제외",Near(EnemyCombatBudget.EffectiveDamageMultiplier(StageKind.Event,96,20),1));
            Check("범용 내구 보존·후속 등급 내구",Near(EnemyCombatBudget.HpMultiplier(StageKind.Boss,8),1) &&
                Near(EnemyCombatBudget.HpMultiplier(StageKind.Boss,68,3202),1) &&
                Near(EnemyCombatBudget.HpMultiplier(StageKind.Boss,68),1.4f) && Near(EnemyCombatBudget.HpMultiplier(StageKind.Elite,51),1.15f));
            scheduler.BeginRound();EnemyCombatBudget.Apply(enemy);EnemyCombatBudget.Apply(enemy);
            int encounterSize=scheduler.InitialEnemyCount;
            Check("일반 무리 피해 보정",Near(EnemyCombatBudget.NormalEncounterFactor(1),1) && Near(EnemyCombatBudget.NormalEncounterFactor(2),1) &&
                Near(EnemyCombatBudget.NormalEncounterFactor(3),Mathf.Sqrt(2f/3)) && Near(EnemyCombatBudget.NormalEncounterFactor(4),Mathf.Sqrt(.5f)) && encounterSize==AliveEnemies().Count);
            Check("중립·중복 없음",enemy.GetAllStatuses().Count(s=>s.Key==EnemyCombatBudget.StatusKey)==1 &&
                enemy.GetAllStatuses().Single(s=>s.Key==EnemyCombatBudget.StatusKey).Category==StatusCategory.Neutral);
            // 경계 AV만 주입한다. 실제 TakeDamage 경로가 현재 스케줄러 시계를 읽는지 확인한다.
            var clock=typeof(ActionScheduler).GetProperty(nameof(ActionScheduler.CombatActionValueElapsed));
            foreach(float round in new[]{0f,.5f,1f,1.15f,1.35f,2f,3f,4f,20f})
            {
                clock.SetValue(scheduler,round*100f);target.RestoreHpOutsideCombat(target.HpMax);
                var hit=new DamageContext(enemy,10000,CodeType.Normal,new(){DamageTag.TrueDamage},false);
                target.TakeDamage(hit);
                float baseline=EnemyCombatBudget.DamageMultiplier(StageKind.Normal,stage);
                float factor=round<=1.15f?.35f+.65f*Mathf.Clamp01((round-.65f)/.5f):Mathf.Min(2f,1f+.35f*(round-1.15f));
                if(round<1.35f) factor=Mathf.Min(factor,.2f);
                int expected=Mathf.RoundToInt(10000*Mathf.Max(2.2f,baseline*factor*EnemyCombatBudget.NormalEncounterFactor(encounterSize)));
                Check("실제 피해 "+round,Mathf.Abs(hit.ResolvedDamage-expected)<=1,hit.ResolvedDamage+"/"+expected);
            }
            float av=scheduler.CombatActionValueElapsed;yield return WaitUnscaled(.2f);
            Check("연출 시간은 압박 미증가",Near(scheduler.CombatActionValueElapsed,av));
            if(AliveEnemies().Count>1)
            {
                AliveEnemies().Last(u=>u!=enemy).Die(target);
                Check("처치 뒤 무리 예산 고정",scheduler.InitialEnemyCount==encounterSize && AliveEnemies().Count==encounterSize-1);
            }
            scheduler.BeginRound();Check("다음 전투 시계 초기화",Near(scheduler.ReferenceCyclesElapsed,0) &&
                Near(EnemyCombatBudget.EffectiveDamageMultiplier(StageKind.Normal,stage,scheduler.ReferenceCyclesElapsed),
                    Mathf.Max(2.2f,EnemyCombatBudget.DamageMultiplier(StageKind.Normal,stage)*.35f)) && !scheduler.HasStartedNormalAction(target));
            enemy.Invoke(UnitEventType.OnRoundEnd,new EventContext(enemy));
            Check("전투 종료 정리",!enemy.HasStatusKey(EnemyCombatBudget.StatusKey));
            scheduler.EndRound();
            foreach(Unit foe in AliveEnemies()) { YvAddTestStatus(foe,99889,"pressure_telemetry_hp",new StatModifierEffect(StatType.MaxHp,20000000),StatusCategory.Neutral); foe.RestoreHpOutsideCombat(foe.HpMax); }
            var observed=new DotDiagnosisRow();
            using(var recorder=new DotDiagnosisRecorder(observed))
            {
                Unit acted=CfAlly(60),unacted=CfAlly(66);
                Unit source=AliveEnemies().First();
                bool tracked=false,individual=false;
                void Started(Unit actor,ActionScheduler.ActionKind kind,string label)
                {
                    if(actor!=acted || kind!=ActionScheduler.ActionKind.Normal)return;
                    tracked=scheduler.HasStartedNormalAction(acted) && !scheduler.HasStartedNormalAction(unacted);
                    EnemyCombatBudget.Apply(source);
                    var first=new DamageContext(source,10000,CodeType.Effect,new(){DamageTag.TrueDamage},false);
                    var later=new DamageContext(source,10000,CodeType.Effect,new(){DamageTag.TrueDamage},false);
                    unacted.TakeDamage(first);acted.TakeDamage(later);
                    float baseline=EnemyCombatBudget.DamageMultiplier(StageKind.Normal,stage)*EnemyCombatBudget.NormalEncounterFactor(scheduler.InitialEnemyCount);
                    individual=Mathf.Abs(first.ResolvedDamage-Mathf.RoundToInt(10000*Mathf.Max(2.2f,baseline*.2f)))<=1 &&
                        Mathf.Abs(later.ResolvedDamage-Mathf.RoundToInt(10000*Mathf.Max(2.2f,baseline*.35f)))<=1;
                }
                ActionScheduler.AnyActionStarted+=Started;
                try { yield return NordAction(acted,ActionScheduler.ActionKind.Normal,sch=>sch.AdvanceAction(acted,1)); }
                finally { ActionScheduler.AnyActionStarted-=Started; }
                Check("일반 시작 기록·추가행동과 분리",tracked);
                Check("첫 일반행동 이후 개별 완충 종료",individual);
                foreach(Unit victim in new[]{unacted,acted})
                    victim.TakeDamage(new DamageContext(source,20000000,CodeType.Effect,new(){DamageTag.TrueDamage},false));
                Check("첫일반행동전 사망 계측",observed.frontlineDeathsBeforeFirstNormal==1 &&
                    observed.units.Single(u=>u.id==66).diedBeforeFirstNormal && !observed.units.Single(u=>u.id==60).diedBeforeFirstNormal);
                Check("동일AV에서도 행동 시작 순서 구별",Near(observed.units.Single(u=>u.id==60).firstNormalCycle,0) &&
                    Near(observed.units.Single(u=>u.id==66).firstDeathCycle,0) && observed.units.Single(u=>u.id==66).firstNormalCycle<0);
            }
            scheduler.BeginRound();Check("행동 기록 새 전투 초기화",!scheduler.HasStartedNormalAction(CfAlly(60)));
            scheduler.EndRound();
            OsFinish(enabled);
        }

        // 피해 투자·적 방어와 독립된 유지력 및 중단 경계를 실제 스케줄러에서 검증한다.
        private sealed class ChandraAuditModifier : Effects.Base.BaseEffect
        {
            private readonly bool _healing;
            public ChandraAuditModifier(bool healing) : base(0) => _healing = healing;
            public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context) => _healing ? 1f : 3f;
            public override float OutgoingHealingMultiplierModifier(Unit source, Unit target) => _healing ? 2f : 1f;
        }

        private IEnumerator ScenarioChandraRole(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); _cfActionIdSequence=0; bool enabled=true;
            var spec=CampaignBuildCatalog().teams.Single(x=>x.aliases.Contains("검증 편성/b48_lokapala_surya"));
            config.lineup=spec.key;yield return EnsureLineup(config);yield return YvPrepareScene(config,v=>enabled=v);
            DebugMode.AllyInvincible=DebugMode.EnemyInvincible=false;
            Unit chandra=CfAlly(60);var moon=chandra.ActivePassiveCodes.OfType<Codes.Passive.ChandraNishakara>().Single();
            foreach(Unit unit in AliveAllies().Concat(AliveEnemies()))
            {
                unit.ApplyRewardCodeInventory(Enumerable.Repeat(0,7).ToList(),new List<int>());unit.ClearUltimateResource();CfNoCrit(unit);
                if(unit.IsEnemy)YvAddTestStatus(unit,99891,"chandra_enemy_hp",new StatModifierEffect(StatType.MaxHp,2000000),StatusCategory.Neutral);
                unit.RestoreHpOutsideCombat(unit.HpMax);
            }
            moon.StopCode();
            void Check(string name,bool ok,string actual="")=>YvCheck("찬드라 역할 "+name,"피해·치유 분리 및 기존 지원 유지",actual,ok);
            var heals=new List<(Unit target,int amount)>();
            void Healed(Unit target,Unit source,int effective,int overflow){if(source==chandra)heals.Add((target,effective+overflow));}
            Unit.AnyHealed+=Healed;
            IEnumerator Special()=>NordAction(chandra,ActionScheduler.ActionKind.Special,scheduler=>scheduler.EnqueueSpecial(chandra,"chandra_role",chandra.ActiveSpecialCode.CodeName,chandra.CastSpecialCode));
            void Wound(){heals.Clear();foreach(Unit ally in AliveAllies())ally.RestoreHpOutsideCombat(ally.HpMax/2);}
            int expected=Mathf.RoundToInt(chandra.HpMax*.08f);
            try
            {
                using(var log=new YvRecorder())
                {
                    yield return NordAction(chandra,ActionScheduler.ActionKind.Normal,sch=>sch.AdvanceAction(chandra,1));
                    Check("일반 최대체력15%",log.Incoming.Any(x=>x.Attacker==chandra&&x.Damage==Mathf.RoundToInt(chandra.HpMax*.15f)));
                }
                Wound();using(var log=new YvRecorder())
                {
                    yield return Special();
                    Check("전체 피해 최대체력30%",log.Incoming.Count(x=>x.Attacker==chandra&&x.Damage==Mathf.RoundToInt(chandra.HpMax*.30f))==AliveEnemies().Count);
                    Check("생존 아군 각각 체력8%",heals.Count==5&&heals.All(x=>x.amount==expected),string.Join(",",heals.Select(x=>x.amount)));
                    Check("월광 획득 유지",chandra.GetCombatResource(Codes.Passive.ChandraNishakara.ResourceId)>0);
                }
                YvAddTestStatus(chandra,99892,"chandra_damage_probe",new ChandraAuditModifier(false),StatusCategory.Neutral);
                Wound();yield return Special();Check("피해3배에도 회복 동일",heals.Count==5&&heals.All(x=>x.amount==expected));chandra.RemoveStatusByKey("chandra_damage_probe");
                foreach(Unit enemy in AliveEnemies())YvAddTestStatus(enemy,99893,"chandra_immune_probe",new CfImmune(),StatusCategory.Neutral);
                Wound();using(var log=new YvRecorder())
                {
                    yield return Special();Check("전원 피해면역에도 회복 유지",heals.Count==5&&heals.All(x=>x.amount==expected)&&log.Incoming.Count(x=>x.Attacker==chandra)==AliveEnemies().Count&&log.Incoming.Where(x=>x.Attacker==chandra).All(x=>x.ResolvedDamage==0));
                }
                foreach(Unit enemy in AliveEnemies())enemy.RemoveStatusByKey("chandra_immune_probe");
                YvAddTestStatus(chandra,99894,"chandra_heal_probe",new ChandraAuditModifier(true),StatusCategory.Neutral);
                Wound();yield return Special();Check("공용 주는치유 보정2배",heals.Count==5&&heals.All(x=>x.amount==expected*2));chandra.RemoveStatusByKey("chandra_heal_probe");
                Wound();int impacts=0;
                void Interrupt(DamageResolvedContext hit){if(hit.Attacker==chandra&&++impacts==1)chandra.ControlStarts(new ControlContext(AliveEnemies().First(),1));}
                Unit.AnyDamageDealt+=Interrupt;
                try{yield return Special();}finally{Unit.AnyDamageDealt-=Interrupt;}
                Check("시전 중 제어는 후속 피해·회복 중단",impacts==1&&heals.Count==0&&!chandra.isCasting,impacts+"/"+heals.Count);chandra.ControlEnds();
                Wound();AliveEnemies().First().RestoreHpOutsideCombat(1);yield return Special();Check("과잉 피해·대상 사망에도 회복 유지",heals.Count==5&&heals.All(x=>x.amount==expected));
                Wound();moon.CastCode();yield return Special();
                Check("달빛순환4% 별도 유지",heals.Count==6&&heals.Count(x=>x.amount==expected)==5&&heals.Any(x=>x.target==chandra&&x.amount==Mathf.RoundToInt(chandra.HpMax*.04f)));
                moon.StopCode();moon.CastCode();Wound();impacts=0;Unit.AnyDamageDealt+=Interrupt;
                try{yield return Special();}finally{Unit.AnyDamageDealt-=Interrupt;}
                Check("창야취소8%와 기존달빛순환4% 분리",impacts==1&&heals.Count==1&&heals[0].target==chandra&&heals[0].amount==Mathf.RoundToInt(chandra.HpMax*.04f),string.Join(",",heals.Select(x=>x.amount)));chandra.ControlEnds();
                Check("특수행동 AV 미소모",Near(GameManager.Instance.ActionScheduler.ReferenceCyclesElapsed,0));
            }
            finally{Unit.AnyHealed-=Healed;}
            OsFinish(enabled);
        }

        private IEnumerator ScenarioKuberaPreparation(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();_cfActionIdSequence=0;bool enabled=true;
            var spec=CampaignBuildCatalog().teams.Single(x=>x.aliases.Contains("검증 편성/b48_lokapala_kubera"));
            config.lineup=spec.key;yield return EnsureLineup(config);yield return YvPrepareScene(config,v=>enabled=v);
            DebugMode.AllyInvincible=DebugMode.EnemyInvincible=false;
            Unit kubera=CfAlly(65),indra=CfAlly(63);
            foreach(Unit unit in AliveAllies().Concat(AliveEnemies()))
            {
                unit.ApplyRewardCodeInventory(Enumerable.Repeat(0,7).ToList(),new List<int>());unit.ClearUltimateResource();CfNoCrit(unit);
                YvAddTestStatus(unit,99896,"kubera_preparation_hp",new StatModifierEffect(StatType.MaxHp,unit.IsEnemy?2000000:200000),StatusCategory.Neutral);unit.RestoreHpOutsideCombat(unit.HpMax);
            }
            void Check(string name,bool ok,string actual="")=>YvCheck("재보 개시 준비 "+name,"동시 준비·가용 재보·AV 정책 일치",actual,ok);
            foreach(int available in new[]{0,2,3})
            {
                foreach(Unit ally in AliveAllies().Concat(AliveEnemies()))ally.ClearUltimateResource();PartyTreasury.BeginBattle();PartyTreasury.Charge(kubera,available);
                using(var log=new YvRecorder())
                {
                    yield return CfRun(_=>{indra.FillUltimateResource();kubera.FillUltimateResource();},
                        ()=>log.Finished.Any(x=>x.Unit==indra&&x.Kind==ActionScheduler.ActionKind.Ultimate)&&log.Finished.Any(x=>x.Unit==kubera&&x.Kind==ActionScheduler.ActionKind.Ultimate));
                    int u=log.Started.FindIndex(x=>x.Unit==kubera&&x.Kind==ActionScheduler.ActionKind.Ultimate),g=log.Started.FindIndex(x=>x.Unit==indra&&x.Kind==ActionScheduler.ActionKind.Ultimate),sp=log.Started.FindIndex(x=>x.Unit==kubera&&x.Kind==ActionScheduler.ActionKind.Special);
                    Check("동시 준비 순서 "+available,u>=0&&g>=0&&sp>=0&&(available<3?u<g&&g<sp:g<sp&&sp<u),u+"/"+g+"/"+sp);
                    int spent=available<3?Math.Min(3,available+1):3;float power=Codes.Special.KuberaGoldRush.GoldMultiplier();int raw=Mathf.RoundToInt(Mathf.RoundToInt(kubera.StatValue(StatType.Attack)*(2.8f+spent*1.4f))*power);
                    var hits=log.Incoming.Where(x=>x.Attacker==kubera&&x.HasTag(DamageTag.SpecialAttack)&&!x.HasTag(DamageTag.BonusDamage)&&!x.HasTag(DamageTag.ElementalReaction)).ToList();
                    Check("이번 묶음에 실제 이자 사용 "+available,hits.Count==AliveEnemies().Count&&hits.All(x=>x.Damage==raw),raw+"/"+string.Join(",",hits.Select(x=>x.Damage)));
                    Check("AV·묶음 완료 "+available,Near(GameManager.Instance.ActionScheduler.ReferenceCyclesElapsed,0)&&SpecialAction.CurrentBatch==null);
                }
            }
            foreach(Unit ally in AliveAllies().Concat(AliveEnemies()))ally.ClearUltimateResource();PartyTreasury.BeginBattle();
            using(var log=new YvRecorder())
            {
                yield return NordAction(indra,ActionScheduler.ActionKind.Ultimate,_=>indra.FillUltimateResource());
                Check("미충전 공급자를 기다리지 않음",!log.Started.Any(x=>x.Unit==kubera&&x.Kind==ActionScheduler.ActionKind.Ultimate)&&log.Started.Any(x=>x.Unit==indra&&x.Kind==ActionScheduler.ActionKind.Ultimate)&&SpecialAction.CurrentBatch==null);
            }
            foreach(Unit ally in AliveAllies().Concat(AliveEnemies()))ally.ClearUltimateResource();PartyTreasury.BeginBattle();kubera.ControlStarts(new ControlContext(indra,1));
            using(var log=new YvRecorder())
            {
                yield return NordAction(indra,ActionScheduler.ActionKind.Ultimate,_=>{indra.FillUltimateResource();kubera.FillUltimateResource();});
                Check("제어된 공급자는 개시를 막지 않음",!log.Started.Any(x=>x.Unit==kubera)&&log.Started.Any(x=>x.Unit==indra&&x.Kind==ActionScheduler.ActionKind.Ultimate)&&SpecialAction.CurrentBatch==null);
            }
            kubera.ControlEnds();foreach(Unit ally in AliveAllies().Concat(AliveEnemies()))ally.ClearUltimateResource();PartyTreasury.BeginBattle();
            using(var log=new YvRecorder())
            {
                yield return NordAction(kubera,ActionScheduler.ActionKind.Ultimate,_=>kubera.FillUltimateResource());
                Check("개시자 미충전이면 독립 재보 초월",log.Started.Count(x=>x.Kind==ActionScheduler.ActionKind.Ultimate)==1&&!log.Started.Any(x=>x.Kind==ActionScheduler.ActionKind.Special)&&PartyTreasury.Available==1);
            }
            Check("일반 로카팔라의 예약 순서는 옵트인 없음",AliveAllies().Where(x=>x!=kubera).All(x=>!x.ActiveUltimateCode.PrecedesSpecialGate));
            OsFinish(enabled);
        }


        private IEnumerator ScenarioLokapalaDamage(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); _cfActionIdSequence=0; bool enabled=true;
            var spec=CampaignBuildCatalog().teams.Single(x=>x.aliases.Contains("검증 편성/"+config.lineup));
            config.lineup=spec.key; yield return EnsureLineup(config);
            yield return YvPrepareScene(config,v=>enabled=v);
            DebugMode.AllyInvincible=DebugMode.EnemyInvincible=false;
            foreach(Unit unit in AliveAllies().Concat(AliveEnemies()))
            {
                unit.ApplyRewardCodeInventory(Enumerable.Repeat(0,7).ToList(),new List<int>());
                unit.ClearUltimateResource();CfNoCrit(unit);
                YvAddTestStatus(unit,99896,"loka_damage_hp",new StatModifierEffect(StatType.MaxHp,200000),StatusCategory.Neutral);
                unit.RestoreHpOutsideCombat(unit.HpMax);
            }
            void Check(string name,bool ok,string actual="")=>YvCheck("로카팔라 화력 "+name,"실제 전달·행동·자원 일치",actual,ok);
            bool Primary(DamageContext hit,Unit source)=>hit.Attacker==source && !hit.IsDamageOverTime && !hit.HasTag(DamageTag.ElementalReaction) && !hit.HasTag(DamageTag.BonusDamage);
            IEnumerator Special(Unit unit)=>NordAction(unit,ActionScheduler.ActionKind.Special,scheduler=>scheduler.EnqueueSpecial(unit,"loka_damage_sp",unit.ActiveSpecialCode.CodeName,unit.CastSpecialCode));
            if(CfAlly(67) is Unit surya)
            {
                CfEnsureEffectHit(surya,AliveEnemies());var passive=surya.ActivePassiveCodes.OfType<Codes.Passive.SuryaExecution>().Single();
                foreach(Unit enemy in AliveEnemies())passive.Consume(enemy);
                int turn=surya.TurnCount,mana=surya.ManaCurr;
                using(var log=new YvRecorder())
                {
                    yield return Special(surya);
                    var hits=log.Incoming.Where(x=>Primary(x,surya)).ToList();
                    Check("수리야 미표식 전체 조준140%",hits.Count==AliveEnemies().Count && hits.All(x=>x.Damage==Mathf.RoundToInt(surya.StatValue(StatType.Attack)*1.4f) && !x.AppliesElement));
                    Check("조준 성공 표식1",AliveEnemies().All(x=>passive.Marks(x)==1));
                    Check("특수는 일반 턴·마나 미생성",turn==surya.TurnCount && mana==surya.ManaCurr);
                }
                using(var log=new YvRecorder())
                {
                    yield return Special(surya);
                    var hits=log.Incoming.Where(x=>Primary(x,surya)).ToList();
                    Check("표식 종언탄600%·방어25%무시",hits.Count==AliveEnemies().Count && hits.All(x=>x.Damage==Mathf.RoundToInt(surya.StatValue(StatType.Attack)*6f) && Near(x.DefenseStatMultiplier,.75f) && x.AppliesElement));
                    Check("특수는 표식 보존",AliveEnemies().All(x=>passive.Marks(x)==1));
                }
                Unit selected=null;
                using(var log=new YvRecorder())
                {
                    yield return NordAction(surya,ActionScheduler.ActionKind.Normal,sch=>sch.AdvanceAction(surya,1));
                    selected=log.IncomingTargets.First(x=>Primary(x.Context,surya)).Target;
                    Check("조준 후 일반 사격은2중첩",passive.Marks(selected)==2);
                }
                using(var log=new YvRecorder())
                {
                    yield return NordAction(surya,ActionScheduler.ActionKind.Normal,sch=>sch.AdvanceAction(surya,1));
                    var hit=log.Incoming.First(x=>Primary(x,surya));
                    Check("일반 종언탄600%·표식소모",hit.Damage==Mathf.RoundToInt(surya.StatValue(StatType.Attack)*6f) && hit.AppliesElement && passive.Marks(selected)==0);
                }
                using(var log=new YvRecorder())
                {
                    yield return Special(surya);
                    var hits=log.Incoming.Where(x=>Primary(x,surya)).ToList();
                    Check("혼합 표식별 조준·종언 분기",hits.Count(x=>x.AppliesElement)==AliveEnemies().Count-1 && hits.Count(x=>!x.AppliesElement)==1);
                }
                foreach(Unit enemy in AliveEnemies())passive.Consume(enemy);
                Unit warded=AliveEnemies().First();YvAddTestStatus(warded,99895,"surya_mark_ward",new NegativeStatusWardEffect());
                using(var log=new YvRecorder())
                {
                    yield return Special(surya);
                    Check("조준 표식은 저항을 준수",passive.Marks(warded)==0 && AliveEnemies().Where(x=>x!=warded).All(x=>passive.Marks(x)==1));
                    Check("표식 저항과 무관한 조준 피해",log.IncomingTargets.Any(x=>x.Target==warded && Primary(x.Context,surya) && x.Context.Damage>0 && !x.Context.AppliesElement));
                }
                passive.StopCode();Check("표식 구독·라운드 정리",AliveEnemies().All(x=>passive.Marks(x)==0));passive.CastCode();
            }
            if(CfAlly(65) is Unit kubera)
            {
                var inventory=GameManager.Instance.inventoryManager;int originalGold=inventory.Gold;
                inventory.RestoreGold(999999);Check("금력 상한4",Near(Codes.Special.KuberaGoldRush.GoldMultiplier(),4));
                PartyTreasury.BeginBattle();
                using(var log=new YvRecorder())
                {
                    yield return NordAction(kubera,ActionScheduler.ActionKind.Normal,sch=>sch.AdvanceAction(kubera,1));
                    var hit=log.Incoming.First(x=>Primary(x,kubera));
                    Check("일반 금력 적용·재보1",hit.Damage==Mathf.RoundToInt(Mathf.RoundToInt(kubera.StatValue(StatType.Attack)*1.8f)*4) && PartyTreasury.Available==1 && hit.IsTreasuryAttack);
                }
                int balance=PartyTreasury.Available;
                yield return kubera.ActiveNormalCode.PerformCommandedAttack(AliveEnemies().First(),CfAlly(63));
                Check("지휘 사격 재보 미생성",PartyTreasury.Available==balance);
                foreach(int available in new[]{0,1,2,3,5})
                {
                    PartyTreasury.BeginBattle();PartyTreasury.Charge(kubera,available);int spent=Math.Min(3,available);
                    using(var log=new YvRecorder())
                    {
                        yield return Special(kubera);
                        var hits=log.Incoming.Where(x=>Primary(x,kubera)).ToList();int raw=Mathf.RoundToInt(Mathf.RoundToInt(kubera.StatValue(StatType.Attack)*(2.8f+spent*1.4f))*4);
                        Check("골드 러쉬 단계 소비 "+available,hits.Count==AliveEnemies().Count && hits.All(x=>x.Damage==raw && x.IsTreasurySpendAttack==(spent>0)) && PartyTreasury.Available==available-spent,raw+"/"+PartyTreasury.Available);
                    }
                }
                PartyTreasury.BeginBattle();PartyTreasury.Charge(kubera,5);
                using(var reservation=PartyTreasury.Reserve(kubera))
                {
                    using(var log=new YvRecorder())
                    {
                        yield return Special(kubera);
                        Check("예약 재보는 소비 불가",PartyTreasury.Reserved==5 && PartyTreasury.Available==0 && log.Incoming.Where(x=>Primary(x,kubera)).All(x=>!x.IsTreasurySpendAttack));
                    }
                }
                Check("미소비 예약 반환",PartyTreasury.Available==5);inventory.RestoreGold(originalGold);
            }
            if(CfAlly(62) is Unit agni)
            {
                Unit varuna=CfAlly(66),indra=CfAlly(63);CfEnsureEffectHit(agni,AliveEnemies());CfEnsureEffectHit(varuna,AliveEnemies());
                var passive=agni.ActivePassiveCodes.OfType<Codes.Passive.AgniBurnToTheEnd>().Single();
                using(var log=new YvRecorder())
                {
                    yield return NordAction(varuna,ActionScheduler.ActionKind.Ultimate,_=>varuna.FillUltimateResource());
                    var hits=log.Incoming.Where(x=>Primary(x,agni)).ToList();
                    Check("타인 초월 완료 추공120%",hits.Count==AliveEnemies().Count && hits.All(x=>x.Damage==Mathf.RoundToInt(agni.StatValue(StatType.Attack)*1.2f)));
                    Check("추공은 실제 Additional",log.Finished.Count(x=>x.Unit==agni && x.Kind==ActionScheduler.ActionKind.Additional)==1 && !log.Finished.Any(x=>x.Unit==agni && x.Kind==ActionScheduler.ActionKind.Ultimate));
                }
                using(var log=new YvRecorder())
                {
                    yield return NordAction(varuna,ActionScheduler.ActionKind.Ultimate,_=>varuna.FillUltimateResource());
                    Check("같은 아군 중복 초월 제한",!log.Incoming.Any(x=>Primary(x,agni)));
                }
                using(var log=new YvRecorder())
                {
                    yield return NordAction(agni,ActionScheduler.ActionKind.Normal,sch=>sch.AdvanceAction(agni,1));
                    Check("일반130%·횟수초기화",log.Incoming.Any(x=>Primary(x,agni)&&x.Damage==Mathf.RoundToInt(agni.StatValue(StatType.Attack)*1.3f)) && !passive.HasUsed(varuna));
                }
                using(var log=new YvRecorder())
                {
                    yield return NordAction(indra,ActionScheduler.ActionKind.Ultimate,_=>indra.FillUltimateResource());
                    var hits=log.Incoming.Where(x=>Primary(x,agni)).ToList();int center=Mathf.RoundToInt(agni.StatValue(StatType.Attack)*5.4f),side=Mathf.RoundToInt(agni.StatValue(StatType.Attack)*1.2f);
                    Check("집중540%·나머지120%",hits.Count==AliveEnemies().Count && hits.Count(x=>x.Damage==center)==1 && hits.Count(x=>x.Damage==side)==AliveEnemies().Count-1,string.Join(",",hits.Select(x=>x.Damage)));
                    Check("집중 추공 1건·특수 피해 아님",log.Finished.Count(x=>x.Unit==agni && x.Kind==ActionScheduler.ActionKind.Additional)==1 && hits.All(x=>!x.HasTag(DamageTag.SpecialAttack)) && SpecialAction.CurrentBatch==null);
                }
                yield return NordAction(agni,ActionScheduler.ActionKind.Ultimate,_=>agni.FillUltimateResource());
                Check("자기 초월은 티켓 복제 없이 판돈",passive.StakesActive && !passive.HasUsed(varuna));
                using(var log=new YvRecorder())
                {
                    yield return NordAction(varuna,ActionScheduler.ActionKind.Ultimate,_=>varuna.FillUltimateResource());
                    var hits=log.Incoming.Where(x=>Primary(x,agni)).ToList();
                    Check("판돈 궁극 태그·실제 행동 유지",hits.Count==AliveEnemies().Count && hits.All(x=>x.HasTag(DamageTag.UltAttack)) && log.Finished.Count(x=>x.Unit==agni && x.Kind==ActionScheduler.ActionKind.Additional)==1);
                }
                passive.StopCode();
                using(var log=new YvRecorder())
                {
                    yield return NordAction(varuna,ActionScheduler.ActionKind.Ultimate,_=>varuna.FillUltimateResource());
                    Check("종료 후 추공·오라 정리",!log.Incoming.Any(x=>Primary(x,agni)) && !passive.StakesActive);
                }
            }
            OsFinish(enabled);
        }

        private IEnumerator ScenarioSoloSpecialRewards(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); _cfActionIdSequence = 0; bool enabled = true;
            yield return YvPrepareScene(config, value => enabled = value);
            Unit shi=CfAlly(2),enemy=AliveEnemies().First();
            void Check(string label,bool ok,string actual="")=>YvCheck("독립 특수행동 보상 "+label,"묶음과 독립 특수행동 구분",actual,ok);
            if(shi==null)
            {
                Unit yama=CfAlly(61),varuna=CfAlly(66);
                Check("감전 편성에는 개시자가 없음",!AliveAllies().Any(SpecialAction.CanOpen));
                Check("야마SP 슬롯이 있어도 개시자 없이 활성화 불가",CampaignCodeValue(yama,664)==1 && CampaignCodeValue(yama,839)==1 && CampaignCodeValue(yama,837)==1);
                Check("바루나는 개시자 없이 SP 지원 보상 제외",CampaignCodeValue(varuna,765)==1 && CampaignCodeValue(varuna,663)==1);
                Check("일반행동 원소 부착 코드는 허용",CampaignCodeValue(yama,766)>1 && CampaignCodeValue(varuna,768)>1);
                OsFinish(enabled);yield break;
            }
            foreach(Unit unit in AliveAllies().Concat(AliveEnemies()))
            {
                foreach(var code in unit.ActivePassiveCodes.ToList())code.StopCode();
                unit.ActiveUltimateCode?.StopCode();CfNoCrit(unit);
                unit.ApplyRewardCodeInventory(Enumerable.Repeat(0,7).ToList(),new List<int>());
                YvAddTestStatus(unit,99898,"solo_special_hp",new StatModifierEffect(StatType.MaxHp,200000),StatusCategory.Neutral);unit.RestoreHpOutsideCombat(unit.HpMax);
            }
            SpecialAction.Reset();
            Check("찬드라는 개시자 없이 SP 보상 선택 제외",CampaignCodeValue(CfAlly(60),664)==1 && CampaignCodeValue(CfAlly(60),765)==1);
            Check("시 독립 특수행동·개시자 없음",SpecialAction.CanUse(shi)&&!AliveAllies().Any(SpecialAction.CanOpen));
            Check("연계 전용 코드 자동 선택 제외",CampaignCodeValue(shi,837)==1);
            Check("독립 특수행동 피해 코드 허용",CampaignCodeValue(shi,839)>90);
            int Hit()
            {
                var hit=new DamageContext(shi,1000,CodeType.Normal,new List<int>{DamageTag.SpecialAttack,DamageTag.TrueDamage});enemy.TakeDamage(hit);return hit.ResolvedDamage;
            }
            void Equip(int id)
            {
                shi.ApplyRewardCodeInventory(new[]{id,0,0,0,0,0,0}.ToList(),new List<int>());
                if(id>0)YvCode(shi,id)?.CastCode();
            }
            int baseline=Hit();Equip(837);int linked=0;
            yield return NordAction(shi,ActionScheduler.ActionKind.Special,scheduler=>scheduler.EnqueueSpecial(shi,"solo_link_test","독립 특수행동",()=>linked=Hit()));
            Check("독립 행동은 연계 인원 보너스 없음",linked==baseline,baseline+"/"+linked);
            Equip(839);int demigod=0;
            yield return NordAction(shi,ActionScheduler.ActionKind.Special,scheduler=>scheduler.EnqueueSpecial(shi,"solo_demigod_test","독립 특수행동",()=>demigod=Hit()));
            Check("반신은 독립 특수행동40%",Mathf.Abs(demigod-Mathf.RoundToInt(baseline*1.4f))<=1,demigod.ToString());
            Equip(0);Check("독립 행동이 묶음을 만들지 않음",SpecialAction.CurrentBatch==null);
            OsFinish(enabled);
        }

        private IEnumerator ScenarioLokapalaBudget(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); _cfActionIdSequence = 0; bool enabled = true;
            string requested = config.lineup;
            var catalog = CampaignBuildCatalog();
            config.lineup = catalog.teams.Single(spec => spec.aliases.Contains("검증 편성/" + requested)).key;
            yield return EnsureLineup(config);
            bool rosterGate = GridManager.Instance.heroList.Any(SpecialAction.CanOpen);
            yield return YvPrepareScene(config, v => enabled = v);
            DebugMode.AllyInvincible = DebugMode.EnemyInvincible = false;
            Unit chandra = CfAlly(60), varuna = CfAlly(66), indra = CfAlly(63), kubera = CfAlly(65), surya = CfAlly(67);
            void Check(string label, bool ok, string actual = "") => YvCheck("로카팔라 역할 예산 " + label, "수정 사양", actual, ok);
            foreach (Unit ally in AliveAllies())
            { ally.ApplyRewardCodeInventory(Enumerable.Repeat(0,7).ToList(),new List<int>()); ally.ClearUltimateResource(); ally.RemoveShield(ally.ShieldCurr); CfNoCrit(ally); }
            CfStats(chandra,"찬드라",1800,160,58,3.8f,70,4.5f,155,11.5f,140,10,92,120);
            CfStats(varuna,"바루나",2000,180,82,5.4f,86,5.2f,165,12,125,9,102,100);
            CfStats(indra,"인드라",1050,92,98,6.5f,75,4.5f,65,4.8f,70,5.2f,108,100);
            if (kubera != null) CfStats(kubera,"쿠베라",1150,100,108,6.8f,65,4,75,5.5f,75,5.5f,100,120);
            if (surya != null) Check("수리야 속도98·공격/치확 유지", Near(surya.BaseStat(StatType.Speed),98) && Near(surya.BaseStat(StatType.Attack),128+8*(surya.Level-1)) && Near(surya.BaseStat(StatType.CritRate),.15f) && surya.ManaMax==140);
            if (CfAlly(62) is Unit agni)
            {
                Check("준비형SP는 특수 피해 코드 제외", CampaignCodeValue(agni,664)==1 && CampaignCodeValue(agni,839)==1 && CampaignCodeValue(agni,837)==1 && CampaignCodeValue(agni,765)>90);
                Check("준비 뒤 추공은 원소 부착 코드 허용", CampaignCodeValue(agni,766)>1 && CampaignCodeValue(agni,768)>1);
            }
            Check("처음 편성 로드 직후 개시 능력·지원 점수",rosterGate && CampaignCodeValue(indra,839)>90 && CampaignCodeValue(chandra,837)>90);
            var supportRoles=SynergyCatalog.UnitOf(60);
            Check("찬드라 유지력 역할 분류",supportRoles.roles.Contains("HEAL") && supportRoles.roles.Contains("AMP_DMG") && !supportRoles.roles.Contains("DPS_AOE"));
            Check("창야 독립 치유 표시",supportRoles.provides.Any(x=>x.key=="HEAL" && x.text.Contains("8%") && x.text.Contains("독립")));
            Check("토트 전체 피해감소24% 표시",SynergyCatalog.UnitOf(124).provides.Any(x=>x.key=="BULWARK" && x.text.Contains("24%") && x.text.Contains("아군")));
            var moon = chandra.ActivePassiveCodes.OfType<Codes.Passive.ChandraNishakara>().Single(); moon.CastCode();
            Check("초기 월광1",chandra.GetCombatResource(Codes.Passive.ChandraNishakara.ResourceId)==1);
            chandra.RestoreHpOutsideCombat(chandra.HpMax);
            chandra.TakeDamage(new DamageContext(AliveEnemies().First(),chandra.HpMax*3,CodeType.Normal,new List<int>{DamageTag.TrueDamage,DamageTag.NormalAttack}));
            Check("초기 월광으로 첫 치명상 방어",chandra.isActive && chandra.HpCurr==Mathf.RoundToInt(chandra.HpMax*.5f) && chandra.GetCombatResource(Codes.Passive.ChandraNishakara.ResourceId)==0,chandra.HpCurr.ToString());
            chandra.RestoreHpOutsideCombat(chandra.HpMax);
            foreach (Unit enemy in AliveEnemies())
            { YvAddTestStatus(enemy,99897,"loka_budget_hp",new StatModifierEffect(StatType.MaxHp,200000),StatusCategory.Neutral);enemy.RestoreHpOutsideCombat(enemy.HpMax); }
            CfEnsureEffectHit(varuna,AliveEnemies());
            int partyShield=Mathf.RoundToInt(varuna.StatValue(StatType.Defense)*1.6f),selfShield=Mathf.RoundToInt(varuna.StatValue(StatType.Defense)*.7f);
            Check("고유 보호막 능력 조회",varuna.ActiveUltimateCode.BaseShieldAmount==partyShield && varuna.ActiveSpecialCode.BaseShieldAmount==partyShield);
            yield return NordAction(varuna,ActionScheduler.ActionKind.Normal,scheduler=>scheduler.AdvanceAction(varuna,1));
            Check("물 부착 제방70%",varuna.ShieldCurr==selfShield,varuna.ShieldCurr.ToString());
            var before=AliveAllies().ToDictionary(u=>u,u=>u.ShieldCurr);
            yield return NordAction(varuna,ActionScheduler.ActionKind.Ultimate,_=>varuna.FillUltimateResource());
            Check("초월기 전후열 전원160%·제방 별도",AliveAllies().All(u=>u.ShieldCurr-before[u]==partyShield+(u==varuna?selfShield:0)));
            before=AliveAllies().ToDictionary(u=>u,u=>u.ShieldCurr);
            yield return NordAction(varuna,ActionScheduler.ActionKind.Special,scheduler=>scheduler.EnqueueSpecial(varuna,"loka_budget_sp",varuna.ActiveSpecialCode.CodeName,varuna.CastSpecialCode));
            Check("특수 전후열 전원160%·추가 제방 없음",AliveAllies().All(u=>u.ShieldCurr-before[u]==partyShield));
            if(kubera!=null)
            {
                foreach(int amount in new[]{0,3})
                {
                    PartyTreasury.BeginBattle();PartyTreasury.Charge(kubera,amount);
                    float power=Codes.Special.KuberaGoldRush.GoldMultiplier();
                    int expected=Mathf.RoundToInt(Mathf.RoundToInt(kubera.StatValue(StatType.Attack)*(amount==3?7f:2.8f))*power);
                    using(var log=new YvRecorder())
                    {
                        yield return NordAction(kubera,ActionScheduler.ActionKind.Special,scheduler=>scheduler.EnqueueSpecial(kubera,"loka_budget_gold",kubera.ActiveSpecialCode.CodeName,kubera.CastSpecialCode));
                        var hits=log.Dealt.Where(h=>h.Attacker==kubera && h.DamageContext.HasTag(DamageTag.SpecialAttack)).ToList();
                        Check("골드 러쉬 계수·실제 재보 소비 "+amount,hits.Count>0 && hits.All(h=>h.DamageContext.Damage==expected) && PartyTreasury.Available==0,expected+"/"+string.Join(",",hits.Select(h=>h.DamageContext.Damage)));
                    }
                }
            }
            Check("전후열 배치 변경 없음",varuna.currentCell?.xPos==GridManager.Instance.GetFrontColumn(false) && chandra.currentCell?.xPos==GridManager.Instance.GetFrontColumn(false));
            OsFinish(enabled);
        }

        private IEnumerator ScenarioSpecialRewardsCore(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); _cfActionIdSequence = 0; bool enabled = true;
            yield return YvPrepareScene(config, v => enabled = v);
            DebugMode.AllyInvincible = DebugMode.EnemyInvincible = false;
            Unit indra = CfAlly(63), yama = CfAlly(61), varuna = CfAlly(66), chandra = CfAlly(60), enemy = AliveEnemies().First();
            void Check(string label, bool ok, string actual = "") => YvCheck("특수·원소 보상 " + label, "사양", actual, ok);
            foreach (Unit unit in AliveAllies().Concat(AliveEnemies()))
            {
                foreach (var code in unit.ActivePassiveCodes.ToList()) code.StopCode();
                unit.ActiveUltimateCode?.StopCode(); CfNoCrit(unit); unit.RemoveShield(unit.ShieldCurr);
                unit.ApplyRewardCodeInventory(Enumerable.Repeat(0, 7).ToList(), new List<int>());
                YvAddTestStatus(unit, 99893, "special_test_hp", new StatModifierEffect(StatType.MaxHp, 200000), StatusCategory.Neutral);
                unit.RestoreHpOutsideCombat(unit.HpMax); unit.ClearUltimateResource();
            }
            void Equip(Unit unit, params int[] ids)
            {
                foreach (var p in unit.ActivePassiveCodes.ToList()) p.StopCode();
                unit.ApplyRewardCodeInventory(ids.Concat(Enumerable.Repeat(0, 7 - ids.Length)).ToList(), new List<int>());
                foreach (int id in ids) YvCode(unit, id)?.CastCode();
            }
            int Hit(Unit actor, bool special = false, bool element = false, bool dot = false)
            {
                var tags = new List<int> { DamageTag.TrueDamage, special ? DamageTag.SpecialAttack : DamageTag.NormalAttack };
                var hit = new DamageContext(actor, 1000, special ? CodeType.Special : CodeType.Normal, tags)
                    { AppliesElement = element, IsDamageOverTime = dot };
                enemy.TakeDamage(hit); return hit.ResolvedDamage;
            }
            var pool = RewardManager.Instance.BuildCodePool();
            Check("데이터 DTO·후보 조건 연결", RewardCodeCatalog.Find(917).requiresSpecialGate && RewardCodeCatalog.Find(765).requiresSpecialAction && !RewardCodeCatalog.Find(766).requiresSpecialAction);
            Check("전체 코드 수210", RewardCodeCatalog.All().Count()==210);
            foreach (int id in SpecialElementRewardCode.Ids)
                Check("등록·추첨·상점 " + id, RewardCodeFactory.IsImplemented(id) && RewardCodeCatalog.Find(id).shopFixed && pool.Any(r => r.codeId == id));
            Check("개시·참여 능력 분리", SpecialAction.CanOpen(indra) && !SpecialAction.CanOpen(yama) && SpecialAction.CanUse(yama));
            Check("상위 사다리", RewardCodeCatalog.IsUpperOf(839, 664));
            Equip(yama, 664); Check("황금 특수20%·일반 제외", Hit(yama, true) == 1200 && Hit(yama) == 1000);
            Equip(yama, 839); Check("반신 특수40%", Hit(yama, true) == 1400);
            Equip(indra, 838); Equip(varuna, 838);
            Check("번개 고유 원소만 금강저", Hit(indra, true) == 1350 && Hit(varuna, true) == 1000);
            Equip(yama, 766); Check("원소 시도 본타18%·비부착·지딜 제외", Hit(yama, element:true) == 1180 && Hit(yama) == 1000 && Hit(yama, element:true, dot:true) == 1000);
            Equip(yama, 665, 768); CfEnsureEffectHit(yama, AliveEnemies()); yama.ModifyHp(yama.HpMax / 2); int hp = yama.HpCurr;
            yama.ClearUltimateResource(); enemy.GrantCombatElement(UnitElement.Electro, source:yama);
            int healed = yama.HpCurr - hp, mana = yama.ManaCurr;
            enemy.GrantCombatElement(UnitElement.Electro, source:yama);
            Check("부착 회복·마나 및 다중 부착 공유 대기", healed == Mathf.RoundToInt(yama.HpMax * .03f) && mana == 6 && yama.ManaCurr == mana && yama.HpCurr - hp == healed, healed + "/" + mana);
            Equip(yama); enemy.GrantCombatElement(UnitElement.Electro, source:yama);
            Check("코드 해제 구독 정리", yama.ManaCurr == mana && yama.HpCurr - hp == healed);
            Equip(indra, 666, 767); int initialMana = indra.ManaCurr, initialShield = chandra.ShieldCurr;
            Check("개전 마나25%·전원 보호막", initialMana == Mathf.RoundToInt(indra.ManaMax * .25f) && initialShield == Mathf.RoundToInt(indra.StatValue(StatType.Defense)) * 2, initialMana + "/" + initialShield);
            YvCode(indra, 666).CastCode(); YvCode(indra, 767).CastCode();
            Check("개전 재시전 중복 방지", indra.ManaCurr == initialMana && chandra.ShieldCurr == initialShield);
            Equip(indra); Equip(yama, 765, 663, 513);
            foreach (Unit ally in AliveAllies()) { ally.RemoveShield(ally.ShieldCurr); ally.ModifyHp(ally.HpMax / 2); }
            hp = chandra.HpCurr;
            yield return NordAction(yama, ActionScheduler.ActionKind.Special, s => s.EnqueueSpecial(yama, "special_reward_test", "특수 보상", () => { }));
            int shield = chandra.ShieldCurr;
            Check("특수 회복·방어력 보호막·망루", chandra.HpCurr - hp == Mathf.RoundToInt(chandra.HpMax * .04f) && shield > 0 && yama.ShieldCurr > shield);
            var bad = BuffStatus.Create(99894,"special_bad","저항 검사",enemy,chandra,new StatModifierEffect(StatType.Defense,-.1f,true),duration:2,category:StatusCategory.Negative);
            bad.SkipEffectHitRoll = true; chandra.AddStatus(bad);
            bool resisted = !bad.ApplicationSucceeded;
            var bad2 = BuffStatus.Create(99894,"special_bad","저항 검사",enemy,chandra,new StatModifierEffect(StatType.Defense,-.1f,true),duration:2,category:StatusCategory.Negative);
            bad2.SkipEffectHitRoll = true; chandra.AddStatus(bad2);
            Check("디버프 확정 저항은 한 번", resisted && bad2.ApplicationSucceeded);
            YvAddTestStatus(chandra, 99895, "ward_a", new NegativeStatusWardEffect());
            YvAddTestStatus(chandra, 99896, "ward_b", new NegativeStatusWardEffect());
            var wardResults = new List<bool>();
            for (int n = 0; n < 3; n++)
            {
                var wardBad = BuffStatus.Create(99894,"special_bad","저항 중첩",enemy,chandra,new StatModifierEffect(StatType.Defense,-.1f,true),duration:2,category:StatusCategory.Negative);
                wardBad.SkipEffectHitRoll = true; chandra.AddStatus(wardBad); wardResults.Add(wardBad.ApplicationSucceeded);
            }
            Check("확정 저항 두 출처를 한 번에 소비하지 않음", !wardResults[0] && !wardResults[1] && wardResults[2]);
            hp = chandra.HpCurr;
            yield return NordAction(yama, ActionScheduler.ActionKind.Special, s => s.EnqueueSpecial(yama, "special_reward_again", "대기 검사", () => { }));
            Check("특수 연속 발동 2턴 대기", chandra.ShieldCurr == shield && chandra.HpCurr == hp);
            yama.BeginTurn();
            yield return NordAction(yama, ActionScheduler.ActionKind.Special, s => s.EnqueueSpecial(yama, "special_reward_turn1", "1턴 대기", () => { }));
            Check("보유자1턴 경과 대기 유지", chandra.HpCurr == hp && chandra.ShieldCurr == shield);
            yama.BeginTurn();
            yield return NordAction(yama, ActionScheduler.ActionKind.Special, s => s.EnqueueSpecial(yama, "special_reward_turn2", "2턴 완료", () => { }));
            Check("보유자2턴 경과 재발동", chandra.HpCurr > hp && chandra.ShieldCurr > shield);
            Equip(yama); Equip(indra, 763, 764, 917); Equip(varuna, 837);
            foreach (Unit ally in AliveAllies()) { ally.ModifyHp(ally.HpMax / 2); ally.RemoveShield(ally.ShieldCurr); }
            hp = chandra.HpCurr; bool opened = false, finished = false; int count = 0, openerDamage = 0, participantDamage = 0;
            void Open(SpecialAction.Batch batch)
            {
                opened = batch.Opener == indra; count = batch.Count;
                openerDamage = Hit(indra, true); participantDamage = Hit(varuna, true);
                var defenseProbe = new DamageContext(indra, 100, CodeType.Special, new List<int> { DamageTag.SpecialAttack });
                float defenseScale = indra.CombatEffects().Aggregate(1f, (value, effect) => value * effect.DefenseStatMultiplierModifier(indra, enemy, defenseProbe));
                Check("5인 개시자 방어력20% 무시", Mathf.Abs(defenseScale - .8f) < .001f);
                Check("중복 개시 차단", SpecialAction.OpenGate(indra) == 0);
                Check("개시 회복·정화", chandra.HpCurr - hp == Mathf.RoundToInt(chandra.HpMax * .12f) && !chandra.HasStatusKey("special_bad"));
            }
            void End(SpecialAction.Batch batch) { finished = batch.Opener == indra; }
            SpecialAction.Opened += Open; SpecialAction.Finished += End;
            yield return CfRun(s => SpecialAction.OpenGate(indra), () => finished, 40f);
            SpecialAction.Opened -= Open; SpecialAction.Finished -= End;
            Check("실제 연계 개시·끝·인원 계수", opened && finished && count == 5 && openerDamage == 1400 && participantDamage == 1400, count + "/" + openerDamage + "/" + participantDamage);
            Check("연계 종료 회복·문맥 정리", chandra.HpCurr - hp >= Mathf.RoundToInt(chandra.HpMax * .20f) && SpecialAction.CurrentBatch == null, (chandra.HpCurr-hp).ToString());
            int outsideIndra = Hit(indra, true), outsideVaruna = Hit(varuna, true);
            Equip(indra); Equip(varuna);
            Check("묶음 밖 개시자 강화 소멸", outsideIndra == Hit(indra, true) && outsideVaruna == Hit(varuna, true), outsideIndra + "/" + outsideVaruna);
            Equip(indra, 763, 764, 917);
            hp = chandra.HpCurr; SpecialAction.OpenGate(indra); int afterOpen = chandra.HpCurr;
            SpecialAction.Reset(); Check("취소 정리로 종료 회복 금지", chandra.HpCurr == afterOpen && afterOpen >= hp);
            var saved = new UnitSaveData(); indra.WriteRunState(saved); string instance = indra.ActiveRewardInstance(763).instanceId;
            Equip(indra); indra.RestoreRunState(JsonUtility.FromJson<UnitSaveData>(JsonUtility.ToJson(saved)));
            Check("신규 코드 인스턴스 저장 복원", indra.ActiveRewardInstance(763)?.instanceId == instance);
            foreach (Unit ally in AliveAllies()) ally.Invoke(UnitEventType.OnRoundEnd, new EventContext(ally));
            Check("전투 종료 묶음·효과 정리", SpecialAction.CurrentBatch == null);
            OsFinish(enabled);
        }
    }
}
#endif


