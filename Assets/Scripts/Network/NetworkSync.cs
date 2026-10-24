using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// Keeps NodeFramework + VideoManager identical on every connected device.
// Any device can click: the click goes to the host, the host applies it and
// forwards it to everyone else, so all devices run the same actions in the same order.
[RequireComponent(typeof(NetworkObject))]
public class NetworkSync : NetworkBehaviour
{
    [Header("References")]
    public NodeFramework nodeFramework;
    public VideoManager videoManager;

    [Header("Video Sync")]
    [Tooltip("How far ahead (seconds) a video is scheduled so every device has time to prepare it")]
    public float videoStartLead = 1.0f;

    // Session state kept by the host, read by devices that join late
    private NetworkVariable<FixedString64Bytes> menuId = new NetworkVariable<FixedString64Bytes>();
    private NetworkVariable<int> videoIndex = new NetworkVariable<int>(-1);
    private NetworkVariable<double> videoStartTime = new NetworkVariable<double>(-1);

    // ---------------- SPAWN ----------------

    public override void OnNetworkSpawn()
    {
        nodeFramework.onNodeRequested = RequestNodeClick;
        nodeFramework.onBackRequested = RequestBack;
        videoManager.networkClock = Clock;

        if (IsServer)
        {
            videoManager.onVideoScheduled += OnHostVideoScheduled;
            PublishHostState();
        }
        else
        {
            ApplySessionState();
        }
    }

    public override void OnNetworkDespawn()
    {
        // Back to offline mode: clicks apply locally until we reconnect
        nodeFramework.onNodeRequested = null;
        nodeFramework.onBackRequested = null;
        videoManager.networkClock = null;
        videoManager.onVideoScheduled -= OnHostVideoScheduled;
    }

    private double Clock()
    {
        return NetworkManager.ServerTime.Time;
    }

    // ---------------- REQUESTS (any device -> host) ----------------

    private void RequestNodeClick(string id)
    {
        RequestNodeClickRpc(id);
    }

    private void RequestBack()
    {
        RequestBackRpc();
    }

    [Rpc(SendTo.Server)]
    private void RequestNodeClickRpc(string id)
    {
        double start = Clock() + videoStartLead;

        ApplyClick(id, start);

        menuId.Value = nodeFramework.CurrentMenuId;

        // This click started a video: store it so late joiners land in the same video
        if (videoManager.LastRequestedStartTime == start)
        {
            videoIndex.Value = videoManager.LastRequestedIndex;
            videoStartTime.Value = start;
        }

        ApplyNodeClickRpc(id, nodeFramework.CurrentMenuId, start);
    }

    [Rpc(SendTo.Server)]
    private void RequestBackRpc()
    {
        nodeFramework.ApplyGoBack();
        menuId.Value = nodeFramework.CurrentMenuId;
        ApplyMenuRpc(nodeFramework.CurrentMenuId);
    }

    // ---------------- BROADCASTS (host -> everyone else) ----------------

    [Rpc(SendTo.NotServer)]
    private void ApplyNodeClickRpc(string id, string resultMenuId, double start)
    {
        ApplyClick(id, start);

        // Self-heal: make sure we end on exactly the same menu as the host
        nodeFramework.RestoreMenu(resultMenuId);
    }

    [Rpc(SendTo.NotServer)]
    private void ApplyMenuRpc(string resultMenuId)
    {
        nodeFramework.RestoreMenu(resultMenuId);
    }

    // ---------------- HELPERS ----------------

    private void ApplyClick(string id, double start)
    {
        // Any PlayVideo fired by this node's onSelected starts at the shared time
        videoManager.nextStartTime = start;
        nodeFramework.ApplyNodeClick(id);
        videoManager.nextStartTime = -1;
    }

    // Host: make whatever this device is currently showing the session state
    private void PublishHostState()
    {
        menuId.Value = nodeFramework.CurrentMenuId;

        if (videoManager.CurrentVideoIndex >= 0 && videoManager.videoPlayer.isPlaying)
            AnchorPlayingVideo(videoManager.CurrentVideoIndex);
    }

    // Host: a video started without a shared time (e.g. the intro on Start), anchor it now
    private void OnHostVideoScheduled(int index, double start)
    {
        if (start < 0)
            AnchorPlayingVideo(index);
    }

    private void AnchorPlayingVideo(int index)
    {
        double start = Clock() - videoManager.videoPlayer.time;
        videoManager.SetSyncAnchor(start);
        videoIndex.Value = index;
        videoStartTime.Value = start;
    }

    // Late joiner / reconnect: rebuild the host's menu and jump into its video
    private void ApplySessionState()
    {
        nodeFramework.RestoreMenu(menuId.Value.ToString());

        int index = videoIndex.Value;
        double start = videoStartTime.Value;
        if (index < 0 || start < 0)
            return;

        if (videoManager.CurrentVideoIndex == index && videoManager.videoPlayer.isPlaying)
            videoManager.SetSyncAnchor(start); // same video already playing, drift correction lines it up
        else
            videoManager.PlayVideoAt(index, start, true);
    }
}
