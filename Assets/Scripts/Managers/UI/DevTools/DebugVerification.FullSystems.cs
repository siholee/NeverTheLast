#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using Core;
using Effects.Base;
using Effects.Buffs;
using Effects.Negative;
using Entities;
using Helpers;
using UnityEngine;
using YamlDotNet.Serialization;
using static BaseClasses.BaseEnums;

namespace Managers.UI.DevTools
{
    public sealed partial class DebugVerification
    {
        // 공격의 구현 여부와 확률 판정을 분리하는 검증 대상 전용 효과. 제품 코드에는 적용하지 않는다.
        private sealed class UnavoidableEffect : BaseEffect
        {
            public UnavoidableEffect() : base(0) { }
            public override float EvasionChanceAdditiveModifier(Unit unit, DamageContext context) => -100f;
        }
        private void MakeUnavoidable(Unit unit) => unit.AddStatus(BuffStatus.Create(
            999901,"verification_no_evasion","검증: 회피 제외",unit,unit,new UnavoidableEffect()));

        private static void SeedNextRandomBelow(float chance)
        {
            for(int seed=0;seed<10000;seed++)
            {
                UnityEngine.Random.InitState(seed);
                if(UnityEngine.Random.value >= chance) continue;
                UnityEngine.Random.InitState(seed);
                return;
            }
            throw new InvalidOperationException("Cannot prepare deterministic random sample");
        }
        private void Near(string name,float expected,float actual,float tolerance=.001f) =>
            Assert(name,Mathf.Abs(expected-actual)<=tolerance,expected.ToString(),actual.ToString());
        private static T ReadField<T>(object owner,string name) => (T)owner.GetType()
            .GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);

        private IEnumerator FullSystems()
        {
            Status="전체 데이터/스케줄러/훈련/경제/저장 검증";
            yield return AllData();
            SchedulerRules();
            StatsAndEffects();
            TrainingEquipmentEconomy();
            SelectionAndSave();
            PeriodicCodes();
            GridAndEffects();
            yield return RepeatedRounds();
            yield return SpeedReplay();
        }

