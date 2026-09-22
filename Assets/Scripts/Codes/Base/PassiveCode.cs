using BaseClasses;

namespace Codes.Base
{
  /// <summary>반격 태그를 가진 행동을 제공하는 패시브. 자동 지정 우선순위 판정에 사용한다.</summary>
  public interface ICounterAttackProvider { }

  public class PassiveCode : Code
  {
    /// <summary>
    /// 유닛 정의의 고유 패시브인지 여부. 고유 패시브는 코드 용량을 차지하지 않으며
    /// 원본 그대로 전수되지 않는다. 열화 전수본은 별도의 일반 PassiveCode로 만든다.
    /// </summary>
    private bool _isUniquePassive;

    /// <summary>
    /// 고유 패시브인가. 참으로 두면 등급이 <see cref="BaseEnums.CodeGrade.Unique"/>로 바뀌고
    /// 상위 코드 지정이 지워진다 — 고유 패시브는 은·금 사다리에 끼지 않는다.
    /// </summary>
    public bool IsUniquePassive
    {
      get => _isUniquePassive;
      protected set
      {
        _isUniquePassive = value;
        if (!value) return;
        Grade = BaseEnums.CodeGrade.Unique;
        SupersededByCodeId = 0;
      }
    }

    /// <summary>
    /// INT가 만드는 코드 용량을 차지하지 않는 패시브인지.
    /// 고유 패시브 외에, 진영 전체에 하드코딩으로 배포되는 코드(그리스 팔랑크스)가 여기에 해당한다.
    /// </summary>
    public bool IgnoresCodeCapacity { get; protected set; }

    /// <summary>
    /// 이 코드를 대체하는 <b>강화 등급</b> 코드의 ID. 0이면 대체 관계가 없다.
    ///
    /// 서포터 전수로 같은 계열의 두 등급을 함께 들 수 있으므로, 보유자가 이 ID의 코드를
    /// 배웠으면 <see cref="Entities.Unit"/>이 이 코드를 아예 발동하지 않는다.
    /// 효과마다 상대 상태를 조회해 스스로 눌리던 방식을 한 줄 선언으로 바꾼 것이다.
    /// </summary>
    public int SupersededByCodeId { get; protected set; }

    /// <summary>
    /// 전투가 시작되면 이 패시브가 최대 체력에 곱할 배율. 표시 전용이다.
    /// 패시브는 라운드 시작에 걸리므로, 준비 화면이 보여 주는 체력은 이 배율을 모른다.
    /// <see cref="Entities.Unit.ProjectedHpMax"/>가 이 값으로 "전투에서 실제로 마주할 체력"을 미리 계산한다.
    /// </summary>
    public virtual float PreviewMaxHpMultiplier => 1f;

    /// <summary>
    /// 서포트 카드로 훈련에 앉았을 때 <paramref name="stat"/> 상승량에 더하는 효율(0.10 = +10%).
    /// <b>어느 훈련에 앉든</b> 그 훈련이 올리는 해당 스탯 몫에 붙는다 — 부 스탯 몫도 포함한다.
    /// 전투 효과가 없는 훈련 코드가 여기만 덮는다. <see cref="Managers.TrainingManager"/>가 읽는다.
    /// </summary>
    public virtual float SupportTrainingBonus(BaseEnums.PrimaryStat stat) => 0f;

    /// <summary>
    /// 서포트 카드로 앉아 있는 훈련의 체력 소모 배율. 0.8이면 체력 소모 -20%다.
    /// 회복형 훈련과 실패 추가 소모에는 적용하지 않는다.
    /// </summary>
    public virtual float SupportTrainingEnergyCostMultiplier => 1f;

    /// <summary>
    /// 서포트 카드로 앉아 있는 훈련의 실패율 배율. 0.75면 표시·실제 실패율이 25% 감소한다.
    /// 해당 훈련에 배치되지 않은 서포트의 효과는 적용하지 않는다.
    /// </summary>
    public virtual float SupportTrainingFailureRateMultiplier => 1f;

    /// <summary>메인 본인이 훈련받을 때 <paramref name="stat"/> 상승량에 더하는 효율. 직감·대도·광신도.</summary>
    public virtual float MainTrainingBonus(BaseEnums.PrimaryStat stat) => 0f;

    public PassiveCode(PassiveCodeContext context)
    {
      Caster = context.Caster;
      ActivationType = BaseEnums.CodeActivationType.Passive;
    }
  }

  public abstract class UniquePassiveCode : PassiveCode
  {
    protected UniquePassiveCode(PassiveCodeContext context) : base(context)
    {
      // 고유 패시브는 어떤 경로로도 전수되지 않는다. 열화 전수본 제도는 폐지했다.
      IsUniquePassive = true;
      Transferable = false;
    }
  }
}
