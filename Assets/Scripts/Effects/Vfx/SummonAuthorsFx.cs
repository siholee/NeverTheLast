using System.Collections;
using Effects.Vfx.Presets;
using Entities;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>금빛 활자와 청색 사유를 구분한다. 기존 구매 손그림 시트를 쓰고 중심 정보는 비운다.</summary>
    public static class SummonAuthorsFx
    {
        public static IEnumerator Prayer(Unit unit, bool theatre, bool ultimate = false)
        {
            if (unit == null) yield break;
            Color edge = theatre ? new Color(.85f, .69f, .38f) : new Color(.31f, .67f, .81f);
            Color light = theatre ? new Color(.98f, .91f, .72f) : new Color(.76f, .94f, .98f);
            HandpaintFx.PlayAt(VfxSpace.PortraitPoint(unit), VfxSpace.SortingLayer(unit), HandpaintFx.Impact1White,
                new Vector2(ultimate ? 1.0f : .65f, ultimate ? 1.0f : .65f), light, edge,
                peakAlpha: .62f, glowAlpha: .10f, calmLight: true);
            yield return new WaitForSeconds(ultimate ? .48f : .36f);
        }
        public static void Restore(Unit unit)
        {
            if (unit == null) return;
            HandpaintFx.PlayAt(VfxSpace.PortraitPoint(unit), VfxSpace.SortingLayer(unit), HandpaintFx.Impact1White,
                new Vector2(.60f,.60f), new Color(.98f,.91f,.72f), new Color(.85f,.69f,.38f),
                peakAlpha: .62f, glowAlpha: .10f, calmLight: true);
        }
    }
}
