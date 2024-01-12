#include "UnityShaderVariables.cginc"

struct Attributes
{
    float4 positionOS : POSITION;
};

struct Varyings
{
    float4 vertex : SV_POSITION;
};

float4 GetDefaultColor()
{
    return float4(0.37f, 0.08f, 0.5f, 1.0f);
}

float4 GetVertexPosition(float3 positionOS)
{
    return mul(UNITY_MATRIX_VP, float4(mul(UNITY_MATRIX_M, float4(positionOS, 1.0f)).xyz, 1.0f));
}

Varyings GetVaryings(Attributes inData)
{
    Varyings outData;
    outData.vertex = GetVertexPosition(inData.positionOS);
    return outData;
}

Varyings LitPassVertex(Attributes inData)
{
    return GetVaryings(inData);
}

half4 LitPassFragment(Varyings input) : SV_TARGET
{
    return GetDefaultColor();
}

Varyings ShadowPassVertex(Attributes inData)
{
    return GetVaryings(inData);
}

half4 ShadowPassFragment(Varyings input) : SV_TARGET
{
    return GetDefaultColor();
}

Varyings DepthOnlyVertex(Attributes inData)
{
    return GetVaryings(inData);
}

half4 DepthOnlyFragment(Varyings input) : SV_TARGET
{
    return GetDefaultColor();
}

Varyings UniversalVertexMeta(Attributes inData)
{
    return GetVaryings(inData);
}

half4 UniversalFragmentMeta(Varyings input) : SV_TARGET
{
    return GetDefaultColor();
}

Varyings vert(Attributes inData)
{
    return GetVaryings(inData);
}

half4 frag(Varyings input) : SV_TARGET
{
    return GetDefaultColor();
}