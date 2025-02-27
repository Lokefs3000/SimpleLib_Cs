#include "D3D12.hpp"

#include <d3d12.h>

int D3D12_Impl_CreateDeviceFactory(void* SDKConfiguration1, UINT SDKVersion, LPCSTR SDKPath, REFIID riid, void** ppvFactory)
{
	ID3D12SDKConfiguration1* sdk = (ID3D12SDKConfiguration1*)SDKConfiguration1;
	return sdk->CreateDeviceFactory(SDKVersion, SDKPath, riid, ppvFactory);
}
