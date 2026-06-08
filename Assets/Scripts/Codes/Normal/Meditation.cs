using System.Collections;
using BaseClasses;
using Codes.Base;
using Managers;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>
    /// 명상 (주술사/캐스터 클래스 스킬 — class_skill 슬롯)<br/>
    /// 집중하여 SP +2 획득, 마나 +30 회복. 피해 없음.<br/>
    /// 턴 소모 O, SP 비용 0 — 일반 기본 공격(SP+1) 대신 마나 회복에 집중할 때 선택.
    /// </summary>
    public class Meditation : NormalCode
    {
        private const int SpGain   = 2;   // 기본 공격의 2배 SP 생성
        private const int ManaGain = 30;  // 마나 회복량

        public Meditation(NormalCodeContext context) : base(context)
        {
            CodeType      = BaseEnums.CodeType.Normal;
            CodeName      = "명상";
            Caster        = context.Caster;
            CastingDelay  = 0.8f;
            ManaAmount    = ManaGain;
            Element       = BaseEnums.ElementType.Physical;
            SkillCategory = BaseEnums.SkillCategory.Basic; // SP 생성형
            SpCost        = 0;
            TargetType    = BaseEnums.TargetType.Self;
        }

        public override void CastCode()
        {
            Caster.isCasting = true;
            Debug.Log($"{Caster.UnitName}이 {CodeName} 시전");
            CurrSkillCoroutine = Caster.StartCoroutine(SkillCoroutine());
        }

        protected override IEnumerator SkillCoroutine()
        {
            if (CastingDelay > 0f)
                yield return new WaitForSeconds(CastingDelay);

            // SP +2 생성 (일반 기본 공격의 2배)
            GameManager.Instance.spManager.Generate(SpGain);

            // 마나 회복
            Caster.RecoverMana(ManaGain);

            Debug.Log($"[명상] {Caster.UnitName}: SP +{SpGain}, 마나 +{ManaGain}");
            StopCode();
        }

        public override void StopCode() => Caster.isCasting = false;

        // Self 타겟 — 항상 유효
        public override bool HasValidTarget() => true;
    }
}
