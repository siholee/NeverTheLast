#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Combat;
using Core;
using Effects.Buffs;
using Entities;
using UnityEngine;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioCoreNormalDeath(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();bool enabled=true;yield return YvPrepareScene(config,value=>enabled=value);
            Unit source=AliveAllies().First(unit=>unit.ID==SabahId),enemy=AliveEnemies().First();CfNoCrit(source);
            for(int i=0;i<2;i++)
            {
                bool done=false;
                void Finished(Unit actor,ActionScheduler.ActionKind kind){if(actor==source&&kind==ActionScheduler.ActionKind.Normal)done=true;}
                ActionScheduler.AnyActionFinished+=Finished;
                try {yield return CfRun(s=>s.AdvanceAction(source,1f),()=>done);}
                finally {ActionScheduler.AnyActionFinished-=Finished;}
            }
            DebugMode.AllyInvincible=false;source.RemoveShield(source.ShieldCurr);source.ModifyHp(1);
            int hits=0,resolved=0,hitEventsAfterDeath=0;bool finished=false;
            void Damage(DamageResolvedContext hit)
            {
                if(hit.Attacker!=source||!DirectAttack.IsDirect(hit.DamageContext))return;
                hits++;
                if(hits==1)source.TakeDamage(new DamageContext(enemy,100000,BaseEnums.CodeType.Effect,new List<int>{DamageTag.TrueDamage}));
            }
            void Resolved(EventContext _)=>resolved++;
            void Hit(EventContext _){if(!source.isActive)hitEventsAfterDeath++;}
            void Finish(Unit actor,ActionScheduler.ActionKind kind){if(actor==source&&kind==ActionScheduler.ActionKind.Normal)finished=true;}
            Unit.AnyDamageDealt+=Damage;source.AddListener(BaseEnums.UnitEventType.OnNormalActionResolved,(Action<EventContext>)Resolved);
            source.AddListener(BaseEnums.UnitEventType.OnNormalAttackHit,(Action<EventContext>)Hit);ActionScheduler.AnyActionFinished+=Finish;
            try
            {
                yield return CfRun(s=>s.AdvanceAction(source,1f),()=>finished);
                yield return new WaitForSeconds(2f);
                YvCheck("광역 일반행동 중 사망 후 타격·완료 이벤트 차단","최초피해1·완료0·사망후적중0",$"{hits}/{resolved}/{hitEventsAfterDeath}",!source.isActive&&hits==1&&resolved==0&&hitEventsAfterDeath==0);
                yield return CaptureStill("core_normal_death_cancelled_remaining_hits");
            }
            finally
            {
                Unit.AnyDamageDealt-=Damage;source.RemoveListener(BaseEnums.UnitEventType.OnNormalActionResolved,(Action<EventContext>)Resolved);
                source.RemoveListener(BaseEnums.UnitEventType.OnNormalAttackHit,(Action<EventContext>)Hit);ActionScheduler.AnyActionFinished-=Finish;OsFinish(enabled);
            }
        }

        private IEnumerator ScenarioCoreRemovalSafety(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();bool enabled=true;yield return YvPrepareScene(config,value=>enabled=value);
            Unit owner=AliveAllies().First(unit=>unit.ID==125),enemy=AliveEnemies().First();var pets=Summons.Of(owner);
            int removedOnDeath=0,removedOnClear=0,deaths=0;
            pets[0].AddStatus(BuffStatus.Create(997921,"qa_pet_death","검증 소환 상태",owner,pets[0],new QaLifecycleEffect(remove:()=>removedOnDeath++)));
            pets[1].AddStatus(BuffStatus.Create(997922,"qa_pet_clear","검증 소환 상태",owner,pets[1],new QaLifecycleEffect(remove:()=>removedOnClear++)));
            void Died(Unit unit,Unit attacker){if(unit.IsSummon)deaths++;}
            Unit.AnyUnitDied+=Died;
            try
            {
                pets[0].Die(null);
                YvCheck("소환 사망 폐기 시 상태 정리","OnRemove1",removedOnDeath.ToString(),removedOnDeath==1);
                Summons.ClearAll();
                YvCheck("소환 전체 정리 상태 수명·가상사망 없음","OnRemove1·추가사망0",$"{removedOnClear}/{deaths}",removedOnClear==1&&deaths==1);
            }
            finally {Unit.AnyUnitDied-=Died;}
            Unit holder=AliveAllies().First(unit=>unit!=owner);holder.TryGrantRewardCode(704);holder.CastPassiveCode();
            var code=holder.ActivePassiveCodes.First(item=>item is Codes.Passive.Reward.FighterCode);
            DebugMode.AllyInvincible=false;holder.RemoveShield(holder.ShieldCurr);
            holder.TakeDamage(new DamageContext(enemy,1000000,BaseEnums.CodeType.Effect,new List<int>{DamageTag.TrueDamage}));
            GridManager.Instance.OnRoundEnd();
            var listeners=typeof(Unit).GetField("AnyDamageDealt",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)?.GetValue(null) as Delegate;
            int retained=listeners?.GetInvocationList().Count(callback=>ReferenceEquals(callback.Target,code))??0;
            YvCheck("사망 아군 라운드 종료 정적 보상 구독 해제","죽은704 구독0",retained.ToString(),retained==0);
            yield return CaptureStill("core_removal_safety_completed");OsFinish(enabled);
        }
    }
}
#endif
