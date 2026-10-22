#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Normal;
using Codes.Passive;
using Codes.Ultimate;
using Combat;
using Core;
using Effects.Buffs;
using Entities;
using UnityEngine;
using Kind = Managers.ActionScheduler.ActionKind;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioSummonTankCompare(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); _cfActionIdSequence = 0;
            bool enabled = true;
            yield return YvPrepareScene(config, value => enabled = value);
            DebugMode.AllyInvincible = DebugMode.EnemyInvincible = false;
            Unit pyg = CfAlly(20), anubis = CfAlly(121), sei = CfAlly(1), orpheus = CfAlly(25);
            Unit enemy = AliveEnemies().First();
            foreach (Unit unit in AliveAllies().Concat(AliveEnemies()).ToList())
            {
                foreach (var code in unit.ActivePassiveCodes.ToList()) code.StopCode();
                unit.ActiveUltimateCode?.StopCode();
                unit.ApplyRewardCodeInventory(Enumerable.Repeat(0, 7).ToList(), new List<int>());
                unit.DebugClearStatuses(); unit.RefreshAttributes();
                unit.RemoveShield(unit.ShieldCurr); unit.RestoreHpOutsideCombat(unit.HpMax);
            }
            CfStats(pyg, "피그말리온", 3500, 315, 72, 4.8f, 62, 4.2f, 235, 18, 220, 17, 94, 100);
            CfStats(anubis, "아누비스", 3350, 300, 60, 3.5f, 50, 3, 230, 17.5f, 210, 16, 90, 120);
            // 같은 공격자의 실제 공통 피해 경로를 읽는다. 영혼·보상·본인 초월기 없는 개전 내구도다.
            float Probe(Unit target, bool dot = false)
            {
                target.RestoreHpOutsideCombat(target.HpMax);
                int pool = target.HpCurr + target.ShieldCurr;
                var tags = dot ? new List<int>() : new List<int>
                    { DamageTag.SingleTarget, DamageTag.NormalAttack, DamageTag.ContactAttack, DamageTag.Strike };
                var hit = new DamageContext(enemy, 1000, dot ? BaseEnums.CodeType.Effect : BaseEnums.CodeType.Normal, tags)
                    { IsDamageOverTime = dot };
                target.TakeDamage(hit);
                return hit.ResolvedDamage > 0 ? pool * 1000f / hit.ResolvedDamage : 0;
            }
            float pygBare = Probe(pyg), anubisBare = Probe(anubis);
            float seiBare = Probe(sei), orpheusBare = Probe(orpheus);
            float pygDot = Probe(pyg, true), anubisDot = Probe(anubis, true), orpheusDot = Probe(orpheus, true);
            YvCheck("소환 퓨어 탱커 기본 내구도 Lv." + pyg.Level, "두 탱커 > 세이·오르페우스",
                $"피그말리온 {pygBare:F1}, 아누비스 {anubisBare:F1}, 세이 {seiBare:F1}, 오르페우스 {orpheusBare:F1}",
                Mathf.Min(pygBare, anubisBare) > Mathf.Max(seiBare, orpheusBare));
            YvCheck("소환 퓨어 탱커 지속피해 내구도 Lv." + pyg.Level, "두 탱커 > 오르페우스",
                $"피그말리온 {pygDot:F1}, 아누비스 {anubisDot:F1}, 오르페우스 {orpheusDot:F1}",
                Mathf.Min(pygDot, anubisDot) > orpheusDot);
            foreach (Unit unit in AliveAllies()) unit.RestoreHpOutsideCombat(unit.HpMax);
            yield return NordAction(sei, Kind.Ultimate, scheduler => sei.FillUltimateResource());
            float supportedOrpheus = Probe(orpheus);
            YvCheck("소환 퓨어 탱커와 세이 지원 전열 비교 Lv." + pyg.Level, "본인 초월기 전 두 탱커 > 세이 보호막·경감 오르페우스",
                $"피그말리온 {pygBare:F1}, 아누비스 {anubisBare:F1}, 지원 오르페우스 {supportedOrpheus:F1}",
                Mathf.Min(pygBare, anubisBare) > supportedOrpheus);
            // 개전 방호는 성장 이정표로만 앞당긴다. 기초 스탯·영혼당 성장·흡혈은 늘리지 않는다.
            var soul = anubis.ActivePassiveCodes.OfType<AnubisSoulHarvest>().Single();
            anubis.RemoveShield(anubis.ShieldCurr);
            anubis.SetPermanentStacks(AnubisSoulHarvest.SoulKey, 199); anubis.RefreshAttributes();
            soul.CastCode();
            YvCheck("영혼199 개전 방호 미해금 Lv." + anubis.Level, "보호막0·능력량0",
                $"보호막{anubis.ShieldCurr}·능력량{soul.BaseShieldAmount}", anubis.ShieldCurr == 0 && soul.BaseShieldAmount == 0);
            int hp199 = anubis.HpMax; float def199 = anubis.StatValue(BaseEnums.StatType.Defense);
            soul.StopCode();
            anubis.SetPermanentStacks(AnubisSoulHarvest.SoulKey, 200); anubis.RefreshAttributes();
            soul.CastCode();
            float defense200 = anubis.StatValue(BaseEnums.StatType.Defense);
            int opening = Mathf.RoundToInt(defense200 * 2.7f);
            YvCheck("영혼200 개전 방호·능력량 Lv." + anubis.Level, "자신에게 방어270%·영혼1당 체력8/방어0.4",
                $"보호막{anubis.ShieldCurr}/{opening}·체력차{anubis.HpMax-hp199}·방어차{defense200-def199:F1}",
                anubis.ShieldCurr == opening && soul.BaseShieldAmount == opening && anubis.HpMax-hp199 == 8 && Mathf.Abs(defense200-def199-.4f)<.01f);
            anubis.RestoreHpOutsideCombat(anubis.HpMax); int protectedHp = anubis.HpCurr;
            anubis.TakeDamage(new DamageContext(enemy, 1000, BaseEnums.CodeType.Normal,
                new() { DamageTag.SingleTarget, DamageTag.NormalAttack, DamageTag.ContactAttack, DamageTag.Strike }));
            YvCheck("영혼 개전 보호막 실제 흡수 Lv." + anubis.Level, "체력 손실 없이 보호막 소모",
                $"체력{protectedHp}→{anubis.HpCurr}·보호막{opening}→{anubis.ShieldCurr}",
                anubis.HpCurr == protectedHp && anubis.ShieldCurr < opening);
            soul.StopCode(); anubis.SetPermanentStacks(AnubisSoulHarvest.SoulKey, 0); anubis.RefreshAttributes();
            OsFinish(enabled);
        }

        private IEnumerator ScenarioSummonAuthorsCore(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); _cfActionIdSequence = 0;
            bool enabled = true;
            yield return YvPrepareScene(config, value => enabled = value);
            DebugMode.AllyInvincible = false; DebugMode.EnemyInvincible = false;
            Unit writer=CfAlly(10), thoth=CfAlly(124), uchi=CfAlly(180), pyg=CfAlly(20), loki=CfAlly(80), enemy=AliveEnemies().First();
            var language=writer.ActivePassiveCodes.OfType<ShakespeareLanguage>().Single();
            var records=thoth.ActivePassiveCodes.OfType<ThothLivingRecords>().Single();
            CfStats(writer,"셰익스피어",1140,100,48,3,100,6,60,4.8f,84,5.4f,110,140);
            CfStats(thoth,"토트",1850,165,50,3,90,6,80,6,90,6,103,120);
            Unit fenrir=Summons.Of(loki).Single();
            void Check(string label,bool ok)=>YvCheck(label,"충족",ok?"충족":"미충족",ok);
            void SetLanguage(int value) { writer.AddCombatResource(ShakespeareLanguage.Resource,-language.Language); language.Gain(value); }
            language.StopCode(); language.CastCode();
            Check("언어 시작2·상한12",language.Language==2 && writer.GetCombatResourceMaximum(ShakespeareLanguage.Resource)==12);
            Check("현재 소환 보유자·피그말리온2",records.OwnerCount==2);
            Check("서기관 생성 전 피해경감 없음",!records.Scribe);
            for(int i=0;i<3;i++)Summons.Create(uchi);
            Check("분신 고정 속도105",Summons.Of(uchi).All(x=>x.StatValue(BaseEnums.StatType.Speed)==105));
            Check("분신3기라도 소환자1명",records.OwnerCount==3);
            Check("분신 생성마다 언어1",language.Language==5);
            // 피그말리온의 별도 전역 오라를 제외하고 토트 배율만 분리 측정한다. 자기 소환 판정은 유지한다.
            foreach(Unit ally in AliveAllies())ally.RemoveStatusByKey("pygmalion_galatea_aura");
            language.Gain(100);Check("언어 상한",language.Language==12);
            Check("공격 없는 일반은 지휘 불참",!writer.ActiveNormalCode.CanJoinCommand && !thoth.ActiveNormalCode.CanJoinCommand);
            foreach(var hero in new[]{writer,thoth})
            {
                Check(hero.UnitName+" 기초 스탯·팩토리",hero.HpMax>500 && hero.ActiveNormalCode!=null && hero.ActiveUltimateCode!=null);
                Check(hero.UnitName+" 초상화",Helpers.SpriteResource.LoadPortrait(hero.PortraitPath,out _)!=null);
            }
            float expected=Mathf.Min(.45f,records.OwnerCount*Mathf.Min(.12f,.06f+.04f*SummonSupportCombat.PowerRatio(thoth)));
            Check("보유자 수·특공 계수",Mathf.Abs(records.DamageBonus-expected)<.0001f);
            // 실제 피해 입구: 소환 태그만 증폭하며 일반 본체 공격·지속피해는 제외한다.
            int Probe(bool summon,bool dot=false)
            {
                var context=new DamageContext(uchi,100, dot?BaseEnums.CodeType.Effect:BaseEnums.CodeType.Normal,
                    summon?new(){DamageTag.TrueDamage,DamageTag.SummonAttack}:new(){DamageTag.TrueDamage}){IsDamageOverTime=dot};
                enemy.TakeDamage(context);return context.ResolvedDamage;
            }
            Check("토트 소환 피해만 증폭",Probe(true)==Mathf.RoundToInt(100*(1+expected)) && Probe(false)==100);
            Check("토트 지속피해 제외",Probe(true,true)==100);
            SetLanguage(6);
            yield return NordAction(fenrir,Kind.Additional,s=>s.EnqueueAdditional(fenrir,"authors_multihit","소환 치명 묶음",()=>
            {
                bool a=ShakespeareLanguage.TryEmpower(loki,fenrir,.4f,out float converted);
                bool b=ShakespeareLanguage.TryEmpower(loki,fenrir,.4f,out _);
                bool c=ShakespeareLanguage.TryEmpower(loki,fenrir,.4f,out _);
                Check("다단·광역 언어2 한번 소비",a&&b&&c&&language.Language==4);
                Check("기존 치확→치피",Mathf.Abs(converted-.4f)<.0001f);
            }));
            Check("소환 실제 행동 종료 언어1",language.Language==5);
            SetLanguage(1);
            yield return NordAction(fenrir,Kind.Additional,s=>s.EnqueueAdditional(fenrir,"authors_no_budget","언어 부족",()=>
                Check("언어 부족 소비 없음",!ShakespeareLanguage.TryEmpower(loki,fenrir,.4f,out _)&&language.Language==1)));
            CfNoCrit(loki); CfNoCrit(uchi); CfNoCrit(pyg);
            loki.RemoveStatusByKey("verify_cf_nocrit");
            YvAddTestStatus(loki,99863,"authors_crit40",new StatModifierEffect(BaseEnums.StatType.CritRate,.40f-loki.StatValue(BaseEnums.StatType.CritRate)));
            SetLanguage(2);
            using(var log=new CfLog(AliveAllies()))
            {
                yield return NordAction(fenrir,Kind.Normal,s=>s.AdvanceAction(fenrir,1));
                Check("펜리르 실제 확정 치명",log.Dealt.Any(x=>x.Attacker==loki && x.DamageContext.HasTag(DamageTag.SummonAttack) && x.DamageContext.IsCrit));
                int raw=Mathf.RoundToInt(Mathf.RoundToInt(loki.StatValue(BaseEnums.StatType.Attack)*.60f)*(loki.CritMultiplierCurr+.40f));
                Check("치확40%→치피40%p 실제 피해",log.Dealt.Any(x=>x.Attacker==loki&&x.DamageContext.Damage==raw));
            }
            loki.RemoveStatusByKey("authors_crit40");CfNoCrit(loki);
            SetLanguage(0);
            using(var log=new CfLog(AliveAllies()))
            {
                yield return NordAction(fenrir,Kind.Normal,s=>s.AdvanceAction(fenrir,1));
                Check("언어 없는 펜리르 비치명",log.Dealt.Any(x=>x.Attacker==loki&&!x.DamageContext.IsCrit));
            }
            SetLanguage(6);
            using(var log=new CfLog(AliveAllies()))
            {
                yield return NordAction(loki,Kind.Normal,s=>s.AdvanceAction(loki,1));
                Check("소환자 본체 공격은 언어 미소비",language.Language==6 && log.Dealt.Any(x=>x.Attacker==loki&&!x.DamageContext.IsCrit));
            }
            Unit clone=Summons.Of(uchi).First();SetLanguage(2);
            using(var log=new CfLog(AliveAllies()))
            {
                yield return NordAction(clone,Kind.Normal,s=>s.AdvanceAction(clone,1));
                Check("우치 실제 분신 확정 치명·소멸",log.Dealt.Any(x=>x.Attacker==uchi&&x.DamageContext.IsCrit)&&!clone.isActive);
            }
            Check("분신 사망·완료 언어 각각1",language.Language==2);
            SetLanguage(6);
            using(var log=new CfLog(AliveAllies()))
            {
                yield return NordAction(pyg,Kind.Normal,s=>s.AdvanceAction(pyg,1));
                var attacks=log.Dealt.Where(x=>x.Attacker==pyg&&x.DamageContext.CodeType==BaseEnums.CodeType.Normal).ToList();
                Check("피그말리온 자기 소환 취급 실제 치명",attacks.Count==1 && attacks.All(x=>x.DamageContext.IsCrit));
                Check("피그말리온 언어2·완료1",language.Language==5);
            }
            int cloneCount=Summons.Of(uchi).Count;SetLanguage(12);
            using(var log=new CfLog(AliveAllies()))
            {
                yield return NordAction(uchi,Kind.Ultimate,s=>uchi.FillUltimateResource());
                var hits=log.Dealt.Where(x=>x.Attacker==uchi&&x.DamageContext.HasTag(DamageTag.UltAttack)).ToList();
                Check("우치 초월 여러 분신 각각 확정 치명",hits.Count==cloneCount&&hits.All(x=>x.DamageContext.IsCrit));
                Check("여러 분신 각각2소비·소멸1획득",language.Language==12-cloneCount);
            }
            thoth.ModifyHp(thoth.HpMax/2);int hp=thoth.HpCurr, mana=thoth.ManaCurr;
            yield return NordAction(thoth,Kind.Normal,s=>s.AdvanceAction(thoth,1));
            Check("토트 최대체력24% 치유·마나20",thoth.HpCurr-hp==Mathf.RoundToInt(thoth.HpMax*.24f)&&thoth.ManaCurr==mana+20);
            SetLanguage(0);
            yield return NordAction(writer,Kind.Normal,s=>s.AdvanceAction(writer,1));
            Check("셰익스피어 실제 일반 언어3",language.Language==3);
            yield return NordAction(thoth,Kind.Ultimate,s=>thoth.FillUltimateResource());
            Unit scribe=records.Scribe;
            Check("서기관 생성·수명3·단일",scribe!=null && records.Remaining==3 && Summons.Of(thoth).Count==1);
            yield return null;
            var scribeView=scribe.GetComponent<Entities.View.SummonCardView>();
            Check("피격 가능 서기관 원화·체력 표시",scribeView!=null&&scribeView.PortraitRenderer.enabled&&scribeView.HasHealthDisplay&&Mathf.Abs(scribeView.DisplayedHealthRatio-scribe.HpCurr/(float)scribe.HpMax)<.001f);
            yield return CaptureStill("scribe_visible_health");
            Check("서기관 생성 언어1",language.Language==4);
            Check("서기관 속도90·체력50%·피격 가능",scribe!=null && scribe.StatValue(BaseEnums.StatType.Speed)==90 && scribe.HpMax==Mathf.RoundToInt(thoth.HpMax*.50f)&&!scribe.IsUntargetable);
            Check("서기관 방·특방80%",Mathf.Abs(scribe.StatValue(BaseEnums.StatType.Defense)-thoth.DefCurr*.8f)<.01f && Mathf.Abs(scribe.StatValue(BaseEnums.StatType.SpecialDefense)-thoth.StatValue(BaseEnums.StatType.SpecialDefense)*.8f)<.01f);
            Check("서기관 생성·분신 소멸 현재 보유자3",records.OwnerCount==3);
            Check("실제 적 공격 후보에 서기관 포함",CombatTargets.AliveEnemies(enemy).Contains(scribe));
            Check("대상 불가 분신·펜리르 제외",!CombatTargets.AliveEnemies(enemy).Contains(fenrir));
            int priority=scribe.Priority;scribe.Priority=3;enemy.currentNormalTarget=null;CfNoCrit(enemy);
            using(var log=new CfLog(new[]{enemy,scribe}))
            {
                yield return NordAction(enemy,Kind.Normal,s=>s.AdvanceAction(enemy,1));
                Check("적 AI 일반공격이 서기관 지정·실제 피격",log.Dealt.Any(x=>x.Attacker==enemy&&x.Target==scribe));
            }
            scribe.Priority=priority;
            hp=thoth.HpCurr;
            thoth.TakeDamage(new DamageContext(enemy,100,BaseEnums.CodeType.Normal,new(){DamageTag.TrueDamage}));
            Check("서기관 존재 중 피해24% 경감",hp-thoth.HpCurr==76);
            thoth.ModifyHp(thoth.HpMax/4);hp=thoth.HpCurr;
            int scribeLanguage=language.Language;
            yield return NordAction(scribe,Kind.Normal,s=>s.AdvanceAction(scribe,1));
            Check("서기관 독립 일반·최저 체력 치유",thoth.HpCurr-hp==Mathf.RoundToInt(thoth.HpMax*.20f));
            Check("비공격 소환 행동은 언어 미소비·완료1",language.Language==Mathf.Min(12,scribeLanguage+1));
            int before=language.Language;
            yield return NordAction(thoth,Kind.Ultimate,s=>thoth.FillUltimateResource());
            Check("서기관 재시전 생성·언어 없음",records.Scribe==scribe && language.Language==before && records.Remaining==3);
            yield return NordAction(writer,Kind.Ultimate,s=>writer.FillUltimateResource());
            Check("결계 생성·3회 일반 시계",writer.HasStatusKey(ShakespeareQuestion.FieldKey)&&writer.GetAllStatuses().First(x=>x.Key==ShakespeareQuestion.FieldKey).RemainingTurns==3);
            Check("결계 소환 피해 추가 증폭",Probe(true)>Mathf.RoundToInt(100*(1+records.DamageBonus)));
            Check("결계 일반 본체 공격 제외",Probe(false)==100);
            Unit capClone=Summons.Create(uchi);
            YvAddTestStatus(thoth,99861,"authors_high_sp",new StatModifierEffect(BaseEnums.StatType.SpecialAttack,20000));
            YvAddTestStatus(writer,99862,"authors_high_sp",new StatModifierEffect(BaseEnums.StatType.SpecialAttack,20000));
            Check("토트 특공 과투자 피해 상한45%",Mathf.Abs(records.DamageBonus-.45f)<.0001f);
            Check("결계 특공 과투자 피해 상한65%",Probe(true)==Mathf.RoundToInt(100*1.45f*1.65f));
            thoth.RemoveStatusByKey("authors_high_sp");writer.RemoveStatusByKey("authors_high_sp");capClone.Die(null);
            yield return CaptureStill("summon_authors_field");
            int savedHeals=0;void SavedHeal(Unit x,Unit source,int actual,int over){if(source==writer)savedHeals++;}
            Unit.AnyHealed+=SavedHeal;
            scribe.TakeDamage(new DamageContext(enemy,1000000,BaseEnums.CodeType.Normal,new(){DamageTag.TrueDamage}));
            Unit.AnyHealed-=SavedHeal;
            Check("치명상 방지는 치유 자원 발행 없음",savedHeals==0);
            Check("서기관 치명상1회·정확한 체력30%",scribe.isActive && scribe.HpCurr==Mathf.Max(1,Mathf.RoundToInt(scribe.HpMax*.30f)));
            // 결계 갱신은 사용한 개체의 치명상 방지 횟수를 되돌리지 않는다.
            yield return NordAction(writer,Kind.Ultimate,s=>writer.FillUltimateResource());
            scribe.TakeDamage(new DamageContext(enemy,1000000,BaseEnums.CodeType.Normal,new(){DamageTag.TrueDamage}));
            Check("결계 재시전에도 동일 개체 두번째 사망",!scribe.isActive&&records.Scribe==null&&records.OwnerCount==2);
            hp=thoth.HpCurr;thoth.TakeDamage(new DamageContext(enemy,100,BaseEnums.CodeType.Normal,new(){DamageTag.TrueDamage}));
            Check("서기관 사망 즉시 경감 종료",hp-thoth.HpCurr==100);
            var newScribe=records.SummonScribe();
            Check("새 개체는 새 치명상 방지",newScribe!=scribe);
            newScribe.TakeDamage(new DamageContext(enemy,1000000,BaseEnums.CodeType.Normal,new(){DamageTag.TrueDamage}));
            Check("새 서기관 치명상 방지",newScribe.isActive);
            // 피그말리온은 공격 지원을 받지만 아군 본체를 부활시키지는 않는다.
            Check("소환수 취급 본체 치명상 방지 제외",!language.TrySave(pyg));
            for(int i=0;i<3;i++)yield return NordAction(thoth,Kind.Normal,s=>s.AdvanceAction(thoth,1));
            Check("서기관 자기 일반3회 수명 소멸",records.Scribe==null);
            for(int i=0;i<3;i++)yield return NordAction(writer,Kind.Normal,s=>s.AdvanceAction(writer,1));
            Check("결계 자기 일반3회 만료",!writer.HasStatusKey(ShakespeareQuestion.FieldKey));
            records.SummonScribe();
            int deaths=0;void Died(Unit x,Unit a){if(x.IsSummon&&x.SummonOwner==thoth)deaths++;}
            Unit.AnyUnitDied+=Died;
            records.StopCode();Unit.AnyUnitDied-=Died;
            Check("토트 외부 정지 소환·오라·사망보상 정리",records.Scribe==null&&records.DamageBonus==0&&deaths==0&&AliveAllies().All(x=>!x.HasStatusKey(records.AuraKey)));
            records.CastCode();language.StopCode();language.CastCode();
            Check("재시작 언어·소환 정책 정상",language.Language==2&&Summons.IsSummoner(thoth));
            records.SummonScribe();
            thoth.TakeDamage(new DamageContext(enemy,1000000,BaseEnums.CodeType.Normal,new(){DamageTag.TrueDamage}));
            Check("소환자 사망 시 서기관·오라 제거",!thoth.isActive&&Summons.Of(thoth).Count==0&&records.DamageBonus==0);
            language.StopCode();Check("언어 정지 자원0",language.Language==0);
            using(var log=new CfLog(AliveAllies()))
            {
                yield return NordAction(fenrir,Kind.Normal,s=>s.AdvanceAction(fenrir,1));
                Check("서포터 정지 후 기존 치명 경로",log.Dealt.Any(x=>x.Attacker==loki&&!x.DamageContext.IsCrit));
            }
            yield return CaptureStill("summon_authors_core");
            OsFinish(enabled);
        }
        private IEnumerator ScenarioSummonAuthorsCombat(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();
            yield return PrepareStage(config,Stage(config,8),false,false,false,4);
            var writer=CfAlly(10);var thoth=CfAlly(124);
            int summonCrit=0, heal=0, turns=0;
            void Hit(DamageResolvedContext c){if(c.DamageContext.HasTag(DamageTag.SummonAttack)&&c.DamageContext.IsCrit)summonCrit++;}
            void Heal(Unit target,Unit source,int actual,int over){if(source==thoth&&actual>0)heal++;}
            void End(Unit actor,Kind kind){if((actor==writer||actor==thoth)&&kind==Kind.Normal)turns++;}
            Unit.AnyDamageDealt+=Hit;Unit.AnyHealed+=Heal;ActionScheduler.AnyActionFinished+=End;
            try
            {
                DebugMode.SuiteRunning=false;GameManager.Instance.StartRound();
                yield return WaitForState(BaseEnums.GameState.RoundEnd,120);
                var terminal=CombatLog.Current.LastOrDefault(e=>e.Text.StartsWith("전투 종료 — "));
                YvCheck("소환 파티 실제 보스 전투 종료","RoundEnd",GameManager.Instance.gameState.ToString(),GameManager.Instance.gameState==BaseEnums.GameState.RoundEnd);
                YvCheck("소환 파티 보스 승리","승리",terminal.Text??"없음",terminal.Text?.Contains("전투 종료 — 승리")==true);
                YvCheck("2인 실제 일반·소환 치명·유효 치유","모두 발동",turns+"/"+summonCrit+"/"+heal,turns>1&&summonCrit>0&&heal>0);
                yield return CaptureStill("summon_authors_combat");
            }
            finally{Unit.AnyDamageDealt-=Hit;Unit.AnyHealed-=Heal;ActionScheduler.AnyActionFinished-=End;}
            OsFinish(true);
        }
    }
}
#endif
