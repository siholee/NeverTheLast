using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using UnityEngine;

namespace Codes.Normal
{
    public enum EnemyArchetypeNormalStyle
    {
        Sentinel,
        Fighter,
        Executioner,
        Marksman,
        Mage,
        Tactician,
        Mechanic,
        Support,
    }

    /// <summary>
    /// 일반 등급 적이 테마와 관계없이 병종별로 공유하는 기본 공격.
    /// 타겟 변경이나 상태이상 없이 피해 계수와 접촉 유형만 구분한다.
    /// </summary>
    public sealed class EnemyArchetypeNormal : BaseNormalCode
    {
        private readonly EnemyArchetypeNormalStyle _style;

        public EnemyArchetypeNormal(NormalCodeContext context, EnemyArchetypeNormalStyle style) : base(context)
        {
            _style = style;
            CodeName = style switch
            {
                EnemyArchetypeNormalStyle.Sentinel => "파수꾼의 타격",
                EnemyArchetypeNormalStyle.Fighter => "투사의 공격",
                EnemyArchetypeNormalStyle.Executioner => "처형자의 일격",
                EnemyArchetypeNormalStyle.Marksman => "사수의 사격",
                EnemyArchetypeNormalStyle.Mage => "마법사의 마력탄",
                EnemyArchetypeNormalStyle.Tactician => "책략가의 견제",
                EnemyArchetypeNormalStyle.Mechanic => "메카닉의 투척",
                EnemyArchetypeNormalStyle.Support => "지원가의 원호",
                _ => "적의 공격",
            };
            CastingDelay = 0.4f;
            MaxStage = 1;
        }

        protected override int CalculateDamage(float critMultiplier)
        {
            float ratio = _style switch
            {
                EnemyArchetypeNormalStyle.Sentinel => 0.85f,
                EnemyArchetypeNormalStyle.Fighter => 1f,
                EnemyArchetypeNormalStyle.Executioner => 1.1f,
                EnemyArchetypeNormalStyle.Marksman => 0.95f,
                EnemyArchetypeNormalStyle.Mage => 0.95f,
                EnemyArchetypeNormalStyle.Tactician => 0.8f,
                EnemyArchetypeNormalStyle.Mechanic => 0.85f,
                EnemyArchetypeNormalStyle.Support => 0.7f,
                _ => 1f,
            };
            Power = Mathf.RoundToInt(ratio * 50f);
            return RollDamage(critMultiplier);
        }

        protected override List<int> GetDamageTags()
        {
            bool contact = _style is EnemyArchetypeNormalStyle.Sentinel
                or EnemyArchetypeNormalStyle.Fighter
                or EnemyArchetypeNormalStyle.Executioner;
            bool physical = _style is EnemyArchetypeNormalStyle.Sentinel
                or EnemyArchetypeNormalStyle.Fighter
                or EnemyArchetypeNormalStyle.Executioner
                or EnemyArchetypeNormalStyle.Marksman
                or EnemyArchetypeNormalStyle.Mechanic;

            return new List<int>
            {
                DamageTag.SingleTarget,
                DamageTag.NormalAttack,
                contact ? DamageTag.ContactAttack : DamageTag.NonContactAttack,
                physical ? DamageTag.Physical : DamageTag.Special,
            };
        }
    }
}
