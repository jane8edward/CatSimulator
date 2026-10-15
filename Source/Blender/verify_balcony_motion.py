"""Contact, continuity and tail checks for the adapted balcony character."""
import bpy,json,math
from pathlib import Path
from mathutils import Vector
ROOT=Path('E:/Game/game_develop/CatSimulator');OUT=ROOT/'Source/Verification/CatPolish031';OUT.mkdir(parents=True,exist_ok=True)
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');body=next(o for o in bpy.context.scene.objects if o.type=='MESH')
report={'passed':True,'clips':[]}
for clip in ['Idle','Walk','Run','Jump','Land','Paw','Scratch']:
 action=bpy.data.actions[clip];rig.animation_data.action=action;frames=int(action.frame_range.y)
 entry={'name':clip,'frames':frames,'minVertexZ':100,'maxExtent':0,'maxElbowStep':0,'maxWristStep':0,'minimumPawSeparation':100,'minTailRise':100,'maxTailRise':-100,'maxPawBank':0}
 samples=[]
 for f in range(1,frames+1):
  for sub in ([0,.5] if clip in ['Walk','Run'] and f<frames else [0]):
   bpy.context.scene.frame_set(f,subframe=sub);bpy.context.view_layer.update()
   ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh();points=[ev.matrix_world@v.co for v in mesh.vertices];ev.to_mesh_clear()
   entry['minVertexZ']=min(entry['minVertexZ'],min(v.z for v in points));entry['maxExtent']=max(entry['maxExtent'],max(max(v[i] for v in points)-min(v[i] for v in points) for i in range(3)))
   if sub:continue
   joints={s:[rig.matrix_world@rig.pose.bones[f'tripo::0_{s}_Limb_{i}'].head for i in range(4)] for s in ['Left','Right']}
   entry['minimumPawSeparation']=min(entry['minimumPawSeparation'],joints['Right'][2].x-joints['Left'][2].x)
   base=rig.matrix_world@rig.pose.bones['bone_45'].head;tip=rig.matrix_world@rig.pose.bones['bone_49'].tail
   entry['minTailRise']=min(entry['minTailRise'],tip.z-base.z);entry['maxTailRise']=max(entry['maxTailRise'],tip.z-base.z)
   for s in ['Left','Right']:
    n=f'tripo::0_{s}_Limb_2';delta=(rig.matrix_world@rig.pose.bones[n].matrix).to_quaternion()@(rig.matrix_world@rig.data.bones[n].matrix_local).to_quaternion().inverted()
    entry['maxPawBank']=max(entry['maxPawBank'],abs((delta@Vector((0,0,1))).x))
   samples.append(joints)
 for a,b in zip(samples,samples[1:]):
  for s in ['Left','Right']:
   entry['maxElbowStep']=max(entry['maxElbowStep'],(b[s][1]-a[s][1]).length)
   entry['maxWristStep']=max(entry['maxWristStep'],(b[s][2]-a[s][2]).length)
 entry['loopStep']=max((samples[-1][s][j]-samples[0][s][j]).length for s in ['Left','Right'] for j in [1,2])
 entry['passed']=math.isfinite(entry['maxExtent']) and entry['maxExtent']<1 and entry['minVertexZ']>-.003
 if clip in ['Walk','Run']:
  entry['passed'] &= entry['maxElbowStep']<(.045 if clip=='Walk' else .07) and entry['loopStep']<.0005 and entry['maxPawBank']<.02 and entry['minimumPawSeparation']>.02
 if clip in ['Idle','Walk']:entry['passed'] &= entry['minTailRise']>.12
 report['passed'] &= entry['passed'];report['clips'].append(entry)
(OUT/'blender-motion.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('MOTION_CONTINUITY',json.dumps(report),flush=True)
if not report['passed']:raise RuntimeError('Motion continuity/contact check failed')
