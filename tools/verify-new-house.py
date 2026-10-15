"""Verify the curated New_House sources and the newly supplied cat in Blender.

Run: blender --background --python tools/verify-new-house.py
Reports are written under Source/Verification/NewHouse, never to desktop originals.
"""
import bpy, json, math, runpy
from pathlib import Path
from mathutils import Vector

REPO = Path(__file__).resolve().parents[1]
ROOT = REPO / 'Source/Blender/New_House'
OUT = REPO / 'Source/Verification/NewHouse'
OUT.mkdir(parents=True, exist_ok=True)
runpy.run_path(str(ROOT/'06_脚本/validate_new_house.py'), run_name='__main__')
report = json.loads((ROOT/'00_管理/最终验收.json').read_text())
assert report['status'] == 'passed'
assert report['uniqueTripoAssets'] == 78 and report['readyAssets'] == 79
assert report['collectionInstances'] == 124
(OUT/'native-scene.json').write_text(json.dumps(report, ensure_ascii=False, indent=2))

bpy.ops.wm.read_factory_settings(use_empty=True)
source = next((REPO/'assets/Cats/CuteCat/Original').glob('*.fbx'))
bpy.ops.import_scene.fbx(filepath=str(source), use_image_search=True)
scene = bpy.context.scene
meshes = [o for o in scene.objects if o.type == 'MESH']
assert meshes, 'New cat FBX has no mesh'
missing = [im.name for im in bpy.data.images if im.source == 'FILE' and not im.packed_file
           and not Path(bpy.path.abspath(im.filepath)).is_file()]
assert not missing, missing
points = [o.matrix_world @ Vector(p) for o in meshes for p in o.bound_box]
lo = Vector(tuple(min(p[i] for p in points) for i in range(3)))
hi = Vector(tuple(max(p[i] for p in points) for i in range(3)))
center = (lo + hi) / 2
radius = max((hi-lo).length / 2, .1)
for o in meshes:
    if len(o.data.polygons) > 180000:
        mod = o.modifiers.new('Inspection detail budget only', 'DECIMATE')
        mod.ratio = 180000 / len(o.data.polygons)
world = bpy.data.worlds.new('VerificationWorld'); scene.world=world
world.use_nodes=True
next(n for n in world.node_tree.nodes if n.type=='BACKGROUND').inputs[1].default_value=.65
for name,offset,energy in [('Key',(3,-4,5),450),('Fill',(-3,-2,2),200)]:
    data=bpy.data.lights.new(name,'AREA');data.energy=energy*radius**2;data.size=radius*4
    light=bpy.data.objects.new(name,data);scene.collection.objects.link(light)
    light.location=center+Vector(offset)*radius
    light.rotation_euler=(center-light.location).to_track_quat('-Z','Y').to_euler()
data=bpy.data.cameras.new('VerificationCamera');camera=bpy.data.objects.new(data.name,data)
scene.collection.objects.link(camera);scene.camera=camera
camera.location=center+Vector((4,-6,3.2)).normalized()*radius*4
camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
data.type='ORTHO';data.ortho_scale=radius*2.6
scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=16;scene.cycles.use_denoising=True
scene.render.resolution_x=640;scene.render.resolution_y=640;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.filepath=str(OUT/'cute-cat-source-preview.png')
bpy.ops.render.render(write_still=True)
cat_report={'passed':True,'source':str(source.relative_to(REPO)),'meshObjects':len(meshes),
            'sourceFaces':sum(len(o.data.polygons) for o in meshes),'missingTextures':missing,
            'bounds':list(hi-lo),'preview':'Source/Verification/NewHouse/cute-cat-source-preview.png',
            'sourceModified':False,'scope':'Original source import, material availability and render; no gameplay rig added'}
(OUT/'cute-cat-source.json').write_text(json.dumps(cat_report,ensure_ascii=False,indent=2))
print('NEW_HOUSE_SOURCE_VERIFICATION_PASS', json.dumps(cat_report), flush=True)
