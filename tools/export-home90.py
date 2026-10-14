"""Validate native CatHome90 assets and export a baked Unity preview, preserving sources.

Run: blender --background --python tools/export-home90.py
"""
import bpy
import json
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
HOME = REPO / 'Source/Blender/CatHome90'
SOURCE = HOME / '04_场景工程/Blender/Layout/H90_Residence_v001.blend'
DEST = REPO / 'Game/Assets/Art/CatHome90'
REPORT = REPO / 'Source/Verification/CatHome90'
DEST.mkdir(parents=True, exist_ok=True)
REPORT.mkdir(parents=True, exist_ok=True)

# Read each native library without rebuilding or overwriting artist assets.
native = sorted((HOME / '02_资产库').glob('*/H90-*/Source/*_native.blend'))
assert len(native) == 84, f'Expected 84 native assets, found {len(native)}'
for path in native:
    with bpy.data.libraries.load(str(path), link=False) as (data, _):
        assert data.objects, f'Empty asset: {path.name}'

bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
scene = bpy.data.scenes['H90_Residence_Main']
bpy.context.window.scene = scene
arrays = sum(m.type == 'ARRAY' for o in scene.objects for m in o.modifiers)
instances = sum(o.instance_type == 'COLLECTION' for o in scene.objects)
missing = [i.name for i in bpy.data.images if i.source == 'FILE' and not i.packed_file
           and not Path(bpy.path.abspath(i.filepath)).is_file()]
assert not missing, f'Missing textures: {missing}'
assert arrays >= 22 and instances >= 51

# Presentation masks must not omit ceilings or complete walls from the engine asset.
for col in bpy.data.collections:
    col.hide_viewport = False
    col.hide_render = False
for obj in scene.objects:
    obj.hide_viewport = False
    obj.hide_render = False
    obj.hide_set(False)
bpy.context.view_layer.update()

palette = []
for mat in bpy.data.materials:
    p = next((n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None) if mat.use_nodes else None
    c = list(p.inputs['Base Color'].default_value) if p else list(mat.diffuse_color)
    entry = dict(name=mat.name, color=c, roughness=float(p.inputs['Roughness'].default_value) if p else .5,
                 metallic=float(p.inputs['Metallic'].default_value) if p else 0.)
    if mat.use_nodes:
        im = next((n.image for n in mat.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image), None)
        if im:
            tex = DEST / 'Textures/Calico_BaseColor.png'
            tex.parent.mkdir(exist_ok=True)
            im.filepath_raw = str(tex)
            im.file_format = 'PNG'
            im.save()
            entry['texture'] = 'Assets/Art/CatHome90/Textures/Calico_BaseColor.png'
    palette.append(entry)

# Evaluate modifiers and collection instances into a separate disposable export scene.
# Native .blend files retain the editable arrays and instancing.
deps = bpy.context.evaluated_depsgraph_get()
baked = []
cache = {}
for item in deps.object_instances:
    o = item.object
    if o.type not in {'MESH', 'CURVE', 'SURFACE', 'FONT'}:
        continue
    key = o.original.as_pointer()
    if key not in cache:
        mesh = bpy.data.meshes.new_from_object(o, preserve_all_data_layers=True, depsgraph=deps)
        # Converted curves need explicit slots; evaluated curve materials can be omitted.
        mesh.materials.clear()
        for slot in o.material_slots:
            if slot.material:
                mesh.materials.append(slot.material)
        for polygon in mesh.polygons:
            if polygon.material_index >= len(mesh.materials): polygon.material_index = 0
        cache[key] = mesh
    mesh = cache[key]
    if not mesh.vertices:
        continue
    baked.append((o.name, mesh, item.matrix_world.copy()))

export = bpy.data.scenes.new('H90_Unity_Export')
bpy.context.window.scene = export
export.unit_settings.system = 'METRIC'
export.unit_settings.scale_length = 1
for i, (name, mesh, transform) in enumerate(baked):
    obj = bpy.data.objects.new(f'{name}_E{i:04d}', mesh)
    export.collection.objects.link(obj)
    obj.matrix_world = transform
    obj.select_set(True)
bpy.context.view_layer.objects.active = next(iter(export.objects))
fbx = DEST / 'H90_Residence.fbx'
bpy.ops.export_scene.fbx(filepath=str(fbx), use_selection=True, object_types={'MESH'},
    use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False,
    axis_forward='-Z', axis_up='Y', apply_unit_scale=True, path_mode='RELATIVE')
(DEST / 'palette.json').write_text(json.dumps({'materials': palette}, ensure_ascii=False, indent=2))
report = dict(nativeAssets=len(native), arrays=arrays, collectionInstances=instances,
    missingTextures=missing, exportedMeshObjects=len(baked),
    uniqueMeshes=len(cache), vertices=sum(len(m.vertices) for m in cache.values()),
    engineFile=str(fbx.relative_to(REPO)), engineFileBytes=fbx.stat().st_size,
    sourcePreserved=True, units='metres',
    limitations=['Procedural wood, textile and glass shading is approximated with URP materials; texture baking and LOD remain future work.',
                  'This is a separate scene preview; existing gameplay has not been replaced.'])
(REPORT / 'blender-export-check.json').write_text(json.dumps(report, ensure_ascii=False, indent=2))
print('HOME90_EXPORT_PASS', json.dumps(report), flush=True)
