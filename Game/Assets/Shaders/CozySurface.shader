Shader "CatSimulator/CozySurface"
{
    Properties
    {
        _BaseColor("Color",Color)=(1,1,1,1)
        _Smoothness("Smoothness",Range(0,1))=.3
        _VertexColor("Coat Colors",Float)=0
        _Grain("Surface Grain",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Smoothness, _VertexColor, _Grain;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; half4 color:COLOR; half fog:TEXCOORD2; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS=p.positionCS; o.positionWS=p.positionWS;
                o.normalWS=TransformObjectToWorldNormal(v.normalOS); o.color=v.color;
                o.fog=ComputeFogFactor(p.positionCS.z); return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                half3 albedo=_BaseColor.rgb*lerp(half3(1,1,1),i.color.rgb,_VertexColor);
                float3 p=i.positionWS;
                if (_Grain>0.5 && _Grain<1.5) {
                    float stripe=sin(p.x*7+sin(p.z*95)*.22+sin(p.z*14)*.7);
                    albedo*=.92+.08*stripe;
                }
                if (_Grain>1.5) albedo*=1+.02*sin(p.x*900)*sin(p.z*900)*saturate(1-length(fwidth(p))*300);
                InputData d=(InputData)0;
                d.positionWS=i.positionWS; d.normalWS=normalize(i.normalWS);
                d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);
                d.shadowCoord=TransformWorldToShadowCoord(i.positionWS);
                d.bakedGI=SampleSH(d.normalWS); d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                d.shadowMask=half4(1,1,1,1);
                SurfaceData s=(SurfaceData)0; s.albedo=albedo; s.alpha=1;
                s.smoothness=_Smoothness; s.occlusion=1; s.normalTS=half3(0,0,1);
                half4 color=UniversalFragmentPBR(d,s); color.rgb=MixFog(color.rgb,i.fog); return color;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
