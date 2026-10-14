#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using BaseClasses;
using Core;
using Effects.Buffs;
using Entities;
using UnityEngine;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioCoreSaveSafety(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();bool enabled=true;yield return YvPrepareScene(config,value=>enabled=value);
            Unit source=AliveAllies().First(),dying=AliveAllies()[1],enemy=AliveEnemies().First();
            int id=source.ID,deadId=dying.ID,baseMax=source.HpMax;
            source.RestoreHpOutsideCombat(source.HpMax);
            source.AddStatus(BuffStatus.Create(997931,"qa_bloom_max_hp","개화 검증",source,source,
                new StatModifierEffect(BaseEnums.StatType.MaxHp,.30f,percent:true)));
            source.ModifyHp(Mathf.RoundToInt(source.HpMax*.5f));source.RecoverMana(40);int mana=source.ManaCurr;
            dying.Die(enemy);
            var end=typeof(GameManager).GetMethod("EndRound",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            end.Invoke(GameManager.Instance,new object[]{true});yield return null;
            Unit restored=GridManager.Instance.heroList.Single(unit=>unit.ID==id);
            Unit dead=GridManager.Instance.heroList.Single(unit=>unit.LastActiveId==deadId&&!unit.isActive);
            YvCheck("임시 최대HP 버프 정산 시 비율 보존","기초 최대HP의50%",$"{restored.HpCurr}/{restored.HpMax}",restored.HpMax==baseMax&&restored.HpCurr==Mathf.RoundToInt(baseMax*.5f));
            YvCheck("임시HP 정리 후 마나·사망 보존","기존마나·죽은HP0",$"{restored.ManaCurr}/{dead.HpCurr}",restored.ManaCurr==mana&&dead.HpCurr==0&&!dead.isActive);
            restored.RestoreRunState(new UnitSaveData{level=0,currentHP=restored.HpCurr,ultimateResource=mana});
            YvCheck("복원 레벨0 하한 보호","레벨1·양의 최대HP",$"{restored.Level}/{restored.HpMax}",restored.Level==1&&restored.HpMax>0);
            yield return CaptureStill("core_save_safety_hp_ratio_preserved");OsFinish(enabled);
        }
    }
}
#endif
