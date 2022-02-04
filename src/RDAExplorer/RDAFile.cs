using RDAExplorer.Misc;
using System;
using System.IO;

namespace RDAExplorer
{
    public class RDAFile
    {
        public string OverwrittenFilePath = "";
        public string FileName;
        public FileHeader.Version Version;
        public Flag Flags;
        public ulong Offset;
        public ulong UncompressedSize;
        public ulong CompressedSize;
        public DateTime TimeStamp;
        public BinaryReader BinaryFile;
        public FileStream FileStream;

        readonly object LockObject = new object();
        private byte[] ContentData;
        private bool ReadAndProcessed;

        public void SetFile(string file)
        {
            FileInfo fileInfo = new FileInfo(file);
            OverwrittenFilePath = file;
            TimeStamp = fileInfo.LastWriteTime;
            Offset = 0;
            CompressedSize = (ulong)fileInfo.Length;
            UncompressedSize = (ulong)fileInfo.Length;
            FileStream fileStream = RDAFileStreamCache.Open(file);
            if (fileStream == null)
                return;
            //BinaryFile = new BinaryReader(fileStream);
            FileStream = fileStream;
        }

        public byte[] GetData()
        {
            // Read the source file into a byte array.
            // BinaryFile.BaseStream.Position = (long)Offset;
            //BinaryFile.BaseStream.Position = 0;
            byte[] numArray = null;
            int numBytesToRead = (int)CompressedSize;
            int numBytesRead = 0;
            // One migh considerer this to be a memory leak :)
            if (ContentData is null)
            {
                if (BinaryFile.BaseStream is FileStream)
                {
                    numArray = new byte[CompressedSize];
                    while (numBytesToRead > 0)
                    {
                        // Read may return anything from 0 to numBytesToRead.
                        Span<Byte> dstSpan = numArray.AsSpan()[numBytesRead..numBytesToRead];
                        // int k = BinaryFile.Read(numArrayGood, numBytesRead, numBytesToRead);
                        int n = RandomAccess.Read(FileStream.SafeFileHandle, dstSpan, (long)this.Offset + (long)numBytesRead);

                        // Break when the end of the file is reached.
                        if (n == 0)
                            break;

                        numBytesRead += n;
                        numBytesToRead -= n;
                    }
                    numBytesToRead = numArray.Length;
                }
                else
                {
                    lock (LockObject)
                    {
                        BinaryFile.BaseStream.Position = (long)Offset;
                        numArray = BinaryFile.ReadBytes((int)CompressedSize);
                    }
                }
                lock (LockObject) {
                    this.ContentData = numArray;
                }
            } else
            {
                lock (LockObject) {
                    numArray = this.ContentData;
                }
            }
            
            int r = (int)Flags;
            System.Console.WriteLine(r);
            
            if(!ReadAndProcessed)
            {
                if (string.IsNullOrEmpty(OverwrittenFilePath))
                {
                    if ((Flags & Flag.Encrypted) == Flag.Encrypted)
                        numArray = BinaryExtension.Decrypt(numArray, BinaryExtension.GetDecryptionSeed(Version));
                    if ((Flags & Flag.Compressed) == Flag.Compressed)
                        numArray = ZLib.ZLib.Uncompress(numArray, (int)UncompressedSize);
                }
                lock (LockObject) {
                    this.ContentData = numArray;
                    this.ReadAndProcessed = true;
                }
            }
            return numArray;
        }

        public void ExtractToRoot(string folder)
        {
            string str = folder + "\\" + FileName.Replace("/", "\\");
            string directoryName = Path.GetDirectoryName(str);
            if (!Directory.Exists(directoryName))
                Directory.CreateDirectory(directoryName);
            Extract(str);
        }

        public void Extract(string destinationfile)
        {
            byte[] data = GetData();
            using (FileStream fileStream = new FileStream(destinationfile, FileMode.Create))
                fileStream.Write(data, 0, data.Length);
            new FileInfo(destinationfile).LastWriteTime = TimeStamp;
        }

        public static RDAFile FromUnmanaged(FileHeader.Version version, DirEntry dir, BlockInfo block, BinaryReader reader, RDAMemoryResidentHelper mrm)
        {
            RDAFile rdaFile = new RDAFile();
            rdaFile.FileName = dir.filename;
            rdaFile.Version = version;
            if ((block.flags & 4) != 4)
            {
                if ((block.flags & 1) == 1)
                    rdaFile.Flags |= Flag.Compressed;
                if ((block.flags & 2) == 2)
                    rdaFile.Flags |= Flag.Encrypted;
            }
            if ((block.flags & 4) == 4)
                rdaFile.Flags |= Flag.MemoryResident;
            if ((block.flags & 8) == 8)
                rdaFile.Flags |= Flag.Deleted;
            rdaFile.Offset = dir.offset;
            rdaFile.UncompressedSize = dir.filesize;
            rdaFile.CompressedSize = dir.compressed;
            rdaFile.TimeStamp = DateTimeExtension.FromTimeStamp(dir.timestamp);
            rdaFile.BinaryFile = mrm == null ? reader : new BinaryReader(mrm.Data);
            rdaFile.FileStream = (FileStream)reader.BaseStream;
            return rdaFile;
        }

        public static RDAFile Create(FileHeader.Version version, string file, string folderpath)
        {
            FileInfo fileInfo = new FileInfo(file);
            RDAFile rdaFile = new RDAFile();
            rdaFile.FileName = FileNameToRDAFileName(file, folderpath);
            rdaFile.Version = version;
            rdaFile.OverwrittenFilePath = file;
            rdaFile.TimeStamp = fileInfo.LastWriteTime;
            rdaFile.Offset = 0;
            rdaFile.CompressedSize = (ulong)fileInfo.Length;
            rdaFile.UncompressedSize = (ulong)fileInfo.Length;
            FileStream fileStream = RDAFileStreamCache.Open(file);
            if (fileStream == null)
                return null;
            rdaFile.BinaryFile = new BinaryReader(fileStream);
            rdaFile.FileStream = fileStream;
            return rdaFile;
        }

        public static string FileNameToRDAFileName(string file, string folderpath)
        {
            file = file.Replace("\\", "/").Trim('/');
            folderpath = folderpath.Replace("\\", "/").Trim('/');
            return (folderpath + "/" + Path.GetFileName(file)).Trim('/');
        }

        public enum Flag
        {
            None = 0,
            Compressed = 1,
            Encrypted = 2,
            MemoryResident = 4,
            Deleted = 8,
        }
    }
}
