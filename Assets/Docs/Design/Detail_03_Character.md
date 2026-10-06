# 상세 기획서 03 — 캐릭터와 스탯

> **3계층 문서.** 8스탯 모델, 레벨·성장, 기본 코드 3분류와 선택형 특수행동, 집중, 캐릭터 데이터 스키마.
> 개념 정의는 [서브 기획서](GDD_Sub_Concepts.md) 3장을 본다.

최종 갱신: 2026-10-06 (특공/특방의 효과 명중·저항 역할과 통상 기본 명중 75% 반영; 영구 중첩·소환수·성장·현행 로스터 유지)
관련 코드: `Unit.cs`, `UnitStats.cs`, `CombatantData.cs`, `Code.cs`, `CodeFactory.cs`, `Combat/Concentration.cs`, `Combat/Summons.cs`
관련 데이터: `10_units.yaml`, `20_codes.yaml`, `60_enemies.yaml`

> **2026-09 전면 개편.** 5스탯(STR·DEX·CON·INT·LUK) · 훈련 강화 · 주/부 스탯 배율 · 포켓몬식 위력 ·
> 해금 패시브 · 코드 등급 사다리와 옛 전원 공용 특수행동 체계는 사라졌다. 2026-09-28 찬드라부터
> **특정 트리거가 여는 선택형 SP 슬롯**만 최소 규격으로 다시 사용한다. 옛 설계는 커밋 `a6194c9`에 남아 있다.

---

## 1. 스탯 모델 — 여덟 스탯

전투 수치는 아래 여덟 스탯이 전부다. 파생 스탯은 없다 — 데이터에 적은 값이 곧 전투 값이다.

| 스탯 | 키 | 하는 일 |
| --- | --- | --- |
| **최대 체력** | `MaxHp` | 체력 총량 |
| **공격력** | `Attack` | **거의 모든 피해의 계수** |
| **특수공격력** | `SpecialAttack` | 원소 반응의 위력, 해로운 효과의 명중. **피해 계수가 아니다** |
| **방어력** | `Defense` | 일반 피해 경감 |
| **특수방어력** | `SpecialDefense` | 지속피해 · 원소 반응 피해 경감, 해로운 효과 회피 |
| **속도** | `Speed` | 행동치(AV) = `10000 / 속도` |
| **치명타 확률** | `CritRate` | 0~1로 자른다 |
| **치명타 피해** | `CritDamage` | 치명타 배율 = `1 + 치명타 피해` |

**피해의 계수는 코드마다 정한다.** 대부분 공격력이고, 극소수가 방어력이나 최대 체력을 쓴다
(세이는 방어력 계수 — [Detail_08](Detail_08_Confirmed_Characters.md)). 어느 스탯을 계수로 쓰든 **경감은 방어 규칙 하나**를 탄다 — [Detail_02 §3](Detail_02_Combat.md).

### 1.1 성장 — 롤식 선형

```
기초 스탯 = 기초값 + 성장치 × (레벨 − 1)                  UnitStats.LeveledBase
최종 스탯 = 기초 스탯 × (1 + Σ퍼센트 보정) + Σ고정 보정     체력 · 공격 · 방어 계열 · 속도
최종 스탯 = 기초 스탯 + Σ고정 보정                         치명타 확률 · 치명타 피해
```

- 보정은 상태(`BaseEffect.StatFlatModifier` / `StatPercentModifier`)와 코드 효과가 준다. 장비 보정은 없다.
- 치명타 두 스탯은 이미 비율이라 퍼센트 보정을 받지 않는다(`UnitStats.IsPercentScalable`).
- 아군과 적이 **같은 공식**을 쓴다. 옛 "적은 Lv.41부터 성장 ×1.8" 가속은 훈련 강화를 따라잡으려던
  장치라 훈련과 함께 없앴다.

**표준 성장(설계 참고치, 레벨당)**

| 체력 | 공격 | 특공 | 방어 | 특방 | 속도 | 치확 | 치피 |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| +90 | +5 | +3.5 | +5 | +4 | 0 | 0 | 0 |

