Shader "NeverTheLast/HeavyGreatblade"
{
    Properties
    {
        [PerRendererData] _MainTex("Authored greatblade",2D)="white"{}
        _NoiseTex("Purchased Trail73 breakup",2D)="white"{}
        _EdgeColor("Element palette",Color)=(1,.4,.2,1)
        _Dissolve("Breakup",Range(0,1))=0
        _Opacity("Opacity",Range(0,1))=1
        _Phase("Phase",Float)=0
        _Fall("Downstroke",Range(0,1))=0
        _BodyFrame("Authored falling frame",Float)=0
        _Layer("Drawn layer",Float)=0
        _CleaveFrame("Authored cleave cell",Float)=0
        _Highlight("Impact crest",Range(0,1))=0
    }
    SubShader
    {
        Tags{"Queue"="Transparent" "RenderType"="Transparent"}
        Cull Off ZWrite Off Lighting Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct v2f{float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;};
            sampler2D _MainTex,_NoiseTex;
            fixed4 _EdgeColor;
            float _Dissolve,_Opacity,_Phase,_Fall,_Layer,_CleaveFrame,_Highlight,_BodyFrame;
            float4 _FallRects[16];
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 paint=tex2D(_MainTex,i.uv);
                if(_Layer<.5)
                {
                    float frame=min(_BodyFrame,14.999),a=floor(frame),b=min(a+1,15),f=frac(frame);
                    float4 ra=_FallRects[(int)a],rb=_FallRects[(int)b];
                    fixed4 p=tex2D(_MainTex,ra.xy+i.uv*ra.zw),q=tex2D(_MainTex,rb.xy+i.uv*rb.zw);
                    float alpha=lerp(p.a,q.a,f);float3 premul=lerp(p.rgb*p.a,q.rgb*q.a,f);paint=fixed4(premul/max(alpha,.0001),alpha);
                }
                if(_Layer>.5)
                {
                    float last=_Layer>1.5?14:5;
                    float cell=min(_CleaveFrame,last-.001),a=floor(cell),b=min(a+1,last),f=frac(cell);
                    float2 grid=_Layer>1.5?float2(4,4):float2(3,2);
                    // 잘린 4번 원화는 제외한다. 각각 온전한15개의 실제 그림이다.
                    float ia=_Layer>1.5&&a>=4?a+1:a,ib=_Layer>1.5&&b>=4?b+1:b;
                    float2 ca=float2(fmod(ia,grid.x),grid.y-1-floor(ia/grid.x)),cb=float2(fmod(ib,grid.x),grid.y-1-floor(ib/grid.x));
                    float2 ua=i.uv,ub=i.uv;
                    if(_Layer>1.5)
                    {
                        // 셀별 전체 원화의 바닥 피벗만 맞춘다. 얼굴/필드/HUD 마스크가 아니다.
                        float ga=ia<4?.06:ia<12?.085:.13,gb=ib<4?.06:ib<12?.085:.13;
                        ua.y+=ga-.14;ub.y+=gb-.14;
                    }
                    fixed4 p=tex2D(_MainTex,(saturate(ua)+ca)/grid),q=tex2D(_MainTex,(saturate(ub)+cb)/grid);
                    p.a*=step(0,ua.y)*step(ua.y,1);q.a*=step(0,ub.y)*step(ub.y,1);
                    float alpha=lerp(p.a,q.a,f);float3 premul=lerp(p.rgb*p.a,q.rgb*q.a,f);paint=fixed4(premul/max(alpha,.0001),alpha);
                }
                // 원화의 암부·가는 광맥·불티 알파를 그대로 구분하며 원소색만 공통 팔레트로 정한다.
                float value=dot(paint.rgb,float3(.2126,.7152,.0722));
                float body=pow(saturate(value),.65);
                float hot=_Layer>1.5?pow(saturate(value*1.35),5.0)*(.18+.52*_Highlight):_Layer>.5?0:pow(saturate(value*1.3),3.0)*(.10+.50*_Highlight);
                float3 color=lerp(_EdgeColor.rgb*(.035+body*(.965+_Highlight*.35)),float3(1,1,1),hot);
                float grain=tex2D(_NoiseTex,i.uv*float2(1.8,1.2)+float2(.17,.23)).r;
                float field=.45*grain+.38*body+.17*(1-i.uv.y);
                float cut=lerp(-.1,1.1,_Dissolve);
                float keep=_Layer>.5?1:smoothstep(cut-.035,cut+.055,field);
                return fixed4(color,paint.a*keep*_Opacity);
            }
            ENDCG
        }
    }
}
