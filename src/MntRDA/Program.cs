using System;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;
using RDAExplorer;


namespace Demo
{
    public class Program
    {


        [StructLayout(LayoutKind.Sequential)]
        public struct Fuse3FileInfo
        {
            public UInt32 flags;
            UInt32 bits1;
            UInt32 bits2;
            UInt32 bits3;
            UInt64 fh;
            UInt64 lock_owner;
            UInt32 poll_events;
        };
#if _WINDOWS
        

        [StructLayout(LayoutKind.Sequential)]
        public struct Stat
        {
            UInt64 st_dev;

            UInt64 st_ino;

            public UInt32 st_mode;

            public UInt32 st_nlink;


            public UInt32 st_uid;
            public UInt32 st_gid;


            UInt32 st_rdev;

            public Int64 st_size;

            /* Number 512-byte blocks allocated. */

            public Int64 st_atime;
            UInt64 st_atime_nsec;

            public Int64 st_mtime;
            UInt32 st_mtime_nsec;

            UInt64 st_ctime;
            UInt64 st_ctime_nsec;

            UInt64 __pad1;
            UInt64 __pad2;
            UInt64 __pad3;
        };
#else

        [StructLayout(LayoutKind.Sequential)]
        public struct Stat
        {
            UInt64 st_dev;

            UInt64 st_ino;

            public UInt64 st_nlink;
            public UInt32 st_mode;

            public UInt32 st_uid;
            public UInt32 st_gid;

            UInt32 __pad0;

            UInt64 st_rdev;

            public Int64 st_size;
            UInt64 st_blksize;

            /* Number 512-byte blocks allocated. */
            UInt64 st_blocks;

            public Int64 st_atime;
            UInt64 st_atime_nsec;

            public Int64 st_mtime;
            UInt32 st_mtime_nsec;

            UInt64 st_ctime;
            UInt64 st_ctime_nsec;

            UInt64 __pad1;
            UInt64 __pad2;
            UInt64 __pad3;
        };
#endif
        const string DLL_PATH = @"hello";

        static readonly UInt32 S_IFDIR = Convert.ToUInt32("0040000", 8);
        static readonly UInt32 S_IFREG = Convert.ToUInt32("0100000", 8);

        static readonly UInt32 O_ACCMODE = Convert.ToUInt32("00000003", 8);
        static readonly UInt32 O_RDONLY = 0x0000;

        static readonly UInt32 ENOENT = 2;
        static readonly UInt32 EACCES = 13;

        

        static RDAReader reader = new RDAReader();

        [DllImport(DLL_PATH)]
        private static extern int pre_main(int argc, string[] argv);

        [DllImport(DLL_PATH)]
        private static extern int main();


        [DllImport(DLL_PATH)]
        private static extern IntPtr GetRdaParameter();

        [DllImport(DLL_PATH)]
        private static extern unsafe void PatchOpenOperations(delegate* unmanaged[Cdecl]<IntPtr, Fuse3FileInfo*, int> callback);

        [DllImport(DLL_PATH)]
        private static extern unsafe void PatchFuseGetattrOperations(delegate* unmanaged[Cdecl]<IntPtr, Stat*, int> callback);


        [DllImport(DLL_PATH)]
        private static extern unsafe void PatchFuseReadOperations(delegate* unmanaged[Cdecl]<IntPtr, IntPtr, nuint, nint, IntPtr, int> callback);

