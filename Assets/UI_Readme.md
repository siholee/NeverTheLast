# UI 시스템 정리 문서

## 📋 InfoTab 클릭 및 드래그 시스템

### **개요**
유닛 정보를 표시하는 InfoTab의 활성화/비활성화 로직과 드래그&드롭 시스템을 명확히 분리하여 구현했습니다.

---

## 🎯 핵심 기능

### **1. 클릭 vs 드래그 구분 (하이브리드 방식)**

**구현 위치:** `Cell.cs`

**방식:** 거리 OR 시간 중 하나를 만족하면 드래그로 인식

```csharp
// 임계값 설정
public float dragDistanceThreshold = 10f;   // 10픽셀 이상 움직이면
public float dragTimeThreshold = 0.15f;     // 또는 0.15초 이상 누르면
```

**동작 흐름:**
```
1. OnMouseDown() → 마우스 위치 & 시간 저장
2. OnMouseDrag() → 거리/시간 체크 → 임계값 초과 시 드래그 시작
3. OnMouseUp() → 드래그 여부에 따라 분기
   ├─ 드래그 O → DragAndDropManager.EndDrag()
   └─ 드래그 X → InfoTab 토글 (켜기만)
```

---

### **2. InfoTab 활성화 조건**

**아군 유닛 클릭 시:**
- ✅ Cell을 **짧게 클릭**했을 때 (드래그 임계값 미만)
- ✅ InfoTab이 **꺼져있는 상태**일 때만 켜기

**적군 유닛 클릭 시:**
- ✅ **무조건 InfoTab 활성화** (드래그 불가능)
- ✅ 이미 켜져있어도 해당 유닛 정보로 전환

**구현 코드:**
```csharp
// Cell.cs - OnMouseUp()
if (!isDraggingStarted) // 드래그가 아닌 경우
{
    if (cellUnit.IsEnemy)
    {
        // 적군은 무조건 InfoTab 활성화
        uiManager.ShowInfoTab(cellUnit);
    }
    else
    {
        // 아군은 꺼져있으면 켜기
        if (!uiManager.infoTab.gameObject.activeInHierarchy)
        {
            uiManager.ShowInfoTab(cellUnit);
        }
    }
}
```

---

### **3. InfoTab 비활성화 조건**

**InfoTab이 꺼지는 경우:**
- ✅ **ESC 키**를 누를 때 (`InfoTab.cs`)
- ✅ **Close 버튼**을 클릭할 때 (`InfoTab.cs`)

**구현 코드:**
```csharp
// InfoTab.cs - Update()
if (gameObject.activeInHierarchy && Input.GetKeyDown(KeyCode.Escape))
{
    CloseInfoTab();
}

// InfoTab.cs - OnCloseButtonClick() (버튼 OnClick 이벤트)
public void OnCloseButtonClick()
{
    CloseInfoTab();
}
```

---

### **4. 드래그 가능 조건**

**드래그가 가능한 경우:**
- ✅ GameState가 **Preparation** (준비 단계)
- ✅ 유닛이 **아군** (`IsEnemy == false`)
- ✅ 유닛이 존재하고 활성화 상태

**드래그가 불가능한 경우:**
- ❌ 적군 유닛 (`IsEnemy == true`)
- ❌ Preparation 외의 GameState
- ❌ 비활성화된 유닛

**구현 코드:**
```csharp
// Cell.cs - OnMouseDown()
if (!cellUnit.IsEnemy) // 아군만 드래그 가능
{
    if (GameManager.Instance.gameState != GameState.Preparation) return;
    // 드래그 준비
}
else
{
    // 적군은 드래그 불가능
    isDraggingStarted = false;
}
```

---

### **5. 드롭 가능 지점**

**드롭이 허용되는 Cell:**
- ✅ **벤치 (Bench)** - 대기석
- ✅ **아군 영역** (`xPos < 0`)

**드롭이 불가능한 Cell:**
- ❌ 적군 영역 (`xPos >= 0`)
- ❌ 벤치/아군 영역 외의 셀

**구현 위치:** `DragAndDropManager.cs - IsValidDropTarget()`

---

### **6. InfoTab 활성화 조건 (수정됨)**

**아군 유닛 클릭 시:**
- ✅ InfoTab이 **꺼져있으면** → 켜기
- ✅ InfoTab이 **켜져있으면** → 아무 동작 안 함

**적군 유닛 클릭 시:**
- ✅ **무조건 InfoTab 켜기** (드래그 불가능하므로)
- ✅ 이미 켜져있어도 해당 유닛으로 전환

