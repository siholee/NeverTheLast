# 상세 기획서 02 — 전투

> **3계층 문서.** 전장 구조, 행동 순서, 피해 공식, 보호막, 원소, 상태, 이벤트 훅.
> 개념 정의는 [서브 기획서](GDD_Sub_Concepts.md) 2장을 본다.

최종 갱신: 2026-08-10
관련 코드: `GridManager.cs`, `ActionScheduler.cs`, `Unit.cs`, `UnitStatus.cs`, `BaseEffect.cs`, `Cell.cs`

---

## 1. 전장 구조

세븐나이츠식 **전열/후열** 구조다. 각 진영은 2열이고, **한 열에 4명**이 세로로 늘어선다.

```
   아군 후열   아군 전열        적 전열   적 후열
    (x=-2)     (x=-1)          (x=1)    (x=2)
      □          □      ⚔       □         □      y=1
      □          □              □         □      y=2
      □          □              □         □      y=3
      □          □              □         □      y=4

              대기석  □ □ □ □ □   (전투 미참여)
```

| 항목 | 값 | 코드 |
| --- | --- | --- |
| x 범위 | `[-2, 2]`, **`x = 0` 없음** | `GridManager.xMin/xMax` |
| y 범위 | `[1, 4]` | `GridManager.yMin/yMax` |
| 진영당 | **2열 × 4행 = 8칸** | |
| 전열 | `x = ±1` | `GetFrontColumn()` |
| 후열 | `x = ±2` | |
| 대기석 | **5칸** | `GridManager.benchSize` |

### 1.1 화면 배치 상수

격자가 아니라 **마주 보는 전선**으로 보이도록 간격을 다르게 준다.

| 상수 | 값 | 의미 |
| --- | --- | --- |
| `FrontLineGap` | 26 | 양 진영 전열 사이의 거리 |
| `RowDepthGap` | 16 | 같은 진영의 전열↔후열 거리 |
| `SlotSpacing` | 10 | 한 열 안 4명의 간격 |
| `BenchOffsetY` | 14 | 전장 최하단에서 대기석까지 |

y는 중앙 정렬된다(1~4 → +15 / +5 / −5 / −15).

### 1.2 배치 규칙

| 대상 | 규칙 |
| --- | --- |
| 아군 | 전열(−1) 우선, 가운데 행(y=2,3)부터 채운다 — `TryFindOpenAllyPosition` |
| 적 | `archetype` 7·8·9 → 전열(x=1), 그 외 → 후열(x=2). 각 열 최대 4 — `GetColumnForArchetype` |

- 셀 이름 규칙: `Cell_x_y`
- 실제 출전 인원은 파티 상한(5명)에 종속된다 — 8칸을 다 채울 수 없다
- 대기석 유닛은 전투 판정과 UI 모두에서 제외된다
- 유닛 사망 시 셀은 **2초 예약 대기** 후 재사용 가능해진다

## 2. 행동 순서 — 행동치(AV) 모델

붕괴: 스타레일의 행동치 모델을 따른다. `ActionScheduler`가 전담한다.

### 2.1 공식

| 값 | 계산식 | 상수 |
| --- | --- | --- |
| 속도 | `100 × ActionSpeedCurr` | `SpeedScale = 100` |
| 행동치(AV) | `10000 / 속도` | `BaseActionValue = 10000` |
| AV 감소 | 초당 **40** | `DrainRate = 40` |

`ActionSpeedCurr = 1 + 최종 DEX × 0.01`

독립적인 공격속도 스탯이나 공격속도 버프는 존재하지 않는다. 행동 빈도를 높이는 효과는 모두 DEX를 증가시키며, 행동 속도는 그 최종 DEX에서만 파생된다.

### 2.2 기준 템포

| DEX | 속도 | AV | 행동 주기 |
| --- | --- | --- | --- |
| 0 | 100 | 100 | **2.5초** |
| 10 | 110 | 90.9 | 약 2.27초 |
| 50 | 150 | 66.7 | 약 1.67초 |
| 100 | 200 | 50 | 1.25초 |

> **`DrainRate`가 전투 전체의 템포를 정하는 유일한 상수다.** 전투가 너무 길거나 짧으면 여기부터 만진다.

