using System;

namespace San11PVPToolClient.Models;

public record MessageItem(
    string SenderId,
    string SenderName,
    string Message,
    DateTime Time,
    bool IsSystemMessage = false,
    MessageLevel Level = MessageLevel.Normal,
    string DisplayAlignment = "Center"
)
{
    public string MessageForeGround => Level switch
    {
        MessageLevel.Normal => "Gray",
        MessageLevel.Warning => "Orange",
        MessageLevel.Error => "Red",
        _ => "Gray"
    };
}

public enum MessageLevel
{
    Normal,
    Warning,
    Error
}
