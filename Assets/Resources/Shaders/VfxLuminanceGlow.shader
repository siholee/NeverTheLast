Shader "NeverTheLast/VfxLuminanceGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Glow Tint", Color) = (1,0.82,0.42,1)
        _GlowThreshold ("Glow Threshold", Range(0,1)) = 0.70
        _GlowSoftness ("Glow Softness", Range(0.01,0.5)) = 0.18
        _GlowGain ("Glow Gain", Range(0,4)) = 1.25
        _Desaturate ("Desaturate", Range(0,1)) = 0
        _UvRect ("Sprite UV Rect", Vector) = (0,0,1,1)
        _WorldClipMinY ("Portrait HUD Boundary", Float) = -100000
        _WorldClipFeather ("HUD Boundary Feather", Float) = 0.06
        _EdgeSoftness ("Edge Softness", Range(0,0.25)) = 0.035
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent+10"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha One

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
            float _GlowThreshold;
            float _GlowSoftness;
            float _GlowGain;
            float _Desaturate;
            float4 _UvRect;
            float _WorldClipMinY, _WorldClipFeather;
            float _EdgeSoftness;

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
                float luma = dot(sample.rgb, float3(0.2126, 0.7152, 0.0722));
                float glow = smoothstep(_GlowThreshold - _GlowSoftness, _GlowThreshold + _GlowSoftness, luma);
                float2 localUv = saturate((input.texcoord - _UvRect.xy) / max(_UvRect.zw, float2(0.0001, 0.0001)));
                float edge = min(min(localUv.x, 1.0 - localUv.x), min(localUv.y, 1.0 - localUv.y));
                float feather = _EdgeSoftness > 0.0001 ? smoothstep(0.0, _EdgeSoftness, edge) : 1.0;
                float alpha = sample.a * input.color.a * glow * feather;
                // 몸체와 같은 탈색 값을 적용해야 원본 녹색/보라색이 파스텔 잔광에 다시 섞이지 않는다.
                fixed3 paletteSource = lerp(sample.rgb, luma.xxx, saturate(_Desaturate));
                fixed3 color = paletteSource * input.color.rgb * _GlowGain;
                return fixed4(color, alpha);
            }
            ENDCG
        }
    }
}