### 2.3 규칙

| 규칙 | 내용 |
| --- | --- |
| 직렬 실행 | 한 번에 한 유닛만 행동한다. 행동 중에는 다른 유닛의 AV가 멈춘다 |
| 일반공격 포함 | 일반공격도 예외가 아니다 (병렬 타격 없음) |
| 궁극기 우선 | 속도와 무관하게 조건 충족 즉시 예약되고, 대기 중인 일반 행동을 제친다 |
| 안전장치 | 행동이 제한 시간을 넘기면 강제 종료 — `isCasting`을 되돌리지 못하는 코드가 있어도 전투가 멈추지 않는다 |
| 초기화 | `ActionScheduler.BeginRound()` — 유닛이 전부 배치·활성화된 **뒤에** 호출한다 |

### 2.4 일반 코드 쿨다운

```
normalCooldown -= Time.deltaTime × CodeAcceleration × ActionSpeedCurr
```

## 3. 피해 계산

### 3.1 파이프라인

```
1) OnBeforeDamageTaken           (자신, 공격자)
2) 회피 판정                      EvasionChanceCurr
3) OnTakingDamage                 실제 계산 (아래 3.2)
4) 보호막 흡수 → 체력 차감
5) 사망 방지 판정                  BaseEffect.TryPreventDeath
6) OnAfterDamageTaken            (자신, 공격자)
7) OnDamageDealt                 (공격자에게 실제 가한 피해량 전달)
8) 체력 0 이하 → Die() → OnDeath
```

### 3.2 공식 — 포켓몬식 위력 + 롤 방식 방어력

```
기본피해   = 스킬 위력 × 시전자 주스탯 × 0.2
유효방어력 = max(0, 방어력 − 관통) × 방어무시배율
방어배율   = 기준값 / (기준값 + 유효방어력)          // 방어력 ≥ 0
           = 2 − 기준값 / (기준값 − 유효방어력)      // 방어력 < 0
최종피해   = max(1, round(기본피해 × 치명타 × 주는피해보정 × 방어배율 × 받는피해보정) − 내구)
```

| 요소 | 출처 |
| --- | --- |
| 스킬 위력 | `Code.Power` / `Code.StagePowers[단계]` — **스킬마다 고정** |
| 주스탯 | 시전자의 `mainStat` (스킬이 다른 스탯을 지정할 수도 있다) |
| **방어력** | **STR × 1** (`UnitStats.DefensePerStrPoint`) — 비율 감소 |
| **내구** | 착용 방어구의 `durability` 합 — **고정 감소** |
| 기준값 | `100 + 10 × (레벨 − 1)` (`ArmorConstantBase` / `ArmorConstantPerLevel`) |
| 주는/받는 피해 보정 | `OutgoingDamageModifier` / `ReceivingDamageModifier` 누적곱 |

`DamageTag.TrueDamage`가 붙은 피해는 방어 감쇠를 건너뛴다.

#### 기준값이 레벨에 비례하는 이유

리그 오브 레전드는 기준값이 100 고정이지만, 롤은 아이템으로 피해가 초선형 성장한다.
이 게임은 **모든 스탯이 레벨에 선형 비례**하므로 기준값을 100으로 두면
방어력만 계속 커져 후반에 피해가 무한히 상쇄된다.

기준값을 레벨과 함께 키우면 **감소율이 레벨과 무관하게 일정**해진다.

| | Lv.1 | Lv.10 | Lv.50 | Lv.100 |
| --- | --- | --- | --- | --- |
| 무르밀로 (STR 낮음) | 12% | 12% | 11% | 11% |
| 스파르타쿠스 (STR 높음) | 33% | 33% | 33% | 33% |

즉 **방어력은 캐릭터의 정체성이지 레벨의 함수가 아니다.**

#### 내구 — 고정 경감

방어력이 **비율**로 깎는다면 내구는 **절대량**으로 깎는다.
계산의 맨 마지막에 적용되며, **방어 무시·관통의 영향을 전혀 받지 않는다.**

