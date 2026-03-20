using System.Runtime.CompilerServices;
using System.Text;
using San11PVPToolShared.Models;
using San11PVPToolShared.Structs;

namespace San11PVPToolShared.Utils;

public static class SaveDataParser
{
    public static unsafe SaveDataSummary? LoadSaveDataHeader(string saveDataPath)
    {
        byte[] buffer = new byte[Unsafe.SizeOf<S11SaveDataHeader>()];
        using var fs = File.OpenRead(saveDataPath);
        fs.ReadExactly(buffer);

        var bss = new BinaryStructStream(buffer);
        S11SaveDataHeader s11SaveDataHeader = new();
        bss.Read(ref s11SaveDataHeader);

        var kingNameBytes = new ReadOnlySpan<byte>(s11SaveDataHeader.currentKingName, 9);
        var pk22CodeConverter = new PK22CodeConverter();
        string kingName = pk22CodeConverter.Decode(kingNameBytes);
        if (kingName.Contains('□'))
        { 
            var big5 = Encoding.GetEncoding("big5");
            kingName = big5.GetString(kingNameBytes);
        }

        return new SaveDataSummary(
            kingName,
            s11SaveDataHeader.currentKingId
        );
    }
}
