Shader "CatSimulator/OriginalGroom"
{
    Properties
    {
        _BaseMap("Original Coat Atlas",2D)="white"{}
        _AlphaMap("Fine Strand Alpha",2D)="white"{}
        _BaseColor("Tint",Color)=(1,1,1,1)
        _Cutoff("Strand Cutoff",Range(0,1))=.28
        _Smoothness("Smoothness",Range(0,1))=.16
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest"}
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_AlphaMap); SAMPLER(sampler_AlphaMap);
        CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor;
        half _Cutoff, _Smoothness;
        CBUFFER_END
        struct Attributes
        {
            float4 positionOS:POSITION; float3 normalOS:NORMAL;
            float2 coatUV:TEXCOORD0; float2 strandUV:TEXCOORD1; half4 color:COLOR;
        };
        struct Varyings
        {
            float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0;
            half3 normalWS:TEXCOORD1; float2 coatUV:TEXCOORD2;
            float2 strandUV:TEXCOORD3; half4 color:COLOR; half fog:TEXCOORD4;
        };
        Varyings GroomVert(Attributes v)
        {
            Varyings o;
            VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
            o.positionCS=p.positionCS; o.positionWS=p.positionWS;
            o.normalWS=TransformObjectToWorldNormal(v.normalOS);
            o.coatUV=v.coatUV; o.strandUV=v.strandUV; o.color=v.color;
            o.fog=ComputeFogFactor(p.positionCS.z);return o;
        }
        half StrandAlpha(Varyings i)
        {
            half alpha=SAMPLE_TEXTURE2D(_AlphaMap,sampler_AlphaMap,i.strandUV).a;
            clip(alpha-_Cutoff);return alpha;
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags {"LightMode"="UniversalForward"}
            AlphaToMask On
            HLSLPROGRAM
            #pragma vertex GroomVert
            #pragma fragment GroomFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            half4 GroomFrag(Varyings i):SV_Target
            {
                half alpha=StrandAlpha(i);
                half3 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.coatUV).rgb*_BaseColor.rgb;
                half gray=dot(albedo,half3(.2126,.7152,.0722));
                albedo=lerp(albedo,gray.xxx,i.strandUV.y*.16)*i.color.rgb;
                InputData d=(InputData)0;
                d.positionWS=i.positionWS;d.normalWS=normalize(i.normalWS);
                d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);
                d.shadowCoord=TransformWorldToShadowCoord(i.positionWS);
                d.bakedGI=SampleSH(d.normalWS);d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                d.shadowMask=half4(1,1,1,1);
                SurfaceData s=(SurfaceData)0;
                s.albedo=albedo;s.alpha=1;s.smoothness=_Smoothness;s.occlusion=1;s.normalTS=half3(0,0,1);
                half4 result=UniversalFragmentPBR(d,s);
                half rim=pow(saturate(1-dot(d.normalWS,d.viewDirectionWS)),4);
                result.rgb+=albedo*rim*.07;
                result.rgb=MixFog(result.rgb,i.fog);result.a=alpha;return result;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags {"LightMode"="DepthOnly"}
            ColorMask R
            HLSLPROGRAM
            #pragma vertex GroomVert
            #pragma fragment DepthFrag
            half4 DepthFrag(Varyings i):SV_Target {StrandAlpha(i);return i.positionCS.z;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags {"LightMode"="DepthNormalsOnly"}
            HLSLPROGRAM
            #pragma vertex GroomVert
            #pragma fragment NormalsFrag
            half4 NormalsFrag(Varyings i):SV_Target {StrandAlpha(i);return half4(normalize(i.normalWS),0);}
            ENDHLSL
        }
    }
}
