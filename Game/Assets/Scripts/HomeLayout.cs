using UnityEngine;
namespace CatSimulator
{
    public sealed class HomeLayout : MonoBehaviour
    {
        public float horizontalExpansion=1.18f;
        public int revision=3;
        public Vector3 FromOriginal(Vector3 point)=>new Vector3(point.x*horizontalExpansion,point.y,point.z*horizontalExpansion);
    }
}
