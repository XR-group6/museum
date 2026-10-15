// Project3D's sky, and the reason it is a shader of our own rather than
// `Skybox/Procedural`.
//
// **Measured, not preferred.** `Skybox/Procedural` is a physically-based *daylight*
// model: its scattering is blue, and a warm tint put into it comes out grey. Probed
// with `_SkyTint` pushed to 3x its own saturation -- an orange far past anything a
// scene here declares -- and `_AtmosphereThickness` at 0.40, a dawn tint of
// (0.48, 0.29, 0.21) returned a horizon of (0.305, 0.238, 0.255), a red:green:blue
// ratio of 1.28 : 1.00 : 1.07. That is a warm grey, not a sunrise; at the thickness
// where the horizon is bright enough to be worth having, the shader's own blue takes
// the colour over completely. So the choice was between a dusk and a dawn that read
// as blue, and a sky that does what the scene file says.
//
// This one is a **flat three-colour gradient with a sun lobe**, which is exactly the
// vocabulary the scenes are written in: an hour is a zenith colour, a horizon colour,
// a ground colour and how bright it is. There is no scattering model to fight and
// nothing to be physically wrong about -- the pipeline is low-poly and every other
// surface in it is a flat colour, so a sky that is a flat colour is the *consistent*
// answer rather than the cheap one.
//
// Two properties carry the shape:
//
//   * `_Zenith` and `_Horizon` are blended by elevation, and `_HorizonFalloff` says
//     how tightly the horizon colour hugs the horizon. A low value is a broad wash of
//     the hour; a high one is a narrow bright band with the zenith taking over quickly
//     -- which is what a real dawn looks like, and the reason a dawn reads as a dawn.
//   * `_SunGlow` adds a warm lobe around `_SunDir` that fades out with elevation. This
//     is where an hour's *warmth* lives: a sunset is not an orange sky, it is a blue
//     sky with an orange patch on one side of it, and a gradient with no sun in it
//     cannot be either. `_SunDir` is the scene's own sun direction, so the glow is
//     where the scene's light already comes from.
//
// Below the horizon the zenith is replaced by `_Ground` -- the sea or the land under
// the sky, which is always much darker than the air above it and is what makes the
// horizon a horizon rather than a colour change.
Shader "Project3D/Sky"
{
    Properties
    {
        _Zenith         ("Zenith colour", Color) = (0.20, 0.30, 0.48, 1)
        _Horizon        ("Horizon colour", Color) = (0.55, 0.62, 0.72, 1)
        _Ground         ("Ground colour", Color) = (0.10, 0.12, 0.14, 1)
        _SunGlow        ("Sun glow colour", Color) = (1.00, 0.70, 0.45, 1)
        _SunDir         ("Sun direction (world)", Vector) = (0, 0.3, 1, 0)
        _Brightness     ("Brightness", Float) = 1.0
        _HorizonFalloff ("Horizon falloff", Range(0.05, 4)) = 0.80
        _SunGlowSize    ("Sun glow size", Range(0.5, 64)) = 8.0
        _SunGlowStrength("Sun glow strength", Range(0, 4)) = 0.35
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background"
               "PreviewType" = "Skybox" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Fog { Mode Off }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            fixed4 _Zenith;
            fixed4 _Horizon;
            fixed4 _Ground;
            fixed4 _SunGlow;
            float4 _SunDir;
            float _Brightness;
            float _HorizonFalloff;
            float _SunGlowSize;
            float _SunGlowStrength;

            v2f vert(appdata v)
            {
                v2f o;
                // `UnityObjectToClipPos` is what a skybox uses: Unity draws the sky with a
                // matrix that puts this box at the far plane and strips translation, so the
                // object-space vertex *is* the view direction and no camera position enters
                // into it. That is also why the sky does not swim as the player walks.
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float elevation = dir.y;

                // Above the horizon: zenith over horizon, blended by a power of the
                // elevation so the transition can be tight or broad. Below it: the same
                // horizon colour giving way to the ground, which is the one asymmetric
                // part -- the water meets the air at a hard line, and there is no such
                // thing as a ground horizon that fades upward.
                float3 colour;
                if (elevation >= 0.0)
                {
                    float t = pow(saturate(elevation), _HorizonFalloff);
                    colour = lerp(_Horizon.rgb, _Zenith.rgb, t);
                }
                else
                {
                    float t = saturate(-elevation * 3.0);
                    colour = lerp(_Horizon.rgb, _Ground.rgb, t);
                }

                // The sun lobe. Squared cosine rather than a bare power, so the falloff is
                // smooth away from the disk instead of a cone with a visible edge. Faded
                // out with elevation so it is a *horizon* glow: the same lobe overhead
                // would be a bright patch in the middle of the sky, which is a noon, not
                // a dawn.
                float3 sun = normalize(_SunDir.xyz);
                float facing = saturate(dot(dir, sun));
                float glow = pow(facing, _SunGlowSize) * _SunGlowStrength
                             * saturate(1.0 - abs(elevation) * 1.8);
                colour += _SunGlow.rgb * glow;

                return fixed4(colour * _Brightness, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
