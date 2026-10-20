Shader "OpenWorld/MotorboatWake"
{
    Properties
    {
        _MainTex ("Procedural foam", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Name "Wake"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            // Respect the same cockpit exclusion as the ocean.
            Stencil { Ref 1 ReadMask 1 WriteMask 0 Comp NotEqual Pass Keep }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float _BaseLevel, _WakeTime, _GerstnerStrength;
                float _WaveAmplitude1, _WaveLength1;
                float _WaveAmplitude2, _WaveLength2;
                float _WaveAmplitude3, _WaveLength3;
                float4 _WaveDirection1, _WaveDirection2, _WaveDirection3;
                float4 _WavePhases, _IslandWavePhases, _IslandWaveCenter;
                float _IslandWaveEnabled, _IslandWaveAmplitude;
            CBUFFER_END
            #include "OceanGerstner.hlsl"
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 foam : TEXCOORD1;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                float2 offset, gradient;
                float4 jacobian;
                float height;
                // Evaluate the same parametric surface as the water itself; wave
                // math runs only on these few vertices, never in the fragment shader.
                OceanGerstnerSample(world.xz, offset, jacobian, height, gradient);
                world.xz += offset;
                world.y = _BaseLevel + height + .045;
                output.positionCS = TransformWorldToHClip(world);
                float age = max(0, _WakeTime - input.color.r);
                float fade = saturate(1 - age / 4.0);
                float range = distance(_WorldSpaceCameraPos.xz, world.xz);
                float distanceFade = 1 - smoothstep(100.0, 140.0, range);
                float aboveWater = smoothstep(-.3, .15, _WorldSpaceCameraPos.y - (_BaseLevel + height));
                output.uv = input.uv;
                output.foam = half4(input.color.a, input.color.g * fade * fade * distanceFade * aboveWater,
                    input.color.b, ComputeFogFactor(output.positionCS.z));
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float scroll = _Time.y * (.13 + input.foam.z * .22);
                half broad = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,
                    input.uv * float2(1.7, 1) + float2(scroll * .21, -scroll)).r;
                half detail = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,
                    input.uv * float2(3.1, 1.83) + float2(-scroll * .32, scroll * .6)).r;
                half foam = smoothstep(.20h, .73h, broad * .7h + detail * .3h);
                half alpha = foam * input.foam.x * input.foam.y;
                half3 color = lerp(half3(.65, .83, .86), half3(.96, .99, 1), foam);
                return half4(MixFog(color, input.foam.w), alpha);
            }
            ENDHLSL
        }
    }
}