속도·치명타는 기본적으로 자라지 않는다. 필요한 캐릭터만 성장치를 적는다.

### 1.2 레벨이 끝없이 올라도 무너지지 않는 이유

무한 모드에서 레벨에는 상한이 없다. 모든 스탯이 선형으로 자라므로 **두 스탯의 비율**로 판정하는
식은 레벨과 무관하게 같은 값을 낸다. 그래서 스탯끼리 겨루는 판정은 전부 비율식이다.

| 판정 | 식 | 원본 |
| --- | --- | --- |
| 피해 경감 | `K / (K + 방어)`, `K = 100 + 6 × (공격자 레벨 − 1)` | [Detail_02 §3.2](Detail_02_Combat.md) |
| 효과 명중 | `기본 확률(통상 75%) × 2 × 특공 / (특공 + 특방)`; 적 원소 부착·지속피해·디버프, 아군은 확정 | [Detail_02 §3.5](Detail_02_Combat.md) |
| 반응 증폭 | `1 + 특공 / (특공 + K)` | [Detail_16](Detail_16_Elements.md) |
| 제어 턴 | `특공 / 특방` 비율 | [Detail_02 §4-A](Detail_02_Combat.md) |

`K`의 레벨당 증가분 6은 표준 방어 성장(+5)과 같은 기울기로 맞춘 값이다 — 같은 레벨끼리의
경감률이 Lv.1이든 Lv.500이든 거의 같다.

### 1.3 갱신 시점

`Unit.AttributesUpdate()`가 여덟 스탯과 마나 효율을 다시 모은다. 상태 · 레벨이 바뀐 뒤 부른다.
**체력 비율을 보존한다** — 최대 체력 버프는 현재 체력도 함께 늘린다.

## 2. 레벨과 EXP

| 대상 | 레벨의 출처 |
| --- | --- |
| 적 | **현재 스테이지** (`RoundManager.EnemyLevel = Stage`) |
| 아군 | **누적 EXP로만 오른다.** 훈련이 없으므로 레벨이 곧 성장의 전부다 |

**필요 EXP** (Lv.N → N+1) — `RequiredExp(N) = 100 + 28 × (N − 1)` (`Unit.ExpBaseRequirement` · `ExpRequirementPerLevel`)

| 획득처 | 지급량 | 대상 |
| --- | --- | --- |
| 적 처치 | `4 + 1 × Stage` | 필드의 파티 전원 |
| 스테이지 클리어 | `25 + 9 × Stage` | 필드의 파티 전원 |

전투 중 획득분은 `GameManager.pendingPartyExp`에 모았다가, 라운드 종료 때 **아군 스냅샷을
복원한 다음에** 지급한다(복원이 먼저 오면 성장이 덮여 사라진다).

> 🔸 훈련 EXP와 장비가 빠지면서 아군 레벨이 적 레벨보다 뒤처질 수 있다. EXP 곡선 조정 여부는
> [Design_Backlog](Design_Backlog.md)의 개편 항목에 있다.

## 3. 궁극기 자원 — 마나와 고유 스택

기본형 마나는 **행동과 피격으로** 찬다. 시간으로는 차지 않는다.

| 계기 | 마나 | 상수 |
| --- | ---: | --- |
| 일반행동 1회 | +20 | `Unit.ManaPerNormalAction` |
| 피격 1회 (지속피해 · 덧붙는 피해 제외) | +5 | `Unit.ManaPerHitTaken` |
| 처치 1회 | +10 | `Unit.ManaPerKill` |

- 모든 기본 마나 획득량에 마나 효율(`ManaEfficiencyCurr`, 기본 1, 효과만 바꾼다)을 곱한다.
- 최대치는 유닛 데이터의 **`manaMax`**(생략 시 100). 레벨 · 아이템 · 버프가 건드리지 않는 고유값이다.
- 차는 순간 궁극기가 줄을 선다. 턴을 쓰지 않고 AV도 리셋하지 않는다 — [Detail_02 §2](Detail_02_Combat.md).
- 시전 순간 마나를 0으로 비운다(`CastUltimateCode`).

