Shader "NeverTheLast/ArcherEnvelope"
{
    Properties
    {
        [PerRendererData] _MainTex ("Purchased Wind Ribbon",2D)="white"{}
        _Tint ("Element",Color)=(.588,.796,.176,1)
        _Phase ("Scaled Flight Age",Float)=0
        _Opacity ("Opacity",Range(0,1))=1
        _Ribbon ("History Ribbon",Range(0,1))=0
        _Travel ("Flight Progress",Range(0,1))=0
        _TailExtent ("Travelled Wind Extent",Range(0,1))=0
        _Nucleus ("Foreground Nucleus",Range(0,1))=0
        _ArcProfile ("Electro ink profile",Range(0,1))=0
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent"}
        Cull Off ZWrite Off Lighting Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct Varying {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;};
            sampler2D _MainTex;
            float4 _Tint;
            float _Phase,_Opacity,_Ribbon,_Travel,_TailExtent,_Nucleus,_ArcProfile;
            Varying vert(Input v){Varying o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
            fixed4 frag(Varying i):SV_Target
            {
                float2 uv=i.uv;
                float2 flowing=float2(frac(uv.x*.85-_Phase),saturate(uv.y+.10*sin(uv.x*9+_Phase*4)));
                float wind=pow(tex2D(_MainTex,flowing).r,1.35);
                float secondWind=tex2D(_MainTex,float2(frac(uv.x*1.7+_Phase*.8),saturate(uv.y*1.3-.15))).r;
                float strand=exp(-pow((uv.y-.39-.055*sin(uv.x*10-_Phase*3))*28,2));
                float secondStrand=exp(-pow((uv.y-.60+.05*sin(uv.x*13+_Phase*2))*30,2));
                float width=pow(saturate(1-abs(uv.y*2-1)),1.7);
                float tail=smoothstep(0,.25,uv.x);
                float2 tipUV=(uv-float2(.78,.5))/float2(.035,.055);
                float tip=exp(-dot(tipUV,tipUV));
                float2 haloUV=(uv-float2(.78,.5))/float2(.10,.28);
                float halo=exp(-dot(haloUV,haloUV));
                float spindle=exp(-pow((uv.y-.5)*7,2))*smoothstep(0,.65,uv.x)*(1-smoothstep(.80,1,uv.x));
                float moving=smoothstep(0,.13,_Travel);
                float windHistory=smoothstep(.78-_TailExtent,.84-_TailExtent,uv.x)*(1-smoothstep(.78,.86,uv.x));
                float headProximity=saturate((uv.x-(.78-_TailExtent))/max(.05,_TailExtent));
                float halfWidth=.06+.24*pow(headProximity,.65);
                float windWidth=1-smoothstep(halfWidth*.70,halfWidth,abs(uv.y-.5));
                float windAlpha=(.22+wind*.42+strand*.65+secondStrand*.45)*moving*windHistory*windWidth;
                float filament=pow(width,8);
                float ribbonAlpha=(.18+wind*.35+strand*.65+secondStrand*.45)*tail;
                float alpha=lerp(lerp(windAlpha,max(halo*.76,tip*.98),_Nucleus),ribbonAlpha,_Ribbon);
                float core=lerp(saturate(tip*1.4)*_Nucleus,saturate(strand*secondWind)*.40,_Ribbon);
                // 구매 바람 결의 농담을 색에도 남겨 단색 반투명 띠처럼 뭉개지지 않게 한다.
                float textureRidge=saturate(smoothstep(.18,.82,wind)*.28+strand*.85+secondStrand*.65);
                float3 bodyColor=lerp(_Tint.rgb*float3(.08,.26,.045),lerp(_Tint.rgb,float3(.92,1,.78),.35),textureRidge);
                float3 color=lerp(bodyColor,float3(.98,1,.91),core);
                if(_ArcProfile>.5)
                {
                    // 번개의 외피는 풀빛 편향 대신 같은 원소의 짙은 잉크와 빛을 쓴다.
                    bodyColor=lerp(_Tint.rgb*.16,lerp(_Tint.rgb,float3(1,1,1),.18),textureRidge);
                    color=lerp(bodyColor,lerp(_Tint.rgb,float3(1,1,1),.48),core);
                    if(_Nucleus>.5&&_Ribbon<.5)
                    {
                        float2 inkUV=(uv-float2(.78,.5))/float2(.044,.065);
                        float inkTip=exp(-dot(inkUV,inkUV));
                        float shell=smoothstep(.08,.30,inkTip);
                        float inner=smoothstep(.35,.75,inkTip);
                        float3 tipColor=lerp(_Tint.rgb*.14,lerp(_Tint.rgb,float3(1,1,1),.48),inner);
                        color=lerp(color,tipColor,saturate(inkTip*3));
                        alpha=max(alpha,shell*.92);
                    }
                }
                return fixed4(color,saturate(alpha)*width*_Opacity*lerp(1,.75,_Ribbon));
            }
            ENDCG
        }
    }
}
