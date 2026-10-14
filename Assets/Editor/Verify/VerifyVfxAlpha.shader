Shader "Hidden/NeverTheLast/VerifyVfxAlpha"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _EdgeSoftness ("Feather", Float) = 0
        _AlphaCutoff ("Cutoff", Float) = 0
        _AlphaSoftness ("Softness", Float) = 0.001
        _UvRect ("UV", Vector) = (0,0,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off
        // 알파 자체를 합성한다. 원래 몸체 셰이더의 RGB 혼합과 달리 알파를 제곱하지 않는다.
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input { float4 vertex:POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Output { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
            sampler2D _MainTex;
            fixed4 _Color;
            float _EdgeSoftness, _AlphaCutoff, _AlphaSoftness;
            float4 _UvRect;
            Output vert(Input i) { Output o; o.vertex=UnityObjectToClipPos(i.vertex); o.color=i.color*_Color; o.uv=i.uv; return o; }
            fixed4 frag(Output i):SV_Target
            {
                float a=tex2D(_MainTex,i.uv).a*i.color.a;
                float2 uv=saturate((i.uv-_UvRect.xy)/max(_UvRect.zw,float2(0.0001,0.0001)));
                float edge=min(min(uv.x,1-uv.x),min(uv.y,1-uv.y));
                float feather=_EdgeSoftness>0.0001?smoothstep(0,_EdgeSoftness,edge):1;
                a*=feather*smoothstep(_AlphaCutoff,_AlphaCutoff+max(0.001,_AlphaSoftness),a);
                return a.xxxx;
            }
            ENDCG
        }
    }
}
