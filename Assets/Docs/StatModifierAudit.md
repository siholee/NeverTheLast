# 스탯 모디파이어 정리

이 문서는 현재 코드 기준으로 스탯이 어디서 오고, 어떤 순서로 전투 수치에 반영되는지 정리한다.

## 핵심 구조

유닛의 최종 전투 능력은 크게 세 층으로 나뉜다.

1. 기본 스탯: `10_units.yaml`, `60_enemies.yaml`의 STR/DEX/CON/INT/LUK 또는 레거시 HP/ATK/DEF/CRIT 환산값
2. 성장 스탯: 유닛 레벨에 따른 증가량
3. 런/트레이닝/장비 스탯: 보상, 트레이닝, 장비로 추가되는 업그레이드

메인 캐릭터는 `기본 스탯 + 성장 스탯 + 트레이닝 스탯 + 런 보상 + 장비`로 성장한다.

서포트 캐릭터는 전투 유닛으로 쓰일 때 `기본 스탯 + 성장 스탯 + 런 보상 + 장비`를 가진다. 육성 완료 후 서포트 카드로 등록될 때는 최종 스탯을 직접 계승하지 않고, 최종 기록에서 서포트 효과를 생성한다.

## 5스탯 계산

현재 5스탯 공식은 `Unit`의 `GetBase*` 계열 메서드에 있다.

```text
STR = StrBase + StrIncrementLvl * Level + StrIncrementUpgrade * StrUpgrade + 장비 STR
DEX = DexBase + DexIncrementLvl * Level + DexIncrementUpgrade * DexUpgrade + 장비 DEX
CON = ConBase + ConIncrementLvl * Level + ConIncrementUpgrade * ConUpgrade + 장비 CON
INT = IntBase + IntIncrementLvl * Level + IntIncrementUpgrade * IntUpgrade + 장비 INT
LUK = LukBase + LukIncrementLvl * Level + LukIncrementUpgrade * LukUpgrade + 장비 LUK
```

현재 플레이어 유닛 데이터는 레거시 HP/ATK/DEF/CRIT 중심이라, 5스탯이 비어 있으면 `LoadStatData`에서 임시 환산한다.

## 파생 전투 수치

현재 파생 공식은 다음과 같다.

```text
HP Max = CON * 1000
ATK = STR * 10
DEF = STR * 3
Crit Chance = LUK * 0.01, 0~1 clamp
Crit Damage = 1.5
Code Acceleration = 1 + DEX * 0.01 + CodeAccelerationRunBonus
Evasion = DEX * 0.0025, 최대 0.4
Healing Bonus = (CON - 10) * 0.01, 0~2 clamp
Shield Bonus = (CON - 10) * 0.01, 0~2 clamp
Mana Efficiency = 1 + (INT - 10) * 0.02, 최대 +2
Code Activation Chance = 0.75 + INT * 0.01, 0~1 clamp
Ultimate Mana Max = 현재 고정 100
Stack Ultimate Max = ManaBase
```

주의: 현재 `DEF = STR * 3`이다. CON이 방어/내구 스탯이라면 방어력 공식은 재검토가 필요하다.

## 스탯 변경 진입점

### 트레이닝

`TrainingManager.ApplyTraining(focus)`가 메인 유닛에 `Unit.AddStatUpgrade(focus, gain)`을 호출한다.

현재 강화량:

```text
BaseStatGain = 1
TrainingLevelGain = 1
육성 완료 기록이 없는 서포트: 모든 훈련 +1, 클래스 주스탯 일치 시 추가 +1
육성 완료 기록이 있는 서포트: 등장 판정 성공 시 supportCard.trainingBonus 적용
특기 훈련 일치 시 supportCard.specialtyBonus 추가
우정도 75 이상에서 특기 훈련에 등장하면 supportCard.friendshipBonus 추가
```

훈련 실패 판정은 없다. 준비 페이즈에서 훈련을 선택하면 반드시 메인 캐릭터의 선택 스탯과 트레이닝 레벨이 오른다.

육성 완료 서포트 카드가 사용하는 값:

```text
특기 훈련
특기율
특기 보너스
훈련 보너스
스킬 전수율
초기 우정도
우정도 상승율
우정 보너스
```

서포트 카드 등장/우정/전수 규칙:

```text
특기 훈련 일치: specialtyRate 확률로 등장
특기 훈련 불일치: 35% 확률로 등장
등장 시 우정도 += bondGainRate, 특기 훈련이면 추가 +1
특기 훈련에 등장하면 skillTransferRate 확률로 전수 가능 패시브 중 하나를 메인에게 전수
이미 메인이 배운 패시브는 전수 후보에서 제외
```

### 보상

`RewardManager.ApplyReward()`가 선택 보상에 따라 `Unit.AddRunBonus()`를 호출한다.

현재 레거시 보상 필드의 매핑:

```text
atkBonus -> StrUpgrade
hpBonus -> ConUpgrade
intBonus -> IntUpgrade
codeAccelerationBonus -> DexUpgrade 일부 + CodeAccelerationRunBonus
critChanceBonus / critMultiplierBonus -> LukUpgrade
defBonus -> 현재 StrUpgrade에도 들어감
```

