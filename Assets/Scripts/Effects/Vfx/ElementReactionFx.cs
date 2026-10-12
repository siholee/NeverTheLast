using BaseClasses;
using Entities;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>성공한 원소 부착·반응만 표시한다. 판정과 상태 수명에는 관여하지 않는다.</summary>
    public static class ElementReactionFx
    {
        public static void Attachment(Unit target, BaseEnums.UnitElement element)
        {
            if (target == null || !target.isActive) return;
            Color tint = ElementPalette.ColorFor(element);
            Color color = BattleVfxStyle.Body(tint, tint); color.a = .74f;
            Vector3 point = VfxSpace.PortraitPoint(target);
            float size = VfxSpace.CardSize;
            int layer = VfxSpace.SortingLayer(target);
            // 양옆의 작은 부착 입자. 초상화·피해 숫자·상태 아이콘의 중앙을 비운다.
            Sprite sprite = element switch
            {
                BaseEnums.UnitElement.Geo or BaseEnums.UnitElement.Cryo => VfxArt.Diamond,
                BaseEnums.UnitElement.Anemo or BaseEnums.UnitElement.Dendro => VfxArt.Crescent,
                BaseEnums.UnitElement.Pyro => VfxArt.Streak,
                BaseEnums.UnitElement.Electro => VfxArt.Glint,
                _ => VfxSpace.SoftDot
            };
            for (int i = 0; i < 2; i++)
            {
                Vector3 side = new((i == 0 ? -1 : 1) * .29f * size, -.08f * size, 0);
                SpriteFx.Play(sprite, point + side, color, false,
                    new Vector2(.08f, .14f) * size, new Vector2(.13f, .21f) * size,
                    .48f, i == 0 ? -24 : 24, i == 0 ? -15 : 15,
                    .09f, .30f, SpriteFx.Ease.OutCubic, layer, VfxSpace.OverlaySortingOrder + 55);
            }
        }

        public static void Reaction(Unit target, string name)
        {
            if (target == null || !target.isActive) return;
            string state = name switch { "화상" => "burn", "감전" => "shock", "빙결" => "frozen", "진동" => "stun", _ => null };
            if (state != null) { StatusMotifFx.Play(target, state); return; }
            Color color = name switch
            {
                "증발" or "풍화" => ElementPalette.ColorFor(BaseEnums.UnitElement.Hydro),
                "융해" or "과부하" or "단조" => ElementPalette.ColorFor(BaseEnums.UnitElement.Pyro),
                "확산" => ElementPalette.ColorFor(BaseEnums.UnitElement.Anemo),
                "개화" or "성장" or "활성" => ElementPalette.ColorFor(BaseEnums.UnitElement.Dendro),
                "초전도" or "접지" => ElementPalette.ColorFor(BaseEnums.UnitElement.Electro),
                _ => ElementPalette.ColorFor(BaseEnums.UnitElement.Geo)
            };
            color = BattleVfxStyle.Body(color, color); color.a = .60f;
            SpriteFx.Play(name == "확산" ? VfxArt.Crescent : VfxArt.Shockwave,
                VfxSpace.PortraitPoint(target), color, false,
                .30f * VfxSpace.CardSize, .67f * VfxSpace.CardSize, .52f,
                0, name == "확산" ? 45 : 0, .09f, .32f, SpriteFx.Ease.OutCubic,
                VfxSpace.SortingLayer(target), VfxSpace.OverlaySortingOrder + 56);
        }
    }
}
