using UnityEngine;

public class PosePreview : MonoBehaviour
{
    public Keypoint[] keypoints;

    public void SetActive(bool active)
    {
        gameObject.SetActive(active);
    }

    public void SetKeypoint(int index, bool active, Vector3 position)
    {
        keypoints[index].Set(active, position);
    }
}
