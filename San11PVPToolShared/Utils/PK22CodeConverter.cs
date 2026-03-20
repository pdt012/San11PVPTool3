using System.Text;
using System.Xml;

namespace San11PVPToolShared.Utils;

public class PK22CodeConverter
{
    private const byte ASCII_MAX = 0x7F;

    // ushort -> Unicode
    private static readonly char?[] bytes2uniMap = new char?[65536];

    /// <summary>
    /// 码表初始化
    /// </summary>
    public static void Init(string codeTablePath)
    {
        Array.Clear(bytes2uniMap, 0, bytes2uniMap.Length);

        XmlDocument doc = new();
        doc.Load(codeTablePath);

        XmlElement? root = doc.DocumentElement;
        XmlNodeList? nodeList = root?.SelectNodes("ch");

        if (nodeList == null)
            return;

        foreach (XmlNode node in nodeList)
        {
            var attrs = node.Attributes;
            if (attrs == null)
                continue;

            string? k = attrs["key"]?.Value;
            string? v = attrs["value"]?.Value;

            if (k == null)
                continue;

            ushort code = Convert.ToUInt16(k, 16);

            if (!string.IsNullOrEmpty(v))
            {
                char c = v[0];
                bytes2uniMap[code] = c;
            }
            else
            {
                bytes2uniMap[code] = null;
            }
        }
    }

    public string Decode(ReadOnlySpan<byte> bytes)
    {
        StringBuilder result = new(bytes.Length);
        int i = 0;
        while (i < bytes.Length)
        {
            byte b = bytes[i];
            if (b == 0)
                break;
            if (b <= ASCII_MAX)
            {
                result.Append((char)b);
            }
            else
            {
                if (i + 1 >= bytes.Length) // 意外结尾
                {
                    result.Append('□');
                    break;
                }

                byte b2 = bytes[i + 1];
                ushort code = (ushort)((b << 8) | b2);
                char? c = bytes2uniMap[code];
                result.Append(c ?? '□');
                i++;
            }

            i++;
        }

        return result.ToString();
    }
}
