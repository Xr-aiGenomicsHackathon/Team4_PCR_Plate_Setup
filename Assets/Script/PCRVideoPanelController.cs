using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>Controls playback for the scene's PCR tutorial video panel.</summary>
public sealed class PCRVideoPanelController : MonoBehaviour
{
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RawImage videoSurface;
    [SerializeField] private Button playPauseButton;
    [SerializeField] private Text playPauseLabel;
    [SerializeField] private Slider timeline;
    [SerializeField] private Text timeLabel;
    [SerializeField] private GameObject emptyState;

    private bool playWhenPrepared;

    private void Awake()
    {
        if (videoPlayer == null) videoPlayer = GetComponent<VideoPlayer>();
        if (videoPlayer != null)
        {
            videoPlayer.playOnAwake = false;
            videoPlayer.prepareCompleted += OnVideoPrepared;
            videoPlayer.errorReceived += OnVideoError;
        }

        if (playPauseButton != null)
            playPauseButton.onClick.AddListener(TogglePlayback);
        if (timeline != null)
            timeline.onValueChanged.AddListener(Seek);
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= OnVideoPrepared;
            videoPlayer.errorReceived -= OnVideoError;
        }
    }

    private void Start()
    {
        RefreshSourceState();
    }

    private void Update()
    {
        if (videoPlayer == null) return;
        if (videoSurface != null && videoPlayer.targetTexture != null)
            videoSurface.texture = videoPlayer.targetTexture;

        bool hasVideo = videoPlayer.clip != null || !string.IsNullOrEmpty(videoPlayer.url);
        if (emptyState != null) emptyState.SetActive(!hasVideo);
        if (playPauseLabel != null)
            playPauseLabel.text = videoPlayer.isPlaying ? "Ⅱ" : "▶";

        if (!videoPlayer.isPrepared || videoPlayer.length <= 0d) return;

        if (timeline != null)
            timeline.SetValueWithoutNotify(Mathf.Clamp01((float)(videoPlayer.time / videoPlayer.length)));
        if (timeLabel != null)
            timeLabel.text = FormatTime(videoPlayer.time) + " / " + FormatTime(videoPlayer.length);
    }

    private void TogglePlayback()
    {
        if (videoPlayer == null) return;
        if (videoPlayer.clip == null && string.IsNullOrEmpty(videoPlayer.url))
        {
            RefreshSourceState();
            return;
        }

        if (!videoPlayer.isPrepared)
        {
            playWhenPrepared = true;
            videoPlayer.Prepare();
            return;
        }

        if (videoPlayer.isPlaying)
        {
            videoPlayer.Pause();
            playWhenPrepared = false;
        }
        else
        {
            videoPlayer.Play();
        }
    }

    private void Seek(float normalizedTime)
    {
        if (videoPlayer == null || !videoPlayer.isPrepared || videoPlayer.length <= 0d) return;
        videoPlayer.time = Mathf.Clamp01(normalizedTime) * videoPlayer.length;
    }

    private void OnVideoPrepared(VideoPlayer player)
    {
        if (emptyState != null) emptyState.SetActive(false);
        if (playWhenPrepared)
        {
            playWhenPrepared = false;
            player.Play();
        }
    }

    private void OnVideoError(VideoPlayer player, string message)
    {
        Debug.LogWarning("PCR tutorial video could not be played: " + message, this);
        if (emptyState != null) emptyState.SetActive(true);
        if (playPauseLabel != null) playPauseLabel.text = "▶";
    }

    private void RefreshSourceState()
    {
        bool hasVideo = videoPlayer != null &&
                        (videoPlayer.clip != null || !string.IsNullOrEmpty(videoPlayer.url));
        if (emptyState != null) emptyState.SetActive(!hasVideo);
        if (videoSurface != null && videoPlayer != null)
            videoSurface.texture = videoPlayer.targetTexture;
        if (timeLabel != null && !hasVideo)
            timeLabel.text = "00:00 / 00:00";
    }

    private static string FormatTime(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds)) seconds = 0d;
        int total = Mathf.Max(0, (int)seconds);
        return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
    }
}