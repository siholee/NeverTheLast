Shader "NeverTheLast/SurtrGreatblade"
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
            float _Dissolve,_Opacity,_Phase,_Fall;
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 paint=tex2D(_MainTex,i.uv);
                // 원화의 암부·가는 광맥·불티 알파를 그대로 구분하며 원소색만 공통 팔레트로 정한다.
                float value=dot(paint.rgb,float3(.2126,.7152,.0722));
                float body=pow(saturate(value),.65);
                float hot=pow(saturate(paint.g*1.2),2.5)*.35;
                float3 color=lerp(_EdgeColor.rgb*(.035+body*.965),float3(1,1,1),hot);
                float grain=tex2D(_NoiseTex,i.uv*float2(1.8,1.2)+float2(.17,.23)).r;
                float field=.45*grain+.38*body+.17*(1-i.uv.y);
                float cut=lerp(-.1,1.1,_Dissolve);
                float keep=smoothstep(cut-.035,cut+.055,field);
                return fixed4(color,paint.a*keep*_Opacity);
            }
            ENDCG
        }
    }
}
