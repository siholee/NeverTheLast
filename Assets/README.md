# NTL 스크립트 문서

## Unit

적, 아군 총 24체의 유닛 오브젝트에 할당되는 스크립트.
스탯 등의 프로퍼티는 인스펙터에서 확인할 수 있도록 직렬화 필드를 따로 둠.
```csharp
[SerializeField] private int id;
[SerializeField] private bool isEnemy;
[SerializeField] private string unitName;
[SerializeField] private int level;
public int ID { get => id; protected set => id = value; }
public bool IsEnemy { get => isEnemy; protected set => isEnemy = value; }
public string UnitName { get => unitName; protected set => unitName = value; }
public int Level { get => level; protected set => level = value; }
```
실제로 코드에서 사용하는 수치는 ID로, 직렬화 필드인 id를 사용해 인스펙터에 따로 표시함. 인스펙터에서 수정은 불가능


필드의 24개 셀에는 모두 비활성화 상태의 유닛이 배치되어 있으며 유닛이 소환될 경우 해당 셀의 유닛을 활성화 후 정보를 갱신해 사용함.
유닛이 사망하면 해당 셀의 유닛을 비활성화한 후 2초간 대기함.(즉시 재소환 방지)

### 이벤트
```csharp
private Dictionary<BaseEnums.UnitEventType, Delegate> _eventDict;
```
_eventDict을 사용해 이벤트를 관리함. 이벤트 종류는 BaseEnums.UnitEventType에 정의되어 있으며, 필요한 건 더 추가할 수 있음.

현재 이벤트 종류는 아래와 같음.

```csharp
public enum UnitEventType
{
  OnSpawn, // Unit(자신)
  OnDeath, // Unit, Unit(자신, 공격자)
  OnControlStarts, // Unit, Unit(자신, 공격자)
  OnControlEnds, // Unit(자신)
  OnPassiveActivates, // Unit(자신)
  OnNormalActivates, // Unit(자신)
  OnUltimateActivates, // Unit(자신)
  OnBeforeDamageTaken, // Unit(자신), Unit(공격자)
  OnTakingDamage, // Unit(자신), TakeDamageContext(피해 정보)
  OnAfterDamageTaken, // Unit(자신), Unit(공격자)

  OnStageStart, // Unit(자신)
  OnRoundStart, // Unit(자신)
  OnRoundEnd, // Unit(자신)
  OnStageEnd, // Unit(자신)
}
```
- OnSpawn: 유닛이 스폰될 때 발생하는 이벤트
- OnDeath: 유닛이 죽을 때 발생하는 이벤트
- OnControlStarts: 유닛이 제어당할 때 발생하는 이벤트
- OnControlEnds: 유닛의 제어가 종료될 때 발생하는 이벤트
- OnPassiveActivates: 유닛이 패시브 코드를 시전할 때 발생하는 이벤트
- OnNormalActivates: 유닛이 일반 코드를 시전할 때 발생하는 이벤트
- OnUltimateActivates: 유닛이 궁극기 코드를 시전할 때 발생하는 이벤트
- OnBeforeDamageTaken: 유닛이 피해를 받기 직전에 발생하는 이벤트
- OnTakingDamage: 유닛의 피해를 처리하는 이벤트
- OnAfterDamageTaken: 유닛이 피해를 받은 직후에 발생하는 이벤트
- OnStageStart: 스테이지가 시작될 때 발생하는 이벤트
- OnRoundStart: 라운드가 시작될 때 발생하는 이벤트
- OnRoundEnd: 라운드가 종료될 때 발생하는 이벤트
- OnStageEnd: 스테이지가 종료될 때 발생하는 이벤트


이벤트가 발생하는 시점에 Unit.Invoke를 호출해 처리할 수 있음
```csharp
public virtual void TakeDamage(DamageContext context)
{
    Invoke(BaseEnums.UnitEventType.OnBeforeDamageTaken, (this, context.Attacker));
    Invoke(BaseEnums.UnitEventType.OnTakingDamage, (this, context));
    Invoke(BaseEnums.UnitEventType.OnAfterDamageTaken, (this, context.Attacker));
}
```
피해를 받을 때 _EventDict에 등록된 이벤트(키는 BaseEnums.UnitEventType)를 호출함.

이벤트를 정의할 때 필요한 인수를 튜플로 묶어서 전달함.
```csharp
protected void DefaultTakeDamageEvent((Unit self, DamageContext context) selfContext)
{
    ...
}
```
OnTakingDamage이벤트는 Unit(자신)과 DamageContext(피해 정보 컨텍스트)를 필요로 하므로 `(Unit self, DamageContext context) selfContext`를 인수로 전달
정의된 이벤트에 필요한 인수는 BaseEnum.UnitEventType에 주석으로 정리함. 이건 자동으로 인식하게 못하겠더라

