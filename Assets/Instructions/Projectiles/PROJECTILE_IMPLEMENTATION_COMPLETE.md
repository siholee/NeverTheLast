# ✅ 투사체 경로 시스템 구현 완료

## 📦 구현된 파일들

### 새로 생성된 파일:
1. **`Assets/Scripts/Effects/ProjectilePathType.cs`**
   - 7가지 경로 타입 Enum 정의
   
2. **`Assets/Scripts/Effects/ProjectilePathData.cs`**
   - 경로별 파라미터 저장 클래스
   - 편리한 생성자 메서드 제공

3. **`Assets/PROJECTILE_USAGE_GUIDE.md`**
   - 사용 가이드 및 예시 코드

### 수정된 파일:
1. **`Assets/Hovl Studio/HSFiles/Scripts/HS_ProjectileCustomMover.cs`**
   - 7가지 경로 계산 로직 추가
   - 자동 회전 시스템 추가
   - 경로 데이터 초기화 로직 추가

2. **`Assets/Scripts/Managers/SfxManager.cs`**
   - 오버로드 메서드 3개 추가
   - 하위 호환성 유지

---

## 🎯 구현된 경로 타입

### 1. Linear (직선) ✅
- 기존 코드 유지
- 가장 빠른 경로

### 2. ParabolicArc (포물선) ✅
- 중력 영향 시뮬레이션
- 포물선 방정식: `4 * height * t * (1-t)`
- 투석기, 박격포에 적합

### 3. BezierCurve (베지어 곡선) ✅
- 2차 베지어 곡선 (3개 점)
- 자동 제어점 계산 지원
- 마법 공격에 적합

### 4. MultiPoint (다중 경유) ✅
- 여러 지점을 순차적으로 경유
- 각 세그먼트는 포물선으로 연결
- 연쇄 번개, 바운스 공격에 적합

### 5. DelayedDrop (지연 낙하) ✅
- 3단계: 상승(30%) → 정지(가변) → 낙하(나머지)
- 낙하 시 가속 효과
- 공중 폭격, 유성 낙하에 적합

### 6. Spiral (나선형) ✅
- 회전하면서 전진
- 반지름이 점점 작아짐 (수렴)
- 드릴, 회오리 공격에 적합

### 7. Wave (웨이브) ✅
- 사인파 기반 좌우 진동
- 경로에 수직 방향으로 움직임
- 물 마법, 음파 공격에 적합

---

## 🔧 주요 기능

### 자동 회전 시스템
- 투사체가 자동으로 이동 방향을 바라봄
- `UpdateProjectileRotation()` 메서드
- 부드러운 Slerp 보간

### 경로 데이터 캐싱
- 시작/끝 위치 캐싱
- 베지어 제어점 사전 계산
- 경로 방향/수직 벡터 계산

### 하위 호환성
- 기존 `FireSingleProjectile(prefab, from, to, duration)` 호출 유지
- 자동으로 Linear 경로 사용

---

## 📖 사용 방법

### 기본 사용 (기존 코드와 동일)
```csharp
sfxManager.FireSingleProjectile(arrowPrefab, archer, enemy, 0.5f);
// → 자동으로 Linear 경로 사용
```

### 경로 타입 지정
```csharp
// 포물선
sfxManager.FireSingleProjectile(
    rockPrefab, catapult, target, 2.0f, 
    ProjectilePathType.ParabolicArc
);

// 베지어 곡선
sfxManager.FireSingleProjectile(
    magicPrefab, wizard, target, 1.5f, 
    ProjectilePathType.BezierCurve
);
```

### 파라미터 지정
```csharp
// 포물선 높이 조정
sfxManager.FireSingleProjectile(
    boulderPrefab, trebuchet, castle, 3.0f, 
    ProjectilePathType.ParabolicArc,
    ProjectilePathData.CreateParabolic(8f)  // 높이 8
);

// 나선형 설정
sfxManager.FireSingleProjectile(
    drillPrefab, attacker, target, 1.5f, 
    ProjectilePathType.Spiral,
    ProjectilePathData.CreateSpiral(1f, 2f, 3f)  // 반지름, 속도, 회전수
);
```

