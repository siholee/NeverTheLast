using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Effects.Base;
using Effects.Buffs;
using Entities;
using UnityEngine;

namespace Effects.Negative
{
    /// <summary>반응의 성격. <b>선언 순서가 곧 우선순위</b>다(위가 먼저).</summary>
    public enum ReactionKind
    {
        /// <summary>증폭 — 방금 그 공격을 되짚어 키운다. 이번에 못 쓰면 사라지므로 가장 앞이다.</summary>
        Amplify,

        /// <summary>확산 — 원소를 진영 전체로 퍼뜨려 다음 반응의 재료를 만든다.</summary>
        Spread,

        /// <summary>제어 — 판을 가장 크게 바꾼다. 분쇄·내부 쿨다운으로 따로 눌린다.</summary>
        Control,

        /// <summary>즉발 피해.</summary>
        Burst,

        /// <summary>지속피해. 밀려도 다음 부착에서 다시 잡힌다.</summary>
        Dot,

        /// <summary>디버프.</summary>
        Debuff,

        /// <summary>버프·보호막. 자기 부착으로 만드는 것이라 경합이 드물다.</summary>
        Buff,

        /// <summary>무효 — 두 원소만 지운다. 다른 반응이 가능하면 굳이 고를 이유가 없다.</summary>
        Null,
    }

    /// <summary>반응의 식별자. 재부착 내부 쿨다운의 키이기도 하다.</summary>
    public enum ReactionId
    {
        None = 0,
        Burn,           // 화상   불 + 불
        Vaporize,       // 증발   불 + 물
        Combustion,     // 연소   불 + 풀
        Overload,       // 과부하 불 + 전기
        Melt,           // 융해   불 + 얼음
        Forge,          // 단조   불 + 바위
        Essence,        // 정수   물 + 물
        Bloom,          // 개화   물 + 풀
        Shock,          // 감전   물 + 전기
        Freeze,         // 빙결   물 + 얼음
        Weathering,     // 풍화   물 + 바위
        Rooting,        // 착근   풀 + 풀
        Activation,     // 활성   풀 + 전기
        Dormancy,       // 휴면   풀 + 얼음
        Growth,         // 성장   풀 + 바위
        Charge,         // 축전   전기 + 전기
        Superconduct,   // 초전도 전기 + 얼음
        Grounding,      // 접지   전기 + 바위
        Slow,           // 둔화   얼음 + 얼음
        Hardening,      // 경화   얼음 + 바위
        Vibration,      // 진동   바위 + 바위
        Diffusion,      // 확산   바람 + X (여섯 원소가 같은 반응이다)
        DiffusionBurst, // 확산   바람 + 바람 (같은 이름, 광역 피해)
    }

    /// <summary>
    /// 원소 반응.
    ///
    /// 두 원소가 한 유닛에게 겹치면 반응이 일어나 <b>두 원소가 모두 사라지고</b> 결과가 남는다.
    /// 결과는 언제나 <b>원소가 부착된 유닛</b>이 가져간다 — 버프도 마찬가지다.
    /// 자기 자신에게 원소를 둘러 버프를 만드는 것이 정규 운용이고,
    /// 적에게 잘못 만들어 적을 이롭게 하는 것이 이 시스템의 상성이다.
    ///
    /// 위력은 전부 <b>유발자의 특수공격력</b>에 비례한다(2026-09 개편 — 옛 CON의 자리).
    /// 반응 피해는 <b>대상의 특수방어력</b>으로 경감된다(<see cref="Combat.DamageMitigation"/>).
    ///
    /// 전체 표는 <c>Detail_16 §4</c>에 있다.
    /// </summary>
    public static class ElementalReaction
    {
        // ── 상태 ID ───────────────────────────────────────────────
        public const int ShockStatusId = 5910;
        public const int BurnStatusId = 5911;
        public const int WeatheringStatusId = 5912;
        public const int BuffStatusId = 5913;
        public const int VulnerableStatusId = 5914;

        public const string BurnStatusKey = "burn";
        public const string ActivationStatusKey = "reaction_Activation";
        public const string WeatheringStatusKey = "reaction_weathering";

        // ── 위력(유발자 특수공격력 대비 배율) ─────────────────────

        /// <summary>지속피해(감전·풍화)의 턴당 위력.</summary>
        private const float DotRatio = 0.6f;

        /// <summary>화상은 대상 최대 체력 비례라 유발자 특수공격력을 보지 않는다.</summary>
        private const float BurnMaxHpPercent = 5f;

        /// <summary>즉발 피해(과부하)의 위력.</summary>
        private const float BurstRatio = 1.2f;

        /// <summary>확산(바람 + 바람)의 위력. 진영 전체를 때리므로 단일보다 낮다.</summary>
        private const float DiffusionBurstRatio = 0.7f;

