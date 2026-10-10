"""Convert JonasDichelle's BlendSwap 18519 cat to a real-time Unity asset.

Original file is opened with --disable-autoexec and never saved over.
Walk/Run are sampled from the original evaluated IK rig, then constraints are
removed. Additional gameplay poses are authored for this skeleton. Groom cards
follow the original edited particle paths; their root UVs and skin weights are
interpolated from the subdivided emitter, rather than replacing the cat model.
"""
import bpy
import json
import math
import random
import hashlib
from pathlib import Path
import numpy as np
from mathutils import Matrix, Vector, Quaternion
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform

ROOT = Path('E:/Game/game_develop/CatSimulator')
OUT = ROOT / 'Source/Blender/BlendSwap18519'
ART = ROOT / 'Game/Assets/Art/BlendSwap18519'
TEX = ART / 'Textures'
for path in (OUT, ART, TEX):
    path.mkdir(parents=True, exist_ok=True)
random.seed(18519)
scene = bpy.context.scene
rig = bpy.data.objects['Armature']
body = bpy.data.objects['Cat']
eye_objects = [bpy.data.objects['Sphere'], bpy.data.objects['Sphere.001']]
source_hash = hashlib.sha256(Path(bpy.data.filepath).read_bytes()).hexdigest()
SIZE_FACTOR = .9
SCALE = .052 * SIZE_FACTOR
MAX_INFLUENCES = 8
normalizer = Matrix.Translation((0, .026*SIZE_FACTOR, .146*SIZE_FACTOR)) @ Matrix.Rotation(math.pi, 4, 'Z') @ Matrix.Scale(SCALE, 4)
scene.render.fps = 24
scene.render.fps_base = 1
scene.render.engine = 'CYCLES'
scene.cycles.samples = 16
scene.cycles.use_denoising = True
scene.use_nodes = False
scene.render.image_settings.media_type = 'IMAGE'
scene.render.image_settings.file_format = 'PNG'
try:
    prefs = bpy.context.preferences.addons['cycles'].preferences
    prefs.compute_device_type = 'CUDA'
    prefs.get_devices()
    for device in prefs.devices:
        device.use = device.type == 'CUDA'
    if any(device.use for device in prefs.devices):
        scene.cycles.device = 'GPU'
except Exception:
    scene.cycles.device = 'CPU'

coat = bpy.data.images['Untitled']
(TEX / 'CatCoat.png').write_bytes(coat.packed_file.data)
coat.filepath = str(TEX / 'CatCoat.png')
for track in rig.animation_data.nla_tracks:
    track.mute = True
for ps in body.particle_systems:
    ps.use_hair_dynamics = False
    ps.settings.child_percent = 8
    ps.settings.rendered_child_count = 8

# Capture all constrained bones, including the independent shoulders and neck.
names = [b.name for b in rig.data.bones]
deform_names = {b.name for b in rig.data.bones if b.use_deform}
control_names = [b.name for b in rig.data.bones if not b.use_deform]
rest = {b.name: b.matrix_local.copy() for b in rig.data.bones}
caches = {}
for source_name, game_name, end in [('Walk', 'Walk', 28), ('run', 'Run', 15)]:
    rig.animation_data.action = bpy.data.actions[source_name]
    for pb in rig.pose.bones:
        pb.matrix_basis.identity()
    frames = []
    for f in range(1, end + 1):
        scene.frame_set(f)
        bpy.context.view_layer.update()
        evaluated = rig.evaluated_get(bpy.context.evaluated_depsgraph_get())
        frames.append({n: evaluated.pose.bones[n].matrix.copy() for n in names})
    caches[game_name] = frames
    print('CAPTURED_ORIGINAL', source_name, len(frames), flush=True)

def original_speed(frames):
    rates = []
    for bone in ('Foot_front_L', 'Foot_front_R', 'Foot_Back_L2', 'Foot_Back_L2.002'):
        for i in range(len(frames) - 1):
            a, b = frames[i][bone].translation, frames[i+1][bone].translation
            if max(a.z, b.z) < -2.1 and b.y > a.y:
                rates.append((b.y-a.y)*SCALE*24)
    return float(np.median(rates)) if rates else .45

