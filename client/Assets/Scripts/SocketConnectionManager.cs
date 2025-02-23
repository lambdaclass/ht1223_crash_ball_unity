using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Protobuf;
using Google.Protobuf.Collections;
using NativeWebSocket;
using UnityEngine;
using Communication.Protobuf;

public class SocketConnectionManager : MonoBehaviour
{
    public List<GameObject> players;

    public Dictionary<int, GameObject> projectiles = new Dictionary<int, GameObject>();
    public static Dictionary<int, GameObject> projectilesStatic;

    [Tooltip("Session ID to connect to. If empty, a new session will be created")]
    public string sessionId = "";

    [Tooltip("IP to connect to. If empty, localhost will be used")]
    public string serverIp = "localhost";
    public static SocketConnectionManager Instance;
    public List<OldPlayer> gamePlayers;
    public OldGameEvent gameEvent;
    public List<OldProjectile> gameProjectiles;
    public Dictionary<ulong, string> selectedCharacters;
    public ulong playerId;
    public uint currentPing;
    public uint serverTickRate_ms;
    public string serverHash;
    public (OldPlayer, ulong) winnerPlayer = (null, 0);

    public List<OldPlayer> winners = new List<OldPlayer>();
    public Dictionary<ulong, string> playersIdName = new Dictionary<ulong, string>();

    public ClientPrediction clientPrediction = new ClientPrediction();

    public List<OldGameEvent> gameEvents = new List<OldGameEvent>();

    public EventsBuffer eventsBuffer = new EventsBuffer { deltaInterpolationTime = 100 };
    public bool allSelected = false;

    public float playableRadius;
    public OldPosition shrinkingCenter;

    public List<OldPlayer> alivePlayers = new List<OldPlayer>();

    public bool cinematicDone;

    public bool connected = false;

    public Game.GameState gameState;

    WebSocket ws;

    private string clientId;
    private bool reconnect;

    public class Session
    {
        public string sessionId { get; set; }
    }

    // public void Awake()
    // {
    //     Init();
    // }

    public void Init()
    {
        StartCoroutine(WaitForLobbyConnection());
        if (Instance != null)
        {
            if (this.ws != null)
            {
                this.ws.Close();
            }
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            this.sessionId = LobbyConnection.Instance.GameSession;
            this.serverIp = LobbyConnection.Instance.serverIp;
            this.serverTickRate_ms = LobbyConnection.Instance.serverTickRate_ms;
            this.serverHash = LobbyConnection.Instance.serverHash;
            this.clientId = LobbyConnection.Instance.clientId;
            this.reconnect = LobbyConnection.Instance.reconnect;
            this.playersIdName = LobbyConnection.Instance.playersIdName;

            projectilesStatic = this.projectiles;
            DontDestroyOnLoad(gameObject);

            if (this.reconnect)
            {
                this.selectedCharacters = LobbyConnection.Instance.reconnectPlayers;
                this.allSelected = !LobbyConnection.Instance.reconnectToCharacterSelection;
                this.cinematicDone = true;
            }
        }
    }

    private IEnumerator WaitForLobbyConnection()
    {
        yield return new WaitUntil(() => LobbyConnection.Instance != null);
    }

    void Start()
    {
        Init();
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if (ws != null)
        {
            ws.DispatchMessageQueue();
        }
#endif

        StartCoroutine(IsGameCreated());
    }

    private IEnumerator IsGameCreated()
    {
        yield return new WaitUntil(
            () =>
                LobbyConnection.Instance.GameSession != ""
                && LobbyConnection.Instance.GameSession != null
        );
        this.sessionId = LobbyConnection.Instance.GameSession;
        this.serverIp = LobbyConnection.Instance.serverIp;
        this.serverTickRate_ms = LobbyConnection.Instance.serverTickRate_ms;
        this.serverHash = LobbyConnection.Instance.serverHash;
        this.clientId = LobbyConnection.Instance.clientId;
        this.reconnect = LobbyConnection.Instance.reconnect;

        if (!connected && this.sessionId != "")
        {
            connected = true;
            ConnectToSession(this.sessionId);
        }
    }

    private void ConnectToSession(string sessionId)
    {
        string url = makeWebsocketUrl(
            "/play/"
                + sessionId
                + "/"
                + this.clientId
                + "/"
                + LobbyConnection.Instance.selectedCharacterName
        );
        print(url);
        Dictionary<string, string> headers = new Dictionary<string, string>();
        headers.Add("dark-worlds-client-hash", GitInfo.GetGitHash());
        ws = new WebSocket(url, headers);
        ws.OnMessage += OnWebSocketMessage;
        ws.OnClose += onWebsocketClose;
        ws.OnError += (e) =>
        {
            Debug.Log("Received error: " + e);
        };
        ws.Connect();
    }