        /// <summary>증폭이 얹는 약소 고정 피해의 위력.</summary>
        private const float AmplifyRatio = 0.4f;

        /// <summary>경화가 만드는 보호막의 위력.</summary>
        private const float BarrierRatio = 1.5f;

        /// <summary>지속피해·즉발 반응의 기본 지속 턴.</summary>
        public const int DotDuration = 2;

        /// <summary>버프·디버프 반응의 지속 턴.</summary>
        private const int StatDuration = 3;

        /// <summary>
        /// 버프·디버프가 스탯을 움직이는 비율의 상한. 실제 비율은
        /// <c>상한 × 특공 / (특공 + 기준값)</c>이라 레벨과 무관하게 같은 곳으로 수렴한다.
        /// </summary>
        private const float RatioCap = 0.30f;

        // ── 재부착 내부 쿨다운 ────────────────────────────────────

        /// <summary>제어 반응의 재발동 간격(대상의 턴).</summary>
        private const int ControlCooldownTurns = 3;

        /// <summary>지속피해·즉발 반응의 재발동 간격.</summary>
        private const int DamageCooldownTurns = 1;

        /// <summary>
        /// 반응 계열별 재부착 내부 쿨다운. 부착 자체는 막지 않는다 —
        /// 막으면 원소 숙련 코드("X 원소 보유 적에게 +10%")가 함께 꺼지기 때문이다.
        /// </summary>
        private static int CooldownTurns(ReactionKind kind) => kind switch
        {
            ReactionKind.Control => ControlCooldownTurns,
            ReactionKind.Dot or ReactionKind.Burst => DamageCooldownTurns,
            _ => 0,
        };

        // ── 표 ────────────────────────────────────────────────────

        private readonly struct Reaction
        {
            public readonly BaseEnums.UnitElement A;
            public readonly BaseEnums.UnitElement B;
            public readonly ReactionId Id;
            public readonly string Name;
            public readonly ReactionKind Kind;

            /// <summary>버프·디버프가 건드리는 스탯. 그 외에는 의미가 없다.</summary>
            public readonly BaseEnums.StatType Stat;

            public Reaction(BaseEnums.UnitElement a, BaseEnums.UnitElement b, ReactionId id,
                string name, ReactionKind kind, BaseEnums.StatType stat = BaseEnums.StatType.Defense)
            {
                A = a; B = b; Id = id; Name = name; Kind = kind; Stat = stat;
            }
        }

        private const BaseEnums.UnitElement Pyro = BaseEnums.UnitElement.Pyro;
        private const BaseEnums.UnitElement Hydro = BaseEnums.UnitElement.Hydro;
        private const BaseEnums.UnitElement Dendro = BaseEnums.UnitElement.Dendro;
        private const BaseEnums.UnitElement Electro = BaseEnums.UnitElement.Electro;
        private const BaseEnums.UnitElement Cryo = BaseEnums.UnitElement.Cryo;
        private const BaseEnums.UnitElement Anemo = BaseEnums.UnitElement.Anemo;
        private const BaseEnums.UnitElement Geo = BaseEnums.UnitElement.Geo;