native_speeds = {name: original_speed(frames) for name, frames in caches.items()}

# Eye shaders use generated planar coordinates in the old file. Bake their
# colour into the actual sphere UVs so that the iris does not move in Unity.
rig.data.pose_position = 'REST'
scene.frame_set(1)
for index, obj in enumerate(eye_objects):
    material = obj.data.materials[0].copy()
    material.name = 'BS18519_Eye_' + str(index)
    obj.data.materials[0] = material
    nodes, links = material.node_tree.nodes, material.node_tree.links
    color = nodes['RGB Curves'].outputs['Color']
    output = next(n for n in nodes if n.type == 'OUTPUT_MATERIAL')
    emission = nodes.new('ShaderNodeEmission')
    links.new(color, emission.inputs['Color'])
    links.new(emission.outputs[0], output.inputs['Surface'])
    image = bpy.data.images.new('EyeBaked_'+str(index), 1024, 1024, alpha=False)
    target = nodes.new('ShaderNodeTexImage')
    target.image = image
    nodes.active = target
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.bake(type='EMIT', margin=16, use_clear=True)
    image.filepath_raw = str(TEX / ('EyeBaked_'+str(index)+'.png'))
    image.file_format = 'PNG'
    image.save()
    print('BAKED_EYE', index, flush=True)

# Read groom caches in the bind pose before removing the particle modifiers.
bpy.context.view_layer.update()
evaluated_body = body.evaluated_get(bpy.context.evaluated_depsgraph_get())
groom = []
for system_index, ps in enumerate(evaluated_body.particle_systems):
    total = len(ps.particles) + len(ps.child_particles)
    steps = 1 << ps.settings.display_step
    indices = list(range(total))
    if system_index == 0 and total > 6000:
        indices = sorted(random.sample(indices, 6000))
    paths = []
    for i in indices:
        points = [ps.co_hair(evaluated_body, particle_no=i, step=round(steps*k/4)).copy() for k in range(5)]
        if system_index == 1 and points[0].y > -2:
            # The legacy groom also contains long edited hairs on the tail.
            # Keep facial whiskers; these other strands resemble stray wires.
            continue
        if system_index == 0:
            length=sum((points[k]-points[k-1]).length for k in range(1,5))
            factor=min(1,.12/max(.00001,length))
            points=[points[0]+(point-points[0])*factor for point in points]
        if (points[-1]-points[0]).length > .005:
            paths.append(points)
    groom.append(paths)
    print('GROOM_PATHS', system_index, len(paths), flush=True)

# Keep the original geometry and its interpolated skin weights at subdivision 2.
rig.animation_data_clear()
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.object.mode_set(mode='EDIT')
for bone in rig.data.edit_bones:
    # Cyclic IK can move a connected child's head away from its parent tail.
    # A deform-only bake needs explicit animated translation for that child.
    bone.use_connect=False
bpy.ops.object.mode_set(mode='OBJECT')
for pb in rig.pose.bones:
    for constraint in list(pb.constraints):
        pb.constraints.remove(constraint)
    pb.matrix_basis.identity()
    pb.rotation_mode = 'QUATERNION'
for obj in [body] + eye_objects:
    bpy.context.view_layer.objects.active = obj
    # Only a Basis shape key exists; it has no animation and can be frozen.
    if obj.data.shape_keys:
        obj.shape_key_clear()
    for modifier in list(obj.modifiers):
        if modifier.type == 'SUBSURF':
            modifier.levels = 2 if obj == body else 1
            bpy.ops.object.modifier_apply(modifier=modifier.name)
        elif modifier.type == 'PARTICLE_SYSTEM':
            obj.modifiers.remove(modifier)
    if obj == body:
        obj.name = 'BS18519_Body'
    else:
        obj.name = 'BS18519_EyeMesh_' + str(eye_objects.index(obj))
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    # Preserve shoulder/spine blending; four influences visibly distort this rig.
    for vertex in obj.data.vertices:
        valid = sorted([(g.group, g.weight) for g in vertex.groups
                        if obj.vertex_groups[g.group].name in deform_names], key=lambda v:-v[1])[:MAX_INFLUENCES]
        total = sum(w for _, w in valid)
        for group in obj.vertex_groups:
            group.remove([vertex.index])
        if total:
            for group, weight in valid:
                obj.vertex_groups[group].add([vertex.index], weight/total, 'REPLACE')

