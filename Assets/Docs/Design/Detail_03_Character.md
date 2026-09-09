# 상세 기획서 03 — 캐릭터와 스탯

> **3계층 문서.** 5스탯과 파생, 레벨·EXP, 코드 3분류, 패시브 해금, 캐릭터 데이터 스키마.
> 개념 정의는 [서브 기획서](GDD_Sub_Concepts.md) 3장을 본다.

최종 갱신: 2026-09-02
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
| **INT** | 마나 획득 효율 |
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
    public float Cooldown;       // 궁극기 전용. 일반행동은 쿨다운을 쓰지 않는다
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

현재 아군 영웅의 스택형 궁극기 예시는 호루스의 **우제트**다. 수르트의 라그나로크는 리메이크 후 일반 마나형 발동 궁극기다.

### 4.4 패시브 구조 — 고유 패시브 1개 + 레벨 해금 N개

캐릭터마다 해금 패시브와 별도인 **고유 패시브 1개**와 **고유 궁극기 1개**가 있다.
고유 패시브는 `UniquePassiveCode`이며 코드 용량을 차지하지 않는다.
**고유 패시브는 어떤 경로로도 전수되지 않는다** — 생성 시점에 `Transferable = false`가 붙는다.
서포터 카드가 넘겨줄 수 있는 것은 해금 패시브뿐이다.

| 캐릭터 | 초기 패시브 | 3단계 수치 |
| --- | --- | --- |
| 세이 | 빅뱅 | 8스택 / 미사일 8발 / 발당 INT 20% / 아군 보호막 INT 80% |
| 시 | 시의 종언 | 최대 스택 [3 / 6 / 9] |
| 찬드라 | 니샤카라 | INT→CON 전환 [25 / 50 / 100]% |

레벨 해금 패시브(`levelPassives`)는 고유 패시브와 별개이며, **레벨 조건만 만족하면 전부 활성화된다.**

> 🔸 **코드 보유 한도는 폐지됐다.** 예전에는 INT가 `max(3, INT)`의 코드 용량을 만들고 그 수를
> 넘으면 해금 패시브를 조용히 배우지 못했다. 해금은 낮은 레벨부터 채워지므로 **잘리는 쪽이 항상
> 가장 강한 고레벨 코드**였고, INT가 낮은 적(거인·씨앗 계열)은 설계된 해금의 절반도 얻지 못했다.
> 한도를 없애고 INT는 마나 효율만 담당한다.

