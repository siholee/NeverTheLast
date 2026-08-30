# 상세 기획서 05 — 경제·장비·보상

> **3계층 문서.** 재화 수급과 소비, 장비 시스템, 전투 보상 풀과 티어 확률.
> 개념 정의는 [서브 기획서](GDD_Sub_Concepts.md) 5장을 본다.

최종 갱신: 2026-08-30
관련 코드: `InventoryManager.cs`, `RewardManager.cs`, `Equipment.cs`, `ItemData.cs`
관련 데이터: `40_items.yaml`, `50_tokens.yaml`, `90_rewards.yaml`

---

## 1. 재화

| 재화 | 획득 | 소비처 | 저장 |
| --- | --- | --- | --- |
| **골드** | 적 처치 시 `25 × 현재 스테이지` | 🔴 **없음** | `RunSaveData.gold` |
| **원소 토큰** (8종) | **3킬마다** 무작위 토큰 5개 | 🔴 **없음** | `RunSaveData.tokens` |
| **리롤 티켓** | 보상 효과 `rerollTicketBonus` | 🔴 **없음** | `RunSaveData.rerollTicketCount` |

### 1.1 골드 수급 곡선

```
스테이지 1  : 킬당 25골드
스테이지 50 : 킬당 1,250골드
스테이지 100: 킬당 2,500골드
```

스테이지 하나에 적이 3~5기이므로, 100스테이지 근처에서는 한 전투로 1만 골드가 들어온다.

🔴 **소비처가 하나도 없다.** 개편 전에는 사건 선택지 `pay_gold`가 유일한 소비처였으나,
해당 사건(케찰코아틀 공물)이 테마 정리와 함께 제거되면서 소비처가 0이 되었다.
골드는 이제 벌기만 하고 쓸 수 없다.

### 1.2 토큰

`50_tokens.yaml` — 8종. 원소와 1:1 대응한다.

| ID | 이름 | 대응 원소(추정) |
| --- | --- | --- |
| 1 | 불 | Pyro |
| 2 | 물 | Hydro |
| 3 | 풀 | Dendro |
| 4 | 바람 | Anemo |
| 5 | 땅 | Geo |
| 6 | 얼음 | Cryo |
| 7 | 빛 | — |
| 8 | 어둠 | Void |

🔴 **`InventoryManager.SpendToken()`을 호출하는 곳이 없다.** 획득 로직만 존재한다.
🔸 Electro에 대응하는 토큰이 없고, "빛"에 대응하는 원소가 없다 — 매핑이 어긋나 있다.

## 2. 장비 시스템

### 2.1 슬롯

`EquipmentSlot` — `Head`, `Necklace`, `Armor`, `Ring`, `Shoes`, `MainHand`, `OffHand` (7종)

🔸 현재 아이템은 `Armor`, `MainHand`, `OffHand` 세 슬롯에만 존재한다.

### 2.2 희귀도 (`ItemRarity`)

| 값 | 이름 | 보상 티어 |
| --- | --- | --- |
| 1 | Common | T1 |
| 2 | Uncommon | T2 |
| 3 | Rare | T3 |
| 4 | Epic | T4 |
| 5 | Legendary | T5 |

**아이템의 `rarity`가 그대로 보상 티어로 쓰인다.**

### 2.3 숙련도 (`EquipmentProficiency`)

`None`, `Dagger`, `Wand`, `Orb`, `Greatsword`, `Longbow`, `Shortbow`, `Crossbow`, `LightArmor`, `MediumArmor`, `HeavyArmor`, `Shield`, `Longsword`, `Mace`, `Spear`

`Unit.HasEquipmentProficiency()`가 `10_units.yaml`의 `startingProficiencies`를 검사한다.
모든 영웅은 최소 무기 1종의 숙련으로 시작한다. 방어구 숙련은 캐릭터별 사양이며, 숙련이 없는 캐릭터는 의복을 사용한다. 설정상 필요한 복수 무기·방패는 시작 숙련에 함께 넣고, 성장으로 얻는 나머지는 해금/전수 패시브로 연다.
미숙련 장비는 장착할 수 있으나 중량만 적용되고 스탯·코드·장착 조건 효과가 없다. 의복은 숙련을 요구하지 않는다.

### 2.4 중량

