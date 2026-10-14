#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Normal;
using Codes.Passive;
using Codes.Ultimate;
using Combat;
using Core;
using Effects.Buffs;
using Effects.Negative;
using Entities;
using UnityEngine;
using Kind = Managers.ActionScheduler.ActionKind;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private void OsFinish(bool enabled)
        {
            GameManager.Instance.enabled=enabled;
            if (_yvFailures.Count>0) FailCurrent(string.Join(" / ",_yvFailures));
            _yvFailures.Clear();
        }
        private IEnumerator ScenarioOrionSkadiCore(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); _cfActionIdSequence = 0;
            bool enabled = true;
            yield return YvPrepareScene(config, value => enabled = value);
            DebugMode.AllyInvincible = false; DebugMode.EnemyInvincible = false;
            Unit orion = CfAlly(22), skadi = CfAlly(83), enemy = AliveEnemies().First();
            void Check(string label, bool ok, string actual = "") => YvCheck(label, "기획 사양", actual, ok);
            if (orion == null || skadi == null) { Check("신규 편성", false); OsFinish(enabled); yield break; }
            var sirius = orion.ActivePassiveCodes.OfType<OrionSirius>().Single();
            var mantle = skadi.ActivePassiveCodes.OfType<SkadiFrostMantle>().Single();
            Check("2인 특성·일반·초월 등록", orion.ActiveNormalCode is OrionHunt && orion.ActiveUltimateCode is OrionStarPiercer &&
                skadi.ActiveNormalCode is SkadiSnowHunt && skadi.ActiveUltimateCode is SkadiIcicleSpike);
            Check("원소·기본 사용·마나", orion.Element == "Geo" && skadi.Element == "Cryo" && orion.ManaMax == 130 && skadi.ManaMax == 130);
            Check("스카디 반격·도발 태그 없음", !skadi.HasArchetypeTag("Counter") && !skadi.HasArchetypeTag("Taunt"));
            Check("오리온 초기 충전0", sirius.Charges == 0);
            Check("스카디 개전 보호막", skadi.ShieldCurr>0);
            Check("원화 4종", new[]{"ORION_PORTRAIT","ORION_STANDING","SKADI_PORTRAIT","SKADI_STANDING"}.All(key => (key.EndsWith("_PORTRAIT") ? Helpers.SpriteResource.LoadPortrait(key) : Helpers.SpriteResource.LoadStanding(key)) != null));
            // 보호막의 출처·소진·외부 제거와 속성 재계산을 실제 API로 확인한다.
            skadi.RemoveShield(skadi.ShieldCurr); float bareAtk = skadi.StatValue(BaseEnums.StatType.Attack);
            skadi.AddShield(100, orion);
            Check("외부 보호막 공격력15%", Near(skadi.StatValue(BaseEnums.StatType.Attack), bareAtk * 1.15f));
            skadi.AddShield(100, orion);
            Check("보호막 중복 공격력 중복 없음", Near(skadi.StatValue(BaseEnums.StatType.Attack), bareAtk * 1.15f));
            skadi.RemoveShield(skadi.ShieldCurr);
            Check("보호막 제거 즉시 공격력 복원", Near(skadi.StatValue(BaseEnums.StatType.Attack), bareAtk));
            skadi.AddShield(1, orion);
            skadi.TakeDamage(new DamageContext(enemy, 10, BaseEnums.CodeType.Normal, new(){DamageTag.NormalAttack,DamageTag.TrueDamage}));
            Check("보호막 피격 소진 즉시 복원", skadi.ShieldCurr == 0 && Near(skadi.StatValue(BaseEnums.StatType.Attack), bareAtk));
            skadi.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved, new EventContext(skadi));
            Check("일반 완료 방어력60% 보호막", skadi.ShieldCurr == Mathf.RoundToInt(skadi.DefCurr * .6f));
            int shield = skadi.ShieldCurr;
            skadi.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved, new EventContext(skadi));
            Check("보호막 누적", skadi.ShieldCurr == shield * 2);
            YvAddTestStatus(skadi,99851,"os_hp",new StatModifierEffect(BaseEnums.StatType.MaxHp,5000));
            shield = skadi.ShieldCurr;
            skadi.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved,new EventContext(skadi));
            Check("최대체력 증가가 보호막 계수에 미반영", skadi.ShieldCurr-shield==Mathf.RoundToInt(skadi.DefCurr*.6f));
            skadi.RemoveStatusByKey("os_hp");
            // 실제 행동을 실행한다. 임시 명중/비치명 보정은 계수와 부착 경로만 분리 검증한다.
            CfNoCrit(skadi); CfNoCrit(orion); CfEnsureEffectHit(skadi,AliveEnemies());
            using (var hits = new CfLog(AliveAllies()))
            {
                yield return NordAction(skadi,Kind.Normal,s=>s.AdvanceAction(skadi,1));
                var hit=hits.Dealt.FirstOrDefault(x=>x.Attacker==skadi);
                Check("스카디 단일 일반 피해·사격", hit!=null && hit.DamageContext.HasTag(DamageTag.NormalAttack) && hit.DamageContext.HasTag(DamageTag.Shot));
                Check("스카디 얼음 부착 성공", hits.Dealt.Where(x=>x.Attacker==skadi).Any(x=>x.Target.HasCombatElement(BaseEnums.UnitElement.Cryo)));
            }
            using (var hits = new CfLog(AliveAllies()))
            {
                yield return NordAction(skadi,Kind.Ultimate,s=>skadi.AddUltimateResource(skadi.ManaMax));
                Check("스카디 초월기 광역·정상 완료", hits.Dealt.Count(x=>x.Attacker==skadi)==AliveEnemies().Count && ((SkadiIcicleSpike)skadi.ActiveUltimateCode).CompletedNormally);
                Check("스카디 광역 얼음", AliveEnemies().All(x=>x.HasCombatElement(BaseEnums.UnitElement.Cryo)));
            }
            // 피격만 있으면 시리우스가 찬다. 보호막 피격과 다단(같은 행동 ID)을 함께 확인한다.
            orion.AddShield(10000,skadi); int hp=orion.HpCurr;
            using (var hits=new CfLog(AliveAllies()))
            {
                yield return NordAction(enemy,Kind.Additional,s=>s.EnqueueAdditional(enemy,"os_multi","다단 피격",()=>
                { for(int i=0;i<3;i++) orion.TakeDamage(new DamageContext(enemy,1,BaseEnums.CodeType.Normal,new(){DamageTag.NormalAttack,DamageTag.TrueDamage})); }));
                Check("보호막 다단 피격 행동당 충전1", sirius.Charges==1 && orion.HpCurr==hp);
            }
            yield return NordAction(enemy,Kind.Additional,s=>s.EnqueueAdditional(enemy,"os_exclude","간접 피해 제외",()=>
            {
                orion.TakeDamage(new DamageContext(enemy,1,BaseEnums.CodeType.Effect,new()){IsDamageOverTime=true});
                orion.TakeDamage(new DamageContext(enemy,1,BaseEnums.CodeType.Passive,new(){DamageTag.BonusDamage,DamageTag.TrueDamage}));
                HealthCost.Pay(orion,1);
            }));
            Check("지속·덧붙음·자해 충전 제외", sirius.Charges==1);
            for(int i=0;i<3;i++)
            {
                int index=i;
                yield return NordAction(enemy,Kind.Additional,s=>s.EnqueueAdditional(enemy,"os_hit_"+index,"시리우스 충전",()=>
                    orion.TakeDamage(new DamageContext(enemy,1,BaseEnums.CodeType.Normal,new(){DamageTag.NormalAttack,DamageTag.TrueDamage}))));
            }
            Check("서로 다른 공격4회 충전4", sirius.Charges==4);
            using (var hits=new CfLog(AliveAllies()))
            {
                yield return NordAction(enemy,Kind.Additional,s=>s.EnqueueAdditional(enemy,"os_fifth","다섯 번째 피격",()=>
                    orion.TakeDamage(new DamageContext(enemy,1,BaseEnums.CodeType.Normal,new(){DamageTag.NormalAttack,DamageTag.TrueDamage}))));
                var rain=hits.Dealt.Where(x=>x.Attacker==orion && x.DamageContext.HasTag(DamageTag.CounterAttack)).ToList();
                Check("5회 피격 광역 반격·추공", rain.Count==AliveEnemies().Count && rain.All(x=>x.DamageContext.HasTag(DamageTag.AdditionalAttack) && x.DamageContext.HasTag(DamageTag.AllTarget)));
                Check("화살비 소비·예약 정리", sirius.Charges==0 && !sirius.Pending);
            }
            ControlStatuses.ApplyFixedStun(orion,enemy,1,true);
            sirius.Gain(5); sirius.Gain(10);
            Check("제어 중 충전 보관·상한5", orion.isControlled && sirius.Charges==5 && !sirius.Pending);
            using (var hits=new CfLog(AliveAllies()))
            {
                yield return NordAction(enemy,Kind.Additional,s=>s.EnqueueAdditional(enemy,"os_recover","제어 해제 후 행동",()=>
                { orion.RemoveStatusByKey(ControlStatuses.StunKey); orion.ControlEnds(); }));
                Check("제어 해제 후 단일 화살비", hits.Dealt.Count(x=>x.Attacker==orion && x.DamageContext.HasTag(DamageTag.CounterAttack))==AliveEnemies().Count && sirius.Charges==0);
            }
            orion.RemoveShield(orion.ShieldCurr); orion.ModifyHp(orion.HpMax/2);
            hp=orion.HpCurr;
            using (var hits=new CfLog(AliveAllies()))
            {
                yield return NordAction(orion,Kind.Ultimate,s=>orion.AddUltimateResource(orion.ManaMax));
                Check("오리온 단일 초월·충전2·치유", hits.Dealt.Count(x=>x.Attacker==orion)==1 && sirius.Charges==2 && orion.HpCurr>hp && ((OrionStarPiercer)orion.ActiveUltimateCode).CompletedNormally);
            }
            sirius.Gain(1);
            using (var hits=new CfLog(AliveAllies()))
            {
                yield return NordAction(orion,Kind.Ultimate,s=>orion.AddUltimateResource(orion.ManaMax));
                Check("초월기 충전이 화살비 개방", hits.Dealt.Count(x=>x.Attacker==orion && x.DamageContext.HasTag(DamageTag.CounterAttack))==AliveEnemies().Count && sirius.Charges==0);
            }
            float atk=skadi.StatValue(BaseEnums.StatType.Attack);
            mantle.StopCode();
            Check("스카디 정지 버프·구독 정리", !skadi.HasStatusKey(SkadiFrostMantle.StatusKey) && skadi.StatValue(BaseEnums.StatType.Attack)<atk);
            mantle.CastCode();
            shield=skadi.ShieldCurr;
            skadi.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved,new EventContext(skadi));
            Check("스카디 재시작 중복 구독 없음", skadi.ShieldCurr-shield==Mathf.RoundToInt(skadi.DefCurr*.6f));
            sirius.Gain(2); sirius.StopCode();
            Check("오리온 정지 자원·예약 정리", sirius.Charges==0 && !sirius.Pending);
            sirius.CastCode();
            yield return NordAction(enemy,Kind.Additional,s=>s.EnqueueAdditional(enemy,"os_restart","재시작 피격",()=>
                orion.TakeDamage(new DamageContext(enemy,1,BaseEnums.CodeType.Normal,new(){DamageTag.NormalAttack,DamageTag.TrueDamage}))));
            Check("오리온 재시작 중복 구독 없음", sirius.Charges==1);
            yield return CaptureStill("orion_skadi_core");
            OsFinish(enabled);
        }
        private IEnumerator ScenarioOrionSkadiCombat(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();
            yield return PrepareStage(config,Stage(config,6),false,false,false,4);
            int rain=0, ice=0;
            void Hit(DamageResolvedContext context)
            { if(context.Attacker?.ID==22 && context.DamageContext.HasTag(DamageTag.CounterAttack))rain++; if(context.Attacker?.ID==83)ice++; }
            Unit.AnyDamageDealt+=Hit;
            try
            {
                DebugMode.SuiteRunning=false;
                GameManager.Instance.StartRound();
                yield return WaitForState(BaseEnums.GameState.RoundEnd,120);
                YvCheck("2인 실전 자동 전투 완료", "RoundEnd",GameManager.Instance.gameState.ToString(),GameManager.Instance.gameState==BaseEnums.GameState.RoundEnd);
                var terminal=CombatLog.Current.LastOrDefault(e=>e.Text.StartsWith("전투 종료 — "));
                YvCheck("실전 승리", "전투 종료 — 승리",terminal.Text ?? "기록 없음",terminal.Text?.Contains("전투 종료 — 승리")==true);
                YvCheck("실전 얼음 공격·화살비", "둘 모두 피해",ice+"/"+rain,ice>0 && rain>0);
                yield return CaptureStill("orion_skadi_combat");
            }
            finally { Unit.AnyDamageDealt-=Hit; }
            OsFinish(true);
        }
    }
}
#endif
