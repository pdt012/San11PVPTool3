namespace San11PVPToolClient.Models;

public record UserConfig(
    string UserName = "",
    string ServerAddress = "",
    string SaveDataDir = "",
    bool AutoUpload = false,
    bool AutoDownload = false
);