`ultimateResourceType: Stack`인 유닛은 같은 정수 저장 칸과 HUD 오브를 쓰되 위 표의 공용 충전을 받지 않는다.
고유 코드가 `AddUltimateResource()`로 명시적으로 올리고, 준비 화면의 자원 복원은 0으로 초기화한다. 현재 사례는
사바흐의 **아즈라엘 7중첩**이며 표시명은 `ultimateResourceName: 아즈라엘`이다. 저장 DTO 구조가 달라지지 않아
`RunSaveData.CurrentVersion` 변경은 필요 없다.

### 3.1 영구 중첩

**런이 끝날 때까지 사라지지 않는 유닛별 카운터**다. 전투 · 라운드 · 휴식이 지나도, 유닛이 쓰러져도 남는다.
궁극기 자원(`Stack`)과 다르다 — 그것은 준비 화면에서 0으로 돌아간다. 첫 사례는 아누비스의 **영혼**이다([Detail_08 §9](Detail_08_Confirmed_Characters.md)).

| 항목 | 규칙 | 구현 |
| --- | --- | --- |
| 저장 | 유닛마다 `키 → (정수 · 소수 몫)` 목록으로 저장한다. 보상 코드 저장과 함께 **v12** | `UnitSaveData.permanentStacks`(`PermanentStackSaveData` key · value · fraction), `Unit.WriteRunState` / `RestoreRunState` |
| 획득 | **`Unit.GainPermanentStacks(key, amount)`가 유일한 입구**다. `amount`는 소수일 수 있다(아누비스: 소환수 0.25). 이 한 곳이 획득량 배율을 곱한다 | `Unit.cs`. 읽기는 `GetPermanentStacks` · `GetPermanentStackFraction`, 디버그 · 복원용 `SetPermanentStacks` |
| 보정 | 보유자의 활성 코드가 `PassiveCode.PermanentStackGainMultiplier(key)`로 배율을 낸다. 모두 곱한다. 축적의 촉진(×1.5)은 상위 겹겹이 쌓인 운명(×2)을 받으면 사라지므로 둘이 겹치지 않는다 | `Codes/Passive/Reward/RewardCodesT3.cs` `MultiStackCode`, `RewardCodesT4.cs` `LayeredFateCode` |
| 소수 | 키마다 소수 몫(0 ≤ x < 1)을 함께 저장하고, 합이 1이 될 때마다 정수 중첩으로 옮긴다. 억제 상한 대신 **획득량 자체를 낮춰** 가속 폭을 정한다. 부동소수 오차(0.25 × 4)로 한 칸을 놓치지 않게 아주 작은 여유를 둔다 | `Unit.GainPermanentStacks` |
| 알림 | 정수가 늘어난 순간 `Unit.AnyPermanentStackGained(보유자, 키, 늘어난 정수량)`를 발행한다 | 아누비스 카드 배지가 이 이벤트를 듣는다 |
| 키 선언 | 중첩을 쌓는 고유 코드는 `PassiveCode.PermanentStackKey`로 키를 알린다. 축적의 서 · 무한의 탑 같은 성장 보상 코드가 이 값으로 "자신의 영구 중첩"을 찾는다 | 지금 키는 `anubis.soul` 하나 |
| 표시 | 카드 우상단 숫자 배지에 정수(여러 자리)를 늘 보인다. `CombatResourceLabels`로 이름을 붙인다. 🔸 툴팁에 소수 첫째 자리까지 적는 표시는 아직 없다 | `AnubisSoulHarvest`가 전투 자원 칸에 영구 값을 비춘다 |
| 유닛 태그 | 영구 중첩을 쌓는 유닛은 `Growth`(성장) 태그를 단다 — §5.1 | `10_units.yaml` `archetypeTags` |

성장 보상 코드(축적의 서 · 축적의 촉진 · 겹겹이 쌓인 운명 · 무한의 탑)는 **보유자 자신의 영구 중첩만** 바꾼다 —
[Detail_17 §4](Detail_17_Reward_Codes.md). 파티에 `Growth` 유닛이 없으면 이 코드들은 보상 후보에서 빠진다.

