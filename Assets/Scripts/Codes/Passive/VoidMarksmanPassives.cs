using BaseClasses;
using Codes.Base;
using Effects.Base;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    public static class VoidMarksmanCodeIds
    {
        public const int SightLine = 1500;
    }

    public static class VoidMarksmanStatusIds
    {
        public const int SightLine = 7940;
    }

    /// <summary>
    /// 공허의 사수 P 조준선 — 같은 대상을 연달아 때릴수록 그 대상에게 주는 피해가 커진다.
    /// 대상을 바꾸면 처음부터 다시 센다.
    ///
    /// 사수는 <b>적 후열을 먼저 노리는 유일한 적</b>이라, 이 패시브가 붙으면
    /// "물러선 캐릭터를 계속 물고 늘어진다"가 된다. 플레이어는 도발이나 우선도로
    /// 조준선을 끊어 스택을 되돌리는 쪽으로 대응한다.
    /// </summary>
    public sealed class VoidMarksmanSightLine : PersistentStatusPassive
    {
        private const float PerStack = 0.08f;
        private const int MaxStacks = 5;

        public VoidMarksmanSightLine(PassiveCodeContext context)
            : base(context, VoidMarksmanStatusIds.SightLine, "void_marksman_sight_line", "조준선",
                $"같은 대상을 연속으로 공격할 때마다 그 대상에게 주는 피해가 {PerStack * 100f:F0}% "
                + $"증가합니다(최대 {MaxStacks}중첩). 대상을 바꾸면 초기화됩니다.")
        {
            IsUniquePassive = true;
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new SightLineEffect(PerStack, MaxStacks);
    }

    internal sealed class SightLineEffect : BaseEffect
    {
        private readonly float _perStack;
        private readonly int _maxStacks;

        private Unit _lockedTarget;
        private int _stacks;

        public SightLineEffect(float perStack, int maxStacks) : base(0, perStack)
        {
            _perStack = perStack;
            _maxStacks = maxStacks;
        }

        /// <summary>
        /// 피해 계산 도중에 스택을 올린다. 적중 후 이벤트에서 올리면 <b>이번 타격에는 적용되지 않아</b>
        /// 실제 증가가 한 박자씩 밀린다.
        /// </summary>
        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target || target == null) return 1f;
            // 지속피해는 태그가 비어 있어 '연속 사격'으로 세지 않는다.
            if (context == null || context.CodeType == BaseEnums.CodeType.Effect) return 1f;

            if (_lockedTarget != target)
            {
                _lockedTarget = target;
                _stacks = 0;
            }

            float bonus = 1f + _perStack * _stacks;
            _stacks = Mathf.Min(_maxStacks, _stacks + 1);
            return bonus;
        }

        public override void OnRemove()
        {
            _lockedTarget = null;
            _stacks = 0;
        }
    }
}
