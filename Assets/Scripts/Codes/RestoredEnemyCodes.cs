using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Effects.Negative;
using Entities;
using Managers;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Codes.Restored
{
    public static class RestoredEnemyKit
    {
        private static DataManager _manager;
        private static Dictionary<int, RestoredEnemyKitData> _profiles;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() { _manager = null; _profiles = null; }
        public static RestoredEnemyKitData Find(Unit unit)
        {
            var manager = GameManager.Instance?.dataManager;
            if (unit == null || manager == null) return null;
            // 공격음의 매 단계에서도 호출된다. 큰 YAML을 타격마다 역직렬화하지 않는다.
            if (_profiles == null || _manager != manager)
            {
                _manager = manager;
                _profiles = manager.FetchEnemyDataList()?.enemies?.Where(e => e.reworkKit != null)
                    .ToDictionary(e => e.id, e => e.reworkKit) ?? new();
            }
            return _profiles.TryGetValue(unit.ID, out var profile) ? profile : null;
        }
        public static List<Unit> Allies(Unit source) => CombatTargets.AliveAlliesIncludingSelf(source);
        public static bool Support(string mode) => mode is "Heal" or "Shield" or "Rally" or "Charge";
        public static string Fx(Unit source, bool ultimate = false) => source.VfxClass == "archer" ? "enemy.void_marksman.shot" : source.VfxClass == "mage" ? "enemy.void_dragon.shot" : ultimate ? "enemy.ultimate.smash" : "enemy.void_vanguard.charge";
        public static void PostHit(Unit source, Unit target, DamageContext damage, string element, string dot, float healingReduction = 0)
        {
            if (target == null || !target.isActive || damage.ResolvedDamage <= 0) return;
            if (Enum.TryParse(element, out UnitElement applied) && applied is not UnitElement.None and not UnitElement.Void) target.GrantCombatElement(applied, source: source);
            if (Enum.TryParse(dot, out ActionDotKind kind)) ActionDot.Apply(source, target, kind);
            if (healingReduction > 0) HealingReductionStatus.Apply(target, source, 2, "치유 방해", healingReduction);
        }
        public static void SupportImpact(Unit source, string mode, float ratio, bool all)
        {
            var allies = Allies(source);
            if (!all) allies = allies.OrderBy(a => a.HpCurr / (float)a.HpMax).Take(1).ToList();
            foreach (var ally in allies)
            {
                if (mode == "Heal") ally.Heal(Mathf.RoundToInt(source.HpMax * ratio), source);
                else if (mode == "Shield") ally.AddShield(Mathf.RoundToInt(source.StatValue(StatType.Defense) * ratio), source);
                else
                {
                    var status = BuffStatus.Create(74002, "restored_command", "지휘", source, ally,
                        new OutgoingDamageMultiplierEffect(1.15f), duration: 2, isBeneficial: true, description: "가하는 피해 +15%, 일반행동 2회.");
                    status.NormalActionDuration = true; ally.AddStatus(status);
                    if (ally != source) GameManager.Instance?.ActionScheduler?.AdvanceAction(ally, all ? .15f : .20f);
                }
            }
        }
    }

    public sealed class RestoredEnemyNormal : BaseNormalCode
    {
        private readonly RestoredEnemyKitData _kit;
        public bool Charged { get; private set; }
        public override int AttackForm => RestoredEnemyKit.Support(_kit.normalMode) ? 0 : _kit.attackForm;
        protected override bool IsContact => _kit.contact;
        public RestoredEnemyNormal(NormalCodeContext context) : base(context) { _kit = RestoredEnemyKit.Find(Caster) ?? new(); CodeName = _kit.normalName ?? "공격"; }
        public void Charge()
        {
            // 기존 집중의 종료 콜백을 먼저 처리한 뒤 새 충전 상태를 설치한다.
            Concentration.Begin(Caster, "restored_charge", 2, _ => { Charged = false; Caster?.RemoveStatusByKey("restored_charge"); });
            Charged = true;
            Caster.AddStatus(BuffStatus.Create(74003, "restored_charge", "집중", Caster, Caster, new MarkerBuffEffect(), isBeneficial: true, description: "다음 일반행동이 광역 강타로 바뀝니다. 제어되면 취소됩니다."));
        }
        public override bool IsSubstituteAction => Charged;
        public override string AnnouncementName => Charged ? "집중 방출" : CodeName;
        protected override int BaseDamage(Unit target) => StatDamage(0, StatType.Attack, Charged ? 1.65f : _kit.normalRatio);
        public override bool HasValidTarget() => RestoredEnemyKit.Support(_kit.normalMode) ? Caster != null && Caster.IsOnField && Caster.isActive : base.HasValidTarget();
        protected override List<int> GetDamageTags() => new() { Charged || _kit.normalMode == "Area" ? DamageTag.AllTarget : _kit.normalTargets > 1 ? DamageTag.MultiTarget : DamageTag.SingleTarget, DamageTag.NormalAttack, IsContact ? DamageTag.ContactAttack : DamageTag.NonContactAttack, _kit.attackForm };
        protected override List<Unit> SelectTarget()
        {
            if (Charged || _kit.normalMode == "Area") return GetAvailableEnemies();
            if (_kit.normalTargets > 1) return GetAvailableEnemies().OrderByDescending(x => x.Priority).Take(_kit.normalTargets).ToList();
            return base.SelectTarget();
        }
        protected override IEnumerator SkillCoroutine()
        {
            bool completed = true; yield return WaitForCast(ok => completed = ok);
            if (!completed) { StopCode(); yield break; }
            AudioManager.PlayAction(Caster, SfxActionSlot.Normal, SfxCuePhase.Cast, CodeName);
            if (RestoredEnemyKit.Support(_kit.normalMode))
            {
                yield return DirectAttack.HitOne("orpheus.lament", Caster, Caster, () => {
                    RestoredEnemyKit.SupportImpact(Caster, _kit.normalMode, _kit.normalRatio, _kit.normalTargets > 1);
                    AudioManager.PlayAction(Caster, SfxActionSlot.Normal, SfxCuePhase.Hit, CodeName);
                }, true);
            }
            else
            {
                var routing = AttackTargetRouter.Prepare(Caster, SelectTarget(), CodeType, GetDamageTags(), AnnouncementName);
                yield return routing.ResolvePreAttack();
                if (!routing.CanContinue || !Targeting.Announce(Caster, routing.Targets)) { StopCode(); yield break; }
                if (routing.Targets.Count == 1) yield return Deliver(routing.Targets[0]);
                else yield return DirectAttack.HitEach(RestoredEnemyKit.Fx(Caster), Caster, routing.Targets,
                    t => Hit(t, AttackContext(t)), stopOnControl: true);
                if (Charged) Concentration.End(Caster, "restored_charge", Concentration.EndReason.Expired);
            }
            NotifyActionResolved(); StopCode();
        }
        protected override IEnumerator Deliver(Unit target)
        {
            var context = AttackContext(target);
            if (Caster.VfxClass == "mage")
            {
                yield return DirectAttack.HitOne(RestoredEnemyKit.Fx(Caster), Caster, target, () => Hit(target, context), true);
                yield break;
            }
            yield return DirectAttack.HitOne(RestoredEnemyKit.Fx(Caster), Caster, target, () => Hit(target, context), true,
                presentation: impact => Effects.Vfx.EnemyNormalFx.OriginalNormal(Caster, target, impact));
        }
        private DamageContext AttackContext(Unit target)
        {
            var context = BuildContext(target);
            context.DefenseStatMultiplier = 1 - _kit.armorIgnore;
            return context;
        }
        protected override void OnAttackResolved(Unit target, DamageContext context)
            => RestoredEnemyKit.PostHit(Caster, target, context, _kit.normalElement, _kit.normalDot);
    }

    public sealed class RestoredEnemyUltimate : BaseUltimateCode
    {
        private readonly RestoredEnemyKitData _kit;
        public override int AttackForm => RestoredEnemyKit.Support(_kit.ultimateMode) ? 0 : _kit.attackForm;
        protected override bool RequiresCompletionEvidence => true;
        public RestoredEnemyUltimate(UltimateCodeContext context) : base(context) { _kit = RestoredEnemyKit.Find(Caster) ?? new(); CodeName = _kit.ultimateName ?? "강타"; }
        public override bool HasValidTarget() => Caster != null && Caster.IsOnField && (RestoredEnemyKit.Support(_kit.ultimateMode) || AliveEnemies().Count > 0);
        protected override IEnumerator Perform()
        {
            // 시전음은 BaseUltimateCode.CastCode에서 이미 한 번 재생한다.
            if (RestoredEnemyKit.Support(_kit.ultimateMode))
            {
                yield return DirectAttack.HitOne("orpheus.lament", Caster, Caster, () =>
                {
                    if (_kit.ultimateMode == "Charge") (Caster.ActiveNormalCode as RestoredEnemyNormal)?.Charge();
                    else RestoredEnemyKit.SupportImpact(Caster, _kit.ultimateMode, _kit.ultimateRatio, true);
                    MarkEffectApplied();
                    AudioManager.PlayAction(Caster, SfxActionSlot.Ultimate, SfxCuePhase.Hit, CodeName);
                }, true);
                yield break;
            }
            var picked = _kit.ultimateMode == "Area" ? AliveEnemies() : AliveEnemies().OrderByDescending(t => t.Priority).Take(_kit.ultimateTargets).ToList();
            var tags = new List<int> { _kit.ultimateMode == "Area" ? DamageTag.AllTarget : _kit.ultimateTargets > 1 ? DamageTag.MultiTarget : DamageTag.SingleTarget, DamageTag.UltAttack, _kit.contact ? DamageTag.ContactAttack : DamageTag.NonContactAttack, _kit.attackForm };
            var routing = PrepareTargets(picked, tags); yield return routing.ResolvePreAttack();
            if (!routing.CanContinue || !Targeting.Announce(Caster, routing.Targets)) yield break;
            for (int i = 0; i < _kit.ultimateHits; i++)
            {
                if (!DirectAttack.CasterCanContinue(Caster, true)) yield break;
                yield return DirectAttack.HitEach(RestoredEnemyKit.Fx(Caster, true), Caster, routing.Targets, target =>
                {
                    bool crit = RollCritical(target, out float multiplier);
                    var damage = new DamageContext(Caster, Mathf.RoundToInt(StatDamage(0, StatType.Attack, _kit.ultimateRatio / _kit.ultimateHits) * multiplier), CodeType, new(tags), crit) { DefenseStatMultiplier = 1 - _kit.armorIgnore };
                    target.TakeDamage(damage);
                    AudioManager.PlayAction(Caster, SfxActionSlot.Ultimate, SfxCuePhase.Hit, CodeName);
                    RestoredEnemyKit.PostHit(Caster, target, damage, _kit.ultimateElement, _kit.ultimateDot, _kit.healingReduction);
                    if (_kit.lifesteal > 0 && damage.HpDamage > 0) Caster.Heal(Mathf.RoundToInt(damage.HpDamage * _kit.lifesteal), Caster);
                    MarkEffectApplied();
                }, stopOnControl: true);
            }
        }
    }

    public sealed class RestoredEnemyPassive : UniquePassiveCode
    {
        private readonly RestoredEnemyKitData _kit;
        private bool _active;
        private int _ward;
        private int _armorShield;
        private DamageContext _blocked;
        private Action<EventContext> _before, _after, _control;
        private Action<DamageResolvedContext> _damage;
        private Action<Unit, Unit, int, int> _healed;
        public RestoredEnemyPassive(PassiveCodeContext context) : base(context) { _kit = RestoredEnemyKit.Find(Caster) ?? new(); CodeName = _kit.passiveName ?? "전투 숙련"; }
        public override void CastCode()
        {
            if (_active || Caster == null || !Caster.IsOnField) return;
            _active = true; _ward = _kit.wardHits;
            if (_kit.passiveMode != "None" || _ward > 0)
                Caster.AddStatus(BuffStatus.Create(74001, "restored_enemy_trait", CodeName, Caster, Caster, new RestoredKitEffect(_kit), isBeneficial: true,
                    description: CodeCatalog.Find(CodeCatalog.Slot.Passive, CatalogId)?.description));
            if (_kit.shieldRatio > 0)
            {
                int before = Caster.ShieldCurr;
                Caster.AddShield(Mathf.RoundToInt(Caster.StatValue(StatType.Defense) * _kit.shieldRatio), Caster);
                _armorShield = Caster.ShieldCurr - before;
            }
            _before = ctx => { if (_ward > 0 && ctx.DmgCtx != null && !ctx.DmgCtx.IsCancelled && ctx.DmgCtx.Damage > 0 && DirectAttack.IsDirect(ctx.DmgCtx)) { _ward--; _blocked = ctx.DmgCtx; _blocked.IsCancelled = true; Effects.StatusPopup.ShowHitWard(Caster, _ward); } };
            // 전투 시작 갑주만 추적한다. 이후 동료가 누적한 보호막은 제어되어도 남는다.
            _after = ctx => {
                // 광역 시전자가 재사용하는 컨텍스트에서 이 대상의 방호만 취소한다.
                if (_blocked != null && ReferenceEquals(ctx.DmgCtx, _blocked)) { _blocked.IsCancelled = false; _blocked = null; }
                if (ctx.DmgCtx != null) _armorShield = Mathf.Max(0, _armorShield - Mathf.Max(0, ctx.DmgCtx.ResolvedDamage - ctx.DmgCtx.HpDamage));
                if (_kit.passiveMode == "Rage" && Caster.isActive) Caster.RefreshAttributes();
            };
            if (_kit.passiveMode == "Rage")
            {
                _healed = (target, source, amount, overflow) => { if (_active && target == Caster && amount > 0) Caster.RefreshAttributes(); };
                Unit.AnyHealed += _healed;
            }
            _control = _ => { if (_kit.passiveMode == "Armor") { Caster.RemoveShield(Mathf.Min(_armorShield, Caster.ShieldCurr)); _armorShield = 0; Caster.RemoveStatusByKey("restored_enemy_trait"); } };
            _damage = ctx => { if (_kit.passiveMode == "Rage" && ctx.DamageDealt > 0 && DirectAttack.IsDirect(ctx.DamageContext)) Caster.Heal(Mathf.RoundToInt(ctx.DamageContext.HpDamage * .08f), Caster); };
            Caster.AddListener(UnitEventType.OnBeforeDamageTaken, _before);
            Caster.AddListener(UnitEventType.OnAfterDamageTaken, _after);
            Caster.AddListener(UnitEventType.OnControlStarts, _control);
            Caster.AddListener(UnitEventType.OnDamageDealt, _damage);
        }
        public override void StopCode()
        {
            if (!_active) return; _active = false;
            Caster?.RemoveListener(UnitEventType.OnBeforeDamageTaken, _before); Caster?.RemoveListener(UnitEventType.OnControlStarts, _control); Caster?.RemoveListener(UnitEventType.OnDamageDealt, _damage);
            Caster?.RemoveListener(UnitEventType.OnAfterDamageTaken, _after);
            if (_healed != null) Unit.AnyHealed -= _healed;
            if (_blocked != null) _blocked.IsCancelled = false;
            _blocked = null;
            Caster?.RemoveStatusByKey("restored_enemy_trait");
            Concentration.End(Caster, "restored_charge", Concentration.EndReason.RoundEnd);
        }
    }

    internal sealed class RestoredKitEffect : BaseEffect
    {
        private readonly RestoredEnemyKitData _kit;
        private bool _revived;
        public RestoredKitEffect(RestoredEnemyKitData kit) : base(0) => _kit = kit;
        public override bool IsBeneficial => true;
        public override float StatPercentModifier(Unit unit, StatType stat)
        {
            if (unit != Target) return 0;
            if (_kit.passiveMode == "Armor" && stat == StatType.Defense) return .25f;
            if (_kit.passiveMode == "Rage" && stat == StatType.Speed) return .25f * (1-unit.HpCurr/(float)unit.HpMax);
            return 0;
        }
        public override float OutgoingDamageModifier(Unit actor, Unit target, DamageContext damage)
        {
            if (actor != Target) return 1;
            if (_kit.passiveMode == "Formation") return 1 + .03f * Mathf.Min(4, RestoredEnemyKit.Allies(actor).Count-1);
            if (_kit.passiveMode == "Rage") return 1 + .15f * (1-actor.HpCurr/(float)actor.HpMax);
            return 1;
        }
        public override bool TryPreventDeath(Unit unit, Unit attacker)
        {
            if (_kit.passiveMode != "Rebirth" || _revived || unit != Target) return false;
            _revived = true; unit.Heal(Mathf.RoundToInt(unit.HpMax * .35f) - unit.HpCurr, unit); return true;
        }
    }
}
