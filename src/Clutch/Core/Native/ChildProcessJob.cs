using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Clutch.Core.Native;

/// <summary>Bağlanan süreçler, Clutch kapandığında (çökse bile) Windows tarafından sonlandırılır.</summary>
internal static class ChildProcessJob
{
    private static readonly IntPtr Handle = Create();

    public static void Attach(Process process)
    {
        if (Handle != IntPtr.Zero)
        {
            NativeMethods.AssignProcessToJobObject(Handle, process.Handle);
        }
    }

    private static IntPtr Create()
    {
        var job = NativeMethods.CreateJobObject(IntPtr.Zero, null);
        if (job == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var info = new NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = { LimitFlags = NativeMethods.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE }
        };

        NativeMethods.SetInformationJobObject(
            job,
            NativeMethods.JobObjectExtendedLimitInformation,
            ref info,
            Marshal.SizeOf<NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION>());

        return job;
    }
}
