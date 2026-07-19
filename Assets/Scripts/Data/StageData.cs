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
        public List<string> tags;
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
        // 선택: 화자 초상화 파일명(Resources/Sprite/Portraits 기준, 확장자 제외).
        // 비워두면 speaker 이름을 10_units.yaml의 유닛 이름과 대조해 자동 해석한다.
        // 유닛이 아닌 화자(일행, 내레이션 등)는 비워두면 초상화 없이 표시된다.
        public string portrait;
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
        public int grantPassiveCodeId;
        public bool grantPassiveToAll;
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
        public bool oncePerRun;
        public List<int> blockedUnitIds;
        public List<string> randomSpeakers;
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