        /// <summary>28쌍 전부. 바람이 낀 여섯 쌍은 <b>모두 같은 반응(확산)</b>이다.</summary>
        private static readonly Reaction[] Table =
        {
            // 불
            new(Pyro, Pyro,       ReactionId.Burn,        "화상",   ReactionKind.Dot),
            new(Pyro, Hydro,      ReactionId.Vaporize,    "증발",   ReactionKind.Amplify),
            new(Pyro, Dendro,     ReactionId.Combustion,  "연소",   ReactionKind.Amplify),
            new(Pyro, Electro,    ReactionId.Overload,    "과부하", ReactionKind.Burst),
            new(Pyro, Cryo,       ReactionId.Melt,        "융해",   ReactionKind.Amplify),
            new(Pyro, Geo,        ReactionId.Forge,       "단조",   ReactionKind.Buff,  BaseEnums.StatType.Speed),

            // 물
            new(Hydro, Hydro,     ReactionId.Essence,     "정수",   ReactionKind.Buff,  BaseEnums.StatType.SpecialAttack),
            new(Hydro, Dendro,    ReactionId.Bloom,       "개화",   ReactionKind.Buff,  BaseEnums.StatType.MaxHp),
            new(Hydro, Electro,   ReactionId.Shock,       "감전",   ReactionKind.Dot),
            new(Hydro, Cryo,      ReactionId.Freeze,      "빙결",   ReactionKind.Control),
            new(Hydro, Geo,       ReactionId.Weathering,  "풍화",   ReactionKind.Dot),

            // 풀
            new(Dendro, Dendro,   ReactionId.Rooting,     "착근",   ReactionKind.Control),
            new(Dendro, Electro,  ReactionId.Activation,  "활성",   ReactionKind.Debuff),
            new(Dendro, Cryo,     ReactionId.Dormancy,    "휴면",   ReactionKind.Null),
            new(Dendro, Geo,      ReactionId.Growth,      "성장",   ReactionKind.Buff,  BaseEnums.StatType.Defense),

            // 전기
            new(Electro, Electro, ReactionId.Charge,      "축전",   ReactionKind.Buff,  BaseEnums.StatType.Attack),
            new(Electro, Cryo,    ReactionId.Superconduct,"초전도", ReactionKind.Debuff),
            new(Electro, Geo,     ReactionId.Grounding,   "접지",   ReactionKind.Null),

            // 얼음
            new(Cryo, Cryo,       ReactionId.Slow,        "둔화",   ReactionKind.Debuff, BaseEnums.StatType.Speed),
            new(Cryo, Geo,        ReactionId.Hardening,   "경화",   ReactionKind.Buff),

            // 바위
            new(Geo, Geo,         ReactionId.Vibration,   "진동",   ReactionKind.Control),

            // 바람 — 여섯 쌍이 하나의 '확산'이고, 바람 + 바람만 광역 피해다.
            new(Anemo, Pyro,      ReactionId.Diffusion,   "확산",   ReactionKind.Spread),
            new(Anemo, Hydro,     ReactionId.Diffusion,   "확산",   ReactionKind.Spread),
            new(Anemo, Dendro,    ReactionId.Diffusion,   "확산",   ReactionKind.Spread),
            new(Anemo, Electro,   ReactionId.Diffusion,   "확산",   ReactionKind.Spread),
            new(Anemo, Cryo,      ReactionId.Diffusion,   "확산",   ReactionKind.Spread),
            new(Anemo, Geo,       ReactionId.Diffusion,   "확산",   ReactionKind.Spread),
            new(Anemo, Anemo,     ReactionId.DiffusionBurst, "확산", ReactionKind.Burst),
        };

        /// <summary>자료실이 보여 줄 반응 한 줄. 표가 곧 원본이라 화면에서 다시 적지 않는다.</summary>
        public readonly struct Listing
        {
            public readonly BaseEnums.UnitElement A;
            public readonly BaseEnums.UnitElement B;
            public readonly string Name;
            public readonly ReactionKind Kind;
            public readonly BaseEnums.StatType Stat;

            internal Listing(BaseEnums.UnitElement a, BaseEnums.UnitElement b,
                string name, ReactionKind kind, BaseEnums.StatType stat)
            {
                A = a; B = b; Name = name; Kind = kind; Stat = stat;
            }
        }

        /// <summary>28쌍 전부를 표에 적힌 순서 그대로 돌려준다.</summary>
        public static IEnumerable<Listing> AllReactions()
        {
            foreach (Reaction reaction in Table)
            {
                yield return new Listing(
                    reaction.A, reaction.B, reaction.Name, reaction.Kind, reaction.Stat);
            }
        }

        // ── 판정 ──────────────────────────────────────────────────

        /// <summary>
        /// <paramref name="target"/>이 방금 <paramref name="applied"/> 원소를 부착받았을 때 반응을 검사한다.
        /// 성립하는 쌍이 여럿이면 <see cref="ReactionKind"/> 순서로 하나만 고른다.
        /// </summary>
        /// <param name="source">반응을 일으킨 유닛. 위력이 이 유닛의 특수공격력에 비례한다.</param>
        /// <param name="alreadyAttached">
        /// 부착 이전에 <b>같은 원소가 이미 붙어 있었는지</b>. 같은 원소끼리 겹치는 반응은
        /// 부착 집합만 봐서는 판별할 수 없으므로 부착 지점이 알려 준다.
        /// </param>
        public static bool TryResolve(
            Unit target, BaseEnums.UnitElement applied, Unit source, bool alreadyAttached = false)
        {
            if (target == null || applied == BaseEnums.UnitElement.None) return false;

            Unit actor = source ?? target;
            Reaction? best = null;
            BaseEnums.UnitElement bestPartner = BaseEnums.UnitElement.None;
            int bestRemaining = -1;

            foreach (Reaction candidate in Table)
            {
                BaseEnums.UnitElement partner = Partner(candidate, target, applied, alreadyAttached);
                if (partner == BaseEnums.UnitElement.None) continue;
                if (!target.IsReactionReady((int)candidate.Id, CooldownTurns(candidate.Kind))) continue;
                // 증폭은 피해를 주는 공격이 부착을 일으켰을 때만 성립한다. 곱할 피해가 없으면 넘긴다.
                if (candidate.Kind == ReactionKind.Amplify && AmplifiableDamage(actor, target) <= 0) continue;

                int remaining = target.GetAttachedElementRemainingTurns(partner);
                if (best == null ||
                    candidate.Kind < best.Value.Kind ||
                    (candidate.Kind == best.Value.Kind && remaining > bestRemaining))
                {
                    best = candidate;
                    bestPartner = partner;
                    bestRemaining = remaining;
                }
            }

            if (best == null) return false;

            // 오사방지 — 아군이 아군에게 만든 해로운 반응은 없던 일이 된다.
            // 부착은 이미 끝난 뒤이므로 재료는 그대로 남고 반응만 쉰다.
            if (IsHarmful(best.Value.Kind) && actor != null && actor != target &&
                actor.IsEnemy == target.IsEnemy &&
                Effects.Neutral.FriendlyReactionGuard.Protects(target))
            {
                return false;
            }

            Trigger(target, best.Value, bestPartner, applied, actor);
            return true;
        }

