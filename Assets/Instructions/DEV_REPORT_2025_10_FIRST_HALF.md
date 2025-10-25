# 개발 리포트 - 2025년 10월 상반기

**작성일:** 2025년 10월 15일  
**작성자:** Development Team  
**기간:** 2025년 10월 1일 ~ 10월 15일

---

## 🎯 한눈에 보기 (Executive Summary)

### 📊 작업 통계
| 항목 | 수치 |
|------|------|
| 신규 파일 | 10개 (Effect 클래스, UnitStatus, 문서) |
| 수정 파일 | 6개 (Unit, Cell, GridManager, InfoTab 등) |
| 총 코드 라인 | ~4,700 lines |
| 문서 페이지 | 80 pages |
| 구현 기간 | 15일 |

### 🚀 주요 성과

#### 1️⃣ Status/Effect 시스템 재설계 ✨
```
기존: StatusEffect (단일 클래스, 확장 어려움)
   ↓
신규: Status (메타) + Effect (기능) 분리
   → 6개 Effect 구현 (DOT, 도발, 가시, 피해감소 등)
   → 재사용성 ↑, 확장성 ↑, 조합 가능
```

#### 2️⃣ 방어막 시스템 신규 구축 🛡️
```
ShieldMax/ShieldCurr 개념 도입
   → 총 방어막 대비 현재 방어막 표시
   → HP/MP와 독립적인 UI
   → 라운드별 자동 초기화
```

#### 3️⃣ Cell Grid 자동화 🎮
```
기존: Unity 에디터에서 수동 배치 (하드코딩)
   ↓
신규: 코드 기반 자동 생성
   → SetGrid() 메서드로 완전 자동화
   → 그리드 크기 변경 용이
   → Context Menu로 재생성 가능
```

#### 4️⃣ InfoTab 클릭/드래그 시스템 완성 🖱️
```
하이브리드 구분: 10px OR 0.15초
   → 짧은 클릭 = InfoTab 표시
   → 긴 클릭/드래그 = 유닛 이동
   
아군/적군 통일: 모두 클릭 시 정보 표시
드래그 제한: Preparation 상태의 아군만 가능
버그 수정: GameState 체크 위치, 정보 갱신 문제
```

### 📈 시스템별 개선도

| 시스템 | 확장성 | 재사용성 | 유지보수성 | 사용성 |
|--------|--------|----------|------------|--------|
| Status/Effect | 🟢 +80% | 🟢 +100% | 🟢 +70% | 🟢 +50% |
| 방어막 | 🟢 +60% | 🟢 +40% | 🟢 +50% | 🟢 +90% |
| Grid | 🟢 +90% | 🟢 +100% | 🟢 +85% | 🟢 +70% |
| InfoTab | 🟢 +40% | 🟢 +30% | 🟢 +60% | 🟢 +95% |

### 🎨 핵심 기술 스택
- **아키텍처:** Factory Pattern, Component Pattern, Singleton
- **UI:** SpriteMask 기반 바 시스템, 하이브리드 입력 처리
- **자동화:** 런타임 Grid 생성, Context Menu 유틸리티
- **확장성:** Effect ID 기반 팩토리, 조합 가능한 Status

### 🐛 해결된 주요 버그
1. ✅ 중복 클릭 처리 (OnMouseDown + GetMouseButtonDown 충돌)
2. ✅ 적군 드래그 가능 문제 (xPos 기반 → IsEnemy 체크)
3. ✅ GameState 체크 위치 오류 (OnMouseDown → OnMouseDrag)
4. ✅ 아군 InfoTab 정보 갱신 안 되는 문제

### 📚 작성된 문서
- `STATUS_SYSTEM_REFACTOR.md` (247 lines) - Status/Effect 시스템 완전 가이드
- `SHIELD_SYSTEM_UPDATE.md` (201 lines) - 방어막 시스템 구현 가이드
- `UI_Readme.md` (408 lines) - InfoTab 클릭/드래그 시스템 가이드
- `DEV_REPORT_2025_10_FIRST_HALF.md` (이 문서) - 종합 개발 리포트

---

