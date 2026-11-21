using System.Runtime.InteropServices;

namespace LightsOut;

/// <summary>
/// P/Invoke bridge to the C solver library (lightsout.dll / liblightsout.so /
/// liblightsout.dylib).
/// </summary>
internal static unsafe class NativeSolver
{
#pragma warning disable SYSLIB1054 // DllImport is fine here, the signature is blittable
    [DllImport("lightsout", EntryPoint = "los_solve", CallingConvention = CallingConvention.Cdecl)]
    private static extern int los_solve(int rows, int cols, byte* cells, int wantMin,
                                        byte* clicks, out int count);

    [DllImport("lightsout", EntryPoint = "los_version", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr los_version();
#pragma warning restore SYSLIB1054

    /// <summary>
    /// Solves the board.  Returns 1 = solved, 0 = no solution, -1 = too
    /// large, -2 = invalid arguments / out of memory.
    /// </summary>
    public static int Solve(int rows, int cols, byte[] cells, bool wantMin,
                            byte[] clicks, out int count)
    {
        fixed (byte* pCells = cells)
        fixed (byte* pClicks = clicks)
        {
            return los_solve(rows, cols, pCells, wantMin ? 1 : 0, pClicks, out count);
        }
    }

    public static string Version()
    {
        var ptr = los_version();
        return ptr == IntPtr.Zero ? "unknown" : (Marshal.PtrToStringUTF8(ptr) ?? "unknown");
    }
}
