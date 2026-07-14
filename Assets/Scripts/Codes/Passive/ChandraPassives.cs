using System;
using System.Collections;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Entities;
using Managers;
using StatusEffects.Effects;
using UnityEngine;

namespace Codes.Passive
{
    public class ChandraNishakara : PassiveCode
    {
        public ChandraNishakara(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "니샤카라";
            MaxStage = 3;
            Transferable = false;
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster.AddStatusEffect("ChandraNishakara", new ChandraNishakaraEffect(Caster, GetRatio()));
        }

        public override void StopCode()
        {
            Caster.RemoveStatusEffect("ChandraNishakara");
        }

        private float GetRatio()
        {
            return Mathf.Clamp(CurrentStage, 1, MaxStage) switch
            {
                1 => 0.25f,
                2 => 0.5f,
                _ => 1f,
            };
        }
    }

    public class ChandraBastion : PassiveCode
    {
        public ChandraBastion(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "성채";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster.AddStatusEffect("ChandraBastion", new ShieldBonusEffect(Caster, "ChandraBastion", 0.25f));
        }
    }

    public class ChandraSecondWind : PassiveCode
    {
        private bool _isRegistered;
        private bool _triggered;
        private Action<EventContext> _damageHandler;

        public ChandraSecondWind(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "기사회생";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            _triggered = false;
            if (_isRegistered) return;

            _damageHandler = OnAfterDamageTaken;
            Caster.AddListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _damageHandler);
            _isRegistered = true;
        }

        public override void StopCode()
        {
            if (!_isRegistered) return;

            if (_damageHandler != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _damageHandler);
            }

