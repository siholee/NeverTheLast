using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Hero : Unit
{
    public int[] synergies;
    public override void InitProcess(bool isEnemy, int id)
    {
        level = 1;
        LoadData(isEnemy, id);
        base.InitProcess(isEnemy, id);
        SetBase();
        StatusUpdate();
        Debug.LogWarning($"{unitName} 생성 완료");
        GameManager.Instance.unitManager.SetUnitCooldown(this);
    }
    // 캐릭터 데이터를 로드하는 함수
    public void LoadData(bool isEnemy, int id)
    {
        // 라운드별 스탯 증가량 반영 필요
        HeroData data = GameManager.Instance.heroDataList.heroes.FirstOrDefault(e => e.id == id);
        LoadSprite(data.portrait, isEnemy);

        if (data == null)
        {
            Debug.LogError($"적 데이터(ID: {id})를 찾을 수 없습니다.");
            return;
        }

        // 적 데이터로 Unit 속성 초기화
        base.id = data.id;
        unitName = data.name;
        synergies = data.synergies;

        // 체력 관련 초기화
        statHp = data.hp;
        growthHp = data.hp_increase;
        upgradeHp = 0;
        maxHp = statHp + growthHp;
        hpMultiplicativeBuff = 0f;
        hpAdditiveBuff = 0f;
        currentHp = maxHp;

        // 마나 관련 초기화
        maxMana = data.mana;
        currentMana = 0;
        manaChargeRate = 1f;
        manaChargeBuff = 0;

        // 공격력 관련 초기화
        statAtk = data.atk;
        growthAtk = data.atk_increase;
        upgradeAtk = 0;
        atkMultiplicativeBuff = 0f;
        atkAdditiveBuff = 0f;

        // 방어력 관련 초기화
        statDef = data.def;
        defMultiplicativeBuff = 0f;
        defAdditiveBuff = 0f;
        def = baseDef;
        damageReduction = 0f;
        damageReductionBuff = 0;

        // 기타 초기화
        baseCritChance = 0f;
        critChanceBuff = 0f;
        baseCritDamage = 0f;
        critDamageBuff = 0f;

        cooldownRate = 1f;
        cooldownMultiplicativeBuff = 0f;
        cooldownAdditiveBuff = 0f;

        passiveCodeId = data.codes["passive"];
        normalCodeId = data.codes["normal"];
        ultimateCodeId = data.codes["ultimate"];
    }
}
