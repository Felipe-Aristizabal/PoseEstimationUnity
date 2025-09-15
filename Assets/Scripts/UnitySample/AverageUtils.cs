using UnityEngine;

public static class AverageUtils
{
    public static Vector3 Average(Vector3[] points, int[] indices)
    {
        if (indices == null || indices.Length == 0) return Vector3.zero;

        Vector3 sum = Vector3.zero;
        int count = 0;

        foreach (int i in indices)
        {
            if (points[i] != Vector3.zero)
            {
                sum += points[i];
                count++;
            }
        }

        return count > 0 ? sum / count : Vector3.zero;
    }
}
