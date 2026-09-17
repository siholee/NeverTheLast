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
            RewardHealing();
            RewardRevival();
            SchedulerRules();
            StatsAndEffects();
            TrainingEquipmentEconomy();
            TrainingEnergyAndRest();
            SkillHintsAndLearning();
            TonicsAndShop();
            CandiesValuablesAndStock();
            NordTrilogyWiring();
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
                        "special"=>CodeFactory.CreateSpecialCode(entry.id,new SpecialCodeContext{Caster=caster}),
                        _=>null
                    };
                    Assert("catalog factory "+slot.Key+" "+entry.id,code!=null,"implementation",code?.GetType().Name);
                }
            }
        }
        public sealed class CatalogEntry { public int id; public string verbalName; public string codeName; public string description; }

        private void RewardHealing()
        {
            RewardDataList data=game.dataManager.FetchRewardDataList();
            var healing=(data?.rewards??new List<RewardDef>()).Where(r=>r?.IsHealingReward==true).ToList();
            Equal("healing reward count",5,healing.Count);
            Equal("healing reward unique IDs",healing.Count,healing.Select(r=>r.id).Distinct().Count());
            Assert("healing reward tiers T1-T5",new HashSet<int>(healing.Select(r=>r.tier)).SetEquals(new[]{1,2,3,4,5}),
                "1,2,3,4,5",string.Join(",",healing.Select(r=>r.tier).OrderBy(x=>x)));
            Assert("healing rewards universal",healing.All(r=>r.themeIds==null||r.themeIds.Count==0),
                "empty themeIds","checked");
            Assert("healing bottle art loads",healing.All(r=>!string.IsNullOrWhiteSpace(r.artPath)&&Resources.Load<Sprite>(r.artPath)!=null),
                "all sprites","checked");

            foreach(var pair in new[]{("healing_domain_t1",.2f),("healing_domain_t2",.5f),("healing_domain_t3",.8f)})
            {
                RewardDef reward=healing.FirstOrDefault(r=>r.id==pair.Item1);
                Assert(pair.Item1+" target selection",reward?.RequiresTargetSelection==true,"true",reward?.RequiresTargetSelection.ToString());
                Near(pair.Item1+" heal ratio",pair.Item2,reward?.healPercent??0f);
            }
            RewardDef top=healing.FirstOrDefault(r=>r.id=="healing_domain_t4");
            Assert("T4 single full heal",top?.fullHealTarget==true&&top.fullHealParty==false,
                "single target full","checked");
            RewardDef elixir=healing.FirstOrDefault(r=>r.id=="healing_elixir_t5");
            Assert("T5 elixir party full heal",elixir?.fullHealParty==true&&!elixir.RequiresTargetSelection,
                "party full without target","checked");

            Clear();
            Unit first=SpawnHero(); Unit second=SpawnHero(-2,1);
            first.ModifyHp(first.HpMax/4); second.ModifyHp(second.HpMax/3);
            int firstBefore=first.HpCurr,secondBefore=second.HpCurr,stageBefore=game.RoundManager.Stage;
            RewardDef medium=healing.First(r=>r.id=="healing_domain_t2");
            game.rewardManager.ApplyReward(medium);
            Equal("target heal rejects missing target stage",stageBefore,game.RoundManager.Stage);
            Equal("target heal rejects missing target HP",firstBefore,first.HpCurr);
            int expectedFirst=Mathf.Min(first.HpMax,firstBefore+RewardManager.CalculateHealingAmount(medium,first));
            game.rewardManager.ApplyReward(medium,first);
            Equal("target heal applies selected ally",expectedFirst,first.HpCurr);
            Equal("target heal leaves other ally",secondBefore,second.HpCurr);

            // 범용 보상은 테마와 무관하게 풀에 들어가며, 모두 건강할 때는 무효 카드가 나오지 않는다.
            first.ModifyHp(first.HpMax/2);
            var offered=game.rewardManager.GenerateRewards(999,10,GameMode.Training,null);
            Assert("healing rewards enter any theme pool",healing.All(h=>offered.Any(r=>r.id==h.id)),
                "all healing IDs",string.Join(",",offered.Where(r=>r.IsHealingReward).Select(r=>r.id)));

            first.ModifyHp(first.HpMax); second.ModifyHp(second.HpMax);
            var healthyOffer=game.rewardManager.GenerateRewards(999,10,GameMode.Training,null);
            Assert("healing rewards hidden for healthy party",healthyOffer.All(r=>!r.IsHealingReward),
                "no healing reward",string.Join(",",healthyOffer.Where(r=>r.IsHealingReward).Select(r=>r.id)));

            first.ModifyHp(first.HpMax/5); second.ModifyHp(second.HpMax/5);
            game.rewardManager.ApplyReward(elixir);
            Equal("elixir heals first ally",first.HpMax,first.HpCurr);
            Equal("elixir heals entire party",second.HpMax,second.HpCurr);
            Clear();
        }

        private void RewardRevival()
        {
            RewardDataList data=game.dataManager.FetchRewardDataList();
            var revival=(data?.rewards??new List<RewardDef>()).Where(r=>r?.IsRevivalReward==true).ToList();
            Equal("revival reward count",5,revival.Count);
            Equal("revival reward unique IDs",revival.Count,revival.Select(r=>r.id).Distinct().Count());
            Assert("revival reward tiers T1-T5",new HashSet<int>(revival.Select(r=>r.tier)).SetEquals(new[]{1,2,3,4,5}),
                "1,2,3,4,5",string.Join(",",revival.Select(r=>r.tier).OrderBy(x=>x)));
            Assert("revival rewards universal",revival.All(r=>r.themeIds==null||r.themeIds.Count==0),
                "empty themeIds","checked");
            Assert("revival bottle art loads",revival.All(r=>!string.IsNullOrWhiteSpace(r.artPath)&&Resources.Load<Sprite>(r.artPath)!=null),
                "all sprites","checked");

            foreach(var pair in new[]{("revival_medicine_t1",.1f),("revival_medicine_t2",.5f),
                         ("revival_medicine_t3",1f),("revival_medicine_t4",1f)})
            {
                RewardDef reward=revival.FirstOrDefault(r=>r.id==pair.Item1);
                Assert(pair.Item1+" target selection",reward?.RequiresReviveTargetSelection==true,
                    "true",reward?.RequiresReviveTargetSelection.ToString());
                Near(pair.Item1+" revive ratio",pair.Item2,reward?.revivePercent??0f);
            }
            RewardDef aether=revival.FirstOrDefault(r=>r.id=="revival_aether_t5");
            Assert("T5 aether party full revive",aether?.fullReviveParty==true&&!aether.RequiresTargetSelection,
                "party full without target","checked");

            Clear();
            Unit survivor=SpawnHero(); Unit fallen=SpawnHero(-2,1); Unit otherFallen=SpawnHero(-2,2);
            fallen.Die(null); otherFallen.Die(null);
            int stageBefore=game.RoundManager.Stage;
            RewardDef beginner=revival.First(r=>r.id=="revival_medicine_t1");
            game.rewardManager.ApplyReward(beginner);
            Equal("target revive rejects missing target stage",stageBefore,game.RoundManager.Stage);
            Assert("target revive rejects missing target",!fallen.isActive,"fallen","active");
            int expected=RewardManager.CalculateReviveHp(beginner,fallen);
            game.rewardManager.ApplyReward(beginner,fallen);
            Assert("target revive activates selected ally",fallen.isActive,"active","fallen");
            Equal("target revive applies exact HP",expected,fallen.HpCurr);
            Assert("target revive leaves other ally fallen",!otherFallen.isActive,"fallen","active");

            Clear();
            survivor=SpawnHero(); fallen=SpawnHero(-2,1); otherFallen=SpawnHero(-2,2);
            fallen.Die(null); otherFallen.Die(null);
            var offered=game.rewardManager.GenerateRewards(999,10,GameMode.Training,null);
            Assert("revival rewards enter any theme pool",revival.All(r=>offered.Any(candidate=>candidate.id==r.id)),
                "all revival IDs",string.Join(",",offered.Where(r=>r.IsRevivalReward).Select(r=>r.id)));
            game.rewardManager.ApplyReward(aether);
            Equal("aether revives first fallen ally",fallen.HpMax,fallen.HpCurr);
            Equal("aether revives entire fallen party",otherFallen.HpMax,otherFallen.HpCurr);
            Assert("aether activates all fallen allies",fallen.isActive&&otherFallen.isActive,"all active","fallen remains");
            Equal("aether leaves living ally at full HP",survivor.HpMax,survivor.HpCurr);

            Clear();
            SpawnHero(); SpawnHero(-2,1);
            var livingOffer=game.rewardManager.GenerateRewards(999,10,GameMode.Training,null);
            Assert("revival rewards hidden without fallen ally",livingOffer.All(r=>!r.IsRevivalReward),
                "no revival reward",string.Join(",",livingOffer.Where(r=>r.IsRevivalReward).Select(r=>r.id)));
            Clear();
        }

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
            // 추천 조합은 편성 가능한 아군 카탈로그이며 적은 포함하지 않는다.
            if (!unit.IsEnemy)
                Assert("unit "+id+" synergy lookup",
                    Managers.SynergyCatalog.UnitOf(unit.ID)!=null,"profile",
                    Managers.SynergyCatalog.NameOf(unit.ID));
            Assert("unit "+id+" normal/ultimate",unit.ActiveNormalCode!=null&&unit.ActiveUltimateCode!=null,"both",unit.ActiveNormalCode?.GetType().Name+"/"+unit.ActiveUltimateCode?.GetType().Name);
            foreach(int level in new[]{1,30,60,90,100,1})
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
                // 귀중품은 입는 물건이 아니다. 장착이 <b>거절되는 것</b>이 올바른 동작이다.
                if(item.IsValuable)
                {
                    Assert("valuable "+item.id+" not equippable",!hero.TryEquipItem(item.id,out _),
                        "rejected","equipped");
                    continue;
                }

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

        private void TrainingEnergyAndRest()
        {
            TrainingState state=TrainingManager.State;

            // 실패율 곡선의 경계. 화면에 띄우는 숫자와 같은 함수다.
            state.Reset();
            Equal("no failure at full energy",0,TrainingManager.GetFailureRate(PrimaryStat.STR));
            state.SpendEnergy(40);
            Equal("no failure at the safe floor",0,TrainingManager.GetFailureRate(PrimaryStat.STR));
            state.SpendEnergy(1);
            Equal("failure starts below sixty",1,TrainingManager.GetFailureRate(PrimaryStat.STR));
            state.SpendEnergy(29);
            Equal("failure at the warning floor",18,TrainingManager.GetFailureRate(PrimaryStat.STR));
            state.SpendEnergy(30);
            Equal("failure peaks on empty",66,TrainingManager.GetFailureRate(PrimaryStat.STR));
            Equal("INT training never fails",0,TrainingManager.GetFailureRate(PrimaryStat.INT));

            // 컨디션 — 실패는 확정 하락, 휴식만 확정 회복.
            state.Reset();
            int normal=state.ConditionIndex;
            state.DriftCondition(true);
            Equal("failed training worsens condition",normal+1,state.ConditionIndex);
            state.ImproveCondition();
            Equal("condition repair undoes it",normal,state.ConditionIndex);
            for(int i=0;i<6;i++)state.ImproveCondition();
            Equal("condition tops out",0,state.ConditionIndex);
            for(int i=0;i<8;i++)state.WorsenCondition();
            Equal("condition bottoms out",TrainingState.ConditionNames.Length-1,state.ConditionIndex);

            // 훈련 3회 : 휴식 1회. 가장 비싼 훈련도 세 번째까지 안전지대에서 출발한다.
            state.Reset();
            int launched=0;
            while(TrainingManager.GetFailureRate(PrimaryStat.STR)==0)
            {
                state.SpendEnergy(TrainingManager.GetEnergyCost(PrimaryStat.STR));
                launched++;
                if(launched>10)break;
            }
            Equal("three safe strength trainings per full bar",3,launched);
            Assert("one rest covers that cycle",
                GameManager.RestEnergyRecovery>=3*TrainingManager.GetEnergyCost(PrimaryStat.CON),
                ">= 3 x 16",GameManager.RestEnergyRecovery.ToString());

            // 휴식 — 체력 · 컨디션 · 파티 체력 셋을 한 번에 돌려준다.
            Clear();
            game.RoundManager.InitializeStage(3);
            game.RestorePreparationActionState(false,0);
            Unit hero=SpawnHero();
            hero.ModifyHp(hero.HpMax/10);
            state.Reset();
            state.SpendEnergy(60);
            state.WorsenCondition();
            int energyBefore=state.Energy,conditionBefore=state.ConditionIndex,hpBefore=hero.HpCurr;
            int expectedHeal=Mathf.CeilToInt(hero.HpMax*GameManager.RestPartyHealRatio);

            game.RestFromPreparation();
            Equal("rest restores training energy",
                Mathf.Min(TrainingState.MaxEnergy,energyBefore+GameManager.RestEnergyRecovery),state.Energy);
            Equal("rest repairs one condition step",conditionBefore-1,state.ConditionIndex);
            Equal("rest heals a fraction of max HP",Mathf.Min(hero.HpMax,hpBefore+expectedHeal),hero.HpCurr);
            Assert("rest is not a free elixir",hero.HpCurr<hero.HpMax,"partial heal",
                hero.HpCurr+"/"+hero.HpMax);
            Assert("rest spends the preparation action",game.PreparationActionUsed,"used","free");
            Assert("rest re-rolls the support placement",!state.PlacementReady,"invalidated","kept");

            state.Reset();
            game.RestorePreparationActionState(false,0);
            Clear();
        }

        private void SkillHintsAndLearning()
        {
            Clear();
            Unit main=SpawnHero();
            SkillHintState hints=TrainingManager.Hints;hints.Clear();
            TrainingState state=TrainingManager.State;state.Reset();

            // 은금 사다리 역색인 — 은 코드의 SupersededByCodeId 선언 한 줄이 곧 선행 조건이다.
            Equal("gold 13 requires silver 11",11,PassiveCatalog.RequiredCodeIdFor(13,main));
            Equal("gold 32 requires silver 70",70,PassiveCatalog.RequiredCodeIdFor(32,main));
            Equal("gold 103 requires silver 2",2,PassiveCatalog.RequiredCodeIdFor(103,main));
            Equal("silver needs no prerequisite",0,PassiveCatalog.RequiredCodeIdFor(11,main));
            Assert("catalog reads the enhanced grade",PassiveCatalog.Get(13,main).Grade==CodeGrade.Enhanced,
                "Enhanced",PassiveCatalog.Get(13,main).Grade.ToString());
            Assert("unique passives are never hinted",!PassiveCatalog.Get(221,main).CanBeHinted,
                "blocked","hintable");

            // 힌트가 없으면 어떤 코드도 살 수 없다.
            Assert("no purchase without a hint",!TrainingManager.TryLearnSkill(11,out string noHint),
                "rejected",noHint);

            // 힌트 레벨이 값을 깎는다.
            hints.Add(11,1,"검증");
            Equal("silver base cost",TrainingManager.SilverSkillCost,TrainingManager.GetSkillCost(11));
            hints.Add(11,1,"검증");
            Equal("hint level two discount",
                Mathf.CeilToInt(TrainingManager.SilverSkillCost*SkillHintState.CostMultipliers[1]),
                TrainingManager.GetSkillCost(11));
            hints.Add(11,1,"검증");hints.Add(11,1,"검증");
            Equal("hint level caps",SkillHintState.MaxLevel,hints.LevelOf(11));
            Equal("hint level three discount",
                Mathf.CeilToInt(TrainingManager.SilverSkillCost*SkillHintState.CostMultipliers[2]),
                TrainingManager.GetSkillCost(11));

            Assert("cannot buy without points",!TrainingManager.TryLearnSkill(11,out string broke),
                "rejected",broke);
            Equal("a failed purchase spends nothing",0,state.SkillPoints);

            int cost=TrainingManager.GetSkillCost(11);
            state.GainSkillPoints(cost);
            Assert("silver purchase succeeds",TrainingManager.TryLearnSkill(11,out string silverBuy),
                "learned",silverBuy);
            Equal("purchase spends the points",0,state.SkillPoints);
            Assert("purchased skill is owned",main.HasLearnedPassiveCode(11),"owned","missing");
            // 영구 경로로 들어가야 라운드 종료 후 스냅샷 복원에서 살아남는다.
            Assert("purchase survives the round snapshot",main.GrantedPassiveCodeIds.Contains(11),
                "persisted","lost");
            Equal("a learned hint leaves the list",0,hints.LevelOf(11));

            // 금은 은을 밟고 올라간다.
            hints.Clear();hints.Add(32,1,"검증");
            state.GainSkillPoints(TrainingManager.EnhancedSkillCost*2);
            Assert("gold without its silver is blocked",!TrainingManager.TryLearnSkill(32,out string locked),
                "blocked",locked);
            Assert("the block names the prerequisite",locked!=null&&locked.Contains("선행"),"선행 필요",locked);

            hints.Add(70,1,"검증");
            Assert("the silver prerequisite can be bought",TrainingManager.TryLearnSkill(70,out string silverStep),
                "learned",silverStep);
            Assert("gold opens once the silver is owned",TrainingManager.TryLearnSkill(32,out string goldBuy),
                "learned",goldBuy);
            Assert("gold is owned",main.HasLearnedPassiveCode(32),"owned","missing");

            // 이미 배운 코드는 목록에서 잠긴다.
            hints.Add(32,1,"검증");
            var offers=TrainingManager.GetSkillOffers();
            Assert("owned skills stay locked in the list",
                offers.All(offer=>offer.CodeId!=32||offer.BlockedReason=="이미 보유"),
                "이미 보유",string.Join(",",offers.Select(offer=>offer.CodeId+":"+offer.BlockedReason)));

            // 런 도중 합류한 동료도 그 턴부터 서포트다. 영입은 벤치로 들어온다.
            Clear();
            Unit lead=SpawnHero();
            TrainingManager.State.InvalidatePlacement();
            TrainingManager.EnsureSupportPlacement();
            int supportsBefore=TrainingManager.GetSupportCount();
            int joinerId=game.unitDataList.units.First(def=>def.id!=lead.ID).id;
            Unit joiner=grid.SpawnUnit(0,0,false,joinerId,true);
            Assert("recruit spawns on the bench",joiner!=null,"spawned","null");
            Equal("recruit counts as a support",supportsBefore+1,TrainingManager.GetSupportCount());
            Assert("recruit has no seat yet",!TrainingManager.State.WasPlacementRolled(joiner.ID),
                "unrolled","rolled");
            TrainingManager.EnsureSupportPlacement();
            Assert("recruit gets a seat in the same turn",
                TrainingManager.State.WasPlacementRolled(joiner.ID),"rolled","unrolled");

            TrainingManager.State.SetPlacement(joiner.ID,PrimaryStat.STR);
            Assert("a seated recruit joins that training",
                TrainingManager.GetSupportsOn(PrimaryStat.STR).Contains(joiner),"seated","absent");
            Assert("a seated recruit adds its bonus",
                TrainingManager.GetBaseSupportBonus(PrimaryStat.STR)>=TrainingManager.SupportStatBonusPerUnit,
                ">=1",TrainingManager.GetBaseSupportBonus(PrimaryStat.STR).ToString());

            hints.Clear();state.Reset();
            Clear();
        }

        private void TonicsAndShop()
        {
            RewardDataList data=game.dataManager.FetchRewardDataList();
            var all=data?.rewards??new List<RewardDef>();
            var tonics=all.Where(r=>r?.IsTonic==true).ToList();
            Equal("tonic count",25,tonics.Count);
            Equal("tonic unique IDs",tonics.Count,tonics.Select(r=>r.id).Distinct().Count());
            Assert("tonics stay out of the shop",tonics.All(r=>r.goldCost==0),"no price","checked");
            foreach(var stat in Enum.GetValues(typeof(PrimaryStat)).Cast<PrimaryStat>())
            {
                var family=tonics.Where(r=>r.TryGetTonicStat(out var parsed)&&parsed==stat).ToList();
                Equal("tonic family "+stat,5,family.Count);
                Assert("tonic family "+stat+" spans T1-T5",new HashSet<int>(family.Select(r=>r.tier)).SetEquals(new[]{1,2,3,4,5}),
                    "1,2,3,4,5",string.Join(",",family.Select(r=>r.tier).OrderBy(x=>x)));
                Assert("tonic "+stat+" T1-T3 additive",family.Where(r=>r.tier<=3).All(r=>r.tonicFlat>0&&r.tonicMultiplier<=1f),
                    "flat only","checked");
                Assert("tonic "+stat+" T4-T5 multiplicative",family.Where(r=>r.tier>=4).All(r=>r.tonicMultiplier>1f&&r.tonicFlat==0),
                    "multiplier only","checked");
            }

            // 강화제는 런이 들고 있다. 세이(주 INT·부 DEX)의 STR에는 캐릭터 보너스가 없어
            // 합연산 값이 그대로 드러나므로 검증 스탯으로 쓴다.
            Clear();
            PartyTonicState state=RunManager.Instance?.PartyTonics;
            Assert("party tonic state exists",state!=null,"state","null");
            if(state==null)return;
            state.Clear();

            Unit hero=SpawnHero();
            Unit enemy=SpawnEnemy(game.dataManager.FetchEnemyDataList().enemies[0].id);
            int heroBase=hero.GetBaseStr(),enemyBase=enemy.GetBaseStr();
            RewardDef low=tonics.First(r=>r.id=="tonic_str_t1");
            RewardDef mid=tonics.First(r=>r.id=="tonic_str_t2");
            RewardDef high=tonics.First(r=>r.id=="tonic_str_t4");

            Assert("tonic applies",game.rewardManager.ApplyRewardEffect(low),"true","rejected");
            Equal("tonic raises ally stat",heroBase+low.tonicFlat,hero.GetBaseStr());
            Equal("tonic spares enemies",enemyBase,enemy.GetBaseStr());

            game.rewardManager.ApplyRewardEffect(mid);
            Equal("stronger tonic replaces weaker",heroBase+mid.tonicFlat,hero.GetBaseStr());
            game.rewardManager.ApplyRewardEffect(low);
            Equal("weaker tonic does not stack",heroBase+mid.tonicFlat,hero.GetBaseStr());
            Equal("one bottle per stat and kind",1,state.Active.Count);

            game.rewardManager.ApplyRewardEffect(high);
            Equal("multiplicative tonic stacks on additive",
                Mathf.RoundToInt((heroBase+mid.tonicFlat)*high.tonicMultiplier),hero.GetBaseStr());
            Equal("additive and multiplicative coexist",2,state.Active.Count);

            var saved=state.BuildSaveData();
            state.Clear();
            Equal("cleared tonics stop applying",heroBase,hero.GetBaseStr());
            state.Restore(saved);
            Equal("tonic save round trip",
                Mathf.RoundToInt((heroBase+mid.tonicFlat)*high.tonicMultiplier),hero.GetBaseStr());

            for(int i=0;i<PartyTonicState.BattleDuration-1;i++)state.ConsumeBattle();
            Assert("tonic survives until the last battle",state.HasAny,"active","expired");
            state.ConsumeBattle();
            Assert("tonic expires after five battles",!state.HasAny,"expired","active");
            Equal("expired tonic restores stat",heroBase,hero.GetBaseStr());

            // 상점 — 매대와 가격.
            var goods=game.rewardManager.BuildShopGoods();
            Equal("shop goods count",9,goods.Count);
            Assert("shop sells potions only",goods.All(g=>g.IsHealingReward||g.IsRevivalReward),"potions","checked");
            Assert("every shop good is priced",goods.All(g=>g.goldCost>0),"priced","checked");
            RewardDef potion=goods.First(g=>g.id=="healing_domain_t2");
            Equal("shop price scales with stage",potion.goldCost*7,RewardManager.ShopPrice(potion,7));
            Equal("shop price floors at stage 1",potion.goldCost,RewardManager.ShopPrice(potion,0));
            Equal("revival costs half again",
                goods.First(g=>g.id=="healing_domain_t1").goldCost*3/2,
                goods.First(g=>g.id=="revival_medicine_t1").goldCost);

            game.RestorePreparationActionState(false,0);
            Equal("shop purchases start full",GameManager.MaxShopPurchases,game.ShopPurchasesLeft);
            for(int i=0;i<GameManager.MaxShopPurchases+1;i++)game.NotifyShopPurchase();
            Equal("shop purchase limit holds",0,game.ShopPurchasesLeft);
            game.RestorePreparationActionState(false,0);

            // 보상 장수는 25스테이지마다 +1, 6장에서 멈춘다. 소모품 장수 상한은 없다.
            Equal("reward count stage 1",3,RewardManager.RewardCountForStage(1));
            Equal("reward count stage 25",4,RewardManager.RewardCountForStage(25));
            Equal("reward count stage 75",6,RewardManager.RewardCountForStage(75));
            Equal("reward count caps at 6",6,RewardManager.RewardCountForStage(300));
            hero.ModifyHp(hero.HpMax/2);
            var offer=game.rewardManager.GenerateRewards(6,10,GameMode.Training,null);
            Equal("six-card offer fills",6,offer.Count);
            Assert("offer never lists enemy-only gear",offer.All(r=>r.item?.enemyOnly!=true),"none","enemy-only listed");

            state.Clear();
            Clear();
        }

        /// <summary>이상한 사탕 · 귀중품 주머니 · 상점 장비 매대.</summary>
        private void CandiesValuablesAndStock()
        {
            Clear();
            var all=game.dataManager.FetchRewardDataList()?.rewards??new List<RewardDef>();
            var candies=all.Where(r=>r?.IsLevelGrant==true).ToList();
            Equal("candy count",2,candies.Count);
            Assert("candies start at T3",candies.All(r=>r.tier>=3),">=3",
                string.Join(",",candies.Select(r=>r.tier)));

            RewardDef single=candies.First(r=>!r.levelGrantParty);
            RewardDef party=candies.First(r=>r.levelGrantParty);
            Equal("single candy tier",3,single.tier);
            Equal("party candy tier",4,party.tier);
            Assert("single candy needs a target",single.RequiresLevelTargetSelection,"true","no pick");
            Assert("party candy needs no target",!party.RequiresLevelTargetSelection,"false","asks");

            Unit first=SpawnHero(); Unit second=SpawnHero(-2,1);
            first.DebugSetLevel(10); second.DebugSetLevel(10);
            int exp=first.Exp;

            Assert("single candy applies",game.rewardManager.ApplyRewardEffect(single,first),"true","rejected");
            Equal("single candy raises target",11,first.Level);
            Equal("single candy spares others",10,second.Level);
            Equal("single candy keeps stored EXP",exp,first.Exp);

            game.rewardManager.ApplyRewardEffect(party);
            Equal("party candy raises everyone",13,first.Level);
            Equal("party candy raises second ally",12,second.Level);

            // 쓰러진 아군도 사탕을 받는다. 한 판 진 것으로 성장만 뒤처지면 되돌릴 수 없다.
            second.Die(null);
            game.rewardManager.ApplyRewardEffect(party);
            Equal("party candy reaches fallen ally",14,second.Level);

            // 귀중품 — 주머니로 들어가고, 값은 주울 때 확정된다.
            InventoryManager inventory=game.inventoryManager;
            var valuables=game.itemDataList.items.Where(i=>i.IsValuable).ToList();
            Assert("valuable catalogue exists",valuables.Count>0,">0","none");
            Assert("valuables carry no weight",valuables.All(i=>i.weight==0),"0",
                string.Join(",",valuables.Select(i=>i.weight)));

            int goldBefore=inventory.Gold;
            int pouchBefore=inventory.Valuables.Count;
            ItemData sample=valuables[0];
            var pickup=new RewardDef{id="probe",itemId=sample.id,item=sample,tier=sample.rarity};
            Assert("valuable pickup applies",game.rewardManager.ApplyRewardEffect(pickup),"true","rejected");
            Equal("valuable enters the pouch",pouchBefore+1,inventory.Valuables.Count);
            Equal("valuable pays nothing on pickup",goldBefore,inventory.Gold);
            Assert("valuable never reaches a unit",
                !first.CarriedItemIds.Contains(sample.id),"not carried","carried");

            int worth=inventory.Valuables[inventory.Valuables.Count-1].gold;
            Equal("valuable worth uses stage price",
                RewardManager.ValuablePrice(sample,Mathf.Max(1,game.RoundManager?.Stage??1)),worth);
            Assert("valuable sells",inventory.TrySellValuable(inventory.Valuables.Count-1,out int paid),
                "true","refused");
            Equal("valuable pays what it was worth",worth,paid);
            Equal("valuable leaves the pouch",pouchBefore,inventory.Valuables.Count);
            Equal("valuable adds gold",goldBefore+worth,inventory.Gold);

            // 상점 장비 매대 — 캐릭터 전용은 오르지 않는다.
            var stock=game.rewardManager.BuildShopEquipment();
            Assert("shop stock exists",stock.Count>0,">0","empty");
            Assert("shop stock has no character-only gear",
                stock.All(r=>r.item?.requiredUnitIds==null||r.item.requiredUnitIds.Count==0),"no requiredUnitIds",
                string.Join(",",stock.Where(r=>r.item?.requiredUnitIds!=null&&r.item.requiredUnitIds.Count>0).Select(r=>r.id)));
            Assert("shop stock is priced",stock.All(r=>r.goldCost>0),">0","free item");
            Assert("shop stock holds no valuables",stock.All(r=>r.item?.IsValuable!=true),"none","valuable listed");

            Clear();
        }

        /// <summary>노르드 3부작의 배선 — 체인 · 중간 보스 방아쇠 · 다인 영입 해금.</summary>
        private void NordTrilogyWiring()
        {
            var themes = game.dataManager.FetchStageThemeDataList();
            var all = themes?.stageThemes ?? new List<StageThemeData>();
            StageThemeData nord1 = all.FirstOrDefault(t => t.id == 16);
            StageThemeData nord2 = all.FirstOrDefault(t => t.id == 17);
            StageThemeData nord3 = all.FirstOrDefault(t => t.id == 18);
            Assert("nord trilogy themes exist", nord1 != null && nord2 != null && nord3 != null, "3 themes", "missing");
            if (nord1 == null || nord2 == null || nord3 == null) return;

            // 체인은 16 → 17 → 18에서 끝나고, 17·18만 룰렛에서 16으로 치환된다.
            Equal("nord1 chains to nord2", 17, nord1.chainNextThemeId);
            Equal("nord2 chains to nord3", 18, nord2.chainNextThemeId);
            Equal("nord3 ends the chain", 0, nord3.chainNextThemeId);
            Equal("nord1 is the chain entry", 0, nord1.rotationRedirectThemeId);
            Equal("nord2 redirects to nord1", 16, nord2.rotationRedirectThemeId);
            Equal("nord3 redirects to nord1", 16, nord3.rotationRedirectThemeId);

            // 합류 분기는 6슬롯 중간 보스 자리다. 10슬롯만 보던 방아쇠로는 잡히지 않는다.
            Equal("nord3 midboss slot", 6, nord3.midBossStageInRound);
            Equal("nord3 midboss is sigurd", 2051, nord3.midBossId);

            var recruit = themes.events?.FirstOrDefault(e => e.triggerBossId == nord3.midBossId);
            Assert("midboss triggers a recruit event", recruit != null, "found", "none");
            if (recruit == null) return;

            // 사건 하나가 둘을 내민다. 해금은 <b>전원</b>에게 돌아가야 한다.
            var offered = recruit.RecruitUnitIds.ToList();
            Equal("pair event offers two units", 2, offered.Count);
            Assert("pair event offers sigurd and brynhild", offered.Contains(84) && offered.Contains(85),
                "84,85", string.Join(",", offered));
            Equal("representative recruit id", 84, recruit.RecruitUnitId);

            // 디버그 저장소만 시드한다. 실제 PlayerPrefs에는 쓰지 않는다.
            var write = typeof(SaveSystem).GetMethod("WriteString", BindingFlags.Static | BindingFlags.NonPublic);
            write.Invoke(null, new object[] { "NTL_TrainedCharacters", "{}" });
            Assert("pair starts locked", !SaveSystem.IsStarterUnlocked(84) && !SaveSystem.IsStarterUnlocked(85),
                "locked", "checked");

            var grant = typeof(GameManager).GetMethod("GrantRecruitUnlock", BindingFlags.Instance | BindingFlags.NonPublic);
            grant.Invoke(game, new object[] { recruit });
            Assert("meeting the pair unlocks both",
                SaveSystem.IsStarterUnlocked(84) && SaveSystem.IsStarterUnlocked(85), "both", "checked");

            write.Invoke(null, new object[] { "NTL_TrainedCharacters", "{}" });
            Clear();
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
                Assert("scene cycle "+i+" grid binding", grid == GridManager.Instance, "same instance", "compared");
                Equal("scene cycle "+i+" one GameManager",1,FindObjectsByType<GameManager>().Length);
                Equal("scene cycle "+i+" one GridManager",1,FindObjectsByType<GridManager>().Length);
                Equal("scene cycle "+i+" one RunManager",1,FindObjectsByType<RunManager>().Length);
                Assert("scene cycle "+i+" save protected",DebugMode.SessionActive,"protected","checked");
            }
        }
    }
}
#endif