        private IEnumerator AllData()
        {
            var parser=new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
            foreach(var asset in Resources.LoadAll<TextAsset>("Data"))
            {
                bool parsed=true; string error="parsed";
                try { parser.Deserialize<object>(asset.text); }
                catch(Exception ex) { parsed=false; error=ex.Message; }
                Assert("YAML "+asset.name,parsed,"parses",error);
            }
            var heroes=game.unitDataList.units;
            var enemies=game.dataManager.FetchEnemyDataList().enemies;
            Equal("unique hero IDs",heroes.Count,heroes.Select(x=>x.id).Distinct().Count());
            Equal("unique enemy IDs",enemies.Count,enemies.Select(x=>x.id).Distinct().Count());
            foreach(var def in heroes)
            {
                Clear();
                var unit=grid.SpawnUnit(-1,1,false,def.id);
                VerifyUnitDefinition(unit,def.id,def.portrait,def.standing,def.codes,def.levelPassives);
                Equal(def.id+" normal ID=owner",def.id,def.codes["normal"]);
                Equal(def.id+" ultimate ID=owner",def.id,def.codes["ultimate"]);
                Equal(def.id+" unique passive ID=owner+200",def.id+200,def.codes["passive"]);
                yield return null;
            }
            foreach(var def in enemies)
            {
                Clear();
                var unit=SpawnEnemy(def.id,1);
                VerifyUnitDefinition(unit,def.id,def.portrait,def.standing,def.codes,def.levelPassives);
                yield return null;
            }
            Clear();
            var caster=SpawnHero();
            var catalog=parser.Deserialize<Dictionary<string,Dictionary<string,List<CatalogEntry>>>>(
                Resources.Load<TextAsset>("Data/20_codes").text)["codes"];
            foreach(var slot in catalog)
            {
                Equal("catalog unique IDs "+slot.Key,slot.Value.Count,slot.Value.Select(x=>x.id).Distinct().Count());
                foreach(var entry in slot.Value)
                {
                    object code=slot.Key switch
                    {
                        "passive"=>CodeFactory.CreatePassiveCode(entry.id,new PassiveCodeContext{Caster=caster}),
                        "normal"=>CodeFactory.CreateNormalCode(entry.id,new NormalCodeContext{Caster=caster}),
                        "ultimate"=>CodeFactory.CreateUltimateCode(entry.id,new UltimateCodeContext{Caster=caster}),
                        _=>null
                    };
                    Assert("catalog factory "+slot.Key+" "+entry.id,code!=null,"implementation",code?.GetType().Name);
                }
            }
        }
        public sealed class CatalogEntry { public int id; public string verbalName; public string codeName; public string description; }
        private void VerifyUnitDefinition(Unit unit,int id,string portrait,string standing,Dictionary<string,int> codes,List<LevelPassiveData> passives)
        {
            Assert("unit "+id+" instantiated",unit!=null,"Unit",unit?.UnitName);
            if(unit==null)return;
            Assert("unit "+id+" portrait",SpriteResource.LoadPortrait(portrait)!=null,"Sprite",portrait);
            Assert("unit "+id+" standing",SpriteResource.LoadStanding(standing)!=null,"Sprite",standing);
            // 자료실은 데이터가 아니라 살아 있는 Unit을 그린다. 데이터가 멀쩡해도
            // 스폰된 유닛의 PortraitPath나 ID가 어긋나면 화면에서만 비어 보인다.
            Assert("unit "+id+" spawned ID",unit.ID==id,id.ToString(),unit.ID.ToString());
            Assert("unit "+id+" spawned portrait path",
                SpriteResource.LoadPortrait(unit.PortraitPath)!=null,"Sprite",unit.PortraitPath);
            Assert("unit "+id+" synergy lookup",
                Managers.SynergyCatalog.UnitOf(unit.ID)!=null,"profile",
                Managers.SynergyCatalog.NameOf(unit.ID));
            Assert("unit "+id+" normal/ultimate",unit.ActiveNormalCode!=null&&unit.ActiveUltimateCode!=null,"both",unit.ActiveNormalCode?.GetType().Name+"/"+unit.ActiveUltimateCode?.GetType().Name);
            foreach(int level in new[]{1,30,60,90,1})
            {
                unit.DebugSetLevel(level);
                var expected=new HashSet<int>((passives??new()).Where(p=>p.unlockLevel<=level).Select(p=>p.codeId)){codes["passive"]};
                var actual=new HashSet<int>(unit.LearnedPassiveRecords.Select(p=>p.codeId));
                Assert("unit "+id+" unlocks "+level,expected.SetEquals(actual),string.Join(",",expected),string.Join(",",actual));
            }
        }

