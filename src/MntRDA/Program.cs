using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using System.Text;
using System.Diagnostics;
using RDAExplorer;


namespace MntRDA
{
    internal class Program
    {
        static List<RDAReader> RDAReaders = new ();

        static RDAFolder? MergedFolder;
        static ConcurrentDictionary<long, RDAFile> GlobalFdTable = new();
        static Random FdRandomGen = new Random();
        static SpinLock FdRandomGenSpinLock = new SpinLock();

        internal static RDAFolder? SeachFolderIn(RDAFolder folder, string strPath)
        {
            Queue<string> Queue = new Queue<string>(strPath.Split('/'));
            Queue.Dequeue();
            return SeachFolderIn(folder, Queue);
        }
        internal static RDAFolder? SeachFolderIn(RDAFolder folder, Queue<string> Queue)
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

        internal static Object? LookUpPath(IntPtr path)
        {
            bool IsNotADir = false;
            try
            {

                string strPath = Marshal.PtrToStringUTF8(path)!;
                RDAFolder? folder = null;
                if (strPath.Equals("/"))
                {
                    folder = GetRdaFolder();
                }
                else
                {
                    folder = SeachFolderIn(GetRdaFolder(), strPath);
                    if (folder is null)
                    {
                        folder = SeachFolderIn(GetRdaFolder(), Path.GetDirectoryName(strPath)!.Replace("\\", "/"));
                        IsNotADir = true;
                    }
                }

                if (folder is not null)
                {

                    var file = folder.Files.Find(f => f.FileName.Equals(strPath.Substring(1)));
                    System.Console.WriteLine($"Testing {strPath} file? {file is null}");
                    if (file is not null)
                    {
                        System.Console.WriteLine($"{strPath} is a file!");
                        return file;
                    }
                    else if (!IsNotADir)
                    {
                        return folder;
                    }
                }
            }
            catch
            {
                return null;
            }
            return null;
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe int ImplOpen(IntPtr path, Fuse3FileInfo* fi)
        {
            if ((fi->flags & Interop.OpenFlags.O_ACCMODE) != Interop.OpenFlags.O_RDONLY)
                return (int)-Interop.Error.EACCES;

            var maybeFile = LookUpPath(path) as RDAFile;

            if (maybeFile is not null)
            {
                int fid;
                bool lockTaken = false;
                try
                {
                    FdRandomGenSpinLock.Enter(ref lockTaken);
                    do
                    {
                        fid = FdRandomGen.Next();
                    } while (!GlobalFdTable.TryAdd(fid,maybeFile));
                }
                finally
                {
                    if (lockTaken)
                    {
                        FdRandomGenSpinLock.Exit(false);
                    }
                }
                fi->fh = fid;
                System.Console.WriteLine($"Added fd: {fid}");
            }
            return 0;
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe int ImplRelease(IntPtr path, Fuse3FileInfo* fi)
        {
            var rem = GlobalFdTable.Remove(fi->fh, out RDAFile? _file);
            Debug.Assert(rem);
            System.Console.WriteLine($"Released: {fi->fh}");
            return 0;
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe int ImplGetattr(IntPtr path, Stat* st)
        {
            System.Runtime.CompilerServices.Unsafe.InitBlock(st, 0, (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(Stat)));

            int ret = -Interop.Error.ENOENT;
            st->st_uid = 1000;
            st->st_gid = 1000;

            try
            {
                string strPath = Marshal.PtrToStringUTF8(path)!;
                switch (LookUpPath(path))
                {
                    case RDAFile file:
                        {
                            System.Console.WriteLine($"{strPath} is a file!");
                            st->st_mode = Interop.FileTypes.S_IFREG | Convert.ToUInt32("0444", 8);
                            st->st_nlink = 1;
                            st->st_size = (long)file.UncompressedSize;

                            long timeStamp = ((DateTimeOffset)file.TimeStamp).ToUnixTimeSeconds();
                            st->st_atime = timeStamp;
                            st->st_mtime = timeStamp;
                            st->st_ctime = timeStamp;
                            ret = 0;
                        }
                        break;
                    case RDAFolder folder:
                        {
                            st->st_mode = Interop.FileTypes.S_IFDIR | Convert.ToUInt32("0555", 8);
                            st->st_nlink = 2;

                            long now = DateTimeOffset.Now.ToUnixTimeSeconds();
                            st->st_atime = now;
                            st->st_mtime = now;
                            st->st_ctime = now;
                            ret = 0;
                        }
                        break;
                    default:
                        ret = -Interop.Error.ENOENT;
                        break;
                };
            }
            catch
            {
                // Exceptions escaping out of UnmanagedCallersOnly methods are treated as unhandled exceptions.
                // The errors have to be marshalled manually if necessary.
                ret = -Interop.Error.EACCES;
            }
            return ret;
        }


        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe int ImplReaddir(IntPtr path, IntPtr buffer, delegate* unmanaged[Cdecl]<IntPtr, byte*, IntPtr, nint, int, int> filler, nint offset, IntPtr fi)
        {
            int ret = -Interop.Error.ENOENT;
            try
            {
                string strPath = Marshal.PtrToStringUTF8(path)!;
                switch (LookUpPath(path))
                {
                    case RDAFile file:
                        ret = -Interop.Error.ENOENT;
                        break;
                    case RDAFolder folder:
                        {
                            Span<byte> bufferUtf8NullTerminated = stackalloc byte[255];
                            FillerWrapper(".",bufferUtf8NullTerminated);
                            FillerWrapper("..",bufferUtf8NullTerminated);
                            foreach (var subFolder in folder.Folders)
                            {
                                FillerWrapper(subFolder.Name,bufferUtf8NullTerminated);
                            }
                            foreach (var file in folder.Files)
                            {
                                FillerWrapper(Path.GetFileName(file.FileName),bufferUtf8NullTerminated);
                            }
                            ret = 0;
                        }
                        break;
                    default:
                        ret = -Interop.Error.ENOENT;
                        break;
                };
            }
            catch
            {
                // Exceptions escaping out of UnmanagedCallersOnly methods are treated as unhandled exceptions.
                // The errors have to be marshalled manually if necessary.
                ret = -Interop.Error.EACCES;
            }
            return ret;


            void FillerWrapper(string v,Span<byte> utf8NullTerminated) {
                var res = Encoding.UTF8.GetBytes(v.AsSpan(), utf8NullTerminated);
                utf8NullTerminated[res] = (byte)'\0';
                fixed (byte* p = &utf8NullTerminated[0])
                {
                    filler(buffer, p, IntPtr.Zero, 0, 0);
                }
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe int ImplRead(IntPtr path, IntPtr buffer, nuint size, nint offset, Fuse3FileInfo* fi)
        {
            var file = GlobalFdTable[fi->fh];
            if (file is not null)
            {
                Span<byte> bytes = new Span<byte>((byte*)buffer, (int)size);
                var data = file.GetData();

                int upper = Math.Min(Math.Min((int)size, data.Length) + (int)offset, data.Length);
                int ret = ((int)upper - (int)offset);

                if (ret <= 0)
                {
                    return 0;
                }
                try
                {
                    //System.Console.WriteLine($"{strPath} is a file! upper: {upper} off: {offset} size: {size}");
                    data.AsSpan()[(int)offset..(int)upper].CopyTo(bytes);
                    return (int)upper - (int)offset;
                }
                catch (Exception)
                {
                    throw;
                }
            }
            return -Interop.Error.EBADF;
        }
        internal static string[] ShiftArgs(string[] orignialArgs) {
            var argsShifted = new string[orignialArgs.Length + 1];
            argsShifted[0] = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            Array.Copy(orignialArgs, 0, argsShifted, 1, orignialArgs.Length);
            System.Console.WriteLine(orignialArgs.Length);
            System.Console.WriteLine(argsShifted.ToString());
            return argsShifted;
        }

        public static void Main(string[] badargs)
        {
            var argsShifted = ShiftArgs(badargs);
            FuseNativeAdapter.pre_main(argsShifted.Length, argsShifted);

            FuseNativeAdapter.PrintHelpIfNeeded();

            System.Console.WriteLine($"st_nlink: {Marshal.OffsetOf(typeof(Stat), "st_nlink")}");
            System.Console.WriteLine($"st_mode: {Marshal.OffsetOf(typeof(Stat), "st_mode")}");
            System.Console.WriteLine($"st_uid: {Marshal.OffsetOf(typeof(Stat), "st_uid")}");
            System.Console.WriteLine($"st_gid: {Marshal.OffsetOf(typeof(Stat), "st_gid")}");
            System.Console.WriteLine($"st_rdev: {Marshal.OffsetOf(typeof(Stat), "st_rdev")}");
            System.Console.WriteLine($"st_atime: {Marshal.OffsetOf(typeof(Stat), "st_atime")}");
            System.Console.WriteLine($"st_mtime: {Marshal.OffsetOf(typeof(Stat), "st_mtime")}");

            System.Console.WriteLine($"fh: {Marshal.OffsetOf(typeof(Fuse3FileInfo), "fh")}");

            // var fileName = @"C:\Program Files (x86)\Ubisoft\Related Designs\ANNO 2070 DEMO\maindata\data0.rda";
            string? fileName = Marshal.PtrToStringUTF8(FuseNativeAdapter.GetRdaParameter());

            //var fileName = @"/home/lukas/WinGuest/Data0.rda";
            string[] rdaFiles = {
                @"/home/lukas/Games/ubisoft-connect/drive_c/Program Files (x86)/Ubisoft/Ubisoft Game Launcher/games/Anno 1404 - History Edition/addon/data0.rda",
                @"/home/lukas/Games/ubisoft-connect/drive_c/Program Files (x86)/Ubisoft/Ubisoft Game Launcher/games/Anno 1404 - History Edition/addon/data1.rda",
                @"/home/lukas/Games/ubisoft-connect/drive_c/Program Files (x86)/Ubisoft/Ubisoft Game Launcher/games/Anno 1404 - History Edition/addon/data2.rda",
                @"/home/lukas/Games/ubisoft-connect/drive_c/Program Files (x86)/Ubisoft/Ubisoft Game Launcher/games/Anno 1404 - History Edition/addon/data3.rda",
                @"/home/lukas/Games/ubisoft-connect/drive_c/Program Files (x86)/Ubisoft/Ubisoft Game Launcher/games/Anno 1404 - History Edition/addon/data4.rda",
                @"/home/lukas/Games/ubisoft-connect/drive_c/Program Files (x86)/Ubisoft/Ubisoft Game Launcher/games/Anno 1404 - History Edition/addon/data5.rda"
            };
            //string[] rdaFiles = { @"/home/lukas/WinGuest/Data0.rda" };

            List<RDAFile>[] listOfFilelists = new List<RDAFile>[rdaFiles.Length];
            int i = 0;
            foreach (var file in rdaFiles)
            {
                var reader = new RDAReader();
                reader.FileName = file;
                reader.ReadRDAFile();
                listOfFilelists[i++] = reader.rdaFileEntries;
                RDAReaders.Add(reader);
            }
            var lowest = listOfFilelists[0];
            var uppers = listOfFilelists[1..];
            var mergedFilelist = MergeList(lowest,uppers);

            MergedFolder = RDAFolder.GenerateFrom(mergedFilelist, RDAReaders.First().rdaFolder.Version);
            
            System.Console.WriteLine($"stat size: {System.Runtime.InteropServices.Marshal.SizeOf(typeof(Stat))}");

            unsafe
            {
                Console.WriteLine($"Patching NativeFunctions (fuse operation) callback");
                FuseNativeAdapter.PatchOpen(&ImplOpen);
                FuseNativeAdapter.PatchRelease(&ImplRelease);
                FuseNativeAdapter.PatchFuseRead(&ImplRead);
                FuseNativeAdapter.PatchFuseReaddir(&ImplReaddir);
                FuseNativeAdapter.PatchFuseGetattr(&ImplGetattr);
            }
            FuseNativeAdapter.main();
        }

        static RDAFolder GetRdaFolder() {
            return MergedFolder;
        }

        static List<RDAFile> MergeList(List<RDAFile> lower, IEnumerable<List<RDAFile>> uppers) {
            var dict = lower.ToDictionary(f => f.FileName);
            foreach (var upper in uppers)
            {
                MergeList(dict,upper);
            }
            return dict.Values.ToList<RDAFile>();
        }

        static void MergeList(Dictionary<string,RDAFile> lower, List<RDAFile> upper) {
            foreach (var file in upper)
            {
                lower[file.FileName] = file;
            }
        }
    }
}
