namespace San11PVPToolShared.Models;

public record ChatMessage(
    string SenderId,
    string SenderName,
    string Message,
    DateTime Time
);