```yaml
codes:
  passive: 50         # 고유 패시브 — 1개
codeStages:
  passive: 1          # 현재 단계 (1~3)
levelPassives:        # 레벨로 해금되는 별도 패시브들
  - { codeId: 8,  unlockLevel: 8,  stage: 1 }
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

### 4.5-A 행동을 하는 영역과, 행동을 호출하는 영역

전투에서 벌어지는 일은 두 영역으로 갈린다. **이 구분이 흐려지면
"패시브가 행동을 했다" 같은 표현이 나오고, 행동 횟수를 세는 코드가 전부 어긋난다.**

```
행동을 호출하는 영역                    행동을 하는 영역
────────────────────                  ──────────────────
패시브 코드                     ──►    일반행동 / 대체행동
상태·효과(BaseEffect)           ──►    추가행동
이벤트 훅(OnTargeted 등)        ──►    궁극기
장비가 부여한 코드
```

#### 행동 — 스케줄러 슬롯을 차지하고 실제로 해결되는 것

네 가지뿐이다. `ActionScheduler.ActionKind`가 그대로 이 목록이다.

| 행동 | 표기 | 턴 | AV | 설명 |
| --- | --- | :-: | :-: | --- |
| **일반행동** | `N` | 쓴다 | 리셋 | DEX가 만드는 행동치가 주기를 정한다. 쿨다운 없음 |
| **대체행동** | `N+` | 쓴다 | 리셋 | 조건이 맞으면 **일반행동 자리를 대신** 쓴다 |
| **추가행동** | `Sp` | 안 쓴다 | 유지 | 반격·연계타. 패시브가 부른 것은 같은 턴에서 **먼저** 나간다 |
| **궁극기** | `U` | 안 쓴다 | 유지 | 자원이 차면 누구의 턴이든 줄을 선다 |

#### 호출 — 스스로 행동하지 않고, 행동을 일으키거나 값을 바꾸는 것

| 표기 | 무엇 | 하는 일 |
| --- | --- | --- |
| `P` | 패시브 코드 | 조건을 지켜보다 행동을 예약하거나 상태를 건다 |
| — | 상태·효과 | 스탯·배율·피해 파이프라인의 값을 바꾼다 |
| — | 이벤트 훅 | `OnTargeted` · `OnAfterDamageTaken` 등, 호출이 일어날 자리 |

**패시브는 행동 목록에 없다.** 세이의 `빅뱅`처럼 패시브가 부르는 것도 실제로 실행되는 것은
**우선 추가행동**(`ActionKind.PriorityAdditional`)이며, 다른 추가행동과 똑같이
`OnAdditionalActivates`를 발행한다. 그래서 니콜의 행동 카운터와 바스테트의 스택이 빅뱅도 센다.

### 4.5-B 대체행동

조건이 맞으면 일반행동 대신 나가는 행동이다.
**예전 표기 '강화 일반행동'을 이 이름으로 통일했다** — *강화*는 위력만 오른 같은 공격을
연상시키는데, 실제로는 공격을 아예 하지 않고 방어막·도발·자기 강화를 하는 쪽이 더 많다.
코드에서도 `Empowered*` 식별자를 `Substitute*`로 바꿨다.

| 성질 | 내용 |
| --- | --- |
| 자리 | 일반행동 한 번을 **대신** 쓴다. 추가행동이 아니다 |
| 표기 | UI에는 `대체행동`으로 나온다. 문서 표기는 `N+` |
| 카운트 | 공격하지 않아도 `NotifyActionResolved()`를 불러 **일반행동 1회로 센다** — 철벽(6)처럼 행동 횟수를 세는 코드가 어긋나지 않게 한다 |

| 보유자 | 조건 | 대체행동 내용 |
| --- | --- | --- |
| 수르트 | 도발 중이 아닐 때 | 자기 도발 + 방어막 |
| 스카디 | 도발 중이 아닐 때 | 자기 도발 + 방어막 |
| 가우디 | 스택 6 | 3인 타격 + 풀 부착 |
| 잔 | 전투 후 첫 일반행동 (벌크업) | 자신의 STR +10% |
| 호루스 | 네 번째 일반행동 (우제트) | 추가 피해 + 방어 20% 무시 |
| 아문·라 | 치명타 발생 후 | 단일 강타 + 지옥불 |

### 4.6 일반행동에는 쿨타임이 없다

일반행동의 발동 주기는 **DEX가 만드는 행동치(AV)가 전담**한다.
일반행동은 쿨다운을 갖지 않으며, 실제 시전 시점은 `ActionScheduler`가 정한다.

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

| 분류 | 인원 | 캐릭터 |
| --- | ---: | --- |
| **Starter** | 7 | 아탈란테 · 오리온 · 테세우스 · 수르트 · 바유 · 아누비스 · 사바흐 |
| **Support** | 13 | 세이 · 가우디 · 라이트 · 찬드라 · 프레이아 · 스카디 · 쿠베라 · 바루나 · 오르페우스 · 아그리파 · 토트 · 이시스 · 마리 |
| **Locked** | 17 | 시 · 피그말리온 · 아스클레피아 · 아마테라스 · 케찰코아틀 · 츠쿠요미 · 야마 · 아그니 · 인드라 · 로키 · 스사노오 · 옥타비아 · 카이사르 · 호루스 · 바스테트 · 세트 · 이카리아 |

🔴 **`Locked` 17인 전원에게 획득 경로가 없다.** 해금 상태의 영구 저장도 아직 구현되지 않았고,
합류시킬 영입 사건도 하나도 없다. **실제로 편성 가능한 것은 Starter 7 + Support 13 = 20명뿐이다.**
[Design_Backlog](Design_Backlog.md) 항목 5·21.

## 6. 캐릭터 데이터 스키마 (`10_units.yaml`)

```yaml
- id: 5
  name: 아탈란테
  characterType: Starter        # Starter / Support / Locked
  element: Dendro
  startingProficiencies: [Longbow, MediumArmor]
  startingItemIds: [4106]       # 선택. 없으면 맨손으로 시작한다
  tags: [Greek]
  canStartAsMain: true
  canStartAsSupport: false
  canUseInInfinite: true
  mainStat: DEX                 # 5스탯 어느 것이든 가능
  subStat: CON
  subStats: [CON]               # 복수 부스탯 캐릭터는 [CON, LUK]처럼 표기
  mainStatTrainingBonus: 0.20
  subStatTrainingBonus: 0.10    # 부스탯 2개면 각 0.05
  strBase: 16
  strIncrementLvl: 1
  strIncrementUpgrade: 1
  # dex / con / int / luk 동일 구조
  ultimateResourceType: Stack   # 선택. 생략하면 마나형
  ultimateResourceName: 우제트   # 선택. 스택형 게이지의 표시 이름
  ultimateResourceMax: 4        # 선택
  codes:
    passive: 2
    normal: 1
    ultimate: 3
  codeStages:
    passive: 1
    normal: 1
    ultimate: 1
  levelPassives:
    - { codeId: 140, unlockLevel: 4, stage: 1 }   # 선택
  portrait: ATALANTE_PORTRAIT
  standing: ATALANTE_STANDING                     # 선택
