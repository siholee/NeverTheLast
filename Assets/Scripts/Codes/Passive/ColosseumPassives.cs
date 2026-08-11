using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Entities.Status;
using UnityEngine;

namespace Codes.Passive
{
    internal static class ColosseumCodeNames
    {
        public static string Passive(int codeId)
        {
            return codeId switch
            {
                140 => "노련한 방어",
                141 => "방패 뒤의 동료",
                142 => "버텨낸 보상",
                143 => "최후의 방패",
                144 => "갑주 파쇄",
                145 => "무게 실은 창끝",
                146 => "연속 압박",
                147 => "방패 살해자",
                148 => "강자 선호",
                149 => "갑옷 틈새",
                150 => "상처 추적",
                151 => "결투의 끝",
                152 => "느린 사냥",
                153 => "조여드는 와이어",
                154 => "끌려 나온 약점",
                155 => "마지막 투척",
                156 => "사냥개의 호흡",
                157 => "흔들리지 않는 투구",
                158 => "몰아붙이기",
                159 => "놓치지 않는다",
                160 => "피의 박자",
                161 => "깊은 상처",
                162 => "박수갈채",
                163 => "다음 상대",
                164 => "챔피언의 여유",
                165 => "세 번의 승부",
                166 => "월계관의 정화",
                167 => "결승의 투지",
                168 => "사냥꾼의 표식",
                169 => "채찍 견제",
                170 => "집중 사냥",
                171 => "다음 사냥감",
                176 => "몸이 풀리는군",
                177 => "노련한 검투사",
                178 => "목숨값",
                179 => "다시 한 판",
                _ => "콜로세움 전투술",
            };
        }
    }

    /// <summary>
    /// 콜로세움 적군의 패시브를 한 곳에서 구성한다.
    /// 데이터의 코드 ID는 서로 다르지만, 반복되는 조건부 강화는 공용 Effect로 처리한다.
    /// </summary>
    public sealed class ColosseumPassive : PassiveCode
    {
        private readonly int _codeId;

        public ColosseumPassive(PassiveCodeContext context, int codeId, string codeName) : base(context)
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
                // 무르밀로
                case 140:
                    AddSelf(new PeriodicShieldEffect(0.10f, 6f));
                    break;
                case 141:
                    foreach (Unit ally in ColosseumCombat.Allies(Caster).Where(unit => unit != Caster))
                    {
                        ColosseumCombat.AddStatus(
                            Caster, ally, 6141, $"colosseum_murmillo_guard_{Caster.GetHashCode()}",
                            "방패 뒤의 동료", new SourceAliveDamageReductionEffect(0.9f, true));
                    }
                    break;
                case 142:
                    AddSelf(new ShieldBreakHealingEffect(0.08f));
                    break;
                case 143:
                    AddSelf(new LowHealthPeriodicShieldEffect(0.12f, 6f, 3f, 0.3f));
                    break;

                // 호플로마쿠스
                case 144:
                    AddSelf(new ConditionalOutgoingDamageEffect((_, target, _) =>
                        target != null && target.ShieldCurr > 0 ? 1.2f : 1f));
                    break;
                case 145:
                    AddSelf(new DefenseIgnoreEffect(0.85f));
                    break;
                case 146:
                    AddSelf(new SameTargetRampEffect(0.08f, 4));
                    break;
                case 147:
                    AddSelf(new ShieldBreakCooldownEffect(2f));
                    break;

                // 트라엑스
                case 148:
                    AddSelf(new DefenseIgnoreEffect(0.7f));
                    break;
                case 149:
                    AddSelf(new ConditionalOutgoingDamageEffect((_, target, _) =>
                        target != null && target.ShieldCurr <= 0 ? 1.15f : 1f));
                    break;
                case 150:
                    AddSelf(new ConditionalOutgoingDamageEffect((_, target, _) =>
                        ColosseumCombat.HealthRatio(target) < 0.5f ? 1.25f : 1f));
                    break;
                case 151:
                    AddSelf(new ConditionalOutgoingDamageEffect((_, target, _) =>
                        ColosseumCombat.HealthRatio(target) < 0.2f ? 1.5f : 1f));
                    break;

                // 레티아리우스
                case 152:
                case 153:
                case 154:
                    // 표적 선택, 포획 지속시간, 포획 취약은 일반 코드가 코드 보유 여부를 읽어 처리한다.
                    break;
                case 155:
                    AddSelf(new DeathCaptureEffect());
                    break;