        private void SchedulerRules()
        {
            Clear();
            var hero=SpawnHero();var enemy=SpawnEnemy(1062,1);
            var scheduler=new ActionScheduler();scheduler.BeginRound();
            Near("speed formula",100*hero.ActionSpeedCurr,ActionScheduler.SpeedOf(hero));
            Near("AV formula",10000/ActionScheduler.SpeedOf(hero),ActionScheduler.FullActionValue(hero));
            var order=new List<string>();
            Assert("queue accepts additional",scheduler.EnqueueAdditional(hero,"same","test",()=>order.Add("additional")),"true","enqueue");
            Assert("queue rejects duplicate",!scheduler.EnqueueAdditional(hero,"same","test",()=>order.Add("duplicate")),"false","enqueue");
            scheduler.EnqueuePriorityAdditional(hero,"same","test",()=>order.Add("passive"));
            scheduler.Tick(0);scheduler.Tick(0);
            Assert("passive before additional",order.SequenceEqual(new[]{"passive","additional"}),"passive,additional",string.Join(",",order));
            scheduler.EndRound();scheduler.BeginRound();
            var av=ReadField<Dictionary<Unit,float>>(scheduler,"_actionValues");
            float heroAV=av[hero],enemyAV=av[enemy],elapsed=Mathf.Min(heroAV,enemyAV);
            var fast=heroAV<=enemyAV?hero:enemy;var slow=fast==hero?enemy:hero;
            float slowBefore=av[slow];
            scheduler.Tick(0);
            Assert("lower AV acts first",scheduler.TurnOwner==fast,fast.UnitName,scheduler.TurnOwner?.UnitName);
            Near("all AV reduced by next arrival",slowBefore-elapsed,av[slow]);
            Equal("opening turn count",1,scheduler.TurnsTaken);
            scheduler.EndRound();scheduler.BeginRound();
            hero.FillUltimateResource(false);float before=av.ContainsKey(hero)?av[hero]:0;
            scheduler.Tick(0);
            Equal("ultimate consumes no turn",0,scheduler.TurnsTaken);
            Near("ultimate preserves AV",before,av[hero]);
            hero.DebugResetCombatState();scheduler.EndRound();scheduler.BeginRound();
            hero.isControlled=true;
            bool ran=false;scheduler.EnqueueAdditional(hero,"controlled","test",()=>ran=true);scheduler.Tick(0);
            Assert("controlled reservation discarded",!ran,"not run",ran.ToString());
            hero.isControlled=false;hero.DebugResetCombatState();scheduler.EndRound();scheduler.BeginRound();
            scheduler.EnqueueAdditional(hero,"watchdog","test",()=>hero.isCasting=true);scheduler.Tick(0);scheduler.Tick(8.1f);
            Assert("8 second watchdog releases casting",!hero.isCasting,"released",hero.isCasting.ToString());
            scheduler.EndRound();
            var cooldown=new TurnCooldown(2);RoundStart(hero);
            Assert("turn cooldown first ready",cooldown.TryUse(hero),"ready","first");
            Assert("turn cooldown same turn blocked",!cooldown.IsReady(hero),"blocked","same turn");
            hero.BeginTurn();hero.EndTurn();Assert("turn cooldown one turn blocked",!cooldown.IsReady(hero),"blocked","one turn");
            hero.BeginTurn();hero.EndTurn();Assert("turn cooldown two turns ready",cooldown.IsReady(hero),"ready","two turns");
            var probe=new PeriodProbe(new PassiveCodeContext{Caster=hero});
            for(int round=0;round<12;round++)
            {
                hero.DebugResetCombatState();probe.CastCode();int initial=probe.Fires;
                for(int turn=0;turn<4;turn++){hero.BeginTurn();hero.EndTurn();}
                Equal("periodic round "+round+" two fires",2,probe.Fires-initial);
                hero.DebugResetCombatState();int stopped=probe.Fires;hero.BeginTurn();hero.EndTurn();
                Equal("periodic round "+round+" cleanup",stopped,probe.Fires);
            }
        }
        private sealed class PeriodProbe : PeriodicTurnPassive
        {
            public int Fires;
            public PeriodProbe(PassiveCodeContext c):base(c,2,true) { }
            protected override void OnPeriodElapsed()=>Fires++;
        }

        private void StatsAndEffects()
        {
            Clear();var hero=SpawnHero();var enemy=SpawnEnemy(1062,90);
            foreach(var u in new[]{hero,enemy})
            {
                Equal(u.ID+" CON HP formula",u.GetBaseCon()*100,u.HpMax);
                Near(u.ID+" STR defense formula",u.GetBaseStr(),u.DefCurr);
                Near(u.ID+" skill power formula",50*u.GetBaseInt()*.2f,u.SkillDamage(50,PrimaryStat.INT),1f);
            }
            hero.ModifyHp(hero.HpMax/2);float ratio=(float)hero.HpCurr/hero.HpMax;
            hero.AddStatus(BuffStatus.Create(999902,"verify_con","test",hero,hero,new PrimaryStatMultiplierEffect(2,PrimaryStat.CON)));
            Near("attributes preserve HP ratio",ratio,(float)hero.HpCurr/hero.HpMax,.0001f);
            hero.DebugResetCombatState();
            var origin=new VoidDragonOrigin(new PassiveCodeContext{Caster=enemy});origin.CastCode();
            Assert("persistent aura applies on registration",enemy.ActiveStatuses.Any(s=>s.Key=="void_dragon_origin"),"status before first turn",string.Join(",",enemy.ActiveStatuses.Select(s=>s.Key)));
            origin.StopCode();
            SeedNextRandomBelow(EffectContest.ConHitChance(hero,enemy));
            ElementalReaction.TryApplyBurn(hero,enemy);
            int hp=enemy.HpCurr;enemy.BeginTurn();enemy.EndTurn();
            Equal("burn tick 5% maximum HP",Mathf.RoundToInt(enemy.HpMax*.05f),hp-enemy.HpCurr);
            enemy.DebugResetCombatState();
            ControlStatuses.ApplyFixedStun(enemy,hero,2);enemy.RemoveStatusByKey(ControlStatuses.StunKey);
            for(int i=0;i<3;i++){enemy.BeginTurn();enemy.EndTurn();}
            Equal("control decay after three free turns",2,ControlStatuses.DiminishedTurns(enemy,2));
            RoundStart(enemy);Equal("control round start reset",0,enemy.ControlAppliedCount);
        }

