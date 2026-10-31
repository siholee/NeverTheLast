Shader "NeverTheLast/PurchasedImpactBlend"
{
    Properties
    {
        [PerRendererData] _MainTex ("Hit4 atlas",2D)="white" {}
        _GrainTex ("Purchased wind grain",2D)="gray" {}
        _Color ("Tint",Color)=(1,1,1,1)
        _UvRect ("Current cell",Vector)=(0,0,1,1)
        _NextUvRect ("Next cell",Vector)=(0,0,1,1)
        _FrameBlend ("Cell blend",Float)=0
        _CoreRelease ("Core breakup",Float)=0
        _CurrentCore ("Current core cell",Float)=1
        _NextCore ("Next core cell",Float)=1
        _AlphaGain ("Coverage",Float)=2.1
        _SeedProfile ("Small seed breakup",Float)=0
        _PaletteWhiteMix ("Element core white mix",Range(0,1))=.65
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True"}
        Cull Off Lighting Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata {float4 vertex:POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;};
            struct v2f {float4 vertex:SV_POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;};
            sampler2D _MainTex,_GrainTex;
            fixed4 _Color;
            float4 _UvRect,_NextUvRect;
            float _FrameBlend,_CoreRelease,_CurrentCore,_NextCore,_AlphaGain;
            float _SeedProfile,_PaletteWhiteMix;
            v2f vert(appdata v)
            {v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color*_Color;o.uv=v.uv;return o;}
            // Hit4의 기존 coverage 색 문법을 보존한다. 맥동하는 두 겹 알파 대신 한 번만 합성한다.
            float4 Paint(float2 local,float4 rect,float core,fixed4 tint)
            {
                float2 center=float2(.509,.492);
                float release=_CoreRelease,coreScale=.66;
                if(_SeedProfile>.5){release=.10+release*1.28;coreScale=.56;}
                else{release=.10+release*1.28;coreScale=.36;}
                // 파편 셀만 처음부터 국소 분포로 사용한다. 카드/얼굴/HUD 마스크는 없다.
                float2 delta=local-center;
                float edge=smoothstep(.06,.38,length(delta));
                // 짧은 정점 핵(.66)은 가는 불티 층에 자리를 내주고 실제 파편 분포(.62)로 이어진다.
                float retreat=smoothstep(.12,.62,release);
                float2 uv=center+delta/(lerp(.62,coreScale,core)*(1-core*retreat*.24*edge));
                // 일반 화살의 수직 획은 짧게 두고 실제 도착 축을 따르는 국소 섬광으로 남긴다.
                if(_SeedProfile<.5&&core>.5)uv.y=center.y+(uv.y-center.y)/.58;
                float grain=tex2D(_GrainTex,local*1.7+float2(.13,.27)).r;
                uv+=core*release*.012*float2(sin(local.y*27.0+grain*3.0),cos(local.x*23.0));
                float valid=step(0,uv.x)*step(uv.x,1)*step(0,uv.y)*step(uv.y,1);
                float4 tex=tex2D(_MainTex,rect.xy+saturate(uv)*rect.zw);
                float coverage=tex.a;
                float3 body=tint.rgb*lerp(.90,1.0,coverage);
                float3 light=lerp(tint.rgb,float3(.98,.99,1),_PaletteWhiteMix);
                float3 rgb=lerp(body,light,smoothstep(.38,.74,coverage));
                // 정지된 구매 텍스처의 농담으로 부위마다 풀린다. 시간 노이즈·점멸을 추가하지 않는다.
                float threshold=max(.18,.24+.42*grain-.22*edge);
                float intact=(1-smoothstep(threshold-.14,threshold+.14,release))*(1-smoothstep(.86,1,release));
                // 코어만 밝게 버티지 않게 풀되 파편 셀에는 이 감쇠를 적용하지 않는다.
                float coreFade=lerp(1.0,.25,retreat);
                float alpha=saturate(coverage*_AlphaGain)*tint.a*valid*lerp(1.0,intact*coreFade,core);
                if(_SeedProfile>.5)alpha*=lerp(1.0,.80,core);
                else alpha*=lerp(1.0,.82,core);
                return float4(rgb*alpha,alpha);
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 local=(i.uv-_UvRect.xy)/max(_UvRect.zw,float2(.0001,.0001));
                float4 a=Paint(local,_UvRect,_CurrentCore,i.color);
                float4 b=Paint(local,_NextUvRect,_NextCore,i.color);
                float4 mixed=lerp(a,b,saturate(_FrameBlend));
                return fixed4(mixed.rgb/max(mixed.a,.00001),mixed.a);
            }
            ENDCG
        }
    }
}
