using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Entities;

namespace Codes.Passive
{
    public sealed class SuryaExecution : UniquePassiveCode
    {
        public SuryaExecution(PassiveCodeContext context) : base(context) => CodeName = "황혼의 집행자";
        private string Key => $"surya_execution_{Caster.GetEntityId()}";
        public int Marks(Unit target) => target?.GetAllStatuses().Where(x => x.Key == Key)
            .SelectMany(x => x.Effects).Select(x => x.EffectObject).OfType<ExecutionMark>().Select(x => x.Count).FirstOrDefault() ?? 0;
        public bool Mark(Unit target, int count)
        {
            if (target == null || !target.isActive) return false;
            var status = BuffStatus.Create(70671, Key, "종언 표식", Caster, target,
                new ExecutionMark(UnityEngine.Mathf.Clamp(count, 1, 2)), duration: 0,
                stackPolicy: BaseEnums.StatusStackPolicy.Replace, category: BaseEnums.StatusCategory.Negative,
                description: "2중첩이면 다음 일반 사격이 종언탄이 됩니다. 특수행동은 1중첩 이상을 모두 소모합니다.");
            status.AffectedVfxPartId = "affected.surya.wanted";
            return target.TryAddStatus(status);
        }
        public void Consume(Unit target) => target?.RemoveStatusByKey(Key);
        public override void StopCode()
        { foreach (Unit target in Combat.CombatTargets.AliveEnemies(Caster)) Consume(target); }
        private sealed class ExecutionMark : BaseEffect
        {
            public readonly int Count;
            public ExecutionMark(int count) : base(0, count) { Count = count; Category = BaseEnums.EffectCategory.Negative; }
        }
    }
}
