using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using San11PVPToolShared.Models;

namespace San11PVPToolClient.Events;

public class ClientEvents
{
    private readonly BehaviorSubject<(PlayerInfo?, RoomInfo?)> _loginStateChanged = new((null, null));
    public IObservable<(PlayerInfo?, RoomInfo?)> LoginStateChanged => _loginStateChanged.AsObservable();

    private readonly BehaviorSubject<bool?> _socketConnectionChanged = new(null);
    public IObservable<bool?> SocketConnectionChanged => _socketConnectionChanged.AsObservable();

    private readonly Subject<PlayerKickedEventData> _playerKicked = new();
    public IObservable<PlayerKickedEventData> PlayerKicked => _playerKicked.AsObservable();

    private readonly Subject<Unit> _roomClosed = new();
    public IObservable<Unit> RoomClosed => _roomClosed.AsObservable();

    private readonly BehaviorSubject<RoomInfoUpdatedEventData> _roomInfoUpdated = new(null);
    public IObservable<RoomInfoUpdatedEventData> RoomInfoUpdated => _roomInfoUpdated.AsObservable();

    private readonly Subject<SaveUploadedEventData> _saveUploaded = new();
    public IObservable<SaveUploadedEventData> SaveUploaded => _saveUploaded.AsObservable();

    private readonly Subject<SystemMessage> _systemMessageReceived = new();
    public IObservable<SystemMessage> SystemMessageReceived => _systemMessageReceived.AsObservable();

    private readonly Subject<ChatMessage> _chatMessageReceived = new();
    public IObservable<ChatMessage> ChatMessageReceived => _chatMessageReceived.AsObservable();


    internal void OnLoginStateChanged(PlayerInfo? userInfo, RoomInfo? roomInfo) =>
        _loginStateChanged.OnNext((userInfo, roomInfo));

    internal void OnSocketConnected() => _socketConnectionChanged.OnNext(true);

    internal void OnSocketDisconnected() => _socketConnectionChanged.OnNext(false);

    internal void OnPlayerKicked(PlayerKickedEventData ed) => _playerKicked.OnNext(ed);

    internal void OnRoomClosed() => _roomClosed.OnNext(Unit.Default);

    internal void OnRoomInfoUpdated(RoomInfoUpdatedEventData ed) => _roomInfoUpdated.OnNext(ed);

    internal void OnSaveUploaded(SaveUploadedEventData ed) => _saveUploaded.OnNext(ed);

    internal void OnSystemMessageReceived(SystemMessage msg) => _systemMessageReceived.OnNext(msg);

    internal void OnChatMessageReceived(ChatMessage msg) => _chatMessageReceived.OnNext(msg);
}
