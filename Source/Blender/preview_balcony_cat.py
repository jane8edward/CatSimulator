import bpy,json
from pathlib import Path
from mathutils import Vector
ROOT=Path('E:/Game/game_develop/CatSimulator');OUT=ROOT/'.tools/balcony-cat/poses';OUT.mkdir(parents=True,exist_ok=True)
scene=bpy.context.scene;rig=next(o for o in scene.objects if o.type=='ARMATURE');body=next(o for o in scene.objects if o.type=='MESH')
scene.render.engine='CYCLES';scene.cycles.samples=12;scene.cycles.use_denoising=True
scene.render.resolution_x=760;scene.render.resolution_y=600;scene.render.resolution_percentage=100
scene.world.color=(.26,.26,.26);scene.use_nodes=False;scene.view_settings.view_transform='AgX'
d=bpy.data.cameras.new('Preview');cam=bpy.data.objects.new('Preview',d);scene.collection.objects.link(cam)
cam.location=(.8,1,.6);target=Vector((0,0,.20));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=.8;scene.camera=cam
for name,loc,energy,size in [('Key',(1,1,2),120,2),('Fill',(-1,0,1),70,2)]:
 d=bpy.data.lights.new(name,'AREA');d.energy=energy;d.shape='DISK';d.size=size;o=bpy.data.objects.new(name,d);scene.collection.objects.link(o);o.location=loc;o.rotation_euler=(target-o.location).to_track_quat('-Z','Y').to_euler()
scene.render.image_settings.file_format='PNG'
metrics={}
for name,frame in [('Idle',1),('Walk',8),('Walk',21),('Run',6),('Jump',10),('Paw',12),('Scratch',24)]:
 rig.animation_data.action=bpy.data.actions[name];scene.frame_set(frame);bpy.context.view_layer.update()
 ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh();points=[ev.matrix_world@v.co for v in mesh.vertices]
 metrics[name+str(frame)]={'min':[min(v[i] for v in points) for i in range(3)],'max':[max(v[i] for v in points) for i in range(3)]};ev.to_mesh_clear()
 scene.render.filepath=str(OUT/(name+str(frame)+'.png'));bpy.ops.render.render(write_still=True)
(OUT/'bounds.json').write_text(json.dumps(metrics,indent=2))
