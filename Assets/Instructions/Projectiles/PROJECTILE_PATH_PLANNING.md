# 투사체 경로 다양화 플래닝 (Projectile Path Diversification Planning)

## 📋 현재 상황 분석
- **현재 구조**: `Vector3.Lerp`를 사용한 단순 직선 이동
- **문제점**: 모든 투사체가 동일한 직선 궤적으로만 이동
- **개선 목표**: 다양한 궤적 패턴 지원

---

## 🎯 제안하는 경로 타입 (그리드 기반 전략 게임)

### 1. **직선 (Linear)** - 현재 구현됨 ⭐
- 가장 빠른 경로
- **사용 예시**: 화살, 석궁, 총알, 마법 미사일
- 그리드 타일에서 타일로 직선 발사

### 2. **포물선 (Parabolic Arc)** ⭐⭐⭐
- 중력의 영향을 받는 자연스러운 궤적
- **사용 예시**: 투석기, 박격포, 캐터펄트, 수류탄
- 장애물 위로 넘어가는 효과
- 파라미터: `arcHeight` (궤적의 최고 높이)
- **전략 게임 필수 경로**

### 3. **베지어 곡선 (Bezier Curve)** ⭐⭐
- 부드러운 곡선 경로
- **사용 예시**: 마법 공격, 정령 소환, 특수 스킬
- 시각적으로 화려한 효과
- 파라미터: `controlPoint` (곡선의 제어점) 또는 `arcOffset` (자동 계산)

### 4. **다중 포인트 경유 (Multi-Point)** ⭐
- 여러 그리드를 경유하는 경로
- **사용 예시**: 바운스 공격, 연쇄 번개, 도미노 효과
- 한 번에 여러 타일 공격
- 파라미터: `waypoints[]` (경유 지점 배열)

### 5. **지연 낙하 (Delayed Drop)** ⭐
- 목표 지점 위로 올라간 후 수직 낙하
- **사용 예시**: 공중 폭격, 유성 낙하, 낙뢰
- 강한 임팩트 연출
- 파라미터: `riseHeight`, `hangTime` (정점에서 머무는 시간)

### 6. **나선형 (Spiral)** ⭐
- 회전하면서 전진하는 경로
- **사용 예시**: 드릴 공격, 회오리 마법, 토네이도
- 역동적인 시각 효과
- 파라미터: `spiralRadius`, `spiralSpeed`, `rotations`

### 7. **웨이브 (Wave)** ⭐
- 부드러운 파동 형태로 이동
- **사용 예시**: 물 계열 마법, 음파 공격, 에너지 웨이브
- 유기적인 움직임
- 파라미터: `waveAmplitude`, `waveFrequency`

---

## 🏗️ 구현 아키텍처 설계

### Option A: Enum + Switch 방식 (간단, 추천)
```
장점:
- 구현이 간단하고 직관적
- 성능 오버헤드 최소
- Unity Inspector에서 쉽게 선택 가능

단점:
- 새로운 경로 추가 시 코드 수정 필요
- 복잡한 경로 조합이 어려움
```

**구조:**
```
ProjectilePathType (Enum)
  ├─ Linear
  ├─ ParabolicArc
  ├─ BezierCurve
  ├─ MultiPoint
  ├─ DelayedDrop
  ├─ Spiral
  └─ Wave

HS_ProjectileCustomMover
  └─ Update() → switch (pathType)
```

### Option B: Strategy Pattern (유연성 높음)
```
장점:
- 각 경로 로직이 독립적
- 런타임에 경로 변경 가능
- 새로운 경로 추가가 쉬움
- 경로 조합 가능 (Composite Pattern)

단점:
- 클래스가 많아짐
- 약간의 성능 오버헤드
- 초기 구현 복잡도 높음
```

**구조:**
```
IProjectilePath (Interface)
  ├─ LinearPath
  ├─ ParabolicPath
  ├─ BezierPath
  ├─ HighArcPath
  ├─ MultiPointPath
  └─ DelayedDropPath

HS_ProjectileCustomMover
  └─ IProjectilePath currentPath
```

### Option C: ScriptableObject 기반 (데이터 주도)
```
장점:
- 경로 설정을 데이터로 관리
- 재사용성 극대화
- 프로그래머가 아닌 사람도 수정 가능
- 런타임에 메모리 효율적

단점:
- 초기 설정 복잡
- 파일 관리 필요
```

**구조:**
```
ProjectilePathData (ScriptableObject)
  ├─ pathType
  ├─ parameters (Dictionary or custom class)
  └─ ...

각 Prefab이 PathData를 참조
```

---

## 💡 추천 구현 방안

