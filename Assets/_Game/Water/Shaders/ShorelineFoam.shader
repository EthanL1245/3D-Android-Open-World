Shader "OpenWorld/ShorelineFoam"
{
    Properties
    {
        _MainTex ("Tileable foam breakup", 2D) = "white" {}
        _FoamWidth ("Width", Float) = .65
        _CycleSeconds ("Wash cycle", Float) = 2.8
        _BaseLevel ("Water level", Float) = 0
        _FadeRange ("Distance fade", Vector) = (85,140,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+9" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float _BaseLevel, _FoamWidth, _CycleSeconds, _GerstnerStrength;
                float4 _FadeRange;
                float _WaveAmplitude1, _WaveLength1, _WaveAmplitude2, _WaveLength2;
                float _WaveAmplitude3, _WaveLength3;
                float4 _WaveDirection1, _WaveDirection2, _WaveDirection3;
                float4 _WavePhases, _IslandWavePhases, _IslandWaveCenter;
                float _IslandWaveEnabled, _IslandWaveAmplitude;
            CBUFFER_END
            #include "OceanGerstner.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 worldXZ : TEXCOORD0;
                float shoreDistance : TEXCOORD1;
                half2 fadeFog : TEXCOORD2;
            };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                float2 offset, gradient; float4 jacobian; float height;
                // Invert horizontal orbit to keep the baked shoreline fixed in XZ.
                float2 p = OceanUndisplacedPosition(world.xz);
                OceanGerstnerSample(p, offset, jacobian, height, gradient);
                world.y = _BaseLevel + height + .035;
                o.positionCS = TransformWorldToHClip(world);
                o.worldXZ = world.xz;
                o.shoreDistance = input.uv.x;
                o.fadeFog = half2(1 - smoothstep(_FadeRange.x, _FadeRange.y,
                    distance(_WorldSpaceCameraPos.xz, world.xz)), ComputeFogFactor(o.positionCS.z));
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float t = _Time.y / max(1, _CycleSeconds);
                half broad = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,
                    i.worldXZ * .7 + float2(t * .035, -t * .05)).r;
                half fine = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,
                    i.worldXZ * 2.3 + float2(-t * .04, t * .025)).g;
                float wash = .5 + .5 * sin(t * 6.2831853 + broad * 2.2);
                float edge = _FoamWidth * lerp(.48, 1.0, wash);
                float d = i.shoreDistance;
                half envelope = smoothstep(0, .09, d) * (1 - smoothstep(edge * .55, edge, d));
                half lace = smoothstep(.27h, .68h, broad * .65h + fine * .35h);
                half alpha = envelope * lace * lerp(.52, .85, wash) * i.fadeFog.x;
                half3 color = lerp(half3(.72,.86,.87), half3(.97,.99,1), lace);
                return half4(MixFog(color, i.fadeFog.y), alpha);
            }
            ENDHLSL
        }
    }
}
