using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Unity.InferenceEngine;

/// <summary>
/// Unity 6 Inference wrapper (com.unity.ai.inference).
/// - High cohesion: only handles model loading + inference.
/// - Low coupling: emits OnOutputReady for parsers/consumers; does not know about pose/rig/UI.
/// - Pulls frames from a VideoCapture (RenderTexture) and runs inference on a fixed interval or every frame.
/// </summary>
public class InferenceEngineWrapper : MonoBehaviour
{
    [Header("Model")]
    [Tooltip("ModelAsset imported from ONNX (Assets/Resources/.../model.asset or assign via Inspector).")]
    [SerializeField] private ModelAsset modelAsset;

    [Tooltip("Backend device for inference.")]
    [SerializeField] private BackendType backendType = BackendType.GPUCompute;

    [Header("Input Source")]
    [Tooltip("Provider of frames (RenderTexture). If null, it will try FindObjectOfType<VideoCapture>().")]
    [SerializeField] private VideoCapture videoCapture;

    [Header("Preprocessing")]
    [Tooltip("Expected input width of the model (typical YOLO = 640).")]
    [SerializeField] private int inputWidth = 640;

    [Tooltip("Expected input height of the model (typical YOLO = 640).")]
    [SerializeField] private int inputHeight = 640;

    [Tooltip("Number of input channels (RGB=3).")]
    [SerializeField] private int inputChannels = 3;

    [Header("Run Loop")]
    [Tooltip("Run inference every frame (Update). If false, call RunOnce() or enable timed interval.")]
    [SerializeField] private bool runEveryFrame = true;

    [Tooltip("If > 0, run inference every 'intervalSeconds' instead of every frame.")]
    [SerializeField] private float intervalSeconds = 0f;

    // Public event so parsers/renderers can subscribe without coupling.
    public event Action<Tensor<float>> OnOutputReady;

    // Internals
    private Worker worker;
    private Tensor<float> inputTensor;   // persistent input tensor
    private float nextRunTime;
    private bool isInitialized;
    private bool isBusy;

    // For safety/logging
    private string cachedModelName => modelAsset ? modelAsset.name : "NULL";

    private void Awake()
    {
        Debug.Log("[Inference] Awake()");
    }

    private void Start()
    {
        Debug.Log("[Inference] Start() → Initializing Inference Engine wrapper");

        // 1) Resolve VideoCapture
        if (videoCapture == null)
        {
            videoCapture = FindAnyObjectByType<VideoCapture>();
            Debug.Log(videoCapture
                ? "[Inference] VideoCapture found via FindAnyObjectByType"
                : "[Inference] WARNING: No VideoCapture found in scene.");
        }

        // 2) Validate model
        if (modelAsset == null)
        {
            Debug.LogError("[Inference] ModelAsset is NULL. Assign it in the Inspector or load from Resources before play.");
            return;
        }

        // 3) Create worker from ModelAsset (Unity 6 Inference API)
        try
        {
            var runtimeModel = ModelLoader.Load(modelAsset);
            worker = new Worker(runtimeModel, backendType);
            Debug.Log($"[Inference] Worker created. Model='{cachedModelName}', Backend={backendType}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Inference] Failed to create Worker from ModelAsset '{cachedModelName}'. Exception: {ex}");
            return;
        }

        // 4) Prepare input tensor
        try
        {
            var shape = new TensorShape(1, inputChannels, inputHeight, inputWidth); // NCHW
            inputTensor = new Tensor<float>(shape);
            Debug.Log($"[Inference] Input tensor allocated. Shape=NCHW(1,{inputChannels},{inputHeight},{inputWidth})");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Inference] Failed to allocate input tensor. {ex}");
            return;
        }

