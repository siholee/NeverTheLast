Shader "NeverTheLast/UI/PrismGlass"
{
    Properties
    {
        [PerRendererData] _MainTex ("Shape", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Blur ("Frost radius", Float) = 5
        _Frost ("Frost tint", Range(0,1)) = .78
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        // 이름 있는 GrabPass는 같은 프레임의 모든 글라스 면이 장면 사본 하나를 공유한다.
        GrabPass { "_NTLGlassScene" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float4 grab:TEXCOORD1; float4 world:TEXCOORD2; };
            sampler2D _MainTex, _NTLGlassScene;
            float4 _MainTex_ST, _ClipRect, _Color;
            float _Blur, _Frost;
            v2f vert(appdata v)
            {
                v2f o; o.world=v.vertex; o.vertex=UnityObjectToClipPos(v.vertex);
                o.uv=TRANSFORM_TEX(v.uv,_MainTex); o.color=v.color*_Color;
                o.grab=ComputeGrabScreenPos(o.vertex); return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 uv=i.grab.xy/i.grab.w;
                float2 d=_Blur/_ScreenParams.xy;
                fixed3 scene=tex2D(_NTLGlassScene,uv).rgb*4;
                scene+=tex2D(_NTLGlassScene,uv+float2(d.x,0)).rgb*2;
                scene+=tex2D(_NTLGlassScene,uv-float2(d.x,0)).rgb*2;
                scene+=tex2D(_NTLGlassScene,uv+float2(0,d.y)).rgb*2;
                scene+=tex2D(_NTLGlassScene,uv-float2(0,d.y)).rgb*2;
                scene+=tex2D(_NTLGlassScene,uv+d).rgb;
                scene+=tex2D(_NTLGlassScene,uv-d).rgb;
                scene+=tex2D(_NTLGlassScene,uv+float2(d.x,-d.y)).rgb;
                scene+=tex2D(_NTLGlassScene,uv+float2(-d.x,d.y)).rgb;
                fixed4 shape=tex2D(_MainTex,i.uv)*i.color;
                fixed4 c=fixed4(lerp(scene/16,shape.rgb,_Frost),shape.a);
                #ifdef UNITY_UI_CLIP_RECT
                c.a*=UnityGet2DClipping(i.world.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(c.a-.001);
                #endif
                return c;
            }
            ENDCG
        }
    }
    Fallback "UI/Default"
}
