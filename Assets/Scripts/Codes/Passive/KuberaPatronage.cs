using System.Linq;
using BaseClasses;
using Codes.Base;
using Entities;
using Managers;
namespace Codes.Passive
{
    /// <summary>공용 재보 거래를 확정한 공격만 받는 치피 오라. 최종 치명 질의의 가산이라 원본 스탯에 되먹이지 않는다.</summary>
    public sealed class KuberaPatronage : UniquePassiveCode
    {
        public KuberaPatronage(PassiveCodeContext context) : base(context) => CodeName = "재보천의 후원";
        private bool _active;
        public override void CastCode() { _active = true; (Caster?.ActiveSpecialCode as Special.KuberaGoldRush)?.ResetBattle(); }
        public override void StopCode() => _active = false;
        public static float AdditionalCritDamage(Unit actor)
        {
            if (actor == null || actor.IsEnemy || !actor.IsOnField) return 0;
            return GridManager.Instance?.heroList.Where(u => u != null && !u.IsEnemy && u.IsOnField && u.isActive &&
                u.ActivePassiveCodes.OfType<KuberaPatronage>().Any(p => p._active))
                .Select(u => u.StatValue(BaseEnums.StatType.CritDamage) * .30f).DefaultIfEmpty(0).Max() ?? 0;
        }
    }
}
