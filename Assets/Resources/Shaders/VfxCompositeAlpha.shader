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
        _UvRect ("Sprite UV Rect", Vector) = (0,0,1,1)
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
            float4 _UvRect;

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 sample = tex2D(_MainTex, input.texcoord);
                float sourceGray = dot(sample.rgb, float3(0.2126, 0.7152, 0.0722));
                sample.rgb = lerp(sample.rgb, sourceGray.xxx, saturate(_Desaturate));
                float2 localUv = saturate((input.texcoord - _UvRect.xy) / max(_UvRect.zw, float2(0.0001, 0.0001)));
                float edge = min(min(localUv.x, 1.0 - localUv.x), min(localUv.y, 1.0 - localUv.y));
                float feather = _EdgeSoftness > 0.0001 ? smoothstep(0.0, _EdgeSoftness, edge) : 1.0;

                float sourceLuma = dot(sample.rgb, float3(0.2126, 0.7152, 0.0722));
                float brightBoard = saturate((_BackdropLuminance - 0.40) / 0.35);
                float shadowLift = (1.0 - sourceLuma) * brightBoard * _LiftStrength;
                fixed3 lifted = max(sample.rgb, _PaletteLift.rgb * (0.42 + sourceLuma * 0.58));
                sample.rgb = lerp(sample.rgb, lifted, shadowLift);
                sample *= input.color;
                float alphaMask = smoothstep(_AlphaCutoff,
                    _AlphaCutoff + max(0.001, _AlphaSoftness), sample.a);
                sample.a *= feather * alphaMask;
                return sample;
            }
            ENDCG
        }
    }
}
