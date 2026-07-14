using System;
using System.Collections.Generic;
using BaseClasses;

namespace Core
{
    [Serializable]
    public class TrainedCharacterCollection
    {
        public List<int> unitIds = new();
        public List<int> unlockedStarterUnitIds = new();
        public List<TrainedCharacterRecord> records = new();
    }

    [Serializable]
    public class PrimaryStatSaveData
    {
        public int str;
        public int dex;
        public int con;
        public int intStat;
        public int luk;
    }

    [Serializable]
    public class LearnedPassiveSaveData
    {
        public int codeId;
        public int stage = 1;
        public bool transferable = true;
    }

    [Serializable]
    public class SupportCardSaveData
    {
        public string supportId;
        public int sourceUnitId;
        public string sourceUnitName;
        public string specialtyTraining;
        public int specialtyRate;
        public int specialtyBonus;
        public int trainingBonus;
        public int skillTransferRate;
        public int initialBond;
        public int bondGainRate;
        public int friendshipBonus;
        public int sourcePower;
    }

    [Serializable]
    public class TrainedCharacterRecord
    {
        public int version = 1;
        public int unitId;
        public string unitName;
        public int finalTrainingLevel;
        public PrimaryStatSaveData finalPrimaryStats = new();
        public List<string> titleIds = new();
        public List<LearnedPassiveSaveData> ownedPassiveCodes = new();
        public SupportCardSaveData supportCard = new();
        public long createdAtUnixSeconds;
    }

    [Serializable]
    public class TokenSaveData
    {
        public int tokenId;
        public int amount;
    }

    [Serializable]
    public class SupportBondSaveData
    {
        public int unitId;
        public int currentBond;
    }

    [Serializable]
    public class UnitSaveData
    {
        public int unitId;
        public int currentHP;
        public int xPos;
        public int yPos;
        public bool isBench;
        public int trainingLevel;
        public int strUpgrade;
        public int dexUpgrade;
        public int conUpgrade;
        public int intUpgrade;
        public int lukUpgrade;
        public int hpUpgrade;
        public int atkUpgrade;
        public int defUpgrade;
        public int critChanceUpgrade;
        public int critMultiplierUpgrade;
        public float codeAccelerationBonus;
        public List<int> equippedItemIds = new();
    }

    [Serializable]
    public class RunSaveData
    {
        public int gameMode;
        public int currentStage;
        public int currentRound;
        public int life;
        public int killCount;
        public int rerollTicketCount;
        public int gold;
        public bool preparationActionUsed;
        public List<TokenSaveData> tokens = new();
        public List<int> storedItemIds = new();
        public List<SupportBondSaveData> supportBonds = new();
        public List<UnitSaveData> heroUnits = new();
    }
}
