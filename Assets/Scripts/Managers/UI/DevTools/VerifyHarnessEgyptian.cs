#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Passive;
using Codes.Ultimate;
using Combat;
using Core;
using Effects.Buffs;
using Entities;
using Managers.UI.Core;
using UnityEngine;
using Kind=Managers.ActionScheduler.ActionKind;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioEgyptianCore(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();_cfActionIdSequence=0;bool enabled=true;
            yield return YvPrepareScene(config,x=>enabled=x);
            DebugMode.AllyInvincible=false;DebugMode.EnemyInvincible=false;
            Unit set=CfAlly(123),isis=CfAlly(125),horus=CfAlly(120),carry=CfAlly(81),thoth=CfAlly(124),enemy=AliveEnemies().First();
            var blood=set.ActivePassiveCodes.OfType<SetDesertBlood>().Single();var royalty=horus.ActivePassiveCodes.OfType<HorusRoyalHeir>().Single();
            void Check(string label,bool ok)=>YvCheck(label,"충족",ok?"충족":"미충족",ok);
            CfStats(set,"세트",1340,110,86,5.5f,90,6,70,5,74,5,94,120);
            CfStats(isis,"이시스",1200,100,45,3,65,4.5f,86,6,72,5,96,140);
            CfStats(horus,"호루스",980,85,58,3.5f,90,6,50,4,70,5,108,120);
            Check("신규3인 원화·팩토리",new[]{set,isis,horus}.All(x=>x.ActiveNormalCode!=null&&x.ActiveUltimateCode!=null&&Helpers.SpriteResource.LoadPortrait(x.PortraitPath,out _)!=null));
            Check("자료실 출혈·소환·초월기 용어",GameGuideCatalog.Find("bleed")!=null&&GameGuideCatalog.Find("summon")!=null&&GameGuideCatalog.Find("ultimate")!=null);
            var guardians=Summons.Of(isis);
            yield return CaptureStill("isis_guardians_initial");
            Check("수호령3기 실제 문양 표시",guardians.All(x=>x.GetComponent<Entities.View.SummonCardView>()?.PortraitRenderer is SpriteRenderer renderer&&renderer.enabled&&renderer.sprite==x.SummonPortraitOverride));
            Check("수호령3기 서로 다른 개체",guardians.Count==3&&guardians.Select(x=>x.SummonKind).Distinct().Count()==3);
            Check("수호령 고정 기초속도60·80·100",guardians.Select(x=>x.StatValue(BaseEnums.StatType.Speed)).SequenceEqual(IsisThreefoldGuard.Speeds));
            Check("수호령 피격 불가·벡터 원본192",guardians.All(x=>x.IsUntargetable&&x.SummonPortraitOverride!=null&&x.SummonPortraitOverride.texture.width==192));
            Check("수호령 실제 공격후보 제외",guardians.All(x=>!CombatTargets.AliveEnemies(enemy).Contains(x)));
            Check("수호단 외부 생성 불가",Summons.StyleOf(isis)==SummonStyle.Cohort&&Summons.RequestExtra(isis)==null&&Summons.Of(isis).Count==3);
            var records=thoth.ActivePassiveCodes.OfType<ThothLivingRecords>().Single();
            Check("수호령3기 토트 소환자1명",records.OwnerCount==1);
            foreach(var x in new[]{set,isis,horus,carry})CfNoCrit(x);
            CfEnsureEffectHit(isis,AliveEnemies());CfEnsureEffectHit(set,AliveEnemies());
            using(var log=new CfLog(AliveAllies()))
            {
                yield return NordAction(guardians[0],Kind.Normal,s=>s.AdvanceAction(guardians[0],1));
                Check("수호령 실제 방35% 소환 일반 피해",log.Dealt.Any(x=>x.Attacker==isis&&x.DamageContext.Damage==Mathf.RoundToInt(isis.DefCurr*.35f)&&x.DamageContext.HasTag(DamageTag.SummonAttack)&&x.DamageContext.HasTag(DamageTag.NormalAttack)));
                Check("바위 부착 이시스 명의",log.Elements.Any(x=>x.Source==isis&&x.Element==BaseEnums.UnitElement.Geo));
            }
            foreach(var foe in AliveEnemies())foe.ResetCombatElements();
            int shield=isis.ShieldCurr;
            yield return NordAction(isis,Kind.Normal,s=>s.AdvanceAction(isis,1));
            Check("이시스 전체 방35% 보호막",AliveAllies().All(x=>x.ShieldCurr>=Mathf.RoundToInt(isis.DefCurr*.35f)));
            int delta=isis.ShieldCurr-shield;shield=isis.ShieldCurr;
            YvAddTestStatus(isis,99911,"egypt_hp",new StatModifierEffect(BaseEnums.StatType.MaxHp,5000));
            yield return NordAction(isis,Kind.Normal,s=>s.AdvanceAction(isis,1));
            Check("보호막 누적·체력 무관",isis.ShieldCurr-shield==delta);isis.RemoveStatusByKey("egypt_hp");
            Check("후계자는 공격 마나 초월기 수르트",royalty.Heir==carry);
            float bonus=royalty.Bonus;
            Check("후계자 특공 비례 초월 피해",Mathf.Abs(bonus-Mathf.Min(.55f,.25f+.15f*SummonSupportCombat.PowerRatio(horus)))<.0001f);
            int Probe(bool ultimate)
            {
                var ctx=new DamageContext(carry,100,ultimate?BaseEnums.CodeType.Ultimate:BaseEnums.CodeType.Normal,ultimate?new(){DamageTag.TrueDamage,DamageTag.UltAttack}:new(){DamageTag.TrueDamage,DamageTag.NormalAttack});enemy.TakeDamage(ctx);return ctx.ResolvedDamage;
            }
            Check("후계자 일반 미증폭·초월만 증폭",Probe(false)==100&&Probe(true)==Mathf.RoundToInt(100*(1+bonus)));
            carry.ClearUltimateResource();
            using(var log=new CfLog(AliveAllies()))
            {
                yield return NordAction(horus,Kind.Normal,s=>s.AdvanceAction(horus,1));
                Check("호루스 실제 일반 공80% 사격",log.Dealt.Any(x=>x.Attacker==horus&&x.DamageContext.Damage==Mathf.RoundToInt(horus.StatValue(BaseEnums.StatType.Attack)*.80f)));
            }
            Check("호루스 일반은 동료 충전 없음",carry.ManaCurr==0);
            yield return NordAction(horus,Kind.Ultimate,s=>horus.FillUltimateResource());
            Check("초월기 지정1명만 마나30%",carry.ManaCurr==Mathf.RoundToInt(carry.ManaMax*.30f)&&horus.ManaCurr==0&&carry.HasStatusKey(HorusRoyalInvestiture.BuffKey)&&!isis.HasStatusKey(HorusRoyalInvestiture.BuffKey));
            Check("왕관 피해30% 가산",Probe(true)==Mathf.RoundToInt(100*(1+bonus+.30f)));
            carry.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved,new EventContext(carry));
            Check("왕관 대상 일반1회 유지",carry.HasStatusKey(HorusRoyalInvestiture.BuffKey));
            carry.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved,new EventContext(carry));
            Check("왕관 대상 일반2회 만료",!carry.HasStatusKey(HorusRoyalInvestiture.BuffKey));
            YvAddTestStatus(horus,99912,"egypt_sp",new StatModifierEffect(BaseEnums.StatType.SpecialAttack,20000));
            Check("후계자 증폭 상한55%",Mathf.Abs(royalty.Bonus-.55f)<.0001f);horus.RemoveStatusByKey("egypt_sp");
            using(var log=new CfLog(AliveAllies()))
            {
                yield return NordAction(set,Kind.Normal,s=>s.AdvanceAction(set,1));
                Check("세트 실제 일반 공90%·출혈",log.Dealt.Any(x=>x.Attacker==set&&x.DamageContext.CodeType==BaseEnums.CodeType.Normal&&x.DamageContext.Damage==Mathf.RoundToInt(set.StatValue(BaseEnums.StatType.Attack)*.90f))&&AliveEnemies().Any(x=>x.HasStatusKey("bleed")));
            }
            foreach(var ally in AliveAllies())ally.RemoveShield(ally.ShieldCurr);
            set.ModifyHp(set.HpMax/2);int hp=set.HpCurr;
            using(var log=new CfLog(AliveAllies()))
            {
                for(int i=0;i<3;i++)
                {
                    int serial=i;
                    yield return NordAction(enemy,Kind.Additional,s=>s.EnqueueAdditional(enemy,"egypt_hit_"+serial,"세트 피격",()=>
                    {for(int hit=0;hit<3;hit++)set.TakeDamage(new DamageContext(enemy,1,BaseEnums.CodeType.Normal,new(){DamageTag.NormalAttack,DamageTag.TrueDamage}));}));
                }
                var counters=log.Dealt.Where(x=>x.Attacker==set&&x.DamageContext.HasTag(DamageTag.CounterAttack)).ToList();
                Check("다단은1행동·반격2회 상한",counters.Count==2&&blood.Budget==0);
                Check("반격 공70%·단일",counters.All(x=>x.DamageContext.Damage==Mathf.RoundToInt(set.StatValue(BaseEnums.StatType.Attack)*.70f)&&x.DamageContext.HasTag(DamageTag.SingleTarget)));
                Check("최대체력 흡혈 실제 회복",set.HpCurr>hp);
            }
            blood.ResetBudget();
            using(var log=new CfLog(AliveAllies()))
            {
                yield return NordAction(enemy,Kind.Additional,s=>s.EnqueueAdditional(enemy,"egypt_excluded","반격·지속 제외",()=>
                {set.TakeDamage(new DamageContext(enemy,1,BaseEnums.CodeType.Passive,new(){DamageTag.CounterAttack,DamageTag.TrueDamage}));set.TakeDamage(new DamageContext(enemy,1,BaseEnums.CodeType.Effect,new()){IsDamageOverTime=true});HealthCost.Pay(set,1);}));
                Check("반격·지속·자해 반격 유발 안함",blood.Budget==2&&!log.Dealt.Any(x=>x.Attacker==set&&x.DamageContext.HasTag(DamageTag.CounterAttack)));
            }
            blood.StopCode();ActionDot.Apply(set,enemy,ActionDotKind.Bleed,inheritedHit:true);
            set.AddShield(10000);hp=set.ShieldCurr;
            blood.CastCode();
            set.TakeDamage(new DamageContext(enemy,1000,BaseEnums.CodeType.Normal,new(){DamageTag.TrueDamage}));
            Check("출혈 적에게 받는 피해10% 감소",hp-set.ShieldCurr==900);
            enemy.RemoveStatusByKey("bleed");hp=set.ShieldCurr;
            set.TakeDamage(new DamageContext(enemy,1000,BaseEnums.CodeType.Normal,new(){DamageTag.TrueDamage}));
            Check("출혈 없으면 피해 경감 없음",hp-set.ShieldCurr==1000);
            ActionDot.Apply(set,enemy,ActionDotKind.Bleed,inheritedHit:true);
            enemy.TakeDamage(new DamageContext(set,1,BaseEnums.CodeType.Effect,new()){IsDamageOverTime=true});
            int raw=Mathf.RoundToInt(set.StatValue(BaseEnums.StatType.SpecialAttack)*.30f);
            var dot=enemy.GetAllStatuses().SelectMany(x=>x.Effects).Select(x=>x.EffectObject).OfType<ActionDotEffect>().First(x=>x.Kind==ActionDotKind.Bleed);
            Check("출혈 계수 특공30%·비치명",dot.EstimateDamagePerTurn()==raw&&dot.CreateDamageOverTimeTick().IsDamageOverTime&&!dot.CreateDamageOverTimeTick().IsCrit);
            int duration=enemy.GetAllStatuses().First(x=>x.Key=="bleed").RemainingTurns;
            DamageOverTimeDetonation.Detonate(enemy,1,set);
            Check("출혈 즉시 발동 비소모",enemy.GetAllStatuses().First(x=>x.Key=="bleed").RemainingTurns==duration);
            blood.StopCode();
            // 공격 종료와 두 번째 일반행동의 마지막 틱을 실제 스케줄러로 확인한다.
            enemy.RemoveStatusByKey("bleed");ActionDot.Apply(set,enemy,ActionDotKind.Bleed,inheritedHit:true);
            enemy.currentNormalTarget=isis;
            using(var log=new CfLog(AliveAllies()))
            {
                yield return NordAction(enemy,Kind.Normal,s=>s.AdvanceAction(enemy,1));
                Check("출혈 첫 일반 종료1틱·기간1",log.Dealt.Count(x=>x.DamageContext.StatusVfxKey=="bleed")==1&&enemy.GetAllStatuses().First(x=>x.Key=="bleed").RemainingTurns==1);
                yield return NordAction(enemy,Kind.Normal,s=>s.AdvanceAction(enemy,1));
                Check("출혈 두번째 종료 마지막 틱 후 만료",log.Dealt.Count(x=>x.DamageContext.StatusVfxKey=="bleed")==2&&!enemy.HasStatusKey("bleed"));
            }
            ActionDot.Apply(set,enemy,ActionDotKind.Bleed,inheritedHit:true);
            using(var log=new CfLog(AliveAllies()))
            {
                yield return NordAction(enemy,Kind.Additional,s=>s.EnqueueAdditional(enemy,"egypt_no_attack","비공격 행동",()=>{}));
                Check("비공격 행동 출혈 없음",!log.Dealt.Any(x=>x.DamageContext.StatusVfxKey=="bleed"));
                yield return NordAction(enemy,Kind.Additional,s=>s.EnqueueAdditional(enemy,"egypt_bleed_multi","출혈 다단",()=>
                {for(int i=0;i<3;i++)isis.TakeDamage(new DamageContext(enemy,1,BaseEnums.CodeType.Normal,new(){DamageTag.TrueDamage,DamageTag.NormalAttack}));}));
                Check("추가 다단 공격 종료 출혈1틱·기간불변",log.Dealt.Count(x=>x.DamageContext.StatusVfxKey=="bleed")==1&&enemy.GetAllStatuses().First(x=>x.Key=="bleed").RemainingTurns==2);
            }
            ActionDot.Apply(set,enemy,ActionDotKind.Bleed,inheritedHit:true);
            Check("출혈 재부여 비중첩",enemy.GetAllStatuses().Count(x=>x.Key=="bleed")==1);
            blood.CastCode();set.RemoveShield(set.ShieldCurr);
            YvAddTestStatus(set,99914,"egypt_maxhp",new StatModifierEffect(BaseEnums.StatType.MaxHp,5000));
            set.ModifyHp(set.HpMax/2);int lifesteal=0;
            void Healed(Unit target,Unit source,int actual,int over){if(target==set&&source==set)lifesteal+=actual;}
            Unit.AnyHealed+=Healed;
            yield return NordAction(enemy,Kind.Additional,s=>s.EnqueueAdditional(enemy,"egypt_lifesteal","체력 투자 흡혈",()=>set.TakeDamage(new DamageContext(enemy,1,BaseEnums.CodeType.Normal,new(){DamageTag.TrueDamage}))));
            Unit.AnyHealed-=Healed;
            Check("최대체력 투자 반격 흡혈3%",lifesteal==Mathf.RoundToInt(set.HpMax*.03f));set.RemoveStatusByKey("egypt_maxhp");
            Effects.Negative.ControlStatuses.ApplyFixedStun(set,enemy,1,true);blood.ResetBudget();
            using(var log=new CfLog(AliveAllies()))
            {
                yield return NordAction(enemy,Kind.Additional,s=>s.EnqueueAdditional(enemy,"egypt_control","제어 중 피격",()=>set.TakeDamage(new DamageContext(enemy,1,BaseEnums.CodeType.Normal,new(){DamageTag.TrueDamage}))));
                Check("제어 중 반격·예산 소모 없음",blood.Budget==2&&!log.Dealt.Any(x=>x.Attacker==set&&x.DamageContext.HasTag(DamageTag.CounterAttack)));
            }
            set.RemoveStatusByKey(Effects.Negative.ControlStatuses.StunKey);set.ControlEnds();
            blood.CastCode();blood.ResetBudget();set.ModifyHp(set.HpMax/2);hp=set.HpCurr;
            yield return NordAction(set,Kind.Ultimate,s=>set.FillUltimateResource());
            Check("세트 초월 전체 출혈·체력12% 회복",AliveEnemies().All(x=>x.HasStatusKey("bleed"))&&set.HpCurr-hp==Mathf.RoundToInt(set.HpMax*.12f)&&blood.Budget==2);
            yield return CaptureStill("egyptian_core");
            carry.RemoveShield(carry.ShieldCurr);carry.TakeDamage(new DamageContext(enemy,10000000,BaseEnums.CodeType.Normal,new(){DamageTag.TrueDamage}));
            Check("공격형 후계자 없으면 초월 대상 없음",royalty.Heir==null&&!horus.ActiveUltimateCode.HasValidTarget());
            horus.FillUltimateResource();horus.CastUltimateCode();Check("대상 없는 초월 마나 보존",horus.ManaCurr==horus.ManaMax);
            royalty.StopCode();blood.StopCode();
            int deaths=0;void Died(Unit u,Unit a){if(u.IsSummon&&u.SummonOwner==isis)deaths++;}
            Unit.AnyUnitDied+=Died;isis.ActivePassiveCodes.OfType<IsisThreefoldGuard>().Single().StopCode();Unit.AnyUnitDied-=Died;
            Check("수호령 정지 사망보상 없이 정리",Summons.Of(isis).Count==0&&deaths==0&&!Summons.IsSummoner(isis));
            isis.ActivePassiveCodes.OfType<IsisThreefoldGuard>().Single().CastCode();
            isis.RemoveShield(isis.ShieldCurr);isis.TakeDamage(new DamageContext(enemy,10000000,BaseEnums.CodeType.Normal,new(){DamageTag.TrueDamage}));
            Check("이시스 사망 수호단·정책 함께 제거",!isis.isActive&&Summons.Of(isis).Count==0&&!Summons.IsSummoner(isis));
            OsFinish(enabled);
        }
        private IEnumerator ScenarioIsisCore(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();_cfActionIdSequence=0;bool enabled=true;
            yield return YvPrepareScene(config,x=>enabled=x);DebugMode.AllyInvincible=false;DebugMode.EnemyInvincible=false;
            var isis=CfAlly(125);var thoth=CfAlly(124);var pyg=CfAlly(20);var uchi=CfAlly(180);var writer=CfAlly(10);
            var records=thoth.ActivePassiveCodes.OfType<ThothLivingRecords>().Single();var language=writer.ActivePassiveCodes.OfType<ShakespeareLanguage>().Single();
            void Check(string label,bool ok)=>YvCheck(label,"충족",ok?"충족":"미충족",ok);
            var scribe=records.SummonScribe();for(int i=0;i<3;i++)Summons.Create(uchi);
            Check("이시스·피그말리온·토트·우치 보유자4",records.OwnerCount==4);
            var eligible=SummonSupportCombat.Allies(isis,true).Where(x=>x.IsSummon||x.CountsAsSummon).ToList();
            Check("행동 당김 대상 실제7기·피그말리온1",eligible.Count==8&&eligible.Contains(pyg)&&!eligible.Contains(isis)&&!eligible.Contains(thoth)&&!eligible.Contains(uchi));
            using(var log=new CfLog(AliveAllies().Concat(GridManager.Instance.summonList)))
            {
                yield return NordAction(isis,Kind.Ultimate,s=>isis.FillUltimateResource());
                Check("초월 실제 모든 소환 대상 각각1회 당김",eligible.All(x=>log.OrderAdjusted.Count(e=>e.Unit==x&&e.Advanced)==1)&&log.OrderAdjusted.All(x=>eligible.Contains(x.Unit)));
                Check("초월 정상완료",((IsisStellarProcession)isis.ActiveUltimateCode).CompletedNormally);
            }
            int before=language.Language;Unit guardian=Summons.Of(isis).First();guardian.Die(null);
            Check("손실 직후 즉시 재소환 아님",Summons.Of(isis).Count==2);
            yield return NordAction(isis,Kind.Normal,s=>s.AdvanceAction(isis,1));
            Check("자기 일반 완료 손실 슬롯 보충",Summons.Of(isis).Count==3&&Summons.Of(isis).Select(x=>x.SummonKind).Distinct().Count()==3);
            Check("보충 수호령 실제 표시 연결",Summons.Of(isis).All(x=>x.GetComponent<Entities.View.SummonCardView>()?.PortraitRenderer!=null));
            Check("보충이 기존2기 교체 안함",Summons.Of(isis).Count(x=>x.SummonKind!=guardian.SummonKind)==2);
            Check("소환 손실·보충 언어 연동",language.Language>=Mathf.Min(12,before+2));
            yield return CaptureStill("isis_core");OsFinish(enabled);
        }
        private IEnumerator ScenarioHorusTargets(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();_cfActionIdSequence=0;bool enabled=true;
            yield return YvPrepareScene(config,x=>enabled=x);DebugMode.AllyInvincible=false;DebugMode.EnemyInvincible=false;
            var horus=CfAlly(120);var agni=CfAlly(62);var surtr=CfAlly(81);var royalty=horus.ActivePassiveCodes.OfType<HorusRoyalHeir>().Single();var enemy=AliveEnemies().First();
            void Check(string label,bool ok)=>YvCheck(label,"충족",ok?"충족":"미충족",ok);
            Check("아그니 초월 피해 전환 딜러도 지정",royalty.Heir==agni&&agni.HasStatusKey(royalty.AuraKey)&&!surtr.HasStatusKey(royalty.AuraKey));
            agni.ClearUltimateResource();yield return NordAction(horus,Kind.Ultimate,s=>horus.FillUltimateResource());
            Check("아그니 마나30%·초월 피해 버프",agni.ManaCurr==Mathf.RoundToInt(agni.ManaMax*.30f)&&agni.HasStatusKey(HorusRoyalInvestiture.BuffKey));
            agni.RemoveShield(agni.ShieldCurr);agni.TakeDamage(new DamageContext(enemy,10000000,BaseEnums.CodeType.Normal,new(){DamageTag.TrueDamage}));
            Check("후계자 사망 시 다음 공격 초월기 지정",royalty.Heir==surtr&&surtr.HasStatusKey(royalty.AuraKey)&&!agni.HasStatusKey(royalty.AuraKey));
            royalty.StopCode();Check("호루스 정지 오라 제거",!surtr.HasStatusKey(royalty.AuraKey));royalty.CastCode();
            Check("호루스 재시작 생존 대상 정상지정",royalty.Heir==surtr);
            yield return CaptureStill("horus_targets");
            OsFinish(enabled);
        }
        private IEnumerator ScenarioEgyptianCombat(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();yield return PrepareStage(config,Stage(config,8),false,false,false,4);
            int bleed=0,guardians=0;void Hit(DamageResolvedContext ctx){if(ctx.DamageContext.StatusVfxKey=="bleed")bleed++;if(ctx.Attacker?.ID==125&&ctx.DamageContext.HasTag(DamageTag.SummonAttack))guardians++;}
            Unit.AnyDamageDealt+=Hit;
            try
            {
                DebugMode.SuiteRunning=false;GameManager.Instance.StartRound();yield return WaitForState(BaseEnums.GameState.RoundEnd,120);
                var end=CombatLog.Current.LastOrDefault(x=>x.Text.StartsWith("전투 종료 — "));
                YvCheck("이집트 실전 보스 전투 종료","RoundEnd",GameManager.Instance.gameState.ToString(),GameManager.Instance.gameState==BaseEnums.GameState.RoundEnd);
                YvCheck("이집트 실전 보스 승리","승리",end.Text??"없음",end.Text?.Contains("전투 종료 — 승리")==true);
                YvCheck("실전 출혈/수호령 피해","하나 이상",bleed+"/"+guardians,bleed>0||guardians>0);
                yield return CaptureStill("egyptian_combat");
            }
            finally{Unit.AnyDamageDealt-=Hit;}OsFinish(true);
        }
    }
}
#endif
