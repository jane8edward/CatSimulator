using System.Collections;
using UnityEngine;
using System.Linq;

namespace CatSimulator
{
    public sealed class WaterSpill : MonoBehaviour
    {
        public static void Create(Vector3 source, Material material)
        {
            var root = new GameObject("SpilledWater");
            root.transform.position = source;
            var effect = root.AddComponent<WaterSpill>();
            effect.StartCoroutine(effect.Spill(material));
        }

        IEnumerator Spill(Material material)
        {
            var particles = gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.duration = .65f;
            main.loop = false;
            main.startLifetime = .8f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(.35f, .9f);
            main.startSize = new ParticleSystem.MinMaxCurve(.014f, .036f);
            main.gravityModifier = .8f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission;
            emission.rateOverTime = 95;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 45;
            shape.radius = .06f;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.material = material;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 1.1f;
            var collision = particles.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.bounce = .05f;
            collision.lifetimeLoss = .45f;
            particles.Play();
            yield return new WaitForSeconds(.35f);
            for (var index = 0; index < 2; index++)
            {
                var origin = transform.position + new Vector3(index * .08f, .2f, index * -.4f);
                var hits=Physics.RaycastAll(origin,Vector3.down,3,~(1<<8)).Where(h=>h.collider.attachedRigidbody==null).OrderBy(h=>h.distance).ToArray();
                if(hits.Length==0) continue;
                var hit=hits[0];var puddle = new GameObject("WetSurface");
                puddle.transform.position = hit.point + Vector3.up * .002f;
                var mesh = new Mesh();
                var vertices = new Vector3[34];
                var triangles = new int[96];
                var uv = new Vector2[34];
                uv[0] = new Vector2(.5f, .5f);
                for (var ring = 0; ring <= 32; ring++)
                {
                    var angle = ring * Mathf.PI / 16;
                    var radius = .36f + Mathf.Sin(angle * 5 + index) * .04f + Mathf.Cos(angle * 3) * .055f;
                    vertices[ring + 1] = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius * .7f);
                    uv[ring + 1] = new Vector2(vertices[ring + 1].x + .5f, vertices[ring + 1].z + .5f);
                    if (ring < 32)
                    { triangles[ring * 3] = 0; triangles[ring * 3 + 1] = ring + 2; triangles[ring * 3 + 2] = ring + 1; }
                }
                mesh.vertices = vertices;
                mesh.triangles = triangles;
                mesh.uv = uv;
                mesh.RecalculateNormals();
                puddle.AddComponent<MeshFilter>().sharedMesh = mesh;
                puddle.AddComponent<MeshRenderer>().sharedMaterial = material;
                var start = .05f;
                while (start < 1)
                {
                    start += Time.deltaTime * 1.4f;
                    puddle.transform.localScale = Vector3.one * Mathf.Min(start, 1);
                    yield return null;
                }
            }
        }
    }
}
