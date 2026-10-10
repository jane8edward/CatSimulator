"""Extend the saved spacious living room; original source remains unchanged.

Run with Blender --background spacious-living-room.blend --python this_file.
Blender's -X extension becomes +X in the existing Unity FBX coordinate setup.
"""
import bpy
import json
import math
import random
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Source/Verification/Bedroom'
OUT.mkdir(parents=True, exist_ok=True)
scene = bpy.data.scenes['CatSimulator_AssetWorkshop']
bpy.context.window.scene = scene
if bpy.context.object and bpy.context.object.mode != 'OBJECT':
    bpy.ops.object.mode_set(mode='OBJECT')
room = bpy.data.collections['LivingRoom']
bedroom = bpy.data.collections.new('Bedroom')
room.children.link(bedroom)
random.seed(104)

def link(obj, collection=bedroom):
    for old in list(obj.users_collection):
        old.objects.unlink(obj)
    collection.objects.link(obj)
    return obj

def material(name, color, roughness=.7, kind=0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    p = mat.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = (*color, 1)
    p.inputs['Roughness'].default_value = roughness
    mat['surface_kind'] = kind
    return mat

wall = bpy.data.materials['Wall_Ivory']
trim = bpy.data.materials['Trim_White']
oak = bpy.data.materials['Wood_Oak']
floor = [bpy.data.materials[f'Wood_Floor_{i}'] for i in range(8)]
cream = material('Bedroom_WhiteCotton', (.84, .82, .75), .94, 2)
sage = material('Bedroom_SageLinen', (.30, .40, .32), .95, 2)
walnut = material('Bedroom_Walnut', (.27, .16, .085), .52, 1)
panel = material('Bedroom_PaintedCream', (.71, .70, .61), .72)
head = material('Bedroom_HeadboardLinen', (.49, .42, .33), .91, 2)
rug = material('Bedroom_WovenRug', (.65, .59, .47), .97, 2)
brass = material('Bedroom_Brass', (.40, .29, .13), .28)
lamp_glow = material('Bedroom_LampGlow', (.91, .71, .42), .82)
shade = material('Bedroom_LampShade', (.86, .79, .64), .92, 2)
dark = bpy.data.materials['Metal_Black']
screen = material('Bedroom_Screen', (.10, .17, .21), .26)
desk_mat = material('Bedroom_DeskMat', (.16, .22, .20), .95, 2)
paper = material('Bedroom_Paper', (.91, .88, .80), .96)
phone_screen = material('Bedroom_PhoneScreen', (.055, .11, .12), .22)
charging_green = material('Bedroom_ChargingGreen', (.29, .72, .40), .32)
terracotta = material('Bedroom_Terracotta', (.47, .22, .13), .82)
green = bpy.data.materials['Leaf_Green']
art_bg = material('Bedroom_ArtCanvas', (.77, .73, .63), .95)
ceiling = bpy.data.materials.get('Ceiling_White') or material('Ceiling_White', (.9,.89,.85), .9)

def empty(name, xyz, collection=bedroom):
    obj = bpy.data.objects.new(name, None)
    collection.objects.link(obj)
    obj.location = xyz
    return obj

def parent(obj, group):
    bpy.context.view_layer.update()
    matrix = obj.matrix_world.copy()
    obj.parent = group
    obj.matrix_world = matrix
    return obj

def box(name, xyz, size, mat, bevel=.025, group=None, collection=bedroom):
    bpy.ops.mesh.primitive_cube_add(size=1, location=xyz)
    obj = link(bpy.context.object, collection)
    obj.name = name
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        mod = obj.modifiers.new('Soft furniture edges', 'BEVEL')
        mod.width = bevel
        mod.segments = 3
        bpy.ops.object.modifier_apply(modifier=mod.name)
        obj.modifiers.new('Furniture normals', 'WEIGHTED_NORMAL')
    if mat:
        obj.data.materials.append(mat)
    if group:
        parent(obj, group)
    return obj

def ellipsoid(name, xyz, radii, mat, group=None):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=16, location=xyz)
    obj = link(bpy.context.object)
    obj.name = name
    obj.scale = radii
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    for poly in obj.data.polygons:
        poly.use_smooth = True
    obj.data.materials.append(mat)
    if group:
        parent(obj, group)
    return obj