정의한 이벤트를 등록할 때는 아래의 코드와 같이 등록할 수 있음
```csharp
public virtual void InitializeUnit(bool _isEnemy, int _id)
{
    ...
    AddListener<(Unit, DamageContext)>(BaseEnums.UnitEventType.OnTakingDamage, DefaultTakeDamageEvent);
    AddListener<Unit>(BaseEnums.UnitEventType.OnRoundStart, DefaultRoundStartEvent);
    AddListener<Unit>(BaseEnums.UnitEventType.OnRoundEnd, DefaultRoundEndEvent);
}
```

하나의 UnitEventType에 여러 개의 이벤트를 등록할 수 있음.
등록한 이벤트를 제거할 때는 `RemoveListner`를 호출함.

이를 종합해 아래와 같이 이벤트의 등록 및 해제 예약을 할 수 있음

```csharp
public override void CastCode()
{
    ApplyHolyEnchant();
    // 시전자 사망 시 효과를 멈추기 위한 이벤트 핸들러 생성
    Action<(Unit, Unit)> onDeathHandler = null;
    // 익명함수를 변수화해 이벤트 핸들러에서 삭제가 가능하도록 함
    onDeathHandler = (deathInfo) =>
    {
        StopCode();
        // 부여한 상태효과를 해제하고 스스로를 제거해 일회성 처리로 만듬
        Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, onDeathHandler);
    };

    Debug.Log($"{Caster.UnitName}({Caster.currentCell.xPos}, {Caster.currentCell.yPos})이 {CodeName} 시전");
    // 시전자의 사망 이벤트에 핸들러 등록
    Caster.AddListener(BaseEnums.UnitEventType.OnDeath, onDeathHandler);
}
```

상태효과 이벤트는 해제 트리거를 등록하지 않으면 영원히 지속되므로 주의

### 스탯 변동 관리

```csharp
protected virtual void AttributesUpdate()
```
위 메소드에서 변동된 스탯을 관리함. 필요에 따라 이벤트를 추가하거나 해서 자유도를 높일 수 있음.
스탯의 변동이 있을만한 곳에는 꼭 `AttributesUpdate`를 호출할 것.(예: 상태효과 추가, 제거 등)

## 코드
코드는 Passive, Normal, Ultimate의 3종류 클래스로 나눔. (사실 나눌 필요 있는지도 잘 몰?루겠지만 암튼)
각 코드는 Code 클래스를 상속받음.
```csharp
public abstract class Code
{
public BaseEnums.CodeType CodeType;
public string CodeName; // 코드 이름
public Unit Caster; // 시전유닛
public List<Unit> TargetUnits; // 시전대상유닛
public float Cooldown; // 쿨감 임마 쿨감
public float CastingDelay; // 시전시간(동안 쿨안돔)

protected Coroutine CurrSkillCoroutine;

public virtual void CastCode() { }
protected virtual IEnumerator SkillCoroutine() { yield return null; }
public virtual void StopCode() { }
public virtual bool HasValidTarget() { return true; }
}
```
- `CastCode()`

    코드의 시전을 시작하는 메소드로 시전에 필요한 연산 등을 여기서 처리

- `SkillCoroutine()`

    즉발처리가 아닌 지속 처리가 필요한 경우 코루틴을 사용해 처리

- `StopCode()`

    코드의 처리를 종료하는 메소드로 시전이 완료되거나 처리가 중단될 때 호출됨(시전자의 사망 등)

- `HasValidTarget()`

    시전할 수 있는 올바른 대상이 있는지 확인하는 메소드로 여기서 false를 반환하면 시전되지 않음.


## SFX

현시점에는 투사체 애셋만 존재하므로 투사체만 설명

### 투사체

투사체 프리팹을 생성하기 위한 일련의 단계

1. 원하는 투사체 프리팹을 복사 후 Prefabs/SFX에 붙여넣기
![img.png](Docs/1.png)
![img.png](Docs/2.png)

2. 복사한 프리팹의 콜라이더와 리지드바디, 라이트 컴포넌트를 삭제
3. `Hovl Studio/HSFiles/Scripts/HS_ProjectileCustomMover` 컴포넌트를 추가
4. `HS_ProjectileMover` 컴포넌트의 내용물을 보고 적당히 `HS_ProjectileCustomMover`에 오브젝트 할당
![img.png](Docs/3.png)

여기선 Hit, Hit PS, Flash, Projectile PS를 똑같이 맞춰주면 됨(복사하는 투사체 효과마다 Flash가 없는 등 조금씩 다름)

5. `HS_ProjectileMover` 컴포넌트를 삭제
6. 프리팹 하위 파티클의 사이즈 조절(10배 정도로 키우면 잘 보임)
![img.png](Docs/4.png)