| 성질 | 결과 |
| --- | --- |
| 방어 무시 빌드에 남는 최후의 완충재 | 수르트처럼 방어력을 100% 무시해도 내구는 그대로 깎인다 |
| **다단히트에 특히 강하다** | 발마다 고정값이 빠지므로, 6발로 나눠 때리면 6번 깎인다 |
| 지속피해에는 적용하지 않는다 | 틱당 피해가 작아 내구가 곧 무효화가 되어 버린다 |

**다단히트 대비 효과 (내구 10 기준)**

| 공격 형태 | 총 피해 | 내구 적용 후 | 경감률 |
| --- | --- | --- | --- |
| 1발 300 | 300 | 290 | 3% |
| 6발 × 59 | 354 | 294 | **17%** |

즉 내구는 **저타수 고위력보다 다단히트를 훨씬 강하게 억제한다.**
세이식 유도탄 빌드에 대한 자연스러운 대항 수단이 된다.

> 🔸 **고정값이므로 티어를 따라 올리지 않으면 후반에 무의미해진다.**
> 상위 티어 방어구는 `T1 값 × (1 + (rarity−1) × 0.6)`을 기준으로 올린다.

#### CON과 STR의 역할 분담

| 스탯 | 내구 기여 | 성격 |
| --- | --- | --- |
| CON | 최대 체력 (절대량) | 한 방을 크게 버틴다 |
| STR | 방어력 (비율 감소) | 큰 피해일수록 많이 깎는다 |
| 장비 | 내구 (고정 감소) | 잔타를 무력화한다 |

실효 체력 = `체력 ÷ 방어배율`. 둘을 곱셈으로 쌓으므로 한쪽만 올리면 효율이 떨어진다.

**위력 기준선**

| 구간 | 위력 |
| --- | --- |
| 일반공격 | 38~70 |
| 패시브 연계타 | 60~150 |
| 궁극기 | 75~150 |

> 구 계수 체계에서 옮길 때는 **위력 = 기존 계수 × 50**을 썼다(계수 1.0 → 위력 50).

**기획 사양의 "스탯의 N%" 표기 = 위력 N**

캐릭터 사양서는 "INT의 40%에 해당하는 피해"처럼 적는다. 이를 코드로 옮길 때는
**N을 그대로 위력으로 읽는다** (`SkillDamage(40, PrimaryStat.INT)`).

문자 그대로 `INT × 0.4`로 구현하면 안 된다 — INT가 20~30대이므로 피해가 10 남짓이 되어
체력 1,200~35,000인 적에게 아무 의미가 없다. 실제로 츠쿠요미·찬드라가 그 상태였다.

**보호막·회복도 같은 척도를 쓴다.** "CON의 100%에 해당하는 방어막" → `SkillDamage(100, CON)`.

### 3.3 치명타

| 항목 | 값 |
| --- | --- |
| 확률 | `LUK × 1%` (0~100% clamp) + 효과 가산 |
| 배율 | **150%** 고정 + 효과 가산 (최소 100%) |

### 3.4 회피

| 항목 | 값 |
| --- | --- |
| 파생값 | **0** |
| 판정 | `Random.value < EvasionChanceCurr` → 피해 완전 무효 |

🔸 **파생 회피율이 0으로 고정되어 있다.** 현재 회피는 상태/장비 보정으로만 발생한다.
DEX의 설계 의도(속도 + 회피) 중 절반이 죽어 있다 — 재설계 또는 정식 제거 필요.

## 4. 보호막

| 규칙 | 내용 |
| --- | --- |
| 우선순위 | 체력보다 **먼저** 소모 |
| 관통 예외 | `DamageTag.ShieldPenetration` 피해는 보호막 무시 |
| 지속피해 예외 | `CodeType.Effect` (맹독·화상 등)는 보호막을 무시하고 체력에 직접 적용 |
| 부여량 증폭 | CON 기반 `ShieldBonusCurr` 적용 (`AddShield` / `SetShield`) |
| 소진 처리 | `ShieldCurr`가 0이 되면 `ShieldMax`도 0으로 초기화 |
| 지속 | 라운드 내 유지, `OnRoundEnd`에 정리 |

**흡수 처리**