def cylinder(name, xyz, radius, depth, mat, group=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=radius, depth=depth, location=xyz)
    obj = link(bpy.context.object)
    obj.name = name
    obj.data.materials.append(mat)
    bevel = obj.modifiers.new('Rounded rims', 'BEVEL')
    bevel.width = min(.008, radius / 3)
    bevel.segments = 2
    for face in obj.data.polygons:
        face.use_smooth = True
    if group:
        parent(obj, group)
    return obj

def configure_preview():
    """Native Blender review lighting; lights/camera are excluded from the FBX."""
    for obj in list(bedroom.objects):
        if obj.name.startswith('BedroomPreview_'):
            bpy.data.objects.remove(obj, do_unlink=True)
    for name, xyz, color, watts, size in [
        ('LampLeft',(-8.23,2.28,1.01),(1,.86,.65),25,.22),
        ('LampRight',(-5.65,2.28,1.01),(1,.86,.65),25,.22),
        ('Window',(-9.35,-.75,2.20),(1,.965,.90),180,1.0),
        ('SoftBounce',(-7.05,0,2.60),(1,.94,.83),75,1.1)]:
        data=bpy.data.lights.new('BedroomPreview_'+name,'POINT');data.color=color;data.energy=watts;data.shadow_soft_size=size
        obj=bpy.data.objects.new(data.name,data);bedroom.objects.link(obj);obj.location=xyz
    data=bpy.data.cameras.new('BedroomPreview_Camera');data.lens=27
    obj=bpy.data.objects.new(data.name,data);bedroom.objects.link(obj);obj.location=(-8.94,-2.25,2.35)
    target=Vector((-6.91,.65,.77));obj.rotation_euler=(target-obj.location).to_track_quat('-Z','Y').to_euler();scene.camera=obj
    scene.render.filepath=str(OUT/'blender-bedroom-preview.png')
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                area.spaces.active.region_3d.view_location=target
                area.spaces.active.region_3d.view_distance=6
                area.spaces.active.region_3d.view_rotation=obj.rotation_euler.to_quaternion()

# Replace only the shared wall with segments around a 1.6 m doorway.
for name in ('Wall_Left', 'COL_Wall_-1', 'Baseboard_-1'):
    bpy.data.objects.remove(room.all_objects[name], do_unlink=True)
for name, y0, y1 in [('Wall_Left', -.30, 3.625), ('Wall_Left_EntryReturn', -3.625, -1.90)]:
    box(name, (-4.25, (y0+y1)/2, 1.5), (.16, y1-y0, 3), wall, .012, collection=room)
    box('COL_Wall_'+name, (-4.25, (y0+y1)/2, 1.5), (.16, y1-y0, 3), None, 0, collection=room)
    box('Baseboard_'+name, (-4.145, (y0+y1)/2, .055), (.035, y1-y0, .11), trim, .004, collection=room)
box('Bedroom_DoorHeader', (-4.25, -1.10, 2.675), (.16, 1.6, .65), wall, .01)
for y in (-1.93, -.27):
    box('Bedroom_DoorJamb', (-4.25, y, 1.19), (.21, .055, 2.38), trim, .006)
box('Bedroom_DoorLintel', (-4.25, -1.10, 2.365), (.21, 1.71, .065), trim, .006)

box('Bedroom_FloorBase', (-7.05, 0, -.075), (5.60, 6.10, .15), oak, .004)
box('COL_Floor_Bedroom', (-7.05, 0, -.075), (5.60, 6.10, .15), None, 0)
for row in range(30):
    y = -2.95 + row * .2
    for col in range(6):
        x = -9.355 + col * .935
        box(f'FloorPlank_Bedroom_{row:02}_{col:02}', (x, y, .004), (.927, .194, .014), random.choice(floor), .002)
for name, y in [('Bedroom_HeadWall', 3.05), ('Bedroom_FootWall', -3.05)]:
    box(name, (-7.05, y, 1.5), (5.6, .16, 3), wall, .012)
    box('Bedroom_Skirting_'+name, (-7.05, y + (-.1 if y > 0 else .1), .055), (5.42, .025, .11), trim, .004)
# Wide window on the outward-facing wall, with actual opening and glazing collision.
for suffix, y0, y1 in [('Front', -3.05, -1.75), ('Back', .25, 3.05)]:
    box('Bedroom_WindowWall_'+suffix, (-9.85, (y0+y1)/2, 1.5), (.16, y1-y0, 3), wall, .012)