**구현 코드:**
```csharp
// Cell.cs - OnMouseUp()
if (cellUnit.IsEnemy)
{
    // 적군은 무조건 InfoTab 활성화
    uiManager.ShowInfoTab(cellUnit);
}
else
{
    // 아군은 꺼져있으면 켜기
    if (!uiManager.infoTab.gameObject.activeInHierarchy)
    {
        uiManager.ShowInfoTab(cellUnit);
    }
}
```

---

## 🔄 전체 동작 흐름

### **시나리오 1: 짧은 클릭 (InfoTab 표시)**

```
사용자: Cell 클릭 (10픽셀 미만 이동, 0.15초 미만)
   ↓
Cell.OnMouseDown() → 위치/시간 저장
   ↓
Cell.OnMouseDrag() → 임계값 미달 → 드래그 시작 안 함
   ↓
Cell.OnMouseUp() → isDraggingStarted = false
   ↓
InfoTab.gameObject.activeInHierarchy 체크
   ├─ true (이미 켜짐) → 아무 동작 안 함
   └─ false (꺼짐) → ShowInfoTab() 호출 ✅
```

---

### **시나리오 2: 드래그 (유닛 이동)**

```
사용자: Cell 클릭 후 10픽셀 이상 또는 0.15초 이상 홀드
   ↓
Cell.OnMouseDown() → 위치/시간 저장
   ↓
Cell.OnMouseDrag() → 임계값 초과 ✅
   ↓
DragAndDropManager.StartDrag() 호출
   ├─ isDragging = true
   ├─ dragPreview 활성화
   └─ 원본 유닛 반투명
   ↓
사용자: 마우스 이동 → dragPreview 따라감
   ↓
Cell.OnMouseUp() → isDraggingStarted = true
   ↓
DragAndDropManager.EndDrag() 호출
   ├─ 레이캐스트로 타겟 Cell 찾기
   ├─ 유닛 이동 or 교환
   ├─ dragPreview 숨김
   └─ isDragging = false
   ↓
InfoTab은 변경 안 함 (켜져있으면 켜진 상태 유지)
```

---

### **시나리오 3: ESC 키로 InfoTab 닫기**

```
사용자: InfoTab이 열린 상태에서 ESC 키 입력
   ↓
InfoTab.Update() → Input.GetKeyDown(KeyCode.Escape) 감지
   ↓
InfoTab.CloseInfoTab() 호출
   ├─ isInfoTabActive = false
   ├─ currentDisplayedUnit = null
   └─ gameObject.SetActive(false) ✅
```

---

## 📂 수정된 파일 목록

### **1. Cell.cs**
- ✅ 하이브리드 클릭/드래그 구분 로직 추가
- ✅ OnMouseDrag() 메서드 추가
- ✅ OnMouseUp()에서 InfoTab 토글 로직 추가

**추가된 필드:**
```csharp
private Vector3 mouseDownPosition;
private float mouseDownTime;
private bool isDraggingStarted = false;
public float dragDistanceThreshold = 10f;
public float dragTimeThreshold = 0.15f;
```

**주요 변경:**
- ✅ 아군/적군 구분 로직 추가
- ✅ 아군만 드래그 가능
- ✅ 적군은 클릭 시 무조건 InfoTab 표시

---

### **2. UIManager.cs**
- ✅ Update() 메서드 완전 제거 (클릭 감지 로직 삭제)
- ✅ HandleMouseClick() 메서드 제거
- ✅ HandleCellClick() 메서드 제거

**남은 InfoTab 메서드:**
```csharp
public void ShowInfoTab(Unit unit)  // Cell에서 호출
public void HideInfoTab()           // 사용 안 함 (보존만)
```

---

### **3. DragAndDropManager.cs**
- ✅ StartDrag()의 InfoTab 제어 로직 제거
- ✅ EndDrag()의 InfoTab 재활성화 로직 제거

**변경 내용:**
- 드래그 시작/종료 시 InfoTab을 건드리지 않음
- Cell에서 직접 관리하도록 변경

---

### **4. InfoTab.cs**
- ℹ️ 기존 로직 유지 (변경 없음)
- ESC 키 감지
- Close 버튼 콜백

---

## 🎨 사용자 경험 (UX)

### **장점**

1. **명확한 클릭/드래그 구분**
   - ✅ 의도하지 않은 드래그 방지
   - ✅ 정밀한 클릭 가능

2. **직관적인 InfoTab 제어**
   - ✅ 클릭으로 InfoTab 열기
   - ✅ ESC/버튼으로만 닫기
   - ✅ 드래그 중에도 InfoTab 상태 유지

