using System.Collections;
using UnityEngine;

namespace CatSimulator
{
    // Authored scene furniture shares the existing E interaction and objective flow.
    public sealed class HouseTaskInteractable : CatInteractable
    {
        public enum Kind { Laptop, Slippers, Phone, Food }
        public Kind kind;
        public Transform actionPosition, focus;
        public GameObject intact, damaged, effect;
        public Renderer[] wetRenderers;
        public Material wetMaterial, sparkMaterial;
        public Collider intactCollider;
        public Mesh damagedMesh;
        public bool EffectApplied {get;private set;}
        public int SparkBursts {get;private set;}
        public override bool Available => kind==Kind.Food || !performed;
        public override IEnumerator Perform(CatController cat)
        {
            performed=true;
            yield return cat.MoveTo(actionPosition.position,.7f,kind==Kind.Laptop||kind==Kind.Phone?.25f:0);
            var facing=Vector3.ProjectOnPlane(focus.position-cat.transform.position,Vector3.up);
            if(kind==Kind.Slippers)facing=-facing;
            if(facing.sqrMagnitude>.0001f)cat.transform.rotation=Quaternion.LookRotation(facing);
            cat.HouseAction=kind==Kind.Slippers?"Squat":kind==Kind.Food?"Eat":"Bite";
            cat.HouseActionTarget=focus.position;
            float elapsed=0, duration=kind==Kind.Slippers?2.3f:kind==Kind.Food?2:1.15f;
            if(kind==Kind.Slippers)StartCoroutine(UrineStream(cat,duration));
            while(elapsed<duration)
            {
                elapsed+=Time.deltaTime;cat.HouseActionProgress=Mathf.Clamp01(elapsed/duration);
                if(elapsed>duration*.45f && !EffectApplied)ApplyEffect();
                yield return null;
            }
            cat.HouseAction=null;
            session.CompleteObjective(interactionId);
            if(kind==Kind.Food){session.Discover(interactionId);EffectApplied=false;}
        }
        IEnumerator UrineStream(CatController cat,float duration)
        {
            yield return new WaitForSeconds(.35f);
            var stream=new GameObject("UrineStream").AddComponent<LineRenderer>();stream.sharedMaterial=sparkMaterial;
            stream.widthMultiplier=.0025f;stream.positionCount=5;stream.numCapVertices=2;
            stream.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            for(float t=.35f;t<duration-.3f;t+=Time.deltaTime)
            {
                var from=cat.transform.TransformPoint(new Vector3(0,.10f,-.13f)*cat.BodyScale);var to=focus.position;to.y=.025f;
                for(int i=0;i<5;i++){float k=i/4f;stream.SetPosition(i,Vector3.Lerp(from,to,k)+Vector3.up*(Mathf.Sin(k*Mathf.PI)*.015f));}
                yield return null;
            }
            Destroy(stream.gameObject);
        }
        void ApplyEffect()
        {
            EffectApplied=true;
            if(intact)intact.SetActive(false);
            if(damaged)damaged.SetActive(true);
            if(intactCollider is MeshCollider mesh && damagedMesh)mesh.sharedMesh=damagedMesh;
            if(kind==Kind.Slippers)
            {
                foreach(var renderer in wetRenderers)renderer.sharedMaterial=wetMaterial;
                if(effect){effect.SetActive(true);StartCoroutine(GrowPuddle());}
            }
            if(kind==Kind.Phone)
            {
                SparkBursts++;
                var sparks=new GameObject("CableCutSparks");sparks.transform.position=focus.position;
                var ps=sparks.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=ps.main;main.loop=false;main.duration=.25f;main.startLifetime=new ParticleSystem.MinMaxCurve(.12f,.38f);
                main.startSpeed=new ParticleSystem.MinMaxCurve(.25f,1.1f);main.startSize=new ParticleSystem.MinMaxCurve(.003f,.012f);
                main.startColor=new ParticleSystem.MinMaxGradient(new Color(1,.5f,.08f),new Color(1,1,.65f));main.gravityModifier=.6f;
                var emission=ps.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,26)});
                var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.008f;
                var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=sparkMaterial;
                renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=2;renderer.velocityScale=.03f;
                ps.Play();Destroy(sparks,1);
                if(effect)effect.SetActive(false); // Charging indicator goes out with the severed cable.
            }
            if(kind==Kind.Food && effect)StartCoroutine(EatFood());
        }
        IEnumerator GrowPuddle()
        {
            for(float t=0;t<1;t+=Time.deltaTime)
            {foreach(Transform patch in effect.transform)patch.localScale=Vector3.one*Mathf.Lerp(.1f,1,t);yield return null;}
            foreach(Transform patch in effect.transform)patch.localScale=Vector3.one;
        }
        IEnumerator EatFood()
        {
            int index=0;foreach(Transform piece in effect.transform)
            {piece.gameObject.SetActive(index++%3!=0);yield return new WaitForSeconds(.07f);}
        }
    }
}
