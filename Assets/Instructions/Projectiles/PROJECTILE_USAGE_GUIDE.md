# 투사체 경로 시스템 사용 가이드

## 📚 개요
7가지 투사체 경로 타입을 지원하는 시스템입니다.

## 🎯 지원하는 경로 타입

1. **Linear** - 직선
2. **ParabolicArc** - 포물선
3. **BezierCurve** - 베지어 곡선
4. **MultiPoint** - 다중 경유
5. **DelayedDrop** - 지연 낙하
6. **Spiral** - 나선형
7. **Wave** - 웨이브

---

## 💻 사용 방법

### 1. 기본 사용법 (직선)

```csharp
// 기존 코드와 동일 - 자동으로 직선 경로 사용
sfxManager.FireSingleProjectile(arrowPrefab, archerUnit, enemyUnit, 0.5f);
```

### 2. 포물선 경로

```csharp
// 간단한 방법
sfxManager.FireSingleProjectile(
    rockPrefab, 
    catapultUnit, 
    targetUnit, 
    2.0f, 
    ProjectilePathType.ParabolicArc
);

// 높이 지정
sfxManager.FireSingleProjectile(
    rockPrefab, 
    catapultUnit, 
    targetUnit, 
    2.0f, 
    ProjectilePathType.ParabolicArc,
    ProjectilePathData.CreateParabolic(5f)  // 높이 5
);
```

### 3. 베지어 곡선

```csharp
// 자동 제어점 (중간 지점에서 위로 4만큼)
sfxManager.FireSingleProjectile(
    magicBoltPrefab, 
    wizardUnit, 
    targetUnit, 
    1.5f, 
    ProjectilePathType.BezierCurve,
    ProjectilePathData.CreateBezier(4f)
);

// 수동 제어점 지정
var pathData = new ProjectilePathData 
{ 
    bezierControlPoint = new Vector3(10, 5, 10) 
};
sfxManager.FireSingleProjectile(
    magicBoltPrefab, 
    wizardUnit, 
    targetUnit, 
    1.5f, 
    ProjectilePathType.BezierCurve,
    pathData
);
```

### 4. 다중 경유 (연쇄 공격)

```csharp
// 여러 적을 경유하는 번개
Vector3[] lightningPath = new Vector3[] 
{ 
    casterUnit.transform.position, 
    enemy1.transform.position, 
    enemy2.transform.position, 
    enemy3.transform.position 
};

sfxManager.FireSingleProjectile(
    lightningPrefab, 
    casterUnit, 
    enemy3,  // 마지막 타겟
    2.5f, 
    ProjectilePathType.MultiPoint,
    ProjectilePathData.CreateMultiPoint(lightningPath, 2f)  // 세그먼트 아크 높이 2
);
```

### 5. 지연 낙하 (폭격)

```csharp
// 하늘에서 떨어지는 유성
sfxManager.FireSingleProjectile(
    meteorPrefab, 
    skyUnit,  // 높은 위치의 유닛
    targetUnit, 
    2.0f, 
    ProjectilePathType.DelayedDrop,
    ProjectilePathData.CreateDelayedDrop(15f, 0.2f)  // 높이 15, 정지 시간 0.2
);
```

### 6. 나선형 (드릴 공격)

```csharp
// 회전하면서 전진하는 투사체
sfxManager.FireSingleProjectile(
    drillPrefab, 
    attackerUnit, 
    targetUnit, 
    1.5f, 
    ProjectilePathType.Spiral,
    ProjectilePathData.CreateSpiral(1f, 2f, 3f)  // 반지름 1, 속도 2, 회전수 3
);
```

### 7. 웨이브 (물결 공격)

```csharp
// 물결치듯 움직이는 투사체
sfxManager.FireSingleProjectile(
    waterBoltPrefab, 
    mageUnit, 
    targetUnit, 
    1.5f, 
    ProjectilePathType.Wave,
    ProjectilePathData.CreateWave(1f, 2f)  // 진폭 1, 주파수 2
);
```

---

## 🎮 실전 예시

### 스킬 시스템에서 사용

