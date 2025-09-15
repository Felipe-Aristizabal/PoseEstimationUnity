using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class VideoCapture : MonoBehaviour
{
    [Header("Input Settings")]
    public bool useWebCam = true;
    public int webCamIndex = 0;
    public VideoPlayer videoPlayer;

    [Header("UI")]
    public RawImage videoScreen;

    public RenderTexture MainTexture { get; private set; }

    private WebCamTexture webCamTexture;


    public void Init(int width, int height)
    {
        Debug.Log($"[VideoCapture] Init called → useWebCam={useWebCam}, webCamIndex={webCamIndex}, width={width}, height={height}");

        if (useWebCam)
            StartWebCam(width, height);
        else
            StartVideo(width, height);
    }

    private void StartWebCam(int width, int height)
    {
        Debug.Log("[VideoCapture] Starting webcam...");

        WebCamDevice[] devices = WebCamTexture.devices;
        Debug.Log($"[VideoCapture] Found {devices.Length} webcam devices.");

        if (devices.Length == 0)
        {
            Debug.LogError("[VideoCapture] No webcam devices found!");
            return;
        }

        if (devices.Length <= webCamIndex)
        {
            Debug.LogWarning($"[VideoCapture] WebCamIndex {webCamIndex} out of range, resetting to 0");
            webCamIndex = 0;
        }

        Debug.Log($"[VideoCapture] Using device: {devices[webCamIndex].name}");

        webCamTexture = new WebCamTexture(devices[webCamIndex].name, width, height);
        webCamTexture.Play();

        if (videoScreen != null)
        {
            videoScreen.texture = webCamTexture;
            Debug.Log("[VideoCapture] Assigned webcam texture to RawImage");
        }
        else
        {
            Debug.LogWarning("[VideoCapture] No RawImage assigned in inspector!");
        }

        MainTexture = new RenderTexture(width, height, 0, RenderTextureFormat.RGB565);
        Debug.Log("[VideoCapture] Created MainTexture RenderTexture for inference");

        // Copiar frame cada update
        Debug.Log("[VideoCapture] Webcam started successfully");
    }

    private void StartVideo(int width, int height)
    {
        Debug.Log("[VideoCapture] Starting video playback...");

        if (videoPlayer == null)
        {
            Debug.LogError("[VideoCapture] No VideoPlayer assigned!");
            return;
        }

        MainTexture = new RenderTexture(width, height, 0, RenderTextureFormat.RGB565);
        Debug.Log("[VideoCapture] Created MainTexture RenderTexture for video");

        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = MainTexture;

        if (videoScreen != null)
        {
            videoScreen.texture = MainTexture;
            Debug.Log("[VideoCapture] Assigned video RenderTexture to RawImage");
        }
        else
        {
            Debug.LogWarning("[VideoCapture] No RawImage assigned in inspector!");
        }

        videoPlayer.Play();
        Debug.Log("[VideoCapture] VideoPlayer started successfully");
    }

    private void Update()
    {
        if (useWebCam && webCamTexture != null && MainTexture != null)
        {
            // Copiar cada frame de la webcam al RenderTexture
            Graphics.Blit(webCamTexture, MainTexture);
        }
    }
}
