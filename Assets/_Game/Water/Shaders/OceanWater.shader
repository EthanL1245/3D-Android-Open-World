Shader "OpenWorld/OceanWater"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.05, 0.55, 0.72, 1)
        _DeepColor ("Deep Color", Color) = (0.01, 0.10, 0.28, 1)
        _FoamColor ("Foam Color", Color) = (0.86, 0.96, 1.0, 1)
        _FoamStrength ("Foam Strength", Range(0,1)) = 1
        _Alpha ("Base Alpha", Range(0.1, 0.95)) = 0.72
        _Smoothness ("Smoothness", Range(0, 1)) = 0.82

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
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
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
                float _Smoothness;

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
                float _IslandWaveEnabled;
                float4 _IslandWaveCenter;
                float _IslandWaveAmplitude;
                float _IslandWaveSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float waveHeight : TEXCOORD2;
            };

            float2 NormalizeDirection(float2 direction)
            {
                float lengthSquared = dot(direction, direction);

                if (lengthSquared < 0.0001)
                    return float2(1.0, 0.0);

                return direction * rsqrt(lengthSquared);
            }

            void EvaluateWave(
                float2 position,
                float time,
                float amplitude,
                float wavelength,
                float speed,
                float2 direction,
                out float height,
                out float2 derivative)
            {
                float2 dir = NormalizeDirection(direction);
                float frequency = 6.28318530718 / max(0.1, wavelength);
                float phase =
                    dot(position, dir) * frequency +
                    time * speed;

                height = sin(phase) * amplitude;

                derivative =
                    cos(phase) *
                    amplitude *
                    frequency *
                    dir;
                if (_IslandWaveEnabled > 0.5)
                {
                    float2 delta = position - _IslandWaveCenter.xy;
                    float distance = length(delta);
                    float width = max(0.01, _IslandWaveCenter.w - _IslandWaveCenter.z);
                    float t = saturate((distance - _IslandWaveCenter.z) / width);
                    float blend = 1.0 - t*t*(3.0 - 2.0*t);
                    float roughPhase = dot(position,dir)*frequency + time*speed*_IslandWaveSpeed;
                    float roughHeight = sin(roughPhase)*amplitude*_IslandWaveAmplitude;
                    float2 roughDerivative = cos(roughPhase)*amplitude*_IslandWaveAmplitude*frequency*dir;
                    float2 gradient = (-6.0*t*(1.0-t)/width)*delta/max(distance,0.001);
                    derivative = lerp(derivative,roughDerivative,blend)+(roughHeight-height)*gradient;
                    height = lerp(height,roughHeight,blend);
                }
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float3 worldPosition =
                    TransformObjectToWorld(input.positionOS.xyz);

                float height1;
                float height2;
                float height3;

                float2 derivative1;
                float2 derivative2;
                float2 derivative3;

                EvaluateWave(
                    worldPosition.xz,
                    _OceanTime,
                    _WaveAmplitude1,
                    _WaveLength1,
                    _WaveSpeed1,
                    _WaveDirection1.xy,
                    height1,
                    derivative1
                );

                EvaluateWave(
                    worldPosition.xz,
                    _OceanTime,
                    _WaveAmplitude2,
                    _WaveLength2,
                    _WaveSpeed2,
                    _WaveDirection2.xy,
                    height2,
                    derivative2
                );

                EvaluateWave(
                    worldPosition.xz,
                    _OceanTime,
                    _WaveAmplitude3,
                    _WaveLength3,
                    _WaveSpeed3,
                    _WaveDirection3.xy,
                    height3,
                    derivative3
                );

                float totalHeight =
                    height1 + height2 + height3;

                float2 totalDerivative =
                    derivative1 + derivative2 + derivative3;

                worldPosition.y += totalHeight;

                output.positionWS = worldPosition;
                output.positionCS =
                    TransformWorldToHClip(worldPosition);

                output.normalWS = normalize(
                    float3(
                        -totalDerivative.x,
                        1.0,
                        -totalDerivative.y
                    )
                );

                output.waveHeight = totalHeight;

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirection =
                    SafeNormalize(
                        GetWorldSpaceViewDir(input.positionWS)
                    );

                Light mainLight = GetMainLight();

                float diffuse =
                    saturate(
                        dot(normalWS, mainLight.direction)
                    );

                float fresnel =
                    pow(
                        1.0 -
                        saturate(
                            abs(
                                dot(
                                    normalWS,
                                    viewDirection
                                )
                            )
                        ),
                        4.0
                    );

                float3 baseColor =
                    lerp(
                        _ShallowColor.rgb,
                        _DeepColor.rgb,
                        saturate(
                            0.25 + fresnel * 0.75
                        )
                    );

                float3 halfDirection =
                    SafeNormalize(
                        mainLight.direction +
                        viewDirection
                    );

                float specularPower =
                    lerp(
                        36.0,
                        220.0,
                        _Smoothness
                    );

                float specular =
                    pow(
                        saturate(
                            dot(
                                normalWS,
                                halfDirection
                            )
                        ),
                        specularPower
                    );

                float totalAmplitude =
                    max(
                        0.001,
                        _WaveAmplitude1 +
                        _WaveAmplitude2 +
                        _WaveAmplitude3
                    );

                float normalizedCrest =
                    input.waveHeight /
                    totalAmplitude;

                float foam =
                    smoothstep(
                        0.62,
                        0.94,
                        normalizedCrest
                    );

                foam *= _FoamStrength;

                float lighting =
                    0.35 + diffuse * 0.65;

                float3 color =
                    baseColor *
                    lighting *
                    mainLight.color;

                color =
                    lerp(
                        color,
                        _FoamColor.rgb,
                        foam * 0.65
                    );

                color +=
                    specular *
                    mainLight.color *
                    0.8;

                float alpha =
                    saturate(
                        _Alpha +
                        fresnel * 0.16 +
                        foam * 0.10
                    );

                return half4(color, alpha);
            }

            ENDHLSL
        }
    }
}


