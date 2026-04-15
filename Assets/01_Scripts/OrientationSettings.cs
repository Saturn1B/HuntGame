using UnityEngine;
using Sirenix.OdinInspector;

namespace HuntingGame
{
    [System.Serializable]
    public struct OrientationSettings
    {
        public enum Axis { X, Y, Z, NegX, NegY, NegZ };

        [EnumToggleButtons] public Axis forwardAxis;
        [EnumToggleButtons] public Axis upAxis;

        public Vector3 GetLocalVector(Axis axis)
        {
            return axis switch
            {
                Axis.X => Vector3.right,
                Axis.Y => Vector3.up,
                Axis.Z => Vector3.forward,
                Axis.NegX => Vector3.left,
                Axis.NegY => Vector3.down,
                Axis.NegZ => Vector3.back,
                _ => Vector3.forward
            };
        }
    }

    public static class RotationUtils
    {
        public static Quaternion GetCorrectedLookRotation(Vector3 worldForward, Vector3 worldUp, OrientationSettings settings)
        {
            if (worldForward == Vector3.zero) return Quaternion.identity;

            if (Mathf.Abs(Vector3.Dot(worldForward.normalized, worldUp.normalized)) > 0.99f)
            {
                worldUp = Vector3.Cross(worldForward, Vector3.right);
                if (worldUp == Vector3.zero) worldUp = Vector3.Cross(worldForward, Vector3.up);
            }

            Vector3 localForward = settings.GetLocalVector(settings.forwardAxis);
            Vector3 localUp = settings.GetLocalVector(settings.upAxis);

            return Quaternion.LookRotation(worldForward, worldUp) * Quaternion.Inverse(Quaternion.LookRotation(localForward, localUp));
        }
    }
}

