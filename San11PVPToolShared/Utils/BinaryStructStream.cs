using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace San11PVPToolShared.Utils;

public class BinaryStructStream
{
    private readonly byte[] data;

    public int Length => data.Length;

    public int Position { get; private set; }

    public BinaryStructStream(byte[] data)
    {
        this.data = data ?? throw new ArgumentNullException(nameof(data));
        Position = 0;
    }

    public void Seek(int offset, SeekOrigin origin)
    {
        int newPos;

        switch (origin)
        {
            case SeekOrigin.Begin:
                newPos = offset;
                break;

            case SeekOrigin.Current:
                newPos = Position + offset;
                break;

            case SeekOrigin.End:
                newPos = Length + offset;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(origin));
        }

        if (newPos < 0 || newPos > Length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        Position = newPos;
    }

    public void Skip(int size)
    {
        Seek(size, SeekOrigin.Current);
    }

    private void EnsureLength(int size)
    {
        if (Position + size > Length)
            throw new EndOfStreamException(
                $"End Of BinaryStructStream: need {size} bytes at position {Position}");
    }

    public T Read<T>() where T : unmanaged
    {
        int size = Unsafe.SizeOf<T>();
        EnsureLength(size);

        T value = MemoryMarshal.Read<T>(
            data.AsSpan(Position, size)
        );

        Position += size;

        return value;
    }

    public void Read<T>(ref T target) where T : unmanaged
    {
        int size = Unsafe.SizeOf<T>();
        EnsureLength(size);

        target = MemoryMarshal.Read<T>(
            data.AsSpan(Position, size)
        );

        Position += size;
    }

    public void Write<T>(ref T source) where T : unmanaged
    {
        int size = Unsafe.SizeOf<T>();
        EnsureLength(size);

        MemoryMarshal.Write(
            data.AsSpan(Position, size),
            ref source
        );

        Position += size;
    }

    public void Read<T>(T[] array) where T : unmanaged
    {
        int elementSize = Unsafe.SizeOf<T>();
        int totalSize = elementSize * array.Length;

        EnsureLength(totalSize);

        var src = data.AsSpan(Position, totalSize);
        var dst = MemoryMarshal.Cast<T, byte>(array.AsSpan());

        src.CopyTo(dst);

        Position += totalSize;
    }

    public void Write<T>(T[] array) where T : unmanaged
    {
        int elementSize = Unsafe.SizeOf<T>();
        int totalSize = elementSize * array.Length;

        EnsureLength(totalSize);

        var src = MemoryMarshal.Cast<T, byte>(array.AsSpan());
        var dst = data.AsSpan(Position, totalSize);

        src.CopyTo(dst);

        Position += totalSize;
    }

    public void Read(byte[] array)
    {
        EnsureLength(array.Length);
        data.AsSpan(Position, array.Length).CopyTo(array);
        Position += array.Length;
    }

    public void Write(byte[] array)
    {
        EnsureLength(array.Length);
        array.AsSpan().CopyTo(data.AsSpan(Position));
        Position += array.Length;
    }

    public void Read(IBinarySerializable value)
    {
        value.FromStream(this);
    }

    public void Write(IBinarySerializable value)
    {
        value.ToStream(this);
    }

    public void Read(IBinarySerializable[] array)
    {
        for (int i = 0; i < array.Length; i++)
        {
            array[i].FromStream(this);
        }
    }

    public void Write(IBinarySerializable[] array)
    {
        for (int i = 0; i < array.Length; i++)
        {
            array[i].ToStream(this);
        }
    }
}

public interface IBinarySerializable
{
    int Size { get; }

    void FromStream(BinaryStructStream stream);
    void ToStream(BinaryStructStream stream);

    void FromStream(Stream iStream)
    {
        byte[] buffer = new byte[Size];
        iStream.ReadExactly(buffer);
        BinaryStructStream stream = new(buffer);
        FromStream(stream);
    }

    void ToStream(Stream oStream)
    {
        byte[] buffer = new byte[Size];
        var stream = new BinaryStructStream(buffer);
        ToStream(stream);
        oStream.Write(buffer, 0, buffer.Length);
    }
}