body.data.calc_loop_triangles()
points = [v.co.copy() for v in body.data.vertices]
triangles = list(body.data.loop_triangles)
bvh = BVHTree.FromPolygons(points, [list(t.vertices) for t in triangles], all_triangles=True)
uv = body.data.uv_layers.active
vertex_weights = [{body.vertex_groups[g.group].name:g.weight for g in v.groups} for v in body.data.vertices]

def root_data(point):
    hit, normal, triangle_index, distance = bvh.find_nearest(point)
    tri = triangles[triangle_index]
    a, b, c = [points[i] for i in tri.vertices]
    bary = barycentric_transform(hit, a, b, c, Vector((1,0,0)), Vector((0,1,0)), Vector((0,0,1)))
    bary = [max(0, v) for v in bary]
    total = sum(bary)
    bary = [v/total for v in bary]
    root_uv = Vector((0,0))
    weights = {}
    for k, influence in enumerate(bary):
        root_uv += uv.data[tri.loops[k]].uv * influence
        for name, weight in vertex_weights[tri.vertices[k]].items():
            weights[name] = weights.get(name, 0) + weight*influence
    weights = sorted(weights.items(), key=lambda v:-v[1])[:MAX_INFLUENCES]
    total = sum(w for _,w in weights)
    return normal, root_uv, [(n,w/total) for n,w in weights] if total else [('Head',1)]

# One alpha card carries multiple fine strands; retain the original groom bends.
vertices, faces, card_uv, coat_uv, normals, weights, colors = [], [], [], [], [], [], []
for path in groom[0]:
    normal, root_uv, root_weights = root_data(path[0])
    tangent = (path[-1]-path[0]).normalized()
    side = tangent.cross(normal)
    if side.length < .05:
        side = tangent.cross(Vector((0,0,1)))
    if side.length < .05:
        side = tangent.cross(Vector((0,1,0)))
    side.normalize()
    width = random.uniform(.060,.095)
    start = len(vertices)
    for k, point in enumerate(path):
        t = k/4
        half_width = width*(1-.55*t)*.5
        for sign in (-1,1):
            vertices.append(tuple(point + side*half_width*sign))
            card_uv.append(((sign+1)/2,t))
            coat_uv.append(tuple(root_uv))
            normals.append(tuple(normal))
            weights.append(root_weights)
            colors.append((.95+.05*t,.95+.05*t,.95+.05*t,t))
    for k in range(4):
        i = start+k*2
        faces.append((i,i+1,i+3,i+2))
mesh = bpy.data.meshes.new('OriginalGroomCards')
mesh.from_pydata(vertices, [], faces)
mesh.update()
fur = bpy.data.objects.new('BS18519_FurCards', mesh)
scene.collection.objects.link(fur)
fur.parent = rig
for layer_name, values in [('CoatUV',coat_uv),('StrandUV',card_uv)]:
    layer = mesh.uv_layers.new(name=layer_name)
    for loop in mesh.loops:
        layer.data[loop.index].uv = values[loop.vertex_index]
attr = mesh.color_attributes.new(name='GroomColor',type='FLOAT_COLOR',domain='POINT')
for i, color in enumerate(colors):
    attr.data[i].color = color
mesh.normals_split_custom_set_from_vertices(normals)
for polygon in mesh.polygons:
    polygon.use_smooth = True
for name in names:
    fur.vertex_groups.new(name=name)
for i, influences in enumerate(weights):
    for name, weight in influences:
        fur.vertex_groups[name].add([i],weight,'REPLACE')
modifier = fur.modifiers.new('Skin original groom','ARMATURE')
modifier.object = rig

