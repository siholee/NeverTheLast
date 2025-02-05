using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Enemy : Unit
{
    public override void InitProcess(bool isEnemy, int id)
    {
        level = gameManager.roundManager.ROUND;
        LoadData(isEnemy, id);
        base.InitProcess(isEnemy, id);
        SetBase();
        StatusUpdate();
    }
    // 캐릭터 데이터를 로드하는 함수
    public void LoadData(bool isEnemy, int id)
    {
        EnemyData data = gameManager.enemyDataList.enemies.FirstOrDefault(e => e.id == id);
        LoadSprite(data.portrait, isEnemy);

        if (data == null)
        {
            Debug.LogError($"적 데이터(ID: {id})를 찾을 수 없습니다.");
            return;
        }

        // 적 데이터로 Unit 속성 초기화
        base.id = data.id;
        unitName = data.name;
        
        // 체력 관련 초기화
        statHp = data.hp_base;
        growthHp = data.hp_increment;
        upgradeHp = 0;
        maxHp = statHp + growthHp;
        hpMultiplicativeBuff = 0f;
        hpAdditiveBuff = 0f;
        currentHp = maxHp;

        // 마나 관련 초기화
        maxMana = 100;
        currentMana = 0;
        manaChargeRate = 1f;
        manaChargeBuff = 0;

        // 공격력 관련 초기화
        statAtk = data.atk_base;
        growthAtk = data.atk_increment;
        upgradeAtk = 0;
        atkMultiplicativeBuff = 0f;
        atkAdditiveBuff = 0f;

        // 방어력 관련 초기화
        statDef = data.def_base;
        growthDef = data.def_increment;
        defMultiplicativeBuff = 0f;
        defAdditiveBuff = 0f;
        damageReduction = 0f;
        damageReductionBuff = 0;

        // 기타 초기화
        baseCritChance = 0f;
        critChanceBuff = 0f;
        baseCritDamage = 0f;
        critDamageBuff = 0f;

        baseCooldown = 1f;
        cooldownMultiplicativeBuff = 0f;
        cooldownAdditiveBuff = 0f;

        passiveCodeId = data.codes["passive"];
        normalCodeId = data.codes["normal"];
        ultimateCodeId = data.codes["ultimate"];
    }
}