        /// <summary>대상에게 해로운 계열인가. 버프·보호막·무효는 맞아도 손해가 없다.</summary>
        private static bool IsHarmful(ReactionKind kind)
            => kind is ReactionKind.Control or ReactionKind.Burst
                or ReactionKind.Dot or ReactionKind.Debuff;

        /// <summary>이 쌍이 성립하면 상대 원소를, 아니면 None을 돌려준다.</summary>
        private static BaseEnums.UnitElement Partner(
            Reaction reaction, Unit target, BaseEnums.UnitElement applied, bool alreadyAttached)
        {
            if (reaction.A == reaction.B)
            {
                // 같은 원소 반응은 덧붙이기 전에 이미 붙어 있었을 때만 성립한다.
                if (applied != reaction.A || !alreadyAttached) return BaseEnums.UnitElement.None;
                return target.HasAttachedElement(reaction.A) ? reaction.A : BaseEnums.UnitElement.None;
            }

            BaseEnums.UnitElement other =
                applied == reaction.A ? reaction.B :
                applied == reaction.B ? reaction.A :
                BaseEnums.UnitElement.None;
            if (other == BaseEnums.UnitElement.None) return BaseEnums.UnitElement.None;

            // 판정 전용 원소(스사노오 '뇌신')는 반응 재료가 아니다. 실제 부착만 본다.
            if (!target.HasAttachedElement(reaction.A) || !target.HasAttachedElement(reaction.B))
            {
                return BaseEnums.UnitElement.None;
            }
            return other;
        }

        // ── 발동 ──────────────────────────────────────────────────

        private static void Trigger(
            Unit target, Reaction reaction, BaseEnums.UnitElement partner,
            BaseEnums.UnitElement applied, Unit source)
        {
            // 반응한 두 원소는 소모되어 사라진다.
            target.RemoveCombatElement(reaction.A);
            target.RemoveCombatElement(reaction.B);
            target.MarkReactionOccurred((int)reaction.Id);

            switch (reaction.Kind)
            {
                case ReactionKind.Amplify:  ResolveAmplify(target, source); break;
                case ReactionKind.Spread:   ResolveSpread(target, partner == Anemo ? applied : partner, source); break;
                case ReactionKind.Control:  ResolveControl(target, reaction, source); break;
                case ReactionKind.Burst:    ResolveBurst(target, reaction, source); break;
                case ReactionKind.Dot:      ResolveDot(target, reaction, source); break;
                case ReactionKind.Debuff:   ResolveDebuff(target, reaction, source); break;
                case ReactionKind.Buff:     ResolveBuff(target, reaction, source); break;
                case ReactionKind.Null:     break;   // 두 원소를 지우는 것이 전부다
            }

            Unit.NotifyElementalReaction(source, target, reaction.Name);
            Debug.Log($"[원소 반응] {reaction.Name} — {source.UnitName} → {target.UnitName}");
        }

        /// <summary>증폭이 되짚을 수 있는 '방금 그 공격'의 피해. 다른 대상을 때렸으면 0이다.</summary>
        private static int AmplifiableDamage(Unit source, Unit target)
            => source != null && source.LastResolvedTarget == target ? source.LastResolvedDamage : 0;

        /// <summary>유발자 특수공격력 × 배율. 반응 피해 · 보호막의 공통 뼈대다.</summary>
        private static int ReactionPower(Unit source, float ratio)
            => source == null ? 0 : Mathf.Max(1, Mathf.RoundToInt(
                source.StatValue(BaseEnums.StatType.SpecialAttack) * ratio * FieldReactionMultiplier(source)));

