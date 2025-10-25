# 방어막 시스템 업데이트 요약

## 변경 사항 개요
방어막 시스템을 독립적인 바(Background, Fill, Mask)로 변경하고, **ShieldMax(최대 방어막)** 개념을 도입하여 **총 방어막 대비 현재 방어막**을 표시하도록 수정했습니다.

---

## 1. Unit.cs 변경사항

### 새로운 변수 추가
```csharp
[SerializeField] private int shieldMax;   // 최대 방어막 (획득한 총 방어막)
[SerializeField] private int shieldCurr;  // 현재 방어막 (라운드 끝까지 유지)

public int ShieldMax { get => shieldMax; protected set => shieldMax = value; }
public int ShieldCurr { get => shieldCurr; protected set => shieldCurr = value; }
```

### 방어막 관리 로직 변경

#### AddShield() - 방어막 추가
```csharp
// ShieldMax와 ShieldCurr 둘 다 증가
ShieldMax += amount;
ShieldCurr += amount;
```
- 방어막을 획득하면 **Max와 Curr 둘 다 증가**
- 예: AddShield(100) → Max: 0→100, Curr: 0→100

#### RemoveShield() - 방어막 감소 (피해 받을 때)
```csharp
ShieldCurr = Mathf.Max(0, ShieldCurr - amount);

// ShieldCurr이 0이 되면 ShieldMax도 초기화
if (ShieldCurr == 0)
{
    ShieldMax = 0;
}
```
- 피해를 받으면 **ShieldCurr만 감소**
- ShieldCurr이 0이 되면 **ShieldMax도 0으로 초기화**
- 예: RemoveShield(30) → Curr: 100→70 (Max는 100 유지)
- 예: RemoveShield(70) → Curr: 70→0, Max: 100→0 (완전 소진)

#### SetShield() - 방어막 설정
```csharp
ShieldMax = amount;
ShieldCurr = amount;
```
- Max와 Curr을 동시에 설정

#### DefaultRoundEndEvent() - 라운드 종료 시
```csharp
ShieldMax = 0;   // 최대치 초기화
ShieldCurr = 0;  // 현재치 초기화
```

---

## 2. Cell.cs 변경사항

### 방어막 바 컴포넌트 변경
```csharp
[Header("Shield Bar Components - Sprite Mask")]
public SpriteRenderer shieldBarBackground;  // 방어막 바 배경 (새로 추가!)
public SpriteRenderer shieldBarFill;        // 방어막 바 채움
public SpriteMask shieldBarMask;            // 방어막 바 마스크
```
- **HP/MP 바와 동일하게 BG, Fill, Mask 3개 컴포넌트 구성**

### UpdateUI() 로직 변경
```csharp
// 방어막 바 업데이트 (ShieldMax 대비 ShieldCurr)
if (occupiedUnit.ShieldMax > 0)
{
    float shieldRatio = (float)occupiedUnit.ShieldCurr / occupiedUnit.ShieldMax;
    UpdateShieldBar(shieldRatio);
    ActivateShieldBar();
}
else
{
    DeactivateShieldBar();
}
```
- **ShieldMax 대비 ShieldCurr 비율**로 방어막 바 표시
- 기존: `ShieldCurr / HpMax` (최대 체력 대비)
- 변경: `ShieldCurr / ShieldMax` (최대 방어막 대비)

### UpdateShieldBar() - 독립적인 방어막 바 업데이트
```csharp
public void UpdateShieldBar(float shieldRatio)
{
    // Background 기준으로 Mask 스케일 조정
    Vector3 bgScale = shieldBarBackground.transform.localScale;
    Vector3 maskScale = new Vector3(bgScale.x * shieldRatio, bgScale.y, bgScale.z);
    shieldBarMask.transform.localScale = maskScale;
    
    // 왼쪽 정렬 처리
    if (shieldRatio < 1.0f)
    {
        float offsetX = (bgScale.x - maskScale.x) * 0.5f;
        Vector3 maskPos = shieldBarMask.transform.localPosition;
        maskPos.x = -offsetX;
        shieldBarMask.transform.localPosition = maskPos;
    }
    
    // 방어막 색상 (파란색 계열)
    shieldBarFill.color = Color.cyan;
}
```
- HP/MP 바와 동일한 SpriteMask 방식 사용
- **HP 바와 독립적으로 작동**

