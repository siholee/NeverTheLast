Shader "NeverTheLast/HeavySkyfallStage"
{
 Properties { _MainTex("Painted doomscape",2D)="black"{} _ElementColor("Element",Color)=(1,.2,.1,1) _Opacity("Envelope",Range(0,1))=0 _Age("Age",Float)=0 _After("Time after actual land",Float)=-1 _Origin("Actual impact viewport",Vector)=(.67,.4,1.777,0) }
 SubShader
 {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" } Cull Off ZWrite Off Lighting Off Blend SrcAlpha OneMinusSrcAlpha
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
   struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;};
   sampler2D _MainTex;float4 _ElementColor,_Origin;float _Opacity,_Age,_After;
   v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
   fixed4 frag(v2f i):SV_Target
   {
    float after=max(0,_After),land=step(0,_After);
    float2 d=(i.uv-_Origin.xy)*float2(_Origin.z,1);
    // 실제 착지에서 한 번만 달리는 압력파. 반복 점멸 대신 원화의 명암과 굴절을 쓴다.
    float radius=length(d),front=after*2.7;
    float band=exp(-pow((radius-front)/.055,2))*exp(-after*2.8)*land;
    float2 dir=normalize(d+float2(.00001,.00001))/float2(_Origin.z,1);
    float2 uv=(i.uv-.5)*lerp(1.018,1.0,saturate(_Age/.47))+.5-dir*band*.008;
    float2 cloudDrift=float2(.0018*sin(_Age*.7),.0025*_Age)*smoothstep(.48,.9,i.uv.y);
    fixed3 paint=tex2D(_MainTex,saturate(uv+cloudDrift)).rgb;
    float value=dot(paint,float3(.2126,.7152,.0722));
    float heat=saturate((paint.r-max(paint.g,paint.b))*5.0);
    float3 neutral=lerp(paint,value.xxx,.56);
    float3 color=lerp(neutral,_ElementColor.rgb*(.08+value*1.7),heat*.70);
    float pressure=land*exp(-after*7.5);
    color*=lerp(.76,1.0,saturate(_Age/.47));
    color+=_ElementColor.rgb*(band*.14+pressure*.055*exp(-radius*2.2));
    return fixed4(color,saturate(_Opacity));
   }
   ENDCG
  }
 }
}