box('Bedroom_WindowWall_Lower', (-9.85, -.75, .50), (.16, 2, 1), wall, .01)
box('Bedroom_WindowWall_Upper', (-9.85, -.75, 2.82), (.16, 2, .36), wall, .01)
box('Bedroom_WindowSill', (-9.76, -.75, 1.04), (.30, 2.16, .07), trim, .015)
for y in (-1.78, -.75, .28):
    box('Bedroom_WindowFrame', (-9.85, y, 1.82), (.085, .045, 1.6), trim, .004)
for z in (1.04, 2.63):
    box('Bedroom_WindowFrameHorizontal', (-9.85, -.75, z), (.085, 2.1, .055), trim, .004)
box('Bedroom_WindowGlass', (-9.865, -.75, 1.83), (.014, 2, 1.58), bpy.data.materials['Glass_Clear'], .002)
for center in (-1.53, .05):
    verts, faces = [], []
    for row in range(35):
        z = .12 + row / 34 * 2.63
        for col in range(19):
            y = center - .19 + col / 18 * .38
            verts.append((-9.61 + .035*math.sin(col/18*math.tau*4), y, z))
    for row in range(34):
        for col in range(18):
            a = row*19 + col
            faces.append((a, a+1, a+20, a+19))
    mesh = bpy.data.meshes.new('BedroomCurtainMesh'); mesh.from_pydata(verts, [], faces); mesh.update()
    curtain = bpy.data.objects.new('Bedroom_WindowCurtain', mesh); bedroom.objects.link(curtain)
    mesh.materials.append(bpy.data.materials['Curtain_Linen'])
    for poly in mesh.polygons: poly.use_smooth = True
    curtain.modifiers.new('Cloth thickness', 'SOLIDIFY').thickness = .003
box('Bedroom_Ceiling', (-7.05, 0, 3.05), (5.6, 6.1, .10), ceiling, .005)

# Low timber double bed, soft headboard, cotton duvet with modeled folds.
bed = empty('Bedroom_Bed', (-7, 1.55, 0))
box('Bedroom_BedFrame', (-7, 1.55, .42), (2.03, 2.40, .11), walnut, .035, bed)
for x in (-7.87, -6.13):
    for y in (.50, 2.60):
        box('Bedroom_BedLeg', (x, y, .20), (.095, .095, .40), walnut, .014, bed)
box('Bedroom_Mattress', (-7, 1.55, .565), (1.94, 2.30, .20), cream, .09, bed)
box('Bedroom_Headboard', (-7, 2.80, .82), (2.18, .12, 1.35), walnut, .025, bed)
for i in range(4):
    box('Bedroom_HeadboardPanel', (-7.76+i*.507, 2.723, 1.02), (.49, .075, .74), head, .035, bed)

def cloth(name, center_y, length, width, z, mat, hang, group):
    verts, faces = [], []
    nx, ny = 44, 60
    for j in range(ny+1):
        y = center_y - length/2 + j/ny*length
        for i in range(nx+1):
            x = -7 - width/2 + i/nx*width
            side = max(0, abs(x+7)-.935) / max(.001, width/2-.935)
            foot = max(0, .43-y) / .20 if hang else 0
            wrinkle = .013*math.sin(x*15+y*8) + .007*math.sin(y*31-x*5)
            ridge = .028*math.exp(-((x+7.42)**2/.12+(y-1.12)**2/.19))
            verts.append((x, y, z+wrinkle+ridge-hang*min(1, side)-.17*min(1, foot)))
    for j in range(ny):
        for i in range(nx):
            a=j*(nx+1)+i
            faces.append((a, a+1, a+nx+2, a+nx+1))
    mesh=bpy.data.meshes.new(name+'Mesh');mesh.from_pydata(verts, [], faces);mesh.update()
    obj=bpy.data.objects.new(name, mesh);bedroom.objects.link(obj);mesh.materials.append(mat)
    for poly in mesh.polygons:poly.use_smooth=True
    obj.modifiers.new('Cotton thickness','SOLIDIFY').thickness=.009
    return parent(obj,group)