## 4. 코드(스킬) 시스템

모든 유닛은 **고유 특성 1 + 일반행동 1 + 초월기(궁극기) 1**을 가진다. 특정 트리거에 묶인 유닛만
선택형 **특수행동(SP) 1**을 더 가진다. 해금 패시브는 없다.

| 분류 | 기반 클래스 | 발동 |
| --- | --- | --- |
| 특성 | `PassiveCode` / `UniquePassiveCode` | 라운드 시작에 활성화, 이벤트를 듣다 행동을 예약하거나 값을 바꾼다 |
| 일반행동 | `BaseNormalCode` | 행동 차례 |
| 특수행동(선택) | `SpecialCode` | 캐릭터별 특정 트리거가 `ActionScheduler`에 예약 |
| 초월기 | `BaseUltimateCode` | 마나가 차면 |

어떤 코드에도 쿨타임이 없다. 속도가 일반행동의 주기를, `manaMax`가 궁극기의 주기를 정한다.

### 4.1 피해 — 고정값 + 스탯 × 계수

```csharp
int damage = StatDamage(flat: 55, BaseEnums.StatType.Defense, ratio: 0.5f);   // 55 + 방어력 50%
bool crit = RollCritical(out float mult);                                       // 치명타면 mult 배
```

`Code.StatDamage`가 시전자의 **현재 최종 스탯**으로 값을 만든다. 위력(`Power`) 체계는 없다.

### 4.2 일반행동 — `BaseNormalCode`

```
WaitForCast → SelectTarget → Targeting.Announce → Deliver → NotifyActionResolved
```

- `IsContact`면 **카드가 날아가 부딪힌다**(`CardMotion.Charge` + `ImpactFx.Slam`).
- 아니면 카드가 들썩이며 **카드 정중앙에서 투사체가 날아간다**(`CardMotion.Throw` + `ProjectileFx.Fly`).
- 하위 클래스는 `BaseDamage(target)`과 필요하면 `ProjectileFor` · `OnAttackResolved`만 채운다.
- 피해를 주는 코드는 `Code.AttackForm`으로 **공격 형태**(베기 · 찌르기 · 사격 · 타격 · 주문, `DamageTag` 50000번대)를 선언한다. 보상 풀이 "파티에 그 형태의 공격이 있는가"를 묻는 데 쓴다 — [Detail_02 §6](Detail_02_Combat.md), `Combat/AttackForms.cs`.
- `SkillCoroutine`을 통째로 바꾸는 코드도 행동이 실제로 끝나면 `NotifyActionResolved()`를 불러야 한다.

### 4.3 초월기 — `BaseUltimateCode`

`Perform()` 코루틴 하나를 채운다. `isCasting` 처리, 살아 있는 적 · 아군 목록, 우선도 대상 선택,
`BuildContext`를 기반 클래스가 준다.

### 4.4 행동과 호출

행동(스케줄러 슬롯을 쓰는 것)은 일반행동 · 대체행동 · 추가행동 · 특수행동 · 궁극기다.
특성 · 상태 · 이벤트 훅은 **호출**이다 — 스스로 행동하지 않고 행동을 예약하거나 값을 바꾼다.

| 행동 | 턴 | AV | 비고 |
| --- | :-: | :-: | --- |
| 일반행동 | 쓴다 | 리셋 | |
| 대체행동 | 쓴다 | 리셋 | 일반행동 자리를 대신한다. 공격하지 않아도 `NotifyActionResolved()` |
| 추가행동 | 안 쓴다 | 유지 | 특성이 부른 것은 우선 추가행동(`PriorityAdditional`)으로 같은 턴에 먼저 나간다 |
| 특수행동 | 안 쓴다 | 유지 | `OnSpecialActivates`. 추가행동으로 세지 않는다 |
| 궁극기 | 안 쓴다 | 유지 | |

세이의 빅뱅처럼 특성이 부르는 공격도 실제로는 **우선 추가행동**이다.

### 4.5 집중

**효과의 수명을 시전자에게 묶는 규칙이다.** `Combat.Concentration`이 관리한다.

