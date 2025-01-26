using System.Collections.Generic;
using UnityEngine;

public class Unit : MonoBehaviour
{
    public int ID;
    public bool SIDE; // true: 아군, false: 적군
    public string NAME;
    public int HP_CURRENT;
    public int HP_MAX;
    public int ATK;
    public int DEF;
    public float CRT_POS;
    public float CRT_DMG;
    public float CT;

    public int HP_BASE;
    public float HP_MULBUFF;
    public float HP_SUMBUFF;

    public int ATK_BASE;
    public float ATK_MULBUFF;
    public float ATK_SUMBUFF;

    public int DEF_BASE;
    public float DEF_MULBUFF;
    public float DEF_SUMBUFF;

    public float CRT_POS_BASE;
    public float CRT_POS_BUFF;

    public float CRT_DMG_BASE;
    public float CRT_DMG_BUFF;

    public float CT_BASE;
    public float CT_MULBUFF;
    public float CT_SUMBUFF;

    void CreateArts(int artsID)
    {
        // Load Arts data from Resources
        TextAsset artsDataFile = Resources.Load<TextAsset>("Data/03_Arts");
        if (artsDataFile == null)
        {
            Debug.LogWarning("Arts data file not found!");
            return;
        }

        string json = artsDataFile.text;
        ArtsDataWrapper artsWrapper = JsonUtility.FromJson<ArtsDataWrapper>(json);
        ArtsData arts = System.Array.Find(artsWrapper.arts.ToArray(), a => a.ID == artsID);

        if (arts != null)
        {
            GameObject artsObject = new GameObject("Arts_" + artsID);
            Arts newArts = artsObject.AddComponent<Arts>();

            newArts.NAME = arts.NAME;
            newArts.TYPE = arts.SKILL.TYPE;
            newArts.COUNTER = 0;
            newArts.MAX_COUNTER = arts.SKILL.COUNTER;
            newArts.CONDITIONS = arts.SKILL.CONDITION;
            newArts.EFFECTS = arts.SKILL.EFFECT;
            newArts.CT = 0f;
            newArts.MAX_CT = 0f;
            newArts.OWNER = this;
        }
        else
        {
            Debug.LogWarning($"Arts data for ID {artsID} not found!");
        }
    }

    public void StatusUpdate()
    {
        HP_MAX = (int)(HP_BASE * (1 + (HP_MULBUFF * 0.01f)) + HP_SUMBUFF);
        ATK = (int)(ATK_BASE * (1 + (ATK_MULBUFF * 0.01f)) + ATK_SUMBUFF);
        DEF = (int)(DEF_BASE * (1 + (DEF_MULBUFF * 0.01f)) + DEF_SUMBUFF);
        CRT_POS = CRT_POS_BASE + CRT_POS_BUFF;
        CRT_DMG = CRT_DMG_BASE + CRT_DMG_BUFF;
    }

    public void EffectHandler(float damage, string[] tags)
    {
        // Debug.Log("Received Damage: " + damage);

        // foreach (Arts arts in ARTS_MANAGER)
        // {
        //     if (arts.TYPE == "COUNT")
        //     {
        //     foreach (string condition in arts.CONDITIONS)
        //     {
        //         foreach (string tag in tags)
        //         {
        //         if (condition == tag)
        //         {
        //             arts.COUNTER++;
        //             Debug.Log($"Arts: {arts.NAME} - Counter increased to: {arts.COUNTER}");
        //         }
        //         }
        //     }
        //     }
        // }
    }

    public void TakeDamage(float Damage, float IgnoreDefense){
        float damage = Damage * (1 - DEF / (DEF + 300)) * (1 - IgnoreDefense);
        HP_CURRENT -= (int)damage;
        if (HP_CURRENT <= 0)
        {
            HP_CURRENT = 0;
        }
    }

    [System.Serializable]
    public class ArtsDataWrapper
    {
        public List<ArtsData> arts;
    }

    [System.Serializable]
    public class ArtsData
    {
        public int ID;
        public string NAME;
        public SkillData SKILL;
    }

    [System.Serializable]
    public class SkillData
    {
        public string TYPE;
        public int COUNTER;
        public string[] CONDITION;
        public string[] EFFECT;
    }
}