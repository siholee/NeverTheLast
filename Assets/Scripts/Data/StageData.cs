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

        /// <summary>
        /// 중간 보스가 나오는 라운드 내 스테이지 번호. 비워 두면 8이다.
        /// 테마마다 편성이 달라 고정하지 않는다.
        /// </summary>
        public int midBossStageInRound;
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

        // 선택: 화자 스탠딩 일러스트 파일명(Resources/Sprite/Standings 기준, 확장자 제외).
        // 사건 화면에서는 standing을 portrait보다 우선하며, 둘 다 없으면 유닛 데이터에서 자동 해석한다.
        public string standing;

        // 선택: 표정. 초상화 파일명 뒤에 접미사로 붙여 해석한다.
        //   예) portrait=TSUKUYOMI_PORTRAIT, emotion=smile → "TSUKUYOMI_PORTRAIT_smile"
        // 해당 파일이 없으면 표정 없는 기본 초상화로 자동 폴백하므로,
        // 표정 이미지를 나중에 추가하기만 하면 그대로 살아난다.
        public string emotion;

        // 선택: 연출 효과. none(기본) / shake(흔들림) / bounce(톡 튀기) / flash(화면 번쩍)
        public string effect;

        // 선택: 이 대사에서 1회 재생할 효과음 (Resources/Audio/SFX/{sfx})
        public string sfx;

        // 선택: 이 대사 시점부터 재생할 BGM (Resources/Audio/BGM/{bgm}).
        // 같은 곡이 이미 재생 중이면 재시작하지 않는다. "stop"이면 BGM을 정지한다.
        public string bgm;

        // 선택: 타자기 속도 배율(1 = 기본). 0.5면 절반 속도로 느리게, 2면 두 배로 빠르게.
        // 뜸을 들이거나 다급한 장면을 표현할 때 사용한다.
        public float textSpeed;

        /// <summary>
        /// 토큰 치환본을 만든다(예: "{deity}" → "츠쿠요미").
        /// 필드를 추가할 때 이 메서드도 함께 갱신할 것 — 누락하면 해당 연출이 조용히 사라진다.
        /// </summary>
        public StageEventDialogueData CloneWithReplacement(string token, string value)
        {
            return new StageEventDialogueData
            {
                speaker = speaker?.Replace(token, value),
                text = text?.Replace(token, value),
                portrait = portrait?.Replace(token, value),
                standing = standing?.Replace(token, value),
                emotion = emotion,
                effect = effect,
                sfx = sfx,
                bgm = bgm,
                textSpeed = textSpeed,
            };
        }
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
