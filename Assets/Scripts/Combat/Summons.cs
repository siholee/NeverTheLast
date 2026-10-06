using System;
using System.Collections.Generic;
using System.Linq;
using Entities;
using Managers;
using UnityEngine;

namespace Combat
{
    /// <summary>소환수 한 종류의 정의. 소환자 코드가 들고 있다(데이터 표에 넣으면 선택 화면에 섞인다).</summary>
    public sealed class SummonSpec
    {
        /// <summary>소환수 정체 ID. 아군 유닛 ID와 겹치지 않게 900번대를 쓴다.</summary>
        public int Id;
        public string Kind;
        public string Name;
        public string Element;
        public string PortraitKey;
        public float Hp = 1f;
        /// <summary>고정 속도. 0이면 스케줄러가 부르지 않고 소환자 코드가 지시할 때만 움직인다.</summary>
        public float Speed;
        /// <summary>소환수의 일반행동 코드 ID(일반행동 네임스페이스 500번대). 0이면 없다.</summary>
        public int NormalCodeId;
        public bool Targetable;
    }

    /// <summary>소환 방식(Detail_03 §4.7).</summary>
    public enum SummonStyle
    {
        /// <summary>여러 기를 상한까지 거느리고 소모한다(우치). 외부 생성 효과가 소환수를 더 준다.</summary>
        Swarm,
        /// <summary>한 기와 함께 움직인다. 외부 생성 효과는 대체 효과 없이 피해 증가만 준다.</summary>
        Companion,
    }

    /// <summary>소환수가 생긴 경위. 쿠베라 보물은 외부 생성(<see cref="External"/>) 중 자기 초월기를 거른다.</summary>
    public enum SummonSource
    {
        Own,
        External,
    }

    /// <summary>소환자의 규칙. 소환자의 고유 코드가 구현해 <see cref="Summons.Register"/>로 건다.</summary>
    public interface ISummonerPolicy
    {
        SummonStyle Style { get; }
        int Cap { get; }
        SummonSpec Spec { get; }

        /// <summary>
        /// 상한에서 새 소환수가 들어오기 직전에 부른다. 정책이 자리를 비워야 한다(보통 가장 오래된 소환수를 소모).
        /// 자리를 비우지 못하면 새 소환수는 생기지 않는다.
        /// </summary>
        void OnOverflow(Unit owner, Unit oldest);
    }

    /// <summary>
    /// 소환수 생성 · 상한 · 넘침 · 외부 생성의 단일 경로.
    ///
    /// 소환수의 <b>생성</b>은 <see cref="AnySummonCreated"/>로, <b>소멸</b>은 반드시 <see cref="Unit.Die"/>를 지나는 사망 판정으로 알린다.
    /// 생성을 듣는 쪽(쿠베라)과 사망을 듣는 쪽(아누비스)이 같은 소환수 하나에서 각자 이득을 본다.
    /// 라운드 종료로 사라지는 소환수는 사망이 아니다(<see cref="ClearAll"/>).
    /// </summary>
    public static class Summons
    {
        /// <summary>(소환자, 소환수, 경위). 넘쳐 곧바로 사라질 소환수도 생성으로 센다.</summary>
        public static event Action<Unit, Unit, SummonSource, object> AnySummonCreated;

        private static readonly Dictionary<Unit, ISummonerPolicy> Policies = new();
        private static bool _subscribed;

        public static void Register(Unit owner, ISummonerPolicy policy)
        {
            if (owner == null || policy == null) return;
            Policies[owner] = policy;
            EnsureSubscribed();
        }

        public static void Unregister(Unit owner)
        {
            if (owner != null) Policies.Remove(owner);
        }

        public static bool IsSummoner(Unit owner) => owner != null && Policies.ContainsKey(owner);

        public static SummonStyle? StyleOf(Unit owner)
            => owner != null && Policies.TryGetValue(owner, out ISummonerPolicy policy) ? policy.Style : null;

        /// <summary>소환자의 살아 있는 소환수. 만들어진 순서(오래된 것부터).</summary>
        public static List<Unit> Of(Unit owner)
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || owner == null) return new List<Unit>();
            return grid.summonList.Where(unit => unit != null && unit.isActive && unit.SummonOwner == owner).ToList();
        }

        /// <summary>
        /// 소환수를 하나 만든다. 상한이면 정책의 넘침 처리를 먼저 부른다.
        /// <paramref name="sourceTag"/>는 외부 생성자가 자기 생성을 알아보게 붙이는 표식이다(쿠베라 초월기).
        /// </summary>
        public static Unit Create(Unit owner, SummonSource source = SummonSource.Own, object sourceTag = null)
        {
            if (owner == null || !owner.isActive || !Policies.TryGetValue(owner, out ISummonerPolicy policy)) return null;
            GridManager grid = GridManager.Instance;
            if (grid == null) return null;

            List<Unit> current = Of(owner);
            if (current.Count >= Mathf.Max(1, policy.Cap))
            {
                policy.OnOverflow(owner, current[0]);
                if (Of(owner).Count >= Mathf.Max(1, policy.Cap)) return null;
            }

            Unit summon = grid.SpawnSummon(owner, policy.Spec);
            if (summon == null) return null;
            AnySummonCreated?.Invoke(owner, summon, source, sourceTag);
            return summon;
        }

        /// <summary>
        /// 외부 효과가 소환수를 더 준다. <b>다수형에게만</b> 만든다 — 동반형은 대체 효과 없이 무시한다(Detail_03 §4.7).
        /// </summary>
        public static Unit RequestExtra(Unit owner, object sourceTag = null)
            => StyleOf(owner) == SummonStyle.Swarm ? Create(owner, SummonSource.External, sourceTag) : null;

        /// <summary>라운드 종료 — 남은 소환수를 사망 판정 없이 지운다.</summary>
        public static void ClearAll()
        {
            GridManager.Instance?.ClearSummons();
            Policies.Clear();
        }

        private static void EnsureSubscribed()
        {
            if (_subscribed) return;
            _subscribed = true;
            Unit.AnyUnitDied += OnAnyUnitDied;
        }

        /// <summary>소환자가 쓰러지면 남은 소환수도 흩어진다 — 이것은 사망 판정이다.</summary>
        private static void OnAnyUnitDied(Unit dead, Unit attacker)
        {
            if (dead == null || dead.IsSummon) return;
            foreach (Unit summon in Of(dead)) summon.Die(null);
            Policies.Remove(dead);
        }
    }
}