주의: 보상 데이터 설명은 5스탯 기준으로 바뀌었지만 코드 필드는 아직 레거시 명칭이다. `strBonus/dexBonus/conBonus/intBonus/lukBonus`로 정리하는 것이 좋다.

### 장비

장비는 `EquipmentStatBonus.stat` 문자열을 `PrimaryStat`으로 파싱해서 5스탯에 더한다.

장비는 스킬도 부여할 수 있다.

```text
normal
passive
ultimate
```

장비 재장착/복원 시 아이템 패시브는 중복 등록되지 않도록 `EquipStartingItems()`에서 초기화한다.

### 상태 효과

현재 `StatusEffect`가 직접 반영하는 전투 모디파이어는 제한적이다.

```text
CritChanceAdditiveModifier
CritMultiplierAdditiveModifier
ReceivingDamageModifier
```

즉, 5스탯 자체를 임시로 올리는 버프 구조는 아직 없다. 향후 훈련/스킬/장비 버프를 확장하려면 `StatusEffect`에 5스탯 가산/곱연산 훅을 추가하는 방식이 가장 명확하다.

## 저장/복원

런 저장은 `UnitSaveData`에 다음 값을 보존한다.

```text
trainingLevel
str/dex/con/int/lukUpgrade
hp/atk/def/crit/codeAcceleration 레거시 보너스
equippedItemIds
currentHP
position
```

라운드 종료 후 필드 복원도 이제 `UnitSaveData` 기반으로 처리해야 한다. 좌표와 ID만 복원하면 보상/트레이닝/장비 성장이 사라진다.

## 육성 완료 기록 설계

육성 완료 캐릭터는 무한 모드의 직접 계승 캐릭터가 아니라 서포트 카드 원천이다.

현재 `TrainedCharacterCollection`은 기존 호환용 `unitIds`와 신규 `records`를 함께 가진다. `records`의 각 항목은 동일 `unitId` 기준으로 최신 기록으로 갱신된다.

저장되는 기록:

```text
unitId
unitName
finalTrainingLevel
finalPrimaryStats
titleIds
ownedPassiveCodeIds
supportCard
createdAt/version
```

`supportCard`에는 다음 값이 들어간다.

```text
supportId
sourceUnitId
sourceUnitName
specialtyTraining
specialtyRate
specialtyBonus
trainingBonus
skillTransferRate
initialBond
bondGainRate
friendshipBonus
sourcePower
```

현재 서포트 효과 생성 규칙:

```text
특기 훈련: 50% 클래스 주스탯, 30% 최종 최고 스탯, 20% 완전 랜덤
특기율: 35~60 + TrainingLevel / 8
특기 보너스: 1 + 특기 스탯 / 35 + 소량 랜덤
훈련 보너스: 0~2 + sourcePower / 250
스킬 전수율: 10 + 전수 가능 패시브 수 * 6 + LUK / 8 + 랜덤
초기 우정도: 20 + TrainingLevel / 3 + 랜덤
우정도 상승율: 1~3 + TrainingLevel / 50
우정 보너스: 1 + 특기 보너스 / 2 + 랜덤
```

현재 훈련 적용 규칙:

```text
편성된 서포트 유닛에 육성 완료 기록이 있으면 supportCard를 사용한다.
supportCard.trainingBonus는 해당 서포트가 훈련에 등장했을 때 적용된다.
supportCard.specialtyTraining과 현재 집중 훈련이 같고 등장했으면 supportCard.specialtyBonus를 추가한다.
우정도 75 이상인 특기 서포트가 등장하면 supportCard.friendshipBonus를 추가한다.
특기 서포트가 등장하면 supportCard.skillTransferRate로 패시브 전수를 시도한다.
육성 완료 기록이 없는 서포트는 기존 임시 규칙(서포트 1명당 +1, 클래스 주스탯 일치 시 +1)을 사용한다.
```

현재 런 저장에 보존되는 육성 진행 값:

```text
preparationActionUsed
supportBonds
```

`preparationActionUsed`는 같은 스테이지에서 저장/불러오기로 훈련이나 휴식을 반복하는 것을 막는다. `supportBonds`는 서포트 우정도를 현재 런 단위로 저장한다.

저장하지 않아도 되는 것:

```text
일반행동 ID
궁극기 ID
장비 목록
최종 장비 스탯
메인의 트레이닝 스탯 직접 계승값
```

현재 패시브 기록은 `Unit`이 해금한 패시브를 `LearnedPassiveSaveData`로 보관한다. 고유 패시브 원본은 전수되지 않으며, 전수 가능한 고유 P는 `TransferVersionCodeId`가 가리키는 열화 일반 패시브를 별도 기록한다. 매핑이 0이면 전수 불가다.

## 정리할 과제

- `RewardDef`의 레거시 스탯 필드를 5스탯 명칭으로 정리
- `DEF = STR * 3` 공식 재검토
- 추가 전투가 일반 전투와 다른 보너스 전투라면 별도 round/reward 타입 데이터 추가
- ~~고유 패시브 전수 규칙 구현~~ — `UniquePassiveCode.TransferVersionCodeId`와 열화 코드 230~233으로 해결
- 무한 모드는 육성 완료 5인 고정 편성으로 유지
- 육성 모드는 메인 1명만 있어도 시작 가능하고, 서포트는 최대 4명까지 선택 또는 진행 중 합류
