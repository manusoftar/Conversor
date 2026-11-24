using System;
using System.Runtime.InteropServices;

namespace Conversor
{
    /// <summary>
    /// Mete el proceso actual en un Job Object de Windows con la opción "kill on job close".
    /// Los procesos hijos (ffmpeg/ffprobe) heredan el job, así que cuando Conversor termina
    /// por cualquier motivo (cierre normal, cuelgue, "Finalizar tarea") Windows cierra también
    /// a todos sus hijos y no quedan ffmpeg huérfanos consumiendo CPU/memoria.
    /// Si algo falla (por ejemplo un job incompatible) simplemente no se activa.
    /// </summary>
    internal static class ProcessJob
    {
        private const int JobObjectExtendedLimitInformation = 9;
        private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

        private static IntPtr jobHandle = IntPtr.Zero;

        public static bool KillChildrenOnExit()
        {
            try
            {
                if (jobHandle != IntPtr.Zero)
                    return true;

                IntPtr handle = CreateJobObject(IntPtr.Zero, null);
                if (handle == IntPtr.Zero)
                    return false;

                JOBOBJECT_EXTENDED_LIMIT_INFORMATION info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
                info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

                int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
                IntPtr buffer = Marshal.AllocHGlobal(length);
                try
                {
                    Marshal.StructureToPtr(info, buffer, false);
                    if (!SetInformationJobObject(handle, JobObjectExtendedLimitInformation, buffer, (uint)length))
                    {
                        CloseHandle(handle);
                        return false;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }

                if (!AssignProcessToJobObject(handle, GetCurrentProcess()))
                {
                    CloseHandle(handle);
                    return false;
                }

                jobHandle = handle; // se mantiene abierto hasta que termine el proceso
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Warn("No se pudo activar el cierre automático de procesos hijos: " + ex.Message);
                return false;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(IntPtr hJob, int jobObjectInfoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);
    }
}