        private void TrainingEquipmentEconomy()
        {
            Clear();CharacterSelectionManager.Instance?.ClearLineup();var hero=SpawnHero();
            foreach(var focus in Enum.GetValues(typeof(PrimaryStat)).Cast<PrimaryStat>())
            {
                TrainingManager.State.Reset();int before=hero.GetGrowthStatValue(focus);int level=hero.TrainingLevel;
                int expected=TrainingManager.GetProjectedGain(focus);
                var result=TrainingManager.ApplyTraining(focus);
                Assert("training "+focus+" succeeds full energy",!result.Failed,"success",result.Failed.ToString());
                Equal("training "+focus+" preview matches",expected,result.StatGain);
                Equal("training "+focus+" level gain",level+1,hero.TrainingLevel);
                Assert("training "+focus+" stat increases",hero.GetGrowthStatValue(focus)>before,">"+before,hero.GetGrowthStatValue(focus).ToString());
                Equal("training "+focus+" energy",Mathf.Clamp(100-TrainingManager.GetEnergyCost(focus),0,100),TrainingManager.State.Energy);
            }
            TrainingManager.State.SpendEnergy(100);Equal("INT training cannot fail",0,TrainingManager.GetFailureRate(PrimaryStat.INT));
            Equal("empty energy failure chance",66,TrainingManager.GetFailureRate(PrimaryStat.STR));
            var inventory=game.inventoryManager;int gold=inventory.Gold;
            inventory.AddGold(10);Equal("gold addition",gold+10,inventory.Gold);
            Assert("gold spend succeeds",inventory.TrySpendGold(10),"true","spent");Equal("gold round trip",gold,inventory.Gold);
            Assert("gold overspend rejected",!inventory.TrySpendGold(gold+1),"false","attempt");
            inventory.AddToken(1,3);int tokens=inventory.TokensInHand[1];
            Assert("token spend succeeds",inventory.SpendToken(new Dictionary<int,int>{{1,2}}),"true","spent");
            Equal("token balance",tokens-2,inventory.TokensInHand[1]);
            Assert("token overspend rejected",!inventory.SpendToken(new Dictionary<int,int>{{1,tokens+1}}),"false","attempt");
            foreach(int id in hero.EquippedItemIds.ToList())hero.TryUnequip(id,false);
            foreach(var item in game.itemDataList.items)
            {
                bool equipped=hero.TryEquipItem(item.id,out string reason);
                Assert("equipment "+item.id+" valid slot",equipped,"equippable",reason);
                if(!equipped)continue;
                bool usable=hero.CanUseEquipmentEffects(item);
                Assert("equipment "+item.id+" proficiency effects",usable==(item.RequiredProficiency==EquipmentProficiency.None||hero.HasProficiency(item.RequiredProficiency)),"matches proficiency",usable.ToString());
                int expected=(item.codeGrants??new()).Count(g=>g.slot=="passive"&&usable);
                Equal("equipment "+item.id+" passive grants",expected,hero.ActiveItemPassiveCodes.Count);
                Assert("equipment "+item.id+" unequip",hero.TryUnequip(item.id,false),"true","removed");
                Equal("equipment "+item.id+" grants removed",0,hero.ActiveItemPassiveCodes.Count);
            }
            var heavy=game.itemDataList.items.OrderByDescending(i=>i.weight).First();hero.DebugSetLevel(1);
            for(int i=0;i<100&& !hero.IsOverCarryWeightMax;i++)hero.TryStoreItem(heavy.id,out _);
            Assert("weight overcap detected",hero.IsOverCarryWeightMax,"over cap",hero.CarryWeightCurrent+"/"+hero.CarryWeightMax);
            Equal("weight tier 2",2,hero.EncumbranceTier);
        }