        [DllImport(DLL_PATH)]
        private static extern unsafe void PatchFuseReaddirOperations(delegate* unmanaged[Cdecl]<IntPtr, IntPtr, delegate* unmanaged[Cdecl]<IntPtr, byte*, IntPtr, nint, int, int>, nint, IntPtr, int> callback);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static unsafe int impl_open(IntPtr path, Fuse3FileInfo* fi)
        {
            if ((fi->flags & O_ACCMODE) != O_RDONLY)
                return (int)-EACCES;

            return 0;
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static unsafe int impl_getattr(IntPtr path, Stat* st)
        {
            bool IsNotADir = false;
            System.Runtime.CompilerServices.Unsafe.InitBlock(st, 0, 144);



            st->st_uid = 1000;
            st->st_gid = 1000;
            st->st_atime = DateTimeOffset.Now.ToUnixTimeSeconds();
            st->st_mtime = DateTimeOffset.Now.ToUnixTimeSeconds();

            try
            {

                string strPath = Marshal.PtrToStringAnsi(path);
                RDAFolder? folder = null;
                if (strPath.Equals("/"))
                {
                    folder = reader.rdaFolder;
                }
                else
                {
                    // folder = RDAFolder.NavigateTo(reader.rdaFolder, Path.GetDirectoryName(strPath), "");
                    folder = SeachFolderIn(reader.rdaFolder, strPath);
                    if (folder is null)
                    {
                        folder = SeachFolderIn(reader.rdaFolder, Path.GetDirectoryName(strPath).Replace("\\", "/"));
                        IsNotADir = true;
                        System.Console.WriteLine($"{strPath} seems to be a file? {folder is not null}");
                    }
                }

                if (folder is not null)
                {

                    var file = folder.Files.Find(f => f.FileName.Equals(strPath.Substring(1)));
                    System.Console.WriteLine($"Testing {strPath} file? {file is null}");
                    if (file is not null)
                    {
                        System.Console.WriteLine($"{strPath} is a file!");

                        st->st_mode = S_IFREG | Convert.ToUInt32("0444", 8);
                        st->st_nlink = 1;
                        st->st_size = (long)file.UncompressedSize;

                        st->st_atime = ((DateTimeOffset)file.TimeStamp).ToUnixTimeSeconds();
                        st->st_mtime = ((DateTimeOffset)file.TimeStamp).ToUnixTimeSeconds();
                        return 0;
                    }
                    else if (!IsNotADir)
                    {
                        st->st_mode = S_IFDIR | Convert.ToUInt32("0755", 8);
                        st->st_nlink = 2;

                        return 0;
                    }
                }
                else
                {
                    return -2;
                }


            }
            catch
            {
                // Exceptions escaping out of UnmanagedCallersOnly methods are treated as unhandled exceptions.
                // The errors have to be marshalled manually if necessary.
                return -5;
            }

            return -2;
        }

        public static RDAFolder? SeachFolderIn(RDAFolder folder, string strPath)
        {
            Queue<string> Queue = new Queue<string>(strPath.Split('/'));
            Queue.Dequeue();
            return SeachFolderIn(folder, Queue);
        }
        public static RDAFolder? SeachFolderIn(RDAFolder folder, Queue<string> Queue)
        {
            var folderToFind = Queue.Dequeue();
            foreach (RDAFolder rdaFolder in folder.Folders)
            {
                if (rdaFolder.Name.Equals(folderToFind))
                {
                    if (!Queue.Any())
                    {
                        return rdaFolder;
                    }
                    else
                    {
                        return SeachFolderIn(rdaFolder, Queue);
                    }
                }
            }
            return null;
        }


        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static unsafe int impl_readdir(IntPtr path, IntPtr buffer, delegate* unmanaged[Cdecl]<IntPtr, byte*, IntPtr, nint, int, int> filler, nint offset, IntPtr fi)
        {
            try
            {

                string strPath = Marshal.PtrToStringAnsi(path);
                Console.WriteLine($"Trying readdir {strPath} offset: {offset}");
                Console.WriteLine($"readdir {strPath} offset: {offset}");

                RDAFolder? folder = null;
                System.Console.WriteLine(reader.rdaFolder.Folders.Count);
                if (strPath.Equals("/"))
                {
                    folder = reader.rdaFolder;
                }
                else
                {
                    //folder = RDAFolder.NavigateTo(reader.rdaFolder, strPath, "");
                    folder = SeachFolderIn(reader.rdaFolder, strPath);
                }

                System.Console.WriteLine($"GetDirectoryName({strPath}) {Path.GetDirectoryName(strPath)?.Replace("\\", "/")}");

                if (folder is not null)
                {
                    var lfile = folder.Files.Find(f => f.FileName.Equals(strPath.Substring(1)));
                    if (lfile is not null)
                    {
                        System.Console.WriteLine($"{strPath} is a file: {lfile.FileName}");
                        return -2;
                    }
                    else
                    {
                        System.Console.WriteLine($"{strPath} is a Folder! {folder.FullPath}");
                    }

                    var dotdot = Encoding.UTF8.GetBytes(".." + "\0");
                    fixed (byte* p = &dotdot[0])
                    {
                        filler(buffer, p, IntPtr.Zero, 0, 0);
                    }

                    var dot = Encoding.UTF8.GetBytes("." + "\0");
                    fixed (byte* p = &dot[0])
                    {
                        filler(buffer, p, IntPtr.Zero, 0, 0);
                    }

                    foreach (var subFolder in folder.Folders)
                    {
                        var uft8Filename = Encoding.UTF8.GetBytes(subFolder.Name + "\0");
                        fixed (byte* p = &uft8Filename[0])
                        {
                            filler(buffer, p, IntPtr.Zero, 0, 0);
                        }
                    }

                    foreach (var file in folder.Files)
                    {
                        System.Console.WriteLine($"File: {Path.GetFileName(file.FileName)}");
                        var uft8Filename = Encoding.UTF8.GetBytes(Path.GetFileName(file.FileName) + "\0");
                        fixed (byte* p = &uft8Filename[0])
                        {
                            filler(buffer, p, IntPtr.Zero, 0, 0);
                        }
                    }
                    return 0;
                }
                else
                {
                    System.Console.WriteLine($"{strPath} not found!");
                    return -2;
                }


            }
            catch (Exception ex)
            {
                // Exceptions escaping out of UnmanagedCallersOnly methods are treated as unhandled exceptions.
                // The errors have to be marshalled manually if necessary.

                return -1;
            }
            return -2;
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static int impl_read(IntPtr path, IntPtr buffer, nuint size, nint offset, IntPtr fi)
        {
            try
            {
                Span<byte> bytes;
                unsafe { bytes = new Span<byte>((byte*)buffer, (int)size); }

                string strPath = Marshal.PtrToStringAnsi(path);
                RDAFolder folder = null;
                if (strPath.Equals("/"))
                {
                    folder = reader.rdaFolder;
                }
                else
                {
                    // folder = RDAFolder.NavigateTo(reader.rdaFolder, Path.GetDirectoryName(strPath).Replace("\\", "/"), "");
                    folder = SeachFolderIn(reader.rdaFolder, Path.GetDirectoryName(strPath).Replace("\\", "/"));
                }

                if (folder is not null)
                {

                    var file = folder.Files.Find(f => f.FileName.Equals(strPath.Substring(1)));
                    if (file is not null)
                    {
                        var data = file.GetData();

                        int upper = Math.Min(Math.Min((int)size, data.Length) + (int)offset, data.Length);
                        int ret = ((int)upper - (int)offset);
                        if (ret <= 0)

                        {
                            return 0;
                            //throw new Exception("");
                        }
                        try
                        {
                            System.Console.WriteLine($"{strPath} is a file! upper: {upper} off: {offset} size: {size}");
                            data[(int)offset..(int)upper].CopyTo(bytes);
                            return (int)upper - (int)offset;
                        }
                        catch (Exception)
                        {
                            throw;
                        }

                    }
                }


            }
            catch (Exception ex)
            {
                // Exceptions escaping out of UnmanagedCallersOnly methods are treated as unhandled exceptions.
                // The errors have to be marshalled manually if necessary.

                System.Console.WriteLine(ex.ToString());
                return -1;
            }

            return -2;
        }

        public static void Main(string[] badargs)
        {
            var argsShifted = new string[badargs.Length+1];
            argsShifted[0] = "";
            Array.Copy(badargs,0,argsShifted,1,badargs.Length);
            System.Console.WriteLine(badargs.Length);
            System.Console.WriteLine(argsShifted.ToString());
            pre_main(argsShifted.Length,argsShifted);

            System.Console.WriteLine($"st_nlink: {Marshal.OffsetOf(typeof(Stat), "st_nlink")}");
            System.Console.WriteLine($"st_mode: {Marshal.OffsetOf(typeof(Stat), "st_mode")}");
            System.Console.WriteLine($"st_uid: {Marshal.OffsetOf(typeof(Stat), "st_uid")}");
            System.Console.WriteLine($"st_gid: {Marshal.OffsetOf(typeof(Stat), "st_gid")}");
            System.Console.WriteLine($"st_rdev: {Marshal.OffsetOf(typeof(Stat), "st_rdev")}");
            System.Console.WriteLine($"st_atime: {Marshal.OffsetOf(typeof(Stat), "st_atime")}");
            System.Console.WriteLine($"st_mtime: {Marshal.OffsetOf(typeof(Stat), "st_mtime")}");

            // var fileName = @"C:\Program Files (x86)\Ubisoft\Related Designs\ANNO 2070 DEMO\maindata\data0.rda";
            string? fileName = Marshal.PtrToStringAnsi(GetRdaParameter());

            //var fileName = @"/home/lukas/WinGuest/Data0.rda";
            reader.FileName = fileName;
            reader.ReadRDAFile();
            System.Console.WriteLine($"stat size: {System.Runtime.InteropServices.Marshal.SizeOf(typeof(Stat))}");

            unsafe
            {
                Console.WriteLine($"NativeFunctio that invokes a callback!");
                PatchOpenOperations(&impl_open);
                PatchFuseReadOperations(&impl_read);
                PatchFuseReaddirOperations(&impl_readdir);
                PatchFuseGetattrOperations(&impl_getattr);
            }
            //string[] argv = { "", "-f", "-h", "/home/lukas/SSFS/mnt/" };
            //string[] argv = { "", "-f", "-h"};
            main();

        }
    }
}
