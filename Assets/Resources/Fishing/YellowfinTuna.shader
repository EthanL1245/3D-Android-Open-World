Shader "OpenWorld/YellowfinTuna"
{
    Properties
    {
        _BaseMap ("Yellowfin Texture", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _Smoothness ("Skin Smoothness", Range(0,1)) = 0.34
        _FresnelStrength ("Wet Edge Highlight", Range(0,1)) = 0.055

        _BodyAxis ("Body Axis", Vector) = (1,0,0,0)
        _SideAxis ("Side Axis", Vector) = (0,1,0,0)
        _BodyMin ("Body Min", Float) = -1
        _BodyMax ("Body Max", Float) = 1
        _TailAtMin ("Tail At Min", Float) = 1

        _SwimStrength ("Rear Bend Strength", Float) = 0.17
        _SwimSpeed ("Tail Beat Speed", Float) = 8.0
        _SwimPhase ("Swim Phase", Float) = 0
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

                float4 _BodyAxis;
                float4 _SideAxis;
                float _BodyMin;
                float _BodyMax;
                float _TailAtMin;

                float _SwimStrength;
                float _SwimSpeed;
                float _SwimPhase;
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

            float3 DeformFish(float3 positionOS)
            {
                float bodyRange =
                    max(
                        0.0001,
                        _BodyMax - _BodyMin
                    );

                float alongBody =
                    dot(
                        positionOS,
                        _BodyAxis.xyz
                    );

                float normalizedBody =
                    saturate(
                        (alongBody - _BodyMin) /
                        bodyRange
                    );

                float tailPosition =
                    normalizedBody;

                if (_TailAtMin > 0.5)
                {
                    tailPosition =
                        1.0 - normalizedBody;
                }

                // 0 through the front 68% of the fish.
                // The deformation then rises smoothly toward the tail.
                float rearAmount =
                    saturate(
                        (tailPosition - 0.52) /
                        0.48
                    );

                rearAmount =
                    rearAmount *
                    rearAmount *
                    (
                        3.0 -
                        2.0 * rearAmount
                    );

                float tailAmount =
                    saturate(
                        (tailPosition - 0.80) /
                        0.20
                    );

                tailAmount =
                    tailAmount *
                    tailAmount *
                    (
                        3.0 -
                        2.0 * tailAmount
                    );

                float tailDistance =
                    max(
                        0.0,
                        tailPosition - 0.52
                    ) *
                    bodyRange;

                float beat =
                    sin(
                        _Time.y *
                        _SwimSpeed +
                        _SwimPhase
                    );

                float sideOffset =
                    beat *
                    _SwimStrength *
                    tailDistance *
                    rearAmount *
                    (
                        0.34 +
                        rearAmount * 0.30 +
                        tailAmount * 0.56
                    );

                positionOS +=
                    normalize(
                        _SideAxis.xyz
                    ) *
                    sideOffset;

                return positionOS;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float3 deformedPosition =
                    DeformFish(
                        input.positionOS.xyz
                    );

                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(
                        deformedPosition
                    );

                VertexNormalInputs normalInputs =
                    GetVertexNormalInputs(
                        input.normalOS
                    );

                output.positionCS =
                    positionInputs.positionCS;

                output.positionWS =
                    positionInputs.positionWS;

                output.normalWS =
                    normalInputs.normalWS;

                output.uv =
                    TRANSFORM_TEX(
                        input.uv,
                        _BaseMap
                    );

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float4 textureSample =
                    SAMPLE_TEXTURE2D(
                        _BaseMap,
                        sampler_BaseMap,
                        input.uv
                    );

                float3 normalWS =
                    normalize(
                        input.normalWS
                    );

                float3 viewDirection =
                    SafeNormalize(
                        GetWorldSpaceViewDir(
                            input.positionWS
                        )
                    );

                Light mainLight =
                    GetMainLight();

                float diffuseAmount =
                    saturate(
                        dot(
                            normalWS,
                            mainLight.direction
                        )
                    );

                float3 halfDirection =
                    SafeNormalize(
                        mainLight.direction +
                        viewDirection
                    );

                float specularAmount =
                    pow(
                        saturate(
                            dot(
                                normalWS,
                                halfDirection
                            )
                        ),
                        lerp(
                            18.0,
                            95.0,
                            _Smoothness
                        )
                    );

                float fresnelAmount =
                    pow(
                        1.0 -
                        saturate(
                            dot(
                                normalWS,
                                viewDirection
                            )
                        ),
                        3.0
                    );

                float3 ambientLight =
                    SampleSH(
                        normalWS
                    );

                float3 baseColor =
                    textureSample.rgb *
                    _BaseColor.rgb;

                float3 finalColor =
                    baseColor *
                    (
                        ambientLight * 0.82 +
                        mainLight.color *
                        (
                            0.20 +
                            diffuseAmount * 0.80
                        )
                    );

                finalColor +=
                    mainLight.color *
                    specularAmount *
                    lerp(
                        0.045,
                        0.15,
                        _Smoothness
                    );

                finalColor +=
                    fresnelAmount *
                    _FresnelStrength *
                    float3(
                        0.08,
                        0.12,
                        0.13
                    );

                return half4(
                    finalColor,
                    1.0
                );
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
