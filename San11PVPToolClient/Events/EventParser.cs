using System;
using System.Collections.Generic;
using System.Text.Json;
using San11PVPToolShared.Events;
using San11PVPToolShared.Models;

namespace San11PVPToolClient.Events;

public static class EventParser
{
    private static readonly Dictionary<string, Action<JsonElement, ClientEvents>> handlers
        = new()
        {
            [EventTypes.RoomInfoUpdated] = (data, events) =>
            {
                var eventData = JsonSerializer.Deserialize<RoomInfoUpdatedEventData>(data.GetRawText());
                events.OnRoomInfoUpdated(eventData);
            },
            [EventTypes.PlayerKicked] = (data, events) =>
            {
                var eventData = JsonSerializer.Deserialize<PlayerKickedEventData>(data.GetRawText());
                events.OnPlayerKicked(eventData);
            },
            [EventTypes.RoomClosed] = (data, events) =>
            {
                events.OnRoomClosed();
            },
            [EventTypes.SaveUploaded] = (data, events) =>
            {
                var eventData = JsonSerializer.Deserialize<SaveUploadedEventData>(data.GetRawText());
                events.OnSaveUploaded(eventData);
            },
            [EventTypes.SystemMessage] = (data, events) =>
            {
                var message = JsonSerializer.Deserialize<SystemMessage>(data.GetRawText());
                events.OnSystemMessageReceived(message);
            },
            [EventTypes.ChatMessage] = (data, events) =>
            {
                var message = JsonSerializer.Deserialize<ChatMessage>(data.GetRawText());
                events.OnChatMessageReceived(message);
            }
            // 可以继续注册其他事件
        };

    public static void Parse(string json, ClientEvents events)
    {
        var socketEvent = JsonSerializer.Deserialize<SocketEvent>(json);
        if (socketEvent != null && handlers.TryGetValue(socketEvent.Event, out var handler))
        {
            // 直接把 JsonElement 传给对应处理器
            handler((JsonElement)socketEvent.Data, events);
        }
    }
}
