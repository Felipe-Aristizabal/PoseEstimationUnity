using System.Collections.Generic;
using UnityEngine;

public class PoseSkeleton
{
    public List<Vector3> joints;   // x,y,confidence (z usado como score)
    public float confidence;

    public PoseSkeleton(List<Vector3> joints, float confidence)
    {
        this.joints = joints;
        this.confidence = confidence;
    }
}
