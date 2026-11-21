/*
 * lightsout.c -- Lights Out solver library (C99, no dependencies).
 *
 * Two engines:
 *
 * 1) First-row enumeration, used when min(rows, cols) <= 26.
 *    Every solution is produced by exactly one first-row pattern: to clear
 *    row r the presses on row r+1 must be the lamps still lit on row r after
 *    the presses on rows r-1 and r, so the rest of the board is forced.
 *    Enumerating all 2^W patterns therefore covers the whole affine solution
 *    space and "fewest presses" is exact, not a heuristic.
 *
 * 2) Bit-packed GF(2) Gauss-Jordan, used for larger boards.  The nullspace
 *    is swept when it is small enough, otherwise coordinate descent improves
 *    the particular solution (and the caller is told it is not minimal).
 */

#define LIGHTSOUT_BUILD

#include "lightsout.h"

#if defined(_MSC_VER)
#  include <intrin.h>
#  define POPCNT64(x) ((int)__popcnt64((unsigned long long)(x)))
#  define ALWAYS_INLINE static __forceinline
#else
#  define POPCNT64(x) (__builtin_popcountll((unsigned long long)(x)))
#  define ALWAYS_INLINE static __inline__ __attribute__((always_inline))
#endif

#include <stdlib.h>
#include <string.h>
#include <limits.h>

typedef unsigned long long u64;

#define MAXW            26      /* largest min(rows, cols) we enumerate   */
#define GAUSS_MAX_VARS  4096    /* largest rows*cols we eliminate          */
#define GAUSS_MAX_NULL  22      /* largest nullspace we sweep for exact    */

/* working board after the optional transpose: H rows of W bits each */
static int H, W;
static u64 *light;
static int light_cap;

/* reusable buffers so a solve does not hit the allocator */
static u64 *buf_a, *buf_b, *buf_c;
static int buf_cap;

static int ensure_light(int n)
{
    u64 *p;

    if (n <= light_cap)
        return 1;
    p = (u64 *)malloc(sizeof(u64) * (size_t)n);
    if (!p)
        return 0;
    free(light);
    light = p;
    light_cap = n;
    return 1;
}

static int ensure_bufs(int n)
{
    u64 *a, *b, *c;

    if (n <= buf_cap)
        return 1;
    a = (u64 *)malloc(sizeof(u64) * (size_t)n);
    b = (u64 *)malloc(sizeof(u64) * (size_t)n);
    c = (u64 *)malloc(sizeof(u64) * (size_t)n);
    if (!a || !b || !c) {
        free(a);
        free(b);
        free(c);
        return 0;
    }
    free(buf_a);
    free(buf_b);
    free(buf_c);
    buf_a = a;
    buf_b = b;
    buf_c = c;
    buf_cap = n;
    return 1;
}

/* ------------------------------------------------------ first-row recursion */

/*
 * Accepts() evaluates a whole first-row pattern with nothing but the
 * recurrence: four word operations per row, no stores, no popcounts.
 */
ALWAYS_INLINE int accepts(u64 m, u64 full)
{
    u64 prev = 0, p = m;
    int r;

    for (r = 0; r < H; r++) {
        u64 hz = p ^ ((p << 1) & full) ^ (p >> 1);
        u64 need = light[r] ^ hz ^ prev;

        if (r + 1 == H)
            return need == 0;
        prev = p;
        p = need;
    }
    return 0;
}

/* Materialises the click map for a pattern that accepts() already passed. */
static int rebuild(u64 m, u64 full, u64 *cur)
{
    u64 prev = 0, p = m;
    int r, cnt = 0;

    for (r = 0; r < H; r++) {
        cur[r] = p;
        cnt += POPCNT64(p);
        p = light[r] ^ p ^ ((p << 1) & full) ^ (p >> 1) ^ prev;
        prev = cur[r];
    }
    return cnt;
}

static int solve_enum(int want_min, u64 *press, int *pcnt)
{
    u64 full, m, limit, *cur, *best;
    int cnt, bestcnt = INT_MAX, found = 0;

    if (W > MAXW)
        return -1;
    if (!ensure_bufs(H))
        return -2;

    cur = buf_a;
    best = buf_b;
    full = ((u64)1 << W) - 1ULL;
    limit = (u64)1 << W;

    for (m = 0; m < limit; m++) {
        if (!accepts(m, full))
            continue;
        cnt = rebuild(m, full, cur);
        if (!found || cnt < bestcnt) {
            memcpy(best, cur, sizeof(u64) * (size_t)H);
            bestcnt = cnt;
            found = 1;
        }
        if (!want_min)
            break;
    }
    if (!found)
        return 0;
    memcpy(press, best, sizeof(u64) * (size_t)H);
    *pcnt = bestcnt;
    return 1;
}

