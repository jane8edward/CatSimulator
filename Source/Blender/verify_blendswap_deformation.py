"""Compare original post-skin subdivision with the converted game skin."""
import bpy
import json
import numpy as np
from pathlib import Path

ROOT=Path('E:/Game/game_develop/CatSimulator')
OUT=ROOT/'Source/Blender/BlendSwap18519'
rig=bpy.data.objects['Armature'];body=bpy.data.objects['Cat']
for track in rig.animation_data.nla_tracks:track.mute=True
for modifier in body.modifiers:
    if modifier.type=='SUBSURF':modifier.levels=2
    if modifier.type=='PARTICLE_SYSTEM':modifier.show_viewport=False
arm=next(m for m in body.modifiers if m.type=='ARMATURE')
print('ORIGINAL_ARMATURE',arm.use_deform_preserve_volume,flush=True)
source={};source_matrices={}
for name,action,frames in [('Walk','Walk',range(1,29)),('Run','run',range(1,16))]:
    rig.animation_data.action=bpy.data.actions[action]
    for pb in rig.pose.bones:pb.matrix_basis.identity()
    for frame in frames:
        bpy.context.scene.frame_set(frame);bpy.context.view_layer.update()
        deps=bpy.context.evaluated_depsgraph_get();evaluated=body.evaluated_get(deps)
        mesh=evaluated.to_mesh()
        points=np.zeros(len(mesh.vertices)*3,dtype=np.float32)
        mesh.vertices.foreach_get('co',points)
        source[name,frame]=points.reshape(-1,3)
        evaluated.to_mesh_clear()
        source_matrices[name,frame]={b.name:np.array(b.matrix) for b in rig.evaluated_get(deps).pose.bones}
bpy.ops.wm.open_mainfile(filepath=str(OUT/'Cat_Game.blend'),load_ui=False,use_scripts=False)
rig=bpy.data.objects['BS18519_Rig'];body=bpy.data.objects['BS18519_Body']
arm=next(m for m in body.modifiers if m.type=='ARMATURE')
print('CONVERTED_ARMATURE',arm.use_deform_preserve_volume,flush=True)
report=[]
for linear in [False,True]:
    if linear:arm.use_deform_preserve_volume=False
    for (name,frame),points in source.items():
        rig.animation_data.action=bpy.data.actions[name]
        bpy.context.scene.frame_set(frame);bpy.context.view_layer.update()
        deps=bpy.context.evaluated_depsgraph_get();evaluated=body.evaluated_get(deps)
        mesh=evaluated.to_mesh()
        positions=np.zeros(len(mesh.vertices)*3,dtype=np.float32)
        mesh.vertices.foreach_get('co',positions)
        positions=positions.reshape(-1,3)
        scale=json.loads((OUT/'conversion-report.json').read_text(encoding='utf-8'))['scale']
        error=np.linalg.norm(positions-points,axis=1)*scale
        worst=int(np.argmax(error))
        matrices={b.name:np.array(b.matrix) for b in rig.evaluated_get(deps).pose.bones}
        bones=sorted([(n,float(np.max(np.abs(matrices[n]-m)))) for n,m in source_matrices[name,frame].items()],key=lambda p:-p[1])
        bind=np.array([v.co[:] for v in body.data.vertices])
        back=(bind[:,2]>.3)&(bind[:,1]>-1)&(bind[:,1]<2.8)&(np.abs(bind[:,0])<1.4)
        item={'linear':linear,'clip':name,'frame':frame,'max_m':float(error.max()),'p95_m':float(np.quantile(error,.95)),
              'back_max_m':float(error[back].max()),'back_p95_m':float(np.quantile(error[back],.95)),
              'mean_m':float(error.mean()),'worst_vertex':worst,'worst_bind_position':list(body.data.vertices[worst].co),
              'worst_weights':[(body.vertex_groups[g.group].name,g.weight) for g in body.data.vertices[worst].groups],
              'matrix_error':bones[:8]}
        report.append(item)
        print('DEFORM',json.dumps(item),flush=True)
        if name=='Walk' and frame==7 and linear:
            np.savez_compressed(OUT/'walk-deformation.npz',original=points,converted=positions,errors=error)
        evaluated.to_mesh_clear()
(OUT/'deformation-comparison.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