cloth('Bedroom_Duvet', 1.42, 2.40, 2.27, .691, cream, .22, bed)
cloth('Bedroom_FoldedThrow', .77, .78, 2.19, .718, sage, .12, bed)
for x, angle in [(-7.49, -.09), (-6.51, .11)]:
    # Dense, puffy surface supports local indentation in the game instead of a hard cube.
    bpy.ops.mesh.primitive_uv_sphere_add(segments=64, ring_count=32, location=(x, 2.31, .77))
    pillow=link(bpy.context.object);pillow.name='Bedroom_Pillow'
    for vertex in pillow.data.vertices:
        p=vertex.co.copy()
        vertex.co=(math.copysign(abs(p.x)**.62,p.x)*.435,
                   math.copysign(abs(p.y)**.62,p.y)*.28,
                   math.copysign(abs(p.z)**1.15,p.z)*.10)
    for poly in pillow.data.polygons:poly.use_smooth=True
    pillow.data.materials.append(cream);parent(pillow,bed)
    pillow.rotation_euler.z=angle
    pillow.rotation_euler.x=.10

def bedside(x, label):
    group=empty('Bedroom_Bedside_'+label, (x, 2.22, 0))
    box('Bedroom_BedsideBody_'+label, (x, 2.22, .37), (.62, .52, .57), oak, .018, group)
    box('Bedroom_BedsideTop_'+label, (x, 2.22, .675), (.67, .56, .045), oak, .012, group)
    for z in (.255, .47):
        box('Bedroom_BedsideDrawer_'+label, (x, 1.952, z), (.55, .026, .19), walnut, .008, group)
        ellipsoid('Bedroom_DrawerKnob_'+label, (x, 1.925, z), (.025, .015, .025), brass, group)
    lamp_x=x+.12 if label=='Left' else x
    lamp_y=2.28
    lamp=parent(empty('Bedroom_Prop_Lamp_'+label,(lamp_x,lamp_y,.70)),group)
    cylinder('Bedroom_LampBase_'+label, (lamp_x, lamp_y, .715), .115, .03, brass, lamp)
    ellipsoid('Bedroom_LampVase_'+label, (lamp_x, lamp_y, .85), (.085, .085, .14), panel, lamp)
    cylinder('Bedroom_LampStem_'+label, (lamp_x, lamp_y, .98), .017, .16, brass, lamp)
    bpy.ops.mesh.primitive_cone_add(vertices=40, radius1=.20, radius2=.14, depth=.25, location=(lamp_x,lamp_y,1.115))
    obj=link(bpy.context.object);obj.name='Bedroom_LampShade_'+label;obj.data.materials.append(shade);parent(obj,lamp)
    cylinder('Bedroom_LampGlow_'+label, (lamp_x, lamp_y, 1.00), .177, .009, lamp_glow, lamp)
    parent(empty('Bedroom_LampLight_'+label, (lamp_x, lamp_y, 1.01)),lamp)
    return group
bedside(-8.35, 'Left');bedside(-5.65, 'Right')

# Wardrobe along the side wall leaves over a metre beside the bed.
wardrobe=empty('Bedroom_Wardrobe', (-9.44, 1.84, 0))
box('Bedroom_WardrobeBody', (-9.44, 1.84, 1.08), (.64, 1.68, 2.16), oak, .018, wardrobe)
for y in (1.427, 2.253):
    box('Bedroom_WardrobeDoor', (-9.102, y, 1.105), (.038, .796, 2.03), panel, .012, wardrobe)
    for edge_y in (y-.35, y+.35):
        box('Bedroom_WardrobeDoorStile', (-9.076, edge_y, 1.105), (.018, .026, 1.95), oak, .005, wardrobe)
    box('Bedroom_WardrobeHandle', (-9.041, y+(.23 if y<1.84 else -.23), 1.13), (.036, .018, .17), brass, .007, wardrobe)

# A full-size workstation, with enough depth for paws and real loose objects.
desk=empty('Bedroom_Desk', (-8.50, -2.39, 0))
box('Bedroom_DeskTop', (-8.50, -2.39, .81), (2.20, .88, .06), oak, .022, desk)
for x in (-9.48, -7.52):
    for y in (-2.73, -2.05):
        box('Bedroom_DeskLeg', (x,y,.391), (.065,.065,.78), walnut, .008, desk)