/* -------------------------------------------------- Gaussian elimination */

static int solve_gauss(int want_min, u64 *press, int *pcnt)
{
    int K, words, rank = 0;
    int r, c, i, w, ok = 1, nfree, fi;
    int *pcol = NULL, *x = NULL, *ispiv = NULL;
    u64 *mat = NULL, *basis = NULL, *cur = NULL, *bestv = NULL;
    int bestcnt;

    K = H * W;
    if (K > GAUSS_MAX_VARS)
        return -1;
    words = (K + 64) / 64;

    memset(press, 0, sizeof(u64) * (size_t)H);
    mat = (u64 *)calloc((size_t)K * (size_t)words, sizeof(u64));
    pcol = (int *)malloc(sizeof(int) * (size_t)K);
    ispiv = (int *)calloc((size_t)K, sizeof(int));
    if (!mat || !pcol || !ispiv)
        goto oom;

    /* row i describes the lamps affected by pressing cell i */
    for (r = 0; r < H; r++) {
        for (c = 0; c < W; c++) {
            u64 *row;

            i = r * W + c;
            row = mat + (size_t)i * (size_t)words;
            row[i >> 6] |= 1ULL << (i & 63);
            if (c > 0)     row[(i - 1) >> 6] |= 1ULL << ((i - 1) & 63);
            if (c + 1 < W) row[(i + 1) >> 6] |= 1ULL << ((i + 1) & 63);
            if (r > 0)     row[(i - W) >> 6] |= 1ULL << ((i - W) & 63);
            if (r + 1 < H) row[(i + W) >> 6] |= 1ULL << ((i + W) & 63);
            if ((light[r] >> c) & 1)
                row[K >> 6] |= 1ULL << (K & 63);
        }
    }

    for (c = 0; c < K; c++) {
        u64 bit = 1ULL << (c & 63);
        int colw = c >> 6, p = -1;

        for (r = rank; r < K; r++)
            if (mat[(size_t)r * (size_t)words + colw] & bit) {
                p = r;
                break;
            }
        if (p < 0)
            continue;
        if (p != rank) {
            for (w = 0; w < words; w++) {
                u64 t = mat[(size_t)p * (size_t)words + w];

                mat[(size_t)p * (size_t)words + w] =
                    mat[(size_t)rank * (size_t)words + w];
                mat[(size_t)rank * (size_t)words + w] = t;
            }
        }
        {
            u64 *prow = mat + (size_t)rank * (size_t)words;

            for (r = 0; r < K; r++) {
                u64 *rrow;

                if (r == rank)
                    continue;
                rrow = mat + (size_t)r * (size_t)words;
                if (rrow[colw] & bit)
                    for (w = 0; w < words; w++)
                        rrow[w] ^= prow[w];
            }
        }
        pcol[rank] = c;
        ispiv[c] = 1;
        rank++;
    }

    for (r = rank; r < K && ok; r++) {
        u64 *row = mat + (size_t)r * (size_t)words;
        int zero = 1;

        for (w = 0; w < words; w++) {
            u64 v = row[w];

            if (w == (K >> 6))
                v &= ~(1ULL << (K & 63));
            if (v) {
                zero = 0;
                break;
            }
        }
        if (zero && ((row[K >> 6] >> (K & 63)) & 1))
            ok = 0;
    }
    if (!ok) {
        free(mat);
        free(pcol);
        free(ispiv);
        return 0;
    }

    x = (int *)calloc((size_t)K, sizeof(int));
    if (!x)
        goto oom;
    for (r = 0; r < rank; r++)
        x[pcol[r]] = (int)((mat[(size_t)r * (size_t)words + (K >> 6)] >>
                            (K & 63)) & 1);

    nfree = K - rank;
    bestv = (u64 *)calloc((size_t)words, sizeof(u64));
    if (!bestv)
        goto oom;
    for (i = 0; i < K; i++)
        if (x[i])
            bestv[i >> 6] |= 1ULL << (i & 63);
    bestcnt = 0;
    for (w = 0; w < words; w++)
        bestcnt += POPCNT64(bestv[w]);

    if (want_min && nfree > 0) {
        basis = (u64 *)calloc((size_t)nfree * (size_t)words, sizeof(u64));
        if (!basis)
            goto oom;
        fi = 0;
        for (i = 0; i < K; i++) {
            u64 *bv;

            if (ispiv[i])
                continue;
            bv = basis + (size_t)fi * (size_t)words;
            bv[i >> 6] |= 1ULL << (i & 63);
            for (r = 0; r < rank; r++)
                if (mat[(size_t)r * (size_t)words + (i >> 6)] &
                    (1ULL << (i & 63)))
                    bv[pcol[r] >> 6] |= 1ULL << (pcol[r] & 63);
            fi++;
        }

        if (nfree <= GAUSS_MAX_NULL) {
            u64 step, nstep = 1ULL << nfree;

            cur = (u64 *)calloc((size_t)words, sizeof(u64));
            if (!cur)
                goto oom;
            memcpy(cur, bestv, sizeof(u64) * (size_t)words);
            for (step = 1; step < nstep; step++) {
                int b = 0, c2 = 0;

                while (!((step >> b) & 1))
                    b++;
                for (w = 0; w < words; w++)
                    cur[w] ^= basis[(size_t)b * (size_t)words + w];
                for (w = 0; w < words; w++)
                    c2 += POPCNT64(cur[w]);
                if (c2 < bestcnt) {
                    bestcnt = c2;
                    memcpy(bestv, cur, sizeof(u64) * (size_t)words);
                }
            }
        } else {
            int iter;

            for (iter = 0; iter < 2000; iter++) {
                int improved = 0;

                for (fi = 0; fi < nfree; fi++) {
                    u64 *bv = basis + (size_t)fi * (size_t)words;
                    int delta = 0;

                    for (w = 0; w < words; w++)
                        delta += POPCNT64(bestv[w] ^ bv[w]) -
                                 POPCNT64(bestv[w]);
                    if (delta < 0) {
                        for (w = 0; w < words; w++)
                            bestv[w] ^= bv[w];
                        bestcnt += delta;
                        improved = 1;
                    }
                }
                if (!improved)
                    break;
            }
        }
    }

    *pcnt = 0;
    for (i = 0; i < K; i++)
        if ((bestv[i >> 6] >> (i & 63)) & 1) {
            press[i / W] |= 1ULL << (i % W);
            (*pcnt)++;
        }

    free(mat);
    free(pcol);
    free(ispiv);
    free(x);
    free(basis);
    free(cur);
    free(bestv);
    return 1;

oom:
    free(mat);
    free(pcol);
    free(ispiv);
    free(x);
    free(basis);
    free(cur);
    free(bestv);
    return -2;
}

