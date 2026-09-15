using System.Collections.Generic;
using BaseClasses;
using Effects.Projectiles;
using Entities;
using Managers;
using UnityEngine;

namespace Effects
{
    /// <summary>
    /// 공격 하나가 화면에서 취하는 모양.
    ///
    /// 피해 태그의 5만 번대(무기 분류)가 우선이고, 태그가 없으면 <b>손에 든 무기</b>로 정한다.
    /// 같은 50의 피해라도 베는 공격과 쏘는 공격이 다르게 보여야 무엇이 일어났는지 읽힌다.
    /// </summary>
    public enum AttackVisualForm
    {
        /// <summary>베기 — 검·단검. 대상 위를 비스듬히 긋는다.</summary>
        Slash,

        /// <summary>찌르기 — 창. 공격 방향을 따라 꿰뚫는다.</summary>
        Thrust,

        /// <summary>타격 — 둔기·맨손·투척. 방향 없는 충격 고리로 터진다.</summary>
        Impact,

        /// <summary>화살 — 활. 깃이 달린 투사체가 포물선을 그린다.</summary>
        Arrow,

        /// <summary>마법·특수 — 원소색 투사체가 직선으로 날아간다.</summary>
        Bolt,
    }

    /// <summary>
    /// 공격의 겉모습을 한 곳에서 정한다.
    ///
    /// 예전에는 <see cref="Managers.SfxManager"/>가 "검이면 베기"만 알고 나머지를 전부
    /// 같은 투사체로 처리했다. 창도 둔기도 맨손도 똑같은 빛덩이가 날아갔다는 뜻이다.
    /// 판정을 여기로 모아 <b>무기 분류 → 연출</b>이 한 표로 읽히게 한다.
    /// </summary>
    public static class AttackVisuals
    {
        /// <summary>물리 연출이 원소색에서 강철 쪽으로 물러나는 정도. 마법과 구분하기 위한 것이다.</summary>
        private const float PhysicalDesaturation = 0.3f;

        /// <summary>원소가 없는 유닛의 물리 공격 색.</summary>
        private static readonly Color Steel = new(0.82f, 0.87f, 0.94f);

        /// <summary>이 공격이 어떤 모양으로 보일지 고른다.</summary>
        public static AttackVisualForm Classify(Unit attacker, DamageContext context)
        {
            List<int> tags = context?.DamageTags;
            if (tags != null)
            {
                // 코드가 무기 분류를 직접 적었으면 그것이 최우선이다.
                if (tags.Contains(DamageTag.Slash)) return AttackVisualForm.Slash;
                if (tags.Contains(DamageTag.Pierce)) return AttackVisualForm.Thrust;
                if (tags.Contains(DamageTag.Arrow)) return AttackVisualForm.Arrow;
            }

            // 물리가 아니면(특수·마법·지속피해) 원소 투사체다.
            if (tags == null || !tags.Contains(DamageTag.Physical)) return AttackVisualForm.Bolt;

            return FormForWeapon(ResolveWeapon(attacker));
        }

        /// <summary>
        /// 이 모양이 <b>대상 위에서 터지는 근접 연출</b>인가. 아니면 투사체가 날아간다.
        ///
        /// 접촉 태그만으로 가를 수 없다. 일반행동의 기본 태그가 비접촉이라
        /// 그대로 따르면 검사도 창병도 빈손으로 빛을 쏘게 된다. 무기가 근접이면 근접이다.
        /// </summary>
        public static bool IsMelee(Unit attacker, DamageContext context, AttackVisualForm form)
        {
            switch (form)
            {
                case AttackVisualForm.Slash:
                case AttackVisualForm.Thrust:
                    return true;
                case AttackVisualForm.Arrow:
                case AttackVisualForm.Bolt:
                    return false;
                default:
                    // 타격은 둘 다 된다 — 둔기를 들었으면 내려치고, 맨손 비접촉이면 던진다.
                    if (context?.DamageTags?.Contains(DamageTag.ContactAttack) == true) return true;
                    return IsMeleeWeapon(ResolveWeapon(attacker));
            }
        }

        /// <summary>이 모양이 물리 타격인가. 카드 흔들림과 색을 여기서 가른다.</summary>
        public static bool IsPhysical(AttackVisualForm form) => form != AttackVisualForm.Bolt;

