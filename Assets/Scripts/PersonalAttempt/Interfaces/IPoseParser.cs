using Unity.InferenceEngine;

public interface IPoseParser
{
    PoseSkeleton Parse(Tensor<float> output, int imageWidth, int imageHeight);
}