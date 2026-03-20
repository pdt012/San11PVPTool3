using System.Runtime.InteropServices;

namespace San11PVPToolShared.Structs;

using int16 = short;
using int8 = sbyte;
using uint16 = ushort;
using uint8 = byte;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct S11FileHeader
{
    private int __0;
    public int fileType; // 4 文件格式类型
    private fixed byte __8[24]; // 8
    public bool isCompressed; // 20 是否压缩
    private fixed byte __21[57]; // 21
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct S11SaveDataHeader
{
    public S11FileHeader fileHeader; // 文件头

    public fixed byte currentKingName[9]; // 5a 当前君主姓名
    public int16 currentKingId; // 63 当前君主id
    private int8 __65; // 65
    public uint8 scenarioNumber; // 66 剧本编号
    public uint16 startYear; // 67 开始年
    public uint8 startMonth; // 69 开始月
    public uint8 startDay; // 6a 开始日
    public fixed byte scenarioName[17]; // 6b 剧本名
    public uint16 turnCount; // 7c 当前旬数
    private uint16 __7e; // 7e
    public uint8 playerCount; // 80 玩家数
    public uint savedDate; // 81 保存日期（转10进制后分割为年月日：yyyymmdd）
    public uint savedTime; // 85 保存时间（转10进制后每两位分割为时分秒：hhmmss）
    public uint playingMsecs; // 89 游戏总时长(ms)
    public uint8 difficulty; // 8d 难度
    public uint8 ageHistorical; // 8e 是否史实年龄
}
