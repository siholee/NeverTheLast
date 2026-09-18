using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Effects.Negative;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    internal static class AswanStatusIds
    {
        public const int PyreJudgment = 7830;
        public const int PyreSentence = 7831;
        public const int NetherReturn = 7832;
        public const int ImperfectResurrection = 7833;
        public const int Ascension = 7834;
        public const int HolyBlade = 7835;
        public const int CoarseSkin = 7836;
        public const int Transcendence = 7837;
        public const int SoulDrain = 7838;
        public const int Hellfire = 7839;
        public const int DeathChant = 7840;
    }

    /// <summary>
    /// 아스완 테마의 공용 전투 도우미.
    ///
    /// 테마의 두 축은 <b>불</b>과 <b>사령</b>이다. 불은 화상으로, 사령은 소환과 부활로 굴러간다.
    /// 두 축이 여러 코드에 걸쳐 있어 판정과 소환을 여기 한 곳에 모았다.
    /// </summary>
    internal static class AswanCombat
    {
        /// <summary>명계의 사령(60_enemies.yaml). 오시리스의 부위 파괴와 사령의 궁극기가 이 ID를 소환한다.</summary>
        public const int WraithId = 1052;

        /// <summary>아문·라의 '승천' 스택. 궁극기가 소비하고 패시브가 쌓는다.</summary>
        public const string AscensionResource = "amunra_ascension";
        public const int AscensionMax = 99;

        /// <summary>
        /// 출혈은 <see cref="Effects.Negative.BleedStatus"/>가 계열 공용으로 들고 있다.
        /// 콜로세움의 '열상'(6261)과는 별개다 — 저쪽은 시전자 공격력 비례, 이쪽은 최대 체력 비례다.
        /// </summary>
        public const float BleedMaxHpPercent = 1f;

        public static List<Unit> Enemies(Unit caster) => global::Target.GetAllEnemies(caster)
            .Where(unit => unit != null && unit.isActive && !unit.IsUntargetable).ToList();

        public static List<Unit> AlliesIncludingSelf(Unit caster)
        {
            var allies = global::Target.GetAllAllies(caster)
                .Where(unit => unit != null && unit.isActive).ToList();
            if (caster != null && caster.isActive && !allies.Contains(caster)) allies.Add(caster);
            return allies;
        }

        public static bool IsWraith(Unit unit) => unit != null &&
            (unit.ID == WraithId || unit.HasUnitTag("Undead"));

        /// <summary>
        /// 출혈 명중 판정. 화상·제어와 달리 <b>시전자의 LUK+DEX와 대상의 CON</b>을 겨룬다.
        /// 동률 50%, 1 차이마다 1%p이며 극단값에서도 10~90%를 벗어나지 않는다.
        /// </summary>
        public static float BleedHitChance(Unit source, Unit target)
        {
            float attack = source != null ? source.GetBaseLuk() + source.GetBaseDex() : 0f;
            float defense = target != null ? target.GetBaseCon() : 0f;
            return Mathf.Clamp(0.5f + (attack - defense) * 0.01f, 0.1f, 0.9f);
        }

        /// <summary>턴마다 최대 체력의 1%를 깎는 출혈. 재부여는 지속시간만 연장한다.</summary>
        public static bool TryApplyBleed(Unit source, Unit target, int turns = 5)
        {
            if (source == null || target == null || !target.isActive || turns <= 0) return false;
            if (UnityEngine.Random.value > BleedHitChance(source, target)) return false;

            Effects.Negative.BleedStatus.Apply(target, source, turns, BleedMaxHpPercent, "사령의 발톱");
            return true;
        }

        /// <summary>
        /// 아스완의 불 원소 부착.
        ///
        /// <b>부착은 언제나 정상으로 한다.</b> 예전에는 대상이 이미 불을 지녔으면 부착 대신
        /// 화상으로 바꿨는데, 그것은 불 + 불이 기본 반응이 아니던 시절의 임시방편이었다.
        /// 지금은 불 + 불 = 화상이 전역 규칙이므로 여기서 가로챌 이유가 없다.
        ///
        /// 화형 선고는 그 위에 <b>화상을 하나 더 얹는다.</b> 반응을 대체하는 것이 아니라 겹치는
        /// 것이라, 부착된 불이 소모되지 않고 남아 성기사의 <c>화형 심판</c>이 노리는
        /// "불 보유 + 화상"이 동시에 성립한다. 반응만으로는 불이 소모되어 절반만 성립한다.
        /// </summary>
        public static void GrantPyro(Unit source, Unit target, int duration = Unit.CommonElementAuraDuration)
        {
            if (source == null || target == null || !target.isActive) return;

            target.GrantCombatElement(BaseEnums.UnitElement.Pyro, duration, source);

            if (!HasPyreSentenceAlly(source) || !target.isActive) return;
            // 반응을 거치지 않고 화상을 바로 거는 길이라, 무뎌진 장비의 1턴 상한을 여기서도 지킨다.
            int cap = ElementalReaction.ControlTurnCap(source);
            int turns = cap > 0 ? Mathf.Min(ElementalReaction.DotDuration, cap) : ElementalReaction.DotDuration;
            if (!ElementalReaction.TryApplyBurn(source, target, turns))
            {
                Debug.Log($"[화형 선고] {target.UnitName}이(가) 화상에 저항했습니다.");
            }
        }

        private static bool HasPyreSentenceAlly(Unit source)
            => AlliesIncludingSelf(source).Any(ally => ally.HasStatus(AswanStatusIds.PyreSentence));

        /// <summary>
        /// 소환자 진영의 빈 칸에 사령을 세운다. 전열부터 채우고 자리가 없으면 세운 만큼만 돌려준다.
        ///
        /// 라운드 도중에 들어오는 유닛이라 <see cref="GridManager.SpawnUnit"/>만으로는 패시브가 걸리지 않는다.
        /// (패시브는 라운드 시작 이벤트에서 발동한다.) 그래서 세운 뒤 직접 OnRoundStart를 보낸다.
        /// 행동치는 <c>ActionScheduler</c>가 다음 틱의 SyncParticipants에서 자동으로 잡아 준다.
        /// </summary>
        public static int SummonWraiths(Unit summoner, int count)
        {
            GridManager grid = GridManager.Instance;
            if (summoner == null || grid == null || count <= 0) return 0;

            bool isEnemy = summoner.IsEnemy;
            int summoned = 0;

            foreach (int xPos in new[] { grid.GetFrontColumn(isEnemy), grid.GetRearColumn(isEnemy) })
            {
                for (int yPos = grid.yMin; yPos <= grid.yMax && summoned < count; yPos++)
                {
                    if (!grid.IsCellAvailable(xPos, yPos)) continue;

                    grid.SpawnUnit(xPos, yPos, isEnemy, WraithId);
                    Unit spawned = (isEnemy ? grid.enemyList : grid.heroList)
                        .LastOrDefault(unit => unit != null && unit.isActive &&
                                               unit.currentCell != null &&
                                               unit.currentCell.xPos == xPos && unit.currentCell.yPos == yPos);
                    spawned?.Invoke(BaseEnums.UnitEventType.OnRoundStart, new EventContext(spawned));
                    summoned++;
                }
                if (summoned >= count) break;
            }

            if (summoned > 0) Debug.Log($"[아스완] {summoner.UnitName}이(가) 사령 {summoned}기를 소환했습니다.");
            return summoned;
        }

        public static bool HasEmptyFieldSlot(Unit summoner)
        {
            GridManager grid = GridManager.Instance;
            if (summoner == null || grid == null) return false;

            bool isEnemy = summoner.IsEnemy;
            for (int yPos = grid.yMin; yPos <= grid.yMax; yPos++)
            {
                if (grid.IsCellAvailable(grid.GetFrontColumn(isEnemy), yPos)) return true;
                if (grid.IsCellAvailable(grid.GetRearColumn(isEnemy), yPos)) return true;
            }
            return false;
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 종말의 사도 — 성기사 계열
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 360·361 화형 심판 — 불 원소를 보유한 적과 화상 중인 적에게 각각 방어력을 무시한다.
    /// 두 조건은 겹칠 수 있어 최대 두 배까지 쌓인다(파멸 10%+10%, 종말 20%+20%).
    /// </summary>
    public sealed class AswanPyreJudgment : PassiveCode
    {
        private readonly float _ratio;

        public AswanPyreJudgment(PassiveCodeContext context, float ratio) : base(context)
        {
            _ratio = ratio;
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "화형 심판";
            IgnoresActivationChance = true;
            Transferable = false;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            AswanStatusIds.PyreJudgment, "aswan_pyre_judgment", CodeName,
            Caster, Caster, new AswanPyreJudgmentEffect(_ratio),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: $"불 원소를 보유한 적과 화상 중인 적에게 각각 방어력 {_ratio * 100f:F0}%를 무시합니다."));
    }

    internal sealed class AswanPyreJudgmentEffect : BaseEffect
    {
        private readonly float _ratio;
        public AswanPyreJudgmentEffect(float ratio) : base(0) => _ratio = ratio;

        public override float DefenseStatMultiplierModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target || target == null) return 1f;

            float ignored = 0f;
            if (target.HasCombatElement(BaseEnums.UnitElement.Pyro)) ignored += _ratio;
            if (target.HasStatus(ElementalReaction.BurnStatusId)) ignored += _ratio;
            return Mathf.Max(0f, 1f - ignored);
        }
    }

    /// <summary>366 성스러운 칼날 — '베기' 태그로 터진 치명타의 피해를 25% 키운다.</summary>
    public sealed class AswanHolyBlade : PassiveCode
    {
        public AswanHolyBlade(PassiveCodeContext context) : base(context)
        { CodeType = BaseEnums.CodeType.Passive; CodeName = "성스러운 칼날"; IgnoresActivationChance = true; }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            AswanStatusIds.HolyBlade, "aswan_holy_blade", CodeName,
            Caster, Caster, new AswanHolyBladeEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore, isBeneficial: true,
            description: "'베기' 공격의 치명타 피해가 25% 증가합니다."));
    }

    internal sealed class AswanHolyBladeEffect : BaseEffect
    {
        public AswanHolyBladeEffect() : base(0) { }

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && context != null && context.IsCrit &&
               context.DamageTags != null && context.DamageTags.Contains(DamageTag.Slash)
                ? 1.25f : 1f;
    }

    /// <summary>
    /// 367 까칠한 피부 — 접촉 공격을 받으면 방어력으로 깎아 낸 만큼의 절반을 되돌려준다.
    /// 되돌리는 피해는 방어력 감쇠를 다시 받지 않도록 고정 피해로 나간다.
    /// </summary>
    public sealed class AswanCoarseSkin : PassiveCode
    {
        public AswanCoarseSkin(PassiveCodeContext context) : base(context)
        { CodeType = BaseEnums.CodeType.Passive; CodeName = "까칠한 피부"; IgnoresActivationChance = true; }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            AswanStatusIds.CoarseSkin, "aswan_coarse_skin", CodeName,
            Caster, Caster, new AswanCoarseSkinEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore, isBeneficial: true,
            description: "접촉 피해를 받으면 방어력으로 감소시킨 피해의 50%를 공격자에게 되돌려줍니다."));
    }

    internal sealed class AswanCoarseSkinEffect : BaseEffect
    {
        private Action<EventContext> _handler;

        public AswanCoarseSkinEffect() : base(0) { }

        public override void OnApply()
        {
            if (Target == null) return;
            _handler = OnAfterDamageTaken;
            Target.AddListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _handler);
        }

        public override void OnRemove()
        {
            if (Target == null || _handler == null) return;
            Target.RemoveListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _handler);
            _handler = null;
        }

        private void OnAfterDamageTaken(EventContext context)
        {
            DamageContext dmgCtx = context?.DmgCtx;
            if (Target == null || dmgCtx == null || dmgCtx.IsCancelled) return;

            Unit attacker = dmgCtx.Attacker;
            if (attacker == null || attacker == Target || !attacker.isActive) return;
            // 지속피해(화상·출혈)는 태그가 비어 있어 '비접촉이 아니다'로는 걸러지지 않는다.
            // 접촉 태그를 명시적으로 요구해 반사 대상을 실제 근접 공격으로 한정한다.
            if (dmgCtx.CodeType == BaseEnums.CodeType.Effect) return;
            if (dmgCtx.DamageTags == null || !dmgCtx.DamageTags.Contains(DamageTag.ContactAttack)) return;
            // 되돌린 피해가 다시 되돌아오는 것을 막는다.
            if (dmgCtx.DamageTags.Contains(DamageTag.TrueDamage)) return;

            // 방어력이 깎아 낸 몫 = 원래 피해 × (1 − 방어력 감쇠 배율).
            float mitigated = dmgCtx.Damage * (1f - Target.DamageTakenMultiplierFromArmor);
            int reflected = Mathf.RoundToInt(mitigated * 0.5f);
            if (reflected <= 0) return;

            attacker.TakeDamage(new DamageContext(
                Target, reflected, BaseEnums.CodeType.Passive,
                new List<int>
                {
                    DamageTag.SingleTarget, DamageTag.Physical,
                    DamageTag.ContactAttack, DamageTag.TrueDamage,
                }));
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 종말의 사도 — 이단심문관 계열
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 1402 화형 선고 — 필드에 있는 동안 <b>아군이 부여하는 불 원소가 화상을 함께 건다.</b>
    ///
    /// 불 + 불 = 화상은 이제 누구나 일으키는 기본 반응이다. 이단심문관의 값은
    /// <b>불을 소모하지 않고 화상을 얹는다</b>는 데 있다. 그래야 같은 대상에게
    /// 불과 화상이 동시에 남아 성기사의 <c>화형 심판</c>이 방어력 40%를 온전히 무시한다.
    ///
    /// 실제 처리는 <see cref="AswanCombat.GrantPyro"/>가 이 상태를 보고 한다.
    /// 상태 자체는 표식이므로 스탯 훅이 없다.
    /// </summary>
    public sealed class AswanPyreSentence : PassiveCode
    {
        public AswanPyreSentence(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "화형 선고";
            IgnoresActivationChance = true;
            Transferable = false;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            AswanStatusIds.PyreSentence, "aswan_pyre_sentence", CodeName,
            Caster, Caster, new MarkerBuffEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore, isBeneficial: true,
            description: "아군이 부여하는 불 원소가 화상을 함께 겁니다. 불 원소는 소모되지 않습니다."));
    }

    // ══════════════════════════════════════════════════════════════
    // 사령 계열
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 363 명계의 재림 — 전투당 한 번, 치명적인 피해를 막고 다음 자기 턴에 첫 상태로 돌아온다.
    ///
    /// 상태 목록 순회 안전을 위해 <b>복구를 즉시 하지 않는다.</b>
    /// 사망 판정은 상태 목록을 순회하는 도중이라 그 자리에서 상태를 지우면 순회가 깨진다.
    /// 대신 한 턴 무적·행동불능으로 세워 두고 자기 턴이 열릴 때 되살린다.
    /// </summary>
    public sealed class AswanNetherReturn : PassiveCode
    {
        public AswanNetherReturn(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "명계의 재림";
            IgnoresActivationChance = true;
            Transferable = false;
            SupersededByCodeId = VoidPerfectResurrection.CodeId;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            AswanStatusIds.NetherReturn, "aswan_nether_return", CodeName,
            Caster, Caster, new AswanNetherReturnEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            category: BaseEnums.StatusCategory.Neutral,
            isBeneficial: true,
            description: "치명적인 피해를 입으면 전투당 1회 부활하여 첫 상태로 돌아옵니다."));
    }

    internal sealed class AswanNetherReturnEffect : BaseEffect
    {
        private bool _used;
        private bool _returning;

        public AswanNetherReturnEffect() : base(0) { }

        public override bool TryPreventDeath(Unit unit, Unit attacker)
        {
            if (_used || unit == null || unit != Target) return false;
            _used = true;
            _returning = true;
            unit.AddUntargetableSource();
            unit.ControlStarts(new ControlContext(attacker, 1));
            return true;
        }

        public override void OnOwnerTurn()
        {
            if (!_returning || Target == null || !Target.isActive) return;
            _returning = false;

            Target.RemoveUntargetableSource();
            Target.ControlEnds();

            // '첫 상태로 되돌린다' — 걸려 있던 해로운 상태를 털고 체력을 가득 채운다.
            foreach (var status in Target.ActiveStatuses
                         .Where(status => status != null && status.Category == BaseEnums.StatusCategory.Negative)
                         .ToList())
            {
                Target.RemoveStatusByKey(status.Key);
            }
            Target.ResetCombatElements();
            Target.ModifyHp(Target.HpMax, Target);
            Debug.Log($"[명계의 재림] {Target.UnitName}이(가) 되살아났습니다.");
        }
    }

    /// <summary>
    /// 371 죽음의 성가 — 필드의 사령 아군 하나당 아군 전체의 가하는 피해가 5% 증가한다.
    /// 타락한 사제 전용이며, 사령 소환이 그대로 진영 화력으로 이어지게 만드는 축이다.
    /// </summary>
    public sealed class AswanDeathChant : PassiveCode
    {
        public AswanDeathChant(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "죽음의 성가";
            IgnoresActivationChance = true;
            Transferable = false;
        }

        public override void CastCode()
        {
            if (Caster == null) return;
            foreach (Unit ally in AswanCombat.AlliesIncludingSelf(Caster))
            {
                ally.AddStatus(BuffStatus.Create(
                    AswanStatusIds.DeathChant, $"aswan_death_chant_{Caster.GetEntityId()}", CodeName,
                    Caster, ally, new AswanDeathChantEffect(Caster),
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    isBeneficial: true,
                    description: "필드의 사령 아군 하나당 가하는 피해가 5% 증가합니다."));
            }
        }
    }

    internal sealed class AswanDeathChantEffect : BaseEffect
    {
        private readonly Unit _chanter;
        public AswanDeathChantEffect(Unit chanter) : base(0) => _chanter = chanter;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target || _chanter == null || !_chanter.isActive) return 1f;
            int wraiths = AswanCombat.AlliesIncludingSelf(_chanter).Count(AswanCombat.IsWraith);
            return 1f + wraiths * 0.05f;
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 오시리스
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 364 불완전한 부활 — 체력이 14개의 부위로 나뉜다.
    ///
    /// · 최대 체력 ×14
    /// · 시작 시 '받는 피해 감소 −140%'(= 받는 피해 2.4배)를 지고 시작하며,
    ///   부위 하나가 깨질 때마다 그 수치가 10%p씩 회복되어 마지막 부위에서 1.0배가 된다.
    /// · 부위가 깨질 때마다 사령 3기가 필드에 선다.
    ///
    /// 즉 <b>깎을수록 단단해지는</b> 보스다. 초반 화력으로 부위를 몰아 깨면 이득이 크지만
    /// 그만큼 사령이 쏟아지므로, 화력과 처리량 중 무엇을 먼저 낼지 고르게 만든다.
    /// </summary>
    public sealed class OsirisImperfectResurrection : PassiveCode
    {
        public OsirisImperfectResurrection(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "불완전한 부활";
            IgnoresActivationChance = true;
            Transferable = false;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            AswanStatusIds.ImperfectResurrection, "osiris_imperfect_resurrection", CodeName,
            Caster, Caster, new OsirisImperfectResurrectionEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            category: BaseEnums.StatusCategory.Neutral,
            isBeneficial: true,
            description: "최대 체력이 14배가 되고 받는 피해가 2.4배에서 시작합니다. " +
                         "부위(체력 1/14)가 깨질 때마다 받는 피해가 10%p 줄고 사령 3기가 소환됩니다."));
    }

    internal sealed class OsirisImperfectResurrectionEffect : BaseEffect
    {
        public const int PartCount = 14;
        private const float StartingAmplification = 1.40f;
        private const float AmplificationPerPart = 0.10f;
        private const int WraithsPerPart = 3;

        private int _partsBroken;
        private Action<EventContext> _handler;

        public OsirisImperfectResurrectionEffect() : base(0) { }

        public override void OnApply()
        {
            if (Target == null) return;
            _handler = _ => CheckPartBreak();
            Target.AddListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _handler);
            // 최대 체력을 따로 채우지 않는다. AttributesUpdate가 체력 '비율'을 보존하므로
            // 가득 찬 상태에서 ×14가 붙으면 현재 체력도 함께 14배가 된다.
        }

        public override void OnRemove()
        {
            if (Target == null || _handler == null) return;
            Target.RemoveListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _handler);
            _handler = null;
        }

        public override float MaxHpMultiplierModifier(Unit unit) => unit == Target ? PartCount : 1f;

        /// <summary>체력 바를 14칸으로 끊어 보여 준다. 부위 파괴가 눈에 보이게 하는 유일한 단서다.</summary>
        public override int HpSegmentCount(Unit unit) => unit == Target ? PartCount : 0;

        public override float ReceivingDamageModifier(Unit unit)
        {
            if (unit != Target) return 1f;
            float amplification = Mathf.Max(0f, StartingAmplification - _partsBroken * AmplificationPerPart);
            return 1f + amplification;
        }

        private void CheckPartBreak()
        {
            if (Target == null || !Target.isActive || Target.HpMax <= 0) return;

            float partSize = Target.HpMax / (float)PartCount;
            if (partSize <= 0f) return;

            int broken = Mathf.Clamp(
                Mathf.FloorToInt((Target.HpMax - Target.HpCurr) / partSize), 0, PartCount);
            if (broken <= _partsBroken) return;

            int newlyBroken = broken - _partsBroken;
            _partsBroken = broken;
            Target.RefreshAttributes();

            AswanCombat.SummonWraiths(Target, newlyBroken * WraithsPerPart);
            Debug.Log($"[불완전한 부활] {Target.UnitName}의 부위 {_partsBroken}/{PartCount}이(가) 파괴되었습니다.");
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 아문·라
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 365 승천 — 적을 공격한 행동마다 1스택을 얻고 피해를 받으면 1스택을 잃는다.
    /// 스택 하나당 치명타 피해 +5%, 가하는 피해 +5%.
    ///
    /// '행동마다 1개'이므로 한 행동이 여러 대상을 때려도 한 번만 쌓인다.
    /// 스케줄러의 행동 카운트를 표식으로 삼아 같은 행동에서 두 번 세지 않는다.
    /// </summary>
    public sealed class AmunRaAscension : PassiveCode
    {
        public AmunRaAscension(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "승천";
            IgnoresActivationChance = true;
            Transferable = false;
        }

        public override void CastCode()
        {
            if (Caster == null) return;
            Caster.SetCombatResourceMaximum(AswanCombat.AscensionResource, AswanCombat.AscensionMax, true);
            Caster.AddStatus(BuffStatus.Create(
                AswanStatusIds.Ascension, "amunra_ascension", CodeName,
                Caster, Caster, new AmunRaAscensionEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                category: BaseEnums.StatusCategory.Neutral,
                isBeneficial: true,
                description: "'승천' 스택 하나당 치명타 피해 +5%, 가하는 피해 +5%. 피해를 받으면 1스택을 잃습니다."));
        }
    }

    internal sealed class AmunRaAscensionEffect : BaseEffect
    {
        private Action<DamageResolvedContext> _dealtHandler;
        private Action<EventContext> _takenHandler;
        private int _lastCountedAction = -1;

        public AmunRaAscensionEffect() : base(0) { }

        private int Stacks => Target?.GetCombatResource(AswanCombat.AscensionResource) ?? 0;

        public override void OnApply()
        {
            if (Target == null) return;

            _dealtHandler = context =>
            {
                if (context == null || context.DamageDealt <= 0 || Target == null) return;

                int actionCount = GameManager.Instance?.ActionScheduler?.ActionCount ?? 0;
                if (actionCount == _lastCountedAction) return;
                _lastCountedAction = actionCount;

                Target.AddCombatResource(AswanCombat.AscensionResource, 1);
                Target.RefreshAttributes();
            };

            _takenHandler = context =>
            {
                if (context?.DmgCtx == null || context.DmgCtx.IsCancelled || Target == null) return;
                if (Stacks <= 0) return;
                Target.AddCombatResource(AswanCombat.AscensionResource, -1);
                Target.RefreshAttributes();
            };

            Target.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _dealtHandler);
            Target.AddListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _takenHandler);
        }

        public override void OnRemove()
        {
            if (Target == null) return;
            if (_dealtHandler != null) Target.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _dealtHandler);
            if (_takenHandler != null) Target.RemoveListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _takenHandler);
            _dealtHandler = null;
            _takenHandler = null;
        }

        public override float CritMultiplierAdditiveModifier(Unit unit)
            => unit == Target ? Stacks * 0.05f : 0f;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target ? 1f + Stacks * 0.05f : 1f;
    }

    /// <summary>
    /// 368 초월 — 전투 시간 15초가 지나면 최대 체력이 20% 늘고 늘어난 만큼 회복한다.
    ///
    /// 🔸 원문은 '전투 후 15초 후'다. 전투가 턴제로 바뀐 뒤 초 축은 남아 있지 않으므로
    ///    스케줄러의 <b>전투 시간</b>(AV가 흐른 만큼만 늘어나는 축)으로 읽었다.
    /// </summary>
    public sealed class AmunRaTranscendence : PassiveCode
    {
        public AmunRaTranscendence(PassiveCodeContext context) : base(context)
        { CodeType = BaseEnums.CodeType.Passive; CodeName = "초월"; IgnoresActivationChance = true; }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            AswanStatusIds.Transcendence, "amunra_transcendence", CodeName,
            Caster, Caster, new AmunRaTranscendenceEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore, isBeneficial: true,
            description: "전투를 8턴 치르면 최대 체력이 20% 증가하고 그만큼 회복합니다."));
    }

    internal sealed class AmunRaTranscendenceEffect : BaseEffect
    {
        private const int TriggerTurns = 8;
        private const float HpBonus = 0.20f;

        private bool _triggered;
        private int _turns;

        public AmunRaTranscendenceEffect() : base(0) { }

        public override float MaxHpMultiplierModifier(Unit unit)
            => unit == Target && _triggered ? 1f + HpBonus : 1f;

        public override void OnOwnerTurn()
        {
            if (_triggered || Target == null || !Target.isActive) return;
            if (++_turns < TriggerTurns) return;

            int hpMaxBefore = Target.HpMax;
            int hpBefore = Target.HpCurr;
            _triggered = true;
            Target.RefreshAttributes();

            // AttributesUpdate는 체력 '비율'을 보존한다. 기획은 '늘어난 최대 체력만큼' 회복이므로
            // 비율 보정분에 얹지 않고 절대값으로 다시 맞춘다.
            int gain = Mathf.Max(0, Target.HpMax - hpMaxBefore);
            Target.ModifyHp(Mathf.Min(Target.HpMax, hpBefore + gain), Target);
            Debug.Log($"[초월] {Target.UnitName}의 최대 체력이 늘었습니다: {hpMaxBefore} → {Target.HpMax}");
        }
    }

    /// <summary>369 영혼 흡수 — 적을 처치하면 최대 체력의 20%를 회복한다.</summary>
    public sealed class AmunRaSoulDrain : PassiveCode
    {
        public AmunRaSoulDrain(PassiveCodeContext context) : base(context)
        { CodeType = BaseEnums.CodeType.Passive; CodeName = "영혼 흡수"; IgnoresActivationChance = true; }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            AswanStatusIds.SoulDrain, "amunra_soul_drain", CodeName,
            Caster, Caster, new AmunRaSoulDrainEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore, isBeneficial: true,
            description: "적을 처치하면 최대 체력의 20%를 회복합니다."));
    }

    internal sealed class AmunRaSoulDrainEffect : BaseEffect
    {
        private Action<EventContext> _handler;

        public AmunRaSoulDrainEffect() : base(0) { }

        public override void OnApply()
        {
            if (Target == null) return;
            _handler = _ =>
            {
                if (Target == null || !Target.isActive) return;
                Target.ModifyHp(Target.HpCurr + Mathf.Max(1, Mathf.RoundToInt(Target.HpMax * 0.20f)), Target);
            };
            Target.AddListener(BaseEnums.UnitEventType.OnKill, _handler);
        }

        public override void OnRemove()
        {
            if (Target == null || _handler == null) return;
            Target.RemoveListener(BaseEnums.UnitEventType.OnKill, _handler);
            _handler = null;
        }
    }

    /// <summary>
    /// 370 지옥불 — 대체행동을 쓴 뒤 '승천' 스택 수만큼 무작위 단일 적에게 칼날을 더 날린다.
    /// 표식 상태이며, 실제 발사는 대체행동(<c>AmunRaSubstituteNormal</c>)이 끝낼 때 처리한다.
    /// </summary>
    public sealed class AmunRaHellfire : PassiveCode
    {
        public AmunRaHellfire(PassiveCodeContext context) : base(context)
        { CodeType = BaseEnums.CodeType.Passive; CodeName = "지옥불"; IgnoresActivationChance = true; }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            AswanStatusIds.Hellfire, "amunra_hellfire", CodeName,
            Caster, Caster, new MarkerBuffEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore, isBeneficial: true,
            description: "대체행동 이후 '승천' 스택만큼 무작위 적에게 칼날을 추가로 발사합니다."));
    }
}
