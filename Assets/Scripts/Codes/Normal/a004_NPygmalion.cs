using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>
    /// 피그말리온의 일반공격: a004-N피그말리온
    /// 자신의 방어력 80%에 해당하는 단일공격
    /// </summary>
    public class a004_NPygmalion : BaseNormalCode
    {
        public a004_NPygmalion(NormalCodeContext context) : base(context)
        {
            CodeName = "a004-N피그말리온";
        }

        /// <summary>
        /// 피그말리온은 방어력의 80%로 데미지 계산
        /// </summary>
        protected override int CalculateDamage(float critMultiplier)
        {
            return (int)(Caster.DefCurr * 0.8f * critMultiplier);
        }
    }
}