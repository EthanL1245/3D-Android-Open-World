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

        _SwimStrength ("Body Flex Strength", Float) = 0.17
        _SwimSpeed ("Tail Beat Speed", Float) = 5.8
        _SwimPhase ("Swim Phase", Float) = 0
        _TurnBend ("Turn Bend", Range(-1,1)) = 0
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
                float _TurnBend;
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

                // One coherent beat across the body.
                // The head stays stable, the mid-body participates visibly,
                // and the tail receives the largest displacement.
                float bodyFlex =
                    saturate(
                        (tailPosition - 0.12) /
                        0.88
                    );

                bodyFlex =
                    bodyFlex *
                    bodyFlex *
                    (
                        3.0 -
                        2.0 * bodyFlex
                    );

                float rearFlex =
                    saturate(
                        (tailPosition - 0.42) /
                        0.58
                    );

                rearFlex =
                    rearFlex *
                    rearFlex *
                    (
                        3.0 -
                        2.0 * rearFlex
                    );

                float tailFlex =
                    saturate(
                        (tailPosition - 0.74) /
                        0.26
                    );

                tailFlex =
                    tailFlex *
                    tailFlex *
                    (
                        3.0 -
                        2.0 * tailFlex
                    );

                // Yellowtail-style progressive flex:
                // the mid-body begins the stroke, the rear follows slightly
                // later, and the tail has the largest delayed kick.
                // The phase offsets are deliberately modest so this stays
                // tuna-like rather than becoming an eel wave.
                float bodyBeat =
                    sin(
                        _SwimPhase
                    );

                float rearBeat =
                    sin(
                        _SwimPhase -
                        0.30
                    );

                float tailBeat =
                    sin(
                        _SwimPhase -
                        0.58
                    );

                float3 bodyAxis =
                    normalize(
                        _BodyAxis.xyz
                    );

                float3 sideAxis =
                    normalize(
                        _SideAxis.xyz
                    );

                float3 tailAxis =
                    _TailAtMin > 0.5
                        ? -bodyAxis
                        : bodyAxis;

                // Curve the centerline itself during a turn instead of
                // rotating the fish like a rigid board. The head is t=0 and
                // remains fixed; each farther-back section follows a larger
                // portion of the arc. At a hard reversal the body naturally
                // becomes a strong C/U shape.
                float turnAmount =
                    clamp(
                        _TurnBend,
                        -1.0,
                        1.0
                    );

                float turnMagnitude =
                    abs(
                        turnAmount
                    );

                float totalTurnAngle =
                    turnMagnitude *
                    2.75;

                if (totalTurnAngle > 0.001)
                {
                    float distanceFromHead =
                        tailPosition *
                        bodyRange;

                    float localAngle =
                        totalTurnAngle *
                        tailPosition;

                    float radius =
                        bodyRange /
                        totalTurnAngle;

                    float arcForward =
                        sin(
                            localAngle
                        ) *
                        radius;

                    float arcSide =
                        (
                            1.0 -
                            cos(
                                localAngle
                            )
                        ) *
                        radius *
                        sign(
                            turnAmount
                        );

                    float3 straightCenter =
                        tailAxis *
                        distanceFromHead;

                    float3 curvedCenter =
                        tailAxis *
                        arcForward +
                        sideAxis *
                        arcSide;

                    positionOS +=
                        curvedCenter -
                        straightCenter;
                }

                // Normal tail beat remains, but it yields to the turn shape
                // as curvature increases so the tail does not fight momentum.
                float swimDuringTurn =
                    lerp(
                        1.0,
                        0.48,
                        turnMagnitude
                    );

                float sideOffset =
                    _SwimStrength *
                    bodyRange *
                    swimDuringTurn *
                    (
                        bodyFlex *
                        0.24 *
                        bodyBeat +
                        rearFlex *
                        0.34 *
                        rearBeat +
                        tailFlex *
                        0.42 *
                        tailBeat
                    );

                positionOS +=
                    sideAxis *
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
