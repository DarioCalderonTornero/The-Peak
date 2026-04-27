using UnityEngine;

public static class ClimberCameraUtils
{
    public static bool TryCalculateCameraPosition(
        Transform climberTransform,
        LayerMask mountainLayer,
        float distance,
        float height,
        float side,
        out Vector3 resultPosition,
        out Quaternion resultRotation)
    {
        resultPosition = climberTransform.position;
        resultRotation = Quaternion.identity;

        Vector3 headPosition = climberTransform.position + Vector3.up * 1.5f;
        Vector3 baseOrigin = climberTransform.position + Vector3.up * 0.5f;
        Vector3 outwardsDirection = Vector3.back;

        if (Physics.Raycast(baseOrigin, Vector3.down, out RaycastHit groundHit, 5f, mountainLayer))
            outwardsDirection = groundHit.normal.normalized;

        Vector3[] sideDirections = new Vector3[]
        {
            climberTransform.right,
            -climberTransform.right,
            Vector3.zero
        };

        foreach (Vector3 sideDir in sideDirections)
        {
            Vector3 targetPos = climberTransform.position
                              + outwardsDirection * distance
                              + sideDir * side
                              + Vector3.up * height;

            Vector3 dir = targetPos - headPosition;
            float dist = dir.magnitude;

            if (!Physics.SphereCast(headPosition, 0.5f, dir.normalized, out RaycastHit wallHit, dist, mountainLayer))
            {
                resultPosition = targetPos;
                resultRotation = Quaternion.LookRotation(headPosition - targetPos);
                return true;
            }
        }

        return false;
    }
}