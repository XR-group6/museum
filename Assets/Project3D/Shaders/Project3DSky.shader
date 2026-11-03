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

        // Four authored skies, selected by view azimuth. A count of zero is the old
        // single-sky path, byte-for-byte; a scene that declares sectors sets it to 1..4.
        // Each sector is a complete sky -- its own zenith, horizon, ground, glow, sun and
        // shape -- because the four hours are not one palette at four brightnesses.
        _SectorCount      ("Sector count", Float) = 0
        _SectorBlend      ("Sector blend (degrees)", Range(0.01, 44)) = 6
        _Sector1Zenith    ("Sector 1 zenith", Color) = (0, 0, 0, 1)
        _Sector1Horizon   ("Sector 1 horizon", Color) = (0, 0, 0, 1)
        _Sector1Ground    ("Sector 1 ground", Color) = (0, 0, 0, 1)
        _Sector1Glow      ("Sector 1 glow", Color) = (0, 0, 0, 1)
        _Sector1Centre    ("Sector 1 centre", Vector) = (1, 0, 0, 0)
        _Sector1Sun       ("Sector 1 sun", Vector) = (0, 1, 0, 0)
        _Sector1Shape     ("Sector 1 brightness, falloff, glow size, glow strength", Vector) = (1, 0.8, 8, 0.35)
        _Sector2Zenith    ("Sector 2 zenith", Color) = (0, 0, 0, 1)
        _Sector2Horizon   ("Sector 2 horizon", Color) = (0, 0, 0, 1)
        _Sector2Ground    ("Sector 2 ground", Color) = (0, 0, 0, 1)
        _Sector2Glow      ("Sector 2 glow", Color) = (0, 0, 0, 1)
        _Sector2Centre    ("Sector 2 centre", Vector) = (0, 0, 1, 0)
        _Sector2Sun       ("Sector 2 sun", Vector) = (0, 1, 0, 0)
        _Sector2Shape     ("Sector 2 brightness, falloff, glow size, glow strength", Vector) = (1, 0.8, 8, 0.35)
        _Sector3Zenith    ("Sector 3 zenith", Color) = (0, 0, 0, 1)
        _Sector3Horizon   ("Sector 3 horizon", Color) = (0, 0, 0, 1)
        _Sector3Ground    ("Sector 3 ground", Color) = (0, 0, 0, 1)
        _Sector3Glow      ("Sector 3 glow", Color) = (0, 0, 0, 1)
        _Sector3Centre    ("Sector 3 centre", Vector) = (-1, 0, 0, 0)
        _Sector3Sun       ("Sector 3 sun", Vector) = (0, 1, 0, 0)
        _Sector3Shape     ("Sector 3 brightness, falloff, glow size, glow strength", Vector) = (1, 0.8, 8, 0.35)
        _Sector4Zenith    ("Sector 4 zenith", Color) = (0, 0, 0, 1)
        _Sector4Horizon   ("Sector 4 horizon", Color) = (0, 0, 0, 1)
        _Sector4Ground    ("Sector 4 ground", Color) = (0, 0, 0, 1)
        _Sector4Glow      ("Sector 4 glow", Color) = (0, 0, 0, 1)
        _Sector4Centre    ("Sector 4 centre", Vector) = (0, 0, -1, 0)
        _Sector4Sun       ("Sector 4 sun", Vector) = (0, 1, 0, 0)
        _Sector4Shape     ("Sector 4 brightness, falloff, glow size, glow strength", Vector) = (1, 0.8, 8, 0.35)
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
            float4 _Sector1Zenith;
            float4 _Sector1Horizon;
            float4 _Sector1Ground;
            float4 _Sector1Glow;
            float4 _Sector1Centre;
            float4 _Sector1Sun;
            float4 _Sector1Shape;
            float4 _Sector2Zenith;
            float4 _Sector2Horizon;
            float4 _Sector2Ground;
            float4 _Sector2Glow;
            float4 _Sector2Centre;
            float4 _Sector2Sun;
            float4 _Sector2Shape;
            float4 _Sector3Zenith;
            float4 _Sector3Horizon;
            float4 _Sector3Ground;
            float4 _Sector3Glow;
            float4 _Sector3Centre;
            float4 _Sector3Sun;
            float4 _Sector3Shape;
            float4 _Sector4Zenith;
            float4 _Sector4Horizon;
            float4 _Sector4Ground;
            float4 _Sector4Glow;
            float4 _Sector4Centre;
            float4 _Sector4Sun;
            float4 _Sector4Shape;
            float _SectorCount;
            float _SectorBlend;

            // The weight of one sector at an azimuth, in degrees. Each sector owns the
            // 90 degrees around its door; the seam is narrow and smooth on both diagonal
            // construction joints, so two adjacent skies sum to 1 through the corner
            // yards and the rest of each quarter is that era's own sky.
            float SectorWeight(float azimuth, float4 centre)
            {
                float centreAzimuth = atan2(centre.z, centre.x) * 57.29577951308232;
                float delta = azimuth - centreAzimuth;
                delta = frac((delta + 180.0) / 360.0) * 360.0 - 180.0;
                float distance = abs(delta);
                float blend = clamp(_SectorBlend, 0.01, 44.0);
                return 1.0 - smoothstep(45.0 - blend, 45.0 + blend, distance);
            }

            // One sky's gradient, shared by the single-sky path and the blended sectors.
            // `dir` is Y-up Unity world space; `ground` is what is below the horizon.
            float3 SkyGradient(float3 dir, float3 zenith, float3 horizon, float3 ground,
                               float3 glowColour, float3 sun, float brightness,
                               float falloff, float glowSize, float glowStrength)
            {
                float elevation = dir.y;
                float3 colour;
                if (elevation >= 0.0)
                {
                    float t = pow(saturate(elevation), falloff);
                    colour = lerp(horizon, zenith, t);
                }
                else
                {
                    float t = saturate(-elevation * 3.0);
                    colour = lerp(horizon, ground, t);
                }

                float3 sunDir = dot(sun, sun) > 1e-8 ? normalize(sun) : float3(0.0, 1.0, 0.0);
                float facing = saturate(dot(dir, sunDir));
                float glow = pow(facing, max(glowSize, 0.5)) * glowStrength
                             * saturate(1.0 - abs(elevation) * 1.8);
                colour += glowColour * glow;
                return colour * brightness;
            }

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
                float3 colour;
                if (_SectorCount >= 0.5)
                {
                    float azimuth = atan2(dir.z, dir.x) * 57.29577951308232;
                    float4 weights = float4(0.0, 0.0, 0.0, 0.0);
                    if (_SectorCount >= 0.5) weights.x = SectorWeight(azimuth, _Sector1Centre);
                    if (_SectorCount >= 1.5) weights.y = SectorWeight(azimuth, _Sector2Centre);
                    if (_SectorCount >= 2.5) weights.z = SectorWeight(azimuth, _Sector3Centre);
                    if (_SectorCount >= 3.5) weights.w = SectorWeight(azimuth, _Sector4Centre);
                    float total = weights.x + weights.y + weights.z + weights.w;
                    if (total < 1e-4)
                    {
                        weights = float4(1.0, 0.0, 0.0, 0.0);
                        total = 1.0;
                    }
                    weights /= total;

                    // Evaluate each complete sky, then blend the *finished* colours.
                    // Blending the parameters would blend the four sun directions into one
                    // that belongs to none of them -- a false sun on every diagonal seam.
                    // A quarter is an hour, and an hour includes where its light comes from.
                    float3 colour1 = SkyGradient(dir, _Sector1Zenith.rgb, _Sector1Horizon.rgb,
                                                 _Sector1Ground.rgb, _Sector1Glow.rgb,
                                                 _Sector1Sun.xyz, _Sector1Shape.x,
                                                 _Sector1Shape.y, _Sector1Shape.z,
                                                 _Sector1Shape.w);
                    float3 colour2 = SkyGradient(dir, _Sector2Zenith.rgb, _Sector2Horizon.rgb,
                                                 _Sector2Ground.rgb, _Sector2Glow.rgb,
                                                 _Sector2Sun.xyz, _Sector2Shape.x,
                                                 _Sector2Shape.y, _Sector2Shape.z,
                                                 _Sector2Shape.w);
                    float3 colour3 = SkyGradient(dir, _Sector3Zenith.rgb, _Sector3Horizon.rgb,
                                                 _Sector3Ground.rgb, _Sector3Glow.rgb,
                                                 _Sector3Sun.xyz, _Sector3Shape.x,
                                                 _Sector3Shape.y, _Sector3Shape.z,
                                                 _Sector3Shape.w);
                    float3 colour4 = SkyGradient(dir, _Sector4Zenith.rgb, _Sector4Horizon.rgb,
                                                 _Sector4Ground.rgb, _Sector4Glow.rgb,
                                                 _Sector4Sun.xyz, _Sector4Shape.x,
                                                 _Sector4Shape.y, _Sector4Shape.z,
                                                 _Sector4Shape.w);
                    colour = colour1 * weights.x + colour2 * weights.y
                           + colour3 * weights.z + colour4 * weights.w;
                }
                else
                {
                    colour = SkyGradient(dir, _Zenith.rgb, _Horizon.rgb, _Ground.rgb,
                                         _SunGlow.rgb, _SunDir.xyz, _Brightness,
                                         _HorizonFalloff, _SunGlowSize, _SunGlowStrength);
                }
                return fixed4(colour, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
