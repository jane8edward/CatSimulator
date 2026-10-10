"""Bake portable colour maps and export a separate mesh/rig asset.

The native Blender short-hair groom is intentionally not included in FBX.
This script works on the loaded copy and never overwrites LihuaCat.blend.
"""
import bpy
import json
from pathlib import Path
import bmesh

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Source/Blender/LihuaCat/GameExport'
TEX=OUT/'Textures'
TEX.mkdir(parents=True,exist_ok=True)
scene=bpy.data.scenes['LihuaCat_ReferenceWorkshop']
if bpy.context.window:bpy.context.window.scene=scene
scene.render.engine='CYCLES'
scene.cycles.samples=1
scene.cycles.device='CPU'
rig=bpy.data.objects['LihuaCat_Rig']
collection=bpy.data.collections['LihuaCat_Model']

def activate(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active=obj

original_objects=list(collection.objects)
export_objects=[]
for obj in original_objects:
    if obj.type=='CURVES':
        obj.hide_render=True
        obj.hide_viewport=True
        continue
    if obj.type=='CURVE':
        obj.data.resolution_u=4
        obj.data.bevel_resolution=0
        for spline in obj.data.splines:
            spline.resolution_u=4
        activate(obj)
        bpy.ops.object.convert(target='MESH')
        obj=bpy.context.object
        group=obj.vertex_groups.new(name=obj.get('deform_bone','Head'))
        group.add(list(range(len(obj.data.vertices))),1,'REPLACE')
        mod=obj.modifiers.new('Head skin','ARMATURE');mod.object=rig
    if obj.type in {'MESH','ARMATURE'}:
        export_objects.append(obj)
    if obj.type=='MESH':
        for mod in obj.modifiers:
            if mod.type=='SUBSURF':
                mod.levels=mod.render_levels=0

maps=[]
for name in ['LihuaCat_Body','LihuaCat_Tail','Ear.L','Ear.R','Eye.L','Eye.R','EyelidSkin.L','EyelidSkin.R']:
    obj=bpy.data.objects[name]
    activate(obj)
    if not obj.data.uv_layers:
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(angle_limit=1.1,island_margin=.014)
        bpy.ops.object.mode_set(mode='OBJECT')
    # Separate materials prevent one eye's atlas replacing the other eye's map.
    mat=obj.data.materials[0].copy()
    mat.name=name+'_BakedMaterial'
    obj.data.materials[0]=mat
    size=2048 if name=='LihuaCat_Body' else (1024 if name=='LihuaCat_Tail' else 512)
    image=bpy.data.images.new(name+'_Albedo',width=size,height=size,alpha=False)
    image.generated_color=(.10,.09,.07,1)
    node=mat.node_tree.nodes.new('ShaderNodeTexImage')
    node.image=image
    mat.node_tree.nodes.active=node
    for n in mat.node_tree.nodes:n.select=False
    node.select=True
    bpy.ops.object.bake(type='DIFFUSE',pass_filter={'COLOR'},margin=16,use_clear=True)
    filename=TEX/(name.replace('.','_')+'_Albedo.png')
    image.filepath_raw=str(filename)
    image.file_format='PNG'
    image.save()
    bsdf=mat.node_tree.nodes.get('Principled BSDF')
    mat.node_tree.links.new(node.outputs['Color'],bsdf.inputs['Base Color'])
    maps.append(str(filename))

bpy.ops.object.select_all(action='DESELECT')
for obj in export_objects:obj.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.context.view_layer.update()
export_file=OUT/'LihuaCat.fbx'
bpy.ops.export_scene.fbx(filepath=str(export_file),use_selection=True,
    object_types={'MESH','ARMATURE'},axis_forward='-Z',axis_up='Y',
    apply_unit_scale=True,add_leaf_bones=False,bake_anim=False,
    use_mesh_modifiers=True,mesh_smooth_type='FACE',path_mode='COPY',embed_textures=True)

# Validate the continuous body independently of deliberate open eye/ear patches.
body=bpy.data.objects['LihuaCat_Body']
bm=bmesh.new();bm.from_mesh(body.data)
nonmanifold=sum(not e.is_manifold for e in bm.edges)
bm.free()
unweighted=sum(not v.groups for v in body.data.vertices)
vertices=sum(len(o.data.vertices) for o in export_objects if o.type=='MESH')
triangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in export_objects if o.type=='MESH')
bpy.ops.file.make_paths_relative()
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'LihuaCat_Game.blend'),compress=True)
report={'fbx':str(export_file),'textures':maps,'mesh_vertices':vertices,
        'base_triangles_before_nonbody_modifiers':triangles,
        'body_nonmanifold_edges':nonmanifold,'body_unweighted_vertices':unweighted,
        'bones':len(rig.data.bones),'native_groom_in_fbx':False,
        'animation_validation':'Rest pose only. No walk cycle or Unity integration has been validated.'}
(OUT/'export-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('LIHUA_EXPORT '+json.dumps(report))
