using System;
using System.Collections.Generic;

namespace Managers
{
    [Serializable]
    public class StageThemeData
    {
        public int id;
        public string name;

        /// <summary>
        /// 이 테마를 런에 넣을지. 기본값은 true이며 yaml에 적지 않으면 켜져 있다.
        ///
        /// 리메이크가 필요한 테마를 지우지 않고 잠시 빼둘 때 쓴다. 항목을 통째로 지우면
        /// 그 테마를 참조하는 사건·적·보상 데이터가 함께 죽어 되살리기 번거롭다.
        /// </summary>
        public bool enabled = true;

        /// <summary>
        /// 룰렛에서 이 테마가 뽑혔을 때 <b>대신 시작할</b> 테마. 0이면 자기 자신이다.
        ///
        /// 연작 테마의 확률을 올리는 장치다. 노르드 2·3도 룰렛에 그대로 들어가되
        /// 뽑히면 노르드 1로 치환되므로, 균등 추첨을 유지한 채 노르드 1의 등장 확률만
        /// 세 배가 된다. 추첨표를 손대지 않고 무게를 주는 방법이다.
        /// </summary>
        public int rotationRedirectThemeId;

        /// <summary>
        /// 이 테마의 라운드가 끝나면 <b>추첨 없이</b> 이어질 테마. 0이면 다시 추첨한다.
        /// 노르드 1 → 2 → 3처럼 순서가 있는 연작을 묶는다.
        /// </summary>
        public int chainNextThemeId;

        public int enemyThemeId;
        public string description;
        public int midBossId;

        /// <summary>
        /// 중간 보스가 나오는 라운드 내 스테이지 번호. 비워 두면 8이다.
        /// 테마마다 편성이 달라 고정하지 않는다.
        /// </summary>
        public int midBossStageInRound;
        public int bossId;

        /// <summary>
        /// 전장 환경광 색(#RRGGBB). 비워 두면 무채색 기본값을 쓴다.
        ///
        /// 전장 배경이 검기만 하면 대비는 좋아도 <b>어느 테마에서 싸우는지</b>가 사라진다.
        /// 낮은 명도로 깔아 카드 실루엣을 죽이지 않으면서 분위기만 얹는 값이다.
        /// </summary>
        public string ambientColor;

        public List<string> tags;
        public List<ThemeStagePatternData> stagePatterns;

        /// <summary>파싱한 환경광. 값이 없거나 형식이 틀리면 기본 회청색이다.</summary>
        public UnityEngine.Color Ambient =>
            !string.IsNullOrWhiteSpace(ambientColor) &&
            UnityEngine.ColorUtility.TryParseHtmlString(ambientColor.Trim(), out UnityEngine.Color parsed)
                ? parsed
                : new UnityEngine.Color(0.13f, 0.15f, 0.20f);
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

        // 선택: 화자 초상화 키(Sprite/Portraits의 분류 폴더·확장자 제외).
        // 비워두면 speaker 이름을 10_units.yaml의 유닛 이름과 대조해 자동 해석한다.
        // 유닛이 아닌 화자(일행, 내레이션 등)는 비워두면 초상화 없이 표시된다.
        public string portrait;

        // 선택: 화자 스탠딩 키(Sprite/Standings의 분류 폴더·확장자 제외).
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

        /// <summary>
        /// 영입(<see cref="grantUnitId"/>) 선택지에서 <b>자리가 하나도 없을 때</b> 띄울 문구.
        ///
        /// 자리가 없다고 조용히 실패시키면 플레이어는 합류를 골랐는데 아무 일도 없었다고 읽는다.
        /// 이 문구를 띄운 뒤 누구를 떠나보낼지 고르게 하거나 합류를 포기하게 한다.
        /// 비워 두면 범용 기본 문구를 쓴다.
        /// </summary>
        public string rosterFullText;
    }

    [Serializable]
    public class StageEventData
    {
        public string id;
        public int themeId;
        public int stageInRound;
        /// <summary>사건 등급. 0이면 고정/무등급 사건, 1~5면 T1~T5 사건이다.</summary>
        public int tier;
        /// <summary>이 보스를 영구 기록상 격파한 뒤에만 사건 풀에 들어간다.</summary>
        public int requiresBossDefeatId;

        /// <summary>
        /// 이 보스를 넘어선 직후 <b>추첨 없이 예약되는</b> 사건. 0이면 스테이지 슬롯 추첨만 탄다.
        ///
        /// 이 값이 붙은 사건은 <c>themeId</c>·<c>stageInRound</c>를 보지 않고
        /// 슬롯 풀에서도 빠진다. 보스 격파가 곰 해금인 영입 사건이 여기 속한다.
        /// </summary>
        public int triggerBossId;
        public string title;
        public bool oncePerRun;
        public List<int> blockedUnitIds;
        public List<string> randomSpeakers;
        public List<StageEventDialogueData> dialogue;
        public List<StageEventChoiceData> choices;

        /// <summary>
        /// 이 사건이 영입을 제안하는 유닛 ID. 영입 선택지가 없으면 0이다.
        ///
        /// <b>사건 하나는 한 명만 제안한다</b>는 규칙을 전제로 첫 값만 읽는다.
        /// 해금 여부·중복 판정을 여러 곳에서 같은 모양으로 캐물었어서 한 군데로 모았다.
        /// </summary>
        public int RecruitUnitId
        {
            get
            {
                if (choices == null) return 0;
                foreach (StageEventChoiceData choice in choices)
                {
                    if ((choice?.grantUnitId ?? 0) > 0) return choice.grantUnitId;
                }
                return 0;
            }
        }

        /// <summary>
        /// 이 사건이 제안하는 <b>모든</b> 영입 대상.
        ///
        /// 시구르드·브륀힐드처럼 한 사건이 둘을 함께 내미는 경우가 생겼다.
        /// <see cref="RecruitUnitId"/>는 "지금 띄울 수 있는가"를 묻는 자리가 쓰는 대표값이고,
        /// 이쪽은 <b>해금</b>이 쓴다 — 만난 사람은 전부 해금되어야 하기 때문이다.
        /// </summary>
        public IEnumerable<int> RecruitUnitIds
        {
            get
            {
                if (choices == null) yield break;
                foreach (StageEventChoiceData choice in choices)
                {
                    if ((choice?.grantUnitId ?? 0) > 0) yield return choice.grantUnitId;
                }
            }
        }
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
