using System;
using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Codes.Ultimate;
using Combat;
using Effects.Vfx;
using Entities;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>
    /// 로키 — 일반행동 「기만의 칼바람」(Detail_08 §22.3). 공격력 90% 비접촉 단일 베기.
    /// 유효한 사냥감 표식이 있으면 그 적을, 없으면 평소의 우선도 대상을 노린다. 원소는 부착하지 않는다.
    /// </summary>
    public sealed class LokiTrickWind : BaseNormalCode
    {
        public const float AttackRatio = 0.90f;

        public override int AttackForm => DamageTag.Slash;

        public LokiTrickWind(NormalCodeContext context) : base(context)
        {
            CodeName = "기만의 칼바람";
            CastingDelay = 0.10f;
        }

        protected override int BaseDamage(Unit target) => StatDamage(0f, BaseEnums.StatType.Attack, AttackRatio);

        protected override List<Unit> SelectTarget()
        {
            Unit prey = (Caster.ActiveUltimateCode as LokiTwilight)?.MarkedTarget;
            if (prey != null && GetAvailableEnemies().Contains(prey))
            {
                Caster.currentNormalTarget = prey;
                return new List<Unit> { prey };
            }
            return base.SelectTarget();
        }

        // 은보라 칼바람은 로키 전용 표시다. 정점 시간과 피해 콜백은 기존 계약을 유지한다.
        protected override IEnumerator Deliver(Unit target)
        {
            DamageContext context = BuildContext(target);
            yield return DirectAttack.HitOne("loki.normal", Caster, target, () => Hit(target, context),
                presentation: impact => VfxComposer.PlaySingleTarget("loki.trick_wind", Caster, target, impact,
                    context.IsCrit ? 1.12f : 1f));
        }
    }

    /// <summary>
    /// 펜리르의 일반행동(소환수 일반 네임스페이스 501). 고정 속도 90의 제 차례에 로키 현재 공격력 60%로 한 번 할퀸다.
    /// 피해는 <b>로키 명의</b> + 소환수 표지라 처치 · 마나 · 오라가 로키를 따른다. 치명은 공용 치명 판정에 로키의 스탯 · 효과를,
    /// 행동자 기준 오라에는 펜리르를 넘긴다.
    /// 일반 공격 판정이라 나폴레옹의 지휘 사격에도 응한다. 펜리르 자신의 피해는 로키 특성의 추격 방아쇠가 아니다.
    /// </summary>
    public sealed class FenrirClaw : BaseNormalCode
    {
        public const int CodeId = 501;
        public const float AttackRatio = 0.60f;

        public override int AttackForm => DamageTag.Slash;

        public FenrirClaw(NormalCodeContext context) : base(context)
        {
            CodeName = "펜리르의 발톱";
            CastingDelay = 0.10f;
        }

        protected override Unit DamageOwner => Caster != null ? Caster.SummonOwner : null;

        public override bool HasValidTarget()
            => DamageOwner != null && DamageOwner.isActive && GetAvailableEnemies().Count > 0;

        protected override int BaseDamage(Unit target)
        {
            Unit owner = DamageOwner;
            return owner == null ? 0 : Mathf.Max(0, Mathf.RoundToInt(owner.StatValue(BaseEnums.StatType.Attack) * AttackRatio));
        }

        protected override List<Unit> SelectTarget()
        {
            Unit prey = (DamageOwner?.ActiveUltimateCode as LokiTwilight)?.MarkedTarget;
            if (prey != null && GetAvailableEnemies().Contains(prey)) return new List<Unit> { prey };
            return base.SelectTarget();
        }

        protected override List<int> GetDamageTags()
        {
            List<int> tags = base.GetDamageTags();
            tags.Add(DamageTag.SummonAttack);
            return tags;
        }

        protected override IEnumerator Deliver(Unit target)
        {
            Unit owner = DamageOwner;
            if (owner == null || !owner.isActive) yield break;
            // 치명은 로키의 확률 · 피해 · 대상별 치명 효과로 굴리고, 행동자 기준 오라(마리 보급)는 실제로 치는 펜리르를 본다.
            bool crit = RollCriticalFor(owner, Caster, target, BaseEnums.CodeType.Normal, out float multiplier);
            int damage = Mathf.Max(1, Mathf.RoundToInt(BaseDamage(target) * multiplier));
            var context = new DamageContext(owner, damage, BaseEnums.CodeType.Normal, GetDamageTags(), crit);
            StampCommand(context);
            yield return DirectAttack.HitOne("loki.fenrir_claw", Caster, target, () => Hit(target, context),
                presentation: impact => FenrirPresentation.Strike(Caster, target, impact));
        }
    }

    /// <summary>
    /// 펜리르 공격의 표시. 칸 없는 소환수라 카드 이동 없이 대상 초상화에 늑대 세 갈래 할퀴기와 짧은 반동만 연다.
    /// 피해 판정은 넘겨받은 콜백 한 번이 소유한다.
    /// </summary>
    internal static class FenrirPresentation
    {
        private const string Key = "loki.fenrir_claw";

        public static IEnumerator Strike(Unit fenrir, Unit target, Action impact)
        {
            if (fenrir == null || target == null)
            {
                impact?.Invoke();
                yield break;
            }
            VfxComposer.PlayScreen(Key, fenrir, target);
            VfxComposer.PlaySlot(Key, "hit", 0, fenrir, target, VfxSpace.PortraitPoint(target), VfxSpace.SortingLayer(target));
            // 세 획은 판정 전에 그리기 시작한다. 피해 .10초와 후속 대기 .16초는 보존한다.
            yield return new WaitForSeconds(0.10f);
            CardMotion.RakeReaction(target, VfxSpace.AimFrom(fenrir, target).Direction, 0.30f);
            impact?.Invoke();
            yield return new WaitForSeconds(0.16f);
        }
    }
}