    private void OnWebSocketMessage(byte[] data)
    {
        try
        {
            TransitionGameEvent gameEvent = TransitionGameEvent.Parser.ParseFrom(data);

            // TODO: Fix missing NewGameEvent, current missing are
            //      - PING_UPDATE
            //      - PLAYER_JOINED
            if (gameEvent.OldGameEvent.Type != GameEventType.PingUpdate
                && gameEvent.OldGameEvent.Type != GameEventType.PlayerJoined) {
                try {
                    switch (gameEvent.NewGameEvent.EventCase) {
                        case GameEvent.EventOneofCase.GameState:
                            gameState = new Game.GameState(gameEvent.NewGameEvent.GameState);
                            break;
                        default:
                            print("Unexpected message: " + gameEvent.NewGameEvent.EventCase);
                            break;
                    }
                } catch (Exception e) {
                    Debug.Log(gameEvent);
                    Debug.Log(e);
                    throw e;
                }
            }

            switch (gameEvent.OldGameEvent.Type)
            {
                case GameEventType.StateUpdate:
                    this.playableRadius = gameEvent.OldGameEvent.PlayableRadius;
                    this.shrinkingCenter = gameEvent.OldGameEvent.ShrinkingCenter;
                    eventsBuffer.AddEvent(gameEvent.OldGameEvent);
                    this.gamePlayers = gameEvent.OldGameEvent.Players.ToList();
                    this.gameProjectiles = gameEvent.OldGameEvent.Projectiles.ToList();
                    alivePlayers = gameEvent.OldGameEvent.Players
                        .ToList()
                        .FindAll(el => el.Health > 0);
                    KillFeedManager.instance.putEvents(gameEvent.OldGameEvent.Killfeed.ToList());
                    break;
                case GameEventType.PingUpdate:
                    currentPing = (uint)gameEvent.OldGameEvent.Latency;
                    break;
                case GameEventType.GameFinished:
                    winnerPlayer.Item1 = gameEvent.OldGameEvent.WinnerPlayer;
                    winnerPlayer.Item2 = gameEvent.OldGameEvent.WinnerPlayer.KillCount;
                    this.gamePlayers = gameEvent.OldGameEvent.Players.ToList();
                    break;
                case GameEventType.PlayerJoined:
                    this.playerId = gameEvent.OldGameEvent.PlayerJoinedId;
                    break;
                case GameEventType.GameStarted:
                    this.playableRadius = gameEvent.OldGameEvent.PlayableRadius;
                    this.shrinkingCenter = gameEvent.OldGameEvent.ShrinkingCenter;
                    eventsBuffer.AddEvent(gameEvent.OldGameEvent);
                    this.gamePlayers = gameEvent.OldGameEvent.Players.ToList();
                    this.gameProjectiles = gameEvent.OldGameEvent.Projectiles.ToList();
                    LobbyConnection.Instance.gameStarted = true;
                    break;
                default:
                    print("Message received is: " + gameEvent.OldGameEvent.Type);
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.Log("InvalidProtocolBufferException: " + e);
        }
    }

    private void onWebsocketClose(WebSocketCloseCode closeCode)
    {
        Debug.Log("closeCode:" + closeCode);
        if (closeCode != WebSocketCloseCode.Normal)
        {
            LobbyConnection.Instance.errorConnection = true;
            this.Init();
            LobbyConnection.Instance.Init();
        }
    }

    public Dictionary<ulong, string> fromMapFieldToDictionary(MapField<ulong, string> dict)
    {
        Dictionary<ulong, string> result = new Dictionary<ulong, string>();

        foreach (KeyValuePair<ulong, string> element in dict)
        {
            result.Add(element.Key, element.Value);
        }

        return result;
    }

    public static OldPlayer GetPlayer(ulong id, List<OldPlayer> playerList)
    {
        return playerList.Find(el => el.Id == id);
    }

    public void SendAction(ClientAction action)
    {
        using (var stream = new MemoryStream())
        {
            action.WriteTo(stream);
            var msg = stream.ToArray();
            ws.Send(msg);
        }
    }

    public void SendGameAction<T>(IMessage<T> action)
        where T : IMessage<T>
    {
        using (var stream = new MemoryStream())
        {
            action.WriteTo(stream);
            var msg = stream.ToArray();
            ws.Send(msg);
        }
    }

    private string makeUrl(string path)
    {
        var useProxy = LobbyConnection.Instance.serverSettings.RunnerConfig.UseProxy;
        int port;

        if (useProxy == "true")
        {
            port = 5000;
        }
        else
        {
            port = 4000;
        }

        if (serverIp.Contains("localhost"))
        {
            return "http://" + serverIp + ":" + port + path;
        }
        else if (serverIp.Contains("10.150.20.186"))
        {
            return "http://" + serverIp + ":" + port + path;
        }
        // Load test server
        else if (serverIp.Contains("168.119.71.104"))
        {
            return "http://" + serverIp + ":" + port + path;
        }
        // Load test runner server
        else if (serverIp.Contains("176.9.26.172"))
        {
            return "http://" + serverIp + ":" + port + path;
        }
        else
        {
            return "https://" + serverIp + path;
        }
    }

    private string makeWebsocketUrl(string path)
    {
        // var useProxy = LobbyConnection.Instance.serverSettings.RunnerConfig.UseProxy;

        int port = 4000;

        // if (useProxy == "true")
        // {
        //     port = 5000;
        // }
        // else
        // {
        //     port = 4000;
        // }

        if (serverIp.Contains("localhost"))
        {
            return "ws://" + serverIp + ":" + port + path;
        }
        else if (serverIp.Contains("10.150.20.186"))
        {
            return "ws://" + serverIp + ":" + port + path;
        }
        // Load test server
        else if (serverIp.Contains("168.119.71.104"))
        {
            return "ws://" + serverIp + ":" + port + path;
        }
        // Load test runner server
        else if (serverIp.Contains("176.9.26.172"))
        {
            return "ws://" + serverIp + ":" + port + path;
        }
        else
        {
            return "wss://" + serverIp + path;
        }
    }

    public void closeConnection()
    {
        ws.Close();
    }

    public bool isConnectionOpen()
    {
        return ws.State == NativeWebSocket.WebSocketState.Open;
    }

    public bool GameHasEnded()
    {
        return winnerPlayer.Item1 != null;
    }

    public bool PlayerIsWinner(ulong playerId)
    {
        return GameHasEnded() && winnerPlayer.Item1.Id == playerId;
    }
}
