"""Evaluate every adapted frame, independently of Unity's import validation."""
import bpy,json,math
from pathlib import Path
ROOT=Path('E:/Game/game_develop/CatSimulator')
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
body=next(o for o in bpy.context.scene.objects if o.type=='MESH')
report={'passed':True,'clips':[]}
for name in ['Idle','Walk','Run','Jump','Land','Paw','Scratch']:
    action=bpy.data.actions[name];rig.animation_data.action=action
    record={'name':name,'frames':int(action.frame_range.y),'minimumVertexZ':100,'maximumExtent':0,'finite':True}
    for frame in range(1,record['frames']+1):
        bpy.context.scene.frame_set(frame);bpy.context.view_layer.update()
        ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh()
        points=[ev.matrix_world@v.co for v in mesh.vertices]
        record['finite'] &= all(all(math.isfinite(c) for c in p) for p in points)
        low_index=min(range(len(points)),key=lambda i:points[i].z)
        if points[low_index].z<record['minimumVertexZ']:
            record['minimumVertexZ']=points[low_index].z;record['minimumFrame']=frame
            record['minimumWeights']=[(body.vertex_groups[g.group].name,g.weight) for g in body.data.vertices[low_index].groups]
        extent=max(max(p[i] for p in points)-min(p[i] for p in points) for i in range(3))
        record['maximumExtent']=max(record['maximumExtent'],extent);ev.to_mesh_clear()
    record['passed']=record['finite'] and record['maximumExtent']<1
    if name in ['Idle','Walk','Run']:record['passed'] &= record['minimumVertexZ']>-.003
    report['passed'] &= record['passed'];report['clips'].append(record)
destination=ROOT/'Source/Verification/HomeUpgrade030/balcony-deformation.json'
destination.write_text(json.dumps(report,indent=2),encoding='utf-8')
print('BALCONY_DEFORMATION',json.dumps(report),flush=True)
if not report['passed']:raise RuntimeError('Deformation validation failed')
