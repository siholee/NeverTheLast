Shader "NeverTheLast/VfxClawRake"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Renderer Color", Color) = (1,1,1,1)
        _BodyColor ("Body Color", Color) = (0.01,0.005,0.008,1)
        _EdgeColor ("Edge Color", Color) = (0.72,0.01,0.02,1)
        _CoreColor ("Core Color", Color) = (1,0.06,0.03,1)
        _Reveal ("Reveal", Range(0,1)) = 0
        _RevealSoftness ("Reveal Softness", Range(0.001,0.2)) = 0.045
        _TipWidth ("Tip Width", Range(0.005,0.25)) = 0.075
        _Alpha ("Alpha", Range(0,1)) = 1
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

        // 몸체는 비가산 알파로 남겨 밝은 진주색 전장에서도 검은 할퀴기 실루엣을 보존한다.
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBody
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
            fixed4 _BodyColor;
            fixed4 _EdgeColor;
            float _Reveal;
            float _RevealSoftness;
            float _Alpha;

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color * _Color;
                output.texcoord = input.texcoord;
                return output;
            }

            fixed4 fragBody(v2f input) : SV_Target
            {
                fixed4 sample = tex2D(_MainTex, input.texcoord);
                float sourceAlpha = smoothstep(0.055, 0.14, sample.a);
                float wipe = 1.0 - smoothstep(_Reveal - _RevealSoftness, _Reveal + _RevealSoftness,
                    input.texcoord.x);
                float luma = dot(sample.rgb, float3(0.2126, 0.7152, 0.0722));
                float edge = smoothstep(0.035, 0.30, luma);
                fixed3 palette = lerp(_BodyColor.rgb, _EdgeColor.rgb, edge);
                return fixed4(palette * input.color.rgb,
                    sourceAlpha * wipe * _Alpha * input.color.a * lerp(_BodyColor.a, _EdgeColor.a, edge));
            }
            ENDCG
        }

        // 원화의 밝은 칼심과 현재 선두만 가산해 어두운 판에서도 움직이는 발톱 끝이 읽히게 한다.
        Pass
        {
            Blend SrcAlpha One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragCore
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
            fixed4 _CoreColor;
            float _Reveal;
            float _RevealSoftness;
            float _TipWidth;
            float _Alpha;

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color * _Color;
                output.texcoord = input.texcoord;
                return output;
            }

            fixed4 fragCore(v2f input) : SV_Target
            {
                fixed4 sample = tex2D(_MainTex, input.texcoord);
                float sourceAlpha = smoothstep(0.055, 0.14, sample.a);
                float wipe = 1.0 - smoothstep(_Reveal - _RevealSoftness, _Reveal + _RevealSoftness,
                    input.texcoord.x);
                float luma = dot(sample.rgb, float3(0.2126, 0.7152, 0.0722));
                // 원색 몸체 전체가 흰빛으로 번지지 않게 원화의 가장 밝은 붓심만 가산한다.
                float sourceCore = smoothstep(0.54, 0.88, luma);
                float tip = 1.0 - smoothstep(_TipWidth * 0.35, _TipWidth,
                    abs(input.texcoord.x - _Reveal));
                float glow = max(sourceCore * 0.82, tip * sourceAlpha);
                float alpha = sourceAlpha * wipe * glow * _Alpha * input.color.a * _CoreColor.a;
                return fixed4(_CoreColor.rgb * input.color.rgb * (0.72 + glow * 0.55), alpha);
            }
            ENDCG
        }
    }
}
