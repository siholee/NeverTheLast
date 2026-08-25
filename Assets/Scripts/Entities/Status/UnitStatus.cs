using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Effects.Base;
using Entities;
using UnityEngine;

namespace Entities.Status
{
    /// <summary>
    /// 코드에서 직접 정의하는 상태 정의.
    /// LoadStatusData의 ID 스위치에 등록하지 않고도 상태를 만들 수 있다 (버프류에 사용).
    /// </summary>
    public class StatusDefinition
    {
        public int Id;
        /// <summary>중복/중첩 판정용 고유 키. null이면 Id 문자열을 사용한다.
        /// 시전자별 중첩이 필요하면 키에 시전자 ID를 포함시킨다.</summary>
        public string Key;
        public string Name;
        public string Description = "";
        public string IconPath = "";
        public BaseEnums.StatusCategory Category = BaseEnums.StatusCategory.Neutral;
        public BaseEnums.StatusStackPolicy StackPolicy = BaseEnums.StatusStackPolicy.Replace;
        /// <summary>지속 <b>턴</b> 수. 0 이하 = 무한(라운드 종료 시 정리).
        /// 보유자의 턴이 시작될 때마다 1씩 소모된다.</summary>
        public int Duration = -1;
        /// <summary>이로운 상태 여부 (OnBeneficialEffectReceived 발행 판정)</summary>
        public bool IsBeneficial;
    }

    /// <summary>
    /// 상태(Status) 클래스
    /// 유닛에게 적용되는 상태를 표현하며, 여러 효과(Effect)를 포함할 수 있습니다.
    /// </summary>
    public class UnitStatus
    {
        /// <summary>상태 ID</summary>
        public int StatusId { get; private set; }
        
        /// <summary>상태 이름</summary>
        public string StatusName { get; private set; }
        
        /// <summary>상태 설명</summary>
        public string StatusDescription { get; private set; }
        
        /// <summary>상태 아이콘 경로</summary>
        public string IconPath { get; private set; }
        
        /// <summary>상태 우선도 (높을수록 먼저 처리)</summary>
        public int Priority { get; private set; }
        
        /// <summary>상태 분류 (긍정적/부정적/중립적)</summary>
        public BaseEnums.StatusCategory Category { get; private set; }
        
        /// <summary>상태 중첩 정책</summary>
        public BaseEnums.StatusStackPolicy StackPolicy { get; private set; }
        
        /// <summary>복수 보유 가능 여부 (같은 StatusId를 여러 개 가질 수 있는지)</summary>
        public bool CanStack { get; private set; }

        /// <summary>중복/중첩 판정용 고유 키 (기본값: StatusId 문자열)</summary>
        public string Key { get; private set; }

        /// <summary>이로운 상태 여부 (부여 시 OnBeneficialEffectReceived 이벤트 발행)</summary>
        public bool IsBeneficial { get; private set; }
        
        /// <summary>상태 시전자</summary>
        public Unit Caster { get; set; }
        
        /// <summary>상태 보유자</summary>
        public Unit Owner { get; set; }
        
        /// <summary>지속 턴 수 (-1이면 무한)</summary>
        public int Duration { get; set; }
        
        /// <summary>보유자의 턴을 몇 번 넘겼는지</summary>
        public int ElapsedTurns { get; set; }

        /// <summary>남은 턴 수. 무한이면 <see cref="int.MaxValue"/>.</summary>
        public int RemainingTurns => Duration <= 0 ? int.MaxValue : Mathf.Max(0, Duration - ElapsedTurns);
        
        /// <summary>효과 목록 (EffectId%계수 형식)</summary>
        public List<EffectInstance> Effects { get; private set; }
        
        /// <summary>
        /// 효과 인스턴스 (ID + 계수)
        /// </summary>
        public class EffectInstance
        {
            public int EffectId { get; set; }
            public float Coefficient { get; set; }
            public BaseEffect EffectObject { get; set; }
            
            public EffectInstance(int effectId, float coefficient)
            {
                EffectId = effectId;
                Coefficient = coefficient;
            }
        }
        
        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="statusId">상태 ID</param>
        /// <param name="caster">시전자</param>
        /// <param name="owner">보유자</param>
        public UnitStatus(int statusId, Unit caster, Unit owner)
        {
            StatusId = statusId;
            Caster = caster;
            Owner = owner;
            ElapsedTurns = 0;
            Effects = new List<EffectInstance>();

            LoadStatusData(statusId);
            Key = StatusId.ToString();
        }

        /// <summary>
        /// StatusDefinition 기반 생성자 — 코드에서 직접 정의한 상태(버프류)에 사용.
        /// </summary>
        public UnitStatus(StatusDefinition definition, Unit caster, Unit owner)
        {
            StatusId = definition.Id;
            Caster = caster;
            Owner = owner;
            ElapsedTurns = 0;
            Effects = new List<EffectInstance>();

            StatusName = definition.Name;
            StatusDescription = definition.Description;
            IconPath = definition.IconPath;
            Priority = 0;
            Category = definition.Category;
            StackPolicy = definition.StackPolicy;
            CanStack = definition.StackPolicy == BaseEnums.StatusStackPolicy.Stack;
            Duration = definition.Duration;
            Key = string.IsNullOrEmpty(definition.Key) ? definition.Id.ToString() : definition.Key;
            IsBeneficial = definition.IsBeneficial;
        }
        
