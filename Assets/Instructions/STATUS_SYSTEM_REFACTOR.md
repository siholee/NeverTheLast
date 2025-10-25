# 상태(Status) 시스템 개편 완료 보고서

## 📋 작업 개요

기존의 StatusEffect 시스템을 **Status (상태) + Effect (효과)** 구조로 대규모 개편하여 확장성과 재사용성을 크게 향상시켰습니다.

---

## 🎯 주요 변경사항

### **1. 새로운 폴더 구조**

```
Scripts/
├── Effects/                    # 새로 생성 ✨
│   ├── Base/
│   │   ├── BaseEffect.cs      # 효과 기본 추상 클래스
│   │   └── EffectFactory.cs   # 효과 팩토리 (ID 기반 생성)
│   ├── Positive/               # 긍정적 효과 (버프)
│   │   └── DamageReductionEffect.cs
│   ├── Negative/               # 부정적 효과 (디버프)
│   │   ├── DamageOverTimeEffect.cs
│   │   └── PercentDamageOverTimeEffect.cs
│   └── Neutral/                # 중립적 효과
│       ├── DisableNormalAttackEffect.cs
│       ├── TauntEffect.cs
│       └── ThornEffect.cs
│
├── Entities/
│   └── Status/                 # 새로 생성 ✨
│       └── UnitStatus.cs      # 상태 클래스
│
└── StatusEffects/              # 기존 (하위 호환용 유지)
    ├── Base/
    └── Effects/
```

---

## 🆕 새로운 시스템 구조

### **개념 정리**

#### **Status (상태)**
- 유닛에게 적용되는 상태 (예: 맹독, 화상, 핏빛 장미)
- 여러 Effect를 조합하여 구성
- 지속시간, 우선도, 분류 등 메타 정보 보유

#### **Effect (효과)**
- 상태를 구성하는 실제 기능 단위
- 재사용 가능한 모듈형 설계
- ID + 계수 조합으로 다양한 효과 생성

---

## 📊 구현된 Effect 목록

### **Negative (부정적 효과)**

| Effect ID | 이름 | 설명 | 예시 |
|-----------|------|------|------|
| 1001 | DamageOverTimeEffect | 시전자 공격력 기반 DOT | 맹독 (공격력 10%) |
| 1002 | PercentDamageOverTimeEffect | 최대 체력 기반 DOT | 화상 (최대체력 2%) |

### **Neutral (중립적 효과)**

| Effect ID | 이름 | 설명 | 예시 |
|-----------|------|------|------|
| 2001 | DisableNormalAttackEffect | 일반 공격 불가 | 행동불가 |
| 2002 | TauntEffect | 우선도 증가 | 도발 (+1) |
| 2003 | ThornEffect | 접촉 시 화상 부여 | 가시 (반사) |

### **Positive (긍정적 효과)**

| Effect ID | 이름 | 설명 | 예시 |
|-----------|------|------|------|
| 3001 | DamageReductionEffect | 받는 피해 감소 | 피해 감소 (25%) |

---

## 💡 사용 예시

### **예시 1: 맹독 상태 생성 (아탈란테)**

```csharp
// 맹독 상태 생성 (StatusId = 1)
var poisonStatus = new UnitStatus(1, Caster, target);

// DOT 효과 추가 (EffectId = 1001, 공격력의 10%)
poisonStatus.AddEffect(1001, 10f);

// Effect 객체 생성 및 할당
foreach (var effectInstance in poisonStatus.Effects)
{
    effectInstance.EffectObject = EffectFactory.CreateEffect(
        effectInstance.EffectId,
        effectInstance.Coefficient,
        Caster,
        target
    );
}

// 상태 적용
target.AddStatus(poisonStatus);
```

### **예시 2: 핏빛 장미 상태 (피그말리온 예정)**

```csharp
// 핏빛 장미 상태 생성
var bloodyRoseStatus = new UnitStatus(3, Caster, Caster);

// 효과 추가
bloodyRoseStatus.AddEffect(2001, 100f);   // 행동불가
bloodyRoseStatus.AddEffect(2002, 1f);     // 도발 +1
bloodyRoseStatus.AddEffect(3001, 25f);    // 받는 피해 25% 감소
bloodyRoseStatus.AddEffect(2003, 100f);   // 가시 (접촉 시 화상)

// Effect 객체 생성...
// 상태 적용
Caster.AddStatus(bloodyRoseStatus);
```