3. **일관된 동작**
   - ✅ 아군 Cell 클릭 → InfoTab 열림 (꺼져있을 때만)
   - ✅ 적군 Cell 클릭 → InfoTab 열림 (무조건)
   - ✅ ESC/버튼 → InfoTab 닫힘
   - ✅ 드래그 → InfoTab 영향 없음
   - ✅ 적군은 드래그 불가능

---

## ⚙️ 설정 가능한 값

### **드래그 임계값 조정**

Unity Inspector에서 Cell 프리팹/인스턴스 선택 후:

```
Drag Threshold Settings:
├─ Drag Distance Threshold: 10 (픽셀)
│   - 낮음 (5): 예민한 드래그
│   - 보통 (10): 권장값
│   - 높음 (20): 둔한 드래그
│
└─ Drag Time Threshold: 0.15 (초)
    - 빠름 (0.1): 빠른 드래그
    - 보통 (0.15): 권장값
    - 느림 (0.3): 느린 드래그
```

---

## 🔧 디버깅 팁

### **드래그가 너무 쉽게 시작되는 경우**
→ `dragDistanceThreshold`를 **15~20**으로 증가

### **드래그가 너무 어려운 경우**
→ `dragDistanceThreshold`를 **5~7**로 감소

### **InfoTab이 안 열리는 경우**
1. Cell에 유닛이 있는지 확인
2. InfoTab GameObject가 씬에 있는지 확인
3. UIManager.infoTab 참조가 연결되었는지 확인

### **드래그가 안 되는 경우**
1. GameState가 Preparation인지 확인
2. 유닛이 아군(`IsEnemy == false`)인지 확인
3. DragAndDropManager.Instance가 있는지 확인

### **적군 InfoTab이 안 열리는 경우**
1. Unit.IsEnemy 값이 올바르게 설정되었는지 확인
2. UIManager.infoTab 참조가 연결되었는지 확인

### **드래그가 잘못된 위치로 가능한 경우**
1. IsValidDropTarget 로직 확인
2. 벤치 셀 또는 xPos < 0 영역만 허용되는지 확인

---

## 📊 시스템 비교

### **이전 시스템 (문제점)**
- ❌ OnMouseDown에서 즉시 드래그 시작
- ❌ UIManager.Update()에서 중복 클릭 감지
- ❌ 드래그와 InfoTab 열림이 동시 발생
- ❌ 드래그 종료 시 InfoTab이 강제로 열림
- ❌ xPos 기반으로 드래그 판단 (적군 영역도 드래그 가능했음)

### **현재 시스템 (개선됨)**
- ✅ OnMouseDrag에서 임계값 체크 후 드래그 시작
- ✅ Cell에서 클릭/드래그를 직접 판단
- ✅ 짧은 클릭만 InfoTab 열기
- ✅ InfoTab은 ESC/버튼으로만 닫기
- ✅ 아군/적군 명확히 구분
- ✅ 적군은 드래그 불가능, InfoTab만 표시
- ✅ 드롭 지점 제한 (벤치 또는 아군 영역만)

---

## 🚀 향후 개선 가능 사항

1. **아군 InfoTab 토글 기능**
   - 현재: 아군 클릭으로 열기만 가능
   - 개선: 같은 Cell 재클릭 시 닫기

2. **다른 Cell 클릭 시 전환 (아군)**
   - 현재: InfoTab 열린 상태에서 다른 아군 Cell 클릭 시 무반응
   - 개선: 다른 Cell 클릭 시 해당 유닛으로 전환

3. **드래그 피드백 강화**
   - 현재: 드래그 프리뷰만 표시
   - 개선: 드래그 가능/불가능 시각적 피드백 추가 (적군에 대한 "드래그 불가" 표시)

4. **모바일 최적화**
   - 터치 입력에 맞게 임계값 자동 조정
   - 롱 프레스 제스처 지원

---

## 📝 용어 정리

- **InfoTab**: 유닛 상세 정보를 표시하는 UI 패널
- **드래그 임계값 (Threshold)**: 클릭과 드래그를 구분하는 기준값
- **하이브리드 방식**: 거리 OR 시간 조건을 사용하는 방식
- **토글 (Toggle)**: On/Off 상태를 전환하는 동작

---

**작성일:** 2025-10-11  
**작성자:** AI Assistant  
**관련 파일:**
- `Cell.cs`
- `InfoTab.cs`
- `UIManager.cs`
- `DragAndDropManager.cs`
