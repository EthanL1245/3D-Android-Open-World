Shader "OpenWorld/FishingHotspotParticles"
{
    Properties
    {
        _MainTex ("Particle Texture", 2D) = "white" {}
        _FollowSurface ("Conform to ocean", Float) = 0
        _EffectAlpha ("Cast ripple opacity", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float _FollowSurface, _HotspotSeaLevel, _EffectAlpha;
                float _WaveAmplitude1, _WaveAmplitude2, _WaveAmplitude3;
                float _WaveLength1, _WaveLength2, _WaveLength3;
                float4 _WaveDirection1, _WaveDirection2, _WaveDirection3;
                float4 _WavePhases, _IslandWavePhases, _IslandWaveCenter;
                float _IslandWaveEnabled, _IslandWaveAmplitude;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; half fog:TEXCOORD1; };
            float Wave(float2 p, float amplitude, float wavelength, float2 direction, float phase, float islandPhase)
            {
                direction = dot(direction,direction) > 0.0001 ? normalize(direction) : float2(1,0);
                float spatial = dot(p,direction) * 6.28318530718 / max(0.1,wavelength);
                float height = sin(spatial + phase) * amplitude;
                if (_IslandWaveEnabled > 0.5)
                {
                    float t = saturate((distance(p,_IslandWaveCenter.xy)-_IslandWaveCenter.z) /
                        max(0.01,_IslandWaveCenter.w-_IslandWaveCenter.z));
                    float blend = 1.0-t*t*(3.0-2.0*t);
                    height = lerp(height,sin(spatial+islandPhase)*amplitude*_IslandWaveAmplitude,blend);
                }
                return height;
            }
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                if (_FollowSurface > 0.5)
                {
                    world.y = _HotspotSeaLevel + 0.055 +
                        Wave(world.xz,_WaveAmplitude1,_WaveLength1,_WaveDirection1.xy,_WavePhases.x,_IslandWavePhases.x) +
                        Wave(world.xz,_WaveAmplitude2,_WaveLength2,_WaveDirection2.xy,_WavePhases.y,_IslandWavePhases.y) +
                        Wave(world.xz,_WaveAmplitude3,_WaveLength3,_WaveDirection3.xy,_WavePhases.z,_IslandWavePhases.z);
                }
                output.positionCS = TransformWorldToHClip(world);
                output.color = input.color;
                output.uv = input.uv;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv) * input.color;
                // Default 1.0 keeps existing hotspot and cast-splash appearance unchanged.
                color.a *= saturate(_EffectAlpha);
                color.rgb = MixFog(color.rgb,input.fog);
                return color;
            }
            ENDHLSL
        }
    }
}
