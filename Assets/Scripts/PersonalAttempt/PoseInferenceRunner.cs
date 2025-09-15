using System;
using UnityEngine;
using Unity.InferenceEngine;

[RequireComponent(typeof(InferenceEngineWrapper))]
public class PoseInferenceRunner : MonoBehaviour
{
    [Header("Parser Settings")]
    [Tooltip("Concrete parser that knows how to interpret the model output (e.g., YoloPoseParser, VNectParser).")]
    [SerializeField] private YoloPoseParser parserBehaviour;

    private IPoseParser parser;
    private InferenceEngineWrapper inference;

    // Event so external systems can react (renderers, avatar controllers, etc.)
    public event Action<PoseSkeleton> OnSkeletonReady;

    private void Awake()
    {
        inference = GetComponent<InferenceEngineWrapper>();

        if (parserBehaviour == null)
        {
            Debug.LogError("[PoseInferenceRunner] No parser assigned!");
            return;
        }

        parser = parserBehaviour as IPoseParser;
        if (parser == null)
        {
            Debug.LogError("[PoseInferenceRunner] Assigned parser does not implement IPoseParser.");
        }
    }

    private void OnEnable()
    {
        if (inference != null)
            inference.OnOutputReady += HandleOutput;
    }

    private void OnDisable()
    {
        if (inference != null)
            inference.OnOutputReady -= HandleOutput;
    }

    private void HandleOutput(Tensor<float> output)
    {
        if (parser == null)
        {
            Debug.LogWarning("[PoseInferenceRunner] No parser available, skipping.");
            return;
        }

        int w = 640;
        int h = 640;

        PoseSkeleton skeleton = parser.Parse(output, w, h);

        if (skeleton != null)
        {
            OnSkeletonReady?.Invoke(skeleton);
            Debug.Log($"[PoseInferenceRunner] Skeleton parsed with {skeleton.joints.Count} joints (conf {skeleton.confidence:F2}).");
        }

        output.Dispose();
    }
}