        /// <summary>
        /// 연출에 입힐 색. 시전자의 원소색을 쓰되 궁극기는 파스텔, 물리는 강철 쪽으로 물린다.
        ///
        /// 카드의 궁극기 링도 같은 표(<see cref="ElementalProjectiles"/>)를 쓰므로,
        /// 날아가는 색만 봐도 누가 쏜 것인지 알 수 있다.
        /// </summary>
        public static Color AccentFor(Unit attacker, DamageContext context, AttackVisualForm form)
        {
            BaseEnums.UnitElement element = ElementalProjectiles.Parse(attacker?.Element);
            if (element == BaseEnums.UnitElement.None) return Steel;

            bool ultimate = context?.CodeType == BaseEnums.CodeType.Ultimate ||
                            context?.DamageTags?.Contains(DamageTag.UltAttack) == true;
            Color accent = ultimate
                ? ElementalProjectiles.PastelColorFor(element)
                : ElementalProjectiles.ColorFor(element);

            // 물리는 원소를 알아볼 만큼만 남기고 강철에 가깝게 둔다. 마법은 원소색 그대로다.
            return IsPhysical(form) ? Color.Lerp(accent, Steel, PhysicalDesaturation) : accent;
        }

        /// <summary>투사체 외형. 근접 연출에는 쓰지 않는다.</summary>
        public static ProjectileVisualStyle StyleFor(AttackVisualForm form, DamageContext context)
        {
            bool ultimate = context?.CodeType == BaseEnums.CodeType.Ultimate ||
                            context?.DamageTags?.Contains(DamageTag.UltAttack) == true;
            return form switch
            {
                AttackVisualForm.Arrow => ultimate
                    ? ProjectileVisualStyle.UltimateArrow
                    : ProjectileVisualStyle.Arrow,
                AttackVisualForm.Impact => ProjectileVisualStyle.Thrown,
                AttackVisualForm.Thrust => ProjectileVisualStyle.Lance,
                _ => ProjectileVisualStyle.Bolt,
            };
        }

        /// <summary>
        /// 지금 손에 든 무기의 분류. 없으면 시작 숙련에서 찾는다.
        ///
        /// 장비가 먼저다 — 창 숙련자가 검을 들었으면 검을 휘두르는 것이 맞다.
        /// 숙련으로 물러나는 이유는 전투 시작 시점에 장비가 비어 있는 적 유닛 때문이다.
        /// </summary>
        public static EquipmentProficiency ResolveWeapon(Unit attacker)
        {
            if (attacker == null) return EquipmentProficiency.None;

            foreach (ItemData item in attacker.EquippedItems)
            {
                if (item == null) continue;
                EquipmentProficiency proficiency = item.RequiredProficiency;
                // 방패는 무기 분류지만 때리는 모양을 정하지 않는다.
                if (!proficiency.IsWeapon() || proficiency == EquipmentProficiency.Shield) continue;
                return proficiency;
            }

            foreach (string value in attacker.StartingProficiencies)
            {
                if (!System.Enum.TryParse(value, true, out EquipmentProficiency parsed)) continue;
                if (!parsed.IsWeapon() || parsed == EquipmentProficiency.Shield) continue;
                return parsed;
            }

            return EquipmentProficiency.None;
        }

        private static AttackVisualForm FormForWeapon(EquipmentProficiency weapon) => weapon switch
        {
            EquipmentProficiency.Longsword or EquipmentProficiency.Greatsword or
                EquipmentProficiency.Dagger => AttackVisualForm.Slash,
            EquipmentProficiency.Spear or EquipmentProficiency.Shortspear => AttackVisualForm.Thrust,
            EquipmentProficiency.Longbow or EquipmentProficiency.Shortbow or
                EquipmentProficiency.Crossbow => AttackVisualForm.Arrow,
            // 지팡이·보주를 든 채로 물리 피해를 주면 마법이 아니라 후려치는 것이다.
            _ => AttackVisualForm.Impact,
        };

        private static bool IsMeleeWeapon(EquipmentProficiency weapon) => weapon switch
        {
            EquipmentProficiency.Longsword or EquipmentProficiency.Greatsword or
                EquipmentProficiency.Dagger or EquipmentProficiency.Spear or
                EquipmentProficiency.Shortspear or EquipmentProficiency.Mace => true,
            _ => false,
        };
    }
}
