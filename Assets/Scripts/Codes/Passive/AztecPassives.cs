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
    internal static class AztecCodeNames
    {
        public static string Passive(int codeId)
        {
            return codeId switch
            {
                180 => "재규어 갑주",
                181 => "두꺼운 가죽",
                182 => "태양석 방패",
                183 => "무거운 일격",
                184 => "독수리의 눈",
                185 => "강한 투창",
                186 => "급강하",
                187 => "태양 관통",
                188 => "독 묻은 칼날",
                189 => "진한 독",
                190 => "오래 남는 독",
                191 => "독안개 농축",
                192 => "빠른 심장",
                193 => "민첩한 의식",
                194 => "강한 고동",
                195 => "긴 노래",
                196 => "밤의 지혜",
                197 => "깊은 밤",
                198 => "검은 깃 강화",
                199 => "밤의 파동 강화",
                200 => "민첩한 사냥꾼",
                201 => "빠른 발",
                202 => "연속 투창 강화",
                203 => "코요테의 손놀림",
                204 => "비를 부르는 자",
                205 => "폭우의 박자",
                206 => "거센 소나기",
                207 => "빗물 장막",
                208 => "금이 간 흡연경",
                209 => "어디에나 있는 그림자",
                210 => "재규어의 밤",
                211 => "신좌 없는 신",
                _ => "아즈텍 전투술",
            };
        }
    }

    /// <summary>
    /// 아즈텍 적 패시브. 일반 병종은 기본 능력치와 기존 코드만 강화하고,
    /// 별도 조건을 요구하는 전투 기믹은 테스카틀리포카에게만 둔다.
    /// </summary>
    public sealed class AztecPassive : PassiveCode
    {
        private readonly int _codeId;

        public AztecPassive(PassiveCodeContext context, int codeId, string codeName) : base(context)
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

            BaseEffect effect = _codeId switch
            {
                // 재규어 전사
                180 => new AztecPrimaryStatEffect(BaseEnums.PrimaryStat.CON, 4),
                181 => new AztecPrimaryStatEffect(BaseEnums.PrimaryStat.CON, 3),
                182 => new AztecShieldBonusEffect(0.25f),
                183 => new AztecCodeDamageEffect(BaseEnums.CodeType.Ultimate, 1.2f),

                // 독수리 전사
                184 => new AztecCritChanceEffect(0.08f),
                185 => new AztecPrimaryStatEffect(BaseEnums.PrimaryStat.STR, 3),
                186 => new AztecCritMultiplierEffect(0.2f),
                187 => new AztecDefenseIgnoreEffect(BaseEnums.CodeType.Ultimate, 0.75f),

                // 방울뱀 사제
                188 => null, // 독 부여는 일반 코드가 처리한다.
                189 => new AztecDotApplicationEffect(1.2f),
                190 => null, // 독 지속시간은 공격 코드가 패시브 보유 여부를 읽는다.
                191 => null, // 궁극기의 추가 중첩은 공격 코드가 처리한다.

                // 벌새 사제
                192 => new AztecCodeAccelerationEffect(0.12f),
                193 => new AztecPrimaryStatEffect(BaseEnums.PrimaryStat.DEX, 3),
                194 => null, // 버프 수치 강화는 공격 코드가 처리한다.
                195 => null, // 버프 지속시간 강화는 공격 코드가 처리한다.

                // 올빼미 주술사
                196 => new AztecPrimaryStatEffect(BaseEnums.PrimaryStat.INT, 3),
                197 => new AztecPrimaryStatEffect(BaseEnums.PrimaryStat.INT, 3),
                198 => new AztecCodeDamageEffect(BaseEnums.CodeType.Normal, 1.15f),
                199 => new AztecCodeDamageEffect(BaseEnums.CodeType.Ultimate, 1.2f),

                // 코요테 척후병
                200 => new AztecCodeAccelerationEffect(0.12f),
                201 => new AztecPrimaryStatEffect(BaseEnums.PrimaryStat.DEX, 3),
                202 => new AztecCodeDamageEffect(BaseEnums.CodeType.Normal, 1.15f),
                203 => null, // 궁극기 추가 타격은 공격 코드가 처리한다.

                // 틀랄록의 대사제
                204 => new AztecPrimaryStatEffect(BaseEnums.PrimaryStat.INT, 5),
                205 => new AztecCodeAccelerationEffect(0.15f),
                206 => new AztecCodeDamageEffect(BaseEnums.CodeType.Ultimate, 1.2f),
                207 => new AztecShieldBonusEffect(0.25f),

                // 테스카틀리포카
                208 => new SmokingMirrorEffect(),
                209 => new AztecFirstControlImmunityEffect(),
                210 => new JaguarNightEffect(),
                211 => new AztecDeathWardEffect(0.2f),
                _ => null,
            };

            if (effect != null)
            {
                AddSelf(effect);
            }
        }

        private void AddSelf(BaseEffect effect)
        {
            AztecCombat.AddStatus(
                Caster,
                Caster,
                7000 + _codeId,
                $"aztec_passive_{_codeId}",
                CodeName,
                effect);
        }
    }

    internal static class AztecCombat
    {
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

        public static void ApplyPoison(Unit caster, Unit target, float duration, float coefficient)
        {
            if (caster == null || target == null || !target.isActive) return;
            var poison = new UnitStatus(1, caster, target)
            {
                Duration = duration,
            };
            poison.AddEffect(1001, coefficient);
            target.AddStatus(poison);
        }
    }

    internal sealed class AztecPrimaryStatEffect : BaseEffect
    {
        private readonly BaseEnums.PrimaryStat _stat;
        private readonly int _amount;

        public AztecPrimaryStatEffect(BaseEnums.PrimaryStat stat, int amount) : base(0, amount)
        {
            _stat = stat;
            _amount = amount;
        }

        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Target && stat == _stat ? _amount : 0;
    }

    internal sealed class AztecCritChanceEffect : BaseEffect
    {
        private readonly float _amount;
        public AztecCritChanceEffect(float amount) : base(0, amount) => _amount = amount;
        public override float CritChanceAdditiveModifier(Unit unit) => unit == Target ? _amount : 0f;
    }

    internal sealed class AztecCritMultiplierEffect : BaseEffect
    {
        private readonly float _amount;
        public AztecCritMultiplierEffect(float amount) : base(0, amount) => _amount = amount;
        public override float CritMultiplierAdditiveModifier(Unit unit) => unit == Target ? _amount : 0f;
    }

    internal sealed class AztecCodeAccelerationEffect : BaseEffect
    {
        private readonly float _amount;
        public AztecCodeAccelerationEffect(float amount) : base(0, amount) => _amount = amount;
        public override float CodeAccelerationAdditiveModifier(Unit unit) => unit == Target ? _amount : 0f;
    }

    internal sealed class AztecShieldBonusEffect : BaseEffect
    {
        private readonly float _amount;
        public AztecShieldBonusEffect(float amount) : base(0, amount) => _amount = amount;
        public override float ShieldBonusAdditiveModifier(Unit unit) => unit == Target ? _amount : 0f;
    }

    internal sealed class AztecDotApplicationEffect : BaseEffect
    {
        private readonly float _multiplier;
        public AztecDotApplicationEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;
        public override float DamageOverTimeApplicationMultiplier(Unit unit)
            => unit == Target ? _multiplier : 1f;
    }

    internal sealed class AztecCodeDamageEffect : BaseEffect
    {
        private readonly BaseEnums.CodeType _codeType;
        private readonly float _multiplier;

        public AztecCodeDamageEffect(BaseEnums.CodeType codeType, float multiplier) : base(0, multiplier)
        {
            _codeType = codeType;
            _multiplier = multiplier;
        }

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && context != null && context.CodeType == _codeType ? _multiplier : 1f;
    }

    internal sealed class AztecDefenseIgnoreEffect : BaseEffect
    {
        private readonly BaseEnums.CodeType _codeType;
        private readonly float _defenseMultiplier;

        public AztecDefenseIgnoreEffect(BaseEnums.CodeType codeType, float defenseMultiplier)
            : base(0, defenseMultiplier)
        {
            _codeType = codeType;
            _defenseMultiplier = defenseMultiplier;
        }

        public override float DefenseStatMultiplierModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && context != null && context.CodeType == _codeType
                ? _defenseMultiplier
                : 1f;
    }

    /// <summary>5번째 피격마다 공격자에게 흑요석 반사 피해를 준다.</summary>
    internal sealed class SmokingMirrorEffect : BaseEffect
    {
        private Action<EventContext> _beforeHandler;
        private Action<EventContext> _afterHandler;
        private int _durabilityBefore;
        private int _hits;

        public SmokingMirrorEffect() : base(0) { }

        public override void OnApply()
        {
            _beforeHandler = _ =>
            {
                _durabilityBefore = Target == null ? 0 : Target.HpCurr + Target.ShieldCurr;
            };
            _afterHandler = context =>
            {
                Unit attacker = context?.Grantor;
                if (Target == null || !Target.isActive || attacker == null || !attacker.isActive || attacker == Target)
                    return;
                if (Target.HpCurr + Target.ShieldCurr >= _durabilityBefore) return;

                _hits++;
                if (_hits < 5) return;
                _hits = 0;

                int damage = Mathf.Max(1, Target.SkillDamage(28));
                attacker.TakeDamage(new DamageContext(
                    Target,
                    damage,
                    BaseEnums.CodeType.Passive,
                    new List<int>
                    {
                        DamageTag.SingleTarget,
                        DamageTag.Special,
                        DamageTag.NonContactAttack,
                    }));
            };
            Target.AddListener(BaseEnums.UnitEventType.OnBeforeDamageTaken, _beforeHandler);
            Target.AddListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _afterHandler);
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnBeforeDamageTaken, _beforeHandler);
            Target?.RemoveListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _afterHandler);
        }
    }

    internal sealed class AztecFirstControlImmunityEffect : BaseEffect
    {
        private bool _used;
        private Action<EventContext> _handler;

        public AztecFirstControlImmunityEffect() : base(0) { }

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

    /// <summary>체력 50% 아래에서 공격 속도·피해·흡혈을 얻고 대신 받는 피해가 증가한다.</summary>
    internal sealed class JaguarNightEffect : BaseEffect
    {
        private Action<DamageResolvedContext> _damageHandler;

        public JaguarNightEffect() : base(0) { }

        private bool IsActive => Target != null && AztecCombat.HealthRatio(Target) < 0.5f;

        public override void OnApply()
        {
            _damageHandler = context =>
            {
                if (!IsActive || context?.Attacker != Target || context.DamageDealt <= 0) return;
                Target.ModifyHp(Target.HpCurr + Mathf.RoundToInt(context.DamageDealt * 0.1f), Caster ?? Target);
            };
            Target.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _damageHandler);
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _damageHandler);
        }

        public override float CodeAccelerationAdditiveModifier(Unit unit)
            => unit == Target && IsActive ? 0.35f : 0f;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && IsActive ? 1.25f : 1f;

        public override float ReceivingDamageModifier(Unit unit)
            => unit == Target && IsActive ? 1.15f : 1f;
    }

    internal sealed class AztecDeathWardEffect : BaseEffect
    {
        private readonly float _recoveryRatio;
        private bool _used;
        private bool _pendingRecovery;

        public AztecDeathWardEffect(float recoveryRatio) : base(0, recoveryRatio)
        {
            _recoveryRatio = recoveryRatio;
        }

        public override bool TryPreventDeath(Unit unit, Unit attacker)
        {
            if (_used || unit != Target) return false;
            _used = true;
            _pendingRecovery = true;
            unit.ultimateCooldown = 0f;
            unit.AddUltimateResource(unit.ManaMax);
            return true;
        }

        public override void OnUpdate(float deltaTime)
        {
            if (!_pendingRecovery || Target == null || !Target.isActive) return;
            _pendingRecovery = false;
            Target.ModifyHp(Mathf.Max(1, Mathf.RoundToInt(Target.HpMax * _recoveryRatio)), Caster ?? Target);
        }
    }
}
