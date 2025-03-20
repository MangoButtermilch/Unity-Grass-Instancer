using UnityEngine;

namespace Acetix.Grass
{
    public class GrassUtils
    {
        public static bool IsInViewFrustum(Vector4[] viewFrustumPlanes, Vector3[] corners)
        {
            for (int i = 0; i < corners.Length; i++)
            {
                bool isInside = true;
                for (int j = 0; j < 6; j++)
                {
                    if (!IsInFrontOfPlane(viewFrustumPlanes[j], corners[i]))
                    {
                        isInside = false;
                        break;
                    }
                }
                if (isInside)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsInFrontOfPlane(Vector4 plane, Vector3 p)
        {
            return PlaneDistance(plane, p) > 0;
        }

        public static float PlaneDistance(Vector4 plane, Vector3 p)
        {
            Vector3 temp = new Vector3(plane.x, plane.y, plane.z);
            return Vector3.Dot(temp, p) + plane.w;
        }
    }
}