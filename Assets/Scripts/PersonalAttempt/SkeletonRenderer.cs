using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders a PoseSkeleton (joints + bones) onto a RawImage overlay.
/// Subscribe this to PoseInferenceRunner.OnSkeletonReady.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class SkeletonRenderer : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private Color keypointColor = Color.red;
    [SerializeField] private Color boneColor = Color.green;
    [SerializeField] private int keypointRadius = 4;

    private RawImage overlayUI;
    private Texture2D overlayTexture;
    private Color[] clearColors;

    // Skeleton connections (COCO style: 17 keypoints)
    private readonly int[,] bones = new int[,]
    {
        {0,1}, {1,2}, {2,3}, {3,4},       // right arm
        {0,5}, {5,6}, {6,7}, {7,8},       // left arm
        {0,9}, {9,10}, {10,11},           // right leg
        {0,12}, {12,13}, {13,14},         // left leg
        {0,15}, {0,16}                    // eyes/ears
    };

    private void Awake()
    {
        overlayUI = GetComponent<RawImage>();
    }

    /// <summary>
    /// Initializes the overlay texture with the given dimensions (match video input).
    /// </summary>
    public void Init(int width, int height)
    {
        overlayTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        overlayTexture.filterMode = FilterMode.Point;

        clearColors = new Color[width * height];
        for (int i = 0; i < clearColors.Length; i++) clearColors[i] = Color.clear;

        overlayUI.texture = overlayTexture;
        Debug.Log($"[SkeletonRenderer] Initialized overlay {width}x{height}");
    }

    /// <summary>
    /// Draw a skeleton overlay on the UI.
    /// </summary>
    public void Render(PoseSkeleton skeleton)
    {
        if (overlayTexture == null || skeleton == null) return;

        overlayTexture.SetPixels(clearColors);

        // Draw bones
        for (int i = 0; i < bones.GetLength(0); i++)
        {
            int a = bones[i, 0];
            int b = bones[i, 1];
            if (a < skeleton.joints.Count && b < skeleton.joints.Count)
            {
                if (skeleton.joints[a].z > 0.5f && skeleton.joints[b].z > 0.5f)
                {
                    DrawLine((int)skeleton.joints[a].x, (int)skeleton.joints[a].y,
                             (int)skeleton.joints[b].x, (int)skeleton.joints[b].y,
                             boneColor);
                }
            }
        }

        // Draw keypoints
        foreach (var kp in skeleton.joints)
        {
            if (kp.z > 0.5f) // confidence threshold
                DrawCircle((int)kp.x, (int)kp.y, keypointRadius, keypointColor);
        }

        overlayTexture.Apply();
    }

    private void DrawCircle(int cx, int cy, int r, Color col)
    {
        for (int x = -r; x <= r; x++)
        {
            for (int y = -r; y <= r; y++)
            {
                if (x * x + y * y <= r * r)
                {
                    int px = cx + x;
                    int py = cy + y;
                    if (px >= 0 && px < overlayTexture.width &&
                        py >= 0 && py < overlayTexture.height)
                    {
                        overlayTexture.SetPixel(px, py, col);
                    }
                }
            }
        }
    }

    private void DrawLine(int x0, int y0, int x1, int y1, Color col)
    {
        int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy, e2;

        while (true)
        {
            if (x0 >= 0 && x0 < overlayTexture.width &&
                y0 >= 0 && y0 < overlayTexture.height)
            {
                overlayTexture.SetPixel(x0, y0, col);
            }

            if (x0 == x1 && y0 == y1) break;
            e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }
}