box('Bedroom_DeskDrawer', (-8.50,-2.53,.70), (1.87,.52,.15), panel, .015, desk)
mat_root=parent(empty('Bedroom_Prop_DeskMat',(-8.63,-2.23,.843)),desk)
box('Bedroom_DeskMat',(-8.63,-2.23,.843),(1.12,.46,.006),desk_mat,.002,mat_root)
monitor=parent(empty('Bedroom_Prop_Monitor',(-8.85,-2.64,.846)),desk)
cylinder('Bedroom_MonitorStand', (-8.85,-2.64,.859), .14,.025,dark,monitor)
box('Bedroom_MonitorStem', (-8.85,-2.67,1.01), (.045,.03,.28),dark,.006,monitor)
box('Bedroom_MonitorFrame', (-8.85,-2.69,1.235), (.64,.048,.39),dark,.012,monitor)
box('Bedroom_MonitorScreen', (-8.85,-2.661,1.235), (.592,.009,.342),screen,.003,monitor)
keyboard=parent(empty('Bedroom_Prop_Keyboard',(-8.81,-2.17,.85)),desk)
box('Bedroom_Keyboard', (-8.81,-2.17,.864), (.43,.155,.028),panel,.008,keyboard)
for row in range(4):
    for col in range(12):
        box('Bedroom_KeyboardKey',(-9.001+col*.033,-2.225+row*.034,.884),(.025,.025,.009),cream,.003,keyboard)
mouse=parent(empty('Bedroom_Prop_Mouse',(-8.39,-2.14,.849)),desk)
ellipsoid('Bedroom_Mouse',(-8.39,-2.14,.867),(.037,.058,.019),dark,mouse)
ellipsoid('Bedroom_MouseWheel',(-8.39,-2.165,.887),(.007,.012,.006),panel,mouse)
computer=parent(empty('Bedroom_Prop_Computer',(-7.69,-2.55,.842)),desk)
box('Bedroom_ComputerCase',(-7.69,-2.55,1.061),(.27,.40,.438),panel,.014,computer)
box('Bedroom_ComputerFront',(-7.69,-2.337,1.061),(.237,.016,.398),dark,.008,computer)
for z in (.985,1.14):
    fan=cylinder('Bedroom_ComputerFan',(-7.69,-2.324,z),.064,.012,dark,computer)
    fan.rotation_euler.x=math.pi/2
    for a in range(6):
        blade=ellipsoid('Bedroom_FanBlade',(-7.69+math.cos(a*math.tau/6)*.033,-2.315,z+math.sin(a*math.tau/6)*.033),(.024,.007,.009),sage,computer)
        blade.rotation_euler.y=-a*math.tau/6
box('Bedroom_ComputerPower',(-7.60,-2.319,1.235),(.014,.007,.009),charging_green,.002,computer)
tissues=parent(empty('Bedroom_Prop_Tissues',(-7.74,-2.08,.842)),desk)
box('Bedroom_TissueBox',(-7.74,-2.08,.907),(.235,.145,.13),sage,.016,tissues)
box('Bedroom_TissueSlot',(-7.74,-2.08,.974),(.13,.025,.008),dark,.004,tissues)
sheet=box('Bedroom_Tissue',(-7.74,-2.075,1.02),(.105,.012,.09),paper,.005,tissues)
sheet.rotation_euler.x=.22;sheet.rotation_euler.y=.10
for i, color in enumerate((sage, cream, terracotta)):
    book_root=parent(empty('Bedroom_Prop_Book_'+str(i),(-9.39,-2.40,.849+i*.035)),desk)
    book=box('Bedroom_StudyBook', (-9.39,-2.40,.864+i*.035), (.25,.20,.03),color,.003,book_root)
    book.rotation_euler.z=.06*(i-1)
pen_cup=parent(empty('Bedroom_Prop_PenCup',(-9.42,-2.70,.841)),desk)
cylinder('Bedroom_PenCup',(-9.42,-2.70,.906),.045,.13,terracotta,pen_cup)
for i in range(3):
    pen=cylinder('Bedroom_Pen',(-9.44+i*.016,-2.70,.993),.004,.17,dark,pen_cup)
    pen.rotation_euler.y=(i-1)*.13
