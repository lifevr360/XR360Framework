using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

// Finds or creates the session automatically - no buttons, no IP typing.
// On launch: listen on the Wi-Fi for a host for a few seconds. Found one -> join it.
// None -> become the host and announce. If the host disappears, everyone searches again
// and one device takes over. If two hosts ever exist, the smaller one steps down and joins.
[RequireComponent(typeof(NetworkManager))]
public class LanSessionStarter : MonoBehaviour
{
    [Header("Session")]
    [Tooltip("Only devices using the same key join each other")]
    public string sessionKey = "InvestKarnataka";
    public ushort gamePort = 7777;
    public ushort discoveryPort = 47777;

    [Header("Timing")]
    [Tooltip("Seconds to listen for an existing host before becoming host")]
    public float searchTime = 3f;
    [Tooltip("Random extra search time so devices launched together don't all become host")]
    public float searchJitter = 1.5f;
    public float announceInterval = 1f;
    [Tooltip("How quickly a lost host is noticed")]
    public int disconnectTimeoutMS = 5000;

    [Header("Debug (Optional)")]
    public TMP_Text statusLabel;

    private enum State { Idle, Searching, Connecting, Client, Host }

    private const string MessagePrefix = "XR360";

    private NetworkManager networkManager;
    private UnityTransport transport;
    private UdpClient udp;
    private readonly string myId = Guid.NewGuid().ToString("N");

    private State state = State.Idle;
    private float searchUntil;
    private float nextAnnounce;
    private string foundHostIp;
    private string pendingJoinIp;
    private bool restartScheduled;
    private bool isQuitting;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject multicastLock;
#endif

    // ---------------- UNITY ----------------

    private void Start()
    {
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        networkManager = GetComponent<NetworkManager>();
        transport = GetComponent<UnityTransport>();

        if (transport == null)
        {
            SetStatus("No UnityTransport on the NetworkManager - offline");
            enabled = false;
            return;
        }

        // Saves a manual step: NetworkManager refuses to start without a transport assigned
        if (networkManager.NetworkConfig.NetworkTransport == null)
            networkManager.NetworkConfig.NetworkTransport = transport;

        // Must stay on: without scene management, NGO 2.x only spawns registered prefabs and
        // clients discard in-scene NetworkObjects like NetworkSync. Clients that already have
        // the host's scene loaded reuse it instead of reloading.
        networkManager.NetworkConfig.EnableSceneManagement = true;

        // Keeps the shared clock accurate over long sessions (video sync relies on it)
        networkManager.NetworkConfig.EnableTimeResync = true;

        // Fail fast so a dead host is replaced in seconds, not a minute
        transport.DisconnectTimeoutMS = disconnectTimeoutMS;
        transport.ConnectTimeoutMS = 1000;
        transport.MaxConnectAttempts = 5;

        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientStopped += OnNetworkStopped;
        networkManager.OnServerStopped += OnNetworkStopped;

        AcquireMulticastLock();

        if (OpenDiscoverySocket())
            BeginSearch();
        else
            SetStatus("Discovery socket failed - offline");
    }

    private void Update()
    {
        PollDiscovery();

        switch (state)
        {
            case State.Searching:
                if (foundHostIp != null)
                    StartAsClient(foundHostIp);
                else if (Time.time >= searchUntil)
                    StartAsHost();
                break;

            case State.Host:
                if (Time.time >= nextAnnounce)
                {
                    nextAnnounce = Time.time + announceInterval;
                    Announce();
                }
                break;
        }
    }

    private void OnApplicationQuit()
    {
        isQuitting = true;
    }

    private void OnDestroy()
    {
        isQuitting = true;

        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientStopped -= OnNetworkStopped;
            networkManager.OnServerStopped -= OnNetworkStopped;
        }

        if (udp != null)
            udp.Close();

