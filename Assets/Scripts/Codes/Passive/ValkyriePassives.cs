using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>발키리가 쓰는 상태·코드 ID. 노르드 블록의 1630대다.</summary>
    public static class ValkyrieIds
    {
        public const int Chooser = 1630;
        public const int VictorySong = 1631;

        public const int StatusChooser = 8030;
        public const int StatusVictorySong = 8031;
    }

    /// <summary>
    /// 발키리 고유 P — 전장의 선택자.
    ///
    /// 필드에 선 아군을 세어 <b>노르드 하나당 STR +3%</b>, <b>신성 하나당 LUK +3%</b>를 얻는다.
    /// 자기 자신도 노르드라 혼자 서 있어도 최소 한 몫은 붙는다.
    ///
    /// 발키리를 여럿 세우거나 친위대를 함께 세울수록 커지므로 <b>편성의 크기가 곧 화력</b>이다.
    /// 반대로 플레이어가 하나씩 지워 갈수록 남은 발키리가 약해진다 — 광역으로 한꺼번에 쓸면
    /// 그 이득을 못 보고, 하나씩 끊으면 뒤로 갈수록 쉬워진다.
    /// </summary>
    public sealed class ValkyrieChooser : PersistentStatusPassive
    {
        /// <summary>노르드 아군 하나가 주는 STR 배율.</summary>
        public const float StrPerNord = 0.03f;

        /// <summary>신성 아군 하나가 주는 LUK 배율.</summary>
        public const float LukPerDivine = 0.03f;

        public ValkyrieChooser(PassiveCodeContext context)
            : base(context, ValkyrieIds.StatusChooser, "valkyrie_chooser", "전장의 선택자",
                "필드의 노르드 아군 하나당 STR +3%, 신성 아군 하나당 LUK +3%를 얻습니다.")
        {
            IsUniquePassive = true;
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new ValkyrieChooserEffect();
    }

    internal sealed class ValkyrieChooserEffect : BaseEffect
    {
        public ValkyrieChooserEffect() : base(0) { }

        public override bool IsBeneficial => true;

        public override float PrimaryStatMultiplierModifier(Unit unit, BaseEnums.PrimaryStat stat)
        {
            if (unit != Target) return 1f;

            return stat switch
            {
                BaseEnums.PrimaryStat.STR => 1f + CountTag("Nord") * ValkyrieChooser.StrPerNord,
                BaseEnums.PrimaryStat.LUK => 1f + CountTag("Divine") * ValkyrieChooser.LukPerDivine,
                _ => 1f,
            };
        }

        /// <summary>필드에 선 아군 중 그 태그를 가진 수. 자기 자신도 센다.</summary>
        private int CountTag(string tag)
            => Combat.CombatTargets.AliveAlliesIncludingSelf(Target)
                .Count(ally => ally != null && ally.IsOnField && ally.HasUnitTag(tag));
    }

    /// <summary>
    /// 승전보 — 처치하면 가득, 처치에 손을 보탰으면 절반을 회복한다.
    ///
    /// 파죽지세(29)와 같은 방식으로 <b>피해를 준 적</b>을 기억해 두었다가 그 적이 죽을 때
    /// 마무리한 쪽이 자신인지를 본다. 광역으로 여럿을 한꺼번에 눕히면 회복이 겹쳐 터지므로,
    /// 잡졸을 붙여 놓고 발키리를 오래 두면 되레 체력이 차오른다.
    /// </summary>
    public sealed class ValkyrieVictorySong : PersistentStatusPassive
    {
        private const float KillRatio = 1.0f;
        private const float AssistRatio = 0.5f;

        public ValkyrieVictorySong(PassiveCodeContext context)
            : base(context, ValkyrieIds.StatusVictorySong, "valkyrie_victory_song", "승전보",
                "적을 처치하면 최대 체력의 100%, 처치에 관여하면 50%를 회복합니다.")
        {
        }

        protected override BaseEffect CreateInitialEffect()
            => new KillHealEffect(KillRatio, AssistRatio);
    }

    /// <summary>처치와 처치 관여를 나눠 회복시킨다.</summary>
    internal sealed class KillHealEffect : BaseEffect
    {
        private readonly float _killRatio;
        private readonly float _assistRatio;
        private readonly HashSet<Unit> _touched = new();

        private Action<DamageResolvedContext> _damageHandler;

        public KillHealEffect(float killRatio, float assistRatio) : base(0, killRatio)
        {
            _killRatio = killRatio;
            _assistRatio = assistRatio;
        }

        public override bool IsBeneficial => true;

        public override void OnApply()
        {
            if (Target == null) return;

            _damageHandler = context =>
            {
                if (context?.Target != null && context.DamageDealt > 0) _touched.Add(context.Target);
            };
            Target.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _damageHandler);
            Unit.AnyUnitDied += OnAnyUnitDied;
        }

        public override void OnRemove()
        {
            if (Target != null && _damageHandler != null)
            {
                Target.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _damageHandler);
            }
            Unit.AnyUnitDied -= OnAnyUnitDied;
            _touched.Clear();
            _damageHandler = null;
        }

        private void OnAnyUnitDied(Unit dead, Unit killer)
        {
            if (Target == null || !Target.isActive || dead == null) return;
            if (dead.IsEnemy == Target.IsEnemy) return;

            bool participated = _touched.Remove(dead);
            bool finished = killer == Target;
            if (!participated && !finished) return;

            float ratio = finished ? _killRatio : _assistRatio;
            int heal = Mathf.Max(1, Mathf.RoundToInt(Target.HpMax * ratio));
            Target.ModifyHp(Target.HpCurr + heal, Target);
        }
    }
}
