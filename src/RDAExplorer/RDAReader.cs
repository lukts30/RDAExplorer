using RDAExplorer.Misc;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace RDAExplorer
{
    public class RDAReader : IDisposable
    {
        public Dictionary<string,RDAFile> rdaFileEntries { get; } = new Dictionary<string,RDAFile>();
        public RDAFolder rdaFolder = new RDAFolder(FileHeader.Version.Version_2_2);
        public string FileName;
        private BinaryReader read;
        private FileHeader fileHeader;
        public uint rdaReadBlocks;
        private List<RDASkippedDataSection> skippedDataSections = new List<RDASkippedDataSection>();
        public BackgroundWorker backgroundWorker;
        public string backgroundWorkerLastMessage;

        public IList<RDASkippedDataSection> SkippedDataSections
        {
            get
            {
                return skippedDataSections;
            }
        }

        public uint NumSkippedBlocks
        {
            get
            {
                return (uint)skippedDataSections.Count;
            }
        }

        public ulong NumSkippedFiles
        {
            get
            {
                return (ulong)skippedDataSections.Sum(section => section.blockInfo.fileCount);
            }
        }

        public RDAReader()
        {
        }

        private void UpdateOutput(string message)
        {
            if (UISettings.EnableConsole)
                Console.WriteLine(message);
            if (backgroundWorker == null)
                return;
            backgroundWorkerLastMessage = message;
            backgroundWorker.ReportProgress((int)((double)read.BaseStream.Position / read.BaseStream.Length * 100.0));
        }

        public void ReadRDAFile()
        {
            read = new BinaryReader(new FileStream(FileName, FileMode.Open,FileAccess.Read,FileShare.Read, 0x8000, FileOptions.None));

            byte[] firstTwoBytes = read.ReadBytes(2); read.BaseStream.Position = 0;
            if (firstTwoBytes[0] == 'R' && firstTwoBytes[1] == '\0')
            {
                fileHeader = ReadFileHeader(read, FileHeader.Version.Version_2_0);
            }
            else if (firstTwoBytes[0] == 'R' && firstTwoBytes[1] == 'e')
            {
                fileHeader = ReadFileHeader(read, FileHeader.Version.Version_2_2);
            }
            else
            {
                throw new Exception("Invalid or unsupported RDA file!");
            }

            rdaReadBlocks = 0;
            skippedDataSections.Clear();

            ulong beginningOfDataSection = (ulong)read.BaseStream.Position;
            ulong currentBlockOffset = fileHeader.firstBlockOffset;
            while (currentBlockOffset < (ulong)read.BaseStream.Length)
            {
                ulong nextBlockOffset = ReadBlock(currentBlockOffset, beginningOfDataSection);
                beginningOfDataSection = currentBlockOffset + BlockInfo.GetSize(fileHeader.version);
                currentBlockOffset = nextBlockOffset;
            }

            // When writing we need to make sure that the section that is latest in the file is the last one in this list.
            skippedDataSections.Sort((a, b) => a.offset.CompareTo(b.offset));

            rdaFolder = RDAFolder.GenerateFrom(rdaFileEntries.Values, fileHeader.version);
            UpdateOutput("Done. " + rdaFileEntries.Count + " files. " + rdaReadBlocks + " blocks read, " + NumSkippedBlocks + " encrypted blocks skipped (" + NumSkippedFiles + " files).");
        }

        private static FileHeader ReadFileHeader(BinaryReader reader, FileHeader.Version expectedVersion)
        {
            Encoding expectedEncoding = FileHeader.GetMagicEncoding(expectedVersion);
            string expectedMagic = FileHeader.GetMagic(expectedVersion);
            int expectedByteCount = expectedEncoding.GetByteCount(expectedMagic);
            byte[] actualBytes = reader.ReadBytes(expectedByteCount);
            string actualMagic = expectedEncoding.GetString(actualBytes);

            if (actualMagic == expectedMagic)
            {
                uint unknownBytes = FileHeader.GetUnknownSize(expectedVersion);
                return new FileHeader
                {
                    magic = actualMagic,
                    version = expectedVersion,
                    unkown = reader.ReadBytes((int)unknownBytes),
                    firstBlockOffset = ReadUIntVersionAware(reader, expectedVersion),
                };
            }
            else
            {
                throw new Exception("Invalid or unsupported RDA file!");
            }
        }

        private static ulong ReadUIntVersionAware(BinaryReader reader, FileHeader.Version version)
        {
            return FileHeader.ReadUIntVersionAware(reader, version);
        }
        private ulong ReadUIntVersionAware(BinaryReader reader)
        {
            return ReadUIntVersionAware(reader, fileHeader.version);
        }

        private static uint GetUIntSizeVersionAware(FileHeader.Version version)
        {
            return FileHeader.GetUIntSize(version);
        }
        private uint GetUIntSizeVersionAware()
        {
            return GetUIntSizeVersionAware(fileHeader.version);
        }

        private ulong ReadBlock(ulong Offset, ulong beginningOfDataSection)
        {
            UpdateOutput("----- Reading Block at " + Offset);
            read.BaseStream.Position = (long)Offset;

            BlockInfo blockInfo = new BlockInfo
            {
                flags = read.ReadUInt32(),
                fileCount = read.ReadUInt32(),
                directorySize = ReadUIntVersionAware(read),
                decompressedSize = ReadUIntVersionAware(read),
                nextBlock = ReadUIntVersionAware(read),
            };

            if ((blockInfo.flags & 8) != 8)
            {
                bool isMemoryResident = false;
                bool isEncrypted = false;
                bool isCompressed = false;
                if ((blockInfo.flags & 4) == 4)
                {
                    UpdateOutput("MemoryResident");
                    isMemoryResident = true;
                }
                if ((blockInfo.flags & 2) == 2)
                {
                    UpdateOutput("Encrypted");
                    isEncrypted = true;
                }
                if ((blockInfo.flags & 1) == 1)
                {
                    UpdateOutput("Compressed");
                    isCompressed = true;
                }
                if (blockInfo.flags == 0)
                    UpdateOutput("No Flags");

                int decryptionSeed = 0;
                if (isEncrypted)
                {
                    try {
                        decryptionSeed = BinaryExtension.GetDecryptionSeed(fileHeader.version);
                    } catch (ArgumentException e) {
                        UpdateOutput("Skipping (" + blockInfo.fileCount + " files) -- " + e.Message);
                        skippedDataSections.Add(new RDASkippedDataSection()
                        {
                            blockInfo = blockInfo,
                            offset = beginningOfDataSection,
                            size = (Offset - beginningOfDataSection),
                        });
                        return blockInfo.nextBlock;
                    }
                }
                read.BaseStream.Position = (long)(Offset - blockInfo.directorySize);
                if (isMemoryResident)
                    read.BaseStream.Position -= GetUIntSizeVersionAware() * 2;
                byte[] numArray2 = read.ReadBytes((int)blockInfo.directorySize);
                if (isEncrypted)
                    numArray2 = BinaryExtension.Decrypt(numArray2, decryptionSeed);
                if (isCompressed)
                    numArray2 = ZLib.ZLib.Uncompress(numArray2, (int)blockInfo.decompressedSize);

                RDAMemoryResidentHelper mrm = null;
                if (isMemoryResident)
                {
                    ulong beginningOfHeader = (ulong)read.BaseStream.Position;
                    ulong compressedSize = ReadUIntVersionAware(read);
                    ulong uncompressedSize = ReadUIntVersionAware(read);
                    mrm = new RDAMemoryResidentHelper(beginningOfHeader - blockInfo.directorySize - compressedSize, uncompressedSize, compressedSize, read.BaseStream, blockInfo, fileHeader.version);
                }

                uint dirEntrySize = DirEntry.GetSize(fileHeader.version);
                if (blockInfo.fileCount * dirEntrySize != blockInfo.decompressedSize)
                    throw new Exception("Unexpected directory entry size or count");

                ++rdaReadBlocks;
                UpdateOutput("-- DirEntries:");
                ReadDirEntries(numArray2, blockInfo, mrm);
            }

            return blockInfo.nextBlock;
        }

        private void ReadDirEntries(byte[] buffer, BlockInfo block, RDAMemoryResidentHelper mrm)
        {
            MemoryStream memoryStream = new MemoryStream(buffer);
            BinaryReader reader = new BinaryReader(memoryStream);

            for (uint fileId = 0; fileId < block.fileCount; ++fileId)
            {
                byte[] fileNameBytes = reader.ReadBytes((int)DirEntry.GetFilenameSize());
                string fileNameString = Encoding.Unicode.GetString(fileNameBytes).Replace("\0", "");

                DirEntry dirEntry = new DirEntry
                {
                    filename = fileNameString,
                    offset = ReadUIntVersionAware(reader),
                    compressed = ReadUIntVersionAware(reader),
                    filesize = ReadUIntVersionAware(reader),
                    timestamp = ReadUIntVersionAware(reader),
                    unknown = ReadUIntVersionAware(reader),
                };

                RDAFile rdaFile = RDAFile.FromUnmanaged(fileHeader.version, dirEntry, block, read, mrm);
                rdaFileEntries.Add(rdaFile.FileName,rdaFile);
            }
        }

        public void CopySkippedDataSextion(ulong offset, ulong size, Stream output)
        {
            read.BaseStream.Position = (long)offset;
            read.BaseStream.CopyLimited(output, size);
            // TODO memory resident?
        }

        public void Dispose()
        {
            if (read != null)
                read.Close();
            rdaFileEntries.Clear();
            rdaFolder = null;
            foreach (Stream stream in RDAFileStreamCache.Cache.Values)
                stream.Close();
            RDAFileStreamCache.Cache.Clear();
            GC.Collect();
        }
    }
}
