// KeepWorldScale.cs
using UnityEngine;

[DefaultExecutionOrder(10000)] // run late
public class KeepWorldScale : MonoBehaviour
{
    public Vector3 targetWorldScale = Vector3.one;

    void LateUpdate()
    {
        Apply(transform, targetWorldScale);
    }

    public static void Apply(Transform t, Vector3 desiredWorldScale)
    {
        var p = t.parent;
        Vector3 parentLossy = p ? p.lossyScale : Vector3.one;

        // avoid divide-by-zero
        float ix = parentLossy.x == 0f ? 1f : 1f / parentLossy.x;
        float iy = parentLossy.y == 0f ? 1f : 1f / parentLossy.y;
        float iz = parentLossy.z == 0f ? 1f : 1f / parentLossy.z;

        t.localScale = new Vector3(
            desiredWorldScale.x * ix,
            desiredWorldScale.y * iy,
            desiredWorldScale.z * iz
        );
    }
}
