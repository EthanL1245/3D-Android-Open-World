Shader "Fishing/SnapperRockBoxProjection"
{
    Properties
    {
        [MainTexture] _BaseMap("Rock texture", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (0.88,0.86,0.82,1)
        _Tiling("World tiling", Range(0.05,1.0)) = 0.22
        _Smoothness("Smoothness", Range(0,1)) = 0.16
        _AmbientLift("Ambient lift", Range(0,1)) = 0.16
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Tiling;
                half _Smoothness;
                half _AmbientLift;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.shadowCoord = TransformWorldToShadowCoord(pos.positionWS);
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            half3 SampleRock(float3 worldPos, half3 worldNormal)
            {
                half3 a = abs(normalize(worldNormal));
                float2 uv;
                if(a.x >= a.y && a.x >= a.z) uv = worldPos.zy;
                else if(a.y >= a.z) uv = worldPos.xz;
                else uv = worldPos.xy;
                uv = uv * _Tiling + _BaseMap_ST.zw;
                return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb * _BaseColor.rgb;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 n = normalize(input.normalWS);
                half3 albedo = SampleRock(input.positionWS, n);
                Light mainLight = GetMainLight(input.shadowCoord);
                half ndl = saturate(dot(n, mainLight.direction));
                // Two-sided cards must light naturally from either face.
                ndl = max(ndl, saturate(dot(-n, mainLight.direction)) * 0.72h);
                half3 ambient = max(SampleSH(n), half3(_AmbientLift,_AmbientLift,_AmbientLift));
                half3 color = albedo * (ambient + mainLight.color * ndl * mainLight.shadowAttenuation);
                return half4(MixFog(color, input.fogFactor), 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}