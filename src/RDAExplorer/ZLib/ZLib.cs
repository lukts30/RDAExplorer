using System;
using System.IO;
using System.Runtime.InteropServices;
using System.IO.Compression;

namespace RDAExplorer.ZLib
{
    public class ZLib
    {
        /*
        [DllImport("zlib.DLL")]
        private static extern int uncompress(byte[] des, ref int destLen, byte[] src, int srcLen);

        [DllImport("zlib.DLL")]
-       private static extern int compress(byte[] des, ref int destLen, byte[] src, int srcLen);
        */

        public static byte[] Uncompress(byte[] input, int uncompressedSize)
        {
            //byte[] des = new byte[uncompressedSize];
            //Console.WriteLine("\tDecompressing returned " + uncompress(des, ref uncompressedSize, input, input.Length));
            //return des;

            var outputStream = new MemoryStream();
            using (var compressedStream = new MemoryStream(input))
                try
                {
                    using (var inputStream = new ZLibStream(compressedStream, CompressionMode.Decompress))
                    {
                        inputStream.CopyTo(outputStream);
                        outputStream.Position = 0;
                        var arr = outputStream.ToArray();
                        Array.Resize(ref arr, uncompressedSize);
                        return arr;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\tDecompressing returned: ERROR");
                    return new byte[uncompressedSize];
                }

        }


        public static byte[] Compress(byte[] input)
        {
            using var stream = new MemoryStream(input);
            using var memoryStream = new MemoryStream();
            using var zlibStream = new ZLibStream(memoryStream, CompressionMode.Compress);


            stream.Position = 0;
            stream.CopyTo(zlibStream);
            zlibStream.Close();

            return memoryStream.ToArray();
        }
    }
}
