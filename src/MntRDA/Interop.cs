namespace MntRDA
{
    internal static class Interop
    {
        internal static class Error
        {
            internal const int ENOENT = 0x02;
            internal const int EBADF = 0x09;
            internal const int EACCES = 0x0d;
        }

        internal static class FileTypes
        {
            internal const int S_IFMT = 0xF000;
            internal const int S_IFIFO = 0x1000;
            internal const int S_IFCHR = 0x2000;
            internal const int S_IFDIR = 0x4000;
            internal const int S_IFREG = 0x8000;
            internal const int S_IFLNK = 0xA000;
            internal const int S_IFSOCK = 0xC000;
        }


        internal static class OpenFlags
        {
            internal const int O_ACCMODE = 0x0003;

            // Access modes (mutually exclusive)
            internal const int O_RDONLY = 0x0000;
            internal const int O_WRONLY = 0x0001;
            internal const int O_RDWR = 0x0002;

            // Flags (combinable)
            internal const int O_CLOEXEC = 0x0010;
            internal const int O_CREAT = 0x0020;
            internal const int O_EXCL = 0x0040;
            internal const int O_TRUNC = 0x0080;
            internal const int O_SYNC = 0x0100;
        }
    }
}