        /// <summary>
        /// 증폭(증발·연소·융해) — 약소한 고정 피해에 더해, 부착 직전에 들어간 그 공격의
        /// 피해를 특수공격력 비례로 한 번 더 얹는다. 부착은 언제나 피해 뒤에 오므로
        /// "이번 공격을 키운다"를 사후 추가 피해로 환산한 것이다.
        /// </summary>
        private static void ResolveAmplify(Unit target, Unit source)
        {
            float ratio = Combat.DamageMitigation.ReactionAmplifier(source) - 1f;
            int bonus = Mathf.RoundToInt(AmplifiableDamage(source, target) * ratio);
            int flat = ReactionPower(source, AmplifyRatio);
            DealReactionDamage(target, source, Mathf.Max(1, Mathf.RoundToInt((flat + bonus) * HarmfulPotency(source))));
        }

        /// <summary>
        /// 확산 한 번의 결과. 퍼진 원소 · 원래 대상 · 실제로 부착이 남은 대상을 알린다.
        /// 반응 판정 · 부착 규칙은 바꾸지 않는다 — 바유의 원소 취약(Detail_08 §14)처럼 "실제 전달"을 읽어야 하는 쪽이 듣는다.
        /// </summary>
        public sealed class SpreadResult
        {
            public readonly Unit Source;
            public readonly Unit OriginalTarget;
            public readonly BaseEnums.UnitElement Element;
            public readonly IReadOnlyList<Unit> Delivered;

            public SpreadResult(Unit source, Unit originalTarget, BaseEnums.UnitElement element, IReadOnlyList<Unit> delivered)
            {
                Source = source;
                OriginalTarget = originalTarget;
                Element = element;
                Delivered = delivered;
            }
        }

        /// <summary>확산이 원소를 실제로 퍼뜨린 뒤(반응 알림 전). 바람 + 바람의 광역 피해는 확산이 아니라 오지 않는다.</summary>
        public static event System.Action<SpreadResult> AnySpreadResolved;

        /// <summary>확산 — 부착된 유닛과 <b>같은 진영 전체</b>에 원소를 퍼뜨린다.</summary>
        private static void ResolveSpread(Unit target, BaseEnums.UnitElement spreadElement, Unit source)
        {
            if (spreadElement == BaseEnums.UnitElement.None || spreadElement == Anemo) return;

            var delivered = new List<Unit>();
            foreach (Unit ally in global::Target.GetAllAllies(target).Where(unit => unit != null && unit.isActive))
            {
                // 확산이 뿌린 부착은 다시 반응하지 않는다. 확산 → 반응 → 확산의 무한 연쇄를 막는다.
                // 원래 대상은 확산을 유발한 부착에서 이미 명중했다. 나머지는 각자 특방으로 저항한다.
                if (ally.GrantCombatElement(spreadElement, Unit.CommonElementAuraDuration, source,
                    suppressReaction: true, effectHitResolved: ally == target)) delivered.Add(ally);
            }
            AnySpreadResolved?.Invoke(new SpreadResult(source, target, spreadElement, delivered));
        }

        private static void ResolveControl(Unit target, Reaction reaction, Unit source)
        {
            switch (reaction.Id)
            {
                case ReactionId.Freeze:
                {
                    int turns = ControlStatuses.FreezeTurns(source, target);
                    int cap = ControlTurnCap(source);
                    ControlStatuses.ApplyFreeze(target, source, cap > 0 ? Mathf.Min(turns, cap) : turns, effectHitResolved: true);
                    break;
                }
                case ReactionId.Vibration:
                {
                    int cap = ControlTurnCap(source);
                    if (cap > 0) ControlStatuses.ApplyFixedStun(target, source, cap, effectHitResolved: true);
                    else ControlStatuses.ApplyStun(target, source, effectHitResolved: true);
                    break;
                }
                case ReactionId.Rooting:
                    // 착근은 행동을 막지 않고 뒤로 민다. 제어 분쇄의 대상이 아닌 유일한 제어다.
                    Managers.GameManager.Instance?.ActionScheduler?.DelayAction(
                        target, RootingDelayRatio * HarmfulPotency(source));
                    break;
            }
        }

        /// <summary>착근이 미는 양. 한 번의 행동에 필요한 AV 대비 비율이다.</summary>
        private const float RootingDelayRatio = 0.5f;

        private static void ResolveBurst(Unit target, Reaction reaction, Unit source)
        {
            if (reaction.Id == ReactionId.DiffusionBurst)
            {
                int splash = Mathf.Max(1, Mathf.RoundToInt(ReactionPower(source, DiffusionBurstRatio) * HarmfulPotency(source)));
                foreach (Unit unit in global::Target.GetAllAllies(target).Where(u => u != null && u.isActive && u.HpCurr > 0))
                    DealReactionDamage(unit, source, splash);
                return;
            }

            DealReactionDamage(target, source,
                Mathf.Max(1, Mathf.RoundToInt(ReactionPower(source, BurstRatio) * HarmfulPotency(source))));
        }

