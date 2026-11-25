/* SherlogSeedFastFail.exe: ends itself with __fastfail, as security checks in real apps do.
   Expected record: Application Error 1000, app and module SherlogSeedFastFail.exe, code 0xc0000409. */
#include <intrin.h>

int main(void)
{
    __fastfail(7); /* FAST_FAIL_FATAL_APP_EXIT */
    return 0;
}
