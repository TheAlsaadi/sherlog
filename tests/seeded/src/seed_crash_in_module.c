/* SherlogSeedCrash.exe: loads SherlogSeedModule.dll and calls the function that crashes in it.
   Expected record: Application Error 1000, app SherlogSeedCrash.exe, module SherlogSeedModule.dll, code 0xc0000005. */
#include <windows.h>
#include <stdio.h>

typedef void (*seed_fn)(void);

int main(void)
{
    HMODULE module = LoadLibraryW(L"SherlogSeedModule.dll");
    if (module == NULL)
    {
        fprintf(stderr, "Could not load SherlogSeedModule.dll (error %lu)\n", GetLastError());
        return 2;
    }

    seed_fn crash = (seed_fn)GetProcAddress(module, "seed_crash");
    if (crash == NULL)
    {
        fprintf(stderr, "seed_crash not found\n");
        return 3;
    }

    crash();
    return 0;
}
