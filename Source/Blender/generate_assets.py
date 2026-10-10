"""Self-made first-pass game assets. Run inside Blender via its official MCP."""
import bpy
import math
import random
from pathlib import Path
from mathutils import Vector

ROOT = Path('E:/Game/game_develop/CatSimulator')
ART = ROOT / 'Game/Assets/Art'
ART.mkdir(parents=True, exist_ok=True)
random.seed(41)
scene = bpy.data.scenes.new('CatSimulator_AssetWorkshop')
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
room = bpy.data.collections.new('LivingRoom')
cat_collection = bpy.data.collections.new('ReferenceCat')
scene.collection.children.link(room)
scene.collection.children.link(cat_collection)
active_collection = room

def move_collection(obj):
    for collection in list(obj.users_collection):
        collection.objects.unlink(obj)
    active_collection.objects.link(obj)
    return obj

def material(name, color, roughness=.65, kind=0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    p = mat.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = (*color, 1)
    p.inputs['Roughness'].default_value = roughness
    if kind:
        nodes = mat.node_tree.nodes
        noise = nodes.new('ShaderNodeTexNoise')
        noise.inputs['Scale'].default_value = 7 if kind == 1 else 130
        noise.inputs['Detail'].default_value = 2
        bump = nodes.new('ShaderNodeBump')
        bump.inputs['Strength'].default_value = .15
        bump.inputs['Distance'].default_value = .004 if kind == 1 else .001
        mat.node_tree.links.new(noise.outputs['Fac'], bump.inputs['Height'])
        mat.node_tree.links.new(bump.outputs['Normal'], p.inputs['Normal'])
    mat['surface_kind'] = kind
    return mat

def box(name, location, dimensions, mat, bevel=.02, rotation=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = move_collection(bpy.context.object)
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.rotation_euler.z = rotation
    if bevel:
        mod = obj.modifiers.new('Soft edges', 'BEVEL')
        mod.width = bevel
        mod.segments = 3
        bpy.ops.object.modifier_apply(modifier=mod.name)
        mod = obj.modifiers.new('Weighted normals', 'WEIGHTED_NORMAL')
    if mat: obj.data.materials.append(mat)
    return obj

def ellipsoid(name, location, scale, mat=None, segments=40, rings=24):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=location)
    obj = move_collection(bpy.context.object)
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    for face in obj.data.polygons: face.use_smooth = True
    if mat: obj.data.materials.append(mat)
    return obj

def cylinder(name, location, radius, depth, mat, vertices=48):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=location)
    obj = move_collection(bpy.context.object)
    obj.name = name
    if mat: obj.data.materials.append(mat)
    bevel = obj.modifiers.new('Rounded rims', 'BEVEL')
    bevel.width = .008
    bevel.segments = 3
    for face in obj.data.polygons: face.use_smooth = True
    return obj

def anchor(name, position):
    obj = bpy.data.objects.new(name, None)
    active_collection.objects.link(obj)
    obj.location = position
    obj.empty_display_size = .08
    return obj

def parent_keep(obj, parent):
    matrix = obj.matrix_world.copy()
    obj.parent = parent
    obj.matrix_world = matrix

wall = material('Wall_Ivory', (.78, .755, .69), .85)
ceiling = material('Ceiling_White', (.9, .89, .85), .9)
trim = material('Trim_White', (.72, .70, .65), .5)
black = material('Metal_Black', (.022, .026, .029), .25)
linen = material('Fabric_Linen', (.64, .59, .48), .88, 2)
pillow = material('Fabric_Cream', (.77, .73, .63), .88, 2)
rug = material('Rug_Oatmeal', (.52, .48, .4), .97, 2)
curtain_mat = material('Curtain_Linen', (.77, .72, .61), .92, 2)
sheer = material('Curtain_Sheer', (.86, .85, .81), .95, 2)
wood = material('Wood_Oak', (.49, .29, .125), .49, 1)
rope = material('Rope_Sisal', (.56, .43, .27), .93, 2)
cardboard = material('Cardboard', (.45, .31, .17), .9, 1)
leaf = material('Leaf_Green', (.105, .19, .095), .72)
ceramic = material('Ceramic_Cream', (.67, .63, .53), .34)
glass = material('Glass_Clear', (.78, .88, .86), .07)
p = glass.node_tree.nodes.get('Principled BSDF')
p.inputs['Transmission Weight'].default_value = 1
p.inputs['IOR'].default_value = 1.46
water = material('Water_Clear', (.55, .72, .75), .035)
water.node_tree.nodes.get('Principled BSDF').inputs['Transmission Weight'].default_value = 1
wood_variants = [material(f'Wood_Floor_{i}', (.47 + i*.008, .31+i*.006, .17+i*.004), .54, 1) for i in range(8)]

