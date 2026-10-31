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
        _BlackMatte ("Source Uses Black Matte", Range(0,1)) = 0
        _Desaturate ("Desaturate", Range(0,1)) = 0
        _PaintContrast ("Handpaint Local Contrast", Range(0,1)) = 0
        _CoverageCoreStrength ("HDR Coverage Core", Range(0,1)) = 0
        _AlphaGain ("Body Alpha Gain", Range(1,3)) = 1
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
                float worldY : TEXCOORD1;
                float2 worldXY : TEXCOORD2;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _BackdropLuminance;
            fixed4 _PaletteLift;
            float _LiftStrength;
            float _EdgeSoftness;
            float _AlphaCutoff;
            float _AlphaSoftness;
            float _BlackMatte;
            float _Desaturate;
            float _PaintContrast;
            float _CoverageCoreStrength;
            float _AlphaGain;
            float4 _UvRect;


            float _InkOutlineWidth;

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.worldY = mul(unity_ObjectToWorld,input.vertex).y;
                output.worldXY = mul(unity_ObjectToWorld,input.vertex).xy;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 sample = tex2D(_MainTex, input.texcoord);
                // 전용 연속 원화의 검은 매트만 런타임에서 제거한다. 기존 RGBA 시트는 기본값0으로 보존한다.
                float matte = smoothstep(0.015,0.075,max(sample.r,max(sample.g,sample.b)));
                sample.a *= lerp(1.0,matte,saturate(_BlackMatte));
                float sourceCoverage=sample.a;
                sample.a=saturate(sample.a*_AlphaGain);
                if(_InkOutlineWidth>0.0)
                {
                    float2 stepUv=_UvRect.zw*_InkOutlineWidth;
                    float2 uvMin=_UvRect.xy+_UvRect.zw*.001;
                    float2 uvMax=_UvRect.xy+_UvRect.zw*.999;
                    float ink=sample.a;
                    for(int iy=-1;iy<=1;iy++)for(int ix=-1;ix<=1;ix++)
                    {
                        fixed4 neighbor=tex2D(_MainTex,clamp(input.texcoord+float2(ix,iy)*stepUv,uvMin,uvMax));
                        float neighborMatte=smoothstep(0.015,0.075,max(neighbor.r,max(neighbor.g,neighbor.b)));
                        ink=max(ink,neighbor.a*lerp(1.0,neighborMatte,saturate(_BlackMatte)));
                    }
                    // 팽창한 알파의 차집합만 먹선으로 남겨 원화 위에 검은 면이 생기지 않게 한다.
                    sample=fixed4(1,1,1,pow(saturate(ink-sample.a),.55));
                }
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
                if(_CoverageCoreStrength>0.0)
                {
                    // 불투명도를 올려도 원본 HDR 명암은 남긴다. 원소색 몸체와 밝은 심을 별도로 합성한다.
                    fixed3 body=input.color.rgb*lerp(.58,1.0,sourceCoverage);
                    float hdrCore=smoothstep(.38,.74,sourceCoverage)*_CoverageCoreStrength;
                    sample.rgb=lerp(body,coreColor,hdrCore);
                }
                float alphaMask = smoothstep(_AlphaCutoff,
                    _AlphaCutoff + max(0.001, _AlphaSoftness), sample.a);
                sample.a *= feather * alphaMask;
                return sample;
            }
            ENDCG
        }
    }
}