| 규칙 | 내용 |
| --- | --- |
| 지속 | **시전자의 턴**으로 센다. 시전자의 턴이 끝날 때 1씩 준다 |
| 해제 | 지속 종료 · **시전자가 행동불능(제어)에 걸림** · 시전자 사망 · 라운드 종료 |
| 중복 | 같은 시전자가 같은 키로 다시 집중하면 이전 것을 끝내고 새로 건다(`Replaced`) |
| 끝날 때 | 코드가 넘긴 콜백이 `EndReason`(Expired · Interrupted · Replaced · RoundEnd)을 받아 효과를 걷는다 |

집중 효과를 받은 쪽(아군 전체 등)이 무엇을 당하든 상관없다. 오직 시전자의 상태만 본다.

### 4.6 코드 등록 절차

1. C# 구현 — `Assets/Scripts/Codes/{Passive|Normal|Special|Ultimate}/`
2. `CodeFactory`의 해당 슬롯 스위치에 ID 연결
3. `20_codes.yaml`에 이름 · 설명 등록

ID 규칙은 [Detail_11 §1](Detail_11_Code_Catalog.md).

### 4.7 소환수

우치의 분신부터 쓰는 공용 규칙이다. 소환수는 `Combat/Summons.cs`가 만들고 세며, 소환자의 고유 코드가
`ISummonerPolicy`(방식 · 상한 · 정의 · 넘침 처리)를 구현해 `Summons.Register(소환자, 정책)`으로 건다.
피그말리온은 실제 소환수는 없고 **소환수 판정**(`Unit.CountsAsSummon`)만 받는다.

| 항목 | 규칙 | 구현 |
| --- | --- | --- |
| 자리 | 칸을 차지하지 않는다. 소환자 카드 뒤에 겹쳐 서고, 별도 표기가 없으면 적의 대상이 되지 않는다(`SummonSpec.Targetable = false`) | `GridManager.summonList` · `SpawnSummon` · `SummonOverhang`, `Unit.InitializeAsSummon` |
| 정의 | 소환수는 유닛 데이터 표(`10_units.yaml`)에 넣지 않는다 — 선택 화면에 섞이기 때문이다. 소환자 코드가 `SummonSpec`(ID · 종류 · 이름 · 체력 · 고정 속도 · 일반행동 코드 ID)을 들고 있다. 소환수 ID는 900번대(우치 분신 901), 소환수의 일반행동 코드는 일반행동 네임스페이스 500번대(분신 500) | `Combat.SummonSpec`, [Detail_11 §1.4](Detail_11_Code_Catalog.md) |
| 행동 | 소환자 코드가 정한다. 고정 속도(`Speed > 0`)를 가진 소환수는 **행동치를 가지고 행동서열에 끼며** 소환자의 스탯 · 버프를 물려받지 않는다(체력 1, 치명타 0). 속도 0인 소환수는 소환자 코드가 지시할 때만 움직인다. 고정 속도 소환수는 스케줄러의 **추월 제한 밖**이다 — 한 번 치고 사라지는 분신이 제한에 끼어 느린 분신을 기다리면 안 된다 | `Unit.TakesScheduledTurns`, `ActionScheduler`(소환수는 `summonList`에서 참가 · 추월 제한 면제) |
| 방식 | **다수형 `Swarm`** — 여러 기를 상한까지 거느리고 소모한다(우치). **동반형 `Companion`** — 한 기와 함께 움직인다. 외부 생성 효과(쿠베라 초월기)는 `Summons.RequestExtra`로 다수형에게만 소환수를 더 주고, 동반형에게는 대체 효과 없이 피해 증가만 준다 | `Combat.SummonStyle` |
| 상한 | 소환자 정책의 `Cap`이 정한다. 상한에서의 추가 생성은 정책의 `OnOverflow`(**넘침**)를 먼저 부른다 — 생성 판정은 그대로 일어난다. 넘침이 자리를 비우지 못하면 새 소환수는 생기지 않는다 | `Summons.Create` |
| 피해 | 소환수의 공격은 **소환자 명의**(처치 · 마나 귀속)로 기록하고 소환자의 현재 스탯으로 계산하며, 소환수 피해로 센다 — 피그말리온(`CountsAsSummon`) 또는 `SummonAttack`(20006) 태그가 소환수 피해다. 갈라테아 · 소환술사 · 소환사 · 쿠베라 후원이 받는다 | `DamageTag.SummonAttack`, `KuberaPatronage.IsSummonDamage` |
| 판정 | 생성은 `Combat.Summons.AnySummonCreated`(소환자, 소환수, 경위, 표식 — 넘쳐 곧바로 사라질 소환수도 센다), 소모 · 넘침 · 소환자 사망에 의한 소멸은 `Unit.Die()`를 지나는 **사망 판정**(`Unit.AnyUnitDied`)이다. 소환자가 쓰러지면 남은 소환수도 흩어진다(사망 판정). **라운드 종료로 사라지는 것은 사망이 아니다**(`Summons.ClearAll`) | `Combat/Summons.cs` |
| 구독 | 정적 이벤트를 듣는 코드는 사망 · 라운드 종료에 구독을 푼다 | |

