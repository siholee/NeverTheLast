using System;
using System.Collections.Generic;

namespace Core
{
    [Serializable]
    public class TrainedCharacterCollection
    {
        public List<int> unitIds = new();
    }

    [Serializable]
    public class TokenSaveData
    {
        public int tokenId;
        public int amount;
    }

    [Serializable]
    public class UnitSaveData
    {
        public int unitId;
        public int currentHP;
        public int xPos;
        public int yPos;
        public bool isBench;
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
        public List<TokenSaveData> tokens = new();
        public List<UnitSaveData> heroUnits = new();
    }
}
