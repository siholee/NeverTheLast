using System.Collections.Generic;
using UnityEngine;

public class Unit : MonoBehaviour
{
    public string ID;
    public string NAME;
    public string POS;
    public int RANK;
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

    public List<Status> STATUS_MANAGER;

    public void StatusUpdate()
    {
        HP_MAX = (int)(HP_BASE * (1 + (HP_MULBUFF * 0.01f)) + HP_SUMBUFF);
        ATK = (int)(ATK_BASE * (1 + (ATK_MULBUFF * 0.01f)) + ATK_SUMBUFF);
        DEF = (int)(DEF_BASE * (1 + (DEF_MULBUFF * 0.01f)) + DEF_SUMBUFF);
        CRT_POS = CRT_POS_BASE + CRT_POS_BUFF;
        CRT_DMG = CRT_DMG_BASE + CRT_DMG_BUFF;
    }
}