# Preserve the author's edited whiskers as small skinned tubes.
w_vertices, w_faces, w_weights = [], [], []
for path in groom[1]:
    normal, _, root_weights = root_data(path[0])
    start = len(w_vertices)
    direction = (path[-1]-path[0]).normalized()
    side = direction.cross(Vector((0,0,1)))
    if side.length < .05:
        side = direction.cross(Vector((0,1,0)))
    side.normalize()
    up = direction.cross(side).normalized()
    for k, point in enumerate(path):
        radius = .0035*(1-.9*k/4)
        for j in range(5):
            angle = j/5*math.tau
            w_vertices.append(tuple(point + radius*(side*math.cos(angle)+up*math.sin(angle))))
            w_weights.append(root_weights)
    for k in range(4):
        for j in range(5):
            w_faces.append((start+k*5+j,start+k*5+(j+1)%5,start+(k+1)*5+(j+1)%5,start+(k+1)*5+j))
wm = bpy.data.meshes.new('OriginalWhiskers')
wm.from_pydata(w_vertices,[],w_faces)
wm.update()
whiskers = bpy.data.objects.new('BS18519_Whiskers',wm)
scene.collection.objects.link(whiskers)
whiskers.parent = rig
for name in names:
    whiskers.vertex_groups.new(name=name)
for i, influences in enumerate(w_weights):
    for name, weight in influences:
        whiskers.vertex_groups[name].add([i],weight,'REPLACE')
whiskers.modifiers.new('Skin original whiskers','ARMATURE').object = rig
for polygon in wm.polygons:
    polygon.use_smooth = True

# Generated strand alpha; the coat colours still come from the author's atlas.
rng = np.random.default_rng(18519)
height, width = 512, 128
x = np.arange(width)[None,:]/(width-1)
t = np.arange(height)[:,None]/(height-1)
alpha = np.zeros((height,width),dtype=np.float32)
for centre in np.linspace(.03,.97,21):
    amplitude = rng.uniform(.005,.015)
    phase = rng.uniform(0,math.tau)
    curve = centre + amplitude*np.sin(t*7+phase)*t
    strand = np.exp(-((x-curve)/rng.uniform(.004,.007))**2)
    strand *= np.clip((rng.uniform(.76,1.03)-t)*14,0,1)
    alpha = np.maximum(alpha,strand)
pixels = np.ones((height,width,4),dtype=np.float32)
pixels[:,:,3] = alpha
mask = bpy.data.images.new('FurStrandAlpha',width,height,alpha=True)
mask.pixels.foreach_set(pixels.ravel())
mask.filepath_raw = str(TEX / 'FurStrandAlpha.png')
mask.file_format = 'PNG'
mask.save()

def surface(name,image=None,color=(1,1,1,1),roughness=.7):
    material=bpy.data.materials.new(name)
    material.use_nodes=True
    bsdf=material.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value=color
    bsdf.inputs['Roughness'].default_value=roughness
    if image:
        node=material.node_tree.nodes.new('ShaderNodeTexImage')
        node.image=image
        material.node_tree.links.new(node.outputs['Color'],bsdf.inputs['Base Color'])
    return material

body_material=surface('BS18519_Coat',coat)
body.data.materials.clear()
body.data.materials.append(body_material)
for p in body.data.polygons:p.material_index=0
fur_material=surface('BS18519_Fur',coat)
fur.data.materials.append(fur_material)
fn, fl = fur_material.node_tree.nodes, fur_material.node_tree.links
alpha_node=fn.new('ShaderNodeTexImage');alpha_node.image=mask
uv_node=fn.new('ShaderNodeUVMap');uv_node.uv_map='StrandUV'
fl.new(uv_node.outputs['UV'],alpha_node.inputs['Vector'])
fl.new(alpha_node.outputs['Alpha'],fn.get('Principled BSDF').inputs['Alpha'])
for index,obj in enumerate(eye_objects):
    obj.data.materials.clear()
    obj.data.materials.append(surface('BS18519_Eye_'+str(index),bpy.data.images['EyeBaked_'+str(index)],roughness=.14))
whiskers.data.materials.append(surface('BS18519_Whisker',color=(.65,.62,.55,1),roughness=.5))

# Independent gameplay animations for the same rig. Matrix-space offsets avoid
# relying on old IK controls at runtime. Walk/Run above remain the author's poses.
front_left = set()
front_right = set()
for side, bone in [(front_left,rig.data.bones['Thigh_front_L']),
                   (front_right,rig.data.bones['Thigh_front_R'])]:
    side.add(bone.name)
    side.update(b.name for b in bone.children_recursive)
