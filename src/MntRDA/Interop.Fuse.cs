using System.Runtime.InteropServices;

namespace MntRDA
{

    [StructLayout(LayoutKind.Sequential)]
    internal struct Fuse3FileInfo
    {
        internal UInt32 flags;
        UInt32 bits1;
        UInt32 bits2;
        UInt32 bits3;
        internal Int64 fh;
        UInt64 lock_owner;
        UInt32 poll_events;
    };
#if _WINDOWS
        

        [StructLayout(LayoutKind.Sequential)]
        internal struct Stat
        {
            UInt64 st_dev;

            UInt64 st_ino;

            internal UInt32 st_mode;

            internal UInt32 st_nlink;


            internal UInt32 st_uid;
            internal UInt32 st_gid;


            UInt32 st_rdev;

            internal Int64 st_size;

            /* Number 512-byte blocks allocated. */

            internal Int64 st_atime;
            UInt64 st_atime_nsec;

            internal Int64 st_mtime;
            UInt32 st_mtime_nsec;

            UInt64 st_ctime;
            UInt64 st_ctime_nsec;

            UInt64 __pad1;
            UInt64 __pad2;
            UInt64 __pad3;
        };
#else

    [StructLayout(LayoutKind.Sequential)]
    internal struct Stat
    {
        UInt64 st_dev;

        UInt64 st_ino;

        internal UInt64 st_nlink;
        internal UInt32 st_mode;

        internal UInt32 st_uid;
        internal UInt32 st_gid;

        UInt32 __pad0;

        UInt64 st_rdev;

        internal Int64 st_size;
        UInt64 st_blksize;

        /* Number 512-byte blocks allocated. */
        UInt64 st_blocks;

        internal Int64 st_atime;
        UInt64 st_atime_nsec;

        internal Int64 st_mtime;
        UInt32 st_mtime_nsec;

        internal Int64 st_ctime;
        UInt64 st_ctime_nsec;

        UInt64 __pad1;
        UInt64 __pad2;
        UInt64 __pad3;
    };
#endif
    internal static class FuseNativeAdapter
    {
        internal const string DLL_PATH = @"FuseNativeAdapter";

        [DllImport(DLL_PATH)]
        internal static extern int pre_main(int argc, string[] argv);

        [DllImport(DLL_PATH)]
        internal static extern unsafe int PrintHelpIfNeeded(delegate* unmanaged[Cdecl]<int> callback);

        [DllImport(DLL_PATH)]
        internal static extern int main();


        [DllImport(DLL_PATH)]
        internal static extern IntPtr GetRdaParameter();

        [DllImport(DLL_PATH)]
        internal static extern unsafe void PatchOpen(delegate* unmanaged[Cdecl]<IntPtr, Fuse3FileInfo*, int> callback);

        [DllImport(DLL_PATH)]
        internal static extern unsafe void PatchRelease(delegate* unmanaged[Cdecl]<IntPtr, Fuse3FileInfo*, int> callback);

        [DllImport(DLL_PATH)]
        internal static extern unsafe void PatchFuseGetattr(delegate* unmanaged[Cdecl]<IntPtr, Stat*, int> callback);


        [DllImport(DLL_PATH)]
        internal static extern unsafe void PatchFuseRead(delegate* unmanaged[Cdecl]<IntPtr, IntPtr, nuint, nint, Fuse3FileInfo*, int> callback);

        [DllImport(DLL_PATH)]
        internal static extern unsafe void PatchFuseReaddir(delegate* unmanaged[Cdecl]<IntPtr, IntPtr, delegate* unmanaged[Cdecl]<IntPtr, byte*, IntPtr, nint, int, int>, nint, IntPtr, int> callback);

    }
}