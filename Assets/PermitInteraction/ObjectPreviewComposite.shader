Shader "LocalPrototype/ObjectPreviewComposite" {
 Properties { [PerRendererData] _MainTex("Texture",2D)="white" {} }
 SubShader {
 Tags { "Queue"="Overlay" "RenderType"="Transparent" }
 Pass {
 ZTest Always ZWrite Off Cull Off Blend SrcAlpha OneMinusSrcAlpha
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "UnityCG.cginc"
 struct input { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
 struct output { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
 sampler2D _MainTex;
 output vert(input v) { output o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o; }
 fixed4 frag(output i):SV_Target { fixed4 s=tex2D(_MainTex,i.uv); s.rgb/=max(s.a,0.001); return s*i.color; }
 ENDCG
 }
 }
}