        // 반응은 원소 부착에 성공한 뒤에만 여기까지 온다. 특공/특방 명중은 한 번,
        // 코드가 제공하는 추가 상태 저항은 실제 상태 부여 때 별도로 적용한다.
        private static void AddReactionStatus(Unit target, Entities.Status.UnitStatus status)
        {
            status.SkipEffectHitRoll = true;
            target.AddStatus(status);
        }

        private static void ResolveDot(Unit target, Reaction reaction, Unit source)
        {
            if (reaction.Id == ReactionId.Burn)
            {
                // 화상은 대상 최대 체력 비례라 위력을 깎을 곳이 없다. 대신 무뎌진 유발자는 1턴만 태운다.
                int cap = ControlTurnCap(source);
                ApplyBurnWithoutContest(source, target, cap > 0 ? Mathf.Min(DotDuration, cap) : DotDuration);
                return;
            }

            int perTurn = Mathf.Max(1, Mathf.RoundToInt(ReactionPower(source, DotRatio) * HarmfulPotency(source)));

            if (reaction.Id == ReactionId.Weathering)
            {
                AddReactionStatus(target, BuffStatus.Create(
                    WeatheringStatusId, WeatheringStatusKey, reaction.Name,
                    source, target, new WeatheringEffect(perTurn),
                    duration: DotDuration,
                    stackPolicy: BaseEnums.StatusStackPolicy.ExtendDuration,
                    category: BaseEnums.StatusCategory.Negative,
                    description: $"행동을 시작할 때마다 {perTurn}의 풍화 피해를 받습니다."));
                return;
            }

            AddReactionStatus(target, BuffStatus.Create(
                ShockStatusId, $"reaction_shock_{source.GetEntityId()}", reaction.Name,
                source, target, new ReactionDotEffect(perTurn),
                duration: DotDuration,
                stackPolicy: BaseEnums.StatusStackPolicy.ExtendDuration,
                category: BaseEnums.StatusCategory.Negative,
                description: $"턴마다 {perTurn}의 {reaction.Name} 피해를 받습니다."));
        }

        private static void ResolveDebuff(Unit target, Reaction reaction, Unit source)
        {
            float ratio = StatRatio(source) * HarmfulPotency(source);
            if (ratio <= 0f) return;

            // 둔화만 스탯을 깎고, 활성·초전도는 특정 경감 계열의 피해를 더 받게 한다.
            if (reaction.Id == ReactionId.Slow)
            {
                AddReactionStatus(target, BuffStatus.Create(
                    BuffStatusId, $"reaction_{reaction.Id}", reaction.Name,
                    source, target, new StatModifierEffect(reaction.Stat, -ratio, percent: true),
                    duration: StatDuration,
                    category: BaseEnums.StatusCategory.Negative,
                    description: $"{UnitStats.DisplayName(reaction.Stat)}이(가) {ratio * 100f:0.#}% 감소합니다."));
                return;
            }

            // 활성은 특수방어력으로 막는 피해(지속피해·반응)를, 초전도는 방어력으로 막는 피해를 키운다.
            bool isActivation = reaction.Id == ReactionId.Activation;
            BaseEnums.StatType against = isActivation ? BaseEnums.StatType.SpecialDefense : BaseEnums.StatType.Defense;
            string label = isActivation ? "지속·반응" : "일반";
            AddReactionStatus(target, BuffStatus.Create(
                VulnerableStatusId, $"reaction_{reaction.Id}", reaction.Name,
                source, target, new MitigationVulnerabilityEffect(against, 1f + ratio),
                duration: StatDuration,
                category: BaseEnums.StatusCategory.Negative,
                description: $"{label} 피해를 {ratio * 100f:0.#}% 더 받습니다."));
        }

        private static void ResolveBuff(Unit target, Reaction reaction, Unit source)
        {
            // 경화만 보호막이고 나머지는 스탯 배율이다.
            if (reaction.Id == ReactionId.Hardening)
            {
                target.AddShield(ReactionPower(source, BarrierRatio), source);
                return;
            }

            float ratio = StatRatio(source);
            if (ratio <= 0f) return;

            target.AddStatus(BuffStatus.Create(
                BuffStatusId, $"reaction_{reaction.Id}", reaction.Name,
                source, target, new StatModifierEffect(reaction.Stat, ratio, percent: true),
                duration: StatDuration,
                isBeneficial: true,
                description: $"{UnitStats.DisplayName(reaction.Stat)}이(가) {ratio * 100f:0.#}% 증가합니다."));
        }

