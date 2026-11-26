/* SherlogSeedOverflow.exe: recurses until the stack runs out.
   Expected record: Application Error 1000, app and module SherlogSeedOverflow.exe, code 0xc00000fd. */
#include <stdio.h>

static int depth(volatile char *previous)
{
    volatile char frame[4096];
    frame[0] = previous ? previous[0] : 1;
    return depth(frame) + frame[0]; /* not a tail call, so the compiler can't turn it into a loop */
}

int main(void)
{
    printf("%d\n", depth(NULL));
    return 0;
}
