Shader "NeverTheLast/VfxCompositeAlpha"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BackdropLuminance ("Backdrop Luminance", Range(0,1)) = 0.62
        _PaletteLift ("Bright Backdrop Lift", Color) = (1,0.91,0.72,1)
        _LiftStrength ("Lift Strength", Range(0,1)) = 0.35
        _EdgeSoftness ("Edge Softness", Range(0,0.25)) = 0.045
        _AlphaCutoff ("Alpha Cutoff", Range(0,1)) = 0
        _AlphaSoftness ("Alpha Softness", Range(0.001,0.25)) = 0.001
        _Desaturate ("Desaturate", Range(0,1)) = 0
        _PaintContrast ("Handpaint Local Contrast", Range(0,1)) = 0
        _UvRect ("Sprite UV Rect", Vector) = (0,0,1,1)
        _WorldClipMinY ("Portrait HUD Boundary", Float) = -100000
        _WorldClipFeather ("HUD Boundary Feather", Float) = 0.06
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float worldY : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _BackdropLuminance;
            fixed4 _PaletteLift;
            float _LiftStrength;
            float _EdgeSoftness;
            float _AlphaCutoff;
            float _AlphaSoftness;
            float _Desaturate;
            float _PaintContrast;
            float4 _UvRect;
            float _WorldClipMinY, _WorldClipFeather;

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.worldY = mul(unity_ObjectToWorld,input.vertex).y;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 sample = tex2D(_MainTex, input.texcoord);
                sample.a *= smoothstep(_WorldClipMinY,_WorldClipMinY+max(.0001,_WorldClipFeather),input.worldY);
                float sourceGray = dot(sample.rgb, float3(0.2126, 0.7152, 0.0722));
                // 구매 시트의 짙은 원본 색을 중립 명도로 옮기되 붓결의 명암은 남긴다.
                float neutral = 0.58 + 0.42 * sqrt(saturate(sourceGray));
                // 폭발·불꽃만 원화의 셀 명암을 복구한다. 기본값0은 기존 합성과 정확히 같다.
                float painted = 0.24 + 0.76 * pow(saturate(sourceGray), 0.65);
                neutral = lerp(neutral, painted, saturate(_PaintContrast));
                sample.rgb = lerp(sample.rgb, neutral.xxx, saturate(_Desaturate));
                float2 localUv = saturate((input.texcoord - _UvRect.xy) / max(_UvRect.zw, float2(0.0001, 0.0001)));
                float edge = min(min(localUv.x, 1.0 - localUv.x), min(localUv.y, 1.0 - localUv.y));
                float feather = _EdgeSoftness > 0.0001 ? smoothstep(0.0, _EdgeSoftness, edge) : 1.0;

                float sourceLuma = dot(sample.rgb, float3(0.2126, 0.7152, 0.0722));
                float brightBoard = saturate((_BackdropLuminance - 0.40) / 0.35);
                float shadowLift = (1.0 - sourceLuma) * brightBoard * _LiftStrength;
                fixed3 lifted = max(sample.rgb, _PaletteLift.rgb * (0.42 + sourceLuma * 0.58));
                sample.rgb = lerp(sample.rgb, lifted, shadowLift);
                sample *= input.color;
                // 손그림의 밝은 중심만 얇은 유색 코어로 남긴다. 넓은 몸체/먹선은 탈색해도 평면이 되지 않는다.
                float coreMask = lerp(smoothstep(0.62, 0.94, sourceGray),
                    smoothstep(0.30, 0.64, sourceGray) * 0.70, saturate(_PaintContrast));
                float coreWeight = coreMask * 0.52 * saturate(_Desaturate);
                fixed3 coreColor = lerp(input.color.rgb, fixed3(0.98, 0.99, 1.0), 0.65);
                sample.rgb = lerp(sample.rgb, coreColor, coreWeight);
                float alphaMask = smoothstep(_AlphaCutoff,
                    _AlphaCutoff + max(0.001, _AlphaSoftness), sample.a);
                sample.a *= feather * alphaMask;
                return sample;
            }
            ENDCG
        }
    }
}
