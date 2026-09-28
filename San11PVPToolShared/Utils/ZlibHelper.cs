using Ionic.Zlib;

namespace San11PVPToolShared.Utils;

public static class ZlibHelper
{
    public static byte[] Compress(byte[] input)
    {
        using MemoryStream inputStream = new(input);
        using ZlibStream deflateStream = new(inputStream, CompressionMode.Compress);
        using MemoryStream outputStream = new();
        deflateStream.CopyTo(outputStream);
        return outputStream.ToArray();
    }

    public static byte[] DecompressToBytes(byte[] compressedBytes)
    {
        using MemoryStream inputStream = new(compressedBytes);
        using ZlibStream deflateStream = new(inputStream, CompressionMode.Decompress);
        using MemoryStream outputStream = new();
        deflateStream.CopyTo(outputStream);
        return outputStream.ToArray();
    }
}
