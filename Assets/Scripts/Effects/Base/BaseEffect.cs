using BaseClasses;
using Entities;
using UnityEngine;

namespace Effects.Base
{
    /// <summary>
    /// 효과의 기본 추상 클래스
    /// 모든 효과(Effect)는 이 클래스를 상속받아야 합니다.
    /// </summary>
    public abstract class BaseEffect
    {
        /// <summary>효과 ID</summary>
        public int EffectId { get; protected set; }
        
        /// <summary>효과 이름</summary>
        public string EffectName { get; protected set; }
        
        /// <summary>효과 설명</summary>
        public string EffectDescription { get; protected set; }
        
        /// <summary>효과 분류 (긍정적/부정적/중립적)</summary>
        public BaseEnums.EffectCategory Category { get; protected set; }
        
        /// <summary>효과 계수 (예: 공격력의 20% DOT라면 계수는 20)</summary>
        public float Coefficient { get; set; }
        
        /// <summary>효과의 시전자</summary>
        public Unit Caster { get; set; }
        
        /// <summary>효과의 대상</summary>
        public Unit Target { get; set; }
        
        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="effectId">효과 ID</param>
        /// <param name="coefficient">효과 계수 (기본값 100 = 100%)</param>
        protected BaseEffect(int effectId, float coefficient = 100f)
        {
            EffectId = effectId;
            Coefficient = coefficient;
        }
        
        /// <summary>
        /// 효과 적용 시 호출 (초기화)
        /// </summary>
        public abstract void OnApply();
        
        /// <summary>
        /// 매 라운드 시작 시 호출
        /// </summary>
        public abstract void OnRoundStart();
        
        /// <summary>
        /// 매 프레임 업데이트 시 호출 (DoT 등)
        /// </summary>
        /// <param name="deltaTime">프레임 델타 타임</param>
        public abstract void OnUpdate(float deltaTime);
        
        /// <summary>
        /// 효과 제거 시 호출
        /// </summary>
        public abstract void OnRemove();
        
        /// <summary>
        /// 효과 복제 (상태 중첩 등에 사용)
        /// </summary>
        public abstract BaseEffect Clone();
    }
}
