// Fog plane shader for VR Sledding — URP compatible, stereo-safe.
// Alpha-blended transparent quad that creates an animated fog bank.
// Used by SledFogController.cs which positions multiple layers ahead of the sled.

Shader "Custom/FogPlane"
{
    Properties
    {
        _FogColor       ("Fog Colour",              Color)           = (0.80, 0.87, 0.95, 1.0)
        _Density        ("Density",                 Range(0, 1))     = 0.50
        _HorizSoft      ("Horizontal Edge Softness",Range(0.01,0.5)) = 0.18
        _VertSoft       ("Vertical Edge Softness",  Range(0.01,0.5)) = 0.22
        _ScrollSpeedX   ("Scroll Speed X",          Float)           = 0.018
        _ScrollSpeedY   ("Scroll Speed Y",          Float)           = 0.008
        _NoiseScale     ("Noise Scale",             Float)           = 2.30
        _PhaseOffset    ("Phase Offset (per layer)",Float)           = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent+10"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend  SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull   Off
        Lighting Off

        Pass
        {
            Name "FogPlane"

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ── Vertex input / output ──────────────────────────────────────
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ── Material properties (URP constant buffer) ──────────────────
            CBUFFER_START(UnityPerMaterial)
                float4 _FogColor;
                float  _Density;
                float  _HorizSoft;
                float  _VertSoft;
                float  _ScrollSpeedX;
                float  _ScrollSpeedY;
                float  _NoiseScale;
                float  _PhaseOffset;
            CBUFFER_END

            // ── Two-octave trig noise (no texture required) ────────────────
            // Returns a value remapped to roughly [0.15, 0.85] with smooth variation.
            float FogNoise(float2 p)
            {
                // Octave 1 – large, slow wisps
                float a = sin(p.x * 3.2 + p.y * 5.1) * cos(p.y * 2.8 - p.x * 4.3);
                // Octave 2 – finer, faster detail
                float b = sin(p.x * 8.7 + p.y * 11.2) * cos(p.y * 7.1 + p.x * 9.4) * 0.5;
                // Remap to [0,1]
                return saturate((a + b) * 0.28 + 0.5);
            }

            // ── Vertex shader ──────────────────────────────────────────────
            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv         = IN.uv;
                return OUT;
            }

            // ── Fragment shader ────────────────────────────────────────────
            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float2 uv = IN.uv;
                float  t  = _Time.y;

                // ── Edge masks ─────────────────────────────────────────────
                // Horizontal: fade smoothly to transparent at left & right edges.
                float hMask = smoothstep(0.0, _HorizSoft, uv.x)
                            * smoothstep(0.0, _HorizSoft, 1.0 - uv.x);

                // Vertical: denser at the bottom (ground), fades out at top (sky).
                float vMask = smoothstep(0.0, _VertSoft,       uv.y)
                            * smoothstep(0.0, _VertSoft * 0.5, 1.0 - uv.y);

                // ── Animated noise (two independent scroll directions) ──────
                // _PhaseOffset staggers layers so they don't look identical.
                float2 scroll1 = float2( t * _ScrollSpeedX + _PhaseOffset     ,
                                         t * _ScrollSpeedY * 0.6);
                float2 scroll2 = float2(-t * _ScrollSpeedX * 0.7 + _PhaseOffset * 0.61,
                                         t * _ScrollSpeedY * 1.5);

                float n1 = FogNoise(uv * _NoiseScale              + scroll1);
                float n2 = FogNoise(uv * _NoiseScale * 1.85       + scroll2);
                float n  = lerp(n1, n2, 0.38);

                // ── Final alpha ────────────────────────────────────────────
                // Noise modulates the base density (70% base + 30% noise variation).
                float alpha = hMask * vMask * _Density * (0.70 + n * 0.30);
                alpha       = saturate(alpha);

                return half4(_FogColor.rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