        /// <summary>버프·디버프가 공유하는 특수공격력 비례 배율. 0 ~ <see cref="RatioCap"/>.</summary>
        private static float StatRatio(Unit source)
        {
            if (source == null) return 0f;
            float attack = Mathf.Max(0f, source.StatValue(BaseEnums.StatType.SpecialAttack));
            return RatioCap * attack / (attack + UnitStats.ArmorConstant(source.Level));
        }

        private static void DealReactionDamage(Unit target, Unit source, int damage)
        {
            if (target == null || !target.isActive || damage <= 0) return;
            target.TakeDamage(new DamageContext(
                source, damage, BaseEnums.CodeType.Effect,
                new List<int> { DamageTag.SingleTarget, DamageTag.ElementalReaction, DamageTag.NonContactAttack }));
        }

        // ── 공용 ──────────────────────────────────────────────────

        /// <summary>
        /// 필드가 만들어 내는 원소 반응 피해 배율. 같은 계열이라 <b>가장 높은 하나만</b> 적용된다.
        /// </summary>
        public static float FieldReactionMultiplier(Unit source)
        {
            if (source == null) return 1f;

            var allies = global::Target.GetAllAllies(source).Where(unit => unit != null && unit.isActive).ToList();
            if (!allies.Contains(source)) allies.Add(source);

            float bonus = 0f;
            foreach (Unit ally in allies)
            foreach (var status in ally.ActiveStatuses)
            foreach (var effect in status.Effects)
            {
                if (effect.EffectObject is IReactionAmplifier amplifier)
                    bonus = Mathf.Max(bonus, amplifier.ReactionDamageBonus);
            }
            return 1f + Mathf.Max(0f, bonus);
        }

        /// <summary>
        /// 공통 화상 부여. 별도 명시가 없으면 2턴이며, 특수공격력 대 특수방어력으로 명중을 판정한다
        /// (같으면 50%). 모든 출처가 같은 키를 써서 재부여 시 지속시간이 연장된다.
        /// </summary>
        public static bool TryApplyBurn(Unit source, Unit target, int duration = DotDuration)
        {
            if (source == null || target == null || !target.isActive || duration <= 0) return false;
            if (!Combat.DamageMitigation.RollEffectHit(source, target, 0.5f)) return false;

            return ApplyBurnWithoutContest(source, target, duration);
        }

        /// <summary>명중 판정 없이 화상을 확정으로 건다. 피해·기간·연장 규칙은 <see cref="TryApplyBurn"/>과 같다.</summary>
        public static bool ApplyBurnWithoutContest(Unit source, Unit target, int duration = DotDuration)
        {
            if (source == null || target == null || !target.isActive || duration <= 0) return false;

            var burn = BuffStatus.Create(
                BurnStatusId, BurnStatusKey, "화상",
                source, target, new PercentDamageOverTimeEffect(0, BurnMaxHpPercent),
                duration: duration,
                stackPolicy: BaseEnums.StatusStackPolicy.ExtendDuration,
                category: BaseEnums.StatusCategory.Negative,
                description: $"{duration}턴간 턴마다 최대 체력의 {BurnMaxHpPercent:0.#}%에 해당하는 화상 피해를 받습니다.");
            burn.SkipEffectHitRoll = true;   // 명중은 TryApplyBurn이 이미 굴렸거나, 확정 부여다
            return target.TryAddStatus(burn);
        }

        /// <summary>
        /// 유발자가 지닌 약화 효과가 <b>해로운 반응</b>의 위력에 곱하는 배율. 여럿이면 가장 약한 값 하나.
        /// </summary>
        public static float HarmfulPotency(Unit source)
        {
            float potency = 1f;
            foreach (IReactionDampener dampener in Dampeners(source))
                potency = Mathf.Min(potency, Mathf.Clamp01(dampener.HarmfulReactionPotency));
            return potency;
        }

        /// <summary>유발자가 일으킨 행동불능 반응(빙결·진동)과 화상의 턴 상한. 0이면 상한이 없다.</summary>
        public static int ControlTurnCap(Unit source)
        {
            int cap = 0;
            foreach (IReactionDampener dampener in Dampeners(source))
            {
                if (dampener.ReactionTurnCap <= 0) continue;
                cap = cap == 0 ? dampener.ReactionTurnCap : Mathf.Min(cap, dampener.ReactionTurnCap);
            }
            return cap;
        }

        private static IEnumerable<IReactionDampener> Dampeners(Unit source)
        {
            if (source == null) yield break;
            foreach (var status in source.ActiveStatuses)
            foreach (var effect in status.Effects)
            {
                if (effect.EffectObject is IReactionDampener dampener) yield return dampener;
            }
        }
    }
    /// <summary>원소 반응 피해를 키우는 코드가 구현한다. 가장 큰 보너스 하나만 적용된다.</summary>
    public interface IReactionAmplifier
    {
        /// <summary>가산 비율. 0.25 = +25%.</summary>
        float ReactionDamageBonus { get; }
    }