생성을 듣는 쪽(쿠베라)과 사망을 듣는 쪽(아누비스)이 같은 분신 하나에서 각자 이득을 본다 — [Detail_08 §12](Detail_08_Confirmed_Characters.md).
소환수 피해 증가 보상 코드(소환술사 · 소환사)는 [Detail_17](Detail_17_Reward_Codes.md)에 있다.

## 5. 캐릭터 분류

캐릭터는 **해금 / 미해금**으로만 나뉜다. 메인 · 서포트 구분도, 초기 서포터 구분도 없다.

| `characterType` | 뜻 |
| --- | --- |
| `Unlocked` | 처음부터 편성할 수 있다 |
| `Locked` | 계정 해금(영입 사건 등, `SaveSystem.AddStarterUnlock`)이 있어야 편성한다. `unlockCondition`을 적으면 선택 화면에 회색 미해금 타일로 뜬다 |

판정은 `CharacterSelectionManager.IsUnlocked` 한 곳이다. 해금한 캐릭터는 누구나 무한 모드 편성(1~5명)에 오른다.
**해금 코드(레벨로 열리는 패시브)는 어떤 유닛에도 없다.** 선택형 특수행동은 레벨 해금이 아니라 고유 트리거 슬롯이다.

현재 로스터는 **세이 · 시 · 사바흐 · 잔 · 피그말리온 · 히미코 · 찬드라 · 인드라 · 바유 · 쿠베라 · 바스테트 · 아누비스 · 우치 · 야마 · 바루나 · 아그니 · 수리야 열일곱 명**(모두 Unlocked)이다 — [Detail_08](Detail_08_Confirmed_Characters.md).

### 5.1 전투 성향 UI 태그 (`archetypeTags`)

| 키 | 표시 | 설명 |
| --- | --- | --- |
| `Burst` | 방출 | 궁극기가 주력 딜링이나 버프 유지의 핵심이라 **마나 효율 · 궁극기 피해 증가를 잘 받는** 유닛. 초월기에 피해가 있다는 것만으로는 달지 않는다(우치 · 쿠베라 제외). `Stack` 자원 유닛(사바흐)은 예외로 판단한다 |
| `Precision` | 정밀 | 지속적인 피해를 입힘 |
| `FollowUp` | 연속 | 추가 행동을 주로 사용 |
| `Swift` | 신속 | 추가행동을 하거나 추가행동 지원에 특화 |
| `Support` | 지원 | 아군 지원에 특화 |
| `Control` | 제어 | 적 방해·약화에 특화 |
| `Healing` | 치유 | 아군 체력 회복 가능 |
| `Shielding` | 방어 | 아군에게 방어막 부여 가능 |
| `Infusion` | 부여 | 적에게 원소 부여에 특화 |
| `Sturdy` | 견고 | 매우 높은 생존력 |
| `Special` | 특수 | 특정 트리거로 발동되는 특수행동 보유 |
| `Summon` | 소환 | 소환수 관련 능력 보유 |
| `Indomitable` | 불굴 | 자신의 체력을 자원으로 소모하거나 그와 연관됨 |
| `Growth` | 성장 | 런 내내 영구 중첩을 쌓아 강해짐 (§3.1). 현재 아누비스 |
| `Domination` | 지배 | 지속피해 또는 행동 방해 상태를 부여함 |

