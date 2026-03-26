using System.Runtime.CompilerServices;
using System.Text;
using San11PVPToolShared.Models;
using San11PVPToolShared.structs;
using San11PVPToolShared.Structs;

namespace San11PVPToolShared.Utils;

public static class SaveDataParser
{
    public static unsafe SaveDataSummary? LoadSaveDataHeader(string saveDataPath)
    {
        byte[] buffer = new byte[Unsafe.SizeOf<S11SaveDataHeader>()];
        using (var fs = File.OpenRead(saveDataPath))
        {
            fs.ReadExactly(buffer);
        }

        var bss = new BinaryStructStream(buffer);
        S11SaveDataHeader s11SaveDataHeader = new();
        bss.Read(ref s11SaveDataHeader);

        San11Version version;
        var kingNameBytes = new ReadOnlySpan<byte>(s11SaveDataHeader.currentKingName, 9);
        var pk22CodeConverter = new PK22CodeConverter();
        string kingName = pk22CodeConverter.Decode(kingNameBytes);
        if (kingName.Contains('□'))
        {
            var big5 = Encoding.GetEncoding("big5");
            kingName = big5.GetString(kingNameBytes);
            version = San11Version.PK11;
        }
        else
        {
            version = San11Version.PK22;
        }

        string nextKingName = "";
        int nextForceId = -1;
        if (version == San11Version.PK22)
        {
            // PK2.2 加载存档中的行动顺序
            // 读取存档
            buffer = File.ReadAllBytes(saveDataPath);
            bss = new BinaryStructStream(buffer);
            S11SaveData s11SaveData = new();
            s11SaveData.FromStream(bss);
            // 获取信息
            bss = new BinaryStructStream(s11SaveData._rawData);
            TryGetNextPlayer(bss, ref nextKingName, ref nextForceId);
        }
        else
        {
            nextKingName = kingName;
        }

        return new SaveDataSummary(kingName, nextKingName, nextForceId);
    }

    private static bool TryGetNextPlayer(BinaryStructStream bss, ref string nextKingName, ref int nextForceId)
    {
        var turnTable = GetTurnTable(bss);
        
        int loopStartId = turnTable.currentTurnIndex + 1; // 从下一个势力开始寻找
        for (int i = 0; i < turnTable.turnTableSize - 1 /*排除当前行动势力*/; i++)
        {
            int forceId = (loopStartId + i) % turnTable.turnTableSize;
            if (forceId is < 0 or >= 47) continue;
            int playerId = GetForcePlayerId(bss, forceId);
            if (playerId == -1) continue;
            int nextKingId = GetForceKingId(bss, forceId);
            nextKingName = GetPersonName(bss, nextKingId);
            nextForceId = forceId;
            return true; // 找到
        }
        return false;
    }

    private const int RawDataStart = 0x1B3;

    private static string GetPersonName(BinaryStructStream stream, int personId)
    {
        int address = 0x48A57 + personId * 0xCB - RawDataStart;
        stream.Seek(address, SeekOrigin.Begin);
        byte[] familyNameB = new byte[5];
        byte[] givenNameB = new byte[5];
        stream.Read(familyNameB);
        stream.Read(givenNameB);

        var pk22CodeConverter = new PK22CodeConverter();
        string familyName = pk22CodeConverter.Decode(familyNameB);
        string givenName = pk22CodeConverter.Decode(givenNameB);
        return $"{familyName}{givenName}";
    }

    private static int GetForceKingId(BinaryStructStream stream, int forceId)
    {
        int address = 0x808DF + forceId * 0xD5 - RawDataStart;
        stream.Seek(address, SeekOrigin.Begin);
        short kingId = -1;
        stream.Read(ref kingId);
        return kingId;
    }

    private static int GetForcePlayerId(BinaryStructStream stream, int forceId)
    {
        int address = 0x808DF + forceId * 0xD5 - RawDataStart;
        stream.Seek(address + 0x48, SeekOrigin.Begin);
        sbyte playerId = -1;
        stream.Read(ref playerId);
        return playerId;
    }

    private static S11SaveDataTurnTable GetTurnTable(BinaryStructStream stream)
    {
        stream.Seek(0x256 - RawDataStart, SeekOrigin.Begin);
        S11SaveDataTurnTable turnTable = new();
        stream.Read(ref turnTable);
        return turnTable;
    }
}
