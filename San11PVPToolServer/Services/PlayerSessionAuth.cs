using San11PVPToolServer.Models;

namespace San11PVPToolServer.Services;

public static class PlayerSessionAuth
{
    private const string BearerPrefix = "Bearer ";

    public static Player? Authenticate(HttpRequest request, string roomId)
    {
        var authorization = request.Headers.Authorization.ToString();
        if (!authorization.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var sessionToken = authorization[BearerPrefix.Length..].Trim();
        if (!Guid.TryParse(sessionToken, out _))
            return null;

        return RoomManager.GetPlayerBySessionToken(roomId, sessionToken);
    }
}
