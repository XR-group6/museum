Shader "TeamPrototype/LanguagePanelUI" {
 Properties { [PerRendererData] _MainTex("Texture",2D)="white" {} _Color("Tint",Color)=(1,1,1,1) }
 SubShader { Tags { "Queue"="Overlay" "RenderType"="Transparent" }
 Pass { ZTest Always ZWrite Off Cull Off Blend SrcAlpha OneMinusSrcAlpha
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "UnityCG.cginc"
 struct input { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
 struct output { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
 sampler2D _MainTex; fixed4 _Color,_TextureSampleAdd;
 output vert(input v) { output o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color*_Color; return o; }
 fixed4 frag(output i):SV_Target { return (tex2D(_MainTex,i.uv)+_TextureSampleAdd)*i.color; }
 ENDCG
 } }
}