```

`ultimateResourceType`을 `Stack`으로 두면 궁극기 링이 칸으로 나뉘어 그려진다
(호루스의 우제트 4칸, 테스카틀리포카의 흡연경 8칸). 생략하면 연속형 마나 게이지다.

**폐지된 필드**: `atkBase`, `atkIncrementLvl`, `defBase`, `defIncrementLvl`
(DTO에서도 제거되었으므로 YAML에 남겨도 무시된다)


## 7. 현재 캐릭터

**총 37명.** 기초 스탯·레벨당 성장·트레이닝 보너스는 여기서 중복해 적지 않고
[Detail_08 공통 스탯 표](Detail_08_Confirmed_Characters.md#스탯-표기-규칙) 한 곳에서 관리한다.
전투 사양(P/N/U와 해금 패시브)은 [Detail_08](Detail_08_Confirmed_Characters.md),
코드 ID 색인은 [Detail_12](Detail_12_Code_Weapon_Catalog.md)를 본다.

| ID | 이름 | 분류 | 원소 | 주/부 | 역할 |
| ---: | --- | --- | --- | --- | --- |
| 21 | 아탈란테 | Starter | Dendro | DEX / CON | 후열 사수 · 지속피해 딜러 |
| 22 | 오리온 | Starter | Geo | STR / CON | 전열 탱커 · 힘사수 딜탱 |
| 23 | 테세우스 | Starter | Hydro | DEX / CON | 범용 전열 딜러 |
| 80 | 수르트 | Starter | Pyro | STR / CON | 도발·광역 방어막 전열 수호자 |
| 64 | 바유 | Starter | Anemo | CON / DEX | 정화형 방어 서포터 |
| 121 | 아누비스 | Starter | Geo | CON / STR | 전열 탱커 · 사령 특효 |
| 3 | 사바흐 | Starter | Electro | LUK / DEX | 디버프 연계 치명타 딜러 |
| 1 | 세이 | Support | Geo | INT / DEX | 공격형 서포터 → 육성 시 딜러급 |
| 4 | 가우디 | Support | Dendro | INT 단일 | 바위 연계 대체행동 서브딜러 |
| 6 | 니콜 | Support | Electro | INT / DEX·LUK | 아군 전기 부착 · 치명타 피해 서포터 |
| 7 | 잔 | Support | Cryo | STR / LUK | 노려지면 먼저 때리는 상시 도발 딜탱 |
| 5 | 라이트 | Support | Anemo | CON / DEX·INT | 궁극기 연계 정화·소환 서포터 |
| 60 | 찬드라 | Support | Geo | CON / LUK | 방어형 스타터 서포터 |
| 81 | 프레이아 | Support | Dendro | INT / DEX | 체력 감소 파티의 코어 힐러 |
| 83 | 스카디 | Support | Cryo | STR / CON | 도발·방어막·반격 전열 서포터 |
| 65 | 쿠베라 | Support | Geo | CON / STR | 베다·에어본 파티 탱커 |
| 66 | 바루나 | Support | Hydro | CON / INT | 감전 파티 코어 · 물 부여 |
| 25 | 오르페우스 | Support | Anemo | LUK / INT | 강인도를 부여하는 특수 서포터 |
| 100 | 아그리파 | Support | Geo | DEX / INT | 궁극기 딜러 보조형 서포터 |
| 124 | 토트 | Support | Hydro | INT / CON | 강인도 서포트 #2 |
| 125 | 이시스 | Support | Geo | CON / INT | 범용 바위 부여 서브딜러 |
| 160 | 마리 | Support | Cryo | LUK / DEX·STR | 치명타 파티 지휘 서포터 |
| 2 | 시 | 🔴 Locked | Anemo | DEX / LUK | 연타 암살자 |
| 20 | 피그말리온 | 🔴 Locked | Pyro | CON / STR·INT | 지속피해 반격 전열 탱커 |
| 24 | 아스클레피아 | 🔴 Locked | Electro | INT / CON·LUK | 하이브리드 치유·서브 딜러 |
| 40 | 아마테라스 | 🔴 Locked | Pyro | DEX / STR·LUK | 후열 일반행동 사수 |
| 140 | 케찰코아틀 | 🔴 Locked | Dendro | INT / LUK | 풀 파티 서브 딜러·지원 |
| 41 | 츠쿠요미 | 🔴 Locked | Electro | INT / LUK | 지속피해 술사 |
| 61 | 야마 | 🔴 Locked | Electro | INT / DEX·CON | 지속피해 정산 딜러 |
| 62 | 아그니 | 🔴 Locked | Pyro | INT / LUK | 불 파티 서브 딜러 |
| 63 | 인드라 | 🔴 Locked | Electro | INT / LUK | 번개 하이퍼캐리 |
| 82 | 로키 | 🔴 Locked | Pyro | LUK / STR | 추가행동 기반 사수 |
| 42 | 스사노오 | 🔴 Locked | Hydro | LUK / DEX | 에어본 파티 하이퍼캐리 |
| 101 | 옥타비아 | 🔴 Locked | Anemo | INT / LUK | 저스핏 고화력 궁극기 딜러 |
| 102 | 카이사르 | 🔴 Locked | Anemo | LUK / DEX | 로마 파티 공격형 서포터 |
| 120 | 호루스 | 🔴 Locked | Pyro | DEX / STR | 속도 고정형 사수 |
| 122 | 바스테트 | 🔴 Locked | Geo | LUK / DEX | 추가행동·반격 연계 암살자 |
| 123 | 세트 | 🔴 Locked | Pyro | STR / CON | 지속피해 파티의 전열 탱커 |
| 26 | 이카리아 | 🔴 Locked | Pyro | INT / CON·LUK | 체력 소모형 마법 딜러 |

🔴 표시는 **획득 경로가 없어 현재 편성할 수 없는 캐릭터**다.

### 7.1 획득 경로가 없는 캐릭터

**Locked 17인 전원이 여기 해당한다.** 데이터·전투 코드는 있으나 일부 아트가 미완성이고,
`characterType: Locked`는 "런 중 합류 시 영구 해금"을 뜻하는데 **합류시킬 영입 사건이 하나도 없다.**
해금 상태를 저장하는 코드도 아직 없다.

| 막고 있는 것 | 상태 |
| --- | --- |
| 영입 사건 | 🔴 없음. 현재 사건 4개는 전부 복선형이다 — [Detail_06 §6](Detail_06_Events.md) |
| 해금 영구 저장 | 🔴 미구현 |
| 사건 액션 | `grantUnitId`가 스키마에 있으나 데이터에 한 번도 쓰이지 않았다 |

임시로 열려면 `canStartAsMain` / `canStartAsSupport`를 켜면 된다.
설계 판단은 [Design_Backlog](Design_Backlog.md) 항목 5·21에 있다.

### 7.2 패시브 보유 현황

유닛마다 **고유 패시브 1개**를 가지며, 가우디와 라이트를 제외하면 해금 패시브 4~8개가 있다.
유닛별 정확한 목록은 [Detail_12 §2](Detail_12_Code_Weapon_Catalog.md)에 한 벌만 둔다.

| 해금 패시브 수 | 유닛 |
| ---: | --- |
| 8 | 세이 · 오리온 |
| 7 | 아탈란테 · 찬드라 · 바스테트 |
| 6 | 시 · 아스클레피아 · 아마테라스 · 케찰코아틀 · 수르트 · 야마 · 인드라 · 쿠베라 · 아그리파 · 카이사르 · 아누비스 |
| 5 | 피그말리온 · 츠쿠요미 · 바유 · 로키 · 스카디 · 바루나 · 오르페우스 · 스사노오 · 옥타비아 · 호루스 · 사바흐 |
| 4 | 테세우스 · 아그니 · 프레이아 · 세트 · 토트 · 이시스 · 이카리아 · 마리 |
| 0 | 가우디 · 라이트 (해금 패시브 미기획) |

팔랑크스(183)는 이 수에 포함하지 않는다. 해금 패시브가 아니라 **Greek 속성 유닛이 전원
하드코딩으로 갖는 진영 공통 코드**이며 코드 용량도 차지하지 않는다.

### 7.4 캐릭터 ID — 소속별 블록

**ID는 소속이 정한다.** 20칸씩 끊어 진영마다 한 블록을 준다. 새 캐릭터는 자기 소속
블록의 다음 빈 번호를 받는다. 블록이 차면 그때 뒤에 20칸을 더 붙인다.

| 구간 | 소속 | 인원 | 사용 중 | 캐릭터 |
| --- | --- | ---: | --- | --- |
| 1~19 | 아카샤 | 5명 | 1~5 | 세이 · 시 · 사바흐 · 가우디 · 라이트 |
| 20~39 | 그리스 | 7명 | 20~26 | 피그말리온 · 아탈란테 · 오리온 · 테세우스 · 아스클레피아 · 오르페우스 · 이카리아 |
| 40~59 | 다카마가하라 | 3명 | 40~42 | 아마테라스 · 츠쿠요미 · 스사노오 |
| 60~79 | 베다 | 7명 | 60~66 | 찬드라 · 야마 · 아그니 · 인드라 · 바유 · 쿠베라 · 바루나 |
| 80~99 | 노르드 | 4명 | 80~83 | 수르트 · 프레이아 · 로키 · 스카디 |
| 100~119 | 로마 | 3명 | 100~102 | 아그리파 · 옥타비아 · 카이사르 |
| 120~139 | 이집트 | 6명 | 120~125 | 호루스 · 아누비스 · 바스테트 · 세트 · 토트 · 이시스 |
| 140~159 | 메히코 | 1명 | 140 | 케찰코아틀 |
| 160~179 | 갈리아 | 1명 | 160 | 마리 |

세이·시·가우디·라이트는 세계관의 중심축인 아카샤 블록에 둔다.
케찰코아틀도 소속 태그가 없어 테마(메히코)를 기준으로 넣었다.

적은 이 표를 쓰지 않는다. `60_enemies.yaml`이 별도 ID 공간(1020~ · 3000~)을 쓴다 —
[Detail_09](Detail_09_Enemy_Catalog.md)를 본다.

> **ID를 바꾸면 저장본이 깨진다.** 진행 중이던 런의 아군은 ID로 복원되므로,
> 재배치할 때는 `RunSaveData.CurrentVersion`을 함께 올려 옛 저장본을 폐기한다.
> 이번 재배치에서 4 → 5로 올렸다.

### 7.5 코드 등급 — 일반(은색)과 강화(금색)

같은 효과 계열에 두 코드가 있으면 **강화 등급이 일반 등급을 대체한다.**
서포터 전수로 두 등급을 함께 들 수 있기 때문에, 일반 등급 코드는
`PassiveCode.SupersededByCodeId`에 자신을 덮는 강화 코드 ID를 적어 둔다.
`Unit.TryCastPassiveCode`가 발동 직전에 그 코드를 배웠는지 보고 아예 발동시키지 않는다.

| 등급 | 색 | 뜻 |
| --- | --- | --- |
| 일반 | 은색 | 기본 판. 같은 계열의 강화 등급을 함께 들면 발동하지 않는다 |
| 강화 | 금색 | 같은 계열의 일반 등급을 대체한다 |

한 유닛이 함께 들 수 있는 계열만 이렇게 막는다. 서로 다른 유닛이 들고 필드 전체에
퍼지는 오라 계열(원소술사 269 ↔ 대해의 판결 274 등)은 매 순간 높은 쪽을 골라야 하므로
최댓값 판정이나 공용 상태 키로 처리하고, 등급은 표시로만 쓴다.
계열 목록은 [Detail_12 §3.2](Detail_12_Code_Weapon_Catalog.md)에 한 벌만 둔다.

> **열화 전수본 제도는 폐지되었다.** 고유 패시브는 전수되지 않으며,
> 서포터 카드가 넘겨줄 수 있는 것은 해금 패시브뿐이다.
> 옛 전수본 230·231·233·224는 삭제했고, 232만 이시스의 독립 해금 패시브 `생명의 물`로 재편입되었다.

## 8. 적 데이터 스키마 (`60_enemies.yaml`)

캐릭터 스키마와 동일하며 다음 필드가 추가된다.

| 필드 | 설명 |
| --- | --- |
| `themeId` | 테마의 `enemyThemeId`와 매칭 |
| `archetype` | 편성 패턴 매칭 + 배치 열 결정 |
| `tier` | `normal` / `elite` / `boss` |

**적도 `mainStat` / `subStat`를 가진다.** 개편 전에는 빈 문자열이었으나,
위력이 주스탯에서 파생되므로 반드시 지정해야 한다.
