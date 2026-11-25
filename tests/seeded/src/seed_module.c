/* SherlogSeedModule.dll: a DLL that crashes on purpose, so the crash is recorded "in a named module".
   Built and run only on throwaway CI machines (ADR 0007). */
#include <windows.h>

__declspec(dllexport) void seed_crash(void)
{
    volatile int *nowhere = (volatile int *)0;
    *nowhere = 42; /* access violation, exception code 0xc0000005 */
}

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID reserved)
{
    (void)instance;
    (void)reason;
    (void)reserved;
    return TRUE;
}
