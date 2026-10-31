Shader "NeverTheLast/StageFigureSlice"
{
    Properties
    {
        [PerRendererData] _MainTex ("Standing", 2D) = "white" {}
        _SpriteUV ("Texture rect UV", Vector) = (0,0,1,1)
        _ClipV ("Top-down vertical interval", Vector) = (0,1,0,0)
        _Ellipse ("Head cx cy rx ry", Vector) = (.5,.1,.1,.1)
        _Feather ("Ellipse feather", Float) = .3
        _KeepAboveV ("Keep hair above", Float) = -1
        _AboveFadeV ("Hair fade interval", Float) = .02
        _Head ("Head mask", Float) = 0
        _Brightness ("Focus brightness", Float) = 1
        [HideInInspector] _DstBlend ("Destination blend", Float) = 10
        [HideInInspector] _Flip ("Sprite flip", Vector) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" }
        Cull Off ZWrite Off Lighting Off
        Blend SrcAlpha [_DstBlend]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct Varying { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            sampler2D _MainTex;
            float4 _SpriteUV, _ClipV, _Ellipse;
            float _Feather, _KeepAboveV, _AboveFadeV, _Head, _Brightness;
            float2 _Flip;
            Varying vert(Input v) { Varying o; v.vertex.xy*=_Flip; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o; }
            fixed4 frag(Varying i):SV_Target
            {
                float2 p=(i.uv-_SpriteUV.xy)/_SpriteUV.zw;
                p.y=1-p.y;
                float faceStrip=1-smoothstep(_Ellipse.z,_Ellipse.z*1.3,abs(p.x-_Ellipse.x));
                // 머리 바깥으로 솟은 무기·장식은 몸의 축소 비율로 이어서 그린다.
                if (_Head>.5) clip(p.y-_ClipV.x);
                clip(_ClipV.y-p.y);
                fixed4 c=tex2D(_MainTex,i.uv)*i.color;
                c.rgb *= _Brightness;
                if (_Head<.5 && p.y<_ClipV.x) c.a*=1-faceStrip;
                if (_Head>.5)
                {
                    float radius=length((p-_Ellipse.xy)/_Ellipse.zw);
                    float ellipse=1-smoothstep(1-_Feather,1,radius);
                    float hair=(1-smoothstep(_KeepAboveV,_KeepAboveV+_AboveFadeV,p.y))*faceStrip;
                    c.a*=max(ellipse,hair);
                }
                return c;
            }
            ENDCG
        }
    }
}
