using System.IO;
using System.Text.Json;
using San11PVPToolClient.Models;

namespace San11PVPToolClient.Services;

public class UserConfigService
{
    public UserConfig Config
    {
        get;
        set
        {
            field = value;
            Save();
        }
    }

    public UserConfigService()
    {
        Config = Load();
    }

    public void Save()
    {
        string json = JsonSerializer.Serialize(Config);
        File.WriteAllText("userconfig.json", json);
    }

    private UserConfig Load()
    {
        if (!File.Exists("userconfig.json"))
            return new UserConfig();

        string json = File.ReadAllText("userconfig.json");
        return JsonSerializer.Deserialize<UserConfig>(json)!;
    }
}