        isInitialized = true;
        Debug.Log("[Inference] Initialization complete.");
    }

    private void Update()
    {
        if (!isInitialized) return;
        if (videoCapture == null || videoCapture.MainTexture == null)
        {
            // This will happen the first few frames until VideoCapture sets up
            return;
        }

        if (isBusy) return;

        if (runEveryFrame && intervalSeconds <= 0f)
        {
            _ = RunOnceAsync();
        }
        else if (intervalSeconds > 0f && Time.time >= nextRunTime)
        {
            nextRunTime = Time.time + intervalSeconds;
            _ = RunOnceAsync();
        }
    }

    /// <summary>
    /// Public method to trigger a single inference externally (e.g., button).
    /// </summary>
    public void RunOnce()
    {
        if (!isInitialized || isBusy) return;
        _ = RunOnceAsync();
    }

    /// <summary>
    /// Single inference pass: copy RenderTexture -> inputTensor, schedule worker, readback output, emit event.
    /// </summary>
    private async Task RunOnceAsync()
    {
        isBusy = true;
        try
        {
            var src = videoCapture?.MainTexture;
            if (src == null)
            {
                Debug.LogWarning("[Inference] RunOnceAsync() skipped: VideoCapture.MainTexture is null.");
                isBusy = false;
                return;
            }

            // 1) Preprocess: Texture -> Tensor (resizes to tensor shape)
            try
            {
                TextureConverter.ToTensor(src, inputTensor);
                // If needed: normalize here by iterating inputTensor and dividing by 255f, etc.
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Inference] TextureConverter.ToTensor failed. Check texture & tensor shapes. {ex}");
                isBusy = false;
                return;
            }

            // 2) Inference
            try
            {
                worker.Schedule(inputTensor);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Inference] Worker.Schedule failed. {ex}");
                isBusy = false;
                return;
            }

            // 3) Readback: clone output to CPU so parsers can read arrays safely
            Tensor outputCpu = null;
            try
            {
                outputCpu = await worker.PeekOutput().ReadbackAndCloneAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Inference] ReadbackAndCloneAsync failed. {ex}");
                isBusy = false;
                return;
            }

            if (outputCpu is Tensor<float> outputFloat)
            {
                OnOutputReady?.Invoke(outputFloat); // Consumers must NOT Dispose this tensor here; they can clone arrays if needed.
            }
            else
            {
                Debug.LogWarning("[Inference] Output is not Tensor<float>. Did you export the model correctly?");
                outputCpu?.Dispose();
            }

            // NOTE: Do NOT dispose outputFloat here if listeners still need it.
            // Pattern: listeners copy data they need (ToReadOnlyArray) and then notify back so we can dispose.
            // For simplicity, we dispose immediately after invoking event if nobody subscribed.
            if (OnOutputReady == null && outputCpu != null)
            {
                outputCpu.Dispose();
            }
        }
        finally
        {
            isBusy = false;
        }
    }

    /// <summary>
    /// Helper to print a TensorShape as [N,C,H,W] or indices if dimension count differs.
    /// </summary>
    private string DescribeShape(TensorShape shape)
    {
        try
        {
            return $"[N={shape[0]}, C={shape[1]}, H={shape[2]}, W={shape[3]}]";
        }
        catch
        {
            // Fallback for non-4D shapes
            var dims = "";
            for (int i = 0; i < shape.rank; i++)
                dims += (i == 0 ? "" : ",") + shape[i];
            return $"[rank={shape.rank}: {dims}]";
        }
    }

    private void OnDestroy()
    {
        Debug.Log("[Inference] OnDestroy → disposing resources");
        try
        {
            inputTensor?.Dispose();
            inputTensor = null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Inference] Disposing input tensor threw: {ex}");
        }

        try
        {
            worker?.Dispose();
            worker = null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Inference] Disposing worker threw: {ex}");
        }
    }

    #region Public setters (for flexibility from GameManager/Installer)
    public void SetModel(ModelAsset newModel, BackendType newBackend, int inW, int inH, int inC = 3)
    {
        Debug.Log("[Inference] SetModel() called.");

        // Dispose previous worker/tensor
        OnDestroy();

        modelAsset = newModel;
        backendType = newBackend;
        inputWidth = inW;
        inputHeight = inH;
        inputChannels = inC;

        // Re-init quickly
        Start();
    }
    #endregion
}
