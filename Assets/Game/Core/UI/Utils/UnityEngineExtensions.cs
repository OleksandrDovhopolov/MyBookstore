using UnityEngine;

namespace Game.UI
{
    public static class UnityEngineExtensions
    {
        public static Vector3 ReplaceX(this Vector3 vec, float x)
        {
            vec.x = x;
            return vec;
        }

        public static Vector3 ReplaceY(this Vector3 vec, float y)
        {
            vec.y = y;
            return vec;
        }
        
        public static Vector3 ReplaceZ(this Vector3 vec, float z)
        {
            vec.z = z;
            return vec;
        }
    }
}
