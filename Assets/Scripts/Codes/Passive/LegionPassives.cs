using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    internal static class LegionCodeNames
    {
        public static string Passive(int codeId)
        {
            return codeId switch
            {
                1200 => "방진 편성",
                1201 => "중열의 방벽",
                1202 => "최후의 방벽",
                1203 => "백인대의 방벽",
                1204 => "산병 지원",
                1205 => "조준 사격",
                1206 => "기병의 회피",
                1207 => "부관의 신호",
                1208 => "천부장의 신호",
                300 => "화합",
                301 => "천천히 서둘러라",
                302 => "왔노라, 보았노라, 이겼노라",
                1212 => "최고의 2인자",
                1213 => "팍스 로마나",
                1214 => "갈리아의 정복자",
                1215 => "대범한 관용",
                1216 => "위대한 전술가",
                1217 => "독재관",
                _ => "군단 전투술",
            };
        }
    }

    /// <summary>
    /// '로마' 테마 적군(레기온)의 고유 패시브를 한 곳에서 구성한다.
    /// 콜로세움과 같은 구조 — 데이터의 코드 ID로 분기하고, 반복되는 조건부 강화는 공용 Effect로 처리한다.
    ///
    /// 300~311은 각 병종의 P 슬롯, 312~317은 <b>보스로 등장할 때만</b> 붙는 추가 패시브다.
    /// </summary>
    public sealed class LegionPassive : PassiveCode
    {
        private readonly int _codeId;

        public LegionPassive(PassiveCodeContext context, int codeId, string codeName) : base(context)
        {
            _codeId = codeId;
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = codeName;
            IgnoresActivationChance = true;
            Transferable = false;
        }

        public override void CastCode()
        {
            if (Caster == null) return;

            switch (_codeId)
            {
                // ── 전열 중장보병 — 전투 시작 시 보호막 ──────────────
                // 보호막 위력 = 고정값 + CON × 계수. 사다리를 따라 고정값과 계수가 함께 오른다.
                case 1200:
                    AddSelf(new RoundStartShieldEffect(200, 1.0f));
                    break;
                case 1201:
                    AddSelf(new RoundStartShieldEffect(225, 1.1f));
                    break;
                case 1202:
                    AddSelf(new RoundStartShieldEffect(250, 1.2f));
                    break;
                case 1203:
                    AddSelf(new RoundStartShieldEffect(300, 1.4f));
                    break;

                // ── 후열 사수 — 전열 아군 수에 비례한 화력 ───────────
                case 1204:
                    AddSelf(new FrontLineAllyDamageEffect(0.05f));
                    break;
                case 1205:
                    AddSelf(new FrontLineAllyDamageEffect(0.10f));
                    break;

                // ── 에퀴테스 ─────────────────────────────────────────
                case 1206:
                    AddSelf(new ContactEvasionEffect());
                    break;

                // ── 옵티오 · 트리뷴 ──────────────────────────────────
                case 1207:
                case 1208:
                    AddSelf(new SignalMarkEffect());
                    break;

                // ── 아그리파 ─────────────────────────────────────────
                case 300:
                    AddSelf(new ConcordiaEffect());
                    break;

                // ── 옥타비아 ─────────────────────────────────────────
                case 301:
                    AddSelf(new FestinaLenteEffect());
                    break;

                // ── 카이사르 ─────────────────────────────────────────
                case 302:
                    AddSelf(new VeniVidiViciEffect());
                    break;

                // ── 보스 전용 ────────────────────────────────────────
                case 1212:
                    AddSelf(new SecondInCommandEffect());
                    break;
                case 1213:
                    AddSelf(new PaxRomanaEffect());
                    break;
                case 1214:
                    AddSelf(new ConquerorOfGaulEffect(0.9f));
                    break;
                case 1215:
                    AddSelf(new ClementiaEffect());
                    break;
                case 1216:
                    AddSelf(new GrandTacticianEffect());
                    break;
                case 1217:
                    // 궁극기 지속 턴 +1은 카이사르 궁극기가 코드 보유 여부를 읽어 처리한다.
                    break;
            }
        }

        private void AddSelf(BaseEffect effect)
        {
            ColosseumCombat.AddStatus(
                Caster,
                Caster,
                6000 + _codeId,
                $"legion_passive_{_codeId}",
                CodeName,
                effect);
        }
    }

    /// <summary>레기온 공용 도우미. 전투 판정은 <see cref="ColosseumCombat"/>과 공유한다.</summary>
    internal static class LegionCombat
    {
        public const string ConcordiaKey = "legion_concordia";

        /// <summary>'로마' 태그를 가진 같은 편. 카이사르의 진영 버프가 대상을 고르는 기준이다.</summary>
        public static List<Unit> RomanAllies(Unit caster)
        {
            return ColosseumCombat.Allies(caster)
                .Where(unit => unit.HasUnitTag("Rome"))
                .ToList();
        }

        public static List<Unit> RomanFrontLineAllies(Unit caster)
        {
            return RomanAllies(caster).Where(IsFrontLine).ToList();
        }

        public static bool IsFrontLine(Unit unit)
        {
            return unit?.currentCell != null && Managers.GridManager.Instance != null &&
                   unit.currentCell.xPos == Managers.GridManager.Instance.GetFrontColumn(unit.IsEnemy);
        }

        public static int FrontLineAllyCount(Unit caster)
        {
            return ColosseumCombat.Allies(caster).Count(IsFrontLine);
        }

        /// <summary>단일 대상 피해인가. 부관의 신호·위대한 전술가의 발동 조건이다.</summary>
        public static bool IsSingleTargetDamage(DamageContext context)
        {
            return context?.DamageTags != null && context.DamageTags.Contains(DamageTag.SingleTarget);
        }

        /// <summary>
        /// '추가공격으로 일반공격'을 예약한다. 실제 일반공격 코루틴 대신
        /// 그 유닛의 일반공격 위력으로 한 방을 넣는다 — 연출 없이 판정만 같게 두기 위해서다.
        /// </summary>
        public static void EnqueueExtraNormalAttack(Unit attacker, Unit target, string key, string label)
        {
            if (attacker == null || target == null || !attacker.isActive || !target.isActive) return;

            Managers.GameManager.Instance?.ActionScheduler.EnqueueAdditional(
                attacker, key, label, () => ExtraNormalAttack(attacker, target));
        }

        private static void ExtraNormalAttack(Unit attacker, Unit target)
        {
            if (attacker == null || target == null || !attacker.isActive || !target.isActive) return;

            int power = attacker.ActiveNormalCode?.CurrentPower ?? 50;
            bool isCrit = UnityEngine.Random.value <= attacker.CritChanceCurr;
            float critMultiplier = isCrit ? attacker.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(attacker.SkillDamage(power) * critMultiplier));

            target.TakeDamage(new DamageContext(
                attacker,
                damage,
                BaseEnums.CodeType.Normal,
                new List<int>
                {
                    DamageTag.SingleTarget,
                    DamageTag.NormalAttack,
                    DamageTag.AdditionalAttack,
                    DamageTag.Physical,
                    DamageTag.ContactAttack,
                },
                isCrit));
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 효과
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 전투 시작 시 보호막. 라운드마다 한 번만 붙는다.
    /// 보호막 위력 = <c>고정값 + CON × 계수</c>이고, 실제 양은 그 위력에 CON을 다시 곱해 낸다.
    /// </summary>
    internal sealed class RoundStartShieldEffect : BaseEffect
    {
        private readonly int _flatPower;
        private readonly float _conCoefficient;

        public RoundStartShieldEffect(int flatPower, float conCoefficient) : base(0, flatPower)
        {
            _flatPower = flatPower;
            _conCoefficient = conCoefficient;
        }

        public override void OnApply()
        {
            if (Target == null || !Target.isActive) return;

            int con = Target.GetBasePrimaryStat(BaseEnums.PrimaryStat.CON);
            int power = _flatPower + Mathf.RoundToInt(con * _conCoefficient);
            Target.AddShield(
                Mathf.Max(1, Target.SkillDamage(power, BaseEnums.PrimaryStat.CON)),
                Caster ?? Target);
        }
    }

    /// <summary>필드의 전열 아군 수에 비례해 주는 피해가 늘어난다.</summary>
    internal sealed class FrontLineAllyDamageEffect : BaseEffect
    {
        private readonly float _perAlly;

        public FrontLineAllyDamageEffect(float perAlly) : base(0, perAlly) => _perAlly = perAlly;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target) return 1f;
            return 1f + LegionCombat.FrontLineAllyCount(Target) * _perAlly;
        }
    }

    /// <summary>접촉 기술만 LUK% 확률로 회피한다. 회피율은 최종 LUK에서 매번 다시 읽는다.</summary>
    internal sealed class ContactEvasionEffect : BaseEffect
    {
        public ContactEvasionEffect() : base(0) { }

        public override float EvasionChanceAdditiveModifier(Unit unit, DamageContext context)
        {
            if (unit != Target || context?.DamageTags == null) return 0f;
            if (!context.DamageTags.Contains(DamageTag.ContactAttack)) return 0f;
            return Mathf.Clamp01(Target.GetBaseLuk() * 0.01f);
        }
    }

    /// <summary>
    /// 부관의 신호 — 전투 시작 시 CON+STR이 가장 높은 전열 아군을 지정한다.
    /// 지정된 아군이 단일 대상 피해를 주면 같은 대상에게 추가공격으로 일반공격을 넣는다.
    /// </summary>
    internal sealed class SignalMarkEffect : BaseEffect
    {
        private Unit _marked;
        private Action<DamageResolvedContext> _handler;

        public SignalMarkEffect() : base(0) { }

        public override void OnApply()
        {
            _marked = ColosseumCombat.Allies(Caster ?? Target)
                .Where(LegionCombat.IsFrontLine)
                .OrderByDescending(unit => unit.GetBaseCon() + unit.GetBaseStr())
                .FirstOrDefault();
            if (_marked == null) return;

            _handler = context =>
            {
                if (context?.Attacker != _marked || context.Target == null) return;
                if (context.DamageContext == null) return;
                // 추가공격이 다시 추가공격을 부르지 않도록 막는다.
                if (context.DamageContext.DamageTags != null &&
                    context.DamageContext.DamageTags.Contains(DamageTag.AdditionalAttack)) return;
                if (!LegionCombat.IsSingleTargetDamage(context.DamageContext)) return;

                LegionCombat.EnqueueExtraNormalAttack(
                    _marked, context.Target, "legion_signal", "부관의 신호");
            };
            _marked.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _handler);
        }

        public override void OnRemove()
        {
            if (_marked != null && _handler != null)
            {
                _marked.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _handler);
            }
        }
    }

    /// <summary>
    /// 화합(Concordia) — 아그리파가 단일 아군에게 이로운 효과를 주면 그 아군에게 '화합' 2스택을 준다.
    /// 화합 보유자는 가하는 피해 +25%. 아그리파의 턴마다 1스택 줄고, 새로 부여하면 이전 보유자가 잃는다.
    /// </summary>
    internal sealed class ConcordiaEffect : BaseEffect
    {
        private const int MaxStacks = 2;
        private const float DamageBonus = 0.25f;

        private Unit _holder;
        private int _stacks;
        private Action<EventContext> _handler;

        public ConcordiaEffect() : base(0, DamageBonus) { }

        public override void OnApply()
        {
            // OnBeneficialEffectGranted는 Grantee = 부여자, Grantor = 받은 쪽으로 발행된다.
            _handler = context =>
            {
                if (context == null || context.Grantee != Target) return;
                Unit receiver = context.Grantor;
                if (receiver == null || receiver == Target || !receiver.isActive) return;
                Grant(receiver);
            };
            Target.AddListener(BaseEnums.UnitEventType.OnBeneficialEffectGranted, _handler);
        }

        /// <summary>아그리파의 턴마다 1스택 줄어든다.</summary>
        public override void OnOwnerTurn()
        {
            if (_holder == null) return;

            _stacks--;
            if (_stacks > 0) return;
            Clear();
        }

        private void Grant(Unit receiver)
        {
            if (_holder != null && _holder != receiver) Clear();

            _holder = receiver;
            _stacks = MaxStacks;
            ColosseumCombat.AddStatus(
                Target, receiver, 6309, LegionCombat.ConcordiaKey, "화합",
                new ConcordiaDamageEffect(DamageBonus),
                description: "가하는 피해가 25% 증가합니다.");
        }

        private void Clear()
        {
            _holder?.RemoveStatusByKey(LegionCombat.ConcordiaKey);
            _holder = null;
            _stacks = 0;
        }

        public override void OnRemove()
        {
            Clear();
            Target?.RemoveListener(BaseEnums.UnitEventType.OnBeneficialEffectGranted, _handler);
        }
    }

    internal sealed class ConcordiaDamageEffect : BaseEffect
    {
        private readonly float _bonus;
        public ConcordiaDamageEffect(float bonus) : base(0, bonus) => _bonus = bonus;
        public override bool IsBeneficial => true;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target ? 1f + _bonus : 1f;
    }

    /// <summary>
    /// 천천히 서둘러라(Festina lente) — 자신의 DEX −25%, 궁극기로 가하는 피해 +25%.
    ///
    /// 느려지는 대신 한 방이 커진다. DEX가 행동 속도를 전담하므로 턴이 덜 돌아오고,
    /// 궁극기 자원은 행동 횟수가 아니라 전투 시간에 비례해 차므로 손해가 상쇄된다.
    /// '저스핏 고화력 궁극기 딜러'라는 컨셉이 이 한 쌍으로 성립한다.
    /// </summary>
    internal sealed class FestinaLenteEffect : BaseEffect
    {
        private const float DexMultiplier = 0.75f;
        private const float UltimateDamageMultiplier = 1.25f;

        public FestinaLenteEffect() : base(0, UltimateDamageMultiplier) { }

        public override float PrimaryStatMultiplierModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Target && stat == BaseEnums.PrimaryStat.DEX ? DexMultiplier : 1f;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target || context == null) return 1f;
            bool isUltimate = context.CodeType == BaseEnums.CodeType.Ultimate ||
                              (context.DamageTags != null && context.DamageTags.Contains(DamageTag.UltAttack));
            return isUltimate ? UltimateDamageMultiplier : 1f;
        }
    }

    /// <summary>
    /// 왔노라, 보았노라, 이겼노라 — 적을 처치할 때마다 모든 '로마' 아군의 가하는 피해 +10%.
    /// 자기과신과 같은 방식으로 무제한 중첩된다.
    /// </summary>
    internal sealed class VeniVidiViciEffect : BaseEffect
    {
        private const float PerKill = 0.10f;

        private int _stacks;
        private Action<EventContext> _handler;

        public VeniVidiViciEffect() : base(0, PerKill) { }

        public override void OnApply()
        {
            _handler = _ => _stacks++;
            Target.AddListener(BaseEnums.UnitEventType.OnKill, _handler);
        }

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (_stacks <= 0 || attacker == null) return 1f;
            if (attacker != Target && !(attacker.HasUnitTag("Rome") && attacker.IsEnemy == Target.IsEnemy)) return 1f;
            return 1f + _stacks * PerKill;
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnKill, _handler);
        }
    }

    /// <summary>
    /// 최고의 2인자 — 필드에 옥타비아가 있으면 아그리파의 일반공격이 무조건 옥타비아를 향하고,
    /// 추가로 옥타비아의 행동 게이지를 100% 채운다(= 다음 행동을 즉시 당긴다).
    /// 지명 자체는 아그리파의 일반공격 코드(<c>LegionAttack.SecondInCommandTarget</c>)가 처리한다.
    /// </summary>
    internal sealed class SecondInCommandEffect : BaseEffect
    {
        public const int OctaviaEnemyId = 2007;

        public SecondInCommandEffect() : base(0) { }

        /// <summary>일반공격이 옥타비아를 지목했을 때 아그리파의 코드가 부른다.</summary>
        public static void PushForward(Unit octavia)
        {
            if (octavia == null) return;
            Managers.GameManager.Instance?.ActionScheduler.AdvanceAction(octavia, 1f);
        }
    }

    /// <summary>팍스 로마나 — 적중한 대상 수 하나당 최대 마나의 2%를 회복한다.</summary>
    internal sealed class PaxRomanaEffect : BaseEffect
    {
        private const float PerTarget = 0.02f;

        private Action<DamageResolvedContext> _handler;

        public PaxRomanaEffect() : base(0, PerTarget) { }

        public override void OnApply()
        {
            _handler = context =>
            {
                if (context?.Attacker != Target || context.Target == null) return;
                Target.AddUltimateResource(Mathf.Max(1, Mathf.RoundToInt(Target.ManaMax * PerTarget)));
            };
            Target.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _handler);
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _handler);
        }
    }

    /// <summary>
    /// 대범한 관용(Clementia) — 적을 처치하고 자기 전열에 빈 자리가 있으면 센츄리온을 소환한다.
    /// 실제 개체로 스폰하므로 소환수(<c>Combat.Summons</c>)와 달리 칸을 차지한다.
    /// </summary>
    internal sealed class ClementiaEffect : BaseEffect
    {
        public const int CenturionEnemyId = 1034;

        private Action<EventContext> _handler;

        public ClementiaEffect() : base(0) { }

        public override void OnApply()
        {
            _handler = _ => Summon();
            Target.AddListener(BaseEnums.UnitEventType.OnKill, _handler);
        }

        private void Summon()
        {
            Managers.GridManager grid = Managers.GridManager.Instance;
            if (grid == null || Target == null || !Target.isActive) return;

            int frontColumn = grid.GetFrontColumn(Target.IsEnemy);
            var occupied = ColosseumCombat.Allies(Target)
                .Where(unit => unit.currentCell != null && unit.currentCell.xPos == frontColumn)
                .Select(unit => unit.currentCell.yPos)
                .ToHashSet();

            for (int y = 1; y <= 4; y++)
            {
                if (occupied.Contains(y)) continue;
                grid.SpawnUnit(frontColumn, y, Target.IsEnemy, CenturionEnemyId);
                return;
            }
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnKill, _handler);
        }
    }

    /// <summary>
    /// 갈리아의 정복자 — 자기 편 '로마' 아군에게 감면 상태를 나눠 준다.
    ///
    /// 받는 피해 보정은 <b>맞는 유닛 자신의 효과에서만</b> 질의되므로,
    /// 카이사르에게 붙은 효과 하나로 남을 지켜 줄 수는 없다. 그래서 아군마다 상태를 건다.
    /// 카이사르의 턴마다 다시 훑어 '대범한 관용'이 불러낸 센츄리온도 받아 간다.
    /// </summary>
    internal sealed class ConquerorOfGaulEffect : BaseEffect
    {
        private const int StatusId = 6314;

        private readonly float _multiplier;
        private readonly List<Unit> _granted = new();

        public ConquerorOfGaulEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;

        public override void OnApply() => Grant();

        public override void OnOwnerTurn() => Grant();

        private void Grant()
        {
            if (Target == null || !Target.isActive) return;

            foreach (Unit ally in LegionCombat.RomanAllies(Target))
            {
                if (_granted.Contains(ally)) continue;

                ColosseumCombat.AddStatus(
                    Target, ally, StatusId, $"legion_gallia_{Target.GetEntityId()}",
                    "갈리아의 정복자", new FrontLineDamageReductionEffect(_multiplier),
                    description: "전열에 있는 동안 받는 피해가 10% 감소합니다.");
                _granted.Add(ally);
            }
        }
    }

    /// <summary>전열에 서 있는 동안에만 받는 피해를 줄인다. 자리를 옮기면 자연히 꺼진다.</summary>
    internal sealed class FrontLineDamageReductionEffect : BaseEffect
    {
        private readonly float _multiplier;

        public override bool IsBeneficial => true;

        public FrontLineDamageReductionEffect(float multiplier) : base(0, multiplier)
            => _multiplier = multiplier;

        public override float ReceivingDamageModifier(Unit unit)
            => unit == Target && LegionCombat.IsFrontLine(unit) ? _multiplier : 1f;
    }

    /// <summary>
    /// 위대한 전술가 — 자기 전열의 '로마' 아군이 단일 대상 피해를 주면
    /// 그 아군이 같은 대상에게 추가공격으로 일반공격을 한 번 더 넣는다.
    /// </summary>
    internal sealed class GrandTacticianEffect : BaseEffect
    {
        private readonly List<Unit> _watched = new();
        private Action<DamageResolvedContext> _handler;

        public GrandTacticianEffect() : base(0) { }

        public override void OnApply()
        {
            _handler = context =>
            {
                if (context?.Attacker == null || context.Target == null || context.DamageContext == null) return;
                if (context.DamageContext.DamageTags != null &&
                    context.DamageContext.DamageTags.Contains(DamageTag.AdditionalAttack)) return;
                if (!LegionCombat.IsSingleTargetDamage(context.DamageContext)) return;
                if (!LegionCombat.IsFrontLine(context.Attacker)) return;

                LegionCombat.EnqueueExtraNormalAttack(
                    context.Attacker, context.Target, "legion_grand_tactician", "위대한 전술가");
            };

            Rescan();
        }

        /// <summary>
        /// 카이사르의 턴마다 감시 대상을 다시 훑는다.
        /// '대범한 관용'이 전투 도중 센츄리온을 불러내므로 시작 시점 명단만 봐서는 새 소환수를 놓친다.
        /// </summary>
        public override void OnOwnerTurn() => Rescan();

        private void Rescan()
        {
            if (Target == null || !Target.isActive) return;

            foreach (Unit ally in LegionCombat.RomanAllies(Target))
            {
                if (_watched.Contains(ally)) continue;
                ally.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _handler);
                _watched.Add(ally);
            }
        }

        public override void OnRemove()
        {
            foreach (Unit ally in _watched)
            {
                ally?.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _handler);
            }
            _watched.Clear();
        }
    }
}