head = {b.name for b in rig.data.bones['Neck'].children_recursive} | {'Neck'}
spine = {n for n in names if n.startswith('Spine') and 'controller' not in n}
upper = front_left | front_right | head | spine | {'Shoulder_front_L'}
tail = {n for n in names if n.startswith('Tail') and n!='Tail_controller'}
hip = rest['Hip'].translation.copy()

def rotate_about(point, angle, axis='X'):
    return Matrix.Translation(point) @ Matrix.Rotation(angle,4,axis) @ Matrix.Translation(-point)

def pose(name,t):
    matrices={n:m.copy() for n,m in rest.items()}
    if name=='Idle':
        angle=.02*math.sin(t*math.tau)
        turn=rotate_about(rest['Head'].translation,angle,'Z')
        for n in head:matrices[n]=turn@matrices[n]
        sway=rotate_about(rest['Tail'].translation,.05*math.sin(t*math.tau),'Y')
        for n in tail:matrices[n]=sway@matrices[n]
    elif name=='Paw':
        reach=math.sin(math.pi*min(1,t/.78)) if t<.78 else 0
        raise_paw=rotate_about(rest['Thigh_front_R'].translation,-.95*reach)
        for n in front_right:matrices[n]=raise_paw@matrices[n]
        look=rotate_about(rest['Head'].translation,.08*reach,'X')
        for n in head:matrices[n]=look@matrices[n]
    elif name=='Scratch':
        entry=min(1,t/.12,(1-t)/.12)
        entry=entry*entry*(3-2*entry)
        rearing=rotate_about(hip,-math.radians(48)*entry)
        for n in upper:matrices[n]=rearing@matrices[n]
        for group,phase in [(front_left,0),(front_right,math.pi)]:
            stroke=.22*math.sin(t*math.tau*5+phase)*entry
            shoulder=next(n for n in group if n.startswith('Thigh_front'))
            pivot=matrices[shoulder].translation
            motion=rotate_about(pivot,stroke)
            for n in group:matrices[n]=motion@matrices[n]
    elif name=='Jump':
        tuck=math.sin(math.pi*t)
        for group in [front_left,front_right]:
            shoulder=next(n for n in group if n.startswith('Thigh_front'))
            motion=rotate_about(rest[shoulder].translation,-.34*tuck)
            for n in group:matrices[n]=motion@matrices[n]
        for thigh in ['Thigh_Back_L','Thigh_Back_R']:
            motion=rotate_about(rest[thigh].translation,.28*tuck)
            for bone in [rig.data.bones[thigh]]+list(rig.data.bones[thigh].children_recursive):
                matrices[bone.name]=motion@matrices[bone.name]
    elif name=='Land':
        crouch=math.sin(math.pi*t)*.22
        for n in head|spine|{'Hip','Shoulder_front_L'}:
            matrices[n].translation.z-=crouch
    return matrices

for name,duration in [('Idle',2),('Jump',.8),('Land',.25),('Paw',.95),('Scratch',2.4)]:
    count=round(duration*24)+1
    caches[name]=[pose(name,i/(count-1)) for i in range(count)]

for action in list(bpy.data.actions):
    bpy.data.actions.remove(action)
rig.data.pose_position='POSE'
rig.animation_data_create()
clips=[]
for name,frames in caches.items():
    action=bpy.data.actions.new(name)
    action.use_fake_user=True
    rig.animation_data.action=action
    previous={}
    for index,matrices in enumerate(frames):
        rebuilt={}
        for bone in rig.data.bones:
            target=matrices[bone.name]
            arguments={}
            if bone.parent:
                arguments={'parent_matrix':rebuilt[bone.parent.name],
                           'parent_matrix_local':bone.parent.matrix_local}
            basis=bone.convert_local_to_pose(target,bone.matrix_local,invert=True,**arguments)
            location,rotation,scale=basis.decompose()
            if bone.name in previous and previous[bone.name].dot(rotation)<0:
                rotation.negate()
            previous[bone.name]=rotation.copy()
            # Old IK stretch can introduce shear. Compensate children using the
            # exported parent's TRS matrix, preserving their world positions.
            rebuilt[bone.name]=bone.convert_local_to_pose(Matrix.LocRotScale(location,rotation,scale),bone.matrix_local,**arguments)
            pb=rig.pose.bones[bone.name]
            pb.location=location;pb.rotation_quaternion=rotation;pb.scale=scale
            for path in ['location','rotation_quaternion','scale']:
                pb.keyframe_insert(data_path=path,frame=index+1,group=bone.name)
    clips.append({'name':name,'frames':len(frames),'seconds':(len(frames)-1)/24,
                  'origin':'JonasDichelle original IK bake' if name in ['Walk','Run'] else 'CatSimulator supplemental gameplay animation'})

