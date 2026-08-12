# 상세 기획서 03 — 캐릭터와 스탯

> **3계층 문서.** 5스탯과 파생, 레벨·EXP, 코드 3분류, 패시브 해금, 캐릭터 데이터 스키마.
> 개념 정의는 [서브 기획서](GDD_Sub_Concepts.md) 3장을 본다.

최종 갱신: 2026-08-12
관련 코드: `Unit.cs`, `UnitStats.cs`, `Code.cs`, `CodeFactory.cs`, `GameManager.cs`
관련 데이터: `10_units.yaml`, `20_codes.yaml`, `60_enemies.yaml`

---

## 1. 스탯 모델 — 5스탯 단일 체계

**공격력·방어력을 데이터에 직접 적는 방식은 폐지되었다.**
`atkBase` / `defBase` 같은 필드는 사라졌고, 모든 전투 수치가 5스탯에서 파생된다.
피해는 **스킬 고정 위력 × 주스탯**, 방어력은 **STR 파생**이다.

| 스탯 | 공격 외 고유 역할 |
| --- | --- |
| **STR** | **방어력** (받는 피해 비율 감소) + 장비 중량 한도 |
| **DEX** | 행동 속도 |
| **CON** | 최대 체력, 치유 보너스, 보호막 보너스 |
| **INT** | 마나 획득 효율, 코드 보유 한도 |
| **LUK** | 치명타 확률 |

**주스탯이 된 스탯은 공격의 근거가 된다.** 5스탯 어느 것이든 주스탯이 될 수 있으며,
그 결과 주스탯은 공격과 고유 역할을 동시에 얻는다 — **이중 이득은 의도된 설계다.**

### 1.1 스탯 산출 순서

```
raw = base
    + incrementLvl     × 성장레벨          // 성장레벨 = Level − 1 (아군·적 동일)
    + incrementUpgrade × 강화 횟수          // 육성/보상 (아군 전용)
    + 장비 보너스
    + 상태 보너스
  → 캐릭터별 트레이닝 보너스
     일반: 주 +20%, 부 +10% / 부 2개면 각 +5%
     세이·시: 주 +25%, 부 +15%
  → 상태 배율
```

### 1.2 주·부 스탯과 트레이닝 보너스

| 구분 | 주스탯 | 부스탯 |
| --- | --- | --- |
| 일반, 부스탯 1개 | +20% | +10% |
| 일반, 부스탯 2개 | +20% | 각 +5% |
| 특수 판정 세이·시 | +25% | +15% |

데이터는 `mainStat`, `subStats`, `mainStatTrainingBonus`, `subStatTrainingBonus`로 분리한다.
`subStat`은 기존 데이터 호환을 위해 첫 번째 부스탯을 함께 보존한다.

> **주스탯은 5스탯 어느 것이든 쓸 수 있다.** 이중 이득은 의도된 설계다.

## 2. 파생 스탯

| 파생 | 계산식 | 상수 |
| --- | --- | --- |
| **피해** | `스킬 위력 × 주스탯 × 0.2` | `UnitStats.SkillPowerScale` |
| **최대 체력** | `CON × 100` | `HpPerConPoint` |
| **방어력** | `STR × 1` → 받는 피해 배율 `기준값 / (기준값 + 방어력)` | `DefensePerStrPoint` |
| 치명타 확률 | `clamp01(LUK × 0.01)` + 효과 가산 | |
| 치명타 피해 | `1.5` + 효과 가산 (최소 1.0) | |
| 공격 속도 | `1 + DEX × 0.01` | AV 계산 입력 |
| 회피율 | `0` | 🔸 파생 미구현 |
| 치유 보너스 | `clamp((CON − 10) × 0.01, 0, 2)` | |
| 보호막 보너스 | `clamp((CON − 10) × 0.01, 0, 2)` + 효과 가산 | |
| 마나 효율 | `1 + INT × 0.02` × 효과 배율 | |
| 코드 발동률 | `1.0` 고정 | 🔸 INT 연동 무효 |
| 코드 가속 | `max(0.1, 1.0 + 런 보너스 + 효과 가산)` | |
| 최대 마나 | `100` | |