chair=empty('Bedroom_Chair', (-8.65,-1.63,0))
box('Bedroom_ChairSeat', (-8.65,-1.63,.45), (.48,.46,.085),sage,.04,chair)
box('Bedroom_ChairBack', (-8.65,-1.40,.74), (.48,.065,.50),oak,.035,chair)
for x in (-8.84,-8.46):
    for y in (-1.81,-1.45):box('Bedroom_ChairLeg',(x,y,.23),(.04,.04,.45),walnut,.008,chair)

dresser=empty('Bedroom_Dresser',(-5.27,-2.64,0))
box('Bedroom_DresserBody',(-5.27,-2.64,.45),(1.32,.57,.81),oak,.018,dresser)
box('Bedroom_DresserTop',(-5.27,-2.64,.88),(1.38,.61,.05),oak,.012,dresser)
for z in (.20,.45,.70):
    box('Bedroom_DresserDrawer',(-5.27,-2.34,z),(1.21,.029,.22),panel,.008,dresser)
    for x in (-5.58,-4.96):ellipsoid('Bedroom_DresserKnob',(x,-2.30,z),(.025,.015,.025),brass,dresser)
plant=parent(empty('Bedroom_Prop_Plant',(-4.91,-2.63,.905)),dresser)
cylinder('Bedroom_PlantPot',(-4.91,-2.63,1.002),.09,.19,terracotta,plant)
for i in range(7):
    a=i*2.4
    leaf=ellipsoid('Bedroom_PlantLeaf',(-4.91+math.cos(a)*.065,-2.63+math.sin(a)*.065,1.17+i*.018),(.045,.018,.12),green,plant)
    leaf.rotation_euler=(.35*math.cos(a),.35*math.sin(a),a)

# Wall socket, plugged-in adapter and a phone charging on the left nightstand.
socket=empty('Bedroom_Outlet',(-8.40,2.952,.72))
box('Bedroom_OutletPlate',(-8.40,2.952,.72),(.16,.023,.12),trim,.012,socket)
for x in (-8.443,-8.357):
    for dx in (-.014,.014):
        box('Bedroom_OutletSlot',(x+dx,2.938,.73),(.006,.005,.027),dark,.002,socket)
charger=parent(empty('Bedroom_Charger',(-8.40,2.922,.72)),socket)
box('Bedroom_ChargingAdapter',(-8.40,2.905,.72),(.055,.055,.048),cream,.01,charger)
parent(empty('Bedroom_ChargingWallAnchor',(-8.40,2.876,.713)),charger)
phone=empty('Bedroom_Prop_Phone',(-8.53,2.05,.703))
box('Bedroom_PhoneBody',(-8.53,2.05,.712),(.078,.16,.015),dark,.008,phone)
box('Bedroom_PhoneScreen',(-8.53,2.05,.721),(.069,.148,.003),phone_screen,.006,phone)
box('Bedroom_PhoneBatteryOutline',(-8.53,2.05,.724),(.041,.070,.003),cream,.004,phone)
box('Bedroom_PhoneBattery',(-8.53,2.05,.726),(.032,.057,.003),charging_green,.002,phone)
box('Bedroom_PhoneBatteryTip',(-8.53,2.088,.724),(.014,.007,.003),cream,.002,phone)
box('Bedroom_PhoneConnector',(-8.53,2.137,.712),(.013,.021,.010),cream,.003,phone)
parent(empty('Bedroom_ChargingPhoneAnchor',(-8.53,2.148,.712)),phone)
curve=bpy.data.curves.new('BedroomChargingCable','CURVE');curve.dimensions='3D';curve.bevel_depth=.0028;curve.bevel_resolution=2
path=curve.splines.new('POLY');path.points.add(31)
for i,p in enumerate(path.points):
    t=i/31
    p.co=(-8.40-.13*t,2.876+(2.148-2.876)*t,.713-.22*math.sin(t*math.pi),1)
obj=bpy.data.objects.new('Bedroom_ChargingCable',curve);bedroom.objects.link(obj);curve.materials.append(cream)
bpy.context.view_layer.objects.active=obj;obj.select_set(True);bpy.ops.object.convert(target='MESH');obj.select_set(False)

box('Bedroom_Rug',(-7,-.14,.021),(2.80,1.75,.034),rug,.025)
bench=empty('Bedroom_Bench',(-7,-.10,0))
box('Bedroom_BenchCushion',(-7,-.10,.36),(1.17,.42,.10),sage,.045,bench)
for x in (-7.49,-6.51):
    for y in (-.25,.05):box('Bedroom_BenchLeg',(x,y,.155),(.047,.047,.31),walnut,.007,bench)
