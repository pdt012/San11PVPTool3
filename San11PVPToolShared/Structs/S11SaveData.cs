using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using San11PVPToolShared.Structs;
using San11PVPToolShared.Utils;

namespace San11PVPToolShared.structs;

using int8 = sbyte;
using uint8 = byte;

public class S11SaveData : IBinarySerializable
{
    private const int CompressedDataMarker = 0x1A43574D;

    public S11SaveDataHeader header = new(); // 存档头部

    private int __1b3 = 0x1A43574D; // 1b3
    private int _rawDataSize; // 1b7 压缩数据解压后大小
    private int _compressedDataSize; // 1bb 压缩数据块大小
    public byte[] _rawData = Array.Empty<byte>(); // 1b3 数据

    public int Size => Unsafe.SizeOf<S11SaveDataHeader>() + _rawData.Length;

    public void FromStream(BinaryStructStream stream)
    {
        stream.Read(ref header);
        if (header.fileHeader.isCompressed) // 如果处于压缩状态，则先解压
        {
            if (stream.Length - stream.Position < sizeof(int) * 3)
                throw new InvalidDataException("Compressed save metadata is incomplete.");

            stream.Read(ref __1b3);
            stream.Read(ref _rawDataSize);
            stream.Read(ref _compressedDataSize);

            if (__1b3 != CompressedDataMarker)
                throw new InvalidDataException("Compressed save marker is invalid.");
            if (_rawDataSize <= 0 || _rawDataSize > SaveDataLimits.MaxDecompressedSizeBytes)
                throw new InvalidDataException("Declared decompressed save size is invalid.");
            if (_compressedDataSize <= 0 || _compressedDataSize > SaveDataLimits.MaxFileSizeBytes)
                throw new InvalidDataException("Declared compressed save size is invalid.");
            if (_compressedDataSize != stream.Length - stream.Position)
                throw new InvalidDataException("Compressed save size does not match the file length.");

            var compressedData = new byte[_compressedDataSize];
            stream.Read(compressedData);
            _rawData = ZlibHelper.DecompressToBytes(
                compressedData,
                _rawDataSize);
            if (_rawData.Length != _rawDataSize)
                throw new InvalidDataException("Decompressed save size does not match the declared size.");

            header.fileHeader.isCompressed = false;
        }
        else
        {
            var rawDataSize = stream.Length - Unsafe.SizeOf<S11SaveDataHeader>();
            if (rawDataSize < 0 || rawDataSize > SaveDataLimits.MaxDecompressedSizeBytes)
                throw new InvalidDataException("Uncompressed save size is invalid.");

            _rawData = new byte[rawDataSize];
            stream.Read(_rawData);
        }
    }

    public void ToStream(BinaryStructStream stream)
    {
        stream.Write(ref header);
        stream.Write(_rawData);
    }
}


[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct S11SaveDataTurnTable
{
    public fixed int8 turnTable[47];        // 256 turnIndex -> forceId
    public uint8 currentTurnIndex;          // 285 当前行动 index
    public uint8 turnTableSize;             // 286 当前存活势力数
}
