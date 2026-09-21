Shader "OpenWorld/ShopSurface"
{
    Properties
    {
        [MainTexture] _BaseMap("Surface texture",2D)="white"{}
        [MainColor] _BaseColor("Color",Color)=(1,1,1,1)
        _Smoothness("Smoothness",Range(0,1))=0.3
        _Metallic("Metallic",Range(0,1))=0
        [HideInInspector] _Cull("Cull",Float)=2
        [HideInInspector] _AlphaClip("Alpha clip",Float)=0
        [HideInInspector] _Cutoff("Cutoff",Float)=0.5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;half4 _BaseColor;half _Smoothness;half _Metallic;
            CBUFFER_END
            struct A {float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;};
            struct V {float4 p:SV_POSITION;float3 world:TEXCOORD0;float3 n:TEXCOORD1;float2 uv:TEXCOORD2;half fog:TEXCOORD3;half3 vertexLight:TEXCOORD4;};
            V Vert(A i)
            {
                V o;o.world=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.world);o.n=TransformObjectToWorldNormal(i.n);
                o.uv=TRANSFORM_TEX(i.uv,_BaseMap);o.fog=ComputeFogFactor(o.p.z);o.vertexLight=VertexLighting(o.world,o.n);return o;
            }
            half3 Illuminate(Light light,float3 n,float3 view,half3 albedo)
            {
                half diffuse=saturate(dot(n,light.direction));
                half spec=pow(saturate(dot(n,SafeNormalize(view+light.direction))),lerp(16,96,_Smoothness));
                return light.color*light.distanceAttenuation*light.shadowAttenuation*(albedo*diffuse+lerp(half3(0.04,0.04,0.04),albedo,_Metallic)*spec*_Smoothness);
            }
            half4 Frag(V i):SV_Target
            {
                half3 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb;
                float3 n=normalize(i.n),view=SafeNormalize(GetWorldSpaceViewDir(i.world));
                half3 color=albedo*(max(SampleSH(n),half3(0.1,0.1,0.1))+i.vertexLight);
                color+=Illuminate(GetMainLight(TransformWorldToShadowCoord(i.world)),n,view,albedo);
                #if defined(_ADDITIONAL_LIGHTS)
                uint count=GetAdditionalLightsCount();
                for(uint index=0u;index<count;index++)color+=Illuminate(GetAdditionalLight(index,i.world),n,view,albedo);
                #endif
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
