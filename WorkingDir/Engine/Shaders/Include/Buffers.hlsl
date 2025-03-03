#include "Common.hlsl"

struct PerModelData
{
    uint Model;
};

struct CameraBufferData
{
    float3 ViewPosition;
    float ___padding0;
    float4x4 ViewProjection;
};

StructuredBuffer<float4x4> SB_Transforms : register(t7);

#ifndef INSTANCING_ENABLED
cbuffer CB_PerModel : register(b5)
{
    PerModelData PerModelBuffer;
};
#else
StructuredBuffer<PerModelData> SB_PerModel_Data : register(t8);
#define PerModelBuffer SB_PerModel_Data[instance]
#endif

cbuffer CB_CameraBuffer : register(b6)
{
    CameraBufferData CameraBuffer;
};