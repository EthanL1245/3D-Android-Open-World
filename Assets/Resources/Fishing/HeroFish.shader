Shader "OpenWorld/HeroFish"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Smoothness ("Smoothness", Range(0,1)) = 0.72
        _FresnelStrength ("Fresnel", Range(0,1)) = 0.32
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Smoothness;
                float _FresnelStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs pos =
                    GetVertexPositionInputs(
                        input.positionOS.xyz
                    );

                VertexNormalInputs normal =
                    GetVertexNormalInputs(
                        input.normalOS
                    );

                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = normal.normalWS;
                output.uv =
                    TRANSFORM_TEX(
                        input.uv,
                        _BaseMap
                    );

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS =
                    normalize(input.normalWS);

                float3 viewDir =
                    SafeNormalize(
                        GetWorldSpaceViewDir(
                            input.positionWS
                        )
                    );

                Light mainLight =
                    GetMainLight();

                float ndotl =
                    saturate(
                        dot(
                            normalWS,
                            mainLight.direction
                        )
                    );

                float3 halfDir =
                    SafeNormalize(
                        mainLight.direction +
                        viewDir
                    );

                float specular =
                    pow(
                        saturate(
                            dot(
                                normalWS,
                                halfDir
                            )
                        ),
                        lerp(
                            18.0,
                            140.0,
                            _Smoothness
                        )
                    );

                float fresnel =
                    pow(
                        1.0 -
                        saturate(
                            dot(
                                normalWS,
                                viewDir
                            )
                        ),
                        3.0
                    );

                float4 tex =
                    SAMPLE_TEXTURE2D(
                        _BaseMap,
                        sampler_BaseMap,
                        input.uv
                    );

                float3 ambient =
                    SampleSH(normalWS);

                float3 color =
                    tex.rgb *
                    _BaseColor.rgb *
                    (
                        ambient * 0.72 +
                        mainLight.color *
                        (
                            0.24 +
                            ndotl * 0.76
                        )
                    );

                float3 iridescence =
                    lerp(
                        float3(0.05, 0.16, 0.22),
                        float3(0.12, 0.28, 0.20),
                        saturate(
                            normalWS.y * 0.5 + 0.5
                        )
                    );

                color +=
                    iridescence *
                    fresnel *
                    _FresnelStrength;

                color +=
                    mainLight.color *
                    specular *
                    lerp(
                        0.18,
                        0.55,
                        _Smoothness
                    );

                return half4(
                    color,
                    1.0
                );
            }

            ENDHLSL
        }
    }
}
