using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class VideoManager : MonoBehaviour
{
    #region Public Variables / Structures
    public enum Language { Hindi, English }
    // DeviceFolder: plays <index><ext> from the app's OBB folder on Android (/sdcard/Android/obb/<package name>/)
    public enum VideoSourceMode { URL, LocalClips, DeviceFolder }

    [Header("References")]
    public VideoPlayer videoPlayer;
    public VideoPlayer firstvideoPlayer;
    public AudioSource voiceOverAudioSource;
    public Mouselook mouseLook; // Kept exactly as your original script
    public Transform fps;

    [Header("Video Source Settings")]
    public VideoSourceMode videoSourceMode = VideoSourceMode.URL; // Toggle between URL and Local

    [Header("URL Settings (Used if Mode is URL, and in the Editor for DeviceFolder)")]
    [Tooltip("Ensure this ends with a forward slash /")]
    public string rootURL = "https://moxyblr1.s3.ap-south-1.amazonaws.com/Nestle/";
    public string fileExtension = ".mp4";

    [Header("Local Video Clips (Used if Mode is LocalClips)")]
    [Tooltip("Drag and drop your baked video clips here. Slot 0 matches index 0, etc.")]
    public List<VideoClip> localVideoClips;

    [Header("Localized Audio Clips")]
    [Tooltip("Place Hindi audio clips here. Slot 0 matches Video 0, Slot 1 matches Video 1, etc.")]
    public List<AudioClip> hindiAudioClips;
    [Tooltip("Place English audio clips here. Slot 0 matches Video 0, Slot 1 matches Video 1, etc.")]
    public List<AudioClip> englishAudioClips;

    [Header("Network Sync")]
    [Tooltip("Re-seek when this device drifts further than this (seconds) from the shared timeline")]
    public bool correctDrift = true;
    public float driftTolerance = 0.5f;
    public float driftCheckInterval = 2f;

    // Set by NetworkSync. networkClock returns the shared session time; nextStartTime is
    // consumed by the next PlayVideo call so every device starts that video at the same moment.
    [System.NonSerialized] public System.Func<double> networkClock;
    [System.NonSerialized] public double nextStartTime = -1;

    // Raised when a video is actually started (index, shared start time or -1 when unsynced)
    public event System.Action<int, double> onVideoScheduled;

    public int CurrentVideoIndex { get { return currentVideoIndex; } }
    public int LastRequestedIndex { get; private set; } = -1;
    public double LastRequestedStartTime { get; private set; } = -1;
    #endregion

    #region Private Variables
    private Language selectedLanguage = Language.English; // Default language
    private int currentVideoIndex = -1; // Track the currently playing video
    private double currentStartTime = -1; // Shared start time of the current video
    private float nextDriftCheck;
    private Quaternion defaultRotation;
    #endregion

    #region Unity Methods
    private void Start()
    {
        // Cache camera rotation settings (From your original script)
        if (fps != null)
            defaultRotation = fps.localRotation;

        // Apply WebGL Best Practice settings (From your original script)
        if (videoPlayer != null)
        {
            videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
            videoPlayer.playOnAwake = false;
            videoPlayer.waitForFirstFrame = true;
        }

        PlayVideo(0); // Play the first video (0) on start
    }

    private void Update()
    {
        if (!correctDrift || networkClock == null || currentStartTime < 0 || !videoPlayer.isPlaying)
            return;

        if (Time.time < nextDriftCheck)
            return;
        nextDriftCheck = Time.time + driftCheckInterval;

        double expected = GetPlaybackOffset(networkClock() - currentStartTime);
        if (expected >= 0 && System.Math.Abs(videoPlayer.time - expected) > driftTolerance)
        {
            Debug.Log("Video drifted " + (videoPlayer.time - expected).ToString("F2") + "s, re-syncing.");
            videoPlayer.time = expected;
        }
    }
    #endregion

    #region Custom Public Methods
    public void SetLanguage(int value)
    {
        selectedLanguage = (Language)value;
        Debug.Log("Language set to: " + selectedLanguage);
    }

    public void PlayVideo(int videoIndex)
    {
        double startTime = nextStartTime;
        nextStartTime = -1;
        PlayVideoAt(videoIndex, startTime, false);
    }

    // startTime: shared session time to start at (-1 = start now). force: restart even if already playing.
    public void PlayVideoAt(int videoIndex, double startTime, bool force)
    {
        // Check if the requested video is already playing
        if (!force && videoIndex == currentVideoIndex && videoPlayer.isPlaying)
        {
            Debug.Log("Video is already playing. Ignoring request.");
            return;
        }

        // Clean up firstvideoPlayer if active and playing (From your original script)
        if (firstvideoPlayer != null && firstvideoPlayer.isPlaying)
        {
            firstvideoPlayer.Stop();
            if (firstvideoPlayer.gameObject != this.gameObject)
                firstvideoPlayer.gameObject.SetActive(false);
        }

        LastRequestedIndex = videoIndex;
        LastRequestedStartTime = startTime;

        Debug.Log("Stopped coroutines.");
        StopAllCoroutines();
        StopVideoAndAudio();
        currentStartTime = -1;
        StartCoroutine(StartPlayingVideoRoutine(videoIndex, startTime));
    }

    // Used by the host when the session starts, to anchor the video that is already playing
    public void SetSyncAnchor(double startTime)
    {
        currentStartTime = startTime;
    }

    public void StopAllVideos()
    {
        StopAllCoroutines();
        StopVideoAndAudio();
        if (firstvideoPlayer != null) firstvideoPlayer.Stop();
    }
    #endregion

    #region Private Helper Methods & Coroutines
    private string GetVideoURL(int index)
    {
        // Direct URL generation using the index (From your original script)
        return rootURL + index + fileExtension;
    }

    private string GetDeviceFolderURL(int index)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return "file:///storage/emulated/0/Android/obb/" + Application.identifier + "/" + index + fileExtension;
#else
        return GetVideoURL(index);
#endif
    }

    // Converts "seconds since the shared start" into a position in the clip (-1 if past the end)
    private double GetPlaybackOffset(double elapsed)
    {
        if (elapsed <= 0)
            return 0;

        double length = videoPlayer.length;
        if (length <= 0)
            return elapsed;

        if (videoPlayer.isLooping)
            return elapsed % length;

        return elapsed < length ? elapsed : -1;
    }

    private IEnumerator StartPlayingVideoRoutine(int index, double startTime)
    {
        // Stop current playback and reset frame to prevent the "rushing" catch-up effect
        videoPlayer.Stop();
        videoPlayer.frame = 0;

        // Check which mode is selected to assign the correct source
        if (videoSourceMode == VideoSourceMode.URL || videoSourceMode == VideoSourceMode.DeviceFolder)
        {
            videoPlayer.source = VideoSource.Url;
            string videoURL = videoSourceMode == VideoSourceMode.URL ? GetVideoURL(index) : GetDeviceFolderURL(index);

            // WebGL-friendly performance workaround: toggle GameObject visibility when setting URL
            if (videoPlayer.gameObject != this.gameObject)
            {
                videoPlayer.gameObject.SetActive(false);
                yield return null;
                videoPlayer.url = videoURL;
                videoPlayer.gameObject.SetActive(true);
            }
            else
            {
                videoPlayer.url = videoURL;
            }
        }
        else if (videoSourceMode == VideoSourceMode.LocalClips)
        {
            videoPlayer.source = VideoSource.VideoClip;

            if (localVideoClips != null && index < localVideoClips.Count && localVideoClips[index] != null)
            {
                videoPlayer.clip = localVideoClips[index];
            }
            else
            {
                Debug.LogError("Local video clip is missing or index is out of bounds for index: " + index);
                yield break; // Exit early if no video is found
            }
        }

        yield return null;

        videoPlayer.Prepare();

        // Safe loop timeout logic
        float timeout = 0;
        while (!videoPlayer.isPrepared && timeout < 10f)
        {
            timeout += Time.deltaTime;
            yield return null;
        }

        if (videoPlayer.isPrepared)
        {
            videoPlayer.frame = 0; // Double check reset before play

            // Synced start: wait for the shared start moment, or seek forward if we are late
            bool synced = startTime >= 0 && networkClock != null;
            if (synced)
            {
                // networkClock is cleared if the session drops while we wait
                float waited = 0;
                while (networkClock != null && networkClock() < startTime && waited < 5f)
                {
                    waited += Time.deltaTime;
                    yield return null;
                }

                synced = networkClock != null;
                if (synced)
                {
                    double offset = GetPlaybackOffset(networkClock() - startTime);
                    if (offset < 0)
                        videoPlayer.time = System.Math.Max(0, videoPlayer.length - 0.1); // already finished on the others
                    else if (offset > 0.1)
                        videoPlayer.time = offset;
                }
            }

            // Resolve the audio clip by index dynamically from the language lists
            AudioClip selectedClip = null;
            if (selectedLanguage == Language.Hindi && hindiAudioClips != null && index < hindiAudioClips.Count)
            {
                selectedClip = hindiAudioClips[index];
            }
            else if (selectedLanguage == Language.English && englishAudioClips != null && index < englishAudioClips.Count)
            {
                selectedClip = englishAudioClips[index];
            }

            if (selectedClip == null)
            {
                Debug.Log("No localized audio clip found for index " + index + ". Playing video with embedded audio.");
                if (voiceOverAudioSource != null) voiceOverAudioSource.Stop();
            }
            else
            {
                Debug.Log("Playing video with external localized audio clip.");
                PlayVoiceOver(selectedClip);
                if (synced && voiceOverAudioSource != null && videoPlayer.time > 0 && videoPlayer.time < selectedClip.length)
                    voiceOverAudioSource.time = (float)videoPlayer.time;
            }

            videoPlayer.Play();
            currentVideoIndex = index;
            currentStartTime = synced ? startTime : -1;
            nextDriftCheck = Time.time + driftCheckInterval;

            if (onVideoScheduled != null)
                onVideoScheduled(index, currentStartTime);

            if (videoSourceMode == VideoSourceMode.LocalClips)
                Debug.Log("Playing baked local video: " + videoPlayer.clip.name);
            else
                Debug.Log("Playing video from URL: " + videoPlayer.url);

            // Reset player orientation after load finish (From your original script)
            if (mouseLook != null)
                mouseLook.ResetRotation(defaultRotation);
        }
        else
        {
            Debug.LogError("Video failed to prepare. Check CORS, URL, or local file.");
        }
    }

    private void PlayVoiceOver(AudioClip clip)
    {
        if (clip == null) return;

        if (voiceOverAudioSource != null)
        {
            voiceOverAudioSource.Stop();
            voiceOverAudioSource.clip = clip;
            voiceOverAudioSource.Play();
            Debug.Log("Playing internal audio.");
        }
    }

    private void StopVideoAndAudio()
    {
        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            videoPlayer.Stop();
            Debug.Log("Stopped current video.");
        }

        if (voiceOverAudioSource != null && voiceOverAudioSource.isPlaying)
        {
            voiceOverAudioSource.Stop();
        }
    }
    #endregion
}