# Soft little cat bed, away from the door route.
cylinder('Bedroom_CatBedBase',(-5.02,.80,.05),.29,.08,rug)
bpy.ops.mesh.primitive_torus_add(major_segments=40,minor_segments=12,location=(-5.02,.80,.13),major_radius=.25,minor_radius=.065)
nest=link(bpy.context.object);nest.name='Bedroom_CatBedRim';nest.data.materials.append(sage)
for face in nest.data.polygons:face.use_smooth=True

# Original geometric wall art, no external image assets.
box('Bedroom_ArtFrame',(-7,2.934,2.035),(1.39,.052,.69),walnut,.009)
box('Bedroom_ArtCanvas',(-7,2.900,2.035),(1.30,.015,.60),art_bg,.002)
ellipsoid('Bedroom_ArtSun',(-7.28,2.88,2.14),(.13,.007,.13),terracotta)
ellipsoid('Bedroom_ArtHill',(-6.84,2.878,1.88),(.39,.008,.14),sage)
ellipsoid('Bedroom_ArtHillSmall',(-7.35,2.876,1.83),(.23,.007,.095),head)

for name, xyz in {
    'BedroomDoorLiving':(-3.45,-1.10,.055),
    'BedroomDoorInside':(-5.08,-1.10,.055),
    'BedroomWalkStart':(-5.38,-1.10,.055),
    'BedroomWalkEnd':(-8.20,-1.10,.055),
    'BedroomWindowLight':(-9.35,-.75,2.20),
    'BedroomCenter':(-7.05,0,1.4),
    'BedroomPreviewCat':(-6.35,-1.00,.055),
}.items():empty(name,xyz)

bpy.context.view_layer.update()
configure_preview()
export=[o for o in room.all_objects if o.type in {'MESH','EMPTY'}]
for o in export:
    if o.name.startswith('COL_'):o.hide_render=True
bpy.ops.object.select_all(action='DESELECT')
for o in export:o.select_set(True)
bpy.context.view_layer.objects.active=room.all_objects['FloorBase']
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Source/Blender/living-room-bedroom.blend'),compress=True)
bpy.ops.export_scene.fbx(filepath=str(ROOT/'Game/Assets/Art/LivingRoom.fbx'),use_selection=True,
    object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,
    use_mesh_modifiers=True,bake_anim=False,path_mode='AUTO',mesh_smooth_type='FACE')
palette=[]
for mat in bpy.data.materials:
    p=mat.node_tree.nodes.get('Principled BSDF') if mat.use_nodes else None
    if p:palette.append({'name':mat.name,'color':list(p.inputs['Base Color'].default_value),
                          'roughness':p.inputs['Roughness'].default_value,'kind':int(mat.get('surface_kind',0))})
(ROOT/'Game/Assets/Art/palette.json').write_text(json.dumps({'materials':palette},indent=2),encoding='utf-8')
def world_bounds(items):
    points=[o.matrix_world@Vector(c) for o in items if o.type=='MESH' for c in o.bound_box]
    return [min(p[i] for p in points) for i in range(3)], [max(p[i] for p in points) for i in range(3)]
bed_min, bed_max=world_bounds([room.all_objects['Bedroom_BedFrame'],room.all_objects['Bedroom_Duvet']])
closet_min, closet_max=world_bounds(list(wardrobe.children))
report={'reference':'assets/video/catSimulator02.mp4','livingRoomM':[8.5,7.25],
        'bedroomM':[5.6,6.1],'doorwayWidthM':1.6,'bedM':[2.03,2.4],
        'deskM':[2.2,.88], 'looseProps':[o.name for o in bedroom.objects if o.name.startswith('Bedroom_Prop_')],
        'bedSideClearanceM':{'wardrobeSide':round(bed_min[0]-closet_max[0],3),'otherSide':round(-4.33-bed_max[0],3)},
        'mainWalkingRouteBlender':[[-5.38,-1.1],[-8.20,-1.1]],
        'objects':len(export),'meshObjects':sum(o.type=='MESH' for o in export),
        'source':'Source/Blender/living-room-bedroom.blend','existingCatUnchanged':True}
(OUT/'layout.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('BEDROOM_EXPORTED',json.dumps(report),flush=True)
