#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Passive;
using Codes.Base;
using Effects.Buffs;
using Effects.Negative;
using Effects.Base;
using Effects.Vfx;
using Combat;
using Core;
using Entities;
using UnityEngine;
namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private sealed class TrioUltimateCritical : BaseEffect
        {
            public TrioUltimateCritical():base(0,0){}
            public override bool ForcesCriticalAgainst(Unit attacker,Unit target,BaseEnums.CodeType kind)=>kind==BaseEnums.CodeType.Ultimate;
        }
        private sealed class TrioPriority : BaseEffect
        {
            public TrioPriority():base(0,0){}
            public override int TargetPriorityAdditiveModifier(Unit unit)=>100;
        }
        private sealed class TrioSummonPolicy : ISummonerPolicy
        {
            public SummonStyle Style=>SummonStyle.Swarm;
            public int Cap=>1;
            public SummonSpec Spec=>new SummonSpec{Id=999,Kind="verify_trio",Name="검증 소환",Element="Anemo",PortraitKey="VAYU_PORTRAIT",Hp=1,Speed=70,NormalCodeId=500,Targetable=false};
            public void OnOverflow(Unit owner,Unit oldest)=>oldest.Die(null);
        }
        // 각 독립 스케줄러 fixture에서 수리야의 행동 ID 기록도 함께 초기화한다.
        private IEnumerator TrioAction(Unit owner, ActionScheduler.ActionKind kind, Action<ActionScheduler> setup,
            Action<YvRecorder> inspect, Func<YvRecorder, bool> extra = null)
        {
            foreach (Unit unit in AliveAllies().Concat(AliveEnemies())) unit.ClearUltimateResource();
            foreach (Unit unit in AliveAllies())
                foreach (var code in unit.ActivePassiveCodes.OfType<SuryaBeforeSunset>()) { code.StopCode(); code.CastCode(); }
            using var recorder = new YvRecorder();
            yield return YvRunScheduled(setup, () => (recorder.Finished.Any(x => x.Unit == owner && x.Kind == kind) || ((owner == null || !owner.isActive) && recorder.Started.Any(x => x.Unit == owner && x.Kind == kind)))
                && GameManager.Instance.ActionScheduler.ExecutingUnit == null
                && !AliveAllies().Any(x => x.isCasting)
                && GameManager.Instance.ActionScheduler.DebugQueuedCount(ActionScheduler.ActionKind.Additional) == 0
                && (extra == null || extra(recorder)));
            inspect(recorder);
        }
        private IEnumerator ScenarioVedicTrioCore(VerifyScenarioRequest config)
        {
            bool enabled = true;
            yield return YvPrepareScene(config, x => enabled = x);
            Unit vayu = AliveAllies().First(x => x.ID == 64), agni = AliveAllies().First(x => x.ID == 62), surya = AliveAllies().First(x => x.ID == 67);
            YvAddTestStatus(surya,99922,"verify_trio_mark_hit",new StatModifierEffect(BaseEnums.StatType.SpecialAttack,100000));
            var sun = surya.ActivePassiveCodes.OfType<SuryaBeforeSunset>().Single();
            sun.StopCode(); sun.CastCode();
            var fire = agni.ActivePassiveCodes.OfType<AgniBurnToTheEnd>().Single();
            Unit target = sun.ResolveTarget();
            int bullets = sun.Bullets;
            YvProbe(vayu, target, BaseEnums.CodeType.Normal, 100);
            RecordMechanic("수리야 비실행 탐침 배제", bullets.ToString(), sun.Bullets.ToString(), bullets == sun.Bullets);
            yield return TrioAction(vayu, ActionScheduler.ActionKind.Normal, s => s.AdvanceAction(vayu, 1f), r =>
            {
                RecordMechanic("바유 실제 N 단일", "1", r.Incoming.Count(x => x.Attacker == vayu && x.CodeType == BaseEnums.CodeType.Normal).ToString(), r.Incoming.Count(x => x.Attacker == vayu && x.CodeType == BaseEnums.CodeType.Normal) == 1);
            });
            foreach (Unit enemy in AliveEnemies()) YvResetEnemy(enemy);
            yield return TrioAction(vayu, ActionScheduler.ActionKind.Ultimate, s => vayu.FillUltimateResource(), r =>
            {
                RecordMechanic("바유 U 전체 실제 피해", AliveEnemies().Count.ToString(), r.Incoming.Count(x => x.Attacker == vayu && x.CodeType == BaseEnums.CodeType.Ultimate).ToString(), r.Incoming.Count(x => x.Attacker == vayu && x.CodeType == BaseEnums.CodeType.Ultimate) == AliveEnemies().Count);
                foreach (Unit enemy in AliveEnemies())
                {
                    RecordMechanic("바유 Defense 전용 침식 " + enemy.GetEntityId(), "침식+SpDef 불변", enemy.HasStatusKey(VayuErosion.Key).ToString(), enemy.HasStatusKey(VayuErosion.Key) && Near(enemy.StatValue(BaseEnums.StatType.SpecialDefense), enemy.BaseStat(BaseEnums.StatType.SpecialDefense)));
                }
            });
            yield return TrioAction(vayu, ActionScheduler.ActionKind.Special, s => s.EnqueueSpecial(vayu, "trio_core", vayu.ActiveSpecialCode.CodeName, vayu.CastSpecialCode), r =>
            {
                foreach (Unit enemy in AliveEnemies())
                    RecordMechanic("바유 SP 단일 전속성 상태 " + enemy.GetEntityId(), "1 / 2턴", string.Join(",", enemy.GetAllStatuses().Where(x => x.Key == VayuVulnerability.GaleAllKey).Select(x => x.RemainingTurns)), enemy.GetAllStatuses().Count(x => x.Key == VayuVulnerability.GaleAllKey && x.RemainingTurns == 2) == 1);
            });
            foreach (Unit enemy in AliveEnemies()) YvResetEnemy(enemy);
            int atUltimateBullets = -1; bool atUltimateStrong = false;
            Action<Unit, ActionScheduler.ActionKind> snapshot = (unit, kind) =>
            {
                if (unit == surya && kind == ActionScheduler.ActionKind.Ultimate) { atUltimateBullets = sun.Bullets; atUltimateStrong = sun.Empowered; }
            };
            ActionScheduler.AnyActionFinished += snapshot;
            yield return TrioAction(surya, ActionScheduler.ActionKind.Ultimate, s => surya.FillUltimateResource(), r =>
            {
                RecordMechanic("수리야 실제 U 강탄+재장전", "2 / 강탄1", atUltimateBullets + "/" + atUltimateStrong, atUltimateBullets == 2 && atUltimateStrong);
                RecordMechanic("아그니 실제 아군U 추가행동", "Additional 1", r.Finished.Count(x => x.Unit == agni && ActionScheduler.IsAdditional(x.Kind)).ToString(), r.Finished.Count(x => x.Unit == agni && ActionScheduler.IsAdditional(x.Kind)) == 1);
            });
            ActionScheduler.AnyActionFinished -= snapshot;
            RecordMechanic("수리야 실제 강탄 연쇄 소비", "1 / 강탄0", sun.Bullets + "/" + sun.Empowered, sun.Bullets == 1 && !sun.Empowered);
            yield return TrioAction(surya, ActionScheduler.ActionKind.Normal, s => s.AdvanceAction(surya, 1f), r =>
            {
                RecordMechanic("수리야 실제 N 재장전", "2", sun.Bullets.ToString(), sun.Bullets == 2);
            });
            yield return TrioAction(agni, ActionScheduler.ActionKind.Ultimate, s => agni.FillUltimateResource(), r =>
            {
                RecordMechanic("아그니 U 판돈", "3", fire.StakesLeft.ToString(), fire.StakesLeft == 3);
                RecordMechanic("아그니 U 전원 오라", AliveAllies().Count.ToString(), AliveAllies().Count(x => x.HasStatusKey(AgniBurnToTheEnd.StakesKey)).ToString(), AliveAllies().All(x => x.HasStatusKey(AgniBurnToTheEnd.StakesKey)));
            });
            for (int i = 2; i >= 0; i--)
            {
                int expected = i;
                yield return TrioAction(agni, ActionScheduler.ActionKind.Normal, s => s.AdvanceAction(agni, 1f), r => RecordMechanic("아그니 일반완료 판돈 " + expected, expected.ToString(), fire.StakesLeft.ToString(), fire.StakesLeft == expected));
            }
            RecordMechanic("아그니 판돈 종료 오라 정리", "0", AliveAllies().Count(x => x.HasStatusKey(AgniBurnToTheEnd.StakesKey)).ToString(), !AliveAllies().Any(x => x.HasStatusKey(AgniBurnToTheEnd.StakesKey)));
            // 실제 행동 번호를 쓰는 다단/복수 예약: 한 행동 두 타격은 한 탄환, 별도 두 행동은 두 탄환.
            foreach (int actions in new[] { 1, 2 })
            {
                yield return TrioAction(vayu, ActionScheduler.ActionKind.Additional, scheduler =>
                {
                    for (int i = 0; i < actions; i++) scheduler.EnqueueAdditional(vayu, "probe_" + i, "수리야 다단 검증", () =>
                    {
                        Unit wanted = sun.ResolveTarget();
                        YvProbe(vayu, wanted, BaseEnums.CodeType.Normal, 100);
                        YvProbe(vayu, wanted, BaseEnums.CodeType.Normal, 100);
                    });
                }, r =>
                {
                    int shots = r.Finished.Count(x => x.Unit == surya && ActionScheduler.IsAdditional(x.Kind));
                    RecordMechanic("수리야 다단 원시 실제 피해 " + actions,"양의 실제 피해",string.Join(",",r.Dealt.Where(x=>x.Attacker==vayu).Select(x=>x.DamageDealt)),r.Dealt.Count(x=>x.Attacker==vayu && x.DamageDealt>0)==actions*2);
                    RecordMechanic("수리야 실제 " + actions + "행동 다단 중복/예약", actions.ToString(), shots.ToString(), shots == actions && sun.Bullets == 2 - actions);
                    RecordMechanic("수리야 0탄 표식 유지", "수배 유지", (sun.Wanted != null).ToString(), sun.Wanted != null);
                });
            }
            // 취소된 실제 U와 정상 타격 후 제어된 U를 서로 구분한다.
            foreach (int mode in new[] { 0, 1, 2 })
            {
                fire.StopCode(); fire.CastCode(); surya.ControlEnds();
                int currentMode = mode;
                Action<Unit> cancel = unit =>
                {
                    if (unit != surya) return;
                    if (currentMode == 0) unit.ActiveUltimateCode.StopCode();
                    if (currentMode == 1) unit.ApplyControlAtLeast(vayu, 1);
                };
                Action<DamageResolvedContext> afterDamage = hit =>
                {
                    if (currentMode == 2 && hit.Attacker == surya && hit.DamageContext.CodeType == BaseEnums.CodeType.Ultimate) surya.ApplyControlAtLeast(vayu, 1);
                };
                Unit.AnyUltimateCast += cancel; Unit.AnyDamageDealt += afterDamage;
                yield return TrioAction(surya, ActionScheduler.ActionKind.Ultimate, scheduler => surya.FillUltimateResource(), r =>
                {
                    int hits = r.Incoming.Count(x => x.Attacker == surya && x.CodeType == BaseEnums.CodeType.Ultimate);
                    int tickets = r.Finished.Count(x => x.Unit == agni && ActionScheduler.IsAdditional(x.Kind));
                    int expected = currentMode == 2 ? 1 : 0;
                    RecordMechanic("실제 U 취소/선제제어/타격후제어 " + currentMode, expected + " hit/ticket", hits + "/" + tickets, hits == expected && tickets == expected);
                });
                Unit.AnyUltimateCast -= cancel; Unit.AnyDamageDealt -= afterDamage;
                surya.ControlEnds();
            }
            fire.StopCode();fire.CastCode();
            bool duringVfx=false;
            Action<VfxSlotPlaybackEvent> controlInVfx=slot=>
            {
                if(slot.Caster==vayu && slot.SkillKey=="vayu.storm" && slot.SlotName=="cast" && slot.Phase=="start")
                {
                    duringVfx=true;vayu.ApplyControlAtLeast(agni,1);
                }
            };
            VfxComposer.SlotPlayed+=controlInVfx;
            yield return TrioAction(vayu,ActionScheduler.ActionKind.Ultimate,scheduler=>vayu.FillUltimateResource(),r=>
            {
                int hits=r.Incoming.Count(x=>x.Attacker==vayu && x.CodeType==BaseEnums.CodeType.Ultimate);
                int tickets=r.Finished.Count(x=>x.Unit==agni && ActionScheduler.IsAdditional(x.Kind));
                RecordMechanic("바유 실제VFX시작후 제어0피해U 완료배제","cast진입/0hit/0ticket",duringVfx+"/"+hits+"/"+tickets,duringVfx && hits==0 && tickets==0 && !((BaseUltimateCode)vayu.ActiveUltimateCode).CompletedNormally);
            });
            VfxComposer.SlotPlayed-=controlInVfx;
            vayu.ControlEnds();
            // 실제 TakeDamage로 최고 취약만 적용되는지, 반응/지속/Bonus가 증폭되지 않는지 비교한다.
            target = sun.ResolveTarget();
            YvResetEnemy(target);
            YvAddTestStatus(vayu, 99921, "verify_trio_effect_hit", new StatModifierEffect(BaseEnums.StatType.SpecialAttack, 100000));
            var direct = new List<int> { DamageTag.NormalAttack, DamageTag.NonContactAttack, DamageTag.Shot };
            int Probe(List<int> tags, BaseEnums.CodeType type)
            {
                var context = new DamageContext(agni, 10000, type, tags, false);
                target.TakeDamage(context); return context.ResolvedDamage;
            }
            int plain = Probe(direct, BaseEnums.CodeType.Normal);
            int dotPlain = Probe(new List<int>(), BaseEnums.CodeType.Effect);
            int reactionPlain = Probe(new List<int>{DamageTag.ElementalReaction}, BaseEnums.CodeType.Effect);
            int bonusPlain = Probe(new List<int>{DamageTag.BonusDamage}, BaseEnums.CodeType.Passive);
            VayuVulnerability.Apply(vayu, target, BaseEnums.UnitElement.Pyro, false);
            int baseHit = Probe(direct, BaseEnums.CodeType.Normal);
            VayuVulnerability.ApplyAll(vayu, target);
            int galeHit = Probe(direct, BaseEnums.CodeType.Normal);
            RecordMechanic("바유 취약 실제15%/max25%", "1.15/1.25", (baseHit/(float)plain).ToString("F3") + "/" + (galeHit/(float)plain).ToString("F3"), Near(baseHit/(float)plain,1.15f) && Near(galeHit/(float)plain,1.25f));
            RecordMechanic("바유 지속/반응/Bonus 이중증폭 배제", "동일", "실피해 비교", Probe(new List<int>(),BaseEnums.CodeType.Effect)==dotPlain && Probe(new List<int>{DamageTag.ElementalReaction},BaseEnums.CodeType.Effect)==reactionPlain && Probe(new List<int>{DamageTag.BonusDamage},BaseEnums.CodeType.Passive)==bonusPlain);
            target.BeginTurn();
            VayuVulnerability.ApplyAll(vayu,target);
            RecordMechanic("바유 단일 취약 지속 갱신", "2턴·1상태", target.GetStatus(VayuVulnerability.GaleStatusId).RemainingTurns.ToString(), target.GetStatus(VayuVulnerability.GaleStatusId).RemainingTurns==2 && target.GetAllStatuses().Count(x=>x.Key==VayuVulnerability.GaleAllKey)==1);
            vayu.RemoveStatusByKey("verify_trio_effect_hit");
            // 살아 있는 표적을 예약한 뒤 그 행동 안에서 처치하면 대기 응답은 다음 생존 적을 쏜다.
            Unit deadWanted = null;
            yield return TrioAction(vayu, ActionScheduler.ActionKind.Additional, scheduler => scheduler.EnqueueAdditional(vayu,"trio_retarget","표적 처치",()=>
            {
                deadWanted = sun.ResolveTarget();
                YvProbe(vayu, deadWanted, BaseEnums.CodeType.Normal,100);
                deadWanted.TakeDamage(new DamageContext(vayu, 10000000, BaseEnums.CodeType.Effect, new List<int>{DamageTag.TrueDamage},false));
            }), r=>
            {
                var shots=r.IncomingTargets.Where(x=>x.Context.Attacker==surya && x.Context.HasTag(DamageTag.AdditionalAttack)).ToList();
                RecordMechanic("수리야 표적 사망 대기 응답 재표적", "살아 있는 다른 적 1회", shots.Count.ToString(), shots.Count==1 && shots[0].Target!=deadWanted && sun.Wanted!=deadWanted);
            });
            foreach(Unit enemy in AliveEnemies())YvResetEnemy(enemy);
            YvAddTestStatus(vayu,99924,"verify_trio_spread_hit",new StatModifierEffect(BaseEnums.StatType.SpecialAttack,100000));
            Unit original=CombatTargets.AliveEnemies(vayu).OrderByDescending(x=>x.Priority).First();
            original.GrantCombatElement(BaseEnums.UnitElement.Hydro,3,vayu,suppressReaction:true);
            vayu.currentNormalTarget=original;
            ElementalReaction.SpreadResult spread=null;
            Action<ElementalReaction.SpreadResult> spreadTrace=result=>{if(result.Source==vayu)spread=result;};
            ElementalReaction.AnySpreadResolved+=spreadTrace;
            yield return TrioAction(vayu,ActionScheduler.ActionKind.Normal,scheduler=>scheduler.AdvanceAction(vayu,1f),r=>
            {
                bool matching=spread!=null && spread.OriginalTarget==original && spread.Element==BaseEnums.UnitElement.Hydro && spread.Delivered.All(x=>x.HasStatusKey(VayuVulnerability.BaseKey(BaseEnums.UnitElement.Hydro))) && original.HasStatusKey(VayuVulnerability.BaseKey(BaseEnums.UnitElement.Hydro));
                RecordMechanic("바유 실제 확산 전달목록 취약","원래+실전달 성공 목록",spread==null?"없음":spread.Delivered.Count.ToString(),matching);
            });
            ElementalReaction.AnySpreadResolved-=spreadTrace;
            VayuVulnerability.ApplyAll(vayu,original);
            var preserved=original.GetStatus(VayuVulnerability.GaleStatusId);
            var randomState=UnityEngine.Random.state;
            YvAddTestStatus(original,99925,"verify_trio_resist",new StatModifierEffect(BaseEnums.StatType.SpecialDefense,100000000));
            UnityEngine.Random.InitState(4321);
            bool failed=!VayuVulnerability.ApplyAll(vayu,original);
            UnityEngine.Random.state=randomState;
            RecordMechanic("바유 공통 효과명중 실패 기존취약 보존","실패+기존같은객체",failed.ToString(),failed && ReferenceEquals(preserved,original.GetStatus(VayuVulnerability.GaleStatusId)));
            original.RemoveStatusByKey("verify_trio_resist");
            RecordMechanic("바유 None 취약 제외","0",VayuVulnerability.BonusFor(original,BaseEnums.UnitElement.None).ToString(),VayuVulnerability.BonusFor(original,BaseEnums.UnitElement.None)==0f);
            vayu.RemoveStatusByKey("verify_trio_spread_hit");
            Unit indra=AliveAllies().First(x=>x.ID==IndraId);
            fire.StopCode();fire.CastCode();
            int released=0,focused=0;
            Action<Unit,Unit,AgniBurnToTheEnd.TicketKind> holdTrace=(owner,other,kind)=>
            {
                if(owner!=agni)return;
                if(kind==AgniBurnToTheEnd.TicketKind.ReleasedBasic)released++;
                if(kind==AgniBurnToTheEnd.TicketKind.ReleasedFocused)focused++;
            };
            IEnumerator StopSpecialLater(){yield return null;agni.ActiveSpecialCode.StopCode();}
            Action<Unit,ActionScheduler.ActionKind,string> stopSpecial=(owner,kind,label)=>
            {
                if(owner==agni && kind==ActionScheduler.ActionKind.Special)StartCoroutine(StopSpecialLater());
            };
            AgniBurnToTheEnd.AnyTicket+=holdTrace;ActionScheduler.AnyActionStarted+=stopSpecial;
            yield return TrioAction(indra,ActionScheduler.ActionKind.Ultimate,scheduler=>indra.FillUltimateResource(),r=>
            {
                int fired=r.Finished.Count(x=>x.Unit==agni && ActionScheduler.IsAdditional(x.Kind));
                RecordMechanic("아그니 SP 외부취소 보류표 일회 기본복귀","basic1/focused0/P1",released+"/"+focused+"/"+fired,released==1 && focused==0 && fired==1 && !((Codes.Special.AgniFirstToBurn)agni.ActiveSpecialCode).HasHeldTicket);
            },r=>r.Finished.Count(x=>x.Kind==ActionScheduler.ActionKind.Special)>=SpecialAction.Participants(indra).Count);
            AgniBurnToTheEnd.AnyTicket-=holdTrace;ActionScheduler.AnyActionStarted-=stopSpecial;
            fire.StopCode();fire.CastCode();
            foreach(Unit enemy in AliveEnemies())YvResetEnemy(enemy);
            var allies=AliveAllies().Where(x=>x!=agni).ToList();
            int totalTickets=0;
            for(int phase=0;phase<2;phase++)
            {
                if(phase==1)
                {
                    yield return TrioAction(agni,ActionScheduler.ActionKind.Ultimate,scheduler=>agni.FillUltimateResource(),r=>{});
                    YvAddTestStatus(agni,99926,"verify_trio_ult_crit",new TrioUltimateCritical());
                }
                foreach(Unit unit in AliveAllies().Concat(AliveEnemies()))unit.ClearUltimateResource();
                sun.StopCode();sun.CastCode();
                using(var r=new YvRecorder())
                {
                    yield return YvRunScheduled(scheduler=>{foreach(Unit ally in allies)ally.FillUltimateResource();},()=>
                        allies.All(ally=>r.Finished.Any(x=>x.Unit==ally&&x.Kind==ActionScheduler.ActionKind.Ultimate)) &&
                        r.Finished.Count(x=>x.Unit==agni&&ActionScheduler.IsAdditional(x.Kind))>=4 &&
                        GameManager.Instance.ActionScheduler.ExecutingUnit==null &&
                        GameManager.Instance.ActionScheduler.DebugQueuedCount(ActionScheduler.ActionKind.Special)==0 &&
                        GameManager.Instance.ActionScheduler.DebugQueuedCount(ActionScheduler.ActionKind.Additional)==0);
                    int count=r.Finished.Count(x=>x.Unit==agni&&ActionScheduler.IsAdditional(x.Kind));totalTickets+=count;
                    RecordMechanic("아그니 U초기화 전후 실제4티켓 " + phase,"4",count.ToString(),count==4);
                    if(phase==1)
                    {
                        var hits=r.Incoming.Where(x=>x.Attacker==agni&&x.HasTag(DamageTag.AdditionalAttack)).ToList();
                        RecordMechanic("아그니 Ult-only 치명 질의 실제P","모든P UltTag+IsCrit",hits.Count.ToString(),hits.Count>0&&hits.All(x=>x.IsCrit&&x.HasTag(DamageTag.UltAttack)));
                    }
                }
            }
            agni.RemoveStatusByKey("verify_trio_ult_crit");
            RecordMechanic("아그니 네아군 Ureset전후8티켓 loss/dupe","8",totalTickets.ToString(),totalTickets==8);
            foreach(Unit enemy in AliveEnemies())YvResetEnemy(enemy);
            sun.StopCode();sun.CastCode();
            Unit wantedSummon=sun.ResolveTarget();
            YvAddTestStatus(wantedSummon,99927,"verify_trio_summon_target",new TrioPriority());
            Summons.Register(vayu,new TrioSummonPolicy());
            Unit clone=Summons.Create(vayu);
            yield return TrioAction(clone,ActionScheduler.ActionKind.Normal,scheduler=>scheduler.AdvanceAction(clone,1f),r=>
            {
                var summoned=r.Dealt.Where(x=>x.Attacker==vayu&&x.DamageContext.HasTag(DamageTag.SummonAttack)).ToList();
                int shots=r.Finished.Count(x=>x.Unit==surya&&ActionScheduler.IsAdditional(x.Kind));
                RecordMechanic("수리야 실제 소환행동/소환자피해 귀속","소환hit1+응답1",summoned.Count+"/"+shots,summoned.Count==1&&summoned[0].Target==sun.Wanted&&shots==1);
            });
            wantedSummon.RemoveStatusByKey("verify_trio_summon_target");
            Summons.Unregister(vayu);
            // 광역 첫 피격의 반격으로 시전자가 실제 사망해도 뒤의 피해/후처리를 열지 않는다.
            foreach(Unit enemy in AliveEnemies())YvResetEnemy(enemy);
            bool wasInvincible=DebugMode.AllyInvincible;
            DebugMode.AllyInvincible=false;
            bool killed=false;
            Action<DamageResolvedContext> killCaster=hit=>
            {
                if(killed || hit.Attacker!=vayu || hit.DamageContext.CodeType!=BaseEnums.CodeType.Special)return;
                killed=true;
                vayu.TakeDamage(new DamageContext(hit.Target,10000000,BaseEnums.CodeType.Effect,new List<int>{DamageTag.TrueDamage},false));
            };
            Unit.AnyDamageDealt+=killCaster;
            yield return TrioAction(vayu,ActionScheduler.ActionKind.Special,scheduler=>scheduler.EnqueueSpecial(vayu,"trio_ghost","시전자 사망",vayu.CastSpecialCode),r=>
            {
                int hits=r.Incoming.Count(x=>x.Attacker==vayu && x.CodeType==BaseEnums.CodeType.Special);
                RecordMechanic("바유 SP 실제 사망 광역후속/취약 취소","피격1+취약0",hits+"/"+AliveEnemies().Count(x=>x.HasStatusKey(VayuVulnerability.GaleAllKey)),killed && !vayu.isActive && hits==1 && !AliveEnemies().Any(x=>x.HasStatusKey(VayuVulnerability.GaleAllKey)));
            });
            Unit.AnyDamageDealt-=killCaster;
            DebugMode.AllyInvincible=wasInvincible;
            bool killedSun=false;
            Action<VfxSlotPlaybackEvent> killSun=slot=>
            {
                if(killedSun || slot.Caster!=surya || slot.SlotName!="cast" || slot.Phase!="start" || !slot.SkillKey.StartsWith("surya."))return;
                killedSun=true;
                surya.TakeDamage(new DamageContext(AliveEnemies().First(),10000000,BaseEnums.CodeType.Effect,new List<int>{DamageTag.TrueDamage},false));
            };
            DebugMode.AllyInvincible=false;
            VfxComposer.SlotPlayed+=killSun;
            yield return TrioAction(agni,ActionScheduler.ActionKind.Additional,scheduler=>scheduler.EnqueueAdditional(agni,"trio_sun_death","사격도중사망",()=>YvProbe(agni,sun.ResolveTarget(),BaseEnums.CodeType.Normal,1000)),r=>
            {
                int hits=r.Incoming.Count(x=>x.Attacker==surya && x.HasTag(DamageTag.AdditionalAttack));
                RecordMechanic("수리야 실제 사격cast중 사망 latecallback 취소","실사망/추공hit0/탄0강탄0",killedSun+"/"+hits,killedSun && !surya.isActive && hits==0 && sun.Bullets==0 && !sun.Empowered && sun.Wanted==null);
            });
            VfxComposer.SlotPlayed-=killSun;
            DebugMode.AllyInvincible=wasInvincible;
            surya.RemoveStatusByKey("verify_trio_mark_hit");
            yield return EndOfFrame;
            Capture("trio_core_final");
            // 사망 fixture는 실제 공용 전투후 복원 API로 다음 독립 시나리오에 오염을 남기지 않는다.
            vayu.ReviveAfterBattle();
            surya.ReviveAfterBattle();
            GameManager.Instance.gameState = BaseEnums.GameState.Preparation;
            GameManager.Instance.enabled = enabled;
        }
        private IEnumerator ScenarioVedicTrioBalance(VerifyScenarioRequest config)
        {
            bool enabled = true;
            yield return YvPrepareScene(config, x => enabled = x);
            Unit vayu = AliveAllies().First(x => x.ID == 64), agni = AliveAllies().First(x => x.ID == 62), surya = AliveAllies().First(x => x.ID == 67);
            foreach (Unit unit in new[] { vayu, agni, surya })
            {
                float[] bases = unit.ID == 64 ? new float[]{1000,80,100,60,75,108,5,50} : unit.ID==62 ? new float[]{1100,100,85,70,65,96,5,50} : new float[]{960,108,70,60,60,90,5,50};
                float[] growths = unit.ID == 64 ? new float[]{90,4.8f,6.2f,4.5f,5.2f,0,0,0} : unit.ID==62 ? new float[]{100,6.5f,5.3f,5.2f,4.8f,0,0,0} : new float[]{88,6.8f,4,4.5f,4.5f,0,0,0};
                for(int stat=0;stat<8;stat++)
                {
                    float expectedStat=bases[stat]+growths[stat]*(unit.Level-1);
                    if(stat>=6)expectedStat/=100f;
                    RecordMechanic("실제8스탯 성장 " + unit.ID + " Lv" + unit.Level + " " + (BaseEnums.StatType)stat,expectedStat.ToString("F2"),unit.BaseStat((BaseEnums.StatType)stat).ToString("F2"),Near(unit.BaseStat((BaseEnums.StatType)stat),expectedStat));
                }
                float atk = unit.ID == 64 ? 80f : unit.ID == 62 ? 100f : 108f;
                float growth = unit.ID == 64 ? 4.8f : unit.ID == 62 ? 6.5f : 6.8f;
                float expected = atk + growth * (unit.Level - 1);
                RecordMechanic("실제 성장 ATK " + unit.ID + " Lv" + unit.Level, expected.ToString("F2"), unit.BaseStat(BaseEnums.StatType.Attack).ToString("F2"), Near(unit.BaseStat(BaseEnums.StatType.Attack), expected));
            }
            foreach (int id in new[] { 724, 804, 803, 723 }) RecordMechanic("아그니 실제 보상 코드 " + id, "습득", agni.TryGrantRewardCode(id).ToString(), agni.ActivePassiveCodes.Any(x => x.CatalogId == id));
            foreach (int id in new[] { 724, 805 }) RecordMechanic("수리야 실제 보상 코드 " + id, "습득", surya.TryGrantRewardCode(id).ToString(), surya.ActivePassiveCodes.Any(x => x.CatalogId == id));
            RecordMechanic("바유 실제 원소 침식712", "습득", vayu.TryGrantRewardCode(712).ToString(), vayu.ActivePassiveCodes.Any(x => x.CatalogId == 712));
            foreach (Unit unit in new[] { agni, surya, vayu })
                foreach (var code in unit.ActivePassiveCodes.Where(x => x.CatalogId >= 500)) code.CastCode();
            RecordMechanic("803 사전준비 실제 시작마나", "반마나", agni.ManaCurr.ToString(), agni.ManaCurr == agni.ManaMax / 2);
            Unit indra = AliveAllies().First(x => x.ID == IndraId);
            yield return TrioAction(indra, ActionScheduler.ActionKind.Ultimate, s => indra.FillUltimateResource(), r =>
            {
                RecordMechanic("보상 편성 로카팔라 전체 완료", "모든 SP 완료", r.Finished.Count(x => x.Kind == ActionScheduler.ActionKind.Special).ToString(), r.Finished.Count(x => x.Kind == ActionScheduler.ActionKind.Special) >= SpecialAction.Participants(indra).Count);
                var hits = r.Dealt.Where(x => x.Attacker == agni && x.DamageContext.HasTag(DamageTag.AdditionalAttack)).ToList();
                RecordMechanic("724 추가행동 완료 마나", "5", agni.ManaCurr.ToString(), agni.ManaCurr == 5);
                RecordMechanic("아그니 보상 집중 추공 실제피해 Lv" + agni.Level, "양의 실제 피해", string.Join(",", hits.Select(x => x.DamageDealt)), hits.Count > 0 && hits.All(x => x.DamageDealt > 0));
                int basicRaw=Mathf.RoundToInt(agni.StatValue(BaseEnums.StatType.Attack)*0.45f);
                int centerRaw=Mathf.RoundToInt(agni.StatValue(BaseEnums.StatType.Attack)*1.8f);
                var raw=r.Incoming.Where(x=>x.Attacker==agni && x.HasTag(DamageTag.AdditionalAttack)).ToList();
                bool rawValid=raw.Count==AliveEnemies().Count && raw.All(x=>x.Damage==Mathf.RoundToInt((x.Damage>=centerRaw?centerRaw:basicRaw)*(x.IsCrit?1f+agni.StatValue(BaseEnums.StatType.CritDamage):1f)));
                RecordMechanic("아그니 중심180/나머지45 실제 원시계수", "전체 대상·계수일치", string.Join(",",raw.Select(x=>x.Damage)),rawValid);
                RecordMechanic("아그니 보상 추공 실제행동 분리", "Additional, actualU 아님", r.Finished.Count(x => x.Unit == agni && ActionScheduler.IsAdditional(x.Kind)).ToString(), r.Finished.Any(x => x.Unit == agni && ActionScheduler.IsAdditional(x.Kind)) && !r.Finished.Any(x => x.Unit == agni && x.Kind == ActionScheduler.ActionKind.Ultimate));
            }, r => r.Finished.Count(x => x.Kind == ActionScheduler.ActionKind.Special) >= SpecialAction.Participants(indra).Count);
            foreach (int gift in new[] {10,0})
            {
                yield return TrioAction(agni, ActionScheduler.ActionKind.Ultimate, scheduler=>agni.FillUltimateResource(),r=>
                {
                    RecordMechanic("804 실제 U 마나 한번 제한 " + gift,gift.ToString(),vayu.ManaCurr.ToString(),vayu.ManaCurr==gift);
                });
            }
            // 태그 별 실제 처리 배율. 치명/경감 없이 보상과 판돈만 비교한다.
            var sun = surya.ActivePassiveCodes.OfType<SuryaBeforeSunset>().Single();
            sun.StopCode();
            Unit target=AliveEnemies().First();
            YvResetEnemy(target);
            foreach (int mode in new[]{0,1,2})
            {
                var tags=new List<int>{DamageTag.TrueDamage,DamageTag.NonContactAttack,DamageTag.Spell};
                if(mode!=1)tags.Add(DamageTag.AdditionalAttack);
                if(mode!=0)tags.Add(DamageTag.UltAttack);
                var context=new DamageContext(agni,10000,BaseEnums.CodeType.Passive,tags,false);
                target.TakeDamage(context);
                int expected=mode==0?12500:mode==1?15000:18750;
                RecordMechanic("판돈+724+723 실제 태그 배율 " + mode,expected.ToString(),context.ResolvedDamage.ToString(),context.ResolvedDamage==expected);
            }
            sun.CastCode();
            foreach(Unit enemy in AliveEnemies())YvResetEnemy(enemy);
            YvAddTestStatus(vayu,99923,"verify_trio_hacker_hit",new StatModifierEffect(BaseEnums.StatType.SpecialAttack,100000));
            var before = AliveEnemies().ToDictionary(x=>x,x=>(normal:YvProbe(vayu,x,BaseEnums.CodeType.Normal,10000),dot:YvProbe(vayu,x,BaseEnums.CodeType.Effect,10000)));
            Unit hackerTarget=null;
            yield return TrioAction(vayu,ActionScheduler.ActionKind.Normal,scheduler=>scheduler.AdvanceAction(vayu,1f),r=>
            {
                hackerTarget=r.IncomingTargets.First(x=>x.Context.Attacker==vayu && x.Context.CodeType==BaseEnums.CodeType.Normal).Target;
                float k=UnitStats.ArmorConstant(vayu.Level),def=hackerTarget.StatValue(BaseEnums.StatType.Defense),spDef=hackerTarget.StatValue(BaseEnums.StatType.SpecialDefense);
                float normal=YvProbe(vayu,hackerTarget,BaseEnums.CodeType.Normal,10000)/(float)before[hackerTarget].normal;
                float dot=YvProbe(vayu,hackerTarget,BaseEnums.CodeType.Effect,10000)/(float)before[hackerTarget].dot;
                RecordMechanic("712 실제 원소 부착 양방어 감소", "공식비율",normal.ToString("F3")+"/"+dot.ToString("F3"),Near(normal,(k+def)/(k+def*0.8f)) && Near(dot,(k+spDef)/(k+spDef*0.8f)));
                VayuErosion.Apply(vayu,hackerTarget,0.2f,2);
                normal=YvProbe(vayu,hackerTarget,BaseEnums.CodeType.Normal,10000)/(float)before[hackerTarget].normal;
                dot=YvProbe(vayu,hackerTarget,BaseEnums.CodeType.Effect,10000)/(float)before[hackerTarget].dot;
                RecordMechanic("712+바유 침식 직접/지속 실피해", "Def0.64/SpDef0.8",normal.ToString("F3")+"/"+dot.ToString("F3"),Near(normal,(k+def)/(k+def*0.64f)) && Near(dot,(k+spDef)/(k+spDef*0.8f)));
            });
            vayu.RemoveStatusByKey("verify_trio_hacker_hit");
            yield return EndOfFrame;
            Capture("trio_balance_L" + agni.Level);
            GameManager.Instance.gameState = BaseEnums.GameState.Preparation;
            GameManager.Instance.enabled = enabled;
        }
    }
}
#endif
