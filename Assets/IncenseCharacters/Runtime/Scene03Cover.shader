Shader "IncenseCharacters/Scene03Cover"
{
    Properties { _Color ("Color", Color) = (0,0,0,1) _Weave ("Woven cloth", Float) = 0 }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Opaque" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            fixed4 _Color;
            float _Weave;
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float folds = .8 + .2 * sin(i.uv.x * 95);
                float threads = .85 + .15 * sin(i.uv.x * 1800) * sin(i.uv.y * 1300);
                float edge = lerp(.45, 1, smoothstep(0, .035, i.uv.y));
                return fixed4(_Color.rgb * lerp(1, folds * threads * edge, _Weave), 1);
            }
            ENDCG
        }
    }
}
