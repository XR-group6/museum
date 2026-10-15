Shader "LocalPrototype/SoftOutline"
{
 Properties {
  _Color ("Outline Color", Color) = (1,0.91,0.65,0.24)
  _Width ("Width in metres", Float) = 0.004
 }
 SubShader {
  Tags { "Queue"="Transparent+10" "RenderType"="Transparent" }
  Pass {
   ColorMask 0 ZWrite Off ZTest LEqual Cull Back
   Stencil { Ref 73 Comp Always Pass Replace }
  }
  Pass {
   Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha One, One OneMinusSrcAlpha
   Stencil { Ref 73 Comp NotEqual Pass Keep }
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "UnityCG.cginc"
   float4 _Color; float _Width;
   struct Input { float4 vertex:POSITION; float3 normal:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct Output { float4 vertex:SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
   Output vert(Input v) {
    Output o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    float3 world = mul(unity_ObjectToWorld,v.vertex).xyz;
    world += UnityObjectToWorldNormal(v.normal) * _Width;
    o.vertex = mul(UNITY_MATRIX_VP,float4(world,1)); return o;
   }
   fixed4 frag(Output i):SV_Target { return _Color; }
   ENDCG
  }
 }
}
