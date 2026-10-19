using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CatSimulator
{
    // A floor grid supplies room routes; swept physical clearance remains authoritative.
    [DefaultExecutionOrder(20)]
    public sealed class RobotVacuum : MonoBehaviour
    {
        public const float Radius=.18f, Height=.055f;
        const float Cell=.12f;
        public float speed=.45f, turnSpeed=110;
        public bool IsTurning { get; private set; }
        public bool Ready { get; private set; }
        public CatController Rider { get; private set; }
        public string Status { get; private set; }="正在规划路线";
        public int RoomsVisited => visited.Count;
        public string VisitedRoomNames => string.Join(",",visited);
        public string ReachableRoomNames {get;private set;}
        public string LastMotionBlocker {get;private set;}
        public Vector3 CurrentWaypoint => path.Count>0?path[0]:transform.position;
        public Vector3 Seat => transform.position+Vector3.up*(Height+.009f);
        public bool CanMount => Ready && !Rider && cat && !cat.Paused && !cat.IsBusy &&
            cat.SupportVehicle==this && Clear(transform.position,true);
        public bool HasPassenger => Rider || (cat && cat.SupportVehicle==this);
        float PassengerRadius => HasPassenger?Mathf.Max(.245f,Vector3.ProjectOnPlane(cat.transform.position-transform.position,Vector3.up).magnitude+cat.BodyFrontReach):Radius;
        public LevelDefinition definition;
        public Vector3 ParkedPosition {get;private set;}
        Vector3 sittingOffset;
        Quaternion sittingRotation;
        CatController cat;
        LevelDefinition level;
        Rigidbody body;
        BoxCollider clearanceProbe;
        readonly Collider[] overlaps=new Collider[128];
        readonly RaycastHit[] hits=new RaycastHit[128];
        readonly RaycastHit[] groundHits=new RaycastHit[32];
        string currentBlocker;
        readonly List<Vector3> path=new List<Vector3>();
        readonly HashSet<string> visited=new HashSet<string>();
        bool[] free;
        float[] elevations;
        int width,depth,roomCursor;
        Vector3 origin;
        float retryAt,blockedSince=-1;
        bool planning;
        float surveyedAt;
        bool surveyedMounted;

        IEnumerator Start()
        {
            cat=FindFirstObjectByType<CatController>();level=definition;
            body=GetComponent<Rigidbody>();
            clearanceProbe=gameObject.AddComponent<BoxCollider>();clearanceProbe.enabled=false;
            ParkedPosition=transform.position;
            yield return Initialize();
        }
        IEnumerator Initialize()
        {
            yield return null;
            var bounds=level.activityBounds;origin=new Vector3(bounds.min.x,0,bounds.min.z);
            width=Mathf.CeilToInt(bounds.size.x/Cell);depth=Mathf.CeilToInt(bounds.size.z/Cell);free=new bool[width*depth];elevations=new float[free.Length];
            yield return Survey();
            if(!Clear(transform.position,false,true)){Status="停放位置通道受阻";Debug.LogError(Status+": "+currentBlocker);yield break;}
            Ready=true;
            yield return Plan();
        }
        Vector3 Point(int index)=>origin+new Vector3((index%width+.5f)*Cell,elevations==null?.008f:elevations[index],(index/width+.5f)*Cell);
        int Nearest(Vector3 point)
        {
            int x=Mathf.Clamp(Mathf.RoundToInt((point.x-origin.x)/Cell-.5f),0,width-1);
            int z=Mathf.Clamp(Mathf.RoundToInt((point.z-origin.z)/Cell-.5f),0,depth-1);return z*width+x;
        }
        bool Ignored(Collider c,bool includeCat)
        {
            if(!c || c.transform.IsChildOf(transform))return true;
            // Thin static mats and door tracks are rolling support, not walls.
            if(!c.attachedRigidbody && c.bounds.max.y<=.065f && c.bounds.min.y>=-.2f)return true;
            var player=c.GetComponentInParent<CatController>();
            return player && (!includeCat || player==Rider || player.SupportVehicle==this);
        }
        float SupportHeight(Vector3 p)
        {
            float highest=.0025f;
            for(int probe=0;probe<5;probe++)
            {
                var offset=probe==0?Vector3.zero:probe==1?Vector3.right:probe==2?Vector3.left:probe==3?Vector3.forward:Vector3.back;
                var from=p+offset*(Radius+.06f);from.y=.20f;
                int count=Physics.RaycastNonAlloc(from,Vector3.down,hits,.35f,~(1<<8),QueryTriggerInteraction.Ignore);
                for(int i=0;i<count;i++)
                {
                    var hit=hits[i];if(hit.collider.transform.IsChildOf(transform)||hit.collider.attachedRigidbody)continue;
                    bool thin=!hit.collider.attachedRigidbody&&hit.collider.bounds.max.y<=.065f;
                    if(hit.normal.y>(thin?.1f:.9f) && hit.point.y<=.065f)highest=Mathf.Max(highest,hit.point.y);
                }
            }
            return highest+.008f;
        }
        bool Clear(Vector3 p,bool mounted,bool includeCat=false,Vector3? escapeFrom=null)
        {
            var bounds=level.activityBounds;
            if(p.x<bounds.min.x+Radius+.012f||p.x>bounds.max.x-Radius-.012f||p.z<bounds.min.z+Radius+.012f||p.z>bounds.max.z-Radius-.012f)
            {currentBlocker="activity boundary";return false;}
            int groundCount=Physics.RaycastNonAlloc(p+Vector3.up*.10f,Vector3.down,groundHits,.22f,~(1<<8),QueryTriggerInteraction.Ignore);
            int nearest=-1;float groundDistance=float.PositiveInfinity;
            for(int i=0;i<groundCount;i++)if(!groundHits[i].collider.transform.IsChildOf(transform)&&groundHits[i].distance<groundDistance)
            {nearest=i;groundDistance=groundHits[i].distance;}
            if(nearest<0||groundCount==groundHits.Length){currentBlocker="no floor support";return false;}
            var floor=groundHits[nearest];
            bool thinSupport=!floor.collider.attachedRigidbody&&floor.collider.bounds.max.y<=.065f;
            if(floor.normal.y<(thinSupport?.1f:.9f) || Mathf.Abs(floor.point.y-p.y)>.08f){currentBlocker="floor height: "+floor.collider.name;return false;}
            float top=mounted?Height+.40f:Height;
            float radius=mounted?PassengerRadius:Radius;
            var half=new Vector3(radius+.012f,(top-.025f)*.5f,radius+.012f);
            int count=Physics.OverlapBoxNonAlloc(p+Vector3.up*(top+.025f)*.5f,half,overlaps,Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
            if(count==overlaps.Length)return false;
            for(int i=0;i<count;i++)if(!Ignored(overlaps[i],includeCat) &&
                !(escapeFrom.HasValue&&Escaping(overlaps[i],escapeFrom.Value,p,mounted)))
            {currentBlocker=overlaps[i].name;return false;}
            return true;
        }
        bool Escaping(Collider other,Vector3 from,Vector3 to,bool mounted)
        {
            float top=mounted?Height+.40f:Height, radius=mounted?PassengerRadius:Radius;
            clearanceProbe.center=Vector3.up*(top+.025f)*.5f;
            clearanceProbe.size=new Vector3(2*(radius+.012f),top-.025f,2*(radius+.012f));
            if(!Physics.ComputePenetration(clearanceProbe,from,Quaternion.identity,other,other.transform.position,other.transform.rotation,out var outward,out float before))return false;
            if(Vector3.Dot(to-from,outward)<=0)return false;
            return !Physics.ComputePenetration(clearanceProbe,to,Quaternion.identity,other,other.transform.position,other.transform.rotation,out _,out float after)||after<before-.000001f;
        }
        bool Sweep(Vector3 from,Vector3 to,bool mounted,bool includeCat)
        {
            var delta=to-from;float distance=delta.magnitude;if(distance<.00001f)return true;
            float top=mounted?Height+.40f:Height;
            float radius=mounted?PassengerRadius:Radius;
            int count=Physics.BoxCastNonAlloc(from+Vector3.up*(top+.025f)*.5f,
                new Vector3(radius+.012f,(top-.025f)*.5f,radius+.012f),delta/distance,hits,Quaternion.identity,distance,~0,QueryTriggerInteraction.Ignore);
            if(count==hits.Length)return false;
            for(int i=0;i<count;i++)if(!Ignored(hits[i].collider,includeCat) &&
                !(hits[i].distance<.0001f&&Escaping(hits[i].collider,from,to,mounted)))
            {currentBlocker=hits[i].collider.name;return false;}
            return Clear(to,mounted,includeCat,from);
        }
        IEnumerator Survey()
        {
            float slice=Time.realtimeSinceStartup;
            for(int i=0;i<free.Length;i++)
            {
                elevations[i]=SupportHeight(Point(i));free[i]=Clear(Point(i),HasPassenger,true);
                if(Time.realtimeSinceStartup-slice>.003f){yield return null;slice=Time.realtimeSinceStartup;}
            }
            surveyedAt=Time.time;surveyedMounted=HasPassenger;
        }
        IEnumerator Plan(bool refresh=false)
        {
            if(planning)yield break;planning=true;path.Clear();Status="正在规划路线";
            if(refresh || surveyedMounted!=(HasPassenger) || Time.time-surveyedAt>20)yield return Survey();
            int start=Nearest(transform.position);
            if(!free[start] || !Sweep(transform.position,Point(start),HasPassenger,true))
            {
                start=-1;float best=1;
                for(int i=0;i<free.Length;i++)if(free[i])
                {
                    float distance=(Point(i)-transform.position).sqrMagnitude;
                    if(distance<best&&Sweep(transform.position,Point(i),HasPassenger,true)){best=distance;start=i;}
                }
                if(start<0){planning=false;retryAt=Time.time+.75f;Status="等待身边通道恢复";yield break;}
            }
            var parent=new int[free.Length];
            for(int i=0;i<parent.Length;i++)parent[i]=-2;
            var queue=new Queue<int>();queue.Enqueue(start);parent[start]=-1;
            var reachable=new HashSet<string>();
            while(queue.Count>0)
            {
                int n=queue.Dequeue(),x=n%width,z=n/width;
                reachable.Add(level.LocationAt(Point(n)));
                for(int d=0;d<4;d++)
                {
                    int nx=x+(d==0?1:d==1?-1:0),nz=z+(d==2?1:d==3?-1:0);
                    if(nx<0||nz<0||nx>=width||nz>=depth)continue;
                    int k=nz*width+nx;if(parent[k]!=-2||!free[k])continue;parent[k]=n;queue.Enqueue(k);
                }
            }
            ReachableRoomNames=string.Join(",",reachable);
            int target=-1;
            for(int attempt=0;attempt<level.areas.Length;attempt++)
            {
                var area=level.areas[roomCursor++%level.areas.Length];float best=float.PositiveInfinity;
                if(area.name==level.LocationAt(transform.position))continue;
                for(int i=0;i<free.Length;i++)if(parent[i]!=-2 && free[i] && area.bounds.Contains(Point(i)+Vector3.up*.15f))
                {float d=(Point(i)-area.bounds.center).sqrMagnitude;if(d<best){best=d;target=i;}}
                if(target>=0)break;
            }
            // A temporarily closed door must not stall cleaning the current room.
            if(target<0)
            {
                float far=0;for(int i=0;i<free.Length;i++)if(parent[i]!=-2 && free[i])
                {float d=(Point(i)-transform.position).sqrMagnitude;if(d>far){far=d;target=i;}}
            }
            if(target>=0)
            {
                var reverse=new List<Vector3>();for(int n=target;n>=0;n=parent[n])reverse.Add(Point(n));reverse.Reverse();
                for(int i=0;i<reverse.Count;i++)
                {
                    if(i>0&&i<reverse.Count-1 && Vector3.Dot((reverse[i]-reverse[i-1]).normalized,(reverse[i+1]-reverse[i]).normalized)>.999f)continue;
                    path.Add(reverse[i]);
                }
            }
            planning=false;retryAt=Time.time+1;Status=path.Count>0?"巡扫中":"等待通道恢复";
        }
        void Update()
        {
            if(!Ready||!cat||cat.Paused||ContentPreviewMenu.GameplayInputBlocked)return;
            if(!cat.Automated && Input.GetKeyDown(KeyCode.F))
            {if(Rider)TryDismount();else if(CanMount)TryMount();}
        }
        void FixedUpdate()
        {
            if(!Ready||!cat||cat.Paused||ContentPreviewMenu.GameplayInputBlocked||Time.timeScale<=0)return;
            string room=level.LocationAt(transform.position);
            bool known=false;foreach(var area in level.areas)if(area.name==room){known=true;break;}
            if(known&&visited.Add(room))Debug.Log("ROBOT_ROOM "+room+" position="+transform.position);
            if(planning)return;
            if(path.Count==0){IsTurning=false;if(Time.time>=retryAt)StartCoroutine(Plan());return;}
            var delta=path[0]-transform.position;delta.y=0;
            if(delta.magnitude<.00005f){path.RemoveAt(0);return;}
            var facing=Quaternion.LookRotation(delta);
            IsTurning=Quaternion.Angle(transform.rotation,facing)>.5f;
            if(IsTurning)
            {
                Status="原地转向";body.MoveRotation(Quaternion.RotateTowards(body.rotation,facing,turnSpeed*Time.fixedDeltaTime));return;
            }
            float step=speed*Time.fixedDeltaTime;
            var next=delta.magnitude<=step?transform.position+delta:transform.position+transform.forward*step;
            next.y=Mathf.MoveTowards(transform.position.y,SupportHeight(next),.5f*Time.fixedDeltaTime);
            if(!Sweep(transform.position,next,HasPassenger,true))
            {
                LastMotionBlocker=currentBlocker;
                Status="前方有障碍，等待绕行";
                if(blockedSince<0)blockedSince=Time.time;
                if(Time.time-blockedSince>.45f){blockedSince=-1;StartCoroutine(Plan(true));}return;
            }
            blockedSince=-1;Status="巡扫中";body.MovePosition(next);
        }
        void LateUpdate()
        {
            if(Rider)Rider.transform.SetPositionAndRotation(transform.TransformPoint(sittingOffset),transform.rotation*sittingRotation);
        }
        public bool TryMount()
        {
            if(!CanMount)return false;
            sittingOffset=transform.InverseTransformPoint(cat.transform.position);
            sittingRotation=Quaternion.Inverse(transform.rotation)*cat.transform.rotation;
            Rider=cat;cat.SetRide(this);
            if(!planning)StartCoroutine(Plan());return true;
        }
        // F changes posture on the platform. Walking or jumping leaves it naturally.
        public bool TryDismount()
        {
            if(!Rider||cat.Paused)return false;
            var passenger=Rider;Rider=null;passenger.SetRide(null);
            return true;
        }
        public void ReleaseForTeleport(){if(!Rider)return;var passenger=Rider;Rider=null;passenger.SetRide(null);}
        void OnDestroy(){ReleaseForTeleport();}
    }
}