        private void SelectionAndSave()
        {
            Clear();var selection=CharacterSelectionManager.Instance;
            if(selection==null)selection=new GameObject("CharacterSelectionManager").AddComponent<CharacterSelectionManager>();
            selection.ClearLineup();
            var defs=game.unitDataList.units;
            var main=defs.First(d=>d.canStartAsMain&&d.characterType=="Starter");
            Assert("support requires main",!selection.AddSupportHero(main.id),"reject","attempt");
            Assert("main selection",selection.AddMainHero(main.id),"accept",main.name);
            Assert("second main rejected",!selection.AddMainHero(main.id),"reject","attempt");
            Assert("duplicate rejected",!selection.AddHero(main.id),"reject","attempt");
            foreach(var def in defs.Where(d=>d.id!=main.id&&d.canStartAsSupport))
            { if(selection.Lineup.Count>=5)break;selection.AddSupportHero(def.id); }
            Equal("party capacity",5,selection.Lineup.Count);
            Assert("sixth party member rejected",!selection.AddHero(main.id),"reject","attempt");
            selection.ClearLineup();
            var hero=SpawnHero();game.DebugLoadStage(31);
            game.runManager.MarkEventTriggered("verification_event");
            var save=game.runManager.CaptureDebugSnapshot();
            SaveSystem.SaveRun(save);var loaded=SaveSystem.LoadRun();
            Assert("save JSON round trip",JsonUtility.ToJson(save)==JsonUtility.ToJson(loaded),"identical","compared");
            game.DebugLoadStage(32);game.runManager.RestoreDebugSnapshot(loaded);
            Equal("restore stage",31,game.RoundManager.Stage);
            Equal("restore party count",save.heroUnits.Count,Heroes.Count);
            Assert("restore event history",game.runManager.HasTriggeredEvent("verification_event"),"true","history");
            SaveSystem.SaveRun(new RunSaveData{version=RunSaveData.CurrentVersion-1});
            Assert("old save clean rejection",SaveSystem.LoadRun()==null&&!SaveSystem.HasSave(),"null and no save","checked");
            SaveSystem.SaveRun(save);
            int persistent=PlayerPrefs.GetInt("NTL_BossDefeated_987654",0);
            SaveSystem.MarkBossDefeated(987654);
            Assert("boss record sandbox",SaveSystem.HasDefeatedBoss(987654)&&PlayerPrefs.GetInt("NTL_BossDefeated_987654",0)==persistent,"memory only","compared");
            int stage=game.RoundManager.Stage;int tickets=game.inventoryManager.rerollTicketCount;
            game.rewardManager.ApplyReward(new RewardDef{rerollTicketBonus=2});
            Equal("reward tickets",tickets+2,game.inventoryManager.rerollTicketCount);
            Equal("reward advances exactly one stage",stage+1,game.RoundManager.Stage);
            game.DebugLoadStage(31);int life=game.life;game.StartRound();game.DebugEndBattle(false);
            Assert("battle defeat loses life",game.life<life,"<"+life,game.life.ToString());
        }

        private IEnumerator RepeatedRounds()
        {
            for(int repeat=0;repeat<12;repeat++)
            {
                Clear();SpawnHero();DebugMode.ForcedThemeId=8;game.DebugLoadStage(31);game.StartRound();
                game.DebugEndBattle(true);
                Equal("repeat battle "+repeat+" restores party",1,Heroes.Count);
                game.DebugLoadStage(31);
                Assert("repeat battle "+repeat+" scheduler clean",game.ActionScheduler.ActingUnit==null,"clear","checked");
                yield return null;
            }
        }

        private static void Turns(Unit unit,int count)
        { for(int i=0;i<count;i++){unit.BeginTurn();unit.EndTurn();} }

