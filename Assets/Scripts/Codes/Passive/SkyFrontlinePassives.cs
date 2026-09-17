using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Effects.Negative;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>
    /// 일본 천공 전선 적 코드(1700~1771). 패시브·일반행동·궁극기가 같은 번호대를 슬롯별로 나눠 쓴다.
    /// 설계 원본은 <c>Theme_Japan_Sky_Frontline.md</c>다.
    /// </summary>
    public static class SkyFrontlineCodeIds
    {
        public const int CrystalCushion = 1700;
        public const int PerfectCrystalCushion = 1701;
        public const int CrystalFilm = 1702;
        public const int CondensedFeather = 1703;

        public const int FlickeringLantern = 1710;
        public const int SmallLantern = 1711;
        public const int LanternSwarm = 1712;
        public const int LingeringLight = 1713;

        public const int SealedCore = 1720;
        public const int ApocalypseSealedCore = 1721;
        public const int CondensedSeal = 1722;

        public const int EmberWings = 1730;
        public const int BurningScales = 1731;
        public const int LingeringEmbers = 1732;

        public const int PredatorHide = 1740;
        public const int WoundTracker = 1741;
        public const int DeepDive = 1742;

        public const int ApocalypseHide = 1750;
        public const int DeepApocalypse = 1751;
        public const int TwinTalons = 1752;

        public const int SkySpawning = 1760;
        public const int HardenedCrystal = 1761;
        public const int HuntClimax = 1762;
        public const int Greed = 1763;

        public const int HungerCore = 1770;
        public const int RuptureCore = 1771;
    }

    // ══════════════════════════════════════════════════════════════
    // 공용
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 결정 완충(1700) / 완전한 결정 완충(1701). 받는 치명타의 <b>추가 피해분</b>만 깎는다.
    ///
    /// 치명타 전체 피해를 깎으면 치명타가 비치명타보다 약해지는 역전이 생긴다. 배율에서 1을 뺀
    /// 몫만 줄이므로 치명타는 언제나 비치명타 이상이고, 치명타 판정 자체도 남아 치명타 조건 코드가 산다.
    /// </summary>
    public sealed class SkyCrystalCushion : PersistentStatusPassive
    {
        private readonly float _reduction;

        public SkyCrystalCushion(PassiveCodeContext context, bool perfect)
            : base(context, SkyFrontlineIds.CushionStatus, "sky_crystal_cushion",
                perfect ? "완전한 결정 완충" : "결정 완충",
                $"받는 치명타의 추가 피해분이 {(perfect ? 65 : 50)}% 줄어듭니다.")
        {
            _reduction = perfect ? 0.65f : 0.5f;
            Transferable = false;
            if (perfect) Grade = BaseEnums.CodeGrade.Enhanced;
            else SupersededByCodeId = SkyFrontlineCodeIds.PerfectCrystalCushion;
        }

        protected override BaseEffect CreateInitialEffect() => new CrystalCushionEffect(_reduction);
    }

    internal sealed class CrystalCushionEffect : BaseEffect
    {
        private readonly float _reduction;
        public CrystalCushionEffect(float reduction) : base(0, reduction) => _reduction = reduction;

        public override bool CountsAsReagentBuff => false;

        /// <summary>
        /// 치명타 배율은 피해를 만들 때 이미 곱해져 들어온다. 공격자의 현재 배율로 되짚는 것은
        /// 거인의 병상첨병·약탈자 장비와 같은 방식이다 — 피해 컨텍스트가 배율을 따로 싣지 않는다.
        /// </summary>
        public override float ReceivingDamageModifier(Unit unit, DamageContext context)
        {
            if (unit != Target || context?.IsCrit != true || context.Attacker == null) return 1f;
            float crit = Mathf.Max(1f, context.Attacker.CritMultiplierCurr);
            if (crit <= 1f) return 1f;
            return (1f + (crit - 1f) * (1f - _reduction)) / crit;
        }
    }

    /// <summary>
    /// 입장 보호막. 최대 체력 비율로 건다. 최대 체력을 바꾸는 패시브(얇은 몸·알파 개체)가
    /// 같은 라운드 시작에 붙으므로 <b>한 프레임 뒤</b> 잰다 — 먼저 재면 바뀌기 전 체력으로 계산된다.
    /// </summary>
    public class SkyEntranceShieldPassive : PassiveCode
    {
        private readonly float _ratio;

        public SkyEntranceShieldPassive(PassiveCodeContext context, string name, float ratio) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = name;
            IgnoresActivationChance = true;
            Transferable = false;
            _ratio = ratio;
        }

        public override void CastCode()
        {
            if (Caster == null) return;
            Unit owner = Caster;
            SkyFrontline.RunNextFrame(owner, () =>
            {
                if (!owner.isActive) return;
                SkyFrontline.ShieldRatio(owner, owner, _ratio);
                OnEntered(owner);
            });
        }

        /// <summary>입장 보호막을 건 직후 파생 코드가 덧붙일 일.</summary>
        protected virtual void OnEntered(Unit owner) { }
    }

    /// <summary>
    /// 결정 피막(1702) — 입장 시 최대 체력 10% 보호막.
    /// </summary>
    public sealed class SkyCrystalFilm : SkyEntranceShieldPassive
    {
        public SkyCrystalFilm(PassiveCodeContext context) : base(context, "결정 피막", 0.10f) { }
    }

    /// <summary>
    /// 이름만 있는 해금 패시브. 효과는 해당 코드(궁극기 등)가 <see cref="Unit.HasLearnedPassiveCode"/>로
    /// 읽는다. 위력 170처럼 한 줄짜리 분기를 상태로 따로 만들면 사본이 두 벌 생기기 때문이다.
    /// </summary>
    public sealed class SkyMarkerPassive : PassiveCode
    {
        public SkyMarkerPassive(PassiveCodeContext context, string name) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = name;
            IgnoresActivationChance = true;
            Transferable = false;
        }

        public override void CastCode() { }
    }

    /// <summary>고치·나비의 얇은 몸. 무적이 끝난 본체가 오래 버티지 못하게 한다.</summary>
    internal sealed class FragileBodyEffect : BaseEffect
    {
        private readonly float _multiplier;
        public FragileBodyEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;
        public override bool CountsAsReagentBuff => false;
        public override float MaxHpMultiplierModifier(Unit unit) => unit == Target ? _multiplier : 1f;
    }

    // ══════════════════════════════════════════════════════════════
    // 공허의 등불나방
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 흔들리는 등불(1710). 자기 일반행동 2회마다 주변 3×3에서 가장 다친 동료 하나를 최대 체력 1.5% 치유한다.
    /// 직전 자기 턴부터 이번 턴 시작의 지속피해 처리까지 지속피해를 받았다면 그 회차는 거른다.
    /// </summary>
    public sealed class SkyFlickeringLantern : PersistentStatusPassive
    {
        public SkyFlickeringLantern(PassiveCodeContext context)
            : base(context, SkyFrontlineIds.MothStatus, "sky_flickering_lantern", "흔들리는 등불",
                "일반행동 2회마다 주변 3×3에서 체력 비율이 가장 낮은 동료를 최대 체력 1.5% 치유합니다. " +
                "직전 턴 이후 지속피해를 받았다면 그 치유는 취소됩니다.")
        {
            IsUniquePassive = true;
        }

        protected override BaseEffect CreateInitialEffect() => new LanternEffect();
    }

    internal sealed class LanternEffect : BaseEffect
    {
        private const int ActionsPerHeal = 2;
        private const float HealRatio = 0.015f;

        private Action<EventContext> _damageHandler;
        private Action<EventContext> _turnHandler;
        private Action<EventContext> _actionHandler;
        private bool _dotSinceTurnStart;
        private bool _suppressedThisTurn;
        private int _actions;

        public LanternEffect() : base(0) { }
        public override bool CountsAsReagentBuff => false;

        /// <summary>지금 치유가 막혀 있는가. 궁극기도 같은 조건을 본다.</summary>
        public bool IsSuppressed => _suppressedThisTurn || _dotSinceTurnStart;

        public static LanternEffect Of(Unit unit)
            => unit?.GetStatus(SkyFrontlineIds.MothStatus)?.Effects
                .Select(effect => effect.EffectObject).OfType<LanternEffect>().FirstOrDefault();

        public override void OnApply()
        {
            if (Target == null) return;
            _damageHandler = OnAfterDamageTaken;
            _turnHandler = OnTurnStart;
            _actionHandler = OnNormalActionResolved;
            Target.AddListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _damageHandler);
            Target.AddListener(BaseEnums.UnitEventType.OnTurnStart, _turnHandler);
            Target.AddListener(BaseEnums.UnitEventType.OnNormalActionResolved, _actionHandler);
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _damageHandler);
            Target?.RemoveListener(BaseEnums.UnitEventType.OnTurnStart, _turnHandler);
            Target?.RemoveListener(BaseEnums.UnitEventType.OnNormalActionResolved, _actionHandler);
        }

        /// <summary>
        /// 지속피해는 접촉 태그가 없는 효과 피해다. 틱마다 태그 구성이 조금씩 달라(빈 목록 · 단일+특수)
        /// 목록이 비었는지로는 못 가른다. 반응 폭발은 비접촉 태그를 달고 있어 여기서 빠진다.
        /// </summary>
        private void OnAfterDamageTaken(EventContext context)
        {
            DamageContext damage = context?.DmgCtx;
            if (damage == null || damage.IsCancelled || damage.CodeType != BaseEnums.CodeType.Effect) return;
            if (damage.DamageTags != null &&
                (damage.DamageTags.Contains(DamageTag.ContactAttack) ||
                 damage.DamageTags.Contains(DamageTag.NonContactAttack))) return;
            _dotSinceTurnStart = true;
        }

        /// <summary>OnTurnStart는 이번 턴의 지속피해 틱이 끝난 뒤에 온다. 그래서 창이 정확히 맞는다.</summary>
        private void OnTurnStart(EventContext context)
        {
            _suppressedThisTurn = _dotSinceTurnStart;
            _dotSinceTurnStart = false;
        }

        private void OnNormalActionResolved(EventContext context)
        {
            if (Target == null || !Target.isActive) return;
            if (++_actions % ActionsPerHeal != 0) return;

            if (IsSuppressed)
            {
                Debug.Log($"[흔들리는 등불] {Target.UnitName}이(가) 지속피해를 받아 치유를 거릅니다.");
                return;
            }

            Unit patient = SkyFrontline.OrderByWound(SkyFrontline.AlliesAround(Target)).FirstOrDefault();
            SkyFrontline.HealRatio(Target, patient, HealRatio);
        }
    }

    /// <summary>작은 등불(1711) — 입장 시 자신의 CON만큼 보호막.</summary>
    public sealed class SkySmallLantern : PassiveCode
    {
        public SkySmallLantern(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "작은 등불";
            IgnoresActivationChance = true;
            Transferable = false;
        }

        public override void CastCode()
        {
            if (Caster == null || !Caster.isActive) return;
            Caster.AddShield(Mathf.Max(1, Caster.GetBaseCon()), Caster);
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 공허의 고치
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 봉합된 핵(1720 / 종말의 포식자전 1721). 무적 6·8회, 최대 체력 ×0.25, 포식자 하나와 연결한다.
    /// 연결 대상이 쓰러지면 고치도 붕괴해 떠난다 — 처치가 아니다.
    /// </summary>
    public sealed class SkySealedCore : PersistentStatusPassive
    {
        private readonly int _ward;
        private readonly float _healRatio;

        public SkySealedCore(PassiveCodeContext context, bool apocalypse)
            : base(context, SkyFrontlineIds.CocoonLinkStatus, "sky_sealed_core", "봉합된 핵",
                $"입장 시 무적 {(apocalypse ? 8 : 6)}회. 최대 체력이 25%가 됩니다. 포식자 하나와 연결되어 " +
                $"일반행동으로 그 최대 체력의 {(apocalypse ? 1.5f : 2f):0.#}%를 치유합니다. 연결 대상이 쓰러지면 붕괴합니다.")
        {
            _ward = apocalypse ? 8 : 6;
            _healRatio = apocalypse ? 0.015f : 0.02f;
            IsUniquePassive = true;
        }

        protected override BaseEffect CreateInitialEffect() => new CocoonCoreEffect(_healRatio);

        protected override void OnRegistered() => SkyFrontline.RefreshWard(Caster, _ward);
    }

    internal sealed class CocoonCoreEffect : BaseEffect
    {
        private static readonly int[] PredatorIds =
        {
            SkyFrontlineIds.VoidPredator, SkyFrontlineIds.ApocalypsePredator, SkyFrontlineIds.SkyPredator,
        };

        private Unit _link;
        private Action<EventContext> _linkDeathHandler;

        public float HealRatio { get; }

        public CocoonCoreEffect(float healRatio) : base(0, healRatio) => HealRatio = healRatio;
        public override bool CountsAsReagentBuff => false;

        public override float MaxHpMultiplierModifier(Unit unit) => unit == Target ? 0.25f : 1f;

        public static CocoonCoreEffect Of(Unit unit)
            => unit?.GetStatus(SkyFrontlineIds.CocoonLinkStatus)?.Effects
                .Select(effect => effect.EffectObject).OfType<CocoonCoreEffect>().FirstOrDefault();

        /// <summary>연결 대상. 처음 물을 때 정한다 — 라운드 시작 순회 중에는 동료가 아직 덜 섰을 수 있다.</summary>
        public Unit Link
        {
            get
            {
                if (_link != null) return _link.isActive ? _link : null;
                Bind(ChooseLink());
                return _link;
            }
        }

        public override void OnApply()
        {
            if (Target == null) return;
            // 편성이 다 선 뒤에 잇는다.
            Unit owner = Target;
            SkyFrontline.RunNextFrame(owner, () =>
            {
                if (owner.isActive && _link == null) Bind(ChooseLink());
            });
        }

        public override void OnRemove() => Unbind();

        /// <summary>포식자를 먼저 고른다. 고치끼리·나비에게는 잇지 않는다. 포식자가 없으면 가장 다친 동료다.</summary>
        private Unit ChooseLink()
        {
            List<Unit> allies = CombatTargets.AliveAlliesIncludingSelf(Target)
                .Where(unit => unit != Target && unit.ID != SkyFrontlineIds.Cocoon &&
                               unit.ID != SkyFrontlineIds.ApocalypseCocoon && unit.ID != SkyFrontlineIds.Butterfly &&
                               !SkyFrontline.IsCrystalSummon(unit))
                .ToList();
            Unit predator = allies.FirstOrDefault(unit => PredatorIds.Contains(unit.ID));
            return predator ?? SkyFrontline.OrderByWound(allies).FirstOrDefault();
        }

        private void Bind(Unit link)
        {
            if (link == null || Target == null) return;
            _link = link;
            _linkDeathHandler = OnLinkDeath;
            _link.AddListener(BaseEnums.UnitEventType.OnDeath, _linkDeathHandler);
            Debug.Log($"[봉합된 핵] {Target.UnitName}이(가) {_link.UnitName}에게 연결되었습니다.");
        }

        private void Unbind()
        {
            if (_link != null && _linkDeathHandler != null)
                _link.RemoveListener(BaseEnums.UnitEventType.OnDeath, _linkDeathHandler);
            _linkDeathHandler = null;
        }

        private void OnLinkDeath(EventContext context)
        {
            Unbind();
            if (Target == null || !Target.isActive) return;
            Debug.Log($"[봉합된 핵] 연결 대상이 쓰러져 {Target.UnitName}이(가) 붕괴합니다.");
            Target.Withdraw();
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 공허의 나비
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 불씨를 품은 날개(1730). 무적 4회, 최대 체력 ×0.25. <b>실제로 쓰러지면</b> 같은 진영의 다른 생존자
    /// 전원에게 화상을 확정으로 건다. 시전자는 나비를 쓰러뜨린 쪽이라 그 쪽의 지속피해 보정을 받는다.
    /// </summary>
    public sealed class SkyEmberWings : PersistentStatusPassive
    {
        public SkyEmberWings(PassiveCodeContext context)
            : base(context, SkyFrontlineIds.ButterflyStatus, "sky_ember_wings", "불씨를 품은 날개",
                "입장 시 무적 4회. 최대 체력이 25%가 됩니다. 처치되면 같은 진영 전체에 화상을 겁니다.")
        {
            IsUniquePassive = true;
        }

        protected override BaseEffect CreateInitialEffect() => new EmberWingsEffect();

        protected override void OnRegistered() => SkyFrontline.RefreshWard(Caster, 4);
    }

    internal sealed class EmberWingsEffect : BaseEffect
    {
        private Action<EventContext> _hitHandler;
        private Action<EventContext> _deathHandler;
        private Unit _lastOpponentHitter;
        private bool _spread;

        public EmberWingsEffect() : base(0) { }
        public override bool CountsAsReagentBuff => false;

        public override float MaxHpMultiplierModifier(Unit unit) => unit == Target ? 0.25f : 1f;

        public override void OnApply()
        {
            if (Target == null) return;
            _hitHandler = OnBeforeDamageTaken;
            _deathHandler = OnDeath;
            Target.AddListener(BaseEnums.UnitEventType.OnBeforeDamageTaken, _hitHandler);
            Target.AddListener(BaseEnums.UnitEventType.OnDeath, _deathHandler);
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnBeforeDamageTaken, _hitHandler);
            Target?.RemoveListener(BaseEnums.UnitEventType.OnDeath, _deathHandler);
        }

        /// <summary>피해 소유자를 잃은 사망(반사·강제 처리)에 대비해 마지막으로 때린 상대를 적어 둔다.</summary>
        private void OnBeforeDamageTaken(EventContext context)
        {
            Unit attacker = context?.Grantor;
            if (attacker != null && Target != null && attacker.IsEnemy != Target.IsEnemy) _lastOpponentHitter = attacker;
        }

        private void OnDeath(EventContext context)
        {
            if (_spread || Target == null) return;
            _spread = true;

            Unit killer = context?.Grantor;
            Unit source = killer != null && killer.IsEnemy != Target.IsEnemy ? killer : _lastOpponentHitter;
            if (source == null) return;

            int turns = Target.HasLearnedPassiveCode(SkyFrontlineCodeIds.LingeringEmbers) ? 3 : 2;
            foreach (Unit ally in CombatTargets.AliveAlliesIncludingSelf(Target).Where(unit => unit != Target).ToList())
            {
                ElementalReaction.ApplyBurnWithoutContest(source, ally, turns);
            }
            Debug.Log($"[불씨를 품은 날개] {Target.UnitName}이(가) 쓰러지며 동료 전원에게 화상 {turns}턴을 퍼뜨렸습니다.");
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 포식자 계열
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 포식자의 외피(1740). 입장 시 최대 체력 3% 보호막 + 깃털 결정 무적 3회.
    /// 급강하(U)를 마칠 때마다 무적을 3회로 <b>갱신</b>한다 — 갱신은 궁극기 코드가 한다.
    /// </summary>
    public sealed class SkyPredatorHide : SkyEntranceShieldPassive
    {
        public const int WardCharges = 3;

        public SkyPredatorHide(PassiveCodeContext context) : base(context, "포식자의 외피", 0.03f)
        {
            IsUniquePassive = true;
        }

        public override void CastCode()
        {
            SkyFrontline.RefreshWard(Caster, WardCharges);
            base.CastCode();
        }
    }

    /// <summary>종말의 외피(1750). 입장 시 최대 체력 3% 보호막. 깃털 결정 무적은 없다 — 무적은 고치 둘이 맡는다.</summary>
    public sealed class SkyApocalypseHide : SkyEntranceShieldPassive
    {
        public SkyApocalypseHide(PassiveCodeContext context) : base(context, "종말의 외피", 0.03f)
        {
            IsUniquePassive = true;
        }
    }

    /// <summary>상처 추적(1741). 현재 체력 50% 이하인 대상에게 일반행동 피해 +10%.</summary>
    public sealed class SkyWoundTracker : PersistentStatusPassive
    {
        public SkyWoundTracker(PassiveCodeContext context)
            : base(context, SkyFrontlineIds.WoundTrackerStatus, "sky_wound_tracker", "상처 추적",
                "현재 체력이 50% 이하인 대상에게 일반행동 피해가 10% 증가합니다.")
        {
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new WoundTrackerEffect();
    }

    internal sealed class WoundTrackerEffect : BaseEffect
    {
        public WoundTrackerEffect() : base(0) { }
        public override bool CountsAsReagentBuff => false;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target || target == null || target.HpMax <= 0) return 1f;
            if (context?.DamageTags == null || !context.DamageTags.Contains(DamageTag.NormalAttack)) return 1f;
            return (float)target.HpCurr / target.HpMax <= 0.5f ? 1.1f : 1f;
        }
    }

    /// <summary>
    /// 급강하 계열 궁극기가 끝난 뒤의 공용 후처리. 공허의 포식자는 무적을 갱신하고,
    /// 응축된 깃(1703)을 배운 포식자는 최대 체력 1% 보호막을 두른다.
    /// </summary>
    public static class SkyPredatorAftermath
    {
        public static void Resolve(Unit caster)
        {
            if (caster == null || !caster.isActive) return;
            if (caster.HasLearnedPassiveCode(SkyFrontlineCodeIds.PredatorHide))
                SkyFrontline.RefreshWard(caster, SkyPredatorHide.WardCharges);
            if (caster.HasLearnedPassiveCode(SkyFrontlineCodeIds.CondensedFeather))
                SkyFrontline.ShieldRatio(caster, caster, 0.01f);
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 천공의 포식자
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 천공의 산란(1760). 입장 시 허기 결정·파열 비늘을 하나씩 떨구고, 일반행동 3회마다 하나씩 더 떨군다.
    /// 종류는 파열 → 허기 순으로 번갈아 가며, 상한이나 자리 때문에 거르더라도 순서는 넘어간다.
    ///
    /// 허기 결정을 삼킨 횟수(포만)도 여기서 주는 피해 배율로 바꾼다. 포만은 정화할 수 없는 전투 자원이다.
    /// </summary>
    public sealed class SkySpawning : PersistentStatusPassive
    {
        public SkySpawning(PassiveCodeContext context)
            : base(context, SkyFrontlineIds.SkyPredatorStatus, "sky_spawning", "천공의 산란",
                "입장 시 허기 결정과 파열 비늘을 하나씩 떨구고, 일반행동 3회마다 하나씩 더 떨굽니다(파열 → 허기 교대). " +
                "허기 결정을 삼킬 때마다 주는 피해가 증가합니다.")
        {
            IsUniquePassive = true;
        }

        protected override BaseEffect CreateInitialEffect() => new SkySpawningEffect();
    }

    internal sealed class SkySpawningEffect : BaseEffect
    {
        private const int SpawnInterval = 3;
        private const int ClimaxSpawnInterval = 2;
        public const int SatietyMaximum = 999;

        private Action<EventContext> _actionHandler;
        private Action<EventContext> _damageHandler;
        private Action<EventContext> _deathHandler;
        private int _sinceSpawn;
        private bool _nextIsRupture = true;
        private bool _climax;

        public int NormalActions { get; private set; }

        public SkySpawningEffect() : base(0) { }
        public override bool CountsAsReagentBuff => false;

        public static SkySpawningEffect Of(Unit unit)
            => unit?.GetStatus(SkyFrontlineIds.SkyPredatorStatus)?.Effects
                .Select(effect => effect.EffectObject).OfType<SkySpawningEffect>().FirstOrDefault();

        private int ExtraWard => Target != null && Target.HasLearnedPassiveCode(SkyFrontlineCodeIds.HardenedCrystal) ? 2 : 0;

        public override void OnApply()
        {
            if (Target == null) return;
            _actionHandler = OnNormalActionResolved;
            _damageHandler = OnAfterDamageTaken;
            _deathHandler = OnDeath;
            Target.AddListener(BaseEnums.UnitEventType.OnNormalActionResolved, _actionHandler);
            Target.AddListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _damageHandler);
            Target.AddListener(BaseEnums.UnitEventType.OnDeath, _deathHandler);
            Target.SetCombatResourceMaximum(SkyFrontlineResources.Satiety, SatietyMaximum, resetCurrent: true);

            // 라운드 시작 순회 도중에 적 목록을 늘리면 순회가 깨진다. 한 프레임 뒤에 떨군다.
            Unit owner = Target;
            SkyFrontline.RunNextFrame(owner, () =>
            {
                if (!owner.isActive) return;
                SkyFrontline.SpawnCrystal(owner, SkyFrontlineIds.HungerCrystal, ExtraWard);
                SkyFrontline.SpawnCrystal(owner, SkyFrontlineIds.RuptureScale, ExtraWard);
            });
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnNormalActionResolved, _actionHandler);
            Target?.RemoveListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _damageHandler);
            Target?.RemoveListener(BaseEnums.UnitEventType.OnDeath, _deathHandler);
        }

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target) return 1f;
            int stacks = Target.GetCombatResource(SkyFrontlineResources.Satiety);
            if (stacks <= 0) return 1f;
            float perStack = Target.HasLearnedPassiveCode(SkyFrontlineCodeIds.Greed) ? 0.25f : 0.20f;
            return 1f + perStack * stacks;
        }

        private void OnNormalActionResolved(EventContext context)
        {
            if (Target == null || !Target.isActive) return;
            NormalActions++;

            int interval = _climax ? ClimaxSpawnInterval : SpawnInterval;
            if (++_sinceSpawn < interval) return;
            _sinceSpawn = 0;

            int crystalId = _nextIsRupture ? SkyFrontlineIds.RuptureScale : SkyFrontlineIds.HungerCrystal;
            _nextIsRupture = !_nextIsRupture;
            if (SkyFrontline.SpawnCrystal(Target, crystalId, ExtraWard) == null)
                Debug.Log($"[천공의 산란] 상한이나 자리 때문에 이번 소환을 거릅니다.");
        }

        /// <summary>사냥의 절정(1762). 처음 50% 아래로 내려간 순간 주기를 줄이고, 치유로 올라가도 되돌리지 않는다.</summary>
        private void OnAfterDamageTaken(EventContext context)
        {
            if (_climax || Target == null || !Target.isActive || Target.HpMax <= 0) return;
            if (!Target.HasLearnedPassiveCode(SkyFrontlineCodeIds.HuntClimax)) return;
            if (Target.HpCurr > Target.HpMax * 0.5f) return;

            _climax = true;
            Debug.Log($"[사냥의 절정] {Target.UnitName}의 소환 주기가 일반행동 {ClimaxSpawnInterval}회로 줄었습니다.");
        }

        private void OnDeath(EventContext context) => SkyFrontline.WithdrawCrystals(Target);
    }

    // ══════════════════════════════════════════════════════════════
    // 소환체 — 허기 결정 · 파열 비늘
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 허기 결정(1770) / 파열 비늘(1771)의 핵. 무적과 카운트다운을 세운다.
    /// 본체의 추가 무적·고정 체력은 소환하는 쪽이 덮어쓴다.
    /// </summary>
    public sealed class SkyCrystalCore : PersistentStatusPassive
    {
        private readonly bool _rupture;

        public SkyCrystalCore(PassiveCodeContext context, bool rupture)
            : base(context, SkyFrontlineIds.CrystalCoreStatus, rupture ? "sky_rupture_core" : "sky_hunger_core",
                rupture ? "부푼 비늘" : "맥동하는 결정",
                rupture
                    ? $"무적 {SkyFrontline.RuptureScaleWard}회. 자기 턴 {SkyFrontline.CountdownTurns}번이 지나면 " +
                      "터져 적 전체에게 각자 최대 체력의 35% 피해를 줍니다. 방어력·피해 감소를 무시하고 보호막은 흡수합니다."
                    : $"무적 {SkyFrontline.HungerCrystalWard}회. 자기 턴 {SkyFrontline.CountdownTurns}번이 지나면 " +
                      "본체가 삼켜 최대 체력 10%를 회복하고 포만을 얻습니다.")
        {
            _rupture = rupture;
            IsUniquePassive = true;
        }

        protected override BaseEffect CreateInitialEffect() => new CrystalCoreEffect(_rupture);

        protected override void OnRegistered()
        {
            SkyFrontline.RefreshWard(Caster, _rupture ? SkyFrontline.RuptureScaleWard : SkyFrontline.HungerCrystalWard);
            Caster.SetCombatResourceMaximum(SkyFrontlineResources.Countdown, SkyFrontline.CountdownTurns, resetCurrent: true);
            Caster.AddCombatResource(SkyFrontlineResources.Countdown, SkyFrontline.CountdownTurns);
        }
    }

    internal sealed class CrystalCoreEffect : BaseEffect
    {
        private readonly bool _rupture;
        public CrystalCoreEffect(bool rupture) : base(0) => _rupture = rupture;
        public override bool CountsAsReagentBuff => false;

        /// <summary>파열은 피할 수 없다. 대비는 보호막으로 한다.</summary>
        public override bool IgnoresEvasion(Unit attacker) => _rupture && attacker == Target;
    }
}
