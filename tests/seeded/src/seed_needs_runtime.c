/* SherlogSeedNeedsRuntime.exe: linked against the shared Visual C++ runtime (/MD), so it can't start on a PC
   without the Visual C++ Redistributable. Used only in Windows Sandbox (sandbox/README.md), which has no runtime. */
#include <stdio.h>

int main(void)
{
    puts("The Visual C++ runtime is installed.");
    return 0;
}
