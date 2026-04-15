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

        public Vector3 GetUnitVector(Axis axis)
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

        public Vector3 GetLocalVector(Axis axis, Transform t)
		{
            return axis switch
            {
                Axis.X => t.right,
                Axis.Y => t.up,
                Axis.Z => t.forward,
                Axis.NegX => -t.right,
                Axis.NegY => -t.up,
                Axis.NegZ => -t.forward,
                _ => t.forward
            };
		}

        public Vector3 GetForwardDirection(Transform t)
		{
            return GetLocalVector(forwardAxis, t);
		}

        public Vector3 GetUpDirection(Transform t)
        {
            return GetLocalVector(upAxis, t);
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

            Vector3 localForward = settings.GetUnitVector(settings.forwardAxis);
            Vector3 localUp = settings.GetUnitVector(settings.upAxis);

            return Quaternion.LookRotation(worldForward, worldUp) * Quaternion.Inverse(Quaternion.LookRotation(localForward, localUp));
        }
    }
}

