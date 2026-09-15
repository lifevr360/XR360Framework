using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class VideoManager : MonoBehaviour
{
    #region Public Variables / Structures
    public enum Language { Hindi, English }
    public enum VideoSourceMode { URL, LocalClips } // New feature: Select source mode

    [Header("References")]
    public VideoPlayer videoPlayer;
    public VideoPlayer firstvideoPlayer;
    public AudioSource voiceOverAudioSource;
    public Mouselook mouseLook; // Kept exactly as your original script
    public Transform fps;

    [Header("Video Source Settings")]
    public VideoSourceMode videoSourceMode = VideoSourceMode.URL; // Toggle between URL and Local

    [Header("URL Settings (Used if Mode is URL)")]
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
    #endregion

    #region Private Variables
    private Language selectedLanguage = Language.English; // Default language
    private int currentVideoIndex = -1; // Track the currently playing video
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
    #endregion

    #region Custom Public Methods
    public void SetLanguage(int value)
    {
        selectedLanguage = (Language)value;
        Debug.Log("Language set to: " + selectedLanguage);
    }

    public void PlayVideo(int videoIndex)
    {
        // Check if the requested video is already playing
        if (videoIndex == currentVideoIndex && videoPlayer.isPlaying)
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

        Debug.Log("Stopped coroutines.");
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
    private string GetVideoURL(int index)
    {
        // Direct URL generation using the index (From your original script)
        return rootURL + index + fileExtension;
    }

    private IEnumerator StartPlayingVideoRoutine(int index)
    {
        // Stop current playback and reset frame to prevent the "rushing" catch-up effect
        videoPlayer.Stop();
        videoPlayer.frame = 0;

        // Check which mode is selected to assign the correct source
        if (videoSourceMode == VideoSourceMode.URL)
        {
            videoPlayer.source = VideoSource.Url;
            string videoURL = GetVideoURL(index);

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
            }

            videoPlayer.Play();
            currentVideoIndex = index;

            if (videoSourceMode == VideoSourceMode.URL)
                Debug.Log("Playing video from URL: " + videoPlayer.url);
            else
                Debug.Log("Playing baked local video: " + videoPlayer.clip.name);

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