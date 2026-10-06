using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Effects.Base;
using Entities;
using Entities.Status;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 지속피해 즉시 발동 — 대상에게 붙은 모든 <see cref="BaseEffect.IsDamageOverTime"/> 효과의 <b>원시 한 번분</b>에
    /// 비율을 곱해 지금 추가 피해로 낸다(야마, Detail_08 §15.3).
    ///
    /// 기존 효과는 건드리지 않는다. 지속시간 · 중첩 · 경과 턴 · 풍화의 행동 트리거는 그대로이고,
    /// <c>OnOwnerTurn</c>이나 만료 처리도 부르지 않는다. 피해 정보는 효과가 스스로 만든
    /// <see cref="BaseEffect.CreateDamageOverTimeTick"/>(자연 틱과 같은 함수)에서 받아 시전자 · 종류 · 반응 태그를 보존하고,
    /// 금액만 비율로 줄여 <see cref="Unit.TakeDamage"/>에 한 번 넘긴다. 주는 피해 보정(원 시전자의 811 · 903 등)과
    /// 대상 경감은 그 공용 경로가 각각 한 번만 적용한다. 새 효과 클래스를 쓰지 않으므로 직접 공격 태그를 붙이지 않는다.
    /// </summary>
    public static class DamageOverTimeDetonation
    {
        /// <summary>지금 발동 중인 대상. 피해 처리 중 같은 대상에게 다시 발동이 들어오는 재귀를 막는다.</summary>
        private static readonly HashSet<Unit> Running = new();

        /// <summary>
        /// 발동 한 건이 실제 피해 경로를 지난 직후(발동시킨 유닛, 대상, 원 효과, 넘긴 피해 정보 — <c>ResolvedDamage</c>가 채워져 있다).
        /// 자연 틱과 같은 피해 이벤트만으로는 둘을 가를 수 없어, 연출 · 검증이 즉시 발동만 골라 볼 수 있게 연다.
        /// </summary>
        public static event Action<Unit, Unit, BaseEffect, DamageContext> AnyDetonationHit;

        /// <summary>한 대상의 발동이 끝난 뒤(발동시킨 유닛, 대상, 실제로 피해를 넘긴 횟수). 0건이어도 알린다.</summary>
        public static event Action<Unit, Unit, int> AnyDetonationFinished;

        /// <summary>발동 후보 하나 — 상태와 그 안의 지속피해 효과.</summary>
        public readonly struct Entry
        {
            public readonly UnitStatus Status;
            public readonly UnitStatus.EffectInstance Instance;
            public BaseEffect Effect => Instance.EffectObject;

            public Entry(UnitStatus status, UnitStatus.EffectInstance instance)
            {
                Status = status;
                Instance = instance;
            }
        }

        /// <summary>대상에게 지금 즉시 발동이 진행 중인가. 피해 이벤트 안에서 자연 틱과 가를 때 쓴다.</summary>
        public static bool IsRunning(Unit target) => target != null && Running.Contains(target);

        /// <summary>지금 대상에게 붙은 지속피해 효과의 사본 목록. 발동은 이 스냅샷만 돈다.</summary>
        public static List<Entry> Snapshot(Unit target)
        {
            var entries = new List<Entry>();
            if (target == null) return entries;
            foreach (UnitStatus status in target.GetAllStatuses())
            {
                if (status?.Effects == null) continue;
                foreach (UnitStatus.EffectInstance instance in status.Effects.ToList())
                {
                    if (instance?.EffectObject != null && instance.EffectObject.IsDamageOverTime)
                        entries.Add(new Entry(status, instance));
                }
            }
            return entries;
        }

        /// <summary>
        /// 대상의 모든 지속피해를 <paramref name="ratio"/>(0~1) 비율로 즉시 발동한다. 실제로 피해를 넘긴 횟수를 돌려준다.
        /// 대상이 쓰러지면 남은 발동을 멈추고, 처리 도중 사라진 효과와 새로 생긴 효과는 건너뛴다.
        /// </summary>
        /// <param name="trigger">발동시킨 유닛(야마). 피해는 원 시전자 몫이며, 이 값은 알림에만 쓴다.</param>
        public static int Detonate(Unit target, float ratio, Unit trigger = null)
        {
            if (target == null || ratio <= 0f || !target.isActive || target.HpCurr <= 0) return 0;
            if (!Running.Add(target)) return 0;

            int delivered = 0;
            try
            {
                foreach (Entry entry in Snapshot(target))
                {
                    if (!target.isActive || target.HpCurr <= 0) break;
                    if (!StillAttached(target, entry)) continue;

                    DamageContext raw = entry.Effect.CreateDamageOverTimeTick();
                    if (raw == null) continue;
                    int scaled = Mathf.RoundToInt(raw.Damage * Mathf.Clamp01(ratio));
                    if (scaled <= 0) continue;

                    DamageContext context = Scale(raw, scaled);
                    target.TakeDamage(context);
                    delivered++;
                    AnyDetonationHit?.Invoke(trigger, target, entry.Effect, context);
                }
            }
            finally
            {
                Running.Remove(target);
            }
            AnyDetonationFinished?.Invoke(trigger, target, delivered);
            return delivered;
        }

        private static bool StillAttached(Unit target, Entry entry)
            => target.GetStatuses().Contains(entry.Status) &&
               entry.Status.Effects.Contains(entry.Instance) &&
               entry.Instance.EffectObject != null;

        /// <summary>시전자 · 종류 · 태그 · 치명 여부를 그대로 두고 금액만 바꾼 새 피해 정보.</summary>
        private static DamageContext Scale(DamageContext raw, int damage)
            => new DamageContext(raw.Attacker, damage, raw.CodeType, new List<int>(raw.DamageTags ?? new List<int>()), raw.IsCrit)
            {
                OutgoingDamageMultiplier = raw.OutgoingDamageMultiplier,
                DefenseStatMultiplier = raw.DefenseStatMultiplier,
            };
    }
}
