Shader "NeverTheLast/Vfx/HardEdgeErosion"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _NoiseTex ("Noise", 2D) = "gray" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _CoreColor ("Core", Color) = (1,1,1,1)
        _BandColor ("Band", Color) = (1,0.65,0.2,1)
        _RimColor ("Rim", Color) = (0.22,0.10,0.04,1)
        _Dissolve ("Dissolve", Range(0,1)) = 0
        _EdgeWidth ("Edge Width", Range(0.005,0.3)) = 0.105
        _NoiseScale ("Noise Scale", Float) = 3.4
        _Radial ("Radial", Range(0,1)) = 0.35
        _UvScroll ("UV Scroll", Vector) = (0,0.08,0,0)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
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
            #pragma vertex SpriteVert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnitySprites.cginc"

            sampler2D _NoiseTex;
            fixed4 _CoreColor;
            fixed4 _BandColor;
            fixed4 _RimColor;
            float _Dissolve;
            float _EdgeWidth;
            float _NoiseScale;
            float _Radial;
            float4 _UvScroll;

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 source = SampleSpriteTexture(IN.texcoord) * IN.color;
                clip(source.a - 0.01);

                float2 scrollUv = IN.texcoord * _NoiseScale + _UvScroll.xy * _Time.y;
                float noise = tex2D(_NoiseTex, scrollUv).r;
                float radial = saturate(length(IN.texcoord - 0.5) * 1.42);
                float field = saturate(lerp(noise, noise * 0.67 + radial * 0.33, _Radial));
                float distanceToCut = field - _Dissolve;
                clip(distanceToCut);

                float edge = 1.0 - saturate(distanceToCut / max(0.001, _EdgeWidth));
                float core = 1.0 - saturate(distanceToCut / max(0.001, _EdgeWidth * 0.28));
                float band = saturate(edge * 2.2);
                fixed3 edgeColor = lerp(_RimColor.rgb, _BandColor.rgb, band);
                edgeColor = lerp(edgeColor, _CoreColor.rgb, core);
                source.rgb = lerp(source.rgb, edgeColor, edge);
                return source;
            }
            ENDCG
        }
    }
}
