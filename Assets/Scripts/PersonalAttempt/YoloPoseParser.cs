using System.Collections.Generic;
using UnityEngine;
using Unity.InferenceEngine;

/// <summary>
/// Parser for YOLOvX Pose output tensors.
/// Implements IPoseParser for use with PoseInferenceRunner.
/// </summary>
[CreateAssetMenu(menuName = "AI/Parsers/YoloPoseParser")]
public class YoloPoseParser : ScriptableObject, IPoseParser
{
    public enum OutputScale
    {
        ZeroToOne,      // model outputs 0–1 normalized coordinates
        ZeroToInputSize // model outputs coordinates relative to model input size (e.g., 0–640)
    }

    [Header("YOLO Pose Settings")]
    [Tooltip("Number of keypoints the model predicts (COCO=17, BlazePose=33, etc.)")]
    [SerializeField] private int numKeypoints = 17;

    [Tooltip("Minimum confidence for detection to be considered valid.")]
    [Range(0f, 1f)]
    [SerializeField] private float confidenceThreshold = 0.5f;

    [Tooltip("IoU threshold for Non-Maximum Suppression.")]
    [Range(0f, 1f)]
    [SerializeField] private float nmsThreshold = 0.45f;

    [Tooltip("How to interpret output coordinates from the model.")]
    [SerializeField] private OutputScale outputScale = OutputScale.ZeroToInputSize;

    [Tooltip("Input size of the model (usually 640 for YOLO). Used if outputScale=ZeroToInputSize.")]
    [SerializeField] private int modelInputSize = 640;

    public PoseSkeleton Parse(Tensor<float> output, int imageWidth, int imageHeight)
    {
        var detections = ParseDetections(output, imageWidth, imageHeight);

        if (detections.Count == 0)
        {
            Debug.Log("[YoloPoseParser] No valid detections found.");
            return null;
        }

        // Pick the highest-confidence detection
        var best = detections[0];
        return new PoseSkeleton(best.keypoints, best.confidence);
    }

    private List<YoloDetection> ParseDetections(Tensor<float> output, int imageWidth, int imageHeight)
    {
        var results = new List<YoloDetection>();

        float[] data = output.AsReadOnlyNativeArray().ToArray();
        int stride = 5 + numKeypoints * 3;
        int numDetections = data.Length / stride;

        for (int i = 0; i < numDetections; i++)
        {
            int offset = i * stride;

            float x = data[offset];
            float y = data[offset + 1];
            float w = data[offset + 2];
            float h = data[offset + 3];
            float conf = data[offset + 4];

            if (conf < confidenceThreshold) continue;

            // Interpret bbox depending on output scale
            if (outputScale == OutputScale.ZeroToOne)
            {
                x *= imageWidth;
                y *= imageHeight;
                w *= imageWidth;
                h *= imageHeight;
            }
            else if (outputScale == OutputScale.ZeroToInputSize)
            {
                float scaleX = (float)imageWidth / modelInputSize;
                float scaleY = (float)imageHeight / modelInputSize;
                x *= scaleX;
                y *= scaleY;
                w *= scaleX;
                h *= scaleY;
            }

            var bbox = new Rect(x - w / 2f, y - h / 2f, w, h);

            var keypoints = new List<Vector3>();
            for (int k = 0; k < numKeypoints; k++)
            {
                float kx = data[offset + 5 + k * 3];
                float ky = data[offset + 5 + k * 3 + 1];
                float kc = data[offset + 5 + k * 3 + 2];

                if (outputScale == OutputScale.ZeroToOne)
                {
                    kx *= imageWidth;
                    ky *= imageHeight;
                }
                else if (outputScale == OutputScale.ZeroToInputSize)
                {
                    float scaleX = (float)imageWidth / modelInputSize;
                    float scaleY = (float)imageHeight / modelInputSize;
                    kx *= scaleX;
                    ky *= scaleY;
                }

                if (i < 5) Debug.Log($"Joint {k}: raw=({kx},{ky}), scaled=({kx * (float)imageWidth / modelInputSize},{ky * (float)imageHeight / modelInputSize})");


                keypoints.Add(new Vector3(kx, ky, kc));
            }

            results.Add(new YoloDetection(bbox, conf, keypoints));
        }

        // Apply NMS to filter duplicates
        return ApplyNMS(results, nmsThreshold);
    }

    private List<YoloDetection> ApplyNMS(List<YoloDetection> detections, float iouThreshold)
    {
        var result = new List<YoloDetection>();
        detections.Sort((a, b) => b.confidence.CompareTo(a.confidence));

        bool[] removed = new bool[detections.Count];

        for (int i = 0; i < detections.Count; i++)
        {
            if (removed[i]) continue;
            var detA = detections[i];
            result.Add(detA);

            for (int j = i + 1; j < detections.Count; j++)
            {
                if (removed[j]) continue;
                var detB = detections[j];

                float iou = ComputeIoU(detA.bbox, detB.bbox);
                if (iou > iouThreshold)
                    removed[j] = true;
            }
        }
        return result;
    }

    private float ComputeIoU(Rect a, Rect b)
    {
        float areaA = a.width * a.height;
        float areaB = b.width * b.height;
        if (areaA <= 0 || areaB <= 0) return 0;

        float minX = Mathf.Max(a.xMin, b.xMin);
        float minY = Mathf.Max(a.yMin, b.yMin);
        float maxX = Mathf.Min(a.xMax, b.xMax);
        float maxY = Mathf.Min(a.yMax, b.yMax);

        float intersection = Mathf.Max(0, maxX - minX) * Mathf.Max(0, maxY - minY);
        return intersection / (areaA + areaB - intersection);
    }

    /// <summary>
    /// Internal detection representation before NMS.
    /// </summary>
    private class YoloDetection
    {
        public Rect bbox;
        public float confidence;
        public List<Vector3> keypoints;

        public YoloDetection(Rect bbox, float confidence, List<Vector3> keypoints)
        {
            this.bbox = bbox;
            this.confidence = confidence;
            this.keypoints = keypoints;
        }
    }
}
