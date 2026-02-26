using UnityEngine;

namespace ProceduralGeneration
{
	public static class BezierCurve
	{
        // Get point at time t (0 to 1)
        public static Vector3 GetPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1 - t;
            return u * u * u * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3;
        }

        // Get direction (tangent) at time t
        public static Vector3 GetDirection(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1 - t;
            return (3 * u * u * (p1 - p0) + 6 * u * t * (p2 - p1) + 3 * t * t * (p3 - p2)).normalized;
        }
    }
}