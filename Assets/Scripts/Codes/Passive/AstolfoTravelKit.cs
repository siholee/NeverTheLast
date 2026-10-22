using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Buffs;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    public sealed class AstolfoTravelKit : UniquePassiveCode
    {
        public const int UnitId = 162;
        public const float PreserveChance = .25f;
        private const string PriorityKey = "astolfo_travel_priority";
        private bool _working;

        public AstolfoTravelKit(PassiveCodeContext context) : base(context) => CodeName = "아직 남아 있어!";
        public override void CastCode()
        {
            if (_working || Caster == null) return;
            _working = true;
            Caster.AddStatus(BuffStatus.Create(71620, PriorityKey, CodeName, Caster, Caster,
                new TargetPriorityModifierEffect(-1), category: BaseEnums.StatusCategory.Neutral,
                description: "타겟 우선도 -1. 전투 비약·심장 제세동기 소모 시25% 확률로 보존."));
        }
        public override void StopCode()
        {
            _working = false;
            Caster?.RemoveStatusByKey(PriorityKey);
        }

        // 같은 특성이 여럿이어도 전투 비약/제세동기 한 번의 소모에 보존 판정은 한 번이다.
        public static bool TryPreserveBattleConsumable()
        {
            var grid = GridManager.Instance;
            if (grid == null || !grid.heroList.Any(unit => unit != null && unit.isActive && unit.IsOnField &&
                unit.ActivePassiveCodes.OfType<AstolfoTravelKit>().Any(code => code._working))) return false;
            return Random.value < PreserveChance;
        }
    }
}
