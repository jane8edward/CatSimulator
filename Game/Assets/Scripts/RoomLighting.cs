using UnityEngine;
using UnityEngine.Rendering;

namespace CatSimulator
{
    public sealed class RoomLighting : MonoBehaviour
    {
        void Awake()
        {
            // A consistent first-pass ambient probe until room lightmaps are authored.
            var ambient=new SphericalHarmonicsL2();
            ambient.AddAmbientLight(new Color(.26f,.235f,.20f));
            ambient.AddDirectionalLight(Vector3.up,new Color(.21f,.24f,.28f),.45f);
            RenderSettings.ambientProbe=ambient;
        }
    }
}
