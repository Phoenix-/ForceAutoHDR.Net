using System.Runtime.InteropServices;

namespace ForceAutoHDR.Core.Discovery.Interop;

/// <summary>
/// Resolves a process id to the full path of its executable.
/// </summary>
/// <remarks>
/// <c>QueryFullProcessImageName</c> with <c>PROCESS_QUERY_LIMITED_INFORMATION</c> rather than
/// <see cref="System.Diagnostics.Process"/>'s <c>MainModule</c>: the latter reads the target's
/// module list, which needs <c>PROCESS_VM_READ</c> and is exactly what an anti-cheat blocks. The
/// limited right was designed to survive that, and games are the processes this has to work on.
/// </remarks>
internal static partial class ProcessImage
{
    private const uint QueryLimitedInformation = 0x1000;

    /// <summary>
    /// The executable path, or <see langword="null"/> when the process is gone or refuses the
    /// handle. A miss is normal -- elevated and protected processes are simply not visible.
    /// </summary>
    public static string? GetPath(uint processId)
    {
        var process = OpenProcess(QueryLimitedInformation, bInheritHandle: false, processId);
        if (process == nint.Zero)
        {
            return null;
        }

        try
        {
            // Long paths are opt-in per process, but the API caps at 32767 either way.
            var buffer = new char[32768];
            var size = (uint)buffer.Length;
            // Passed by reference rather than as an array: the marshalling generator will not
            // touch a char[] unless the whole assembly opts out of runtime marshalling, and a
            // buffer of UTF-16 units needs no conversion in the first place.
            return QueryFullProcessImageName(process, dwFlags: 0, ref buffer[0], ref size) && size > 0
                ? new string(buffer, 0, (int)size)
                : null;
        }
        finally
        {
            CloseHandle(process);
        }
    }

    [LibraryImport("kernel32.dll", EntryPoint = "OpenProcess")]
    private static partial nint OpenProcess(
        uint dwDesiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
        uint dwProcessId);

    // StringMarshalling settles what a bare `char` means to the generator; without it the
    // parameter is ambiguous between ANSI and UTF-16 and the stub refuses to marshal it.
    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool QueryFullProcessImageName(
        nint hProcess,
        uint dwFlags,
        ref char lpExeName,
        ref uint lpdwSize);

    [LibraryImport("kernel32.dll", EntryPoint = "CloseHandle")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint hObject);
}
