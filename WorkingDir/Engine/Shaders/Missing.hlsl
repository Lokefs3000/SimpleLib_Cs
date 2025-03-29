#define VARIANT_INSTANCING

#pragma ZCull LessEqual

#include "Include/Buffers.hlsl"

struct VsInput
{
    float3 Position : POSITION0;
    float2 UV       : TEXCOORD0;
    float3 Normal   : NORMAL0;

    float3 Tangent  : TANGENT0;
};

struct PsInput
{
    float4 Position : SV_Position;

    float3 Vertex : POSITION0;
    float3 Normal : NORMAL0;

    uint C : COLOR;
};

#if __SHADER_TARGET_STAGE == __SHADER_STAGE_VERTEX
PsInput VertexMain(VsInput input, uint instance : SV_InstanceID)
{
    PerModelData perModel = PerModelBuffer;
    CameraBufferData camera = CameraBuffer;
    StructuredBuffer<float4x4> transforms = SB_Transforms;

    PsInput output;
    output.Vertex   = mul(float4(input.Position, 1.0), transforms[perModel.Model]).xyz;  //M
    output.Position = mul(float4(output.Vertex, 1.0), camera.ViewProjection);            //VP
    output.Normal   = input.Normal;
    output.C = perModel.Model;

    return output;
}
#endif

#if __SHADER_TARGET_STAGE == __SHADER_STAGE_PIXEL
float3 HSV2RGB(float3 input)
{
    float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    float3 p = abs(frac(input.xxx + K.xyz) * 6.0 - K.www);

    return input.z * lerp(K.xxx, clamp(p - K.xxx, 0.0, 1.0), input.y);
}

float4 PixelMain(PsInput input) : SV_Target0
{
    CameraBufferData camera = CameraBuffer;
    float dir = dot(input.Vertex, camera.ViewPosition);

    float checker = floor(input.Position.x * 0.02) +
                    floor(input.Position.y * 0.02);
    float isEven  = fmod(checker, 2.0) * 0.2 + 0.9;

    //return float4(float3(0.0, 0.7, 0.7) * isEven, 1.0);
    return float4(HSV2RGB(float3(input.C * 0.1, 1.0, 1.0)), 1.0);
}
#endif