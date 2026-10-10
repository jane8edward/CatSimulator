Shader "CatSimulator/InteractionOutline"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+1" }
        Pass
        {
            Cull Front ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes {float4 positionOS:POSITION; float3 normalOS:NORMAL;};
            float4 Vert(Attributes v):SV_POSITION {return TransformWorldToHClip(TransformObjectToWorld(v.positionOS.xyz)+normalize(TransformObjectToWorldNormal(v.normalOS))*.005);}
            half4 Frag():SV_Target {return half4(1,.72,.04,1);}
            ENDHLSL
        }
    }
}