                // 세쿠토르
                case 156:
                    AddSelf(new CapturedTargetLifestealEffect(0.1f));
                    break;
                case 157:
                    AddSelf(new FirstControlImmunityEffect());
                    break;
                case 158:
                    AddSelf(new SameTargetRampEffect(0.08f, 4));
                    break;
                case 159:
                    AddSelf(new KillCooldownResetEffect());
                    break;

                // 디마카이루스
                case 160:
                    Caster.SetCombatResourceMaximum(ColosseumCombat.BloodRhythmResource, 6, true);
                    AddSelf(new BloodRhythmEffect());
                    break;
                case 161:
                case 163:
                    // 열상과 궁극기 연속 공격은 공격 코드에서 처리한다.
                    break;
                case 162:
                    AddSelf(new CriticalTempoEffect());
                    break;

                // 프리무스 팔루스 마르켈루스
                case 164:
                    AddSelf(new ChampionBalanceEffect());
                    break;
                case 165:
                    Caster.SetCombatResourceMaximum(ColosseumCombat.ChampionSequenceResource, 3, true);
                    break;
                case 166:
                    // 궁극기 시 해로운 효과 하나 제거.
                    break;
                case 167:
                    AddSelf(new LowHealthOffenseEffect(0.5f, 1.2f, 0.25f));
                    break;

                // 베스티아리우스 사비나
                case 168:
                    AddSelf(new SabinaMarkControllerEffect());
                    break;
                case 169:
                case 170:
                    // 공격력 감소와 표식 집중 공격은 액티브 코드에서 처리한다.
                    break;
                case 171:
                    AddSelf(new SabinaMarkControllerEffect(true));
                    break;