```csharp
public class ArcherSkill : Skill
{
    public override void Execute(Unit caster, Unit target)
    {
        // 일반 공격 - 직선
        sfxManager.FireSingleProjectile(
            arrowPrefab, caster, target, 0.5f
        );
    }
}

public class CatapultSkill : Skill
{
    public override void Execute(Unit caster, Unit target)
    {
        // 투석기 - 포물선
        sfxManager.FireSingleProjectile(
            rockPrefab, 
            caster, 
            target, 
            2.0f, 
            ProjectilePathType.ParabolicArc,
            ProjectilePathData.CreateParabolic(6f)
        );
    }
}

public class ChainLightningSkill : Skill
{
    public override void Execute(Unit caster, List<Unit> targets)
    {
        // 연쇄 번개
        Vector3[] path = new Vector3[targets.Count + 1];
        path[0] = caster.transform.position;
        for (int i = 0; i < targets.Count; i++)
        {
            path[i + 1] = targets[i].transform.position;
        }
        
        sfxManager.FireSingleProjectile(
            lightningPrefab, 
            caster, 
            targets[targets.Count - 1], 
            2.0f, 
            ProjectilePathType.MultiPoint,
            ProjectilePathData.CreateMultiPoint(path)
        );
    }
}
```

### 유닛별 투사체 설정

```csharp
public enum UnitType
{
    Archer,
    Catapult,
    Wizard,
    ArtilleryTower
}

public ProjectileConfig GetProjectileConfig(UnitType unitType)
{
    switch (unitType)
    {
        case UnitType.Archer:
            return new ProjectileConfig
            {
                pathType = ProjectilePathType.Linear,
                duration = 0.5f
            };
            
        case UnitType.Catapult:
            return new ProjectileConfig
            {
                pathType = ProjectilePathType.ParabolicArc,
                duration = 2.0f,
                pathData = ProjectilePathData.CreateParabolic(5f)
            };
            
        case UnitType.Wizard:
            return new ProjectileConfig
            {
                pathType = ProjectilePathType.BezierCurve,
                duration = 1.5f,
                pathData = ProjectilePathData.CreateBezier(4f)
            };
            
        case UnitType.ArtilleryTower:
            return new ProjectileConfig
            {
                pathType = ProjectilePathType.DelayedDrop,
                duration = 2.5f,
                pathData = ProjectilePathData.CreateDelayedDrop(12f, 0.15f)
            };
            
        default:
            return new ProjectileConfig { pathType = ProjectilePathType.Linear };
    }
}
```

---

## 🔧 파라미터 조정 가이드

### 포물선 높이 (arcHeight)
- **1~3**: 낮은 궤적 (화살, 작은 돌)
- **4~6**: 중간 궤적 (투석기)
- **7~12**: 높은 궤적 (대포, 공성무기)

### 베지어 오프셋 (arcOffset)
- **2~4**: 부드러운 곡선 (마법)
- **5~8**: 극적인 곡선 (특수 스킬)

### 나선 파라미터
- **반지름 (0.5~2)**: 회전 범위
- **속도 (1~3)**: 회전 빠르기
- **회전수 (2~5)**: 총 회전 횟수

### 웨이브 파라미터
- **진폭 (0.5~2)**: 좌우 흔들림 크기
- **주파수 (1~3)**: 흔들림 빠르기

---

## ⚠️ 주의사항

1. **성능**: 동시에 많은 투사체 사용 시 Linear 사용 권장
2. **MultiPoint**: waypoints 배열을 반드시 제공해야 함
3. **DelayedDrop**: hangTime은 0~0.5 사이 권장
4. **기존 코드 호환**: 기존 FireSingleProjectile 호출은 그대로 작동 (Linear 사용)

---

## 🎨 비주얼 팁

### 경로별 추천 이펙트
- **Linear**: 빠른 파티클, 트레일 얇게
- **ParabolicArc**: 떨어지는 먼지, 그림자 추가
- **BezierCurve**: 화려한 파티클, 반짝임
- **Spiral**: 회전 파티클, 드릴 효과
- **Wave**: 물결 텍스처, 투명도 변화

---

## 📝 테스트 체크리스트

- [ ] Linear: 기존 화살이 정상 작동하는지 확인
- [ ] ParabolicArc: 투석기가 포물선을 그리는지 확인
- [ ] BezierCurve: 마법이 부드러운 곡선으로 날아가는지 확인
- [ ] MultiPoint: 번개가 여러 적을 경유하는지 확인
- [ ] DelayedDrop: 유성이 잠시 멈췄다 떨어지는지 확인
- [ ] Spiral: 투사체가 회전하면서 전진하는지 확인
- [ ] Wave: 투사체가 좌우로 흔들리는지 확인

---

더 자세한 내용은 `PROJECTILE_PATH_PLANNING.md`를 참고하세요.