# All room dimensions and connections remain reusable by future level configurations.
box('FloorBase', (0, 0, -.075), (6.8, 5.8, .15), wood, .005)
box('COL_Floor', (0, 0, -.075), (6.8, 5.8, .15), None, 0)
for row in range(29):
    y = -2.8 + row*.2
    for col in range(7):
        x = -3.0 + col*.99
        box(f'FloorPlank_{row:02}_{col:02}', (x, y, .004), (.982, .194, .014), random.choice(wood_variants), .002)
box('Wall_Left', (-3.4, 0, 1.5), (.16, 5.8, 3), wall)
box('Wall_Right', (3.4, 0, 1.5), (.16, 5.8, 3), wall)
box('Wall_Back', (0, -2.9, 1.5), (6.8, .16, 3), wall)
for side in (-1, 1):
    box(f'COL_Wall_{side}', (side*3.4, 0, 1.5), (.16, 5.8, 3), None, 0)
    box(f'Baseboard_{side}', (side*3.29, 0, .055), (.025, 5.7, .11), trim, .004)
box('COL_Back', (0, -2.9, 1.5), (6.8, .16, 3), None, 0)
box('WindowFrame_Top', (0, 2.86, 2.82), (6.8, .16, .2), trim)
box('WindowFrame_Bottom', (0, 2.86, .16), (6.8, .16, .3), trim)
for x in (-3.1, 0, 3.1): box('Window_Mullion', (x, 2.87, 1.5), (.06, .06, 2.6), trim, .005)
box('COL_WindowWall', (0, 2.99, 1.5), (6.8, .08, 3), None, 0)
anchor('SpawnPoint', (0, -1.75, .04))

sofa_root = anchor('Sofa', (-1.8, .8, 0))
for obj in [box('Sofa_Base', (-1.75, .72, .29), (2.35, 1.02, .36), linen, .12),
            box('Sofa_Back', (-1.75, 1.18, .7), (2.35, .28, .77), linen, .13),
            box('Sofa_Arm_Left', (-2.92, .68, .57), (.25, 1.14, .73), linen, .105),
            box('Sofa_Arm_Right', (-.58, .68, .57), (.25, 1.14, .73), linen, .105)]:
    parent_keep(obj, sofa_root)
for x in (-2.35, -1.23):
    parent_keep(box('Seat_Cushion', (x, .65, .51), (1.06, .82, .21), pillow, .10), sofa_root)
    cushion = box('Back_Cushion', (x, 1.04, .89), (1.04, .19, .59), pillow, .09)
    cushion.rotation_euler.x = -.12
    parent_keep(cushion, sofa_root)
for x in (-2.8, -.7):
    for y in (.3, 1.12): parent_keep(box('Sofa_Leg', (x, y, .1), (.08, .08, .2), wood), sofa_root)
box('COL_Sofa', (-1.75, .76, .31), (2.4, 1.05, .59), None, 0)
box('COL_SofaBack', (-1.75, 1.15, .8), (2.4, .24, .7), None, 0)

box('RugBase', (-.7, -.35, .028), (3.65, 2.75, .045), rug, .045)
vertices, faces = [], []
for i in range(8000):
    x, y = random.uniform(-2.48, 1.08), random.uniform(-1.66, .96)
    z, height = .05, random.uniform(.008, .025)
    base = len(vertices)
    vertices += [(x-.0025, y, z), (x+.0025, y, z), (x+random.uniform(-.008,.008), y+.004, z+height)]
    faces.append((base, base+1, base+2))
mesh = bpy.data.meshes.new('RugFibresMesh'); mesh.from_pydata(vertices, [], faces); mesh.update()
obj = bpy.data.objects.new('RugFibres', mesh); room.objects.link(obj); obj.data.materials.append(rug)

table = anchor('CoffeeTable', (-.65, -.25, 0))
for x in (-1.37, .07):
    for y in (-.65, .15):
        parent_keep(box('Table_Leg', (x,y,.25), (.033,.033,.5), black, .008), table)
for x in (-1.37, .07): parent_keep(box('Table_Frame', (x,-.25,.40), (.035,.87,.035), black, .005), table)
parent_keep(box('GlassTableTop', (-.65,-.25,.48), (1.64,1.03,.012), glass, .006), table)
box('COL_TableTop', (-.65,-.25,.469), (1.64,1.03,.035), None, 0)
anchor('CupActionPosition', (-.7, -.58, .491))

