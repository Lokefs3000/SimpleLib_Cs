//workaround to editor shader package bug
#define VARIANT_NULL

#pragma BlendRT0 StraightAlpha
#pragma ZCull Off

struct VsInput
{
    float2 Position : POSITION0;
    float2 UV       : TEXCOORD0;
    float4 Color    : COLOR0;
};

struct PsInput
{
    float4 Position : SV_Position;
    float2 UV       : TEXCOORD0;
    float4 Color    : COLOR0;
};

[ConstantBufferBehavior(Constants)]
cbuffer CB_FrameData : register(b0)
{
    float4x4 WorldProjection;
}

#if __SHADER_TARGET_STAGE == __SHADER_STAGE_VERTEX
PsInput VertexMain(VsInput input)
{
    PsInput output;
    output.Position = mul(float4(input.Position, 0.0, 1.0), WorldProjection);
    output.UV = input.UV;
    output.Color = input.Color;

    return output;
}
#endif

Texture2D<float4> TX_PrimaryTexture : register(t0);
SamplerState SS_Sampler : register(s0)
{
    Filter = MinMagMipLinear;
    AddressU = Clamp;
    AddressV = Clamp;
};

#if __SHADER_TARGET_STAGE == __SHADER_STAGE_PIXEL
float4 PixelMain(PsInput input) : SV_Target
{
    Texture2D<float4> t2d = TX_PrimaryTexture;
    return t2d.Sample(SS_Sampler, input.UV) * input.Color;
}
#endif