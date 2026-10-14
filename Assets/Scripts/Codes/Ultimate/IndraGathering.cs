using System.Collections;
using BaseClasses;
using Codes.Base;
using Combat;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 인드라 — 초월기 「팔방의 소집」(Detail_08 §13.4). 로카팔라 묶음을 연다 — 로카팔라 묶음을 여는 유일한 수단이다.
    /// 초월기 자체는 피해가 없고, 턴을 쓰지 않으므로 소집 직후 묶음이 바로 이어진다.
    /// </summary>
    public sealed class IndraGathering : BaseUltimateCode
    {
        /// <summary>여는 묶음. 다른 특수 묶음이 생기면 이 데이터를 넓힌다.</summary>
        private static readonly string[] GateTags = { SpecialAction.LokapalaTag };

        public IndraGathering(UltimateCodeContext context) : base(context) => CodeName = "팔방의 소집";

        // 인드라 자신도 참여자라 적이 남아 있으면 언제나 쓸 수 있다.
        public override bool HasValidTarget() => Caster != null && Caster.isActive && Caster.IsOnField && AliveEnemies().Count > 0;

        protected override IEnumerator Perform()
        {
            yield return Effects.Vfx.LokapalaFx.Gather(Caster, () =>
            {
                foreach (string gate in GateTags) SpecialAction.OpenGate(Caster, gate);
            });
        }
    }
}