        /// <summary>
        /// 상태 데이터 로드 (JSON 또는 ScriptableObject에서)
        /// </summary>
        private void LoadStatusData(int statusId)
        {
            // TODO: 데이터 파일에서 로드
            // 임시로 하드코딩
            switch (statusId)
            {
                case 1: // 맹독
                    StatusName = "맹독";
                    StatusDescription = "시전자의 공격력에 비례한 지속 피해를 입힙니다.";
                    IconPath = "Icons/Status/Poison";
                    Priority = 0;
                    Category = BaseEnums.StatusCategory.Negative;
                    StackPolicy = BaseEnums.StatusStackPolicy.Stack; // 중첩 허용
                    CanStack = true; // 복수 보유 가능
                    Duration = 2;   // 3초 → 2턴
                    // Effects는 외부에서 추가
                    break;
                    
                case 2: // 화상
                    StatusName = "화상";
                    StatusDescription = "최대 체력의 2%에 해당하는 지속 피해를 입힙니다.";
                    IconPath = "Icons/Status/Burn";
                    Priority = 0;
                    Category = BaseEnums.StatusCategory.Negative;
                    StackPolicy = BaseEnums.StatusStackPolicy.ExtendDuration; // 지속시간 연장
                    CanStack = false; // 복수 보유 불가
                    Duration = 1;   // 2초 → 1턴
                    break;
                    
                case 3: // 사냥꾼의 독 (아탈란테)
                    StatusName = "사냥꾼의 독";
                    StatusDescription = "아탈란테의 공격력에 비례한 지속 피해를 입힙니다.";
                    IconPath = "Icons/Status/HuntersVenom";
                    Priority = 0;
                    Category = BaseEnums.StatusCategory.Negative;
                    StackPolicy = BaseEnums.StatusStackPolicy.Stack; // 중첩 허용
                    CanStack = true; // 복수 보유 가능
                    Duration = 2;   // 3초 → 2턴
                    // Effects는 외부에서 추가 (EffectId=1001, DOT)
                    break;
                    
                case 4: // 핏빛 장미 (피그말리온)
                    StatusName = "핏빛 장미";
                    StatusDescription = "행동불가 상태가 되며 적의 타겟팅 우선순위가 증가합니다. 접촉 피해를 입힐 시 화상을 부여합니다.";
                    IconPath = "Icons/Status/BloodyRose";
                    Priority = 10;
                    Category = BaseEnums.StatusCategory.Neutral;
                    StackPolicy = BaseEnums.StatusStackPolicy.Ignore; // 중복 무시
                    CanStack = false; // 복수 보유 불가
                    Duration = 4;   // 8초 → 4턴
                    // Effects는 외부에서 추가
                    break;
                    
                default:
                    Debug.LogWarning($"정의되지 않은 상태 ID: {statusId}");
                    StatusName = "알 수 없는 상태";
                    StatusDescription = "";
                    IconPath = "";
                    Priority = 0;
                    Category = BaseEnums.StatusCategory.Neutral;
                    StackPolicy = BaseEnums.StatusStackPolicy.Stack;
                    CanStack = false;
                    Duration = 1;   // 1초 → 1턴
                    break;
            }
        }
        
        /// <summary>
        /// 효과 추가 (EffectFactory ID 기반 — Unit.AddStatusInternal에서 객체 생성)
        /// </summary>
        public void AddEffect(int effectId, float coefficient = 100f)
        {
            var effectInstance = new EffectInstance(effectId, coefficient);
            Effects.Add(effectInstance);
        }

        /// <summary>
        /// 효과 추가 (직접 생성한 BaseEffect 객체 — 생성자 인자가 필요한 버프류에 사용)
        /// </summary>
        public void AddEffect(BaseEffect effect)
        {
            if (effect == null) return;
            var effectInstance = new EffectInstance(effect.EffectId, effect.Coefficient)
            {
                EffectObject = effect,
            };
            Effects.Add(effectInstance);
        }
        
        /// <summary>
        /// 상태 적용 시 호출
        /// </summary>
        public void OnApply()
        {
            foreach (var effectInstance in Effects)
            {
                if (effectInstance.EffectObject != null)
                {
                    effectInstance.EffectObject.OnApply();
                }
            }
            
            Debug.Log($"[상태] {Owner.UnitName}에게 '{StatusName}' 상태 적용 (지속: {Duration}턴, 효과 수: {Effects.Count})");
        }
        
        /// <summary>
        /// 보유자의 턴이 시작될 때 한 번 호출된다. 전투가 턴제이므로 프레임 틱은 쓰지 않는다.
        /// </summary>
        public void OnOwnerTurn()
        {
            ElapsedTurns++;

            foreach (var effectInstance in Effects)
            {
                if (effectInstance.EffectObject != null)
                {
                    effectInstance.EffectObject.OnOwnerTurn();
                }
            }
        }
        
        /// <summary>
        /// 상태 제거 시 호출
        /// </summary>
        public void OnRemove()
        {
            foreach (var effectInstance in Effects)
            {
                if (effectInstance.EffectObject != null)
                {
                    effectInstance.EffectObject.OnRemove();
                }
            }
            
            Debug.Log($"[상태] {Owner.UnitName}에게서 '{StatusName}' 상태 제거");
        }
        
        /// <summary>
        /// 지속 턴 만료 여부
        /// </summary>
        public bool IsExpired()
        {
            return Duration > 0 && ElapsedTurns >= Duration;
        }
    }
}
