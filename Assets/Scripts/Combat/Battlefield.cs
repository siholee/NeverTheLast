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

        /// <summary>눈 — 얼음 속성이 세지고 불·풀이 약해진다. 볼바·토르·혹한 계열이 깐다.</summary>
        Snow,
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
        /// <summary>필드가 자기 원소에게 주는 배율. 햇빛과 눈이 같은 값을 쓴다.</summary>
        private const float FieldBoost = 1.20f;

        /// <summary>필드가 상극 원소에게 주는 배율.</summary>
        private const float FieldPenalty = 0.80f;

        public static FieldKind Current { get; private set; }

        /// <summary>지속을 세는 기준 유닛. 이 유닛의 턴이 지날 때마다 남은 턴이 준다.</summary>
        private static Unit _anchor;
        private static int _remainingTurns;

        public static bool Is(FieldKind kind)
        {
            ValidateAnchor();
            return Current == kind && Current != FieldKind.None;
        }

        /// <summary>
        /// 판을 깐 유닛이 쓰러졌으면 판도 걷는다.
        ///
        /// 지속을 <b>깐 유닛의 턴</b>으로 세기 때문에, 그 유닛이 죽으면 남은 턴을 줄일 사람이
        /// 아무도 없어 라운드가 끝날 때까지 판이 남는다. 눈을 까는 혹한 계열을 쓰러뜨려도
        /// 눈이 걷히지 않으면 <b>처치 순서에 값이 없어진다.</b> 읽는 자리에서 한 번 확인한다.
        /// </summary>
        private static void ValidateAnchor()
        {
            if (Current == FieldKind.None) return;
            if (_anchor != null && _anchor.isActive) return;

            Debug.Log($"[전장] {Current} — 판을 깐 유닛이 쓰러져 걷힙니다");
            Clear();
        }

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
            ValidateAnchor();
            if (attacker == null || Current == FieldKind.None) return 1f;

            BaseEnums.UnitElement element = ElementOf(attacker);
            float multiplier = RawMultiplier(element);

            // 불리한 쪽만 무시한다. 유리한 배율까지 지우면 면역이 손해가 된다.
            if (multiplier < 1f && IgnoresPenalty(attacker)) return 1f;
            return multiplier;
        }

        private static bool IgnoresPenalty(Unit unit)
        {
            foreach (var status in unit.ActiveStatuses)
            {
                foreach (var effect in status.Effects)
                {
                    if (effect.EffectObject != null && effect.EffectObject.IgnoresFieldPenalty(unit)) return true;
                }
            }
            return false;
        }

        private static float RawMultiplier(BaseEnums.UnitElement element)
        {
            return Current switch
            {
                FieldKind.Sunlight => element switch
                {
                    BaseEnums.UnitElement.Pyro => FieldBoost,
                    BaseEnums.UnitElement.Hydro or BaseEnums.UnitElement.Dendro
                        or BaseEnums.UnitElement.Cryo => FieldPenalty,
                    _ => 1f,
                },

                // 눈은 햇빛의 거울이다. 얼음을 키우고 불·풀을 누른다.
                // 물을 빼 둔 것은 눈과 물이 서로를 방해할 이유가 없어서다 — 같은 추위다.
                FieldKind.Snow => element switch
                {
                    BaseEnums.UnitElement.Cryo => FieldBoost,
                    BaseEnums.UnitElement.Pyro or BaseEnums.UnitElement.Dendro => FieldPenalty,
                    _ => 1f,
                },

                _ => 1f,
            };
        }

        private static BaseEnums.UnitElement ElementOf(Unit unit)
            => System.Enum.TryParse(unit.Element, true, out BaseEnums.UnitElement element)
                ? element
                : BaseEnums.UnitElement.None;
    }
}