```
보호막 >= 피해 : 보호막 −= 피해,  체력 변화 없음
보호막 <  피해 : 체력 −= (피해 − 보호막),  보호막 = 0
```

## 5. 피해 태그

| 태그 | 효과 |
| --- | --- |
| `FlatDamage` | 고정 피해 |
| `SplitDamage` | 분할 피해 |
| `ShieldPenetration` | 보호막 무시 |
| ~~`DefensePenetration`~~ | 🔸 **사문화** — 방어력이 폐지되어 아무 효과가 없다. enum은 남아 있다 |

## 6. 원소

### 6.1 원소 종류

`None, Pyro, Hydro, Dendro, Anemo, Electro, Cryo, Geo, Void` — 8종 + 무속성

### 6.2 부착 규칙

| 항목 | 값 |
| --- | --- |
| 고유 원소 | 유닛 데이터의 `element`. 라운드 내내 유지 |
| 부착 지속시간 | **9.5초** (`Unit.CommonElementAuraDuration`) |
| 근거 | 원신의 1U 오라 감쇠 시간 |
| 부여 | `GrantCombatElement(element, duration)` |
| 조회 | `HasCombatElement(element)` |
| 초기화 | 라운드 시작/종료 시 `ResetCombatElements()` — 고유 원소만 남는다 |

고유 원소를 다시 부착받아도 지속시간 관리 대상에서 제외된다(영구 보유).

### 6.3 원소 반응

두 원소가 한 유닛에게 겹치면 반응이 일어난다. **두 원소가 모두 소모되고** 지속피해가 남는다.

| 반응 | 조합 | 지속 | 피해 |
| --- | --- | --- | --- |
| **감전** | 물 + 번개 | 4초 | 매초 `위력 45 × 유발자 CON × 0.2` |
| **화상** | 불 + 풀 | 6초 | 매초 `위력 45 × 유발자 CON × 0.2` |

| 규칙 | 내용 |
| --- | --- |
| 판정 시점 | `Unit.GrantCombatElement` — 부착 직후 한 번 |
| 피해 기준 | **반응을 일으킨 유닛의 CON** (부착을 발생시킨 공격자) |
| 소모 | 반응한 두 원소는 고유 원소라도 걷힌다 (`RemoveCombatElement`) |
| 중첩 | `ExtendDuration` — 다시 반응하면 지속시간이 늘어난다 |
| 알림 | `Unit.AnyElementalReaction` 이벤트 (야마 '고전압'이 이 신호를 듣는다) |

**설계 의도** — CON을 기준으로 삼아 딜러가 아닌 유닛도 반응으로 기여할 수 있게 했다.
반응은 지속피해이므로 야마의 지속피해 증폭·정산 축과 그대로 맞물린다.

🔸 **미구현** — 원소 간 상성표(가위바위보식 피해 증감)와 원소 저항은 아직 없다.
남은 원소 조합(바위·바람·얼음·어둠)의 반응도 정의되지 않았다.

## 7. 상태(Status) 시스템

`UnitStatus` + `BaseEffect` 단일 체계. (구 `StatusEffect` 딕셔너리 시스템은 제거됨)

### 7.1 UnitStatus 구성

| 요소 | 내용 |
| --- | --- |
| 식별 | 상태 ID + 문자열 Key |
| 시전자별 중첩 | Key에 시전자 ID를 포함시켜 구현 |
| 표시 | 이름, 설명 |
| 지속시간 | **0 이하 = 무한**, 라운드 종료 시 일괄 정리 |
| 카테고리 | `Positive` / `Negative` / `Neutral` |
| 중첩 정책 | 아래 7.2 |
| 효과 | `BaseEffect` 인스턴스 여럿 (팩토리 ID 또는 직접 생성) |
| 이로움 표식 | `IsBeneficial` → 부여 시 `OnBeneficialEffectReceived` 발행 |

### 7.2 중첩 정책 (`StatusStackPolicy`)

| 정책 | 동작 | 대표 사례 |
| --- | --- | --- |
| `Stack` | 독립적으로 중첩 | 맹독 |
| `ExtendDuration` | 기존 효과에 시간 추가 | 화상 |
| `ReplaceIfStronger` | 더 강한 것만 유지 | 방어 감소 |
| `Ignore` | 중복 무시, 기존 유지 | 일회성 표식 |
| `Replace` | 무조건 교체 (지속시간/수치 갱신) | 일반 버프 |

