using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Normal;
using Combat;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 우치 — 초월기 「만천화우」(Detail_08 §10.4). 보유한 분신을 <b>모두</b> 소모해 각각 우선도 · 무작위 적에게 우치 공격력 300%.
    /// 분신이 하나도 없으면 대신 분신 2기를 만든다. 분신이 제 차례에 120%를 주는 대신 300%를 주므로,
    /// 분신이 움직이기 전에 초월기를 겹칠수록 이득이다 — 우치 속도가 그 창을 정한다(§10.5).
    /// </summary>
    public sealed class UchiManchunhwau : BaseUltimateCode
    {
        private const float AttackRatio = 3.0f;
        private const int SummonsWhenEmpty = 2;

        public override int AttackForm => DamageTag.Strike;

        public UchiManchunhwau(UltimateCodeContext context) : base(context) => CodeName = "만천화우";

        protected override IEnumerator Perform()
        {
            List<Unit> clones = Summons.Of(Caster);
            if (clones.Count == 0)
            {
                for (int i = 0; i < SummonsWhenEmpty; i++) Summons.Create(Caster);
                yield return new WaitForSeconds(0.15f);
                yield break;
            }

            yield return Effects.Vfx.UchiFx.PrepareUltimate(Caster, clones);
            foreach (Unit clone in clones)
            {
                Unit target = UchiCloneStrike.PickTarget(Caster);
                if (target != null)
                {
                    Effects.Vfx.UchiFx.Strike(clone, target, ultimate: true);
                    UchiCloneStrike.HitAsOwner(Caster, target, AttackRatio, new List<int>
                    {
                        DamageTag.SingleTarget, DamageTag.UltAttack, DamageTag.ContactAttack, DamageTag.Strike, DamageTag.SummonAttack,
                    }, BaseEnums.CodeType.Ultimate, actor: clone);
                }
                clone.Die(null);
            }
            AudioManager.PlayAction(Caster, SfxActionSlot.Ultimate, SfxCuePhase.Hit, CodeName);
            yield return new WaitForSeconds(0.15f);
        }
    }
}
