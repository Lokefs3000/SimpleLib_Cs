#include <windows.h>

extern "C" BOOL WINAPI DllMain(
    HINSTANCE const instance,  // handle to DLL module
    DWORD     const reason,    // reason for calling function
    LPVOID    const reserved)  // reserved
{
    return TRUE;
}