using BaseClasses;
using Entities;
using UnityEngine;

namespace Combat
{
    /// <summary>전장에 깔리는 상태. 양 진영 모두에게 같은 규칙으로 작용한다.</summary>
    public enum FieldKind
    {
        None,

        /// <summary>햇빛 — 불 속성이 세지고 물·풀·얼음이 약해진다. 수리야 U가 깐다.</summary>
        Sunlight,
    }

    /// <summary>
    /// 전장 상태 — <b>유닛이 아니라 판 전체에 걸리는 것</b>.
    ///
    /// 상태(<c>UnitStatus</c>)로 만들지 않은 이유는 대상이 유닛이 아니기 때문이다.
    /// 유닛마다 하나씩 붙이면 도중에 소환되거나 합류한 쪽이 빠지고, 걷을 때도 전원을 훑어야 한다.
    /// 판에 한 번만 적어 두고 피해 계산이 그때그때 읽는 쪽이 어긋날 자리가 없다.
    ///
    /// 지속은 <b>깐 유닛의 턴</b>으로 센다. 이 게임의 시간 축은 턴 하나뿐이다.
    /// </summary>
    public static class Battlefield
    {
        /// <summary>햇빛에서 불이 얻는 배율.</summary>
        private const float SunlightBoost = 1.20f;

        /// <summary>햇빛에서 물·풀·얼음이 받는 배율.</summary>
        private const float SunlightPenalty = 0.80f;

        public static FieldKind Current { get; private set; }

        /// <summary>지속을 세는 기준 유닛. 이 유닛의 턴이 지날 때마다 남은 턴이 준다.</summary>
        private static Unit _anchor;
        private static int _remainingTurns;

        public static bool Is(FieldKind kind) => Current == kind && Current != FieldKind.None;

        /// <summary>필드를 깐다. 같은 필드를 다시 깔면 지속시간만 새로 매긴다.</summary>
        public static void Set(FieldKind kind, Unit anchor, int turns)
        {
            Current = kind;
            _anchor = anchor;
            _remainingTurns = Mathf.Max(1, turns);
            Debug.Log($"[전장] {kind} — {anchor?.UnitName} 기준 {_remainingTurns}턴");
        }

        /// <summary>라운드가 끝나면 판도 함께 걷힌다.</summary>
        public static void Clear()
        {
            Current = FieldKind.None;
            _anchor = null;
            _remainingTurns = 0;
        }

        /// <summary>기준 유닛의 턴이 열릴 때 호출한다. 다 되면 스스로 걷힌다.</summary>
        public static void OnAnchorTurn(Unit unit)
        {
            if (Current == FieldKind.None || unit == null || unit != _anchor) return;

            if (--_remainingTurns > 0) return;
            Debug.Log($"[전장] {Current} 종료");
            Clear();
        }

        /// <summary>
        /// 판이 공격자에게 얹는 배율. <see cref="Unit.CalculateFinalDamage"/>가 한 번 곱한다.
        ///
        /// <b>속성(고유 원소)으로 판단한다.</b> 부착 원소가 아니다 — 햇빛이 강하게 만드는 것은
        /// "불을 두른 자"가 아니라 "불의 존재"라서다.
        /// </summary>
        public static float OutgoingMultiplier(Unit attacker)
        {
            if (attacker == null || Current != FieldKind.Sunlight) return 1f;

            return ElementOf(attacker) switch
            {
                BaseEnums.UnitElement.Pyro => SunlightBoost,
                BaseEnums.UnitElement.Hydro or BaseEnums.UnitElement.Dendro
                    or BaseEnums.UnitElement.Cryo => SunlightPenalty,
                _ => 1f,
            };
        }

        private static BaseEnums.UnitElement ElementOf(Unit unit)
            => System.Enum.TryParse(unit.Element, true, out BaseEnums.UnitElement element)
                ? element
                : BaseEnums.UnitElement.None;
    }
}