        ReleaseMulticastLock();
    }

    // ---------------- SESSION ----------------

    private void BeginSearch()
    {
        state = State.Searching;
        foundHostIp = null;
        searchUntil = Time.time + searchTime + UnityEngine.Random.Range(0f, searchJitter);
        SetStatus("Searching for session...");
    }

    private void StartAsHost()
    {
        transport.SetConnectionData("127.0.0.1", gamePort, "0.0.0.0");

        if (TryStart(networkManager.StartHost))
        {
            state = State.Host;
            nextAnnounce = 0;
            SetStatus("HOST");
        }
        else
        {
            Debug.LogWarning("LanSession: StartHost failed, searching again.");
            BeginSearch();
        }
    }

    private void StartAsClient(string hostIp)
    {
        transport.SetConnectionData(hostIp, gamePort);

        if (TryStart(networkManager.StartClient))
        {
            state = State.Connecting;
            SetStatus("Joining " + hostIp + "...");
        }
        else
        {
            Debug.LogWarning("LanSession: StartClient failed, searching again.");
            BeginSearch();
        }
    }

    // StartHost/StartClient can throw on bad config; treat that as a failed start so we
    // retry after the next search instead of every frame
    private bool TryStart(Func<bool> start)
    {
        try
        {
            return start();
        }
        catch (Exception e)
        {
            Debug.LogError("LanSession: start failed: " + e.Message);
            return false;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (state == State.Connecting && clientId == networkManager.LocalClientId)
        {
            state = State.Client;
            SetStatus("CLIENT");
        }
    }

    // Called when we lose the host, fail to connect, or step down as host
    private void OnNetworkStopped(bool _)
    {
        // A host fires both OnClientStopped and OnServerStopped; restart once.
        // Never restart while the app (or Play mode) is closing.
        if (restartScheduled || isQuitting)
            return;

        restartScheduled = true;
        state = State.Idle;
        StartCoroutine(RestartWhenShutdownDone());
    }

    private IEnumerator RestartWhenShutdownDone()
    {
        // Wait at least one frame so the second stop event of a host is still ignored
        yield return null;

        while (networkManager.ShutdownInProgress || networkManager.IsListening)
            yield return null;

        restartScheduled = false;

        if (pendingJoinIp != null)
        {
            string ip = pendingJoinIp;
            pendingJoinIp = null;
            StartAsClient(ip);
        }
        else
        {
            BeginSearch();
        }
    }

    // ---------------- DISCOVERY ----------------

    private bool OpenDiscoverySocket()
    {
        try
        {
            udp = new UdpClient();
            udp.ExclusiveAddressUse = false;
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, discoveryPort));
            udp.EnableBroadcast = true;
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("LanSession: could not open discovery port " + discoveryPort + ": " + e.Message);
            return false;
        }
    }

    // Message: XR360|<sessionKey>|<hostId>|<connectedClients>
    private void Announce()
    {
        string message = MessagePrefix + "|" + sessionKey + "|" + myId + "|" + ConnectedClientCount();
        byte[] data = Encoding.UTF8.GetBytes(message);

        try
        {
            udp.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, discoveryPort));
        }
        catch (Exception e)
        {
            Debug.LogWarning("LanSession: announce failed: " + e.Message);
        }
    }

    private void PollDiscovery()
    {
        if (udp == null)
            return;

        try
        {
            while (udp.Available > 0)
            {
                IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                string[] parts = Encoding.UTF8.GetString(udp.Receive(ref sender)).Split('|');

                if (parts.Length < 4 || parts[0] != MessagePrefix || parts[1] != sessionKey || parts[2] == myId)
                    continue;

                int otherClients;
                int.TryParse(parts[3], out otherClients);
                OnHostAnnounced(sender.Address.ToString(), parts[2], otherClients);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("LanSession: discovery receive failed: " + e.Message);
        }
    }

    private void OnHostAnnounced(string ip, string hostId, int hostClients)
    {
        if (state == State.Searching)
        {
            foundHostIp = ip;
            return;
        }

        // Two hosts (e.g. launched at the same moment, or a host waking up from sleep):
        // the one with fewer clients steps down; on a tie the lower ID wins
        if (state == State.Host && pendingJoinIp == null)
        {
            int mine = ConnectedClientCount();
            bool otherWins = hostClients > mine || (hostClients == mine && string.CompareOrdinal(hostId, myId) < 0);

            if (otherWins)
            {
                Debug.Log("LanSession: another host found at " + ip + ", joining it.");
                pendingJoinIp = ip;
                networkManager.Shutdown();
            }
        }
    }

    private int ConnectedClientCount()
    {
        return networkManager.IsServer ? networkManager.ConnectedClientsIds.Count - 1 : 0;
    }

    // ---------------- ANDROID ----------------

    // Android drops incoming broadcast packets unless a multicast lock is held
    // (needs the CHANGE_WIFI_MULTICAST_STATE permission in the manifest)
    private void AcquireMulticastLock()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var wifi = activity.Call<AndroidJavaObject>("getSystemService", "wifi"))
            {
                multicastLock = wifi.Call<AndroidJavaObject>("createMulticastLock", "xr360-discovery");
                multicastLock.Call("acquire");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("LanSession: multicast lock failed (check manifest permission): " + e.Message);
        }
#endif
    }

    private void ReleaseMulticastLock()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (multicastLock != null)
        {
            multicastLock.Call("release");
            multicastLock.Dispose();
            multicastLock = null;
        }
#endif
    }

    private void SetStatus(string text)
    {
        Debug.Log("LanSession: " + text);
        if (statusLabel != null)
            statusLabel.text = text;
    }
}