---

## 🔧 Unit.cs 업데이트

### **새로운 메서드들**

```csharp
// 상태 관리
public void AddStatus(UnitStatus status)       // 상태 추가
public void RemoveStatus(int statusId)         // 상태 제거
public bool HasStatus(int statusId)            // 상태 보유 확인
public UnitStatus GetStatus(int statusId)      // 상태 가져오기
public Dictionary<int, UnitStatus> GetStatuses() // 모든 상태 (UI용)
```

### **하위 호환성**

기존 StatusEffect 시스템도 계속 작동합니다:
```csharp
// 기존 방식 (여전히 작동)
unit.AddStatusEffect(identifier, statusEffect);

// 새로운 방식
unit.AddStatus(unitStatus);
```

---

## ✅ 변경된 파일 목록

### **새로 생성된 파일**
- `Effects/Base/BaseEffect.cs`
- `Effects/Base/EffectFactory.cs`
- `Effects/Negative/DamageOverTimeEffect.cs`
- `Effects/Negative/PercentDamageOverTimeEffect.cs`
- `Effects/Neutral/DisableNormalAttackEffect.cs`
- `Effects/Neutral/TauntEffect.cs`
- `Effects/Neutral/ThornEffect.cs`
- `Effects/Positive/DamageReductionEffect.cs`
- `Entities/Status/UnitStatus.cs`

### **수정된 파일**
- `BaseClasses/Enums.cs` - EffectCategory, StatusCategory enum 추가
- `Entities/Unit.cs` - 새로운 Status 관리 시스템 추가
- `Codes/Normal/a005_NAtlanta.cs` - 새 시스템으로 변경
- `Codes/Ultimate/a005_U_Moonfall.cs` - 새 시스템으로 변경

### **제거된 파일/폴더**
- `Scripts/Skills/` - 빈 폴더 제거 ✅

### **보존된 파일 (하위 호환)**
- `StatusEffects/` - 기존 시스템 유지 (테스트 코드용)
- `StatusEffects/Effects/ShieldEffect.cs` - Nishakara, 테스트에서 사용 중

---

## 🎯 장점 및 효과

### **1. 확장성**
- 새로운 상태를 Effect 조합으로 쉽게 생성
- 복잡한 상태도 모듈 조합으로 구현 가능

### **2. 재사용성**
- Effect를 여러 상태에서 공유 가능
- 중복 코드 제거

### **3. 데이터 기반 설계**
- ID + 계수 방식으로 밸런스 조정 용이
- JSON/ScriptableObject로 데이터화 가능

### **4. 유지보수성**
- 명확한 책임 분리 (Status vs Effect)
- 디버깅 및 추적 용이

### **5. 하위 호환성**
- 기존 StatusEffect 시스템 유지
- 점진적 마이그레이션 가능

---

## 🚀 다음 단계

### **즉시 필요한 작업**
1. ✅ 아탈란테 코드 변경 완료
2. ⏳ InfoTab UI 업데이트 (Status 시스템 표시)
3. ⏳ 기존 테스트 코드 검증

### **향후 작업**
1. 나머지 캐릭터들도 새 시스템으로 마이그레이션
2. Status 데이터를 JSON/ScriptableObject로 외부화
3. 복잡한 상태 구현 (피그말리온의 핏빛 장미 등)
4. Effect 추가 (스턴, 침묵, 공격력 증가 등)

---

## 📝 테스트 권장사항

1. **아탈란테 테스트**
   - 일반 공격 시 맹독 적용 확인
   - Moonfall 궁극기 맹독 적용 확인
   - 맹독 지속시간 및 피해량 확인

2. **Status 중첩 테스트**
   - 동일 상태 중복 적용 시 동작 확인
   - 여러 상태 동시 보유 확인

3. **Effect 동작 확인**
   - DOT 피해 정상 적용
   - 지속시간 만료 시 자동 제거

---

## 🎉 결론

기존의 하드코딩된 StatusEffect 시스템을 **모듈형 Status + Effect 시스템**으로 성공적으로 개편했습니다. 

이를 통해:
- ✅ 확장성과 재사용성이 크게 향상
- ✅ 복잡한 상태 조합 구현 가능
- ✅ 데이터 기반 밸런스 조정 용이
- ✅ 하위 호환성 유지

향후 모든 캐릭터와 상태를 새로운 시스템으로 점진적으로 마이그레이션할 수 있습니다! 🚀