# Confirm the original poses survived the conversion before the FBX stage.
max_error=0
bone_errors=[]
for name in ['Walk','Run']:
    rig.animation_data.action=bpy.data.actions[name]
    for index in [0,len(caches[name])//2,len(caches[name])-1]:
        scene.frame_set(index+1)
        bpy.context.view_layer.update()
        for n in names:
            error=(rig.pose.bones[n].matrix.translation-caches[name][index][n].translation).length*SCALE
            max_error=max(max_error,error)
            if error>.002:bone_errors.append({'clip':name,'frame':index+1,'bone':n,'error':error})
if max_error>.002:
    (OUT/'bake-errors.json').write_text(json.dumps(bone_errors,indent=2),encoding='utf-8')
    raise RuntimeError('Original animation bake changed bone positions: '+str(max_error))
if (OUT/'bake-errors.json').exists():
    (OUT/'bake-errors.json').unlink()

export_objects=[rig,body,fur,whiskers]+eye_objects
for obj in list(bpy.data.objects):
    if obj not in export_objects:
        bpy.data.objects.remove(obj,do_unlink=True)
rig.name='BS18519_Rig'
rig.matrix_world=normalizer
rig.animation_data.action=bpy.data.actions['Idle']
scene.frame_start=1;scene.frame_end=49;scene.frame_set(1)
bpy.context.view_layer.update()
# Remove unused material/image datablocks, including the unrelated studio HDRI.
for material in list(bpy.data.materials):
    if material.users==0:bpy.data.materials.remove(material)
for image in list(bpy.data.images):
    if image.users==0:bpy.data.images.remove(image)
for image in bpy.data.images:
    if image.source=='FILE' and image.size[0]>0:
        image.pack()
scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
bpy.ops.object.select_all(action='DESELECT')
for obj in export_objects:obj.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Cat_Game.blend'),compress=True)
bpy.ops.export_scene.fbx(filepath=str(ART/'AnimatedCat.fbx'),use_selection=True,
    object_types={'ARMATURE','MESH'},axis_forward='-Z',axis_up='Y',
    add_leaf_bones=False,use_armature_deform_only=True,armature_nodetype='NULL',
    bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,
    bake_anim_use_all_actions=True,bake_anim_force_startend_keying=True,
    bake_anim_step=1,bake_anim_simplify_factor=0,mesh_smooth_type='FACE',
    use_mesh_modifiers=True,path_mode='COPY',embed_textures=False)
report={'source':'https://blendswap.com/blend/18519','author':'JonasDichelle',
    'license':'CC-BY (version unspecified on the asset page)',
    'source_sha256':source_hash,'original_file_unchanged':True,'scale':SCALE,
    'bones':len(names),'cards':len(groom[0]),'whiskers':len(groom[1]),
    'nonDeformBones':control_names,'skinWeightsExcludeControllers':True,
    'maxInfluences':MAX_INFLUENCES,
    'sizeFactorFromInitialImport':SIZE_FACTOR,'maxCoatHairLengthM':.12*SCALE,
    'original_animation_max_position_error_m':max_error,'nativeSpeeds':native_speeds,
    'clips':clips,'triangles':sum(sum(len(p.vertices)-2 for p in obj.data.polygons) for obj in export_objects if obj.type=='MESH'),
    'meshes':[{'name':obj.name,'vertices':len(obj.data.vertices),'triangles':sum(len(p.vertices)-2 for p in obj.data.polygons)} for obj in export_objects if obj.type=='MESH']}
(OUT/'conversion-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
(ART/'conversion-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('CAT_CONVERSION_COMPLETE',json.dumps(report),flush=True)