                // 스파르타쿠스
                case 176:
                    Caster.SetCombatResourceMaximum(ColosseumCombat.WarmUpResource, 5, true);
                    AddSelf(new SpartacusWarmUpEffect());
                    break;
                case 177:
                    AddSelf(new FirstControlImmunityEffect());
                    break;
                case 178:
                    AddSelf(new LowHealthSurvivalEffect(0.4f, 0.7f, 0.2f));
                    break;
                case 179:
                    AddSelf(new LastStandEffect(0.3f, false));
                    break;
            }
        }

        private void AddSelf(BaseEffect effect)
        {
            ColosseumCombat.AddStatus(
                Caster,
                Caster,
                6000 + _codeId,
                $"colosseum_passive_{_codeId}",
                CodeName,
                effect);
        }
    }

    internal static class ColosseumCombat
    {
        public const string CaptureKey = "colosseum_capture";
        public const string SabinaMarkKey = "colosseum_sabina_mark";
        public const string BloodRhythmResource = "colosseum_blood_rhythm";
        public const string ChampionSequenceResource = "colosseum_champion_sequence";
        public const string WarmUpResource = "colosseum_spartacus_warmup";

        public static List<Unit> Enemies(Unit caster)
        {
            return global::Target.GetAllEnemies(caster)
                .Where(unit => unit != null && unit.isActive && unit.HpCurr > 0)
                .ToList();
        }

        public static List<Unit> Allies(Unit caster)
        {
            return global::Target.GetAllAllies(caster)
                .Where(unit => unit != null && unit.isActive && unit.HpCurr > 0)
                .ToList();
        }

        public static float HealthRatio(Unit unit)
        {
            return unit == null || unit.HpMax <= 0 ? 1f : (float)unit.HpCurr / unit.HpMax;
        }

        public static bool HasPassive(Unit unit, int codeId)
        {
            return unit != null && unit.LearnedPassiveRecords.Any(record =>
                record != null && record.codeId == codeId);
        }

        public static bool IsCaptured(Unit unit)
        {
            return unit != null && unit.HasStatusKey(CaptureKey);
        }

        public static void AddStatus(
            Unit caster,
            Unit owner,
            int id,
            string key,
            string name,
            BaseEffect effect,
            float duration = -1f,
            BaseEnums.StatusCategory category = BaseEnums.StatusCategory.Positive,
            bool beneficial = true,
            BaseEnums.StatusStackPolicy stackPolicy = BaseEnums.StatusStackPolicy.Replace,
            string description = "")
        {
            if (owner == null || !owner.isActive) return;
            owner.AddStatus(BuffStatus.Create(
                id,
                key,
                name,
                caster,
                owner,
                effect,
                duration,
                stackPolicy,
                category,
                beneficial,
                description));
        }

        public static void ApplyCapture(Unit caster, Unit target, float duration, bool vulnerable)
        {
            BaseEffect effect = vulnerable
                ? new CompositeEffect(new CodeAccelerationEffect(-0.25f), new ColosseumReceivingDamageEffect(1.2f))
                : new CodeAccelerationEffect(-0.25f);
            AddStatus(
                caster,
                target,
                6252,
                CaptureKey,
                "포획",
                effect,
                duration,
                BaseEnums.StatusCategory.Negative,
                false,
                description: vulnerable
                    ? "코드 가속이 25% 감소하고 받는 피해가 20% 증가합니다."
                    : "코드 가속이 25% 감소합니다.");
        }

        public static void ApplyBleed(Unit caster, Unit target, float duration = 5f)
        {
            AddStatus(
                caster,
                target,
                6261,
                $"colosseum_bleed_{caster.GetHashCode()}",
                "열상",
                new BleedEffect(0.12f),
                duration,
                BaseEnums.StatusCategory.Negative,
                false,
                BaseEnums.StatusStackPolicy.Replace,
                "매초 시전자 공격력의 12%에 해당하는 물리 피해를 받습니다.");
        }

        public static void CleanseOneNegative(Unit target)
        {
            UnitStatus negative = target?.GetAllStatuses()
                .FirstOrDefault(status => status.Category == BaseEnums.StatusCategory.Negative);
            if (negative != null)
            {
                target.RemoveStatus(negative.StatusId);
            }
        }
    }

    internal sealed class CompositeEffect : BaseEffect
    {
        private readonly BaseEffect[] _effects;

        public CompositeEffect(params BaseEffect[] effects) : base(0)
        {
            _effects = effects ?? Array.Empty<BaseEffect>();
        }

        public override void OnApply()
        {
            foreach (BaseEffect effect in _effects)
            {
                effect.Caster = Caster;
                effect.Target = Target;
                effect.OnApply();
            }
        }

        public override void OnUpdate(float deltaTime)
        {
            foreach (BaseEffect effect in _effects) effect.OnUpdate(deltaTime);
        }

        public override void OnRemove()
        {
            foreach (BaseEffect effect in _effects) effect.OnRemove();
        }

        public override float ReceivingDamageModifier(Unit unit)
        {
            float result = 1f;
            foreach (BaseEffect effect in _effects) result *= effect.ReceivingDamageModifier(unit);
            return result;
        }

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            float result = 1f;
            foreach (BaseEffect effect in _effects) result *= effect.OutgoingDamageModifier(attacker, target, context);
            return result;
        }

        public override float DefenseStatMultiplierModifier(Unit attacker, Unit target, DamageContext context)
        {
            float result = 1f;
            foreach (BaseEffect effect in _effects) result *= effect.DefenseStatMultiplierModifier(attacker, target, context);
            return result;
        }

        public override float CodeAccelerationAdditiveModifier(Unit unit)
        {
            float result = 0f;
            foreach (BaseEffect effect in _effects) result += effect.CodeAccelerationAdditiveModifier(unit);
            return result;
        }
    }

    internal sealed class CodeAccelerationEffect : BaseEffect
    {
        private readonly float _amount;
        public CodeAccelerationEffect(float amount) : base(0, amount) => _amount = amount;
        public override float CodeAccelerationAdditiveModifier(Unit unit) => unit == Target ? _amount : 0f;
    }

    internal sealed class ColosseumReceivingDamageEffect : BaseEffect
    {
        private readonly float _multiplier;
        public ColosseumReceivingDamageEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;
        public override float ReceivingDamageModifier(Unit unit) => unit == Target ? _multiplier : 1f;
    }

    internal sealed class ConditionalOutgoingDamageEffect : BaseEffect
    {
        private readonly Func<Unit, Unit, DamageContext, float> _modifier;
        public ConditionalOutgoingDamageEffect(Func<Unit, Unit, DamageContext, float> modifier) : base(0)
            => _modifier = modifier;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && _modifier != null ? _modifier(attacker, target, context) : 1f;
    }

    internal sealed class DefenseIgnoreEffect : BaseEffect
    {
        private readonly float _defenseMultiplier;
        public DefenseIgnoreEffect(float defenseMultiplier) : base(0, defenseMultiplier)
            => _defenseMultiplier = defenseMultiplier;

        public override float DefenseStatMultiplierModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target ? _defenseMultiplier : 1f;
    }

    internal sealed class SourceAliveDamageReductionEffect : BaseEffect
    {
        private readonly float _multiplier;
        private readonly bool _rearOnly;

        public SourceAliveDamageReductionEffect(float multiplier, bool rearOnly) : base(0, multiplier)
        {
            _multiplier = multiplier;
            _rearOnly = rearOnly;
        }

        public override float ReceivingDamageModifier(Unit unit)
        {
            if (unit != Target || Caster == null || !Caster.isActive) return 1f;
            if (!_rearOnly || unit.currentCell == null || Managers.GridManager.Instance == null) return _multiplier;
            return unit.currentCell.xPos == Managers.GridManager.Instance.GetRearColumn(unit.IsEnemy)
                ? _multiplier
                : 1f;
        }
    }

    internal class PeriodicShieldEffect : BaseEffect
    {
        protected readonly float ShieldRatio;
        protected float Interval;
        protected float Elapsed;

        public PeriodicShieldEffect(float shieldRatio, float interval) : base(0, shieldRatio)
        {
            ShieldRatio = shieldRatio;
            Interval = interval;
        }

        public override void OnApply()
        {
            Elapsed = Interval;
        }

        public override void OnUpdate(float deltaTime)
        {
            if (Target == null || !Target.isActive) return;
            Elapsed += deltaTime;
            if (Elapsed < CurrentInterval()) return;
            Elapsed = 0f;
            Target.AddShield(Mathf.Max(1, Mathf.RoundToInt(Target.HpMax * ShieldRatio)), Caster ?? Target);
        }

        protected virtual float CurrentInterval() => Interval;
    }

    internal sealed class LowHealthPeriodicShieldEffect : PeriodicShieldEffect
    {
        private readonly float _lowInterval;
        private readonly float _threshold;

        public LowHealthPeriodicShieldEffect(
            float shieldRatio,
            float normalInterval,
            float lowInterval,
            float threshold) : base(shieldRatio, normalInterval)
        {
            _lowInterval = lowInterval;
            _threshold = threshold;
        }

        protected override float CurrentInterval()
            => ColosseumCombat.HealthRatio(Target) < _threshold ? _lowInterval : Interval;
    }

    internal sealed class ShieldBreakHealingEffect : BaseEffect
    {
        private readonly float _healRatio;
        private int _shieldBefore;
        private Action<EventContext> _before;
        private Action<EventContext> _after;

        public ShieldBreakHealingEffect(float healRatio) : base(0, healRatio) => _healRatio = healRatio;

        public override void OnApply()
        {
            _before = _ => _shieldBefore = Target?.ShieldCurr ?? 0;
            _after = _ =>
            {
                if (_shieldBefore > 0 && Target != null && Target.ShieldCurr == 0)
                {
                    Target.ModifyHp(Target.HpCurr + Mathf.RoundToInt(Target.HpMax * _healRatio), Caster ?? Target);
                }
            };
            Target.AddListener(BaseEnums.UnitEventType.OnBeforeDamageTaken, _before);
            Target.AddListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _after);
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnBeforeDamageTaken, _before);
            Target?.RemoveListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _after);
        }
    }

    internal sealed class ShieldBreakCooldownEffect : BaseEffect
    {
        private readonly float _internalCooldown;
        private float _remaining;
        private Action<DamageResolvedContext> _handler;

        public ShieldBreakCooldownEffect(float internalCooldown) : base(0, internalCooldown)
            => _internalCooldown = internalCooldown;

        public override void OnApply()
        {
            _handler = context =>
            {
                if (_remaining > 0f || context?.Attacker != Target || context.Target == null) return;
                if (context.Target.ShieldCurr > 0) return;
                Target.normalCooldown = 0f;
                _remaining = _internalCooldown;
            };
            Target.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _handler);
        }

        public override void OnUpdate(float deltaTime) => _remaining = Mathf.Max(0f, _remaining - deltaTime);

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _handler);
        }
    }

    internal sealed class SameTargetRampEffect : BaseEffect
    {
        private readonly float _perStack;
        private readonly int _maxStacks;
        private Unit _lastTarget;
        private int _stacks;
        private Action<DamageResolvedContext> _handler;

        public SameTargetRampEffect(float perStack, int maxStacks) : base(0, perStack)
        {
            _perStack = perStack;
            _maxStacks = maxStacks;
        }

        public override void OnApply()
        {
            _handler = context =>
            {
                if (context?.Attacker != Target || context.Target == null) return;
                if (_lastTarget == context.Target)
                {
                    _stacks = Mathf.Min(_maxStacks, _stacks + 1);
                }
                else
                {
                    _lastTarget = context.Target;
                    _stacks = 1;
                }
            };
            Target.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _handler);
        }

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && target == _lastTarget ? 1f + _stacks * _perStack : 1f;

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _handler);
        }
    }

    internal sealed class CapturedTargetLifestealEffect : BaseEffect
    {
        private readonly float _ratio;
        private Action<DamageResolvedContext> _handler;

        public CapturedTargetLifestealEffect(float ratio) : base(0, ratio) => _ratio = ratio;

        public override void OnApply()
        {
            _handler = context =>
            {
                if (context?.Attacker != Target || context.DamageDealt <= 0 ||
                    !ColosseumCombat.IsCaptured(context.Target)) return;
                Target.ModifyHp(Target.HpCurr + Mathf.RoundToInt(context.DamageDealt * _ratio), Caster ?? Target);
            };
            Target.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _handler);
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _handler);
        }
    }

    internal sealed class FirstControlImmunityEffect : BaseEffect
    {
        private bool _used;
        private Action<EventContext> _handler;

        public FirstControlImmunityEffect() : base(0) { }

        public override void OnApply()
        {
            _handler = _ =>
            {
                if (_used || Target == null || !Target.isControlled) return;
                _used = true;
                Target.ControlEnds();
            };
            Target.AddListener(BaseEnums.UnitEventType.OnControlStarts, _handler);
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnControlStarts, _handler);
        }
    }

    internal sealed class FullControlImmunityEffect : BaseEffect
    {
        private Action<EventContext> _handler;

        public FullControlImmunityEffect() : base(0) { }

        public override void OnApply()
        {
            _handler = _ =>
            {
                if (Target != null && Target.isControlled)
                {
                    Target.ControlEnds();
                }
            };
            Target.AddListener(BaseEnums.UnitEventType.OnControlStarts, _handler);
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnControlStarts, _handler);
        }
    }

    internal sealed class KillCooldownResetEffect : BaseEffect
    {
        private Action<EventContext> _handler;

        public KillCooldownResetEffect() : base(0) { }

        public override void OnApply()
        {
            _handler = _ =>
            {
                if (Target == null) return;
                Target.normalCooldown = 0f;
                Target.ultimateCooldown = 0f;
            };
            Target.AddListener(BaseEnums.UnitEventType.OnKill, _handler);
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnKill, _handler);
        }
    }

    internal sealed class DeathCaptureEffect : BaseEffect
    {
        private Action<EventContext> _handler;

        public DeathCaptureEffect() : base(0) { }

        public override void OnApply()
        {
            _handler = _ =>
            {
                foreach (Unit enemy in ColosseumCombat.Enemies(Target)
                             .OrderBy(unit => ColosseumCombat.HealthRatio(unit)).Take(2))
                {
                    ColosseumCombat.ApplyCapture(Target, enemy, 4f, false);
                }
            };
            Target.AddListener(BaseEnums.UnitEventType.OnDeath, _handler);
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnDeath, _handler);
        }
    }

    internal sealed class BloodRhythmEffect : BaseEffect
    {
        private Unit _lastTarget;
        private Action<EventContext> _handler;

        public BloodRhythmEffect() : base(0) { }

        public override void OnApply()
        {
            _handler = context =>
            {
                Unit hitTarget = context?.Grantor;
                if (hitTarget == null) return;
                if (_lastTarget == hitTarget)
                {
                    Target.AddCombatResource(ColosseumCombat.BloodRhythmResource, 1);
                }
                else
                {
                    _lastTarget = hitTarget;
                    int current = Target.GetCombatResource(ColosseumCombat.BloodRhythmResource);
                    Target.AddCombatResource(ColosseumCombat.BloodRhythmResource, 1 - current);
                }
            };
            Target.AddListener(BaseEnums.UnitEventType.OnNormalAttackHit, _handler);
        }

        public override void OnUpdate(float deltaTime)
        {
            int stacks = Target?.GetCombatResource(ColosseumCombat.BloodRhythmResource) ?? 0;
            if (stacks > 0)
            {
                Target.normalCooldown = Mathf.Max(0f, Target.normalCooldown - deltaTime * stacks * 0.05f);
            }
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnNormalAttackHit, _handler);
        }
    }

    internal sealed class CriticalTempoEffect : BaseEffect
    {
        private Action<DamageResolvedContext> _handler;
        private float _internalCooldown;

        public CriticalTempoEffect() : base(0) { }

        public override void OnApply()
        {
            _handler = context =>
            {
                if (_internalCooldown > 0f || context?.Attacker != Target ||
                    context.DamageContext?.IsCrit != true) return;
                ColosseumCombat.AddStatus(
                    Target, Target, 6262, "colosseum_critical_tempo", "박수갈채",
                    new CodeAccelerationEffect(0.2f), 4f);
                _internalCooldown = 2f;
            };
            Target.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _handler);
        }

        public override void OnUpdate(float deltaTime)
            => _internalCooldown = Mathf.Max(0f, _internalCooldown - deltaTime);

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _handler);
        }
    }

    internal sealed class ChampionBalanceEffect : BaseEffect
    {
        public ChampionBalanceEffect() : base(0) { }

        public override float ReceivingDamageModifier(Unit unit)
            => unit == Target && ColosseumCombat.HealthRatio(unit) >= 0.5f ? 0.85f : 1f;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && ColosseumCombat.HealthRatio(attacker) < 0.5f ? 1.25f : 1f;
    }

    internal sealed class LowHealthOffenseEffect : BaseEffect
    {
        private readonly float _threshold;
        private readonly float _damageMultiplier;
        private readonly float _cooldownBonus;

        public LowHealthOffenseEffect(float threshold, float damageMultiplier, float cooldownBonus) : base(0)
        {
            _threshold = threshold;
            _damageMultiplier = damageMultiplier;
            _cooldownBonus = cooldownBonus;
        }

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && ColosseumCombat.HealthRatio(attacker) < _threshold
                ? _damageMultiplier
                : 1f;

        public override void OnUpdate(float deltaTime)
        {
            if (Target == null || ColosseumCombat.HealthRatio(Target) >= _threshold) return;
            Target.normalCooldown = Mathf.Max(0f, Target.normalCooldown - deltaTime * _cooldownBonus);
            Target.ultimateCooldown = Mathf.Max(0f, Target.ultimateCooldown - deltaTime * _cooldownBonus);
        }
    }

    internal sealed class SabinaMarkControllerEffect : BaseEffect
    {
        private readonly bool _reassign;
        private float _elapsed;
        private Unit _marked;

        public SabinaMarkControllerEffect(bool reassign = false) : base(0) => _reassign = reassign;

        public override void OnApply()
        {
            AssignMark();
        }

        public override void OnUpdate(float deltaTime)
        {
            if (!_reassign || (_marked != null && _marked.isActive)) return;
            _elapsed += deltaTime;
            if (_elapsed < 1f) return;
            _elapsed = 0f;
            AssignMark();
        }

        private void AssignMark()
        {
            _marked?.RemoveStatusByKey(ColosseumCombat.SabinaMarkKey);
            _marked = ColosseumCombat.Enemies(Target)
                .OrderByDescending(unit => unit.HpMax)
                .FirstOrDefault();
            if (_marked == null) return;
            ColosseumCombat.AddStatus(
                Target,
                _marked,
                6268,
                ColosseumCombat.SabinaMarkKey,
                "사냥꾼의 표식",
                new ColosseumReceivingDamageEffect(1.2f),
                -1f,
                BaseEnums.StatusCategory.Negative,
                false,
                description: "받는 피해가 20% 증가합니다.");
        }

        public override void OnRemove()
        {
            _marked?.RemoveStatusByKey(ColosseumCombat.SabinaMarkKey);
        }
    }

    internal sealed class LastStandEffect : BaseEffect
    {
        private readonly float _recovery;
        private readonly bool _fixedOneHp;
        private bool _used;
        private bool _recoveryPending;

        public LastStandEffect(float recovery, bool fixedOneHp) : base(0, recovery)
        {
            _recovery = recovery;
            _fixedOneHp = fixedOneHp;
        }

        public override bool TryPreventDeath(Unit unit, Unit attacker)
        {
            if (_used || unit != Target) return false;
            _used = true;
            _recoveryPending = !_fixedOneHp;
            unit.ultimateCooldown = 0f;
            unit.AddUltimateResource(unit.ManaMax);
            return true;
        }

        public override void OnUpdate(float deltaTime)
        {
            // Unit의 공통 사망 방지 처리가 해당 프레임 끝에 HP를 1로 고정하므로,
            // 비율 회복은 다음 상태 틱에서 적용한다.
            if (!_recoveryPending || Target == null || !Target.isActive) return;
            _recoveryPending = false;
            Target.ModifyHp(Mathf.Max(1, Mathf.RoundToInt(Target.HpMax * _recovery)), Caster ?? Target);
        }
    }

    internal sealed class LowHealthSurvivalEffect : BaseEffect
    {
        private readonly float _threshold;
        private readonly float _damageTaken;
        private readonly float _lifesteal;
        private Action<DamageResolvedContext> _handler;

        public LowHealthSurvivalEffect(float threshold, float damageTaken, float lifesteal) : base(0)
        {
            _threshold = threshold;
            _damageTaken = damageTaken;
            _lifesteal = lifesteal;
        }

        public override float ReceivingDamageModifier(Unit unit)
            => unit == Target && ColosseumCombat.HealthRatio(unit) < _threshold ? _damageTaken : 1f;

        public override void OnApply()
        {
            _handler = context =>
            {
                if (context?.Attacker != Target || context.DamageDealt <= 0 ||
                    ColosseumCombat.HealthRatio(Target) >= _threshold) return;
                Target.ModifyHp(Target.HpCurr + Mathf.RoundToInt(context.DamageDealt * _lifesteal), Caster ?? Target);
            };
            Target.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _handler);
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _handler);
        }
    }

    internal sealed class SpartacusWarmUpEffect : BaseEffect
    {
        private Action<DamageResolvedContext> _dealt;
        private Action<EventContext> _taken;
        private float _gainCooldown;

        public SpartacusWarmUpEffect() : base(0) { }

        public override void OnApply()
        {
            _dealt = context =>
            {
                if (context?.Attacker == Target) GainStack();
            };
            _taken = _ => GainStack();
            Target.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _dealt);
            Target.AddListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _taken);
        }

        public override void OnUpdate(float deltaTime)
        {
            _gainCooldown = Mathf.Max(0f, _gainCooldown - deltaTime);
            int stacks = Target?.GetCombatResource(ColosseumCombat.WarmUpResource) ?? 0;
            if (stacks <= 0) return;
            Target.normalCooldown = Mathf.Max(0f, Target.normalCooldown - deltaTime * stacks * 0.05f);
            if (ColosseumCombat.HealthRatio(Target) < 0.6f)
            {
                Target.normalCooldown = Mathf.Max(0f, Target.normalCooldown - deltaTime * 0.25f);
            }
        }

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target) return 1f;
            float phaseBonus = ColosseumCombat.HealthRatio(attacker) < 0.25f ? 1.2f : 1f;
            return phaseBonus;
        }

        private void GainStack()
        {
            if (_gainCooldown > 0f || Target == null) return;
            Target.AddCombatResource(ColosseumCombat.WarmUpResource, 1);
            _gainCooldown = 0.5f;
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _dealt);
            Target?.RemoveListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _taken);
        }
    }

    internal sealed class BleedEffect : BaseEffect
    {
        private readonly float _atkRatio;
        private float _elapsed;

        public BleedEffect(float atkRatio) : base(0, atkRatio) => _atkRatio = atkRatio;
        public override bool IsDamageOverTime => true;

        public override int EstimateDamagePerSecond()
            => Caster == null ? 0 : Mathf.Max(1, Caster.SkillDamage(Mathf.RoundToInt(_atkRatio * 50f)));

        public override void OnUpdate(float deltaTime)
        {
            if (Caster == null || Target == null || !Target.isActive) return;
            _elapsed += deltaTime;
            while (_elapsed >= 1f && Target.isActive)
            {
                _elapsed -= 1f;
                Target.TakeDamage(new DamageContext(
                    Caster,
                    EstimateDamagePerSecond(),
                    BaseEnums.CodeType.Effect,
                    new List<int> { DamageTag.Physical, DamageTag.ContactAttack }));
            }
        }
    }
}
