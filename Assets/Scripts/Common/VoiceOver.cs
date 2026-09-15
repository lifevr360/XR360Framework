using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoiceOver : MonoBehaviour
{
    public AudioSource audioSource, backgroundMusic;
    public AudioClip[] audioClips;

    private int currentClipIndex = 0;

    // Start is called before the first frame update
    void Start()
    {
        backgroundMusic.Play();
        ChangeAudioClip(0);
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void ChangeAudioClip(int clipIndex)
    {
        if (clipIndex >= 0 && clipIndex < audioClips.Length)
        {
            // Change the audio clip
            audioSource.clip = audioClips[clipIndex];
            StartCoroutine(PlayAudioClips());
        }
        else
        {
            Debug.LogError("Clip index is out of range: " + clipIndex);
        }
    }

    IEnumerator PlayAudioClips()
    {
        yield return new WaitForSeconds(1.5f);
        audioSource.Play();
    }

    public void StopAudio()
    {
        audioSource.Stop();
        currentClipIndex = 0;
    }
}

