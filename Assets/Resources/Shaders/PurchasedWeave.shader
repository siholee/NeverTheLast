Shader "NeverTheLast/PurchasedWeave"
{
    Properties{[PerRendererData] _MainTex("Purchased Trail73 grain",2D)="white"{}}
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
            struct appdata{float4 vertex:POSITION;float3 uv:TEXCOORD0;fixed4 color:COLOR;};
            struct v2f{float4 vertex:SV_POSITION;float3 uv:TEXCOORD0;fixed4 color:COLOR;};
            sampler2D _MainTex;
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                float grain=tex2D(_MainTex,float2(frac(i.uv.x),i.uv.y)).r;
                float edge=1-smoothstep(.74,1,abs(i.uv.y*2-1));
                float coverage=(.34+.66*saturate(grain*1.8))*edge;
                // 지원의 짙은 외곽만 보강한다. 촉진의 기존 질감은 유지한다.
                if(i.uv.z>.5)coverage=(.64+.36*saturate(grain*1.8))*edge;
                return fixed4(i.color.rgb,i.color.a*coverage);
            }
            ENDCG
        }
    }
}