### 7.3 BaseEffect 훅

**생명주기 훅** — `OnApply` / `OnUpdate` / `OnRemove`

**스탯 질의 훅**

| 훅 | 개입 지점 |
| --- | --- |
| `CritChanceAdditiveModifier` | 치명타 확률 가산 |
| `CritMultiplierAdditiveModifier` | 치명타 피해 가산 |
| `OutgoingDamageModifier` | 주는 피해 배율 |
| `ReceivingDamageModifier` | 받는 피해 배율 |
| ~~`DefenseStatMultiplierModifier`~~ | 🔸 **사문화** — 방어력 폐지로 피해 계산에서 호출되지 않는다 |
| `PrimaryStatBonus` / `PrimaryStatMultiplier` | 5스탯 가산/배율 |
| `ManaRecoveryMultiplierModifier` | 마나 회복 배율 |
| `ShieldBonusAdditiveModifier` | 보호막 부여량 가산 |
| `ShieldReceivedMultiplierModifier` | 받는 보호막 배율 (수르트 `천상의 신체`) |
| `CodeAccelerationAdditive` | 코드 가속 가산 |
| `MaxHpMultiplier` | 최대 체력 배율 |
| `TryPreventDeath` | 사망 직전 개입 (체력 1로 생존) |

### 7.4 상태 정의 두 가지 방식

| 방식 | 용도 | 위치 |
| --- | --- | --- |
| 정식 상태 | 여러 코드가 공유하는 디버프(맹독/화상 등) | `UnitStatus.LoadStatusData()` ID 스위치 |
| 코드 정의 버프 | 특정 코드 전용 | `BuffStatus.Create(...)`, `Effects/Buffs/BuffEffects.cs` |

**버프 상태 ID 대역** (`BuffStatusIds`)

| 대역 | 용도 |
| --- | --- |
| 10~ | 공용 버프 |
| 100~199 | 유닛 고유 버프 |

### 7.5 상태 부여 예시

```csharp
// 정식 상태 (EffectFactory ID 기반)
var status = new UnitStatus(statusId, Caster, target);
status.AddEffect(effectId, coefficient);
target.AddStatus(status);

// 코드 정의 버프 (직접 생성한 효과 객체)
target.AddStatus(BuffStatus.Create(
    BuffStatusIds.MyBuff, "MyBuffKey", "버프 이름",
    Caster, target, new PrimaryStatBonusBuffEffect(BaseEnums.PrimaryStat.STR, 4),
    duration: 4f));
```

## 8. 유닛 이벤트 훅

`BaseEnums.UnitEventType` — 리스너를 여러 개 등록할 수 있다.

| 이벤트 | 전달 인수 |
| --- | --- |
| `OnSpawn` | 자신 |
| `OnDeath` | 자신, 공격자 |
| `OnControlStarts` / `OnControlEnds` | 자신(, 공격자) |
| `OnPassiveActivates` / `OnNormalActivates` / `OnUltimateActivates` | 자신 |
| `OnNormalAttackHit` | 자신, 대상, DamageContext |
| `OnBeneficialEffectReceived` / `OnBeneficialEffectGranted` | 자신, 상대 |
| `OnBeforeDamageTaken` / `OnAfterDamageTaken` | 자신, 공격자 |
| `OnTakingDamage` | 자신, DamageContext |
| `OnDamageDealt` | DamageResolvedContext (실제 가한 피해량 포함) |
| `OnStageStart` / `OnRoundStart` / `OnRoundEnd` / `OnStageEnd` | 자신 |

### 8.1 등록·해제 패턴

영구 효과를 거는 패시브는 **해제 조건을 함께 등록**한다.

```csharp
public override void CastCode()
{
    ApplyHolyEnchant();

    Action<(Unit, Unit)> onDeathHandler = null;
    onDeathHandler = (deathInfo) =>
    {
        StopCode();
        Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, onDeathHandler);
    };
    Caster.AddListener(BaseEnums.UnitEventType.OnDeath, onDeathHandler);
}
```

