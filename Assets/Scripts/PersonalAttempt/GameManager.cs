using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private VideoCapture videoCapture;
    [SerializeField] private PoseInferenceRunner poseRunner;
    [SerializeField] private SkeletonRenderer skeletonRenderer;

    private void Start()
    {
        videoCapture.Init(640, 480);

        skeletonRenderer.Init(640, 480);

        poseRunner.OnSkeletonReady += skeleton =>
        {
            skeletonRenderer.Render(skeleton);
        };

        Debug.Log("[GameManager] Pipeline initialized.");
    }
}
