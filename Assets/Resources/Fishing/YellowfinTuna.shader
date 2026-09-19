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

        _SwimStrength ("Tail Angle", Float) = 0.045
        _SwimSpeed ("Tail Speed", Float) = 3.25
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

            float3 RotateAroundAxis(
                float3 point,
                float3 pivot,
                float3 axis,
                float angle)
            {
                float3 relative =
                    point - pivot;

                float sine =
                    sin(angle);

                float cosine =
                    cos(angle);

                return
                    pivot +
                    relative * cosine +
                    cross(
                        axis,
                        relative
                    ) * sine +
                    axis *
                    dot(
                        axis,
                        relative
                    ) *
                    (1.0 - cosine);
            }

            float3 DeformPosition(float3 positionOS)
            {
                float range =
                    max(
                        0.0001,
                        _BodyMax - _BodyMin
                    );

                float along =
                    dot(
                        positionOS,
                        _BodyAxis.xyz
                    );

                float normalized =
                    saturate(
                        (along - _BodyMin) /
                        range
                    );

                // 0 at the head, 1 at the tail.
                float tailPosition =
                    _TailAtMin > 0.5
                        ? 1.0 - normalized
                        : normalized;

                // Tuna keep the front of the body comparatively rigid.
                // Only the rear third participates, with the tail doing most of the work.
                float rearWeight =
                    smoothstep(
                        0.66,
                        0.88,
                        tailPosition
                    );

                float tailWeight =
                    smoothstep(
                        0.84,
                        1.0,
                        tailPosition
                    );

                if (rearWeight <= 0.0001)
                {
                    return positionOS;
                }

                float3 headToTailAxis =
                    _TailAtMin > 0.5
                        ? -normalize(
                            _BodyAxis.xyz
                        )
                        : normalize(
                            _BodyAxis.xyz
                        );

                float3 sideAxis =
                    normalize(
                        _SideAxis.xyz
                    );

                float3 upAxis =
                    normalize(
                        cross(
                            headToTailAxis,
                            sideAxis
                        )
                    );

                float pivotNormalized =
                    _TailAtMin > 0.5
                        ? 1.0 - 0.66
                        : 0.66;

                float pivotAlong =
                    lerp(
                        _BodyMin,
                        _BodyMax,
                        pivotNormalized
                    );

                float3 pivot =
                    normalize(
                        _BodyAxis.xyz
                    ) *
                    pivotAlong;

                float beat =
                    sin(
                        _Time.y *
                        _SwimSpeed +
                        _SwimPhase
                    );

                // Rear body bends a few degrees.
                float rearAngle =
                    beat *
                    _SwimStrength *
                    rearWeight;

                // The caudal fin adds a little extra snap without creating a body wave.
                float tailAngle =
                    beat *
                    _SwimStrength *
                    0.85 *
                    tailWeight;

                float3 bent =
                    RotateAroundAxis(
                        positionOS,
                        pivot,
                        upAxis,
                        rearAngle
                    );

                if (tailWeight > 0.0001)
                {
                    float tailPivotNormalized =
                        _TailAtMin > 0.5
                            ? 1.0 - 0.84
                            : 0.84;

                    float tailPivotAlong =
                        lerp(
                            _BodyMin,
                            _BodyMax,
                            tailPivotNormalized
                        );

                    float3 tailPivot =
                        normalize(
                            _BodyAxis.xyz
                        ) *
                        tailPivotAlong;

                    bent =
                        RotateAroundAxis(
                            bent,
                            tailPivot,
                            upAxis,
                            tailAngle
                        );
                }

                return bent;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float3 positionOS =
                    DeformPosition(
                        input.positionOS.xyz
                    );

                VertexPositionInputs pos =
                    GetVertexPositionInputs(
                        positionOS
                    );

                VertexNormalInputs normal =
                    GetVertexNormalInputs(
                        input.normalOS
                    );

                output.positionCS =
                    pos.positionCS;

                output.positionWS =
                    pos.positionWS;

                output.normalWS =
                    normal.normalWS;

                output.uv =
                    TRANSFORM_TEX(
                        input.uv,
                        _BaseMap
                    );

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float4 tex =
                    SAMPLE_TEXTURE2D(
                        _BaseMap,
                        sampler_BaseMap,
                        input.uv
                    );

                float3 normalWS =
                    normalize(
                        input.normalWS
                    );

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
                            22.0,
                            150.0,
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

                float3 ambient =
                    SampleSH(
                        normalWS
                    );

                float3 baseColor =
                    tex.rgb *
                    _BaseColor.rgb;

                float3 color =
                    baseColor *
                    (
                        ambient * 0.82 +
                        mainLight.color *
                        (
                            0.20 +
                            ndotl * 0.80
                        )
                    );

                color +=
                    mainLight.color *
                    specular *
                    lerp(
                        0.06,
                        0.20,
                        _Smoothness
                    );

                color +=
                    fresnel *
                    _FresnelStrength *
                    float3(
                        0.08,
                        0.12,
                        0.13
                    );

                return half4(
                    color,
                    1.0
                );
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
