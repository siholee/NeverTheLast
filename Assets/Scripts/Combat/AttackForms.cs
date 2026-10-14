using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Entities;
using Managers;

namespace Combat
{
    /// <summary>
    /// 공격 형태(50000번대) 조회. 보상 풀이 "파티에 베기 공격이 있는가"를 묻는 한 곳이다(Detail_17 §3.1).
    /// 코드마다 <see cref="Code.AttackForm"/>을 선언하므로 피해를 실제로 내기 전에도 답할 수 있다.
    /// </summary>
    public static class AttackForms
    {
        /// <summary>이 유닛이 가진 코드들의 공격 형태.</summary>
        public static IEnumerable<int> Of(Unit unit)
        {
            if (unit == null) yield break;
            var codes = new List<Code> { unit.ActiveNormalCode, unit.ActiveSpecialCode, unit.ActiveUltimateCode };
            codes.AddRange(unit.ActivePassiveCodes);
            foreach (Code code in codes)
            {
                if (code != null && DamageTag.IsAttackForm(code.AttackForm)) yield return code.AttackForm;
            }
        }

        /// <summary>파티(벤치 · 쓰러진 아군 포함)에 그 형태의 공격을 가진 유닛이 있는가.</summary>
        public static bool PartyHas(int formTag)
        {
            if (!DamageTag.IsAttackForm(formTag)) return true;
            return PartyUnits().Any(unit => Of(unit).Contains(formTag));
        }

        /// <summary>보상 풀이 보는 파티. 쓰러진 아군도 다시 일어나므로 포함한다.</summary>
        public static IEnumerable<Unit> PartyUnits()
        {
            GridManager grid = GridManager.Instance;
            if (grid?.heroList == null) return Enumerable.Empty<Unit>();
            return grid.heroList.Where(unit => unit != null && !unit.IsEnemy && !unit.IsSummon &&
                                               (unit.ID > 0 || unit.LastActiveId > 0));
        }
    }
}