        private void PeriodicCodes()
        {
            Clear();var unit=SpawnEnemy(1062,60);var source=SpawnHero();
            var context=new PassiveCodeContext{Caster=unit};
            for(int round=0;round<2;round++)
            {
                unit.DebugResetCombatState();var block=new Block(context);block.CastCode();
                Turns(unit,1);Equal("block no shield before period "+round,0,unit.ShieldCurr);
                SeedNextRandomBelow(.05f);Turns(unit,1);Assert("block at turn two "+round,unit.ShieldCurr>0,"shield",unit.ShieldCurr.ToString());block.StopCode();
                unit.DebugResetCombatState();unit.ModifyHp(unit.HpMax/2);int hp=unit.HpCurr;
                unit.DebugResetCombatState();var wing=new QuetzalcoatlWingedSerpent(context);wing.CastCode();
                Assert("wing fire on start "+round,unit.ActiveStatuses.Any(s=>s.Key.StartsWith("quetzalcoatl_winged_serpent_")),"present","checked");
                Turns(unit,9);Assert("wing absent before ten "+round,!unit.ActiveStatuses.Any(s=>s.Key.StartsWith("quetzalcoatl_winged_serpent_")),"expired","checked");
                Turns(unit,1);Assert("wing renews at ten "+round,unit.ActiveStatuses.Any(s=>s.Key.StartsWith("quetzalcoatl_winged_serpent_")),"present","checked");wing.StopCode();
                unit.DebugResetCombatState();var shield=new TsukuyomiSpellShield(context);shield.CastCode();Turns(unit,3);
                Equal("spellshield before four "+round,0,unit.ShieldCurr);Turns(unit,1);int granted=unit.ShieldCurr;
                Assert("spellshield four "+round,granted>0,"shield",granted.ToString());Turns(unit,8);Equal("spellshield armed pauses "+round,granted,unit.ShieldCurr);
                hp=unit.HpCurr;unit.TakeDamage(new DamageContext(source,100,CodeType.Normal,new List<int>{DamageTag.SingleTarget,DamageTag.TrueDamage}));
                Equal("spellshield cancels attack "+round,hp,unit.HpCurr);Equal("spellshield consumed "+round,0,unit.ShieldCurr);
                Turns(unit,3);Equal("spellshield recharge three "+round,0,unit.ShieldCurr);Turns(unit,1);Assert("spellshield recharge four "+round,unit.ShieldCurr>0,"shield",unit.ShieldCurr.ToString());shield.StopCode();
                unit.DebugResetCombatState();var fickle=new TsukuyomiFickle(context);fickle.CastCode();Turns(unit,1);
                Equal("fickle one turn two modifiers "+round,2,unit.ActiveStatuses.First(s=>s.Key=="tsukuyomi_fickle").Effects.Count);fickle.StopCode();
            }
        }

        private void GridAndEffects()
        {
            Clear();Equal("field x min",-2,grid.xMin);Equal("field x max",2,grid.xMax);Equal("field y min",1,grid.yMin);Equal("field y max",4,grid.yMax);
            for(int x=1;x<=2;x++)for(int y=1;y<=4;y++)SpawnEnemy(1062,1,x,y);
            Equal("enemy field capacity",8,Enemies.Count);
            Assert("occupied cell rejected",grid.SpawnUnit(1,1,true,1062)==null,"null","attempt");
            var unit=Enemies[0];var cell=unit.currentCell;unit.Die(null);
            Assert("death reserves cell until next round",!grid.IsCellAvailable(cell.xPos,cell.yPos),"reserved","checked");
            game.DebugResetBattle();Assert("round reset releases cell",grid.IsCellAvailable(cell.xPos,cell.yPos),"available","checked");
            Clear();var hero=SpawnHero();var enemy=SpawnEnemy(1062,90);MakeUnavoidable(enemy);
            var whip=new WhipCheckEffect();hero.AddStatus(BuffStatus.Create(999906,"verify_whip","test",hero,hero,whip));
            enemy.TakeDamage(new DamageContext(hero,1000,CodeType.Normal,new List<int>{DamageTag.SingleTarget,DamageTag.TrueDamage}));
            var status=enemy.ActiveStatuses.FirstOrDefault(s=>s.Key=="colosseum_whip_check");
            Assert("1105 whip check applied",status!=null,"status","checked");
            Equal("1105 whip check duration",3,status?.Duration??0);
            if(status!=null)Near("1105 whip damage multiplier",.8f,status.Effects[0].EffectObject.OutgoingDamageModifier(enemy,hero,new DamageContext(enemy,100,CodeType.Normal,new List<int>())));
            // 함수형 공용 효과 9종은 타깃/비타깃 분기와 수치를 실제 상태에 장착하여 검사한다.
            var effects=new BaseEffect[]{new ReceivingDamageMultiplierEffect(.8f),new OutgoingDamageMultiplierEffect(1.2f),new PrimaryStatMultiplierEffect(1.3f),new CritMultiplierBonusEffect(.4f),new DamageOverTimeApplicationEffect(1.5f),new ArmorShredEffect(.6f),new AttackTriggeredHealingEffect(1.7f),new ExcessCritConversionEffect(1.8f),new ManaEfficiencyEffect(.9f)};
            for(int i=0;i<effects.Length;i++)hero.AddStatus(BuffStatus.Create(999910+i,"verify_effect_"+i,"test",hero,hero,effects[i]));
            var ctx=new DamageContext(hero,100,CodeType.Normal,new List<int>());
            Near("shared receiving",.8f,effects[0].ReceivingDamageModifier(hero));Near("shared outgoing",1.2f,effects[1].OutgoingDamageModifier(hero,enemy,ctx));
            Near("shared primary",1.3f,effects[2].PrimaryStatMultiplierModifier(hero,PrimaryStat.INT));Near("shared crit",.4f,effects[3].CritMultiplierAdditiveModifier(hero));
            Near("shared DOT",1.5f,effects[4].DamageOverTimeApplicationMultiplier(hero));Near("shared armor",.6f,effects[5].OwnedDefenseStatMultiplierModifier(hero,ctx));
            Near("shared healing",1.7f,effects[6].HealingReceivedMultiplierModifier(hero,hero,true));Near("shared excess crit",1.8f,effects[7].ExcessCritChanceConversionMultiplier(hero));Near("shared mana",.9f,effects[8].ManaRecoveryIntContributionMultiplierModifier(hero));
        }

