#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Combat;
using Core;
using Entities;
using UnityEngine;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioCoreElementsSafety(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();bool enabled=true;yield return YvPrepareScene(config,value=>enabled=value);
            DebugMode.AllyInvincible=DebugMode.EnemyInvincible=false;
            Unit source=AliveAllies().First(unit=>unit.ID==25),indra=AliveAllies().First(unit=>unit.ID==63),enemy=AliveEnemies().First();
            CfNoCrit(source);CfNoCrit(indra);enemy.RemoveShield(enemy.ShieldCurr);
            var baseDamage=new Dictionary<ActionDotKind,int>();
            foreach(var kind in new[]{ActionDotKind.Shock,ActionDotKind.Burn,ActionDotKind.Poison,ActionDotKind.Curse})
            {
                ActionDot.Apply(source,enemy,kind,inheritedHit:true);
                var effect=enemy.GetAllStatuses().SelectMany(status=>status.Effects).Select(item=>item.EffectObject).OfType<ActionDotEffect>().First(item=>item.Kind==kind);
                var damage=effect.CreateDamageOverTimeTick();damage.DefenseStatMultiplier=0;enemy.TakeDamage(damage);baseDamage[kind]=damage.ResolvedDamage;
                enemy.RemoveStatusByKey(ActionDot.Key(kind));
            }
            foreach(var code in new[]{812,903})
            {
                source.TryGrantRewardCode(code);source.CastPassiveCode();
                float bonus=code==812?.25f:.45f;
                foreach(var kind in new[]{ActionDotKind.Shock,ActionDotKind.Burn,ActionDotKind.Poison,ActionDotKind.Curse})
                {
                    ActionDot.Apply(source,enemy,kind,inheritedHit:true);
                    var effect=enemy.GetAllStatuses().SelectMany(status=>status.Effects).Select(item=>item.EffectObject).OfType<ActionDotEffect>().First(item=>item.Kind==kind);
                    var damage=effect.CreateDamageOverTimeTick();damage.DefenseStatMultiplier=0;enemy.TakeDamage(damage);
                    int expected=Mathf.RoundToInt(baseDamage[kind]*(kind is ActionDotKind.Shock or ActionDotKind.Burn?1+bonus:1));
                    YvCheck($"{code} 반응·직접 지속피해 분류 {kind}",expected.ToString(),damage.ResolvedDamage.ToString(),Mathf.Abs(damage.ResolvedDamage-expected)<=1);
                    enemy.RemoveStatusByKey(ActionDot.Key(kind));
                }
            }
            source.RemoveRewardCode(812);source.RemoveRewardCode(903);
            foreach(Unit target in AliveEnemies()){target.ClearToughness();target.GrantToughness(150,source);}
            int before=enemy.ToughnessCurr;bool finished=false;
            void Finished(Unit actor,ActionScheduler.ActionKind kind){if(actor==indra&&kind==ActionScheduler.ActionKind.Special)finished=true;}
            ActionScheduler.AnyActionFinished+=Finished;
            try {yield return CfRun(s=>s.EnqueueSpecial(indra,"qa_vajra",indra.ActiveSpecialCode.CodeName,indra.CastSpecialCode),()=>finished);}
            finally {ActionScheduler.AnyActionFinished-=Finished;}
            YvCheck("인드라 특수 광역 강인도 삭감","15",(before-enemy.ToughnessCurr).ToString(),before-enemy.ToughnessCurr==15);
            for(int i=0;i<5;i++)enemy.TakeDamage(new DamageContext(source,1,BaseEnums.CodeType.Normal,new List<int>{DamageTag.TrueDamage,DamageTag.NormalAttack,DamageTag.SingleTarget}));
            YvCheck("빙결 회복 fixture 격파","강인도0",enemy.ToughnessCurr.ToString(),enemy.ToughnessCurr==0);
            enemy.isControlled=true;enemy.controlTurns=2;int turns=enemy.TurnCount;
            yield return YvRunScheduled(s=>s.AdvanceAction(enemy,1f),()=>enemy.TurnCount>turns);
            YvCheck("행동불능 턴에는 강인도 회복 없음","강인도0",enemy.ToughnessCurr.ToString(),enemy.ToughnessCurr==0);
            bool normalFinished=false;int atNormal=-1;
            void Started(Unit actor,ActionScheduler.ActionKind kind,string label){if(actor==enemy&&kind==ActionScheduler.ActionKind.Normal)atNormal=enemy.ToughnessCurr;}
            void NormalFinished(Unit actor,ActionScheduler.ActionKind kind){if(actor==enemy&&kind==ActionScheduler.ActionKind.Normal)normalFinished=true;}
            ActionScheduler.AnyActionStarted+=Started;ActionScheduler.AnyActionFinished+=NormalFinished;
            try {yield return CfRun(s=>s.AdvanceAction(enemy,1f),()=>normalFinished);}
            finally {ActionScheduler.AnyActionStarted-=Started;ActionScheduler.AnyActionFinished-=NormalFinished;}
            YvCheck("해제 후 실제 일반행동 시작 강인도 복원","150",atNormal.ToString(),atNormal==150);
            yield return CaptureStill("core_elements_safety_completed");OsFinish(enabled);
        }
    }
}
#endif
