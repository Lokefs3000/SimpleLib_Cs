#pragma once

#include <Windows.h>

extern "C"
{
	__declspec(dllexport) extern const UINT D3D12SDKVersion = 615;
	__declspec(dllexport) extern const char* D3D12SDKPath = u8".\\D3D12\\";

	__declspec(dllexport) int D3D12_Impl_CreateDeviceFactory(void* SDKConfiguration1, UINT SDKVersion, LPCSTR SDKPath, REFIID riid, void** ppvFactory);
}