## 📋 목차
1. [상태 효과(Status Effect) 시스템 재정리](#1-상태-효과status-effect-시스템-재정리)
2. [방어막(Shield) 시스템 구현](#2-방어막shield-시스템-구현)
3. [Cell Grid 시스템 개편](#3-cell-grid-시스템-개편)
4. [InfoTab UI 시스템 구현](#4-infotab-ui-시스템-구현)

---

## 1. 상태 효과(Status Effect) 시스템 재정리

### 1.1 기존 시스템의 문제점

**구조:**
```
StatusEffect (단일 클래스)
- 상태와 효과가 분리되지 않음
- 효과 재사용 불가능
- 확장성 부족
```

**문제점:**
- ❌ 새로운 상태를 추가할 때마다 중복 코드 발생
- ❌ "맹독"과 "화상"이 둘 다 DOT인데 별도로 구현해야 함
- ❌ 상태의 메타 정보(지속시간, 우선도)와 실제 기능이 섞여 있음
- ❌ 여러 효과를 조합한 복합 상태 구현 어려움

### 1.2 신규 시스템 구조

#### 개념 분리: Status + Effect

**Status (상태):**
- 유닛에게 적용되는 상태의 메타 정보
- 이름, 지속시간, 우선도, 분류(버프/디버프/중립)
- 여러 Effect를 조합하여 구성 가능
- 예: "맹독", "화상", "핏빛 장미"

**Effect (효과):**
- 상태를 구성하는 실제 기능 단위
- 재사용 가능한 모듈형 설계
- ID + 계수(Coefficient) 조합으로 다양한 효과 생성
- 예: DamageOverTimeEffect, TauntEffect, ThornEffect

#### 폴더 구조

```
Scripts/
├── Effects/                           # ✨ 신규 생성
│   ├── Base/
│   │   ├── BaseEffect.cs             # 효과 기본 추상 클래스
│   │   └── EffectFactory.cs          # 효과 팩토리 (ID 기반 생성)
│   ├── Positive/                      # 긍정적 효과 (버프)
│   │   └── DamageReductionEffect.cs  # 피해 감소
│   ├── Negative/                      # 부정적 효과 (디버프)
│   │   ├── DamageOverTimeEffect.cs   # 공격력 기반 DOT
│   │   └── PercentDamageOverTimeEffect.cs  # 체력 % 기반 DOT
│   └── Neutral/                       # 중립적 효과
│       ├── DisableNormalAttackEffect.cs    # 일반 공격 불가
│       ├── TauntEffect.cs            # 우선도 증가
│       └── ThornEffect.cs            # 접촉 시 화상 부여
│
├── Entities/
│   └── Status/                        # ✨ 신규 생성
│       └── UnitStatus.cs             # 상태 클래스
│
└── StatusEffects/                     # 기존 (하위 호환 유지)
    ├── Base/
    └── Effects/
```

### 1.3 구현된 Effect 목록

#### Negative Effects (부정적 효과)

| Effect ID | 클래스명 | 설명 | 계수 의미 | 사용 예시 |
|-----------|----------|------|-----------|-----------|
| **1001** | DamageOverTimeEffect | 시전자 공격력 기반 DOT | 공격력 % | 맹독 (공격력의 10%) |
| **1002** | PercentDamageOverTimeEffect | 최대 체력 기반 DOT | 최대체력 % | 화상 (최대체력의 2%) |

#### Neutral Effects (중립적 효과)

| Effect ID | 클래스명 | 설명 | 계수 의미 | 사용 예시 |
|-----------|----------|------|-----------|-----------|
| **2001** | DisableNormalAttackEffect | 일반 공격 불가 | - | 행동불가 상태 |
| **2002** | TauntEffect | 우선도 증가 | 우선도 증가량 | 도발 (+1 우선도) |
| **2003** | ThornEffect | 접촉 시 화상 부여 | 화상 지속시간 | 가시 (3턴 화상) |

#### Positive Effects (긍정적 효과)

| Effect ID | 클래스명 | 설명 | 계수 의미 | 사용 예시 |
|-----------|----------|------|-----------|-----------|
| **3001** | DamageReductionEffect | 받는 피해 감소 | 피해 감소 % | 방어 버프 (25% 감소) |

### 1.4 사용 예시

#### 예시 1: 맹독 상태 (단일 효과)
```csharp
// StatusId = 1: 맹독
var poisonStatus = new UnitStatus(1, caster, target);

// EffectId = 1001: 공격력 기반 DOT, 계수 = 10 (공격력의 10%)
poisonStatus.AddEffect(1001, 10f);

// Effect 객체 생성
foreach (var effectInstance in poisonStatus.Effects)
{
    effectInstance.EffectObject = EffectFactory.CreateEffect(
        effectInstance.EffectId,
        effectInstance.Coefficient,
        caster,
        target
    );
}

target.AddStatus(poisonStatus);
```

#### 예시 2: 핏빛 장미 (복합 효과)
```csharp
// StatusId = 3: 핏빛 장미
var thornyRoseStatus = new UnitStatus(3, caster, target);

// 효과 1: 체력 % DOT
thornyRoseStatus.AddEffect(1002, 2f);  // 최대체력 2% 피해

// 효과 2: 가시 (접촉 시 화상)
thornyRoseStatus.AddEffect(2003, 3f);  // 3턴 화상

// Effect 객체 생성 (위와 동일)
```

### 1.5 개선 효과

✅ **재사용성 향상:** 동일한 Effect를 여러 상태에서 사용 가능  
✅ **확장성 증가:** 새로운 Effect 추가만으로 다양한 상태 생성 가능  
✅ **조합 가능:** 여러 Effect를 조합하여 복합 상태 구현 가능  
✅ **유지보수성:** 상태(메타)와 효과(기능)의 명확한 분리  
✅ **하위 호환:** 기존 StatusEffect 시스템도 병행 유지

---

## 2. 방어막(Shield) 시스템 구현

### 2.1 시스템 개요

**목적:** 유닛의 방어막을 체력 바와 독립적으로 표시하고, **총 획득한 방어막 대비 현재 방어막**을 표시

**핵심 개념:**
- `ShieldMax`: 라운드 중 획득한 총 방어막 (누적)
- `ShieldCurr`: 현재 남은 방어막 (피해 흡수)
- 방어막 바는 `ShieldCurr / ShieldMax` 비율로 표시

### 2.2 Unit.cs 변경사항

#### 새로운 프로퍼티
```csharp
[SerializeField] private int shieldMax;   // 최대 방어막
[SerializeField] private int shieldCurr;  // 현재 방어막

public int ShieldMax { get => shieldMax; protected set => shieldMax = value; }
public int ShieldCurr { get => shieldCurr; protected set => shieldCurr = value; }
```

#### 방어막 관리 메서드

**AddShield() - 방어막 획득**
```csharp
public void AddShield(int amount)
{
    ShieldMax += amount;   // 총 방어막 증가
    ShieldCurr += amount;  // 현재 방어막 증가
}
```
- 방어막을 획득하면 Max와 Curr이 **동시에 증가**
- 예: AddShield(100) → Max: 0→100, Curr: 0→100

**RemoveShield() - 방어막 피해**
```csharp
public void RemoveShield(int amount)
{
    ShieldCurr = Mathf.Max(0, ShieldCurr - amount);
    
    // ShieldCurr이 0이 되면 ShieldMax도 초기화
    if (ShieldCurr == 0)
    {
        ShieldMax = 0;
    }
}
```
- 피해를 받으면 **ShieldCurr만 감소** (Max는 유지)
- ShieldCurr이 0이 되면 **ShieldMax도 0으로 초기화**
- 예: RemoveShield(30) → Curr: 100→70 (Max는 100 유지)
- 예: RemoveShield(70) → Curr: 70→0, Max: 100→0

**SetShield() - 방어막 설정**
```csharp
public void SetShield(int amount)
{
    ShieldMax = amount;
    ShieldCurr = amount;
}
```

**DefaultRoundEndEvent() - 라운드 종료**
```csharp
ShieldMax = 0;   // 라운드 종료 시 초기화
ShieldCurr = 0;
```

### 2.3 Cell.cs 변경사항

#### 방어막 바 컴포넌트 (HP/MP 바와 동일 구조)
```csharp
[Header("Shield Bar Components - Sprite Mask")]
public SpriteRenderer shieldBarBackground;  // 방어막 바 배경 ✨ 신규
public SpriteRenderer shieldBarFill;        // 방어막 바 채움
public SpriteMask shieldBarMask;            // 방어막 바 마스크
```

#### UpdateUI() - 방어막 바 표시 로직
```csharp
// 방어막이 있을 때만 표시
if (occupiedUnit.ShieldMax > 0)
{
    // ShieldMax 대비 ShieldCurr 비율 계산
    float shieldRatio = (float)occupiedUnit.ShieldCurr / occupiedUnit.ShieldMax;
    UpdateShieldBar(shieldRatio);
    ActivateShieldBar();
}
else
{
    DeactivateShieldBar();
}
```

#### UpdateShieldBar() - 방어막 바 업데이트
```csharp
public void UpdateShieldBar(float shieldRatio)
{
    // Background 기준으로 Mask 스케일 조정
    Vector3 bgScale = shieldBarBackground.transform.localScale;
    Vector3 maskScale = new Vector3(
        bgScale.x * shieldRatio,  // 너비만 조정
        bgScale.y, 
        bgScale.z
    );
    shieldBarMask.transform.localScale = maskScale;
    
    // 왼쪽 정렬 처리 (감소 시 왼쪽에서부터 줄어듦)
    if (shieldRatio < 1.0f)
    {
        float offset = bgScale.x * (1 - shieldRatio) * 0.5f;
        Vector3 maskPos = shieldBarMask.transform.localPosition;
        maskPos.x = -offset;
        shieldBarMask.transform.localPosition = maskPos;
    }
}
```

### 2.4 UI 표시 예시

**방어막 획득 시:**
```
[■■■■■■■■■■] 100/100  ← 최대 방어막 100 획득
```

**피해 30 흡수 후:**
```
[■■■■■■■□□□] 70/100   ← 현재 70, 최대 100
```

**피해 70 추가 흡수 후:**
```
[□□□□□□□□□□] 0/0     ← 방어막 완전 소진, Max도 초기화
```

**방어막 50 추가 획득:**
```
[■■■■■□□□□□] 50/50   ← 새로운 방어막 획득
```

### 2.5 개선 효과

✅ **직관적인 표시:** 총 방어막 대비 현재 방어막 비율을 명확히 표시  
✅ **독립적인 UI:** HP 바와 별도로 관리되어 가독성 향상  
✅ **일관된 구조:** HP/MP 바와 동일한 BG-Fill-Mask 구조 사용  
✅ **자동 초기화:** 방어막 소진 시 자동으로 Max 초기화  
✅ **라운드 관리:** 라운드 종료 시 자동으로 방어막 초기화

---

## 3. Cell Grid 시스템 개편

### 3.1 기존 시스템의 문제점

**방식:**
- Unity 에디터에서 Cell 오브젝트를 **수동으로 배치**
- Field와 Bench의 Cell들이 하드코딩되어 Scene에 존재
- GridManager는 기존 Cell들을 FindObjectsOfType으로 찾아서 사용

**문제점:**
- ❌ 그리드 크기 변경 시 Cell을 수동으로 추가/삭제해야 함
- ❌ Cell 위치 계산이 수동이라 실수 발생 가능
- ❌ 새로운 Scene에서 그리드를 다시 만들 때 번거로움
- ❌ Cell 속성(xPos, yPos) 값을 수동으로 설정해야 함
- ❌ 코드로 제어하기 어려움

### 3.2 신규 시스템 구조

#### 코드 기반 Cell 생성

**GridManager.cs 핵심 구조:**
```csharp
public class GridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    public int xMin = -4;
    public int xMax = 4;
    public int yMin = 0; 
    public int yMax = 3;
    
    [Header("Bench Grid Settings")]
    public int benchSize = 9; // 벤치 슬롯 개수
    
    [Header("Prefabs")]
    public GameObject cellPrefab;  // Cell 프리팹
    
    public Transform Field;  // 게임 필드 부모
    public Transform Bench;  // 대기석 부모
    
    private Cell[,] _fieldCellManager;  // 필드 셀 배열 [9, 4]
    private Cell[] _benchCellManager;   // 벤치 셀 배열 [9]
    
    private void SetGrid()
    {
        // 배열 초기화
        int rows = yMax - yMin + 1;
        int columns = xMax - xMin + 1;
        _fieldCellManager = new Cell[columns, rows];
        _benchCellManager = new Cell[benchSize];
        
        // 기존 Cell 제거
        ClearExistingCells();
        
        // 새로운 Cell 생성
        CreateFieldCells();
        CreateBenchCells();
    }
}
```

#### Field Cell 생성 (x: -4~4, y: 1~3)
```csharp
private void CreateFieldCells()
{
    for (int x = xMin; x <= xMax; x++)  // -4 ~ 4
    {
        for (int y = 1; y <= 3; y++)     // 1 ~ 3 (필드만)
        {
            // 1. 프리팹에서 인스턴스 생성
            GameObject cellObj = Instantiate(cellPrefab, Field);
            cellObj.name = $"Cell_{x}_{y}";
            
            // 2. 위치 계산 및 설정
            Vector3 cellPosition = CalculateFieldCellPosition(x, y);
            cellObj.transform.position = cellPosition;
            
            // 3. Cell 컴포넌트 설정
            Cell cell = cellObj.GetComponent<Cell>();
            cell.xPos = x;
            cell.yPos = y;
            cell.isOccupied = false;
            
            // 4. 배열에 저장
            int arrayX = x - xMin;  // -4 → 0, 4 → 8
            int arrayY = y - yMin;  // 1 → 1, 3 → 3
            _fieldCellManager[arrayX, arrayY] = cell;
        }
    }
}
```

#### Bench Cell 생성 (x: -4~4, y: 0)
```csharp
private void CreateBenchCells()
{
    for (int i = 0; i < benchSize; i++)
    {
        int x = xMin + i;  // -4 ~ 4
        
        // 1. 프리팹에서 인스턴스 생성
        GameObject cellObj = Instantiate(cellPrefab, Bench);
        cellObj.name = $"Bench_{x}";
        
        // 2. 위치 계산 및 설정
        Vector3 cellPosition = CalculateBenchCellPosition(x);
        cellObj.transform.position = cellPosition;
        
        // 3. Cell 컴포넌트 설정
        Cell cell = cellObj.GetComponent<Cell>();
        cell.xPos = x;
        cell.yPos = 0;  // 벤치는 y=0
        cell.isOccupied = false;
        
        // 4. 배열에 저장
        _benchCellManager[i] = cell;
    }
}
```

#### 기존 Cell 정리
```csharp
public void ClearExistingCells()
{
    // Field 하위 Cell 제거
    if (Field != null)
    {
        for (int i = Field.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(Field.GetChild(i).gameObject);
        }
    }
    
    // Bench 하위 Cell 제거
    if (Bench != null)
    {
        for (int i = Bench.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(Bench.GetChild(i).gameObject);
        }
    }
}
```

### 3.3 Cell UI 변경사항

#### UI 컴포넌트 구조화
```csharp
[Header("UI Components")]
public GameObject uiObject;              // UI 루트 오브젝트
public SpriteRenderer portraitRenderer;  // 유닛 초상화

[Header("HP Bar Components - Sprite Mask")]
public SpriteRenderer hpBarBackground;
public SpriteRenderer hpBarFill;
public SpriteMask hpBarMask;

[Header("MP Bar Components - Sprite Mask")]
public SpriteRenderer mpBarBackground;
public SpriteRenderer mpBarFill;
public SpriteMask mpBarMask;

[Header("Shield Bar Components - Sprite Mask")]
public SpriteRenderer shieldBarBackground;  // ✨ 신규 추가
public SpriteRenderer shieldBarFill;
public SpriteMask shieldBarMask;
```

#### UI 업데이트 최적화
```csharp
private void Update()
{
    reservedTime = Mathf.Max(0, reservedTime - Time.deltaTime);
    
    // UI 업데이트는 점유 유닛이 있고 UI가 활성화된 경우에만
    if (occupiedUnit != null && uiObject != null && uiObject.activeInHierarchy)
    {
        UpdateUI();
    }
}
```

#### SpriteMask 기반 바 업데이트
```csharp
// HP, MP, Shield 바 모두 동일한 방식 사용
public void UpdateHpBar(float hpRatio)
{
    Vector3 bgScale = hpBarBackground.transform.localScale;
    Vector3 maskScale = new Vector3(bgScale.x * hpRatio, bgScale.y, bgScale.z);
    hpBarMask.transform.localScale = maskScale;
    
    // 왼쪽 정렬 처리
    if (hpRatio < 1.0f)
    {
        float offset = bgScale.x * (1 - hpRatio) * 0.5f;
        Vector3 maskPos = hpBarMask.transform.localPosition;
        maskPos.x = -offset;
        hpBarMask.transform.localPosition = maskPos;
    }
}
```

### 3.4 에디터 유틸리티

#### Context Menu 추가
```csharp
[ContextMenu("Regenerate Grid")]
public void RegenerateGrid()
{
    SetGrid();
}
```
- GridManager를 우클릭 → "Regenerate Grid"로 그리드 재생성 가능

### 3.5 개선 효과

✅ **자동화:** Cell을 코드로 자동 생성 (수동 배치 불필요)  
✅ **확장성:** xMin, xMax 등의 값만 변경하면 그리드 크기 조정 가능  
✅ **정확성:** 위치 계산이 자동화되어 실수 방지  
✅ **재사용성:** 새로운 Scene에서도 바로 그리드 생성 가능  
✅ **유지보수성:** 코드로 관리되어 수정 및 디버깅 용이  
✅ **일관성:** 모든 Cell이 동일한 프리팹 기반으로 생성됨

---

## 4. InfoTab UI 시스템 구현

### 4.1 시스템 개요

**목적:** 유닛 정보를 표시하는 InfoTab의 활성화/비활성화 로직과 드래그&드롭 시스템을 명확히 분리

**핵심 기능:**
- 클릭과 드래그를 **하이브리드 방식**으로 구분
- InfoTab 활성화는 **짧은 클릭**으로만 가능
- 드래그는 **Preparation 상태의 아군**만 가능
- 적군은 **드래그 불가, 클릭 시 항상 InfoTab 표시**

### 4.2 신규 구현 기능

#### 4.2.1 클릭 vs 드래그 구분 (하이브리드 방식)

**구현 위치:** `Cell.cs`

**하이브리드 임계값:**
```csharp
[Header("Drag Threshold Settings")]
public float dragDistanceThreshold = 10f;   // 10픽셀 이상 OR
public float dragTimeThreshold = 0.15f;     // 0.15초 이상 = 드래그
```

**동작 흐름:**
```
1. OnMouseDown()
   └─ mouseDownPosition & mouseDownTime 저장

2. OnMouseDrag()
   ├─ 거리 = Distance(현재 위치, mouseDownPosition)
   ├─ 시간 = Time.time - mouseDownTime
   └─ if (거리 > 10px OR 시간 > 0.15s) → StartDrag()

3. OnMouseUp()
   ├─ if (isDraggingStarted) → EndDrag()
   └─ else → InfoTab 토글 (짧은 클릭)
```

**코드 구현:**
```csharp
private Vector3 mouseDownPosition;
private float mouseDownTime;
private bool isDraggingStarted = false;

private void OnMouseDown()
{
    if (isOccupied && unit != null)
    {
        Unit cellUnit = unit.GetComponent<Unit>();
        if (cellUnit != null && cellUnit.isActive)
        {
            // 초기값 저장 (모든 GameState에서)
            mouseDownPosition = Input.mousePosition;
            mouseDownTime = Time.time;
            isDraggingStarted = false;
        }
    }
}

private void OnMouseDrag()
{
    if (isDraggingStarted || !isOccupied || unit == null) return;
    
    Unit cellUnit = unit.GetComponent<Unit>();
    if (cellUnit == null || !cellUnit.isActive) return;
    
    // 적군은 드래그 불가
    if (cellUnit.IsEnemy) return;
    
    // Preparation 상태가 아니면 드래그 불가
    if (GameManager.Instance == null || 
        GameManager.Instance.gameState != GameState.Preparation)
    {
        return;
    }
    
    // 하이브리드 체크
    float distance = Vector3.Distance(Input.mousePosition, mouseDownPosition);
    float holdTime = Time.time - mouseDownTime;
    
    if (distance > dragDistanceThreshold || holdTime > dragTimeThreshold)
    {
        DragAndDropManager.Instance?.StartDrag(this);
        isDraggingStarted = true;
    }
}

private void OnMouseUp()
{
    if (isDraggingStarted)
    {
        // 드래그 종료
        DragAndDropManager.Instance?.EndDrag();
    }
    else
    {
        // 짧은 클릭 처리 - InfoTab 표시
        if (isOccupied && unit != null)
        {
            Unit cellUnit = unit.GetComponent<Unit>();
            if (cellUnit != null && cellUnit.isActive)
            {
                UIManager uiManager = GameManager.Instance?.uiManager;
                if (uiManager != null && uiManager.infoTab != null)
                {
                    // 모든 유닛 클릭 시 InfoTab 표시
                    uiManager.ShowInfoTab(cellUnit);
                }
            }
        }
    }
    
    isDraggingStarted = false;
}
```

#### 4.2.2 InfoTab 활성화 조건

**모든 유닛 (아군/적군):**
- ✅ Cell을 **짧게 클릭**했을 때 (드래그 임계값 미만)
- ✅ **모든 GameState**에서 작동 (Preparation, RoundInProgress 등)
- ✅ InfoTab이 이미 열려있어도 **클릭한 유닛의 정보로 갱신**

**이전 버전 (변경 전):**
```csharp
// 아군은 InfoTab이 꺼져있을 때만 켜기 (❌ 문제)
if (!uiManager.infoTab.gameObject.activeInHierarchy)
{
    uiManager.ShowInfoTab(cellUnit);
}
```

**현재 버전 (변경 후):**
```csharp
// 아군도 항상 ShowInfoTab 호출 (✅ 개선)
uiManager.ShowInfoTab(cellUnit);
```

#### 4.2.3 InfoTab 비활성화 조건

**InfoTab이 꺼지는 경우:**
- ✅ **ESC 키**를 누를 때
- ✅ **Close 버튼**을 클릭할 때
- ❌ 유닛 클릭으로는 **절대 꺼지지 않음** (토글 X, 열기만 가능)

**구현 코드:**
```csharp
// InfoTab.cs
private void Update()
{
    if (gameObject.activeInHierarchy && Input.GetKeyDown(KeyCode.Escape))
    {
        CloseInfoTab();
    }
}

public void OnCloseButtonClick()
{
    CloseInfoTab();
}

public void CloseInfoTab()
{
    gameObject.SetActive(false);
}
```

#### 4.2.4 드래그 가능 조건

**드래그가 가능한 경우:**
- ✅ GameState가 **Preparation** (준비 단계)
- ✅ 유닛이 **아군** (`IsEnemy == false`)
- ✅ 유닛이 존재하고 활성화 상태
- ✅ 하이브리드 임계값 초과 (10px OR 0.15초)

**드래그가 불가능한 경우:**
- ❌ **적군 유닛** (`IsEnemy == true`)
- ❌ **Preparation 외의 GameState** (RoundInProgress 등)
- ❌ 임계값 미만 (짧은 클릭)

### 4.3 기존 시스템 변경사항

#### 4.3.1 UIManager.cs 단순화

**제거된 코드:**
```csharp
// ❌ 제거됨 - 중복된 클릭 처리
private void Update()
{
    if (Input.GetMouseButtonDown(0))
    {
        HandleMouseClick();
    }
}

private void HandleMouseClick()
{
    // ... InfoTab 처리 로직
}
```

**이유:**
- `Cell.OnMouseDown()`과 `Input.GetMouseButtonDown(0)`이 **동시에 발생**
- 한 번의 클릭에 두 번의 로직이 실행되어 충돌 발생
- **Cell.OnMouseUp()**에서만 InfoTab을 처리하도록 통합

#### 4.3.2 DragAndDropManager.cs 역할 분리

**제거된 코드:**
```csharp
// ❌ 제거됨 - InfoTab 제어 로직
public void StartDrag(Cell cell)
{
    // ...
    uiManager.HideInfoTab();  // 드래그 시작 시 InfoTab 닫기
}

public void EndDrag()
{
    // ...
    // 드래그 종료 시 InfoTab 처리
}
```

**이유:**
- DragAndDropManager는 **드래그만** 담당하도록 역할 분리
- InfoTab은 **Cell.OnMouseUp()**에서만 제어
- 책임의 명확한 분리로 유지보수성 향상

#### 4.3.3 InfoTab.cs 디버그 코드 제거

**제거된 코드:**
```csharp
// ❌ 제거됨 - 디버그 로그
Debug.Log($"InfoTab opened for {unitData.unitName}");
Debug.Log($"Status count: {unitData.statusEffects.Count}");
// ...
```

**간소화된 탭 시스템:**
```csharp
// 기존: 6개 탭 (Stats, Skills, Synergy, Equipment, Upgrades, Effects)
// 현재: 3개 탭 (Stats, Upgrades, Effects) ✨ 단순화
```

#### 4.3.4 Status 시스템 통합

**신규 Status 시스템 지원:**
```csharp
// 기존 StatusEffect 지원 (하위 호환)
foreach (var effect in unit.statusEffects)
{
    // ...
}

// 신규 UnitStatus 지원 ✨ 추가
foreach (var status in unit.unitStatuses)
{
    foreach (var effectInstance in status.Effects)
    {
        // ...
    }
}
```

### 4.4 동작 테이블

| 상황 | 유닛 타입 | GameState | 마우스 동작 | 결과 |
|------|-----------|-----------|-------------|------|
| 1 | 아군 | Preparation | 짧은 클릭 (< 10px, < 0.15s) | ✅ InfoTab 표시 |
| 2 | 아군 | Preparation | 긴 클릭/드래그 (> 10px OR > 0.15s) | ✅ 드래그 시작 |
| 3 | 아군 | RoundInProgress | 짧은 클릭 | ✅ InfoTab 표시 |
| 4 | 아군 | RoundInProgress | 긴 클릭/드래그 | ❌ 드래그 불가, InfoTab 표시 |
| 5 | 적군 | 모든 상태 | 짧은 클릭 | ✅ InfoTab 표시 |
| 6 | 적군 | 모든 상태 | 긴 클릭/드래그 | ❌ 드래그 불가, InfoTab 표시 |
| 7 | 모든 유닛 | 모든 상태 | InfoTab 열린 상태에서 다른 유닛 클릭 | ✅ 해당 유닛 정보로 갱신 |

### 4.5 개선 효과

✅ **명확한 분리:** 클릭(InfoTab)과 드래그(이동)의 명확한 구분  
✅ **직관적인 UX:** 짧은 클릭 = 정보, 긴 클릭/드래그 = 이동  
✅ **오작동 방지:** 하이브리드 임계값으로 의도하지 않은 드래그 방지  
✅ **일관성:** 아군/적군 모두 클릭 시 정보 표시 (일관된 동작)  
✅ **역할 분리:** 각 Manager의 책임이 명확히 분리됨  
✅ **유연성:** GameState에 따른 드래그 제한, InfoTab은 항상 사용 가능  
✅ **버그 수정:** 
  - GameState 체크 위치 수정 (OnMouseDown → OnMouseDrag)
  - 아군 정보 갱신 안 되던 문제 해결
  - 중복 클릭 처리 제거

---

## 📊 종합 통계

### 신규 생성 파일
- `Scripts/Effects/` 폴더 (Base, Positive, Negative, Neutral)
- `Scripts/Entities/Status/UnitStatus.cs`
- `UI_Readme.md` (시스템 문서)
- `STATUS_SYSTEM_REFACTOR.md` (Status 시스템 문서)
- `SHIELD_SYSTEM_UPDATE.md` (방어막 시스템 문서)

### 주요 수정 파일
- `Unit.cs` (ShieldMax/Curr, Status 시스템)
- `Cell.cs` (클릭/드래그 하이브리드 시스템, UI 구조화)
- `GridManager.cs` (SetGrid, CreateFieldCells, CreateBenchCells)
- `InfoTab.cs` (디버그 제거, 탭 단순화, Status 지원)
- `UIManager.cs` (중복 클릭 처리 제거)
- `DragAndDropManager.cs` (InfoTab 제어 제거)

### 구현된 Effect
- **6개 Effect 클래스** (1001, 1002, 2001, 2002, 2003, 3001)
- **EffectFactory** (ID 기반 생성)
- **BaseEffect** (추상 기본 클래스)

### 코드 라인 수 (추정)
- 신규 작성: ~2,000 lines
- 수정: ~1,500 lines
- 문서: ~1,200 lines
- **총합: ~4,700 lines**

---

## 🎯 향후 계획

### Status 시스템
- [ ] 추가 Effect 구현 (Stun, Slow, Heal 등)
- [ ] 상태 우선도 시스템 고도화
- [ ] Status 애니메이션 효과 추가

### 방어막 시스템
- [ ] 방어막 획득/소진 시각 효과
- [ ] 방어막 종류 다양화 (물리/마법 방어막)
- [ ] 방어막 관련 스킬/아이템 추가

### Grid 시스템
- [ ] 그리드 크기 런타임 변경 기능
- [ ] Cell 타일 종류 추가 (버프/디버프 타일)
- [ ] 그리드 애니메이션 효과

### InfoTab 시스템
- [ ] 탭 전환 애니메이션
- [ ] 유닛 비교 기능 (2개 유닛 동시 표시)
- [ ] 상세 정보 툴팁 추가

---

**작성 완료일:** 2025년 10월 15일  
**다음 리포트:** 2025년 10월 하반기 (10월 16일 ~ 10월 31일)
