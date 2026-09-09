using System.Collections.Generic;
using BaseClasses;
using Entities;

namespace Combat
{
    /// <summary>
    /// 대상 지정 공지 — <b>피해가 들어가기 전에</b> "네가 지금 노려졌다"를 알린다.
    ///
    /// 피격 시점(<c>OnAfterDamageTaken</c>)에 반응하는 스카디식 반격과 달리, 잔의
    /// <c>최고의 방어</c>처럼 <b>상대의 행동보다 먼저</b> 끼어들어야 하는 코드가 쓴다.
    /// 큐에 추가행동을 예약하면 지금 진행 중인 행동이 끝난 뒤에야 풀리므로 '먼저'가 되지 않는다.
    /// 그래서 이 공지를 듣는 쪽은 <b>그 자리에서 동기적으로</b> 결판을 낸다.
    /// </summary>
    public static class Targeting
    {
        /// <summary>
        /// 대상들에게 지정 사실을 알린다.
        ///
        /// 공지 도중 공격자가 쓰러질 수 있으므로(잔이 되받아쳐 죽이는 경우),
        /// 호출한 쪽은 반환값이 false면 진행 중인 행동을 그대로 접어야 한다.
        /// </summary>
        /// <returns>공격자가 아직 행동을 이어갈 수 있으면 true.</returns>
        public static bool Announce(Unit attacker, IReadOnlyList<Unit> targets)
        {
            if (attacker == null || targets == null) return attacker != null && attacker.isActive;

            for (int i = 0; i < targets.Count; i++)
            {
                Unit target = targets[i];
                if (target == null || !target.isActive) continue;

                target.Invoke(BaseEnums.UnitEventType.OnTargeted, new EventContext(target, attacker));

                // 되받아치기가 공격자를 눕혔다면 남은 대상에게는 알릴 이유가 없다.
                if (!attacker.isActive) return false;
            }

            return attacker.isActive && !attacker.isControlled;
        }
    }
}
