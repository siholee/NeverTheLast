using System;
using System.Collections;
using Entities;

namespace Effects.Vfx.Presets
{
    /// <summary>사바흐의 이전 녹아내림·그림자 이동 연출을 전환 가능한 형태로 보관한다.</summary>
    public static class ShadowAmbushFx
    {
        public static IEnumerator Play(Unit caster, Unit target, Action onImpact)
            => SabahFx.LegacyShadowAmbush(caster, target, onImpact);
    }
}