        private IEnumerator SpeedReplay()
        {
            List<string> baseline=null;
            foreach(float scale in new[]{1f,8f})
            {
                Clear();SpawnHero();DebugMode.ForcedThemeId=8;game.DebugLoadStage(39);
                DebugMode.AllyInvincible=DebugMode.EnemyInvincible=true;
                UnityEngine.Random.InitState(20260908);DebugMode.SetTimeScale(scale);
                var trace=new List<string>();
                foreach(var unit in Heroes.Concat(Enemies))unit.AddListener<EventContext>(UnitEventType.OnTurnStart,_=>trace.Add(unit.ID.ToString()));
                game.StartRound();float deadline=Time.realtimeSinceStartup+90;
                while(trace.Count<16&&game.gameState==GameState.RoundInProgress&&Time.realtimeSinceStartup<deadline)yield return null;
                Equal("speed "+scale+" sixteen turns",16,trace.Count);
                if(baseline==null)baseline=trace;
                else Assert("1x/8x same sixteen turn order",baseline.SequenceEqual(trace),string.Join(",",baseline),string.Join(",",trace));
                game.DebugLoadStage(31);DebugMode.AllyInvincible=DebugMode.EnemyInvincible=false;
            }
            DebugMode.SetTimeScale(8);
        }

        private IEnumerator SceneCycles()
        {
            for(int i=0;i<3;i++)
            {
                GameManager.LoadMainMenuScene();
                for(int frame=0;frame<3;frame++)yield return null;
                Assert("scene cycle "+i+" menu cleanup",GameManager.Instance==null,"no game manager","checked");
                GameManager.LoadBattleScene();
                float deadline=Time.realtimeSinceStartup+30;
                while(GameManager.Instance?.RoundManager==null&&Time.realtimeSinceStartup<deadline)yield return null;
                game=GameManager.Instance;grid=game.gridManager;
                Equal("scene cycle "+i+" one GameManager",1,FindObjectsByType<GameManager>(FindObjectsSortMode.None).Length);
                Equal("scene cycle "+i+" one GridManager",1,FindObjectsByType<GridManager>(FindObjectsSortMode.None).Length);
                Equal("scene cycle "+i+" one RunManager",1,FindObjectsByType<RunManager>(FindObjectsSortMode.None).Length);
                Assert("scene cycle "+i+" save protected",DebugMode.SessionActive,"protected","checked");
            }
        }
    }
}
#endif
