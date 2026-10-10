import bpy,ast,math
from pathlib import Path
ROOT=Path('E:/Game/game_develop/CatSimulator')
source=(ROOT/'Source/Blender/cat_geometry.py').read_text(encoding='utf-8')
tree=ast.parse(source)
function=next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='coat_color')
exec(compile(ast.Module(body=[function],type_ignores=[]),'coat','exec'))
rig=bpy.data.objects['CatRig']; original=rig.location.copy(); rig.location=(0,0,0)
bpy.context.view_layer.update()
objects=list(bpy.data.collections['ReferenceCat'].all_objects)
for obj in objects:
    if obj.type!='MESH': continue
    colors=obj.data.color_attributes.get('CoatColor')
    if not colors: continue
    for vertex in obj.data.vertices:
        point=rig.matrix_world.inverted()@obj.matrix_world@vertex.co
        colors.data[vertex.index].color=coat_color(point)
bpy.ops.object.select_all(action='DESELECT')
for obj in objects: obj.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(ROOT/'Game/Assets/Art/ReferenceCat.fbx'),use_selection=True,
    object_types={'MESH','EMPTY','ARMATURE'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,
    use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,
    bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,mesh_smooth_type='FACE')
rig.location=original
bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
result={'updated':'white chest, white flank, narrower grey-brown tabby stripes'}