> **해제 트리거를 등록하지 않으면 상태가 영원히 지속된다.**

## 9. 라운드 시작/종료 처리

**라운드 시작** (`DefaultRoundStartEvent`)
1. `ResetCombatElements()` — 원소 부착 초기화
2. `CastPassiveCode()` — 패시브 활성화

**라운드 종료** (`DefaultRoundEndEvent`)
1. `StatusController.ClearAll()` — 모든 상태 제거 (`OnRemove` 호출로 리스너 해제)
2. `ResetCombatElements()`
3. 보호막 초기화
4. 아군 필드를 전투 시작 시점 스냅샷으로 복원

## 10. 전투 밸런스 튜닝 노브

| 노브 | 위치 | 현재값 | 영향 |
| --- | --- | --- | --- |
| `DrainRate` | `ActionScheduler` | 40 | **전투 전체 템포** |
| `SpeedScale` | `ActionScheduler` | 100 | DEX 1당 속도 증가 비율 |
| **위력 배율** | `UnitStats.SkillPowerScale` | `0.2` | **전체 화력** — 위력 1 × 스탯 1이 만드는 피해 |
| **체력 계수** | `UnitStats.HpPerConPoint` | `100` | 전투 길이 |
| 스킬별 위력 | 각 `Code.Power` / `StagePowers` | 38~150 | 스킬 간 상대 강도 |
| 치명타 배율 | `GetDerivedCritDamage` | 1.5 | 딜러 분산 |
| 원소 부착 시간 | `CommonElementAuraDuration` | 9.5초 | 조건부 코드 발동 빈도 |
| 전투 제한 시간 | `GameManager.roundProgressTime` | 60초 | 장기전 페널티 |

### 10.1 현재 밸런스 실측

행동 직렬화로 전체 약 2.5행동/초, 일반공격 평균 위력 45(아군)/48(적)로 근사한 값이다.
아군 5인(세이·시·피그말리온·아탈란테·찬드라) 기준, **육성 강화는 제외**했다.

| 스테이지 | 편성 | 아군 Lv | 승리까지 | 전멸까지 | 여유율 |
| --- | --- | --- | --- | --- | --- |
| 1 | 일반 2기 | 1 | 10.3초 | 85.0초 | 12% |
| 3 | 일반 3기 | 4 | 13.4초 | 60.3초 | 22% |
| 4 | 마르켈루스(E) + 일반 | 5 | 17.0초 | 57.9초 | 29% |
| 6 | 일반 4기 | 8 | 16.3초 | 47.3초 | 34% |
| 7 | 일반 5기 | 9 | 22.0초 | 40.9초 | 54% |
| 8 | **사비나 — 중간 보스** | 11 | 20.0초 | 96.1초 | 21% |
| 9 | 필드 엘리트 + 일반 3기 | 12 | 24.2초 | 39.6초 | 61% |
| 10 | **스파르타쿠스 — 보스** | 13 | 29.8초 | 51.4초 | 58% |

여유율 = 승리까지 ÷ 전멸까지. 낮을수록 쉽다. 라운드 안에서 12% → 61%로 조여 오다가
보스에서 58%로 마무리되는 곡선이다. 전 구간이 60초 제한 안에 들어온다.

### 10.2 엘리트의 두 역할

같은 엘리트 등급이라도 출현 방식이 다르면 내구를 다르게 잡아야 한다.

| 적 | 역할 | 출현 | CON |
| --- | --- | --- | --- |
| 마르켈루스(2004) | **필드 엘리트** — 일반 적과 섞임 | 4·9스테이지 | 64 (+5/Lv) |
| 사비나(2005) | **중간 보스** — 단독 출현 | 8스테이지 | 105 (+15/Lv) |
| 스파르타쿠스(3003) | **보스** — 단독 출현 | 10스테이지 | 150 (+22/Lv) |

단독 출현하는 적은 아군 5인의 화력을 혼자 받아내므로 내구가 훨씬 높아야 한다.
**같은 적을 두 역할에 겹쳐 쓰면 한쪽이 반드시 망가진다** — 사비나를 9스테이지 편성에서 뺀 이유다.
