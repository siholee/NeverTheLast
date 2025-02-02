using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Enemy : Unit
{
    public int LEVEL; // 적 레벨

    public override void InitProcess(bool isHero, int id)
    {
        base.InitProcess(isHero, id);
        LoadData(isHero, id);
        StatusUpdate();
    }
    // 캐릭터 데이터를 로드하는 함수
    public void LoadData(bool isHero, int id)
    {
        // 라운드별 스탯 증가량 반영 필요
        EnemyData data = gameManager.enemyDataList.enemies.FirstOrDefault(e => e.id == id);
        LoadSprite(data.portrait, isHero);

        if (data == null)
        {
            Debug.LogError($"적 데이터(ID: {id})를 찾을 수 없습니다.");
            return;
        }

        // 적 데이터로 Unit 속성 초기화
        base.id = data.id;
        unitName = data.name;

        // 체력 관련 초기화
        baseHp = SetBase(data.hp_base, LEVEL, data.hp_increment);
        hpMultiplicativeBuff = 0f;
        hpAdditiveBuff = 0f;
        currentHp = baseHp;

        // 공격력 관련 초기화
        baseAtk = SetBase(data.atk_base, LEVEL, data.atk_increment);
        atkMultiplicativeBuff = 0f;
        atkAdditiveBuff = 0f;
        atk = baseAtk;

        // 방어력 관련 초기화
        baseDef = SetBase(data.def_base, LEVEL, data.def_increment);
        defMultiplicativeBuff = 0f;
        defAdditiveBuff = 0f;
        def = baseDef;

        // 기타 초기화
        baseCritChance = 0f;
        critChanceBuff = 0f;
        baseCritDamage = 0f;
        critDamageBuff = 0f;

        baseCooldown = 0f;
        cooldownMultiplicativeBuff = 0f;
        cooldownAdditiveBuff = 0f;

        passiveCodeId = data.codes["passive"];
        normalCodeId = data.codes["normal"];
        ultimateCodeId = data.codes["ultimate"];
    }
   
    public int SetBase(int n, int lv, int increase){
        return n + lv * increase;
    }
}