| 항목 | 값 |
| --- | --- |
| 1차 | `STR` — 초과 시 DEX −50% |
| 2차 | `ceil(STR × 1.5)` — 초과 시 올스탯 −50%로 대체 |
| 3차 | `STR × 2` — 초과 시 장비 정리 외 진행 행동 불가 |
| 현재량 | 착용 장비 + 유닛 개인 인벤토리 장비의 `weight` 합계 |

STR이 낮은 캐릭터는 무거운 장비를 감당하지 못한다 — STR의 유일한 실질 기능이다.

### 2.5 양손 무기

`twoHanded: true` → MainHand 착용 시 OffHand를 함께 점유한다.

### 2.6 아이템 효과 축

| 효과 | 필드 | 동작 |
| --- | --- | --- |
| **내구** | `durability` | 받는 피해에서 **고정 감소**. 방어구 전용. T1 기준 의복 2 / 경갑 5 / 방패 6 / 중갑 10 / 판금 16 |
| 5스탯 보너스 | `statBonuses: [{stat, amount}]` | `GetEquipmentStatBonus()`로 합산 |
| 코드 부여 | `codeGrants: [{slot, codeId, stage}]` | `ItemPassiveCodes`에 추가 또는 일반/궁극기 교체 |
| 사건 전용 | `eventOnly: true` | 전투 보상 풀에서 제외 |

**코드 부여가 장비를 단순 수치 이상으로 만든다.** 무기를 바꾸면 일반공격 자체가 바뀐다.

## 3. 현재 아이템 목록 (`40_items.yaml`)

T1 무기 10종·방어구 4종과 테마 전용 세트를 운용한다. ID·스탯·중량의 단일 관리 목록은 [Detail_14 — 장비 목록](Detail_14_Equipment_Catalog.md)이다.

### 3.1 티어 분포

| 티어 | 아이템 수 | 보상 풀 등장 |
| --- | --- | --- |
| T1 | 14 | O |
| **T2** | **0** | 🔴 |
| **T3** | **0** | 🔴 |
| **T4** | **0** | 🔴 |
| T5 | 1 | ✕ (사건 전용) |

🔴 **보상 풀에 실제로 존재하는 것은 T1 14종뿐이다.**
아래 §4의 티어 확률표는 라운드 2부터 사실상 공회전한다.

## 4. 전투 보상

### 4.1 흐름

```
전투 종료 → RewardManager.GenerateRewards(count: 3, rewardRound, mode, themeId)
          → 보상 3개 제시 → 1개 선택 → ApplyReward()
```

### 4.2 보상 풀 생성 규칙

1. `40_items.yaml`의 아이템 중 `eventOnly`가 아닌 것을 모두 후보로 삼는다
2. `themeIds`가 비어 있지 않으면 **현재 테마가 그 목록에 있을 때만** 후보에 남는다
   (`RewardManager.IsAvailableInTheme`)
3. 아이템의 `rarity`를 보상 **티어**로 사용한다
4. 라운드별 티어 가중치로 티어를 먼저 뽑고, 그 티어의 아이템 중에서 선택한다
5. 그 티어에 후보가 없으면 가장 가까운 티어로 폴백한다

### 4.3 티어 확률 (`90_rewards.yaml`)

**수치표는 [Detail_13 §3](Detail_13_Reward_Catalog.md)에 한 벌만 둔다.** 여기서는 의도만 적는다.

**설계 의도** — 라운드 8부터 T1이 완전히 사라져 저등급 보상이 나오지 않는다.
후반 보상 밀도가 파워 스파이크를 만든다.

> 🔴 다만 T3 이상이 거의 비어 있어(현재 T3 3종 · T4 0종 · T5는 사문) **후반 확률표가 상당 부분 공회전한다.**
> 빈 티어는 가장 가까운 티어로 폴백하므로 실질적으로 T1·T2가 계속 나온다.

**모드별 적용**

| 모드 | 적용 |
| --- | --- |
| 육성 | 현재 라운드에 해당하는 행 |
| 무한 | 10라운드 이후에도 **10라운드 행 고정** |

### 4.4 보상 효과 축 (`RewardDef`)

`RewardManager.cs`에 정의된 런타임 DTO. 현재는 아이템 보상 위주로 쓰이지만, 사건 보상 등으로 확장 가능하다.

