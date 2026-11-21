/*
 * selftest.c -- sanity checks for the lightsout solver library.
 *
 * Built and run by CI (and by anyone who wants a quick verification):
 *     cmake -S native -B build && cmake --build build && ./build/selftest
 *
 * Every case is verified by actually applying the returned clicks to the
 * board: a solution counts only when the board really ends up dark.
 */

#include "lightsout.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

static unsigned long long rng = 0x243f6a8885a308d3ULL;

static unsigned long long next_rand(void)
{
    rng ^= rng << 13;
    rng ^= rng >> 7;
    rng ^= rng << 17;
    return rng;
}

static void apply(int rows, int cols, unsigned char *cells,
                  const unsigned char *clicks)
{
    int r, c;

    for (r = 0; r < rows; r++) {
        for (c = 0; c < cols; c++) {
            if (!clicks[(size_t)r * (size_t)cols + (size_t)c])
                continue;
            cells[(size_t)r * (size_t)cols + (size_t)c] ^= 1;
            if (r > 0)          cells[(size_t)(r - 1) * (size_t)cols + (size_t)c] ^= 1;
            if (r + 1 < rows)   cells[(size_t)(r + 1) * (size_t)cols + (size_t)c] ^= 1;
            if (c > 0)          cells[(size_t)r * (size_t)cols + (size_t)(c - 1)] ^= 1;
            if (c + 1 < cols)   cells[(size_t)r * (size_t)cols + (size_t)(c + 1)] ^= 1;
        }
    }
}

static int is_dark(const unsigned char *v, int n)
{
    int i;

    for (i = 0; i < n; i++)
        if (v[i])
            return 0;
    return 1;
}

static int failures;

static void check(int cond, const char *what)
{
    if (!cond) {
        printf("FAIL: %s\n", what);
        failures++;
    }
}

/* Solves, applies the answer and verifies the board goes dark. */
static int solve_and_verify(int rows, int cols, const unsigned char *board,
                            int want_min, int *out_cnt)
{
    size_t n = (size_t)rows * (size_t)cols;
    unsigned char *cells = (unsigned char *)malloc(n);
    unsigned char *clicks = (unsigned char *)malloc(n);
    int rc, cnt = 0;

    if (!cells || !clicks) {
        free(cells);
        free(clicks);
        return -2;
    }
    memcpy(cells, board, n);
    rc = los_solve(rows, cols, cells, want_min, clicks, &cnt);
    if (rc == 1) {
        int k = 0, i;

        for (i = 0; i < (int)n; i++)
            k += clicks[i] ? 1 : 0;
        if (k != cnt)
            printf("count mismatch: %d vs %d\n", cnt, k);
        apply(rows, cols, cells, clicks);
        if (!is_dark(cells, (int)n)) {
            printf("FAIL: %dx%d solution does not clear the board\n", rows, cols);
            failures++;
        }
        if (out_cnt)
            *out_cnt = cnt;
    } else if (out_cnt) {
        *out_cnt = -1;
    }
    free(cells);
    free(clicks);
    return rc;
}

int main(void)
{
    int rows, cols, i, cnt, cases = 0;
    unsigned char board[64 * 64];

    printf("lightsout %s self test\n", los_version());

    /* 1. the classic 5x5 single centre lamp: solvable in 11 presses */
    memset(board, 0, 25);
    board[2 * 5 + 2] = 1;
    check(solve_and_verify(5, 5, board, 1, &cnt) == 1, "5x5 centre solves");
    check(cnt == 11, "5x5 centre needs 11 presses");

    /* 2. a corner lamp on 5x5 is not solvable */
    memset(board, 0, 25);
    board[0] = 1;
    check(solve_and_verify(5, 5, board, 1, &cnt) == 0, "5x5 corner is unsolvable");

    /* 3. already dark board: empty solution */
    memset(board, 0, 25);
    check(solve_and_verify(5, 5, board, 1, &cnt) == 1 && cnt == 0,
          "dark board needs no press");

    /* 4. degenerate sizes */
    board[0] = 1;
    check(solve_and_verify(1, 1, board, 1, &cnt) == 1 && cnt == 1, "1x1");
    memset(board, 0, 5);
    board[2] = 1;
    check(solve_and_verify(1, 5, board, 1, &cnt) >= 0, "1x5");
    check(solve_and_verify(5, 1, board, 1, &cnt) >= 0, "5x1");

    /* 5. random boards, both engines, several shapes */
    {
        static const int shapes[][2] = {
            {3, 3}, {4, 4}, {5, 6}, {7, 7}, {8, 9}, {10, 10},
            {12, 7}, {15, 15}, {20, 20}, {26, 26}, {27, 27}, {30, 30}
        };

        for (i = 0; i < (int)(sizeof shapes / sizeof shapes[0]); i++) {
            int t;

            rows = shapes[i][0];
            cols = shapes[i][1];
            for (t = 0; t < 12; t++) {
                int r, c, one, many;

                for (r = 0; r < rows; r++)
                    for (c = 0; c < cols; c++)
                        board[r * cols + c] = (unsigned char)(next_rand() & 1ULL);

                one = solve_and_verify(rows, cols, board, 0, &cnt);
                many = solve_and_verify(rows, cols, board, 1, &cnt);
                check(one == many, "min/first agree on solvability");
                cases++;
            }
        }
    }

    /* 6. argument validation */
    memset(board, 0, 25);
    check(los_solve(0, 5, board, 0, board, &cnt) == -1, "rows=0 rejected");
    check(los_solve(65, 65, board, 0, board, &cnt) == -1,
          "boards wider than 64 rejected");
    check(los_solve(2, 2, NULL, 0, board, &cnt) == -2, "null cells rejected");

    printf("%d random boards verified, %d failure(s)\n", cases, failures);
    return failures ? 1 : 0;
}