cup_root = anchor('Cup', (-.64, .02, .49))
verts, faces = [], []
segments = 64
for radius, height in [(.045,0),(.05,.13),(.0475,.13),(.0415,.007)]:
    for i in range(segments):
        a=i*math.tau/segments; verts.append((radius*math.cos(a), radius*math.sin(a),height))
for ring in range(3):
    for i in range(segments): faces.append((ring*segments+i,ring*segments+(i+1)%segments,(ring+1)*segments+(i+1)%segments,(ring+1)*segments+i))
faces.append(tuple(reversed(range(3*segments,4*segments))))
mesh=bpy.data.meshes.new('CupMesh'); mesh.from_pydata(verts,[],faces); mesh.update()
obj=bpy.data.objects.new('CupGlass',mesh); room.objects.link(obj); obj.parent=cup_root; obj.data.materials.append(glass)
for face in mesh.polygons: face.use_smooth=True
inside=cylinder('WaterInside',(-.64,.02,.555),.043,.115,water); parent_keep(inside,cup_root)

tree = anchor('CatTree', (1.86,1.9,0))
parent_keep(box('TreeBase',(1.86,1.9,.045),(.84,.72,.09),wood,.045),tree)
for x,y in [(1.64,1.9),(2.09,1.9)]:
    parent_keep(cylinder('SisalPost',(x,y,.9),.064,1.73,rope),tree)
for index, (x,y,z) in enumerate([(1.55,1.67,.48),(2.06,1.96,.99),(1.61,1.83,1.50),(1.97,1.96,1.98)]):
    parent_keep(box(f'TreePlatform_{index}',(x,y,z),(.62,.58,.07),wood,.045),tree)
    box(f'COL_TreePlatform_{index}',(x,y,z),(.62,.58,.07),None,0)
    anchor(f'ClimbRoute_{index+1}',(x,y-.035,z+.04))
anchor('ClimbRoute_0',(1.42,1.03,.025))
anchor('TreeInteractAnchor',(1.47,1.03,.2))

def curtain(name, center_x, width, damaged=False):
    verts, faces=[],[]; columns,rows=32,48
    for row in range(rows+1):
        z=.11+row/rows*2.61
        for col in range(columns+1):
            x=center_x-width/2+width*col/columns
            depth=.075*math.sin(col/columns*math.tau*5)+.02*math.sin(z*3+col)
            verts.append((x,2.64+depth,z))
    for row in range(rows):
        for col in range(columns):
            if damaged and 3<row<18 and (8<=col<=10 or 15<=col<=17 or 23<=col<=24): continue
            a=row*(columns+1)+col; faces.append((a,a+1,a+columns+2,a+columns+1))
    mesh=bpy.data.meshes.new(name+'Mesh'); mesh.from_pydata(verts,[],faces); mesh.update()
    obj=bpy.data.objects.new(name,mesh); room.objects.link(obj); obj.data.materials.append(curtain_mat)
    for face in mesh.polygons: face.use_smooth=True
    solid=obj.modifiers.new('Cloth thickness','SOLIDIFY'); solid.thickness=.003
    return obj
curtain('CurtainIntact',2.61,1.15)
curtain('CurtainDamaged',2.61,1.15,True)
curtain('CurtainLeft',-2.63,1.15)
anchor('CurtainInteractAnchor',(2.5,2.35,.24))
anchor('CurtainActionPosition',(2.5,2.24,.025))
for center in (-.98,.86):
    # Light interior drapes; open window remains the main source of daylight.
    obj=curtain('SheerPanel',center,.42)
    obj.data.materials[0]=sheer
    obj.location.y=.10

for index,(x,y,angle) in enumerate([(1.3,-1.3,.18),(2.55,-.3,-.22)]):
    root=anchor(f'CardboardBox_{index}',(x,y,0))
    for name,loc,size in [('Bottom',(x,y,.018),(.63,.47,.035)),('Front',(x,y-.235,.16),(.63,.014,.32)),('Back',(x,y+.235,.16),(.63,.014,.32)),('Left',(x-.315,y,.16),(.014,.47,.32)),('Right',(x+.315,y,.16),(.014,.47,.32))]:
        parent_keep(box('Box_'+name,loc,size,cardboard,.004),root)
    for side in (-1,1):
        flap=box('Box_Flap',(x+side*.4,y,.29),(.2,.46,.014),cardboard,.003)
        flap.rotation_euler.y=side*.4; parent_keep(flap,root)
    anchor(f'BoxInside_{index}',(x,y,.044))