### 🥇 **1단계: Enum + Switch 방식으로 시작**
- 빠른 프로토타이핑
- 핵심 경로 타입 4~5개 구현
- 테스트 및 피드백 수집

### 🥈 **2단계: 필요시 Strategy Pattern으로 리팩토링**
- 경로 타입이 10개 이상으로 증가하면
- 경로 조합이 필요하면 (예: 포물선 + 호밍)
- 런타임 경로 변경이 필요하면

### 🥉 **3단계: ScriptableObject 추가**
- 밸런싱이 중요해지면
- 많은 수의 다른 투사체 프리팹을 관리해야 하면

---

## 📝 구체적인 구현 예시 (1단계)

### 새로운 파일 구조:
```
ProjectilePathType.cs          - Enum 정의
ProjectilePathData.cs          - 경로 파라미터 클래스
HS_ProjectileCustomMover.cs    - 수정: 경로 계산 로직 추가
SfxManager.cs                  - 수정: FireProjectile 메서드 확장
```

### SfxManager에 추가할 메서드:
```csharp
// 기본 메서드 (하위 호환성 유지)
FireSingleProjectile(prefab, from, to, duration)

// 확장 메서드
FireSingleProjectile(prefab, from, to, duration, pathType)
FireSingleProjectile(prefab, from, to, duration, pathType, pathParams)

// 또는 Builder Pattern
FireProjectile(prefab)
    .From(unit)
    .To(unit)
    .WithDuration(duration)
    .WithPath(pathType)
    .WithArcHeight(height)  // 선택적
    .Fire()
```

---

## 🎮 각 경로별 수학 공식

### 1. Linear (직선) - 현재 구현
```csharp
Vector3 CalculateLinearPoint(Vector3 start, Vector3 end, float t)
{
    return Vector3.Lerp(start, end, t);
}
```

### 2. Parabolic Arc (포물선) ⭐⭐⭐
```csharp
Vector3 CalculateParabolicPoint(Vector3 start, Vector3 end, float t, float arcHeight)
{
    Vector3 linearPoint = Vector3.Lerp(start, end, t);
    // 포물선 방정식: 0에서 시작해서 중간에 최고점, 끝에서 0
    float parabola = 4 * arcHeight * t * (1 - t);
    linearPoint.y += parabola;
    return linearPoint;
}
```

### 3. Bezier Curve (베지어 곡선)
```csharp
// 자동 제어점 계산 (간편 버전)
Vector3 CalculateAutoBezierPoint(Vector3 start, Vector3 end, float t, float arcOffset)
{
    // 중간 지점에서 수직으로 offset만큼 떨어진 제어점
    Vector3 midPoint = (start + end) / 2f;
    Vector3 controlPoint = midPoint + Vector3.up * arcOffset;
    
    return CalculateBezierPoint(start, controlPoint, end, t);
}

// 2차 베지어 곡선 (3개 점)
Vector3 CalculateBezierPoint(Vector3 p0, Vector3 p1, Vector3 p2, float t)
{
    float u = 1 - t;
    float tt = t * t;
    float uu = u * u;
    
    return uu * p0 + 2 * u * t * p1 + tt * p2;
}

// 3차 베지어 곡선 (4개 점) - 더 부드러운 곡선
Vector3 CalculateCubicBezierPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
{
    float u = 1 - t;
    float tt = t * t;
    float uu = u * u;
    float uuu = uu * u;
    float ttt = tt * t;
    
    return uuu * p0 + 3 * uu * t * p1 + 3 * u * tt * p2 + ttt * p3;
}
```

### 4. High Arc (높은 포물선)
```csharp
Vector3 CalculateHighArcPoint(Vector3 start, Vector3 end, float t, float arcHeight)
{
    Vector3 linearPoint = Vector3.Lerp(start, end, t);
    
    // 더 가파른 포물선 (EaseInOut 적용)
    float normalizedHeight = Mathf.Sin(t * Mathf.PI);  // 0 -> 1 -> 0 (부드러운 곡선)
    linearPoint.y += normalizedHeight * arcHeight;
    
    return linearPoint;
}
```

### 5. Multi-Point (다중 경유)
```csharp
Vector3 CalculateMultiPointPath(Vector3[] waypoints, float t)
{
    if (waypoints.Length < 2) return waypoints[0];
    
    // 전체 경로를 세그먼트로 나눔
    float totalProgress = t * (waypoints.Length - 1);
    int currentSegment = Mathf.FloorToInt(totalProgress);
    
    if (currentSegment >= waypoints.Length - 1)
        return waypoints[waypoints.Length - 1];
    
    float segmentProgress = totalProgress - currentSegment;
    
    // 각 세그먼트는 포물선으로 연결 (선택적)
    return CalculateParabolicPoint(
        waypoints[currentSegment], 
        waypoints[currentSegment + 1], 
        segmentProgress,
        2f  // 작은 아크
    );
}
```

