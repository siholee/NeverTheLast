using System;
using System.Collections.Generic;

namespace Managers
{
    [Serializable]
    public class StageThemeData
    {
        public int id;
        public string name;
        public int enemyThemeId;
        public string description;
        public int midBossId;
        public int bossId;
        public List<ThemeStagePatternData> stagePatterns;
    }

    [Serializable]
    public class ThemeStagePatternData
    {
        public int stageInRound;
        public List<RoundPattern> patterns;
    }

    [Serializable]
    public class StageEventDialogueData
    {
        public string speaker;
        public string text;
    }

    [Serializable]
    public class StageEventChoiceData
    {
        public string id;
        public string text;
        public string action;
        public int goldCostPerStage;
        public int battleEnemyId;
        public int grantUnitId;
        public int grantItemId;
        public string successText;
        public string failureText;
    }

    [Serializable]
    public class StageEventData
    {
        public string id;
        public int themeId;
        public int stageInRound;
        public string title;
        public List<StageEventDialogueData> dialogue;
        public List<StageEventChoiceData> choices;
    }

    [Serializable]
    public class FixedBossStageData
    {
        public int stage;
        public int bossId;
    }

    [Serializable]
    public class StageThemeDataList
    {
        public List<StageThemeData> stageThemes;
        public List<FixedBossStageData> fixedBossStages;
        public List<StageEventData> events;
    }
}