/* --------------------------------------------------------------- entry */

LOS_API int los_solve(int rows, int cols, const unsigned char *cells,
                      int want_min, unsigned char *clicks, int *count)
{
    int r, c, transposed, rc, cnt = 0;

    if (!cells || !clicks || !count)
        return -2;
    if (rows < 1 || cols < 1)
        return -1;
    if ((long long)rows * (long long)cols > 10000000LL)
        return -1;

    transposed = (cols > rows);
    H = transposed ? cols : rows;
    W = transposed ? rows : cols;

    /* every working row is packed into one 64-bit word */
    if (W > 64)
        return -1;

    if (!ensure_light(H))
        return -2;
    memset(light, 0, sizeof(u64) * (size_t)H);
    for (r = 0; r < rows; r++) {
        for (c = 0; c < cols; c++) {
            if (!cells[(size_t)r * (size_t)cols + (size_t)c])
                continue;
            if (transposed)
                light[c] |= 1ULL << r;
            else
                light[r] |= 1ULL << c;
        }
    }

    if (!ensure_bufs(H))
        return -2;

    if (W > MAXW)
        rc = solve_gauss(want_min, buf_c, &cnt);
    else
        rc = solve_enum(want_min, buf_c, &cnt);
    if (rc != 1)
        return rc;

    for (r = 0; r < rows; r++)
        for (c = 0; c < cols; c++)
            clicks[(size_t)r * (size_t)cols + (size_t)c] = (unsigned char)
                (transposed ? ((buf_c[c] >> r) & 1)
                            : ((buf_c[r] >> c) & 1));
    *count = cnt;
    return 1;
}

LOS_API const char *los_version(void)
{
    return "1.0.0";
}