### 6. Delayed Drop (지연 낙하)
```csharp
Vector3 CalculateDelayedDropPoint(Vector3 start, Vector3 end, float t, 
    float riseHeight, float hangTime)
{
    // 3단계: 상승 -> 정지 -> 낙하
    float risePhase = 0.3f;  // 전체 시간의 30%
    float hangPhase = hangTime;  // 정지 시간
    float dropPhase = 1f - risePhase - hangPhase;
    
    Vector3 targetPosition = end;
    Vector3 peakPosition = new Vector3(
        Mathf.Lerp(start.x, end.x, 0.5f),
        end.y + riseHeight,
        Mathf.Lerp(start.z, end.z, 0.5f)
    );
    
    if (t < risePhase)
    {
        // 상승 단계
        float riseT = t / risePhase;
        return Vector3.Lerp(start, peakPosition, riseT);
    }
    else if (t < risePhase + hangPhase)
    {
        // 정지 단계 (약간의 흔들림 추가 가능)
        return peakPosition;
    }
    else
    {
        // 낙하 단계 (가속)
        float dropT = (t - risePhase - hangPhase) / dropPhase;
        dropT = dropT * dropT;  // 제곱으로 가속감 표현
        return Vector3.Lerp(peakPosition, targetPosition, dropT);
    }
}
```

### 보조 함수: 회전 계산
```csharp
// 투사체가 이동 방향을 바라보도록 회전
void UpdateProjectileRotation(Vector3 previousPos, Vector3 currentPos)
{
    Vector3 direction = (currentPos - previousPos).normalized;
    if (direction != Vector3.zero)
    {
        transform.rotation = Quaternion.LookRotation(direction);
    }
}
```

---

## 🔧 구현 체크리스트

### Phase 1: 기본 구조 (2-3시간)
- [ ] `ProjectilePathType` enum 생성
- [ ] `ProjectilePathData` 클래스 생성
- [ ] `HS_ProjectileCustomMover` 리팩토링
  - [ ] 경로 타입별 계산 메서드 분리
  - [ ] Update 로직 수정
  - [ ] 이전 위치 저장 (회전 계산용)

### Phase 2: 핵심 경로 구현 (3-4시간)
- [ ] Linear (기존 코드 유지)
- [ ] Parabolic Arc ⭐ 우선순위 1
- [ ] High Arc
- [ ] Bezier Curve (자동 제어점 버전)

### Phase 3: SfxManager 통합 (1-2시간)
- [ ] FireSingleProjectile 오버로드 추가
- [ ] 기존 코드 하위 호환성 유지
- [ ] 기본값 설정 (pathType = Linear)
- [ ] Inspector에서 테스트 가능하도록 설정

### Phase 4: 추가 경로 & 폴리싱 (2-3시간)
- [ ] Multi-Point Path 구현
- [ ] Delayed Drop 구현
- [ ] 파티클 회전 자동 업데이트
- [ ] 경로별 시각적 피드백 조정
- [ ] 그리드 타일 좌표에서 자동 높이 보정

---

## 🎨 사용 예시 (그리드 기반 전략 게임)

```csharp
// 직선 화살 - 궁수 유닛
sfxManager.FireSingleProjectile(arrowPrefab, archerUnit, enemyUnit, 0.5f, 
    ProjectilePathType.Linear);

// 포물선 투석 - 투석기
sfxManager.FireSingleProjectile(rockPrefab, catapultUnit, targetUnit, 2.0f, 
    ProjectilePathType.ParabolicArc, 
    new ProjectilePathData { arcHeight = 5f });

// 높은 포물선 - 공성무기 (트레뷰셋)
sfxManager.FireSingleProjectile(boulderPrefab, trebuchetUnit, castleUnit, 3.0f, 
    ProjectilePathType.HighArc, 
    new ProjectilePathData { arcHeight = 12f });

// 베지어 마법 - 마법사 스킬
sfxManager.FireSingleProjectile(magicBoltPrefab, wizardUnit, targetUnit, 1.5f, 
    ProjectilePathType.Bezier, 
    new ProjectilePathData { arcOffset = 4f });

// 다중 경유 - 연쇄 번개 (여러 타일 공격)
Vector3[] lightningPath = new Vector3[] { 
    casterUnit.position, 
    enemy1.position, 
    enemy2.position, 
    enemy3.position 
};
sfxManager.FireSingleProjectile(lightningPrefab, casterUnit, enemy3, 2.5f, 
    ProjectilePathType.MultiPoint, 
    new ProjectilePathData { waypoints = lightningPath });

// 지연 낙하 - 폭격, 유성 낙하
sfxManager.FireSingleProjectile(meteorPrefab, skyPosition, targetTile, 2.0f, 
    ProjectilePathType.DelayedDrop, 
    new ProjectilePathData { riseHeight = 15f, hangTime = 0.2f });
```

