using UnityEngine;
using System.Text;

[System.Serializable]
public class KeypointToIKTarget
{
    [Tooltip("Nombre lógico para identificar este mapping en logs")]
    public string name = "Mapping";

    [Tooltip("Si se desactiva, este mapping no se procesa")]
    public bool enabled = true;

    [Tooltip("Índices BlazePose (p.ej. mano izq: 15,17,19,21)")]
    public int[] keypointIndices;

    [Tooltip("Transform del IK Target (LeftHandTarget, etc.)")]
    public Transform targetTransform;

    [Tooltip("Suavizado del movimiento")]
    public float smoothSpeed = 10f;

    // Estado de debug en runtime
    [HideInInspector] public Vector3 lastComputed;
    [HideInInspector] public bool hadValidValue;
}

public class PoseToAvatarIK : MonoBehaviour
{
    const int NUM_KEYPOINTS = 33;

    [Header("Dependencies")]
    [SerializeField] PoseDetection poseDetection;

    [Header("Mappings")]
    public KeypointToIKTarget[] ikTargets;

    [Header("Retarget (opcional)")]
    public Transform retargetSpace;   // p.ej. Image Quad si quieres transformar desde su espacio
    public float scale = 1f;          // escala global
    public Vector3 worldOffset;       // offset global

    [Header("Debug")]
    public bool debugLogs = true;
    public int debugEveryNFrames = 30;  // reduce spam
    public bool drawGizmos = true;
    public Color gizmoColor = new Color(1f, 0.4f, 0f, 0.8f);

    void OnValidate() { ValidateConfiguration(); }
    void Awake() { ValidateConfiguration(); }

    void ValidateConfiguration()
    {
        if (poseDetection == null)
            Debug.LogWarning($"[{nameof(PoseToAvatarIK)}] 'poseDetection' NO asignado en '{name}'.");

        if (ikTargets == null || ikTargets.Length == 0)
        {
            Debug.LogWarning($"[{nameof(PoseToAvatarIK)}] No hay 'ikTargets' configurados en '{name}'.");
            return;
        }

        for (int t = 0; t < ikTargets.Length; t++)
        {
            var m = ikTargets[t];
            if (m == null) { Debug.LogWarning($"[{nameof(PoseToAvatarIK)}] Mapping #{t} es null."); continue; }

            if (m.targetTransform == null)
                Debug.LogWarning($"[{nameof(PoseToAvatarIK)}] '{m.name}' sin targetTransform.");

            if (m.keypointIndices == null || m.keypointIndices.Length == 0)
            {
                Debug.LogWarning($"[{nameof(PoseToAvatarIK)}] '{m.name}' sin keypointIndices.");
                continue;
            }

            foreach (var idx in m.keypointIndices)
                if (idx < 0 || idx >= NUM_KEYPOINTS)
                    Debug.LogError($"[{nameof(PoseToAvatarIK)}] '{m.name}' índice fuera de rango: {idx} (válido 0..32).");
        }
    }

    void LateUpdate()
    {
        if (poseDetection == null) return;

        var kps = poseDetection.CurrentKeypoints;
        if (kps == null)
        {
            if (ShouldLog()) Debug.LogWarning($"[{nameof(PoseToAvatarIK)}] CurrentKeypoints es NULL.");
            return;
        }
        if (kps.Length < NUM_KEYPOINTS)
        {
            if (ShouldLog()) Debug.LogError($"[{nameof(PoseToAvatarIK)}] CurrentKeypoints.Length={kps.Length} < {NUM_KEYPOINTS}.");
            return;
        }

        for (int t = 0; t < ikTargets.Length; t++)
        {
            var map = ikTargets[t];
            if (map == null || !map.enabled) continue;

            if (map.targetTransform == null)
            {
                if (ShouldLog()) Debug.LogWarning($"[{nameof(PoseToAvatarIK)}] '{map.name}' sin targetTransform.");
                continue;
            }
            if (map.keypointIndices == null || map.keypointIndices.Length == 0)
            {
                if (ShouldLog()) Debug.LogWarning($"[{nameof(PoseToAvatarIK)}] '{map.name}' sin keypointIndices.");
                continue;
            }

            Vector3 avg = Vector3.zero; int count = 0;
            foreach (var idx in map.keypointIndices)
            {
                if (idx < 0 || idx >= kps.Length)
                {
                    if (ShouldLog()) Debug.LogError($"[{nameof(PoseToAvatarIK)}] '{map.name}' índice inválido {idx}.");
                    continue;
                }

                var p = kps[idx];
                if (!IsFinite(p)) { if (ShouldLog()) Debug.LogWarning($"[{nameof(PoseToAvatarIK)}] kp[{idx}] NaN/Inf en '{map.name}'."); continue; }
                if (p == Vector3.zero) continue; 

                avg += p; count++;
            }

            if (count == 0)
            {
                if (ShouldLog()) Debug.LogWarning($"[{nameof(PoseToAvatarIK)}] Ningún punto válido para '{map.name}' este frame.");
                continue;
            }

            avg /= count;

            Vector3 world = avg * scale + worldOffset;
            if (retargetSpace != null)
                world = retargetSpace.TransformPoint(avg * scale) + worldOffset;


            map.lastComputed = world;
            map.hadValidValue = true;

            float s = Mathf.Max(0.01f, map.smoothSpeed);
            map.targetTransform.position = Vector3.Lerp(map.targetTransform.position, world, Time.deltaTime * s);
        }
    }

    bool ShouldLog() => debugLogs && (Time.frameCount % Mathf.Max(1, debugEveryNFrames) == 0);

    static bool IsFinite(Vector3 v)
    {
        return !(float.IsNaN(v.x) || float.IsInfinity(v.x) ||
                 float.IsNaN(v.y) || float.IsInfinity(v.y) ||
                 float.IsNaN(v.z) || float.IsInfinity(v.z));
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos || ikTargets == null) return;
        Gizmos.color = gizmoColor;
        foreach (var m in ikTargets)
        {
            if (m == null || !m.hadValidValue) continue;
            if (m.targetTransform != null)
            {
                Gizmos.DrawSphere(m.targetTransform.position, 0.02f);
                Gizmos.DrawLine(m.targetTransform.position, m.lastComputed);
                Gizmos.DrawWireSphere(m.lastComputed, 0.02f);
            }
            else
            {
                Gizmos.DrawWireSphere(m.lastComputed, 0.02f);
            }
        }
    }
}
