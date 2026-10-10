"""Render actual Blender geometry, with no bitmap compositing over the model."""
import bpy
import sys
import json
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Source/Blender/LihuaCat'
scene=bpy.data.scenes['LihuaCat_ReferenceWorkshop']
if bpy.context.window:
    bpy.context.window.scene=scene
view=sys.argv[sys.argv.index('--')+1] if '--' in sys.argv else 'threequarter'
all_views=view=='all'
if all_views:view='threequarter'
views={
    'threequarter':((1.1,1.55,.65),(0,-.04,.28),1.10),
    'front':((0,2,.34),(0,-.05,.29),1.00),
    'left':((-2,0,.34),(0,-.015,.29),1.05),
    'right':((2,0,.34),(0,-.015,.29),1.05),
    'face':((.24,1.8,.46),(0,.309,.343),.35),
}
if view not in views:
    raise ValueError(view)
# Correct the outward winding of the open eye patches in earlier draft files.
for obj in scene.objects:
    if obj.name.startswith(('Eye.','Pupil.')) and obj.type=='MESH':
        side=-1 if obj.name.endswith('.L') else 1
        outward=Vector((side*.43,.90,.06)).normalized()
        if sum(p.normal.dot(outward) for p in obj.data.polygons)<0:
            for polygon in obj.data.polygons:
                polygon.flip()
            obj.data.update()
camera=scene.camera
pos,target,scale=views[view]
camera.location=pos
camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.ortho_scale=scale
scene.render.engine='CYCLES'
scene.cycles.samples=40
scene.cycles.use_denoising=True
scene.cycles.adaptive_threshold=.04
device='CPU'
try:
    prefs=bpy.context.preferences.addons['cycles'].preferences
    prefs.compute_device_type='CUDA'
    prefs.get_devices()
    devices=[d for d in prefs.devices if d.type=='CUDA']
    if devices:
        for d in prefs.devices:
            d.use=d.type=='CUDA'
        scene.cycles.device='GPU'
        device=', '.join(d.name for d in devices)
except Exception as ex:
    print('GPU_FALLBACK',repr(ex))
scene.render.resolution_x=1600
scene.render.resolution_y=1100
scene.render.resolution_percentage=100
scene.render.filepath=str(OUT/(view+'.png'))
if view=='threequarter':
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'LihuaCat.blend'),compress=True)
bpy.ops.render.render(write_still=True)
result={'view':view,'render':scene.render.filepath,'device':device}
print('LIHUA_RENDER '+json.dumps(result))
if all_views:
    for view in ('front','left','right','face'):
        pos,target,scale=views[view]
        camera.location=pos
        camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler()
        camera.data.ortho_scale=scale
        scene.render.filepath=str(OUT/(view+'.png'))
        bpy.ops.render.render(write_still=True)
        print('LIHUA_RENDER '+json.dumps({'view':view,'render':scene.render.filepath,'device':device}))