### 2.1 피해 산출 (포켓몬식)

**스킬마다 고정 위력이 정해져 있다.** 시전자의 주스탯이 그 위력을 증폭한다.

```csharp
// Code 기반 클래스가 제공
public int Power { get; protected set; }        // 고정 위력
protected int[] StagePowers;                    // 단계별 위력 (있으면 우선)
public int CurrentPower { get; }                // 현재 단계 위력
public int RollDamage(float critMultiplier);    // 위력 → 피해

// Unit
public int SkillDamage(int skillPower);                        // 주스탯 기준
public int SkillDamage(int skillPower, PrimaryStat stat);      // 스탯 지정
```

**설계 의도** — 위력이 스킬에 붙어 있으면 "이 기술이 센 기술인가"를 데이터만 보고 판단할 수 있다.
캐릭터가 강해지는 것은 주스탯이 오르기 때문이고, 스킬 간 상대 강도는 위력이 정한다.

### 2.2 방어력 (롤 방식)

`방어배율 = 기준값 / (기준값 + 방어력)`, `기준값 = 100 + 10 × (레벨 − 1)`

기준값을 레벨에 연동해 **감소율이 레벨과 무관하게 일정**하다. 상세는 [Detail_02 §3.2](Detail_02_Combat.md#32-공식--포켓몬식-위력--롤-방식-방어력).

| 스탯 | 내구 기여 | 성격 |
| --- | --- | --- |
| CON | 최대 체력 (절대량) | 한 방을 크게 버틴다 |
| STR | 방어력 (비율 감소) | 여러 방을 오래 버틴다 |

### 2.3 갱신 시점

`AttributesUpdate()` 호출 시 재계산된다. 스탯에 영향을 주는 변경(상태, 장비, 레벨업, 원소) 후 반드시 호출한다.
체력 비율은 갱신 전후로 보존된다.

### 2.4 용량 제한

| 항목 | 계산식 |
| --- | --- |
| 장비 중량 1차/2차/3차 | `STR / ceil(STR×1.5) / STR×2` |
| 코드 보유 한도 | `max(3, INT)` |
| 현재 보유 코드 수 | `2 + PassiveCodes.Count` |

## 3. 레벨과 성장

성장 공식은 아군·적이 **완전히 동일**하다: `base + incrementLvl × (Level − 1)`.
다른 것은 **Level이 어디서 오는가**뿐이다.

| 캐릭터 구분 | 주스탯 성장 | 부스탯 성장 | 나머지 성장 |
| --- | --- | --- | --- |
| 일반, 부스탯 1개 | +2 | +2 | +1 |
| 일반, 부스탯 2개 | +2 | 각 +1 | +1 |
| 세이·시 | +3 | +2 | +1 |

| 대상 | Level의 출처 |
| --- | --- |
| 적 | **현재 스테이지** (`RoundManager.EnemyLevel = Stage`). 1스테이지 = Lv.1 = 성장분 0 |
| 아군 | **누적 EXP로 직접 레벨업** |

### 3.1 아군 EXP 곡선

**필요 EXP** (Lv.N → N+1)

```
RequiredExp(N) = 100 + 28 × (N − 1)
                 ↑ Unit.ExpBaseRequirement
                       ↑ Unit.ExpRequirementPerLevel
```

**획득처** (`GameManager` 상수, 모두 스테이지 비례)

| 획득처 | 지급량 | 대상 |
| --- | --- | --- |
| 적 처치 | `10 + 2 × Stage` | 활성 아군 전원 |
| 스테이지 클리어 | `50 + 10 × Stage` | 활성 아군 전원 |
| 육성 페이즈 | `30 + 5 × Stage` | 메인만 |

### 3.2 지급 시점 (중요)

전투 중 획득분은 즉시 주지 않고 `GameManager.pendingPartyExp`에 모은다.

```
전투 중 처치        → pendingPartyExp 누적
라운드 종료
  → RestoreAllyFieldState()   // 전투 시작 시점 스냅샷으로 되돌림
  → GrantExpToParty(pending + 클리어 보너스)   // 복원 후에 지급
```

**복원보다 먼저 지급하면 스냅샷에 덮여 성장이 사라진다.**
같은 이유로 `GameManager.BuildUnitSnapshot()`은 `level` / `exp`를 반드시 포함해야 한다.

### 3.3 결과 곡선

적 3기 처치 + 클리어 + 육성 1회를 매 스테이지 반복한다고 가정한 값이다.

| 스테이지 | 아군 Lv | 적 Lv | 격차 |
| --- | --- | --- | --- |
| 1 | 2 | 1 | +1 |
| 5 | 6 | 5 | +1 |
| 10 | 10 | 10 | ±0 |
| 25 | 24 | 25 | −1 |
| 50 | 46 | 50 | −4 |
| 75 | 67 | 75 | −8 |
| 100 | 89 | 100 | −11 |

**의도** — 초반은 아군이 약간 앞서고, 후반으로 갈수록 레벨만으로는 뒤처진다.
그 격차를 메우는 것이 **육성 업그레이드와 장비**다. 즉 후반일수록 육성 선택이 중요해진다.

### 3.4 저장

`UnitSaveData.level` / `.exp` (현재 저장 포맷 **v4**). v4는 유닛별 `carriedItemIds`도 함께 저장한다.

## 4. 코드(스킬) 시스템

### 4.1 3분류

| 분류 | 기반 클래스 | `CodeActivationType` | 발동 조건 | 발동 실패 |
| --- | --- | --- | --- | --- |
| 패시브 | `PassiveCode` | `Passive` | 라운드 시작 시 활성화 | 조건부 스킬은 조건 판정 |
| 일반 | `NormalCode` | `Active` | **행동 차례** (쿨다운 없음) | **없음** |
| 궁극기 | `UltimateCode` | `Ultimate` | 자원 최대 + 행동 가능 | **없음** |

> **일반·궁극기 코드에 발동 실패 판정을 직접 넣지 않는다.**

### 4.2 Code 기반 인터페이스

```csharp
public abstract class Code
{
    public BaseEnums.CodeType CodeType;
    public string CodeName;
    public Unit Caster;
    public List<Unit> TargetUnits;
    public float Cooldown;       // 궁극기 전용. 일반공격은 쿨다운을 쓰지 않는다
    public float CastingDelay;

    public virtual void CastCode() { }
    protected virtual IEnumerator SkillCoroutine() { yield return null; }
    public virtual void StopCode() { }
    public virtual bool HasValidTarget() { return true; }
}
```

피해 계산은 생성자에서 `Power`(또는 `StagePowers`)를 정하고 `RollDamage(critMultiplier)`를 호출한다.

### 4.3 궁극기 자원

| 타입 | 회복 방식 | 최대치 |
| --- | --- | --- |
| `Mana` | `RecoverMana()` 자동 회복 (마나 효율 배율) | 100 |
| `Stack` | 코드/이벤트에서 `AddUltimateResource(n)` 호출 | `ultimateResourceMax` |

- 자원이 최대치에 도달해도 **즉시 시전하지 않는다**
- `!isControlled && !isCasting` 조건 안에서 시전을 검사한다
- 예약된 궁극기는 대기 중인 일반 행동을 제친다

수르트의 **라그나로크**가 스택형 자원(최대 6)을 사용한다. 자동 시전하지 않고 스택 자체가 방어 무시와 일반공격 강화 조건이다.

### 4.4 패시브 구조 — 고유 패시브 1개 + 레벨 해금 N개

캐릭터마다 해금 패시브와 별도인 **고유 패시브 1개**와 **고유 궁극기 1개**가 있다.
고유 패시브는 `UniquePassiveCode`이며 코드 용량을 차지하지 않는다. `<전수 불가>`가 아니면 원본이 가리키는 별도 일반 `PassiveCode` ID를 말딸식 열화 전수 후보로 기록한다.
현재 열화 전수본은 아탈란테 230, 오리온 231, 아스클레피오스 232, 아마테라스 233, 야마 123(츠쿠요미 `저주`를 그대로 물려준다), 바유 224이며 나머지 영웅의 고유 P는 전수 불가다.

| 캐릭터 | 초기 패시브 | 3단계 수치 |
| --- | --- | --- |
| 세이 | 최초의 노래 | INT 계수 [1.5 / 2.2 / 3.0] |
| 시 | 시의 종언 | 최대 스택 [3 / 6 / 9] |
| 찬드라 | 니샤카라 | INT→CON 전환 [25 / 50 / 100]% |

레벨 해금 패시브(`levelPassives`)는 고유 패시브와 별개이며 INT가 만드는 코드 용량 안에서 활성화된다.

```yaml
codes:
  passive: 50         # 고유 패시브 — 1개
codeStages:
  passive: 1          # 현재 단계 (1~3)
levelPassives:        # 레벨로 해금되는 별도 패시브들
  - { codeId: 51, unlockLevel: 6,  stage: 1 }
  - { codeId: 52, unlockLevel: 50, stage: 1 }
```

| 항목 | 내용 |
| --- | --- |
| 판정 기준 | **`PassiveUnlockLevel = Level`** |
| 활성 목록 | `Unit.PassiveCodes` |
| 미해금 목록 | `Unit.PendingLevelPassives` |
| 승격 시점 | 레벨업 후 `RefreshLevelPassives()` |

> 개편 전에는 `Level + TrainingLevel`이었으나, 성장 축이 Level로 단일화되면서 `Level`만 본다.
> 육성은 EXP를 통해 간접적으로 해금을 앞당긴다.

**추가 패시브 경로**

| 경로 | 저장 위치 |
| --- | --- |
| 장비 부여 | `ItemPassiveCodes` |
| 사건 부여 | `GrantedPassiveCodeIds` |
| 서포트 전수 | `LearnedPassiveRecords` |

### 4.5 코드 등록 절차

코드는 **세 곳이 모두 맞아야** 동작한다.

1. **C# 구현** — `Assets/Scripts/Codes/{Passive|Normal|Ultimate}/`
2. **팩토리 연결** — `CodeFactory.Create{Passive|Normal|Ultimate}Code()` 스위치
3. **데이터 등록** — `20_codes.yaml`

ID는 같은 분류 안에서 중복되면 안 된다.

### 4.6 일반공격에는 쿨타임이 없다

일반공격의 발동 주기는 **DEX가 만드는 행동치(AV)가 전담**한다.
`normalCooldown`은 매 프레임 0으로 유지되며, 실제 시전 시점은 `ActionScheduler`가 정한다.

> 과거에는 AV와 쿨다운이라는 두 개의 관문이 겹쳐 있어 DEX의 체감이 흐려졌다.
> 관문을 하나로 줄여 **DEX = 공격 빈도**가 직관적으로 성립하게 했다.

궁극기는 여전히 `Cooldown`을 쓴다(자원이 가득 차도 재시전을 막는 최소 간격).

## 5. 캐릭터 분류

모든 캐릭터는 세 분류 중 하나에 속한다 (`characterType`).

| 분류 | 초기 사용 | 무한 모드 | 설명 |
| --- | --- | --- | --- |
| **Starter** | 메인으로 즉시 | O | 육성 완료 시 **서포트 카드에도 추가**되며, 그 카드는 무한 모드에서 쓸 수 있다 |
| **Support** | 서포터 카드로만 | ✕ | 게임 초기부터 편성 가능하지만 무한 모드에는 나오지 않는다 |
| **Locked** | 불가 | (해금 후) | 육성 모드 중 아군으로 합류하면(포켓로그식) **영구 해금**되어 이후 Starter처럼 쓴다 |

**플래그 대응**

| 분류 | `canStartAsMain` | `canStartAsSupport` | `canUseInInfinite` |
| --- | --- | --- | --- |
| Starter | true | false | true |
| Support | false | true | false |
| Locked | false | false | false |

> **획득처가 아직 정해지지 않은 캐릭터라도 기획이 끝났다면 `Locked`로 등록해 둔다.**
> 기획서에서 빠지면 구현이 표류한다.

### 5.1 현재 분류

| 캐릭터 | 분류 | 비고 |
| --- | --- | --- |
| 수르트 · 아탈란테 · 오리온 · 테세우스 | **Starter** | 메인으로 즉시 선택 가능 |
| 세이 · 찬드라 | **Support** | 세이 = 초기 지급 공격형 서포터, 찬드라 = 방어형 서포터 |
| 시 · 케찰코아틀 · 츠쿠요미 · 피그말리온 | **Locked** | 획득처 미정 또는 테마 복원 시 영입 사건으로 해금 |

🔸 **`Locked` → 해금 상태의 영구 저장은 아직 구현되지 않았다.** 데이터 분류만 확정된 단계다.

## 6. 캐릭터 데이터 스키마 (`10_units.yaml`)

```yaml
- id: 5
  name: 아탈란테
  element: Dendro
  characterType: Starter   # Starter / Support / Locked
  canStartAsMain: true
  canStartAsSupport: false
  canUseInInfinite: true
  mainStat: DEX          # 5스탯 어느 것이든 가능
  subStat: CON
  subStats: [CON]        # 복수 부스탯 캐릭터는 [CON, LUK]처럼 표기
  mainStatTrainingBonus: 0.20
  subStatTrainingBonus: 0.10  # 부스탯 2개면 각 0.05
  startingProficiencies: [Longbow, HeavyArmor]
  tags: [Greek]
  strBase: 16
  strIncrementLvl: 1
  strIncrementUpgrade: 1
  # dex/con/int/luk 동일 구조
  codes:
    passive: 2
    normal: 1
    ultimate: 3
  codeStages:
    passive: 1
    normal: 1
    ultimate: 1
  levelPassives:
    - { codeId: 121, unlockLevel: 12, stage: 1 }   # 선택
  startingItemIds: [4010]                          # 선택
  portrait: ATALANTE_PORTRAIT
  standing: ATALANTE_STANDING                      # 선택
```

**폐지된 필드**: `atkBase`, `atkIncrementLvl`, `defBase`, `defIncrementLvl`
(DTO에서도 제거되었으므로 YAML에 남겨도 무시된다)

## 7. 현재 캐릭터

기초 스탯·레벨당 성장·트레이닝 보너스는 서로 합치지 않고
[Detail_08 공통 스탯 표](Detail_08_Confirmed_Characters.md#스탯-표기-규칙)에서 관리한다.

| ID | 이름 | 원소 | 주스탯 | 부스탯 | 역할 |
| --- | --- | --- | --- | --- | --- |
| 1 | 세이 | Geo | INT | DEX | 후열 지원 |
| 2 | 시 | Anemo | DEX | LUK | 연타 암살자 · **보관** |
| 4 | 피그말리온 | Pyro | CON | STR · INT | 지속피해 반격 전열 탱커 |
| 5 | 아탈란테 | Dendro | DEX | CON | 후열 사수 · 지속피해 딜러 |
| 6 | 오리온 | Geo | STR | CON | 전열 탱커 · 힘사수 딜탱 |
| 7 | 테세우스 | Hydro | DEX | CON | 범용 전열 딜러 |
| 8 | 아스클레피오스 | Electro | INT | CON · LUK | 하이브리드 치유·서브 딜러 |
| 9 | 아마테라스 | Pyro | DEX | STR · LUK | 후열 기본공격 사수 |
| 12 | 찬드라 | Anemo | CON | LUK | 방어 지원 |
| 20 | 케찰코아틀 | Dendro | INT | LUK | 풀 파티 서브 딜러·지원 · **보관** |
| 21 | 츠쿠요미 | Electro | INT | LUK | 지속피해 술사 · **보관** |
| 24 | 수르트 | Pyro | STR | CON | 전열 화력 · **Starter** |
| 30 | 야마 | Electro | INT | DEX · CON | 지속피해 정산 딜러 · **보관** |
| 31 | 아그니 | Pyro | INT | LUK | 불 파티 서브 딜러 · **보관** |
| 32 | 인드라 | Electro | INT | LUK | 번개 하이퍼캐리 · **보관** |
| 33 | 바유 | Anemo | CON | DEX | 정화형 방어 서포터 · **Starter** |

상세 전투 사양과 열화 전수본의 수치는 [Detail_08](Detail_08_Confirmed_Characters.md)를 본다.

### 7.1 기본 5스탯

| 유닛 | STR | DEX | CON | INT | LUK |
| --- | --- | --- | --- | --- | --- |
| 세이 | 10 | **16** | 15 | 18 | 12 |
| 시 | 14 | **20** | 13 | 10 | 20 |
| 피그말리온 | 12 | 8 | **26** | 16 | 10 |
| 아탈란테 | 16 | **20** | 14 | 10 | 24 |
| 오리온 | **24** | 12 | 20 | 10 | 14 |
| 테세우스 | 16 | **22** | 20 | 12 | 14 |
| 아스클레피오스 | 9 | 12 | 16 | **20** | 16 |
| 아마테라스 | 16 | **22** | 12 | 10 | 16 |
| 찬드라 | 10 | 12 | **22** | 18 | 14 |
| 케찰코아틀 | 10 | 12 | 20 | **24** | 12 |
| 츠쿠요미 | 9 | 14 | 16 | **26** | 18 |
| 수르트 | **26** | 12 | 22 | 8 | 12 |
| 야마 | 10 | 18 | 18 | **22** | 12 |
| 아그니 | 8 | 12 | 14 | **24** | 18 |
| 인드라 | 8 | 14 | 14 | **26** | 16 |
| 바유 | 12 | 18 | **22** | 12 | 14 |

(굵은 값은 기초 주스탯이며, 트레이닝 보너스는 별도 축으로 적용된다.)

### 7.2 보관 상태 캐릭터

시 · 케찰코아틀 · 츠쿠요미 · 피그말리온 · 아스클레피오스 · 아마테라스 · 야마 · 아그니 · 인드라는 **현재 획득 경로가 없다.**
시는 획득 사건이 미정이고, 나머지는 영입 사건이 있던 아즈텍 · 일본 테마가 제거되었기 때문이다.
데이터와 구현된 코드는 그대로 보존되어 있다.

다시 쓰려면 `canStartAsMain` / `canStartAsSupport`를 열거나 새 영입 사건을 붙인다.

### 7.3 남은 데이터 공백

피그말리온의 전투 코드·해금 패시브·Standing·Portrait 공백은 2026-08-12에 해소했다. 현재 캐릭터 데이터의 남은 공백은 Locked 영입 사건과 영구 해금 저장이다.

### 7.4 패시브 보유 현황

| 유닛 | 초기 | 해금 | 장비 | 합계 |
| --- | --- | --- | --- | --- |
| 세이 | 1 | 3 | — | 4 |
| 시 | 1 | 7 | — | 8 |
| 피그말리온 | 1 | 6 | — | 7 |
| 아탈란테 | 1 | 7 | — | 8 |
| 오리온 | 1 | 8 | — | 9 |
| 테세우스 | 1 | 5 | — | 6 |
| 아스클레피오스 | 1 | 6 | — | 7 |
| 아마테라스 | 1 | 6 | — | 7 |
| 찬드라 | 1 | 7 | — | 8 |
| 케찰코아틀 | 1 | 6 | — | 7 |
| 츠쿠요미 | 1 | 8 | — | 9 |
| 수르트 | 1 | 7 | — | 8 |
| 야마 | 1 | 6 | — | 7 |
| 아그니 | 1 | 4 | — | 5 |
| 인드라 | 1 | 6 | — | 7 |
| 바유 | 1 | 5 | — | 6 |

고유 P 열화 전수본은 위 합계에 포함하지 않는다. 지원 카드 육성 기록에는 전수 가능한 영웅만 별도 ID 230~233을 후보로 남긴다.

## 8. 적 데이터 스키마 (`60_enemies.yaml`)

캐릭터 스키마와 동일하며 다음 필드가 추가된다.

| 필드 | 설명 |
| --- | --- |
| `themeId` | 테마의 `enemyThemeId`와 매칭 |
| `archetype` | 편성 패턴 매칭 + 배치 열 결정 |
| `tier` | `normal` / `elite` / `boss` |

**적도 `mainStat` / `subStat`를 가진다.** 개편 전에는 빈 문자열이었으나,
위력이 주스탯에서 파생되므로 반드시 지정해야 한다.