### 다중 경유 (연쇄 공격)
```csharp
Vector3[] path = new Vector3[] 
{ 
    caster.position, 
    enemy1.position, 
    enemy2.position, 
    enemy3.position 
};

sfxManager.FireSingleProjectile(
    lightningPrefab, caster, enemy3, 2.5f, 
    ProjectilePathType.MultiPoint,
    ProjectilePathData.CreateMultiPoint(path)
);
```

---

## 🎮 Inspector 설정

투사체 Prefab의 `HS_ProjectileCustomMover` 컴포넌트에서:

1. **Path Type** 드롭다운에서 경로 타입 선택
2. **Path Data** 섹션에서 파라미터 조정:
   - ParabolicArc: `Arc Height`
   - BezierCurve: `Arc Offset`
   - Spiral: `Spiral Radius`, `Spiral Speed`, `Total Rotations`
   - Wave: `Wave Amplitude`, `Wave Frequency`
   - DelayedDrop: `Rise Height`, `Hang Time`
   - MultiPoint: `Segment Arc Height`

---

## ⚙️ 기술적 세부사항

### 경로 계산 흐름
1. `SetProjectileInfo()` 호출 → 경로 데이터 초기화
2. `Update()` 매 프레임:
   - 진행률 `t` 계산 (0~1)
   - `CalculatePositionByPathType(t)` → Switch문으로 경로별 계산
   - 위치 업데이트
   - 회전 업데이트
3. 도착 시 `OnHitTarget()` 호출

### 수학 공식

**포물선:**
```
y_offset = 4 * height * t * (1 - t)
```

**베지어 (2차):**
```
P(t) = (1-t)² * P0 + 2(1-t)t * P1 + t² * P2
```

**나선:**
```
angle = t * rotations * 2π
radius = baseRadius * (1 - t * 0.5)
offset = perpendicular * cos(angle) * radius + up * sin(angle) * radius
```

**웨이브:**
```
wave = sin(t * π * frequency) * amplitude
offset = perpendicular * wave
```

---

## ✅ 테스트 체크리스트

### 기본 기능
- [x] Linear: 기존 직선 이동 정상 작동
- [x] ParabolicArc: 포물선 궤적 생성
- [x] BezierCurve: 부드러운 곡선 생성
- [x] MultiPoint: 여러 지점 경유
- [x] DelayedDrop: 상승→정지→낙하 3단계
- [x] Spiral: 회전하면서 전진
- [x] Wave: 좌우 진동

### 추가 기능
- [x] 자동 회전: 투사체가 이동 방향을 바라봄
- [x] 하위 호환성: 기존 코드 정상 작동
- [x] Inspector 편집: Unity에서 파라미터 조정 가능
- [x] 오버로드: 3가지 메서드 제공

---

## 🚀 다음 단계 제안

### 추가 기능 아이디어:
1. **경로 프리뷰**: 경로를 Gizmos로 미리 보기
2. **이징 함수**: EaseIn/EaseOut 속도 곡선
3. **런타임 경로 변경**: 진행 중 경로 타입 변경
4. **충돌 감지**: 경로 중간에 장애물 회피
5. **파티클 동기화**: 경로별 파티클 효과 자동 조정

### 최적화:
1. 경로 계산 캐싱 (같은 경로 재사용)
2. Object Pooling 개선
3. LOD 시스템 (멀리 있는 투사체는 간단한 경로)

### 밸런싱:
1. 유닛별 기본 경로 설정
2. 스킬별 경로 프리셋
3. 경로별 데미지 보정 (포물선은 더 강하게 등)

---

## 📚 참고 문서

- **플래닝**: `Assets/PROJECTILE_PATH_PLANNING.md`
- **사용 가이드**: `Assets/PROJECTILE_USAGE_GUIDE.md`
- **이 문서**: `Assets/PROJECTILE_IMPLEMENTATION_COMPLETE.md`

---

## 🎉 완료!

7가지 투사체 경로 시스템이 성공적으로 구현되었습니다!
모든 경로 타입이 Enum + Switch 방식으로 구현되어 성능과 유지보수성이 우수합니다.

이제 게임에서 다양한 투사체 공격을 구현할 수 있습니다! 🚀

