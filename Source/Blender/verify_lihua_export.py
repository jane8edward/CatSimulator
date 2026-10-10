"""Reimport the real FBX in a clean Blender process to check scale and portability."""
import bpy
import json
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Source/Blender/LihuaCat/GameExport'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(OUT/'LihuaCat.fbx'))
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
rigs=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
body=next(o for o in meshes if o.name=='LihuaCat_Body')
bpy.context.view_layer.update()
points=[o.matrix_world@Vector(p) for o in meshes for p in o.bound_box]
minimum=[min(p[i] for p in points) for i in range(3)]
maximum=[max(p[i] for p in points) for i in range(3)]
size=[maximum[i]-minimum[i] for i in range(3)]
missing=[im.filepath for im in bpy.data.images if im.source=='FILE' and not im.packed_file and not Path(bpy.path.abspath(im.filepath)).exists()]
textures=[im.name for im in bpy.data.images if im.source=='FILE']
report={'status':'pass','fbx_meshes':len(meshes),'bones':sum(len(r.data.bones) for r in rigs),
        'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes),
        'dimensions_metres':size,'bounds_min':minimum,'bounds_max':maximum,
        'body_has_uv':bool(body.data.uv_layers),'body_has_armature_modifier':any(m.type=='ARMATURE' for m in body.modifiers),
        'body_unweighted_vertices':sum(not v.groups for v in body.data.vertices),
        'missing_image_files':missing,'texture_images':textures}
assert len(rigs)==1 and len(rigs[0].data.bones)==27
assert .45<size[2]<.70 and .50<size[1]<.95 and .10<size[0]<.50,report
assert report['body_has_uv'] and report['body_has_armature_modifier']
assert report['body_unweighted_vertices']==0 and not missing
assert len(textures)>=8
(OUT/'reimport-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('LIHUA_REIMPORT '+json.dumps(report))