원본은 `UnitTagCatalog`다.

## 6. 데이터 스키마

아군(`UnitData`)과 적(`EnemyData`)은 공통 필드를 `CombatantData`에서 물려받는다.

```yaml
- id: 1
  name: 세이
  tagline: "..."                  # 아군만. 캐릭터 선택 화면 한 줄 소개
  characterType: Unlocked         # 아군만. Unlocked / Locked
  element: Geo
  startingProficiencies: [Orb]
  startingItemIds: [4109]
  tags: [Akasha, Divine]
  archetypeTags: [Swift, Support, Shielding]   # 아군만
  hpBase: 1050                    # 스탯마다 …Base / …Growth 한 쌍
  hpGrowth: 95
  atkBase: 55
  atkGrowth: 3.5
  spAtkBase: 60
  spAtkGrowth: 3.5
  defBase: 85
  defGrowth: 6.5
  spDefBase: 70
  spDefGrowth: 5
  speedBase: 112                  # 생략 시 100
  critRateBase: 5                 # 퍼센트 포인트. 생략 시 5
  critDamageBase: 50              # 퍼센트 포인트. 생략 시 50
  manaMax: 120                    # 생략 시 100
  codes: { passive: 201, normal: 1, ultimate: 1 }
  portrait: SEI_PORTRAIT
  standing: SEI_STANDING
```

적은 여기에 `themeId` · `archetype`(배치 열) · `tier`(normal/elite/boss) · `drops`를 더한다.

## 7. 캐릭터 ID — 소속별 블록

**ID는 소속이 정한다.** 20칸씩 끊어 진영마다 한 블록을 준다.

| 구간 | 소속 | 사용 중 |
| --- | --- | --- |
| 1~19 | 아카샤 | 1 세이 · 2 시 · 3 사바흐 · 7 잔 |
| 20~39 | 그리스 | 20 피그말리온 |
| 40~59 | 다카마가하라 | 44 히미코 |
| 60~79 | 베다 | 60 찬드라 · 61 야마 · 62 아그니 · 63 인드라 · 64 바유 · 65 쿠베라 · 66 바루나 · 67 수리야 |
| 80~99 | 노르드 | — |
| 100~119 | 로마 | — |
| 120~139 | 이집트 | 121 아누비스 · 122 바스테트 |
| 140~159 | 메히코 | — |
| 160~179 | 갈리아 | — |
| 180~199 | 화령 | 180 우치 |

> **ID를 바꾸면 저장본이 깨진다.** 저장 포맷은 개편으로 **v9**, 보상 코드 · 영구 중첩 도입으로 **v12**, 활성/보관 분리로 **v13**까지 올랐다(v12 자동 이관)(`RunSaveData.CurrentVersion`).
> 소환수 정의 ID는 아군 유닛 ID와 겹치지 않게 900번대를 쓴다(우치 분신 901) — §4.7.


### 베다 전투 자원 (2026-10-05)

아그니의 판돈·추공 티켓과 수리야의 탄환·강탄·현상수배는 전투 안에서만 유지한다. 영구 스택이나 런 저장 필드를 추가하지 않는다. 수치·완료 조건·정리 원본은 [Detail_08 §17·18](Detail_08_Confirmed_Characters.md)이다.

### 보상 코드 활성/보관 (2026-10-06)

유닛당 **활성 7칸**과 개수 제한 없는 코드 보관함을 둔다. 보관 코드는 패시브 인스턴스·스탯·전투·정산 효과를 내지 않는다.
준비 중 무료 드래그로 활성/보관 및 아군 간 교환한다. 동일 코드·상하위 동시 활성은 금지한다. 장비·중량은 없다.
상세 획득·교환·인장·저장 v13 규칙은 [Detail_17 §2](Detail_17_Reward_Codes.md)를 따른다.
