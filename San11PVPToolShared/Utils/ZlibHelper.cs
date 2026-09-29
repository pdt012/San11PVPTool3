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

    public static byte[] DecompressToBytes(byte[] compressedBytes, int maxOutputBytes)
    {
        if (maxOutputBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxOutputBytes));

        try
        {
            using MemoryStream inputStream = new(compressedBytes);
            using ZlibStream deflateStream = new(inputStream, CompressionMode.Decompress);
            using MemoryStream outputStream = new();
            var buffer = new byte[81920];
            int bytesRead;
            while ((bytesRead = deflateStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (outputStream.Length > maxOutputBytes - bytesRead)
                    throw new InvalidDataException($"Decompressed data exceeds {maxOutputBytes} bytes.");

                outputStream.Write(buffer, 0, bytesRead);
            }

            return outputStream.ToArray();
        }
        catch (ZlibException ex)
        {
            throw new InvalidDataException("Compressed save data is invalid.", ex);
        }
    }
}