### 그리드 타일 기반 사용 예시
```csharp
// 타일 좌표를 사용하는 경우
GridTile startTile = gridManager.GetTile(3, 5);
GridTile targetTile = gridManager.GetTile(8, 12);

// 타일 중앙 위치로 자동 변환
Vector3 startPos = startTile.GetCenterWorldPosition();
Vector3 targetPos = targetTile.GetCenterWorldPosition();

sfxManager.FireSingleProjectile(projectilePrefab, startPos, targetPos, 1.5f, 
    ProjectilePathType.ParabolicArc,
    new ProjectilePathData { arcHeight = 4f });
```

---

## ⚠️ 주의사항

1. **성능 고려**
   - 동시에 수백 개의 투사체가 있다면 직선만 사용
   - 복잡한 계산은 프로파일링 필수
   - 그리드 게임 특성상 동시 발사가 많을 수 있으니 최적화 중요

2. **물리 엔진과의 상호작용**
   - 현재 코드는 Transform 기반 이동
   - Rigidbody 사용 시 별도 처리 필요
   - 그리드 게임은 대부분 Transform 기반이 적합

3. **네트워크 동기화**
   - 멀티플레이어라면 경로 데이터 동기화 필요
   - 결정론적(deterministic) 경로 선호
   - 시작/끝점만 전송하면 클라이언트에서 경로 재생 가능

4. **그리드 타일 높이**
   - 타일마다 높이가 다를 수 있음 (언덕, 계곡 등)
   - 투사체 시작/끝 위치에 타일 높이 자동 보정 필요
   - arcHeight는 지형 높이에 추가되는 값

5. **장애물 처리**
   - 직선 경로는 장애물에 막힐 수 있음
   - 포물선/높은 포물선은 장애물 회피 가능
   - 시각적으로만 넘어가는 것인지, 실제 히트 판정도 무시할지 결정 필요

---

## 📊 우선순위 추천

### 그리드 기반 전략 게임 (현재 프로젝트) ⭐⭐⭐

**필수 경로 (Phase 1-2):**
1. **Linear** (현재 구현) - 기본 원거리 공격 (궁수, 석궁병)
2. **Parabolic Arc** (최우선 구현) - 투석기, 박격포, 수류탄
3. **Bezier Curve** - 마법 공격, 특수 스킬

**선택 경로 (Phase 3-4):**
4. **High Arc** - 공성 무기, 극적인 연출이 필요한 경우
5. **Multi-Point** - 연쇄 공격, 바운스 스킬이 있다면
6. **Delayed Drop** - 공중 공격, 폭격 유닛이 있다면

### 구현 우선순위 결정 기준:
- ✅ 투석기/박격포 같은 유닛이 있나? → **Parabolic Arc 필수**
- ✅ 마법 시스템이 있나? → **Bezier Curve 추천**
- ✅ 공성전/대규모 공격 연출이 중요한가? → **High Arc 추천**
- ✅ 연쇄 공격 메커니즘이 있나? → **Multi-Point 필요**
- ✅ 공중 유닛이나 폭격이 있나? → **Delayed Drop 고려**

### 다른 장르 참고용:

**액션 RPG:**
1. Linear (기본 공격)
2. Bezier (마법)
3. Parabolic (투척 무기)

**타워 디펜스:**
1. Linear (기본 타워)
2. Parabolic (박격포)
3. Bezier (특수 타워)

**실시간 전략 (RTS):**
1. Parabolic (대부분의 공성 무기)
2. Linear (총기류, 레이저)
3. High Arc (대포, 트레뷰셋)
4. Multi-Point (연쇄 효과)

---

## 🚀 다음 단계

이 플래닝을 바탕으로 다음 중 선택해주세요:

1. **즉시 구현 시작** - 1단계(Enum 방식)부터 코드 작성
2. **특정 경로만 구현** - 필요한 경로 타입만 먼저 구현
3. **Strategy Pattern으로 바로 시작** - 더 확장 가능한 구조
4. **프로토타입 예제 코드** - 전체 구조의 샘플 코드 제공

어떤 방향으로 진행하시겠습니까?

