using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class VideoManager : MonoBehaviour
{
    #region Public Variables / Structures
    public enum Language { Hindi, English }
    public enum VideoSourceMode { URL, LocalClips }

    [Header("References")]
    public VideoPlayer videoPlayer;
    public VideoPlayer firstvideoPlayer;
    public AudioSource voiceOverAudioSource;
    public Mouselook mouseLook;
    public Transform fps;

    [Header("Video Source Settings")]
    public VideoSourceMode videoSourceMode = VideoSourceMode.URL;

    [Header("URL Settings (Used if Mode is URL)")]
    [Tooltip("Ensure this ends with a forward slash /")]
    public string rootURL = "https://d2wnafbesrovet.cloudfront.net/videos/"; 
    public string fileExtension = ".mp4";

    [Header("Local Video Clips (Used if Mode is LocalClips)")]
    public List<VideoClip> localVideoClips;

    [Header("Localized Audio Clips")]
    public List<AudioClip> hindiAudioClips;
    public List<AudioClip> englishAudioClips;

    [Header("Silent Videos (No Embedded Sound)")]
    [Tooltip("Add video index numbers here that have NO audio track (e.g., 1, 106) so Unity bypasses audio decoding.")]
    public List<int> silentVideoIndices = new List<int> { 1, 106 };
    #endregion

    #region Private Variables
    private Language selectedLanguage = Language.English;
    private int currentVideoIndex = -1;
    private Quaternion defaultRotation;
    #endregion

    #region Unity Methods
    private void Start()
    {
        if (fps != null)
            defaultRotation = fps.localRotation;

        ConfigureVideoPlayer(videoPlayer);
        ConfigureVideoPlayer(firstvideoPlayer);

        PlayVideo(0);
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
        if (videoIndex == currentVideoIndex && videoPlayer != null && videoPlayer.isPlaying)
        {
            Debug.Log("Video " + videoIndex + " is already playing. Ignoring request.");
            return;
        }

        if (firstvideoPlayer != null && firstvideoPlayer.isPlaying)
        {
            firstvideoPlayer.Stop();
            if (firstvideoPlayer.gameObject != this.gameObject)
                firstvideoPlayer.gameObject.SetActive(false);
        }

        StopAllCoroutines();
        StopVideoAndAudio();
        StartCoroutine(StartPlayingVideoRoutine(videoIndex));
    }

    public void StopAllVideos()
    {
        StopAllCoroutines();
        StopVideoAndAudio();
        if (firstvideoPlayer != null) firstvideoPlayer.Stop();
    }
    #endregion

    #region Private Helper Methods & Coroutines
    private void ConfigureVideoPlayer(VideoPlayer vp)
    {
        if (vp == null) return;

        vp.playOnAwake = false;
        vp.waitForFirstFrame = false;
        vp.skipOnDrop = true;
    }

    private string GetVideoURL(int index)
    {
        return rootURL + index + fileExtension;
    }

    private IEnumerator StartPlayingVideoRoutine(int index)
    {
        if (videoPlayer == null) yield break;

        videoPlayer.Stop();

        // FIX 1: Bypass Audio Decoder for Silent MP4s
        // If the video is in silentVideoIndices, set AudioOutputMode to None BEFORE set URL
        if (silentVideoIndices.Contains(index))
        {
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        }
        else
        {
            videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        }

        if (videoSourceMode == VideoSourceMode.URL)
        {
            videoPlayer.source = VideoSource.Url;
            string targetUrl = GetVideoURL(index);

            if (videoPlayer.url != targetUrl)
            {
                videoPlayer.url = targetUrl;
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
                yield break;
            }
        }

        // FIX 2: Trigger Play() immediately on HTML5 element
        // In WebGL, calling Play() directly tells browser media engine to start rendering 
        // as soon as frame 0 arrives, bypassing the C# coroutine while-loop stall.
        videoPlayer.Play();
        currentVideoIndex = index;

        // Resolve external voiceover audio clip
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
            if (voiceOverAudioSource != null) voiceOverAudioSource.Stop();
        }
        else
        {
            PlayVoiceOver(selectedClip);
        }

        if (mouseLook != null)
            mouseLook.ResetRotation(defaultRotation);
    }

    private void PlayVoiceOver(AudioClip clip)
    {
        if (clip == null || voiceOverAudioSource == null) return;

        if (clip.loadState != AudioDataLoadState.Loaded)
        {
            clip.LoadAudioData();
        }

        voiceOverAudioSource.Stop();
        voiceOverAudioSource.clip = clip;
        voiceOverAudioSource.Play();
    }

    private void StopVideoAndAudio()
    {
        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            videoPlayer.Stop();
        }

        if (voiceOverAudioSource != null && voiceOverAudioSource.isPlaying)
        {
            voiceOverAudioSource.Stop();
        }
    }
    #endregion
}