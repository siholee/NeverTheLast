using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Effects.Base;
using Entities;
using UnityEngine;

namespace Entities.Status
{
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
        
        /// <summary>상태 시전자</summary>
        public Unit Caster { get; set; }
        
        /// <summary>상태 보유자</summary>
        public Unit Owner { get; set; }
        
        /// <summary>지속 시간 (초 단위, -1이면 무한)</summary>
        public float Duration { get; set; }
        
        /// <summary>경과 시간 (초 단위)</summary>
        public float ElapsedTime { get; set; }
        
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
            ElapsedTime = 0f;
            Effects = new List<EffectInstance>();
            
            // 데이터에서 상태 정보 로드 (추후 구현)
            LoadStatusData(statusId);
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
                    Duration = 3f;
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
                    Duration = 2f;
                    break;
                    
                case 3: // 사냥꾼의 독 (아탈란테)
                    StatusName = "사냥꾼의 독";
                    StatusDescription = "아탈란테의 공격력에 비례한 지속 피해를 입힙니다.";
                    IconPath = "Icons/Status/HuntersVenom";
                    Priority = 0;
                    Category = BaseEnums.StatusCategory.Negative;
                    StackPolicy = BaseEnums.StatusStackPolicy.Stack; // 중첩 허용
                    CanStack = true; // 복수 보유 가능
                    Duration = 3f;
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
                    Duration = 8f;
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
                    Duration = 1f;
                    break;
            }
        }
        
        /// <summary>
        /// 효과 추가
        /// </summary>
        public void AddEffect(int effectId, float coefficient = 100f)
        {
            var effectInstance = new EffectInstance(effectId, coefficient);
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
            
            Debug.Log($"[상태] {Owner.UnitName}에게 '{StatusName}' 상태 적용 (지속시간: {Duration}초, 효과 수: {Effects.Count})");
        }
        
        /// <summary>
        /// 매 라운드 시작 시 호출
        /// </summary>
        public void OnRoundStart()
        {
            foreach (var effectInstance in Effects)
            {
                if (effectInstance.EffectObject != null)
                {
                    effectInstance.EffectObject.OnRoundStart();
                }
            }
        }
        
        /// <summary>
        /// 매 프레임 업데이트
        /// </summary>
        public void OnUpdate(float deltaTime)
        {
            ElapsedTime += deltaTime;
            
            foreach (var effectInstance in Effects)
            {
                if (effectInstance.EffectObject != null)
                {
                    effectInstance.EffectObject.OnUpdate(deltaTime);
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
        /// 지속 시간 만료 여부
        /// </summary>
        public bool IsExpired()
        {
            return Duration > 0 && ElapsedTime >= Duration;
        }
    }
}
