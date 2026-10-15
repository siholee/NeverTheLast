using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using UnityEngine;
namespace Codes.Ultimate
{
    public sealed class MansaGoldenProcession : BaseUltimateCode
    {
        public MansaGoldenProcession(UltimateCodeContext context) : base(context) => CodeName = "황금의 행렬";
        public override bool HasValidTarget() => PartyTreasury.CanUse(Caster);
        protected override bool RequiresCompletionEvidence => true;
        public static int Income(float defense) => Mathf.Min(10, 6 + Mathf.FloorToInt(Mathf.Max(0, defense) / 100));
        public static float Buff(float defense) => Mathf.Min(.24f, .08f + Mathf.Max(0, defense) * .0004f);
        protected override IEnumerator Perform()
        {
            yield return new WaitForSeconds(.20f);
            if (!DirectAttack.CasterCanContinue(Caster, true)) yield break;
            float defense = Caster.StatValue(BaseEnums.StatType.Defense);
            PartyTreasury.Charge(Caster, Income(defense));
            Caster.ActivePassiveCodes.OfType<MansaRoyalWarrant>().FirstOrDefault()?.GrantProcession(Buff(defense));
            Effects.Vfx.LokapalaFx.ResourceGained(Caster); MarkEffectApplied(); yield return new WaitForSeconds(.25f);
        }
    }
    public sealed class IshtarRoyalBarrage : BaseUltimateCode
    {
        private PartyTreasury.Magazine _magazine;
        public IshtarRoyalBarrage(UltimateCodeContext context) : base(context) => CodeName = "왕권의 일제 포격";
        public override int AttackForm => DamageTag.Shot;
        public override bool DefersActivationUntilImpact => true;
        public override bool HasValidTarget() => PartyTreasury.CanUse(Caster) && PartyTreasury.Available > 0 && base.HasValidTarget();
        protected override bool RequiresCompletionEvidence => true;
        protected override IEnumerator Perform()
        {
            _magazine = PartyTreasury.Reserve(Caster); if (_magazine == null) yield break;
            bool first = true;
            int shotIndex = 0;
            int retargets = 0;
            try
            {
                while (_magazine != null && _magazine.Remaining > 0 && DirectAttack.CasterCanContinue(Caster, true))
                {
                    var target = PickPriorityEnemy(); if (target == null) break;
                    var tags = new List<int> { DamageTag.SingleTarget, DamageTag.UltAttack, DamageTag.NonContactAttack, DamageTag.Shot };
                    var routing = AttackTargetRouter.Prepare(Caster, new[] { target }, BaseEnums.CodeType.Ultimate, tags, CodeName);
                    yield return routing.ResolvePreAttack();
                    target = routing.Targets.FirstOrDefault(u => u != null && u.isActive);
                    if (!routing.CanContinue || target == null || !Targeting.Announce(Caster, routing.Targets)) break;
                    bool fired = false;
                    yield return DirectAttack.HitOne("ishtar.barrage", Caster, target, () =>
                    {
                        if (!target.isActive || _magazine == null || !_magazine.SpendOne()) return;
                        shotIndex++;
                        if (first) Caster.CommitUltimateActivation();
                        bool crit = RollCritical(target, out float multiplier);
                        if (crit) multiplier += KuberaPatronage.AdditionalCritDamage(Caster) + Codes.Passive.Reward.TreasuryRewardCode.CritDamageBonus(Caster);
                        var hit = new DamageContext(Caster, Mathf.RoundToInt(StatDamage(0, BaseEnums.StatType.Attack, 1.2f) * multiplier),
                            BaseEnums.CodeType.Ultimate, tags, crit) { IsTreasuryAttack = true, IsTreasurySpendAttack = true, TreasuryShotIndex = shotIndex };
                        target.TakeDamage(hit);
                        if (first && target.isActive) target.GrantCombatElement(BaseEnums.UnitElement.Dendro, source: Caster);
                        first = false; fired = true; MarkEffectApplied();
                        Managers.GameManager.Instance?.ActionScheduler.NotifyUltimateProgress(Caster);
                        Managers.AudioManager.PlayAction(Caster, Managers.SfxActionSlot.Ultimate, Managers.SfxCuePhase.Hit, CodeName);
                    }, stopOnControl: true);
                    if (!fired)
                    {
                        // 공지 후 정점 전에 표적이 쓰러졌다면 탄을 쓰지 않고 다른 적을 다시 고른다.
                        // 중단·제어·기타 취소는 계속 쏘지 않으며 비정상 생성 연쇄도 유한하게 막는다.
                        if (target != null && !target.isActive && DirectAttack.CasterCanContinue(Caster, true) && ++retargets <= 16) continue;
                        break;
                    }
                    retargets = 0;
                    yield return new WaitForSeconds(.12f);
                }
            }
            finally { _magazine?.Dispose(); _magazine = null; }
        }
        protected override void OnStoppedExternally() { _magazine?.Dispose(); _magazine = null; }
    }
}
