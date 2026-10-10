"""Widen the room and space furniture without enlarging furniture geometry."""
import bpy
import json
from pathlib import Path
from mathutils import Vector

ROOT=Path('E:/Game/game_develop/CatSimulator')
scene=bpy.data.scenes['CatSimulator_AssetWorkshop']
bpy.context.window.scene=scene
room=bpy.data.collections['LivingRoom']
objects=room.all_objects
FACTOR=1.25

def move(names,offset):
    for name in names:
        obj=objects.get(name)
        if obj:obj.location+=Vector(offset)

def move_group(name,extras=()):
    root=objects[name]
    delta=(root.location.x*(FACTOR-1),root.location.y*(FACTOR-1),0)
    move([name]+list(extras),delta)

move_group('Sofa',['COL_Sofa','COL_SofaBack'])
move_group('CoffeeTable',['COL_TableTop','Cup','CupActionPosition'])
move_group('CatTree',['TreeInteractAnchor']+[f'ClimbRoute_{i}' for i in range(5)]+[f'COL_TreePlatform_{i}' for i in range(4)])
for i in range(2):move_group(f'CardboardBox_{i}',[f'BoxInside_{i}'])
move([o.name for o in objects if o.name.startswith('Plant')],(.675,.2875,0))
move(['MediaCabinet','TVFrame','TVScreen'],(0,-.6525,0))
move(['CurtainIntact','CurtainDamaged','CurtainInteractAnchor','CurtainActionPosition'],(.6525,.66,0))
move(['CurtainLeft'],(-.6575,.66,0))
for obj in objects:
    name=obj.name
    if name.startswith('SheerPanel'):
        points=[obj.matrix_world@Vector(corner) for corner in obj.bound_box]
        center_x=(min(p.x for p in points)+max(p.x for p in points))/2
        obj.location+=Vector((center_x*.25,.685,0))
    elif name.startswith('FloorPlank_') or name in ['FloorBase','COL_Floor','RugBase','RugFibres']:
        obj.location.x*=FACTOR;obj.location.y*=FACTOR
        obj.scale.x*=FACTOR;obj.scale.y*=FACTOR
    elif name in ['Wall_Left','Wall_Right'] or name.startswith('COL_Wall_') or name.startswith('Baseboard_'):
        obj.location.x*=FACTOR;obj.location.y*=FACTOR;obj.scale.y*=FACTOR
    elif name in ['Wall_Back','COL_Back','COL_WindowWall','WindowFrame_Top','WindowFrame_Bottom']:
        obj.location.x*=FACTOR;obj.location.y*=FACTOR;obj.scale.x*=FACTOR
    elif name.startswith('Window_Mullion') or name in ['SpawnPoint','RoomLookTarget']:
        obj.location.x*=FACTOR;obj.location.y*=FACTOR

# Keep the source workshop focused on the enlarged room.
reference=bpy.data.collections.get('ReferenceCat')
if reference:
    for obj in list(reference.all_objects):bpy.data.objects.remove(obj,do_unlink=True)
bpy.context.view_layer.update()
export=[o for o in room.all_objects if o.type in {'MESH','EMPTY'}]
bpy.ops.object.select_all(action='DESELECT')
for obj in export:obj.select_set(True)
bpy.context.view_layer.objects.active=objects['FloorBase']
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Source/Blender/spacious-living-room.blend'),compress=True)
bpy.ops.export_scene.fbx(filepath=str(ROOT/'Game/Assets/Art/LivingRoom.fbx'),use_selection=True,
    object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,
    use_mesh_modifiers=True,bake_anim=False,path_mode='AUTO',mesh_smooth_type='FACE')
report={'factor':FACTOR,'oldFloorM':[6.8,5.8],'newFloorM':[8.5,7.25],
        'furnitureScaleUnchanged':True,'roomObjects':len(export)}
(ROOT/'Source/Verification/room-spacing-conversion.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('ROOM_SPACING_COMPLETE',json.dumps(report),flush=True)
