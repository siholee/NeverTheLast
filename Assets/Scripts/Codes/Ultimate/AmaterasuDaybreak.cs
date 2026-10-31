using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using Effects.Vfx;
using Entities;
using Managers;
using UnityEngine;
using Random = Combat.CombatRandom;

namespace Codes.Ultimate
{
    /// <summary>마나 만충으로 자동 발동한다. 일광은 발동 자격이 아니라 바운스 횟수만 정한다.</summary>
    public sealed class AmaterasuDaybreak : BaseUltimateCode
    {
        protected override bool AppliesElement => true;
        public const int StacksPerBounce = 3;
        public const float HitRatio = 1.60f;
        public int LastSpentStacks { get; private set; }
        public int LastPlannedHits { get; private set; }
        public int LastHits { get; private set; }
        public static int HitsForStacks(int stacks) => 1 + Mathf.Clamp(stacks, 0, AmaterasuRisingSun.MaxStacks) / StacksPerBounce;
        public override int AttackForm => DamageTag.Shot;
        protected override bool RequiresCompletionEvidence => true;
        public AmaterasuDaybreak(UltimateCodeContext context) : base(context) => CodeName = "천조 — 일륜 개방";
        protected override IEnumerator Perform()
        {
            LastSpentStacks = LastHits = 0;
            LastPlannedHits = 1;
            Unit previous = null;
            Vector3? previousContact = null;
            for (int i = 0; i < LastPlannedHits; i++)
            {
                if (!DirectAttack.CasterCanContinue(Caster, true)) yield break;
                var enemies = AliveEnemies();
                if (enemies.Count == 0) yield break;
                Unit chosen;
                if (i == 0) chosen = PickPriorityEnemy();
                else
                {
                    // 둘 이상이면 직전 적을 빼고 튄다. 한 적만 남으면 남은 탄을 모두 집중한다.
                    if (enemies.Count > 1) enemies.Remove(previous);
                    chosen = enemies[Random.Range(0, enemies.Count)];
                }
                var tags = new List<int> { DamageTag.SingleTarget, DamageTag.UltAttack, DamageTag.NonContactAttack, DamageTag.Shot };
                var routing = PrepareTargets(new[] { chosen }, tags);
                yield return routing.ResolvePreAttack();
                if (!routing.CanContinue || routing.Targets.Count == 0 ||
                    !DirectAttack.CasterCanContinue(Caster, true) || !Targeting.Announce(Caster, routing.Targets)) yield break;
                Unit target = routing.Targets[0];
                var context = BuildContext(StatDamage(0, BaseEnums.StatType.Attack, HitRatio), tags, target);
                bool landed = false;
                if (i == 0)
                {
                    VfxComposer.PlaySlot("amaterasu.ultimate", "cast", 0, Caster, target,
                        VfxSpace.WorldPoint(Caster), VfxSpace.SortingLayer(Caster));
                    VfxComposer.PlayScreen("amaterasu.ultimate", Caster, target);
                }
                AudioManager.PlayAction(Caster, SfxActionSlot.Ultimate, SfxCuePhase.Projectile, CodeName);
                yield return DirectAttack.HitOne("amaterasu.ultimate", Caster, target, () =>
                {
                    if (!target.isActive) return;
                    // 첫 실제 적중 시 스냅샷·전량 소비. 선제 취소나 비행 중 사망에는 일광을 쓰지 않는다.
                    if (LastHits == 0)
                    {
                        LastSpentStacks = Caster.GetCombatResource(AmaterasuRisingSun.ResourceKey);
                        LastPlannedHits = HitsForStacks(LastSpentStacks);
                        Caster.TryConsumeCombatResource(AmaterasuRisingSun.ResourceKey, LastSpentStacks);
                    }
                    Vector3 contactBeforeDamage=VfxSpace.ContactPoint(target);
                    target.TakeDamage(context);
                    if (context.IsCancelled)
                    {
                        if (LastHits == 0 && Caster != null && Caster.isActive && Caster.IsOnField)
                            Caster.AddCombatResource(AmaterasuRisingSun.ResourceKey, LastSpentStacks);
                        return;
                    }
                    landed = true;
                    previousContact=contactBeforeDamage;
                    LastHits++;
                    MarkEffectApplied();
                    AudioManager.PlayAction(Caster, SfxActionSlot.Ultimate, SfxCuePhase.Hit, CodeName);
                    // 탄별로 공통 명중/저항·반응을 거친다. 반응은 방금 그 탄만 증폭한다.
                    if (target.isActive && DirectAttack.CasterCanContinue(Caster, true))
                        target.GrantCombatElement(BaseEnums.UnitElement.Pyro, source: Caster);
                }, stopOnControl: true, presentation: impact => PurchasedProjectileFx.Fly(Caster,target,.22f,impact,
                    weight:1.05f,bounceFrom:previous!=target?previous:null,
                    lastBouncePoint:previous!=target?previousContact:null));
                if (!landed) yield break;
                previous = target;
            }
            AudioManager.PlayAction(Caster, SfxActionSlot.Ultimate, SfxCuePhase.Resolve, CodeName);
        }
    }
}