---

## 3. 사용 예시

### 시나리오 1: 찬드라의 소마 패시브
```csharp
// 방어막 100 부여
unit.AddShield(100);
// 결과: ShieldMax=100, ShieldCurr=100, UI는 100% 표시
```

### 시나리오 2: 피해 받기
```csharp
// 초기 상태: ShieldMax=100, ShieldCurr=100
unit.RemoveShield(30);
// 결과: ShieldMax=100, ShieldCurr=70, UI는 70% 표시

unit.RemoveShield(70);
// 결과: ShieldMax=0, ShieldCurr=0, UI는 비활성화
```

### 시나리오 3: 여러 소스에서 방어막 획득
```csharp
// 첫 번째 스킬에서 방어막 50 획득
unit.AddShield(50);
// 결과: ShieldMax=50, ShieldCurr=50

// 두 번째 스킬에서 방어막 30 획득
unit.AddShield(30);
// 결과: ShieldMax=80, ShieldCurr=80, UI는 100% 표시

// 피해 20 받음
unit.RemoveShield(20);
// 결과: ShieldMax=80, ShieldCurr=60, UI는 75% 표시 (60/80)
```

---

## 4. 기존 시스템과의 차이점

### 기존 방식 (HP 바 기반)
- 방어막이 HP 바 옆에 붙어서 표시됨
- 방어막 비율 = `ShieldCurr / HpMax`
- HP + 방어막이 HpMax를 초과하면 노란색으로 오버플로우 표시
- 방어막 바가 Fill + Mask만 있음 (Background 없음)

### 새로운 방식 (독립 바)
- 방어막이 별도의 독립적인 바로 표시됨
- 방어막 비율 = `ShieldCurr / ShieldMax`
- HP와 무관하게 방어막 자체의 충전 상태를 표시
- 방어막 바가 Background + Fill + Mask 완전한 구조
- 방어막이 깎이면 바가 줄어듦 (직관적)
- 방어막이 모두 소진되면 바가 완전히 사라짐

---

## 5. Unity 에디터 설정 필요사항

각 Cell Prefab에 **방어막 바 Background 컴포넌트를 추가**해야 합니다:

1. Cell Prefab 열기
2. ShieldBar 하위에 새로운 GameObject 생성: `ShieldBarBackground`
3. SpriteRenderer 컴포넌트 추가
4. Sprite 설정 (HP/MP Background와 동일한 스프라이트)
5. Color 설정 (방어막 배경색, 예: 어두운 회색)
6. Sorting Order 설정 (mpBarBackground보다 높게)
7. Cell 컴포넌트의 `Shield Bar Background` 필드에 할당

---

## 6. 장점

1. **직관성**: 방어막의 충전 상태를 명확하게 표시
2. **독립성**: HP 바와 독립적으로 작동하여 혼란 감소
3. **일관성**: HP/MP 바와 동일한 구조 (BG+Fill+Mask)
4. **확장성**: 추가적인 방어막 시각 효과 적용 용이
5. **명확성**: 방어막이 깎이는 과정을 시각적으로 명확히 표현

---

## 7. 테스트 권장사항

1. 찬드라 패시브로 방어막 부여 → UI 100% 표시 확인
2. 적에게 피해를 받으면서 방어막 감소 → UI 비율 감소 확인
3. 방어막 완전 소진 → UI 비활성화 확인
4. 라운드 종료 → 방어막 초기화 확인
5. 여러 소스에서 방어막 중첩 → Max/Curr 정상 증가 확인
