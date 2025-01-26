using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Enemy : Unit
{
    public int LEVEL; // 적 레벨

    public override void InitProcess(bool isHero, int id)
    {
        LoadEnemyData(id);
        StatusUpdate();
    }
    // 캐릭터 데이터를 로드하는 함수
    public void LoadEnemyData(int id)
    {
        // 라운드별 스탯 증가량 반영 필요
        EnemyData data = gameManager.enemyDataList.enemies.FirstOrDefault(e => e.ID == id);
        LoadSprite(data.PORTRAIT);

        if (data == null)
        {
            Debug.LogError($"적 데이터(ID: {id})를 찾을 수 없습니다.");
            return;
        }

        // 적 데이터로 Unit 속성 초기화
        ID = data.ID;
        NAME = data.NAME;

        // 체력 관련 초기화
        HP_BASE = SetBase(data.HP_BASE, LEVEL, data.HP_INCREASE);
        HP_MULBUFF = 0f;
        HP_SUMBUFF = 0f;
        HP_CURRENT = HP_BASE;

        // 공격력 관련 초기화
        ATK_BASE = SetBase(data.ATK_BASE, LEVEL, data.ATK_INCREASE);
        ATK_MULBUFF = 0f;
        ATK_SUMBUFF = 0f;
        ATK = ATK_BASE;

        // 방어력 관련 초기화
        DEF_BASE = SetBase(data.DEF_BASE, LEVEL, data.DEF_INCREASE);
        DEF_MULBUFF = 0f;
        DEF_SUMBUFF = 0f;
        DEF = DEF_BASE;

        // 기타 초기화
        CRT_POS_BASE = 0f;
        CRT_POS_BUFF = 0f;
        CRT_DMG_BASE = 0f;
        CRT_DMG_BUFF = 0f;

        CT_BASE = 0f;
        CT_MULBUFF = 0f;
        CT_SUMBUFF = 0f;
    }

    public void LoadSprite(string name)
    {
        Sprite sprite = Resources.Load<Sprite>($"Sprite/Portraits/{name}");
        currentCell.portraitRenderer.sprite = sprite;
    }
   
    public int SetBase(int n, int lv, int increase){
        return n + lv * increase;
    }
}
