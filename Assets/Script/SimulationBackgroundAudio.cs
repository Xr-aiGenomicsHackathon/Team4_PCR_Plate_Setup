using UnityEngine;

/// <summary>Continuous music independent of scene transitions and narration.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class SimulationBackgroundAudio : MonoBehaviour
{
    public static SimulationBackgroundAudio Instance { get; private set; }
    [Header("Background music")]
    [Tooltip("Drag the looping simulation music clip here.")]
    [SerializeField] private AudioClip backgroundAudio;
    [SerializeField, Range(0f, 1f)] private float volume = 0.176f;
    [SerializeField] private bool muted;
    [SerializeField, Min(0.05f)] private float volumeFadeSeconds = 0.4f;
    [Tooltip("Music level during narration, as a fraction of the configured music volume.")]
    [SerializeField, Range(0f, 1f)] private float narrationMusicLevel = 0.15f;
    private AudioSource source;
    private float nextVoiceCheck;
    private bool voicePlaying;
    public AudioClip BackgroundAudio => backgroundAudio;
    public float Volume => volume;
    public bool Muted => muted;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.pitch = 1f;
        source.reverbZoneMix = 0f;
        source.volume = 0f;
        source.clip = backgroundAudio;
        if (backgroundAudio != null) source.Play();
    }

    private void Update()
    {
        if (source.clip != backgroundAudio)
        {
            source.Stop();
            source.clip = backgroundAudio;
            if (backgroundAudio != null) source.Play();
        }
        if (Time.unscaledTime >= nextVoiceCheck)
        {
            nextVoiceCheck = Time.unscaledTime + 0.1f;
            voicePlaying = false;
            foreach (var other in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                if (other != source && other.isPlaying && !other.mute && other.volume > 0f)
                { voicePlaying = true; break; }
        }
        float target = muted ? 0f : Mathf.Clamp01(volume) * (voicePlaying ? narrationMusicLevel : 1f);
        source.volume = Mathf.MoveTowards(source.volume, target,
            Time.unscaledDeltaTime / Mathf.Max(0.05f, volumeFadeSeconds));
    }

    public void SetBackgroundAudio(AudioClip clip) { backgroundAudio = clip; }
    public void SetVolume(float value) { volume = Mathf.Clamp01(value); }
    public void SetMuted(bool value) { muted = value; }
    public void ToggleMuted() { muted = !muted; }
    private void OnDestroy() { if (Instance == this) Instance = null; }
}