| 필드 | 효과 |
| --- | --- |
| `itemId` | 아이템 지급 |
| `healAmount` | 전체 아군 고정 회복 |
| `fullHealParty` | 전체 아군 완전 회복 |
| `tier` | 보상 티어. 장비 보상은 `ItemData.rarity`가 들어온다 |
| `themeIds` | 테마 제한. 장비 보상은 `ItemData.themeIds`를 그대로 물려받는다 |
| `item` | 장비 보상의 원본 `ItemData`. 보상 화면이 부위·중량·스탯을 구조로 보여 주려고 들고 다닌다 |
| `atkBonus` | 선택 유닛 **STR** 강화 |
| `defBonus` | 선택 유닛 **CON** 강화 |
| `hpBonus` | 선택 유닛 **CON** 강화 |
| `intBonus` | 선택 유닛 **INT** 강화 |
| `critChanceBonus` | 선택 유닛 **LUK** 강화 |
| `codeAccelerationBonus` | **DEX** 강화 계열 |
| `rerollTicketBonus` | 리롤 티켓 증가 |
| `randomTokenAmount` | 무작위 토큰 지급 |

> 필드명이 옛 스탯 체계(atk/def/hp/crit)에 남아 있고 실제 효과는 5스탯 강화다.
> 🔸 혼동을 부르는 네이밍 — 리팩터링 대상.

### 4.5 테마 고유 보상

테마 전용 보상은 **테마 쪽이 아니라 아이템 쪽에 선언한다.**

```yaml
# 40_items.yaml
- id: 4201
  name: 레기온 한손검
  themeIds: [5]      # 스테이지 테마 id. 적 themeId가 아니다
```

`RewardManager.IsAvailableInTheme`가 이 목록을 보고 거른다. 목록이 비어 있으면 모든 테마에 나온다.

> 🔸 옛 `stageThemes[].uniqueRewardIds`는 **스키마에서 사라졌다.**
> `StageThemeData`에 그런 필드가 없으므로 yaml에 적어도 무시된다.
> 테마 전용 보상은 전부 위의 `ItemData.themeIds`로만 관리한다 —
> 현재 목록은 [Detail_13 §4](Detail_13_Reward_Catalog.md).

## 5. 인벤토리

`InventoryManager`

| 항목 | 필드 | 비고 |
| --- | --- | --- |
| 골드 | `Gold` (읽기 전용 프로퍼티) | `AddGold` / `TrySpendGold` / `RestoreGold` |
| 토큰 | `TokensInHand` (ID → 수량) | `AddToken` / `SpendToken` |
| 리롤 티켓 | `rerollTicketCount` | |
| 개인 휴대품 | `Unit.carriedItemIds` | 미착용 장비. 유닛의 현재 중량에 포함 |
| 호환용 공유 보관함 | `ItemIdsInHand` | 영웅 생성 전 지급·구버전 세이브용. 장착 시 선택 유닛에게 이전 |

**장비 착용** — `TryEquipStoredItem(unit, itemId, out reason)`
실패 사유를 문자열로 반환한다 (중량 초과 등).

## 6. 경제 밸런스 튜닝 노브

| 노브 | 위치 | 현재값 | 영향 |
| --- | --- | --- | --- |
| 킬당 골드 | `GameManager.OnKillEnemy` | `25 × Stage` | 후반 인플레의 주원인 |
| 토큰 지급 주기 | `GameManager.OnKillEnemy` | 3킬마다 5개 | |
| 보상 선택지 수 | `GenerateRewards(count)` | 3 | 의사결정 폭 |
| 티어 확률 | `90_rewards.yaml` | 위 §4.3 | 성장 실감 곡선 |
| 중량 한도 | `Unit.CarryWeightFirstCap/SecondCap/Max` | `STR / 1.5×STR / 2×STR` | 개인 인벤토리 포함 휴대 한도 |

## 7. 우선 해결 과제

| 순위 | 과제 | 이유 |
| --- | --- | --- |
| 1 | **T2~T4 아이템 추가** | 티어 확률표가 라운드 2부터 공회전한다 |
| 2 | **골드 소비처 도입** | 소비처가 0이다 (준비 페이즈 상점 등) |
| 3 | 토큰 소비처 설계 | 원소 시스템과 연결하는 것이 자연스럽다 |
| 4 | 리롤 티켓 실동작화 | 보상 재추첨 UI 필요 |
| 5 | 테마 고유 보상 설계 | `uniqueRewardIds`를 쓰는 테마가 하나도 없다 |
| 6 | `RewardDef` 필드명 정리 | `atkBonus`가 STR을 올리는 등 이름과 효과가 어긋난다 |
