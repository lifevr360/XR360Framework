using UnityEngine;
using UnityEngine.UI;

public class UIButtonSound : MonoBehaviour
{
    [Header("--- MANAGER SETTINGS ---")]
    [Tooltip("ONLY fill these out if this script is attached to your central UI Audio Source GameObject.")]
    public AudioSource globalAudioSource;
    public AudioClip globalClickSound;

    private static AudioSource cachedSource;
    private static AudioClip cachedClip;

    private Button button;

    private void Awake()
    {
        // 1. If this instance is the central Manager, store the references globally
        if (globalAudioSource != null && globalClickSound != null)
        {
            cachedSource = globalAudioSource;
            cachedClip = globalClickSound;
            
            if (globalAudioSource.playOnAwake) globalAudioSource.playOnAwake = false;
            return; 
        }

        // 2. If this is a button, hook up the click listener
        button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(PlaySound);
        }
    }

    private void PlaySound()
    {
        // Fallback: If the static reference isn't ready yet, search the scene for the manager
        if (cachedSource == null || cachedClip == null)
        {
            UIButtonSound foundManager = Object.FindFirstObjectByType<UIButtonSound>();
            if (foundManager != null && foundManager.globalAudioSource != null)
            {
                cachedSource = foundManager.globalAudioSource;
                cachedClip = foundManager.globalClickSound;
            }
        }

        // Play the sound directly on the manager's AudioSource component
        if (cachedSource != null && cachedClip != null)
        {
            // PlayOneShot runs on the central speaker object. Even if this UI Button 
            // deactivates itself immediately after this line, the sound will keep playing!
            cachedSource.PlayOneShot(cachedClip);
        }
        else
        {
            Debug.LogWarning($"[UI Sound] Missing Manager on {gameObject.name}!");
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(PlaySound);
        }
    }
}