# A small amount of ordinary decoration supports the reference's lived-in room.
pot=cylinder('PlantPot',(2.7,1.15,.21),.19,.42,ceramic)
for i in range(15):
    a=i*2.4; tip=(2.7+math.cos(a)*.22,1.15+math.sin(a)*.22,.45+random.uniform(.15,.7))
    obj=ellipsoid('PlantLeaf',tip,(.06,.025,.23),leaf,20,12)
    obj.rotation_euler=(random.uniform(-.7,.7),random.uniform(-.7,.7),a)
box('MediaCabinet',(0,-2.61,.28),(1.7,.42,.5),wood,.02)
box('TVFrame',(0,-2.55,.99),(1.23,.045,.70),black,.015)
box('TVScreen',(0,-2.516,.99),(1.15,.008,.62),material('TV_Dark',(.014,.022,.03),.18),.003)
anchor('RoomLookTarget',(-.3,.7,.45))

exec((ROOT / 'Source/Blender/fix_room_layout.py').read_text(encoding='utf-8'))
apply_room_layout(room)
ROOM_OBJECTS=list(room.all_objects)

exec((ROOT / 'Source/Blender/cat_geometry.py').read_text(encoding='utf-8'))

def export_fbx(objects, filename, animation=False):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active = rig if animation else objects[0]
    bpy.context.view_layer.update()
    bpy.ops.export_scene.fbx(filepath=str(ART / filename), use_selection=True,
        object_types={'MESH','EMPTY','ARMATURE'}, axis_forward='-Z', axis_up='Y',
        apply_unit_scale=True, use_mesh_modifiers=True, add_leaf_bones=False,
        bake_anim=animation, bake_anim_use_all_actions=animation,
        bake_anim_use_nla_strips=False, bake_anim_simplify_factor=0,
        path_mode='AUTO', mesh_smooth_type='FACE')

export_fbx(ROOM_OBJECTS, 'LivingRoom.fbx')
export_fbx(CAT_OBJECTS, 'ReferenceCat.fbx', True)
import json
palette=[]
for mat in bpy.data.materials:
    if not mat.use_nodes: continue
    p=mat.node_tree.nodes.get('Principled BSDF')
    if not p: continue
    color=list(p.inputs['Base Color'].default_value)
    palette.append({'name':mat.name,'color':color,
                    'roughness':p.inputs['Roughness'].default_value,
                    'kind':int(mat.get('surface_kind',0))})
(ART / 'palette.json').write_text(json.dumps({'materials':palette},indent=2),encoding='utf-8')

active_collection=room
bpy.data.objects.get('CurtainDamaged').hide_render=True
for obj in ROOM_OBJECTS:
    if obj.name.startswith('COL_'): obj.hide_render=True
rig.location=(.45,-.8,.03)
camera_data=bpy.data.cameras.new('WorkshopPreviewCamera')
camera=bpy.data.objects.new('WorkshopPreviewCamera',camera_data); room.objects.link(camera)
camera.location=(2.8,-2.55,1.7)
camera.rotation_euler=(Vector((0,.45,.65))-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.lens=30; scene.camera=camera
for name,loc,energy,size in [('WindowDaylight',(0,2.5,2.2),1000,4),('RoomFill',(-2,-1,2.7),240,3)]:
    data=bpy.data.lights.new(name,'AREA'); data.energy=energy; data.shape='DISK'; data.size=size
    obj=bpy.data.objects.new(name,data); room.objects.link(obj); obj.location=loc
    obj.rotation_euler=(Vector((0,0,.3))-obj.location).to_track_quat('-Z','Y').to_euler()
scene.world=bpy.data.worlds.new('WarmDaylightWorld'); scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.42,.49,.58,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.4
scene.render.engine='CYCLES'; scene.cycles.samples=32; scene.cycles.use_denoising=True
scene.render.resolution_x=1280; scene.render.resolution_y=720; scene.render.resolution_percentage=100
scene.render.filepath=str(ROOT / 'Source/Blender/workshop-preview.png')
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT / 'Source/Blender/first-level.blend'))
result={'scene':scene.name,'room_objects':len(ROOM_OBJECTS),'cat_objects':len(CAT_OBJECTS),
        'cat_vertices':len(body.data.vertices),'actions':[a.name for a in bpy.data.actions],
        'exports':['LivingRoom.fbx','ReferenceCat.fbx'],'source':bpy.data.filepath}
