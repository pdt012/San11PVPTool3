using System;

namespace San11PVPToolClient.Services;

public static class AppInfo
{
    public static string Name { get; } = "三国志11联机工具";

    public static Version Version { get; } = new Version("3.0.0");

    public static string Author { get; } = "氕氘氚";

    public static string VersionDisplay => Version.ToString(3);

    public static string AppTitle => $"{Name} v{VersionDisplay} by {Author}";
}
