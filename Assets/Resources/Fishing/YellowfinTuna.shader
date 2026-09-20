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

        _PathYaw25 ("Path Yaw 25%", Float) = 0
        _PathYaw50 ("Path Yaw 50%", Float) = 0
        _PathYaw75 ("Path Yaw 75%", Float) = 0
        _PathYaw100 ("Path Yaw 100%", Float) = 0
        _PathSideSign ("Path Side Sign", Float) = 1
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

                float _PathYaw25;
                float _PathYaw50;
                float _PathYaw75;
                float _PathYaw100;
                float _PathSideSign;
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

            float SamplePathYaw(float t)
            {
                t =
                    saturate(
                        t
                    );

                if (t <= 0.25)
                {
                    float u =
                        smoothstep(
                            0.0,
                            1.0,
                            t / 0.25
                        );

                    return
                        lerp(
                            0.0,
                            _PathYaw25,
                            u
                        );
                }

                if (t <= 0.50)
                {
                    float u =
                        smoothstep(
                            0.0,
                            1.0,
                            (t - 0.25) / 0.25
                        );

                    return
                        lerp(
                            _PathYaw25,
                            _PathYaw50,
                            u
                        );
                }

                if (t <= 0.75)
                {
                    float u =
                        smoothstep(
                            0.0,
                            1.0,
                            (t - 0.50) / 0.25
                        );

                    return
                        lerp(
                            _PathYaw50,
                            _PathYaw75,
                            u
                        );
                }

                float u =
                    smoothstep(
                        0.0,
                        1.0,
                        (t - 0.75) / 0.25
                    );

                return
                    lerp(
                        _PathYaw75,
                        _PathYaw100,
                        u
                    );
            }

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
                        (tailPosition - 0.24) /
                        0.76
                    );

                bodyFlex =
                    bodyFlex *
                    bodyFlex *
                    (
                        3.0 -
                        2.0 * bodyFle                // ACTUAL HEAD-PATH FOLLOWING:
                // These angles are historical headings sampled by physical
                // distance behind the head/root. A body point therefore uses
                // the heading the head had when it previously occupied that
                // part of the curve. The curve develops naturally over time.
                float3 curvedCenter =
                    float3(
                        0.0,
                        0.0,
                        0.0
                    );

                // Integrate the historical tangent field from the head to
                // this vertex. Eight small segments are enough for this
                // aquarium-scale mesh and avoid any instant full-body bend.
                const int PATH_STEPS = 8;

                float stepT =
                    tailPosition /
                    PATH_STEPS;

                [unroll]
                for (int step = 0;
                     step < PATH_STEPS;
                     step++)
                {
                    float sampleT =
                        (
                            step +
                            0.5
                        ) *
                        stepT;

                    float angle =
                        SamplePathYaw(
                            sampleT
                        );

                    float3 gameRightAxis =
                        sideAxis *
                        _PathSideSign;

                    // tailAxis points head -> tail. A positive historical yaw
                    // rotates that backwards tangent toward -gameRightAxis.
                    float3 tangent =
                        tailAxis *
                        cos(
                            angle
                        ) -
                        gameRightAxis *
                        sin(
                            angle
                        );

                    curvedCenter +=
                        tangent *
                        (
                            stepT *
                            bodyRange
                        );
                }

                float3 straightCenter =
                    tailAxis *
                    (
                        tailPosition *
                        bodyRange
                    );

                positionOS +=
                    curvedCenter -
                    straightCenter;

                float turnMagnitude =
                    saturate(
                        abs(
                            _PathYaw100
                        ) /
                        2.2
                    );

itude =
                    abs(
                        turnAmount
                    );

                // Tuna are stiff-bodied. Even a hard aquarium turn should
                // bend the posterior body by only about 20-24 degrees rather
                // than folding the whole animal into a U shape.
                float totalTurnAngle =
                    turnMagnitude *
                    0.42;

                if (rearT > 0.0 &&
                    totalTurnAngle > 0.001)
                {
                    float rearLength =
                        bodyRange *
                        0.42;

                    float distanceFromRear =
                        rearT *
                        rearLength;

                    float localAngle =
                        totalTurnAngle *
                        rearT;

                    float radius =
                        rearLength /
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

                    float3 straightRear =
                        tailAxis *
                        distanceFromRear;

                    float3 curvedRear =
                        tailAxis *
                        arcForward +
                        sideAxis *
                        arcSide;

                    positionOS +=
                        curvedRear -
                        straightRear;
                }

                // Normal tail beat remains, but it yields to the turn shape
                // as curvature increases so the tail does not fight momentum.
                float swimDuringTurn =
                    lerp(
                        1.0,
                        0.72,
                        turnMagnitude
                    );

                float sideOffset =
                    _SwimStrength *
                    bodyRange *
                    swimDuringTurn *
                    (
                        bodyFlex *
                        0.16 *
                        bodyBeat +
                        rearFlex *
                        0.30 *
                        rearBeat +
                        tailFlex *
                        0.54 *
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