    /// <summary>
    /// 유발자가 일으키는 해로운 원소 반응을 약하게 만드는 공용 효과.
    /// 준비가 덜 된 극초반 파티가 빙결 연쇄로 무너지지 않게 하되, 반응이 무엇인지는 그대로 보여 준다.
    /// </summary>
    public interface IReactionDampener
    {
        /// <summary>해로운 반응 위력 배율. 0.5 = 절반.</summary>
        float HarmfulReactionPotency { get; }

        /// <summary>행동불능 반응·화상의 턴 상한. 0이면 상한 없음.</summary>
        int ReactionTurnCap { get; }
    }

    /// <summary>반응이 남기는 지속피해. 대상의 턴마다 한 번 터진다.</summary>
    public sealed class ReactionDotEffect : BaseEffect
    {
        private readonly int _damagePerTurn;

        public override bool IsDamageOverTime => true;

        public ReactionDotEffect(int damagePerTurn) : base(0, damagePerTurn)
        {
            _damagePerTurn = damagePerTurn;
            Category = BaseEnums.EffectCategory.Negative;
        }

        public override void OnOwnerTurn()
        {
            if (Target == null || !Target.isActive) return;

            Target.TakeDamage(CreateDamageOverTimeTick());
        }

        public override DamageContext CreateDamageOverTimeTick() => new DamageContext(
            Caster, _damagePerTurn, BaseEnums.CodeType.Effect,
            new List<int> { DamageTag.SingleTarget, DamageTag.ElementalReaction });

        public override int EstimateDamagePerTurn() => _damagePerTurn;
    }

    /// <summary>
    /// 풍화 — 턴이 아니라 <b>행동을 시작할 때마다</b> 터지는 지속피해.
    ///
    /// 일반행동·궁극기·추가행동을 모두 센다. 다른 지속피해가 턴당 1회인 것과 달리
    /// 턴을 쓰지 않는 행동까지 세므로 <b>빠르거나 궁극기·추가행동이 잦은 대상일수록 아프다.</b>
    /// 캐스터 보스와 추가행동 빌드의 대항 수단이다.
    /// </summary>
    public sealed class WeatheringEffect : BaseEffect
    {
        private readonly int _damagePerAction;
        private System.Action<EventContext> _handler;

        public override bool IsDamageOverTime => true;

        public WeatheringEffect(int damagePerAction) : base(0, damagePerAction)
        {
            _damagePerAction = damagePerAction;
            Category = BaseEnums.EffectCategory.Negative;
        }

        public override void OnApply()
        {
            if (Target == null) return;
            _handler = _ => Detonate();
            Target.AddListener(BaseEnums.UnitEventType.OnNormalActivates, _handler);
            Target.AddListener(BaseEnums.UnitEventType.OnUltimateActivates, _handler);
            Target.AddListener(BaseEnums.UnitEventType.OnAdditionalActivates, _handler);
        }

        public override void OnRemove()
        {
            if (Target == null || _handler == null) return;
            Target.RemoveListener(BaseEnums.UnitEventType.OnNormalActivates, _handler);
            Target.RemoveListener(BaseEnums.UnitEventType.OnUltimateActivates, _handler);
            Target.RemoveListener(BaseEnums.UnitEventType.OnAdditionalActivates, _handler);
            _handler = null;
        }

        private void Detonate()
        {
            if (Target == null || !Target.isActive) return;
            Target.TakeDamage(CreateDamageOverTimeTick());
        }

        public override DamageContext CreateDamageOverTimeTick() => new DamageContext(
            Caster, _damagePerAction, BaseEnums.CodeType.Effect,
            new List<int> { DamageTag.SingleTarget, DamageTag.ElementalReaction });

        /// <summary>턴당 최소 1회는 행동하므로 한 번분으로 어림한다.</summary>
        public override int EstimateDamagePerTurn() => _damagePerAction;
    }

    /// <summary>특정 방어 스탯이 경감하는 피해에게만 받는 피해가 늘어나는 취약. 활성 · 초전도가 쓴다.</summary>
    public sealed class MitigationVulnerabilityEffect : BaseEffect
    {
        private readonly BaseEnums.StatType _against;
        private readonly float _multiplier;

        public MitigationVulnerabilityEffect(BaseEnums.StatType against, float multiplier) : base(0, multiplier)
        {
            _against = against;
            _multiplier = multiplier;
            Category = BaseEnums.EffectCategory.Negative;
        }

        public override float ReceivingDamageModifier(Unit unit, DamageContext context)
            => unit == Target && Combat.DamageMitigation.TryGetDefenseStat(context, out BaseEnums.StatType stat) && stat == _against
                ? _multiplier
                : 1f;
    }
}
