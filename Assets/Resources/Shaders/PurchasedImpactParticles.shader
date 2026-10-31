Shader "NeverTheLast/PurchasedImpactParticles"
{
    Properties
    {
        _Spark ("Purchased Hit4 Romb3",2D)="black"{}
        _Debris ("Purchased Hit4 Snow4",2D)="black"{}
        _Veil ("Purchased Trail73 brush",2D)="black"{}
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
            struct appdata {float4 vertex:POSITION;float3 uv:TEXCOORD0;fixed4 color:COLOR;};
            struct v2f {float4 vertex:SV_POSITION;float3 uv:TEXCOORD0;fixed4 color:COLOR;};
            sampler2D _Spark,_Debris,_Veil;
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                float3 spark=tex2D(_Spark,i.uv.xy).rgb;
                float3 dust=tex2D(_Debris,i.uv.xy).rgb;
                float4 brush=tex2D(_Veil,i.uv.xy);
                float coverage=i.uv.z<.5?max(spark.r,max(spark.g,spark.b)):i.uv.z<1.5?max(dust.r,max(dust.g,dust.b)):brush.a;
                return fixed4(i.color.rgb,i.color.a*smoothstep(.03,.92,coverage));
            }
            ENDCG
        }
    }
}
