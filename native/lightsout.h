/*
 * lightsout.h -- public interface of the Lights Out solver library.
 *
 * The library is built as lightsout.dll on Windows, liblightsout.so on
 * Linux and liblightsout.dylib on macOS, and is consumed by the Avalonia
 * front end through P/Invoke.
 */

#ifndef LIGHTSOUT_H
#define LIGHTSOUT_H

#if defined(_WIN32)
#  if defined(LIGHTSOUT_BUILD)
#    define LOS_API __declspec(dllexport)
#  else
#    define LOS_API __declspec(dllimport)
#  endif
#else
#  define LOS_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

/*
 * Solves a Lights Out board.
 *
 *   rows, cols  board size, 1..4096 cells, each side <= 64
 *   cells       rows*cols bytes, row major, 1 = lamp lit, 0 = lamp off
 *   want_min    non-zero: return a solution with the fewest presses
 *               zero:     return the first solution found
 *   clicks      rows*cols output bytes, 1 = press that cell
 *   count       receives the number of presses (1 bits in clicks)
 *
 * Returns
 *    1  solved
 *    0  the position cannot be turned off
 *   -1  board too large
 *   -2  bad arguments or out of memory
 *
 * The solver keeps reusable buffers, so it is not thread safe; call it
 * from one thread at a time.  Every call is independent otherwise.
 */
LOS_API int los_solve(int rows, int cols, const unsigned char *cells,
                      int want_min, unsigned char *clicks, int *count);

/* Build metadata, handy for diagnostics. */
LOS_API const char *los_version(void);

#ifdef __cplusplus
}
#endif

#endif /* LIGHTSOUT_H */
