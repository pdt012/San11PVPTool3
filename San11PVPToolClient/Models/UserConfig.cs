namespace San11PVPToolClient.Models;

public record UserConfig(
    string UserName = "",
    string ServerAddress = "",
    string SaveDataDir = "",
    bool AutoUpload = false,
    bool AutoDownload = false,
    int XOffset = 0  // 屏幕x方向偏移修正
);
