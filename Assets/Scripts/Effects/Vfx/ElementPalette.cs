using System;
using System.Collections.Generic;
using BaseClasses;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>
    /// 원소 색 한 표. 카드의 궁극기 링 · 투사체 · 타격 섬광 · 피해 숫자가 모두 여기서 색을 받는다.
    /// 그래서 카드에 달린 링 색과 그 유닛이 쏘는 연출의 색이 항상 같다.
    /// 원신의 원소 색을 기준으로 잡았다.
    /// </summary>
    public static class ElementPalette
    {
        private static readonly Dictionary<BaseEnums.UnitElement, Color> Colors = new()
        {
            { BaseEnums.UnitElement.Pyro,    new Color(1.000f, 0.400f, 0.200f) },   // #FF6633 불
            { BaseEnums.UnitElement.Hydro,   new Color(0.169f, 0.694f, 0.945f) },   // #2BB1F1 물
            { BaseEnums.UnitElement.Dendro,  new Color(0.588f, 0.796f, 0.176f) },   // #96CB2D 풀
            { BaseEnums.UnitElement.Anemo,   new Color(0.361f, 0.855f, 0.702f) },   // #5CDAB3 바람
            { BaseEnums.UnitElement.Electro, new Color(0.702f, 0.482f, 0.937f) },   // #B37BEF 번개
            { BaseEnums.UnitElement.Cryo,    new Color(0.600f, 0.898f, 0.965f) },   // #99E5F6 얼음
            { BaseEnums.UnitElement.Geo,     new Color(1.000f, 0.784f, 0.212f) },   // #FFC836 바위
            { BaseEnums.UnitElement.Void,    new Color(0.620f, 0.400f, 0.800f) },   // 공허
            { BaseEnums.UnitElement.None,    new Color(0.86f, 0.88f, 0.92f) },
        };

        public static BaseEnums.UnitElement Parse(string elementName)
            => !string.IsNullOrWhiteSpace(elementName) &&
               Enum.TryParse(elementName, true, out BaseEnums.UnitElement element)
                ? element
                : BaseEnums.UnitElement.None;

        public static Color ColorFor(BaseEnums.UnitElement element)
            => Colors.TryGetValue(element, out Color color) ? color : Color.white;

        public static Color ColorFor(string elementName) => ColorFor(Parse(elementName));

        /// <summary>흰 바탕 위 UI(링 등)에 쓰는 한 톤 밝은 색.</summary>
        public static Color PastelColorFor(BaseEnums.UnitElement element)
        {
            Color baseColor = ColorFor(element);
            Color pastel = Color.Lerp(baseColor, Color.white, 0.38f);
            pastel.a = baseColor.a;
            return pastel;
        }

        /// <summary>그림자 · 먼지처럼 어둡게 가라앉은 색.</summary>
        public static Color DeepColorFor(BaseEnums.UnitElement element)
        {
            Color baseColor = ColorFor(element);
            return new Color(baseColor.r * 0.42f, baseColor.g * 0.36f, baseColor.b * 0.30f, 1f);
        }
    }
}
