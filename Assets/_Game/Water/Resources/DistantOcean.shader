Shader "OpenWorld/DistantOcean"
{
    Properties
    {
        [Toggle(_WATER_REFERENCE_LOOK)] _ReferenceLook ("Improved Ocean Look", Float) = 0
        [Normal] _RippleNormalA ("Broad Irregular Ripples", 2D) = "bump" {}
        _RippleStrength ("Ripple Normal Strength", Range(0, 1)) = 0.18
        _GerstnerStrength ("Gerstner Horizontal Motion", Range(0, 0.35)) = 0.18
        _ShallowColor ("Shallow Color", Color) = (0.05, 0.55, 0.72, 1)
        _DeepColor ("Deep Color", Color) = (0.01, 0.10, 0.28, 1)
        _FoamColor ("Foam Color", Color) = (0.86, 0.96, 1.0, 1)
        _FoamStrength ("Foam Strength", Range(0,1)) = 1
        [HideInInspector] _ContactFoamTex ("Water Contact Foam", 2D) = "black" {}
        [HideInInspector] _ContactFoamParams ("Width Strength Tiling Fade", Vector) = (.6,0,1.1,120)
        _Alpha ("Base Alpha", Range(0.1, 0.95)) = 0.72
        _WaterFogDensity ("Water Column Fog Density", Range(0.01,0.25)) = 0.085
        _Smoothness ("Smoothness", Range(0, 1)) = 0.82
        _SpecularStrength ("Specular Strength", Range(0, 1)) = 0.8

        _WaveAmplitude1 ("Wave Amplitude 1", Float) = 0.45
        _WaveLength1 ("Wave Length 1", Float) = 20
        _WaveSpeed1 ("Wave Speed 1", Float) = 1.15
        _WaveDirection1 ("Wave Direction 1", Vector) = (1, 0.35, 0, 0)

        _WaveAmplitude2 ("Wave Amplitude 2", Float) = 0.22
        _WaveLength2 ("Wave Length 2", Float) = 9
        _WaveSpeed2 ("Wave Speed 2", Float) = 1.75
        _WaveDirection2 ("Wave Direction 2", Vector) = (-0.4, 1, 0, 0)

        _WaveAmplitude3 ("Wave Amplitude 3", Float) = 0.08
        _WaveLength3 ("Wave Length 3", Float) = 4
        _WaveSpeed3 ("Wave Speed 3", Float) = 2.4
        _WaveDirection3 ("Wave Direction 3", Vector) = (0.8, -0.65, 0, 0)

        [HideInInspector] _IslandWaveEnabled ("Island Waves", Float) = 0
        [HideInInspector] _IslandWaveCenter ("Island center and blend radii", Vector) = (0,0,100,260)
        [HideInInspector] _IslandWaveAmplitude ("Island amplitude", Float) = 2.8
        [HideInInspector] _IslandWaveSpeed ("Island speed", Float) = 1.65
        [HideInInspector] _OceanTime ("Ocean Time", Float) = 0
        [HideInInspector] _WavePhases ("Wave phases", Vector) = (0,0,0,0)
        [HideInInspector] _IslandWavePhases ("Island wave phases", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-5" "RenderType"="Opaque" }
        Pass
        {
            Name "DistantOcean"
            Tags { "LightMode"="UniversalForward" }
            Blend Off
            ZWrite On
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _FoamColor;
                float _Alpha;
                float _FoamStrength;
                float4 _ContactFoamParams;
                float _ContactFoamExclusionCount;
                float4 _ContactFoamExclusions[16];
                float _WaterFogDensity;
                float _Smoothness;
                float _SpecularStrength;
                float _ReferenceLook;
                float _RippleStrength;
                float _GerstnerStrength;

                float _WaveAmplitude1;
                float _WaveLength1;
                float _WaveSpeed1;
                float4 _WaveDirection1;

                float _WaveAmplitude2;
                float _WaveLength2;
                float _WaveSpeed2;
                float4 _WaveDirection2;

                float _WaveAmplitude3;
                float _WaveLength3;
                float _WaveSpeed3;
                float4 _WaveDirection3;

                float _OceanTime;
                float4 _WavePhases;
                float4 _IslandWavePhases;
                float _IslandWaveEnabled;
                float4 _IslandWaveCenter;
                float _IslandWaveAmplitude;
                float _IslandWaveSpeed;
            CBUFFER_END
            #include "../Shaders/OceanGerstner.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 edgeDistance : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 p = TransformObjectToWorld(input.positionOS.xyz);
                float2 offset, derivative;
                float4 jacobian;
                float height;
                OceanGerstnerSample(p.xz, offset, jacobian, height, derivative);
                // Exact existing waves/normals on the shared boundary. Remove
                // undersampled geometry before reaching the large outer triangles.
                float waveWeight = 1.0 - smoothstep(0.0, 225.0, input.edgeDistance.x);
                p.xz += offset * waveWeight;
                p.y += height * waveWeight;
                float3 tx = float3(1.0 + jacobian.x, derivative.x, jacobian.z);
                float3 tz = float3(jacobian.y, derivative.y, 1.0 + jacobian.w);
                output.normalWS = normalize(lerp(float3(0,1,0), normalize(cross(tz,tx)), waveWeight));
                output.positionWS = p;
                output.positionCS = TransformWorldToHClip(p);
                // Extend ONLY this ocean beyond the camera's existing far plane.
                // Keep xy and w unchanged, preserving perspective/interpolation.
                #if UNITY_REVERSED_Z
                    output.positionCS.z = max(output.positionCS.z, 0.0);
                #else
                    output.positionCS.z = min(output.positionCS.z, output.positionCS.w);
                #endif
                return output;
            }

            struct Output
            {
                half4 color : SV_Target;
                float depth : SV_Depth;
            };
            Output Frag(Varyings input)
            {
                Output output;
                float3 toCamera = GetWorldSpaceViewDir(input.positionWS);
                float cameraDistance = max(length(toCamera), 0.001);
                half3 viewDirection = toCamera / cameraDistance;
                half3 normalWS = normalize(input.normalWS);
                Light light = GetMainLight();
                half diffuse = saturate(dot(normalWS, light.direction));
                half fresnel = 1.0 - saturate(abs(dot(normalWS, viewDirection)));
                fresnel *= fresnel;
                fresnel *= fresnel;
                half3 deepColor = _DeepColor.rgb;
                if (_ReferenceLook > 0.5) deepColor *= half3(0.65, 0.66, 1.18);
                half3 color = deepColor * (0.55 + light.color * diffuse * 0.45);
                // Same distant lighting as OceanWater once its ripple/foam fade is zero.
                half horizon = saturate((cameraDistance - 75.0) / 325.0);
                half3 skyTint = lerp(half3(0.12,0.30,0.49), half3(0.28,0.43,0.57), horizon);
                skyTint *= 0.40 + light.color * 0.60;
                if (_ReferenceLook > 0.5) color = lerp(color, skyTint, fresnel * 0.48);
                half3 halfDirection = SafeNormalize(light.direction + viewDirection);
                half power = _ReferenceLook > 0.5 ? 48.0 : lerp(36.0,220.0,_Smoothness);
                half specular = pow(saturate(dot(normalWS, halfDirection)), power);
                color += specular * light.color * _SpecularStrength * (_ReferenceLook > 0.5 ? 0.08 : 1.0);
                // Water-only haze, starting well outside the unchanged near patch.
                color = lerp(color, skyTint, smoothstep(900.0, 6000.0, cameraDistance) * 0.65);
                output.color = half4(color, 1);

                // Reconstruct actual depth per pixel. Simply interpolating clamped
                // vertex depth would incorrectly paint large triangles over islands.
                // This also preserves correct terrain occlusion on GLES and Vulkan.
                float4 clipPosition = TransformWorldToHClip(input.positionWS);
                float depth = clipPosition.z / clipPosition.w;
                #if UNITY_REVERSED_Z
                    output.depth = saturate(depth);
                #else
                    depth = (depth - UNITY_NEAR_CLIP_VALUE) / (1.0 - UNITY_NEAR_CLIP_VALUE);
                    output.depth = saturate(depth);
                #endif
                return output;
            }
            ENDHLSL
        }
    }
}
