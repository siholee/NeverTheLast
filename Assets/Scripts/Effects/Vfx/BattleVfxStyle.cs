using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>전투 효과의 몸체·얇은 먹선·국소 빛을 분리한다. UI와 원화 팔레트는 바꾸지 않는다.</summary>
    public static class BattleVfxStyle
    {
        /// <summary>바위 결정의 은백·옅은 금색. 원본 녹색 틴트가 바람으로 읽히지 않게 제한한다.</summary>
        public static Color Platinum(Color color)
        {
            Color.RGBToHSV(color,out float h,out float s,out float v);
            Color result=Color.HSVToRGB(Mathf.Clamp(h,40f/360f,48f/360f),Mathf.Clamp(s,.15f,.28f),Mathf.Clamp(v,.90f,.97f));
            result.a=color.a; return result;
        }
        public static Color Body(Color color, Color hint)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);
            // 낮은 알파의 접지 그림자와 가는 받침은 밝은 전장의 형태 대비를 담당한다.
            if (v < .32f && color.a < .50f) return color;
            if (v < .32f) Color.RGBToHSV(hint, out h, out s, out _);
            // 무채색 효과는 임의의 붉은색으로 바꾸지 않는다. 원소 힌트가 있으면 그 색을 따른다.
            if(s<.04f)
            {
                Color.RGBToHSV(hint,out float hintHue,out float hintSat,out _);
                if(hintSat>.06f){h=hintHue;s=hintSat;}
            }
            // 따뜻한 화염은 복숭아 먼지와 구별할 만큼 채도를 남긴다. 녹색·공허 색상은 그대로다.
            bool warm=h>=.015f && h<=.09f && s>.18f;
            float saturation=s<.04f?0f:Mathf.Clamp(s,warm?.45f:.18f,warm?.65f:.48f);
            Color result = Color.HSVToRGB(h, saturation, Mathf.Clamp(v, .76f, .92f));
            result.a = color.a;
            return result;
        }

        public static Color Light(Color color)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);
            // 화염의 작은 코어는 주황 정체성을 남긴다. 면적/알파/잔광 증폭 한도는 그대로다.
            float coreSaturation=h>=.02f&&h<=.15f&&s>=.50f?Mathf.Min(s,.60f):Mathf.Min(s,.24f);
            Color result = Color.HSVToRGB(h, coreSaturation, Mathf.Clamp(v, .82f, .96f));
            result.a = Mathf.Min(color.a, .72f);
            return result;
        }

        public static void Composite(FlipbookCompositeStyle style)
        {
            if (style == null) return;
            style.GlowColor = Light(style.GlowColor);
            style.GlowAlpha = Mathf.Min(style.GlowAlpha, .24f);
            style.GlowGain = Mathf.Min(style.GlowGain, 1.04f);
            style.GlowThreshold = Mathf.Max(style.GlowThreshold, .66f);
            style.SceneLightStrength = Mathf.Min(style.SceneLightStrength, .025f);
            style.GroundLightColor.a = Mathf.Min(style.GroundLightColor.a, .075f);
            style.ContactShadowColor.a = Mathf.Min(style.ContactShadowColor.a, .12f);
            style.Desaturate = Mathf.Max(style.Desaturate, .85f);
            style.SoftPulse = true;
        }
    }
}
