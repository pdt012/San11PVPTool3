using System.IO;
using System.Text.Json;
using San11PVPToolClient.Models;

namespace San11PVPToolClient.Services;

public class UserSettingsService
{
    public const string SettingsFileName = "UserSettings.json";
    
    public UserSettings Settings
    {
        get;
        set
        {
            field = value;
            Save();
        }
    }

    public UserSettingsService()
    {
        Settings = Load();
    }

    public void Save()
    {
        string json = JsonSerializer.Serialize(Settings);
        File.WriteAllText(SettingsFileName, json);
    }

    private UserSettings Load()
    {
        if (!File.Exists(SettingsFileName))
            return new UserSettings();

        string json = File.ReadAllText(SettingsFileName);
        return JsonSerializer.Deserialize<UserSettings>(json)!;
    }
}
