namespace San11PVPToolClient.Models;

public record UserSettings(
    string SaveDataDir = "",
    bool AutoUpload = false,
    bool AutoDownload = false,
    int XOffset = 0  // 屏幕x方向偏移修正
);