            _damageHandler = null;
            _isRegistered = false;
            _triggered = false;
        }

        private void OnAfterDamageTaken(EventContext context)
        {
            if (_triggered || context.Grantee != Caster || !Caster.isActive || Caster.HpMax <= 0) return;
            if ((float)Caster.HpCurr / Caster.HpMax >= 0.25f) return;

            _triggered = true;
            Caster.StartCoroutine(HealOverTime());
        }

        private IEnumerator HealOverTime()
        {
            const float duration = 5f;
            float elapsed = 0f;
            float healed = 0f;
            float totalHeal = Caster.HpMax * 0.3f;

            while (elapsed < duration && Caster != null && Caster.isActive)
            {
                float delta = Time.deltaTime;
                elapsed += delta;
                float targetHealed = totalHeal * Mathf.Clamp01(elapsed / duration);
                int tickHeal = Mathf.RoundToInt(targetHealed - healed);
                healed += tickHeal;

                if (tickHeal > 0)
                {
                    Caster.ModifyHp(Caster.HpCurr + tickHeal);
                }

                yield return null;
            }
        }
    }

    public class ChandraBulwark : PassiveCode
    {
        private bool _isRegistered;
        private Action<EventContext> _beneficialHandler;

        public ChandraBulwark(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "보루";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (_isRegistered) return;

            _beneficialHandler = OnBeneficialEffectReceived;
            Caster.AddListener(BaseEnums.UnitEventType.OnBeneficialEffectReceived, _beneficialHandler);
            _isRegistered = true;
        }

        public override void StopCode()
        {
            if (!_isRegistered) return;

            if (_beneficialHandler != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnBeneficialEffectReceived, _beneficialHandler);
            }

            _beneficialHandler = null;
            _isRegistered = false;
        }

        private void OnBeneficialEffectReceived(EventContext context)
        {
            if (context.Grantee != Caster || !Caster.isActive) return;

            Caster.AddStatusEffect(
                "ChandraBulwarkStr",
                new PrimaryStatBonusEffect(Caster, "ChandraBulwarkStr", BaseEnums.PrimaryStat.STR, 4, 4f));
        }
    }

    public class ChandraLastStandFormation : PassiveCode
    {
        public ChandraLastStandFormation(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "배수의 진";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster.RemoveStatusEffect("ChandraLastStandFormation");
            if (!HasOtherFrontAlly())
            {
                Caster.AddStatusEffect(
                    "ChandraLastStandFormation",
                    new PrimaryStatBonusEffect(Caster, "ChandraLastStandFormation", BaseEnums.PrimaryStat.CON, 12));
            }
        }

        private bool HasOtherFrontAlly()
        {
            if (GridManager.Instance == null || Caster.currentCell == null) return false;

            int frontColumn = GridManager.Instance.GetFrontColumn(Caster.IsEnemy);
            return Target.GetAllAllies(Caster).Any(ally =>
                ally != null &&
                ally != Caster &&
                ally.isActive &&
                ally.currentCell != null &&
                ally.currentCell.xPos == frontColumn);
        }
    }

    public class ChandraPurificationBath : PassiveCode
    {
        private bool _isRegistered;
        private Action<EventContext> _roundEndHandler;

        public ChandraPurificationBath(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "목욕제계";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (_isRegistered) return;

            _roundEndHandler = OnRoundEnd;
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _roundEndHandler);
            _isRegistered = true;
        }

        public override void StopCode()
        {
            if (!_isRegistered) return;

            if (_roundEndHandler != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _roundEndHandler);
            }

            _roundEndHandler = null;
            _isRegistered = false;
        }

        private void OnRoundEnd(EventContext context)
        {
            if (context.Grantee != Caster || !Caster.isActive) return;

            float healRatio = Mathf.Max(0f, Caster.GetBaseCon()) * 0.01f;
            foreach (Unit ally in Target.GetAllAllies(Caster))
            {
                if (ally == null || !ally.isActive) continue;

                int healAmount = Mathf.RoundToInt(ally.HpMax * healRatio);
                ally.ModifyHp(ally.HpCurr + healAmount);
            }
        }
    }

    public class ChandraIronWall : PassiveCode
    {
        private const float TriggerTime = 6f;

        private bool _isRegistered;
        private bool _applied;
        private float _elapsed;
        private Action<EventContext> _updateHandler;
        private Action<EventContext> _roundEndHandler;

        public ChandraIronWall(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "철벽";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            _elapsed = 0f;
            _applied = false;
            Caster.RemoveStatusEffect("ChandraIronWall");
            if (_isRegistered) return;

            _updateHandler = OnUpdate;
            _roundEndHandler = OnRoundEnd;
            Caster.AddListener(BaseEnums.UnitEventType.OnUpdate, _updateHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _roundEndHandler);
            _isRegistered = true;
        }

        public override void StopCode()
        {
            if (!_isRegistered) return;

            if (_updateHandler != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnUpdate, _updateHandler);
            }
            if (_roundEndHandler != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _roundEndHandler);
            }

            _updateHandler = null;
            _roundEndHandler = null;
            _isRegistered = false;
            _elapsed = 0f;
            _applied = false;
        }

        private void OnUpdate(EventContext context)
        {
            if (_applied || context.Grantee != Caster || !Caster.isActive) return;

            _elapsed += context.FloatParam;
            if (_elapsed < TriggerTime) return;

            _applied = true;
            Caster.AddStatusEffect(
                "ChandraIronWall",
                new PrimaryStatBonusEffect(Caster, "ChandraIronWall", BaseEnums.PrimaryStat.CON, 6));
        }

        private void OnRoundEnd(EventContext context)
        {
            if (context.Grantee != Caster) return;

            _elapsed = 0f;
            _applied = false;
        }
    }

    public class ChandraLokapala : PassiveCode
    {
        public ChandraLokapala(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "로카팔라";
            Transferable = false;
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster.AddStatusEffect("Lokapala", new LokapalaEffect(Caster));
        }
    }

    public class ChandraMoonlight : PassiveCode
    {
        private const float ImbueDuration = 4f;

        private bool _isRegistered;
        private Action<EventContext> _beneficialGrantedHandler;

        public ChandraMoonlight(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "월광";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (_isRegistered) return;

            _beneficialGrantedHandler = OnBeneficialEffectGranted;
            Caster.AddListener(BaseEnums.UnitEventType.OnBeneficialEffectGranted, _beneficialGrantedHandler);
            _isRegistered = true;
        }

        public override void StopCode()
        {
            if (!_isRegistered) return;

            if (_beneficialGrantedHandler != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnBeneficialEffectGranted, _beneficialGrantedHandler);
            }

            _beneficialGrantedHandler = null;
            _isRegistered = false;
        }

        private void OnBeneficialEffectGranted(EventContext context)
        {
            Unit target = context.Grantor;
            if (context.Grantee != Caster || target == null || !target.isActive) return;
            if (!Target.GetAllAllies(Caster).Contains(target)) return;

            string identifier = $"{AnemoImbueEffect.StatusPrefix}_{Caster.GetEntityId()}";
            target.AddStatusEffect(identifier, new AnemoImbueEffect(Caster, identifier, ImbueDuration));
        }
    }

    public class QuetzalcoatlYorisGuard : PassiveCode
    {
        public const string ResourceId = "Yoris";
        private bool _registered;
        private Action<EventContext> _beforeDamageHandler;

        public QuetzalcoatlYorisGuard(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "요리스틀리";
            MaxStage = 3;
            Transferable = false;
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            int maximum = CurrentStage switch { 1 => 6, 2 => 9, _ => 12 };
            Caster.SetCombatResourceMaximum(ResourceId, maximum, true);
            if (_registered) return;
            _beforeDamageHandler = OnBeforeDamageTaken;
            Caster.AddListener(BaseEnums.UnitEventType.OnBeforeDamageTaken, _beforeDamageHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (_beforeDamageHandler != null) Caster.RemoveListener(BaseEnums.UnitEventType.OnBeforeDamageTaken, _beforeDamageHandler);
            _beforeDamageHandler = null;
            _registered = false;
        }

        private void OnBeforeDamageTaken(EventContext context)
        {
            if (context.Grantee != Caster || context.DmgCtx == null || context.DmgCtx.IsCancelled) return;
            Unit attacker = context.DmgCtx.Attacker;
            if (attacker == null || attacker.IsEnemy == Caster.IsEnemy) return;
            if (!Caster.TryConsumeCombatResource(ResourceId, 1)) return;
            context.DmgCtx.IsCancelled = true;
            Debug.Log($"[요리스틀리] {Caster.UnitName}이 피해를 무효화했습니다.");
        }
    }

    public class QuetzalcoatlRegeneration : PassiveCode
    {
        private bool _registered;
        private float _elapsed;
        private Action<EventContext> _updateHandler;

        public QuetzalcoatlRegeneration(PassiveCodeContext context) : base(context)
        {
            CodeName = "재생력";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            _elapsed = 0f;
            if (_registered) return;
            _updateHandler = OnUpdate;
            Caster.AddListener(BaseEnums.UnitEventType.OnUpdate, _updateHandler);
            _registered = true;
        }

        private void OnUpdate(EventContext context)
        {
            if (context.Grantee != Caster || !Caster.isActive) return;
            _elapsed += context.FloatParam;
            while (_elapsed >= 1f)
            {
                _elapsed -= 1f;
                Caster.ModifyHp(Caster.HpCurr + Caster.GetBaseCon());
            }
        }
    }

    public class QuetzalcoatlLifesteal : PassiveCode
    {
        private bool _registered;
        private Action<DamageResolvedContext> _handler;

        public QuetzalcoatlLifesteal(PassiveCodeContext context) : base(context)
        {
            CodeName = "흡혈";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (_registered) return;
            _handler = OnDamageDealt;
            Caster.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _handler);
            _registered = true;
        }

        private void OnDamageDealt(DamageResolvedContext context)
        {
            if (context.Attacker != Caster || context.DamageDealt <= 0) return;
            Caster.ModifyHp(Caster.HpCurr + Mathf.RoundToInt(context.DamageDealt * 0.2f));
        }
    }

    public class QuetzalcoatlForestGrace : PassiveCode
    {
        public QuetzalcoatlForestGrace(PassiveCodeContext context) : base(context)
        {
            CodeName = "숲의 은총";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster.AddStatusEffect("QuetzalcoatlForestGrace", new ForestGraceEffect(Caster));
        }
    }

    public class QuetzalcoatlSpellSniper : PassiveCode
    {
        public QuetzalcoatlSpellSniper(PassiveCodeContext context) : base(context)
        {
            CodeName = "주문 저격수";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster.AddStatusEffect("QuetzalcoatlSpellSniper", new SpellSniperEffect(Caster));
        }
    }

    public class QuetzalcoatlRooting : PassiveCode
    {
        private bool _registered;
        private float _elapsed;
        private int _stacks;
        private Action<EventContext> _updateHandler;

        public QuetzalcoatlRooting(PassiveCodeContext context) : base(context)
        {
            CodeName = "뿌리박기";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            _elapsed = 0f;
            _stacks = 0;
            if (_registered) return;
            _updateHandler = OnUpdate;
            Caster.AddListener(BaseEnums.UnitEventType.OnUpdate, _updateHandler);
            _registered = true;
        }

        private void OnUpdate(EventContext context)
        {
            if (context.Grantee != Caster || !Caster.CanActAndAttack()) return;
            _elapsed += context.FloatParam;
            if (_elapsed < 4f) return;
            _elapsed -= 4f;
            _stacks++;
            Caster.AddStatusEffect("QuetzalcoatlRootingCon", new PrimaryStatBonusEffect(Caster, "QuetzalcoatlRootingCon", BaseEnums.PrimaryStat.CON, _stacks));
            Caster.AddStatusEffect("QuetzalcoatlRootingInt", new PrimaryStatBonusEffect(Caster, "QuetzalcoatlRootingInt", BaseEnums.PrimaryStat.INT, _stacks));
        }
    }

    public class QuetzalcoatlGiver : PassiveCode
    {
        private bool _registered;
        private Action<DamageResolvedContext> _handler;

        public QuetzalcoatlGiver(PassiveCodeContext context) : base(context)
        {
            CodeName = "베푸는 자";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (_registered) return;
            _handler = OnDamageDealt;
            Caster.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _handler);
            _registered = true;
        }

        private void OnDamageDealt(DamageResolvedContext context)
        {
            if (context.Attacker != Caster || context.DamageDealt <= 0) return;
            Unit ally = Target.GetAllAllies(Caster).Where(unit => unit != null && unit.isActive).OrderBy(unit => unit.HpCurr).FirstOrDefault();
            ally?.ModifyHp(ally.HpCurr + Mathf.RoundToInt(context.DamageDealt * 0.2f));
        }
    }

    public class QuetzalcoatlScholar : PassiveCode
    {
        public QuetzalcoatlScholar(PassiveCodeContext context) : base(context)
        {
            CodeName = "학자";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster.AddStatusEffect("QuetzalcoatlScholar", new ScholarEffect(Caster));
        }
    }

    public class QuetzalcoatlTranscendence : PassiveCode
    {
        private bool _registered;
        private bool _applied;
        private float _elapsed;
        private Action<EventContext> _handler;

        public QuetzalcoatlTranscendence(PassiveCodeContext context) : base(context)
        {
            CodeName = "초월";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            _elapsed = 0f;
            _applied = false;
            if (_registered) return;
            _handler = OnUpdate;
            Caster.AddListener(BaseEnums.UnitEventType.OnUpdate, _handler);
            _registered = true;
        }

        private void OnUpdate(EventContext context)
        {
            if (_applied || context.Grantee != Caster || !Caster.isActive) return;
            _elapsed += context.FloatParam;
            if (_elapsed < 8f) return;
            _applied = true;
            foreach (BaseEnums.PrimaryStat stat in Enum.GetValues(typeof(BaseEnums.PrimaryStat)))
            {
                int amount = Mathf.RoundToInt(Caster.GetGrowthStatValue(stat) * 1.5f);
                if (amount <= 0) continue;
                string identifier = $"QuetzalcoatlTranscendence_{stat}";
                Caster.AddStatusEffect(identifier, new PrimaryStatBonusEffect(Caster, identifier, stat, amount));
            }
        }
    }

    public class QuetzalcoatlPioneer : PassiveCode
    {
        private bool _registered;
        private Action<EventContext> _handler;

        public QuetzalcoatlPioneer(PassiveCodeContext context) : base(context)
        {
            CodeName = "개척";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (_registered) return;
            _handler = OnUltimate;
            Caster.AddListener(BaseEnums.UnitEventType.OnUltimateActivates, _handler);
            _registered = true;
        }

        private void OnUltimate(EventContext context)
        {
            if (context.Grantee != Caster) return;
            foreach (Unit ally in Target.GetAllAllies(Caster))
            {
                ally?.GrantCombatElement(BaseEnums.UnitElement.Dendro);
            }
        }
    }

    public class QuetzalcoatlCall : PassiveCode
    {
        private bool _registered;
        private float _elapsed;
        private Action<EventContext> _handler;

        public QuetzalcoatlCall(PassiveCodeContext context) : base(context)
        {
            CodeName = "케찰코아틀의 부름";
            Transferable = false;
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            _elapsed = 6f;
            if (_registered) return;
            _handler = OnUpdate;
            Caster.AddListener(BaseEnums.UnitEventType.OnUpdate, _handler);
            _registered = true;
        }

        private void OnUpdate(EventContext context)
        {
            if (context.Grantee != Caster || !Caster.isActive || GridManager.Instance == null) return;
            _elapsed += context.FloatParam;
            if (_elapsed < 6f) return;
            int frontColumn = GridManager.Instance.GetFrontColumn(Caster.IsEnemy);
            for (int y = GridManager.Instance.yMin; y <= GridManager.Instance.yMax; y++)
            {
                if (!GridManager.Instance.IsCellAvailable(frontColumn, y)) continue;
                _elapsed = 0f;
                GridManager.Instance.SpawnUnit(frontColumn, y, Caster.IsEnemy, UnityEngine.Random.value < 0.5f ? 1010 : 1011);
                return;
            }
        }
    }

    public class QuetzalcoatlGuardianWill : PassiveCode
    {
        public QuetzalcoatlGuardianWill(PassiveCodeContext context) : base(context)
        {
            CodeName = "수호자의 의지";
            Transferable = false;
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster.AddStatusEffect("QuetzalcoatlGuardianWill", new GuardianWillEffect(Caster));
        }
    }

    public class QuetzalcoatlCipactliSlayer : PassiveCode
    {
        private bool _registered;
        private Action<EventContext> _handler;
        private Action<EventContext> _deathHandler;

        public QuetzalcoatlCipactliSlayer(PassiveCodeContext context) : base(context)
        {
            CodeName = "시팍틀리를 살해한 자";
            Transferable = false;
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            ApplyToAllies();
            if (_registered) return;
            _handler = _ => ApplyToAllies();
            _deathHandler = _ => RemoveFromAllies();
            Caster.AddListener(BaseEnums.UnitEventType.OnUpdate, _handler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _deathHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (_handler != null) Caster.RemoveListener(BaseEnums.UnitEventType.OnUpdate, _handler);
            if (_deathHandler != null) Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _deathHandler);
            RemoveFromAllies();
            _handler = null;
            _deathHandler = null;
            _registered = false;
        }

        private void ApplyToAllies()
        {
            foreach (Unit ally in Target.GetAllAllies(Caster))
            {
                if (ally == null || !ally.isActive) continue;
                string identifier = $"QuetzalcoatlCipactliSlayer_{Caster.GetEntityId()}";
                if (!ally.HasStatusEffect(identifier))
                {
                    ally.AddStatusEffect(identifier, new CipactliSlayerEffect(Caster, identifier));
                }
            }
        }

        private void RemoveFromAllies()
        {
            string identifier = $"QuetzalcoatlCipactliSlayer_{Caster.GetEntityId()}";
            foreach (Unit ally in Target.GetAllAllies(Caster))
            {
                ally?.RemoveStatusEffect(identifier);
            }
        }
    }

    public class MacanaGrowth : PassiveCode
    {
        private bool _registered;
        private Action<EventContext> _handler;

        public MacanaGrowth(PassiveCodeContext context) : base(context)
        {
            CodeName = "마카나";
            Transferable = false;
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (_registered) return;
            _handler = OnKill;
            Caster.AddListener(BaseEnums.UnitEventType.OnKill, _handler);
            _registered = true;
        }

        private void OnKill(EventContext context)
        {
            if (context.Grantee != Caster || UnityEngine.Random.value >= 0.25f) return;
            BaseEnums.PrimaryStat stat = (BaseEnums.PrimaryStat)UnityEngine.Random.Range(0, 5);
            Caster.AddStatUpgrade(stat, 1);
            Debug.Log($"[마카나] {Caster.UnitName}의 {stat}이 영구적으로 1 증가했습니다.");
        }
    }
}
