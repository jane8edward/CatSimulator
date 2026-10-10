"""Editable, reference-driven tabby cat. Run with Blender 5.2, not system Python.

The three supplied views remain unchanged. Their flank colours are projected onto
a newly modelled surface; the face, underside and extremities are painted in 3D.
The output is separate from the existing playable character and living room.
"""
import bpy
import math
import random
import json
import bisect
from pathlib import Path
from mathutils import Vector
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Source/Blender/LihuaCat'
OUT.mkdir(parents=True, exist_ok=True)
random.seed(61010)
if bpy.app.background:
    bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.data.scenes.new('LihuaCat_ReferenceWorkshop')
if bpy.context.window:
    bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0
model = bpy.data.collections.new('LihuaCat_Model')
studio = bpy.data.collections.new('Studio_RenderOnly')
references = bpy.data.collections.new('Reference_Images')
for collection in (model, studio, references):
    scene.collection.children.link(collection)
references.hide_render = True

def activate(obj):
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

def move(obj, collection=model):
    for old in list(obj.users_collection):
        old.objects.unlink(obj)
    collection.objects.link(obj)
    return obj

def mesh_object(name, verts, faces, mat=None, collection=model):
    data = bpy.data.meshes.new(name + '_Mesh')
    data.from_pydata(verts, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    collection.objects.link(obj)
    if mat:
        data.materials.append(mat)
    for face in data.polygons:
        face.use_smooth = True
    return obj

def material(name, color, roughness=.65, metallic=0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*color, 1)
    bsdf.inputs['Roughness'].default_value = roughness
    bsdf.inputs['Metallic'].default_value = metallic
    return mat

coat = material('Lihua_Coat', (.20, .18, .145), .88)
nodes, links = coat.node_tree.nodes, coat.node_tree.links
bsdf = nodes.get('Principled BSDF')
attr = nodes.new('ShaderNodeVertexColor')
attr.layer_name = 'CoatColor'
links.new(attr.outputs['Color'], bsdf.inputs['Base Color'])
noise = nodes.new('ShaderNodeTexNoise')
noise.inputs['Scale'].default_value = 1450
noise.inputs['Detail'].default_value = 2
bump = nodes.new('ShaderNodeBump')
bump.inputs['Strength'].default_value = .21
bump.inputs['Distance'].default_value = .00023
links.new(noise.outputs['Fac'], bump.inputs['Height'])
links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
bsdf.inputs['Subsurface Weight'].default_value = .025
fur_mat = material('Lihua_ShortFur', (.18, .16, .12), .92)
fn = fur_mat.node_tree.nodes.new('ShaderNodeAttribute')
fn.attribute_name = 'CoatColor'
fur_mat.node_tree.links.new(fn.outputs['Color'], fur_mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
pink = material('Nose_Leather', (.22, .095, .073), .46)
ear_skin = material('InnerEar_WarmSkin', (.085, .052, .048), .93)
rim = material('Eyelid_Charcoal', (.016, .012, .009), .65)
mouth_mat = material('Mouth_Nostrils', (.009, .006, .005), .75)
eye_mat = material('Eye_Iris_OliveAmber', (.20, .22, .073), .17)
eye_mat.node_tree.nodes.get('Principled BSDF').inputs['Coat Weight'].default_value = .65
pupil_mat = material('Eye_Pupil', (.0015, .002, .0015), .12)
whisker_mat = material('Whisker_Ivory', (.64, .59, .49), .42)

# Keep orthographic image guides inside the file. No reference bitmap is edited.
reference_images = []
for index, path in enumerate(sorted((ROOT / 'assets/photo/lihuacat').glob('*.png'))):
    im = bpy.data.images.load(str(path), check_existing=False)
    im.pack()
    reference_images.append(im)
    obj = bpy.data.objects.new('Reference_' + path.stem, None)
    references.objects.link(obj)
    obj.empty_display_type = 'IMAGE'
    obj.data = im
    obj.empty_display_size = .9
    obj.location = (1.0 + index * 1.0, 0, .40)
    obj.rotation_euler = (math.pi / 2, 0, 0)
    obj.hide_render = True
references.hide_viewport = True

# Load pixels once. Repeated indexed access to Image.pixels is very expensive.
side_images = []
for im in reference_images[1:]:
    w, h = im.size
    buf = np.empty(w * h * 4, dtype=np.float32)
    im.pixels.foreach_get(buf)
    side_images.append((buf.reshape(h, w, 4), w, h))
front_image=reference_images[0]
fw,fh=front_image.size
front_buf=np.empty(fw*fh*4,dtype=np.float32)
front_image.pixels.foreach_get(front_buf)
front_pixels=front_buf.reshape(fh,fw,4)

def smoothstep(a, b, x):
    t = min(1., max(0., (x-a)/(b-a)))
    return t*t*(3-2*t)

def projected_color(x, y, z):
    # Fit the core flank to the supplied side views; legs have their own paint.
    data, w, h = side_images[0 if x < 0 else 1]
    if x < 0:
        u = (.486 -  .690 * y) * w
        v = (.866 - 1.582 * z) * h
    else:
        u = (.492 +  .685 * y) * w
        v = (.856 - 1.558 * z) * h
    u = min(w-2., max(0., u))
    v = min(h-2., max(0., h-1-v))
    ix, iy = int(u), int(v)
    tx, ty = u-ix, v-iy
    c = ((data[iy,ix,:3]*(1-tx)+data[iy,ix+1,:3]*tx)*(1-ty)
         +(data[iy+1,ix,:3]*(1-tx)+data[iy+1,ix+1,:3]*tx)*ty)
    # Image pixel values are encoded sRGB; colour attributes are scene-linear.
    return np.power(np.maximum(c, 0), 2.2)

def front_color(x,z):
    u=min(fw-2.,max(0.,(.499+.795*x)*fw))
    v=min(fh-2.,max(0.,fh-1-(.832-1.255*z)*fh))
    ix,iy=int(u),int(v);tx,ty=u-ix,v-iy
    c=((front_pixels[iy,ix,:3]*(1-tx)+front_pixels[iy,ix+1,:3]*tx)*(1-ty)
       +(front_pixels[iy+1,ix,:3]*(1-tx)+front_pixels[iy+1,ix+1,:3]*tx)*ty)
    return np.power(np.maximum(c,0),2.2)

def coat_color(point, region='body'):
    x, y, z = point
    fine = .007*math.sin(y*920+z*1570)*math.sin(x*1770-z*840)
    base = np.array((.115+fine, .105+fine, .084+fine))
    dark = np.array((.018, .017, .014))
    if region == 'tail':
        phase = z*93 + y*24
        stripe = smoothstep(.15, .56, math.sin(phase))
        c = base*(1-stripe)+dark*stripe
        if z > .566:
            c = dark*(.85+fine)
        return (*c, 1)
    if region == 'ear':
        return (.12+fine, .105+fine, .075+fine, 1)
    # Individually shaped leg bands, warm pale belly, and tabby M on the head.
    if z < .182 and abs(x) > .035:
        stripe = smoothstep(.57, .85, math.sin(z*136+y*31+abs(x)*18))
        c = base*(1-stripe)+dark*stripe
        if z < .048:
            c = np.array((.14, .127, .105)) + fine
        return (*c, 1)
    if y > .224:
        c = base.copy()
        # Dark brow tracks form a visible M; cheek tracks taper to the muzzle.
        if z > .337:
            tracks = [0., .016+.17*(.385-z), -.016-.17*(.385-z), .048, -.048]
            d = min(abs(x-q) for q in tracks)
            stripe = 1-smoothstep(.0022, .0060, d)
            c = base*(1-stripe)+dark*stripe
        elif abs(x) > .036:
            phase = z*148 + y*19 + abs(x)*32
            stripe = smoothstep(.62, .89, math.sin(phase))
            c = base*(1-stripe)+dark*stripe
        if y > .350 and z < .311:
            c = np.array((.22, .199, .165)) + fine
        if z < .271:
            c = np.array((.18, .163, .135)) + fine
        if .268<z<.413 and y>.281:
            weight=smoothstep(.275,.320,y)*.91
            c=c*(1-weight)+front_color(x,z)*weight
        return (*c, 1)
    phase = y*141 + math.sin(z*35)*1.7 + math.sin(y*33+z*11)*.9
    stripe = smoothstep(.48, .78, math.sin(phase))
    c = base*(1-stripe)+dark*stripe
    if -.259 < y < .202 and .162 < z < .348:
        weight = smoothstep(.020, .065, abs(x))*.94
        weight *= (1-smoothstep(.320, .352, z))
        weight *= smoothstep(.153, .184, z)
        c = c*(1-weight)+projected_color(x,y,z)*weight
    if abs(x) < .031 and z < .20:
        c = c*.4 + np.array((.15, .136, .107))*.6
    return (*np.maximum(c, .002), 1)

def paint(obj, region='body'):
    ca = obj.data.color_attributes.new(name='CoatColor', type='FLOAT_COLOR', domain='POINT')
    colors = np.array([coat_color(obj.matrix_world @ v.co, region) for v in obj.data.vertices],dtype=np.float32)
    ca.data.foreach_set('color', colors.ravel())

def ellipsoid(name, pos, scale, mat=coat, seg=48, rings=32):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, location=pos)
    obj = move(bpy.context.object)
    obj.name = name
    obj.scale = scale
    activate(obj)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if mat:
        obj.data.materials.append(mat)
    for f in obj.data.polygons:
        f.use_smooth = True
    return obj

def tube(name, points, radii, sides=20, mat=coat):
    verts, faces = [], []
    for i, p in enumerate(points):
        p = Vector(p)
        tangent = (Vector(points[min(i+1,len(points)-1)])-Vector(points[max(0,i-1)])).normalized()
        a = tangent.cross(Vector((1,0,0)))
        if a.length < .1:
            a = tangent.cross(Vector((0,1,0)))
        a.normalize()
        b = tangent.cross(a).normalized()
        radius = radii[i]
        for j in range(sides):
            angle = j*math.tau/sides
            verts.append(tuple(p+radius*(a*math.cos(angle)+b*math.sin(angle))))
    for i in range(len(points)-1):
        for j in range(sides):
            a = i*sides+j
            faces.append((a,i*sides+(j+1)%sides,(i+1)*sides+(j+1)%sides,a+sides))
    faces.append(tuple(reversed(range(sides))))
    faces.append(tuple(range((len(points)-1)*sides,len(points)*sides)))
    return mesh_object(name,verts,faces,mat)

def curve(name, points, radius, mat, collection=model):
    data = bpy.data.curves.new(name+'_Curve','CURVE')
    data.dimensions = '3D'
    data.resolution_u = 12
    data.bevel_depth = radius
    data.bevel_resolution = 2
    spline = data.splines.new('BEZIER')
    spline.bezier_points.add(len(points)-1)
    for p, co in zip(spline.bezier_points, points):
        p.co = co
        p.handle_left_type = p.handle_right_type = 'AUTO'
    spline.bezier_points[-1].radius = .08
    obj = bpy.data.objects.new(name,data)
    collection.objects.link(obj)
    data.materials.append(mat)
    return obj

# Anatomical volumes are fused into one watertight editable surface.
parts = []
for name, pos, scale in [
    ('Ribcage',(0,.015,.253),(.078,.185,.079)),
    ('Abdomen',(0,-.115,.245),(.073,.156,.073)),
    ('Pelvis',(0,-.222,.254),(.080,.098,.079)),
    ('Withers',(0,.133,.270),(.070,.081,.073)),
    ('Neck',(0,.214,.289),(.060,.094,.071)),
    ('Skull',(0,.293,.337),(.068,.066,.077)),
    ('Jaw',(0,.306,.294),(.061,.059,.036)),
    ('Cheek.L',(-.043,.311,.302),(.033,.035,.031)),
    ('Cheek.R',(.043,.311,.302),(.033,.035,.031)),
    ('NoseBridge',(0,.346,.317),(.022,.044,.024)),
    ('WhiskerPad.L',(-.019,.370,.296),(.028,.028,.021)),
    ('WhiskerPad.R',(.019,.370,.296),(.028,.028,.021)),
    ('Chin',(0,.356,.276),(.028,.029,.015)),
]:
    parts.append(ellipsoid(name,pos,scale))

for side in (-1,1):
    sx = side*.058
    parts.append(ellipsoid('Scapula',(sx,.117,.251),(.033,.057,.073)))
    parts.append(tube('Foreleg',[(sx,.122,.258),(sx,.111,.191),(sx,.141,.104),(sx,.149,.045)], [.036,.029,.018,.016]))
    parts.append(ellipsoid('ForePaw',(sx,.168,.027),(.024,.038,.022)))
    parts.append(ellipsoid('Thigh',(side*.061,-.218,.216),(.053,.072,.086)))
    parts.append(tube('HindLeg',[(side*.063,-.215,.23),(side*.066,-.158,.147),(side*.064,-.242,.076),(side*.062,-.236,.042)], [.043,.028,.015,.016]))
    parts.append(ellipsoid('HindPaw',(side*.062,-.209,.024),(.026,.042,.019)))
    for toe in range(4):
        tx = sx+(toe-1.5)*.0105
        parts.append(ellipsoid('ForeToe',(tx,.193,.019),(.0068,.018,.013),seg=24,rings=16))
        tx = side*.062+(toe-1.5)*.011
        parts.append(ellipsoid('HindToe',(tx,-.180,.018),(.007,.017,.011),seg=24,rings=16))

for obj in parts:
    obj.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.join()
body = bpy.context.object
body.name = 'LihuaCat_Body'
body.data.remesh_voxel_size = .0025
bpy.ops.object.voxel_remesh()
smooth = body.modifiers.new('Anatomical surface relaxation','SMOOTH')
smooth.factor = .65
smooth.iterations = 4
bpy.ops.object.modifier_apply(modifier=smooth.name)
bpy.ops.object.quadriflow_remesh(use_mesh_symmetry=True, target_faces=16000, smooth_normals=True, seed=14)
body.data.materials.clear()
body.data.materials.append(coat)
for f in body.data.polygons:
    f.use_smooth = True
activate(body)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
# Shape real orbital recesses and eyelid attachment surfaces around both eyes.
for vertex in body.data.vertices:
    p=vertex.co
    if p.y<.309 or not (.316<p.z<.367):continue
    side=-1 if p.x<0 else 1
    center=Vector((side*.0345,.351,.334))
    normal=Vector((side*.43,.90,.06)).normalized()
    right=Vector((.90,-side*.43,0)).normalized()
    up=Vector((0,0,1))
    delta=p-center
    u,v=delta.dot(right),delta.dot(up)
    r=math.sqrt((u/.0180)**2+(v/.0098)**2)
    if r<1.72:
        q=delta.dot(normal)
        target=-.006 if r<.88 else -.0009
        weight=1-smoothstep(1.1,1.72,r)
        vertex.co+=normal*((target-q)*weight)
body.data.update()
paint(body)
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=1.10, island_margin=.009)
bpy.ops.object.mode_set(mode='OBJECT')
sub = body.modifiers.new('Sculpt preview subdivision','SUBSURF')
sub.levels = 1
sub.render_levels = 1

# Tail has dense longitudinal rings and a rounded black tip.
tail_path = [Vector((0,-.277,.278)),Vector((0,-.327,.298)),Vector((0,-.375,.338)),
             Vector((0,-.405,.396)),Vector((0,-.413,.458)),Vector((0,-.412,.525)),
             Vector((0,-.424,.582)),Vector((0,-.438,.598))]
tail = tube('LihuaCat_Tail',tail_path,[.023,.023,.022,.020,.018,.016,.012,.001],32)
paint(tail,'tail')
sub = tail.modifiers.new('Tail smooth surface','SUBSURF')
sub.levels = sub.render_levels = 2

# Rounded, concave pinnae. The ear front is a bowl rather than a flat triangle.
ear_objects = []
for side, label in ((-1,'L'),(1,'R')):
    verts, faces, inner_verts, inner_faces = [], [], [], []
    outer = [(side*.028,.286,.371),(side*.093,.284,.365),(side*.088,.260,.444)]
    center = Vector((side*.069,.278,.393))
    boundary = []
    for i in range(3):
        p, q = Vector(outer[i]), Vector(outer[(i+1)%3])
        for j in range(12):
            t = j/12
            v = p.lerp(q,t)
            v.y += .010*math.sin(math.pi*t)
            boundary.append(v)
    for layer in range(5):
        s = 1-layer/5
        for p in boundary:
            v = center.lerp(p,s)
            v.y -= .013*(1-s*s)
            verts.append(tuple(v))
    verts.append(tuple(center-Vector((0,.013,0))))
    for layer in range(4):
        for j in range(36):
            a=layer*36+j
            faces.append((a,layer*36+(j+1)%36,(layer+1)*36+(j+1)%36,a+36))
    for j in range(36):
        faces.append((144+j,144+(j+1)%36,180))
    ear = mesh_object('Ear.'+label,verts,faces,coat)
    paint(ear,'ear')
    solid = ear.modifiers.new('Pinna thickness','SOLIDIFY')
    solid.thickness = .005
    sub = ear.modifiers.new('Pinna smoothness','SUBSURF')
    sub.levels = sub.render_levels = 2
    ear_objects.append(ear)
    # inset skin follows the same concavity and leaves a fur rim
    for p in verts:
        v = center.lerp(Vector(p), .78)
        v.y += .0025
        inner_verts.append(tuple(v))
    inner = mesh_object('EarInterior.'+label,inner_verts,faces,ear_skin)
    sub = inner.modifiers.new('Inner ear smoothness','SUBSURF')
    sub.levels = sub.render_levels = 2
    ear_objects.append(inner)

# Almond eyes, olive irises, vertical pupils and rounded eyelids.
face_details = []
for side, label in ((-1,'L'),(1,'R')):
    center = Vector((side*.0345,.351,.334))
    normal = Vector((side*.43,.90,.06)).normalized()
    right = Vector((.90,-side*.43,0)).normalized()
    up = normal.cross(right).normalized()
    if up.z < 0:
        up = -up
    verts, faces = [tuple(center+normal*.0045)], []
    outlines = []
    for ring_index in range(1,9):
        t = ring_index/8
        for j in range(64):
            a=j*math.tau/64
            u=.0165*math.cos(a)*t
            v=.0095*math.sin(a)*t + side*u*.10
            depth=.0045*(1-t*t)
            verts.append(tuple(center+right*u+up*v+normal*depth))
            if ring_index==8:
                outlines.append(tuple(center+right*u+up*v))
    for j in range(64):
        faces.append((0,1+j,1+(j+1)%64))
    for ring_index in range(7):
        for j in range(64):
            a=1+ring_index*64+j
            faces.append((a,1+ring_index*64+(j+1)%64,1+(ring_index+1)*64+(j+1)%64,a+64))
    eye = mesh_object('Eye.'+label,verts,[tuple(reversed(f)) for f in faces],eye_mat)
    ca=eye.data.color_attributes.new(name='IrisColor',type='FLOAT_COLOR',domain='POINT')
    cols=[]
    for vertex in eye.data.vertices:
        delta=vertex.co-center
        u,v=delta.dot(right),delta.dot(up)
        r=math.sqrt(u*u+v*v)
        a=math.atan2(v,u)
        streak=.70+.22*math.sin(a*73+r*2200)+.08*math.sin(a*129)
        c=np.array((.072,.084,.026))*streak
        if r>.0091:
            c=np.array((.025,.027,.012))
        cols.append((*c,1))
    ca.data.foreach_set('color',np.array(cols,dtype=np.float32).ravel())
    if not eye_mat.node_tree.nodes.get('Iris_Color'):
        n=eye_mat.node_tree.nodes.new('ShaderNodeVertexColor');n.name='Iris_Color';n.layer_name='IrisColor'
        eye_mat.node_tree.links.new(n.outputs['Color'],eye_mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
    face_details.append(eye)
    pupverts=[tuple(center+normal*.0050)]
    for j in range(48):
        a=j*math.tau/48
        u=.0071*math.cos(a)
        v=.0083*math.sin(a)
        depth=.0047*(1-(u/.0165)**2-(v/.0095)**2)+.0004
        pupverts.append(tuple(center+right*u+up*v+normal*depth))
    pupfaces=[(0,1+j,1+(j+1)%48) for j in range(48)]
    face_details.append(mesh_object('Pupil.'+label,pupverts,[tuple(reversed(f)) for f in pupfaces],pupil_mat))
    # Single continuous upper and lower lid, with a delicate dark wet rim.
    upper=[outlines[j] for j in range(33)]
    lower=[outlines[j] for j in range(32,64)]+[outlines[0]]
    face_details.append(curve('UpperLid.'+label,upper,.00085,rim))
    face_details.append(curve('LowerLid.'+label,lower,.00055,rim))

# Small, inverted triangular nose with separate nostril folds and mouth seams.
noseverts=[(-.012,.392,.304),(.012,.392,.304),(0,.400,.290),
           (-.010,.386,.303),(.010,.386,.303),(0,.391,.290)]
nosefaces=[(0,1,2),(5,4,3),(0,3,4,1),(1,4,5,2),(2,5,3,0)]
nose=mesh_object('Nose',noseverts,nosefaces,pink)
bevel=nose.modifiers.new('Soft leather edge','BEVEL');bevel.width=.0025;bevel.segments=3
face_details.append(nose)
for side in (-1,1):
    face_details.append(curve('Nostril',[(side*.010,.394,.302),(side*.008,.397,.299),(side*.004,.399,.299)],.0008,mouth_mat))
face_details.append(curve('Philtrum',[(0,.399,.291),(0,.390,.281),(0,.384,.278)],.00055,mouth_mat))
for side in (-1,1):
    face_details.append(curve('Mouth',[(0,.384,.278),(side*.017,.381,.278),(side*.033,.371,.281)],.00055,mouth_mat))
    for row in range(3):
        for col in range(3):
            p=(side*(.019+col*.007),.394-col*.004,.287+row*.005)
            face_details.append(ellipsoid('WhiskerFollicle',p,(.0009,.00065,.0009),mouth_mat,16,8))
    for index in range(7):
        start=Vector((side*.027,.384,.291+(index-3)*.003))
        points=[start,start+Vector((side*.035,.017,(index-3)*.003)),
                start+Vector((side*.079,.019,(index-3)*.008)),
                start+Vector((side*.105,.012,(index-3)*.012))]
        face_details.append(curve('Whisker',points,.00023,whisker_mat))
    for index in range(3):
        p=Vector((side*.044,.350,.354))
        face_details.append(curve('EyebrowWhisker',[p,p+Vector((side*.019,.013,.019)),p+Vector((side*.039,.005,.041+index*.009))],.00017,whisker_mat))

# Groomed native hair curves: short on the face, longer on the trunk and tail.
def add_fur(obj, count, region='body'):
    depsgraph=bpy.context.evaluated_depsgraph_get()
    evaluated=obj.evaluated_get(depsgraph)
    mesh=evaluated.to_mesh()
    mesh.calc_loop_triangles()
    tris=list(mesh.loop_triangles)
    areas=np.array([t.area for t in tris],dtype=np.float64)
    cumulative=np.cumsum(areas)
    positions=[];radii=[];colors=[];sizes=[]
    rng=random.Random(81 if region=='body' else 99)
    for _ in range(count):
        tri=tris[bisect.bisect_left(cumulative,rng.random()*cumulative[-1])]
        a,b,c=[mesh.vertices[i] for i in tri.vertices]
        u,v=rng.random(),rng.random()
        if u+v>1:u,v=1-u,1-v
        p=a.co*(1-u-v)+b.co*u+c.co*v
        n=(a.normal*(1-u-v)+b.normal*u+c.normal*v).normalized()
        p=obj.matrix_world@p
        x,y,z=p
        if z<.022:continue
        if region=='body' and y>.338 and .311<z<.348:continue
        if region=='body':
            flow=Vector((0,-.6,-.20))
            if z<.18:flow=Vector((0,-.1,-1))
            if y>.23:flow=Vector((x*8,-.5,-.1))
            length=rng.uniform(.0020,.0046) if y<.22 else rng.uniform(.0009,.0022)
        elif region=='ear':
            flow=Vector((0,0,1));length=rng.uniform(.0015,.0040)
        else:
            flow=Vector((0,0,1));length=rng.uniform(.003,.0055)
        tangent=flow-n*flow.dot(n)
        if tangent.length>.001:tangent.normalize()
        direction=(n*.50+tangent*.80).normalized()
        positions.extend([tuple(p+n*.0001),tuple(p+direction*length*.50+n*.00025),tuple(p+direction*length+n*.00012)])
        radii.extend([.000050,.000034,.000007])
        color=np.array(coat_color(p,region))
        color[:3]*=rng.uniform(.78,1.22)
        colors.append(tuple(color))
        sizes.append(3)
    evaluated.to_mesh_clear()
    data=bpy.data.hair_curves.new(obj.name+'_Groom')
    data.add_curves(sizes)
    data.position_data.foreach_set('vector',np.array(positions,dtype=np.float32).ravel())
    radius=data.attributes.new('radius','FLOAT','POINT')
    radius.data.foreach_set('value',np.array(radii,dtype=np.float32))
    color=data.attributes.new('CoatColor','FLOAT_COLOR','CURVE')
    color.data.foreach_set('color',np.array(colors,dtype=np.float32).ravel())
    data.materials.append(fur_mat)
    data.surface=obj
    fur=bpy.data.objects.new(obj.name+'_ShortFur',data)
    model.objects.link(fur)
    fur['export_note']='Native Blender groom; bake or make fur cards before Unity use.'
    return fur

fur_objects=[add_fur(body,175000),add_fur(tail,21000,'tail')]
for obj in ear_objects:
    if obj.name.startswith('Ear.'):
        fur_objects.append(add_fur(obj,1800,'ear'))
    elif obj.name.startswith('EarInterior.'):
        fur_objects.append(add_fur(obj,6500,'ear'))

# A deformation rig is included for subsequent animation work, in a neutral pose.
rigdata=bpy.data.armatures.new('LihuaCat_Skeleton')
rig=bpy.data.objects.new('LihuaCat_Rig',rigdata)
model.objects.link(rig)
activate(rig)
bpy.ops.object.mode_set(mode='EDIT')
specs=[('Root',(0,0,.02),(0,0,.09),None),('Pelvis',(0,-.21,.25),(0,-.11,.25),'Root'),
       ('Spine',(0,-.11,.25),(0,.045,.26),'Pelvis'),('Chest',(0,.045,.26),(0,.16,.28),'Spine'),
       ('Neck',(0,.16,.28),(0,.26,.32),'Chest'),('Head',(0,.26,.32),(0,.355,.32),'Neck')]
for side,label in ((-1,'L'),(1,'R')):
    specs.extend([(f'ForeUpper.{label}',(side*.058,.12,.25),(side*.058,.11,.19),'Chest'),
                  (f'ForeLower.{label}',(side*.058,.11,.19),(side*.058,.149,.047),f'ForeUpper.{label}'),
                  (f'ForePaw.{label}',(side*.058,.149,.047),(side*.058,.193,.019),f'ForeLower.{label}'),
                  (f'HindUpper.{label}',(side*.063,-.215,.25),(side*.066,-.158,.147),'Pelvis'),
                  (f'HindLower.{label}',(side*.066,-.158,.147),(side*.064,-.242,.076),f'HindUpper.{label}'),
                  (f'HindPaw.{label}',(side*.064,-.242,.076),(side*.062,-.180,.018),f'HindLower.{label}'),
                  (f'Ear.{label}',(side*.062,.282,.371),(side*.085,.266,.438),'Head')])
for i in range(len(tail_path)-1):
    specs.append((f'Tail.{i:02}',tail_path[i],tail_path[i+1],'Pelvis' if i==0 else f'Tail.{i-1:02}'))
for name,head,end,parent in specs:
    b=rigdata.edit_bones.new(name);b.head=head;b.tail=end
    if parent:b.parent=rigdata.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT')
rig.show_in_front=True
rigdata.display_type='STICK'

def skin_weights(point, region):
    x,y,z=point
    if region=='head':return {'Head':1}
    if region.startswith('ear'):return {region.split(':')[1]:1}
    if region=='tail':
        distances=[]
        for i in range(len(tail_path)-1):
            mid=(tail_path[i]+tail_path[i+1])/2
            distances.append(((Vector(point)-mid).length,i))
        closest=sorted(distances)[:2]
        inv=[1/(d+.002)**3 for d,_ in closest]
        return {f'Tail.{i:02}':w/sum(inv) for w,(_,i) in zip(inv,closest)}
    if y>.244:return {'Head':1}
    if z<.213 and abs(x)>.027:
        label='R' if x>0 else 'L'
        if y>-.015:
            if z<.062:return {f'ForePaw.{label}':1}
            t=smoothstep(.15,.224,z)
            return {f'ForeLower.{label}':1-t,f'ForeUpper.{label}':t}
        if z<.088:return {f'HindPaw.{label}':1}
        t=smoothstep(.125,.205,z)
        return {f'HindLower.{label}':1-t,f'HindUpper.{label}':t}
    if y<-.12:return {'Pelvis':1}
    if y>.19:return {'Neck':.7,'Head':.3}
    if y>.08:return {'Chest':1}
    t=smoothstep(-.14,.06,y)
    return {'Pelvis':1-t,'Spine':t}

def skin(obj,region='body'):
    # Curves are converted only in the FBX export copy; retain editable originals.
    if obj.type!='MESH':
        obj.parent=rig
        return
    groups={name:obj.vertex_groups.new(name=name) for name,_,_,_ in specs}
    for v in obj.data.vertices:
        for name,w in skin_weights(obj.matrix_world@v.co,region).items():
            if w>.0001:groups[name].add([v.index],w,'REPLACE')
    mod=obj.modifiers.new('Skin deformation','ARMATURE');mod.object=rig
    obj.parent=rig

skin(body)
skin(tail,'tail')
for obj in ear_objects:
    label='L' if obj.name.endswith('.L') else 'R'
    skin(obj,'ear:Ear.'+label)
for obj in face_details:
    skin(obj,'head')
for obj in fur_objects:
    obj.parent=rig
rig.location.z=-min(v.co.z for v in body.data.vertices)-.0028

# Neutral grey studio to match the reference presentation.
groundmat=material('Studio_Gray',(.19,.195,.205),.83)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.003))
ground=move(bpy.context.object,studio);ground.name='Studio_Ground';ground.data.materials.append(groundmat)
world=bpy.data.worlds.new('LihuaCat_StudioWorld');scene.world=world;world.use_nodes=True
world.node_tree.nodes.get('Background').inputs['Color'].default_value=(.35,.39,.46,1)
world.node_tree.nodes.get('Background').inputs['Strength'].default_value=.38
def area(name,loc,power,size,color):
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size;data.color=color
    obj=bpy.data.objects.new(name,data);studio.objects.link(obj);obj.location=loc
    obj.rotation_euler=(Vector((0,0,.25))-obj.location).to_track_quat('-Z','Y').to_euler()
area('Key_Softbox',(-1.2,1.5,1.7),90,1.2,(1,.91,.82))
area('Fill_Softbox',(1.1,.6,.8),45,1.0,(.83,.9,1))
area('Rim_Softbox',(.2,-1.1,1.4),75,.9,(1,.95,.87))
camera_data=bpy.data.cameras.new('ReferenceCamera');camera=bpy.data.objects.new('ReferenceCamera',camera_data)
studio.objects.link(camera);scene.camera=camera;camera_data.type='ORTHO';camera_data.ortho_scale=.98
camera.location=(1.05,1.55,.77)
camera.rotation_euler=(Vector((0,-.025,.28))-camera.location).to_track_quat('-Z','Y').to_euler()
scene.render.engine='CYCLES'
scene.cycles.samples=32
scene.cycles.use_denoising=True
scene.render.resolution_x=1600;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.view_settings.view_transform='AgX'
scene.view_settings.look='AgX - Medium High Contrast'
scene.render.film_transparent=False
scene.render.fps=30
scene.frame_start=1;scene.frame_end=60

activate(body)
for screen in bpy.data.screens:
    for area_ui in screen.areas:
        if area_ui.type=='VIEW_3D':
            area_ui.spaces.active.region_3d.view_distance=1.15
            area_ui.spaces.active.region_3d.view_location=(0,0,.28)
            area_ui.spaces.active.shading.type='MATERIAL'
            area_ui.spaces.active.clip_start=.001
scene['reference_directory']='assets/photo/lihuacat'
scene['model_status']='Reference reconstruction with native groom; animation and game grooming require further work.'
scene['coordinate_system']='Metres; +Y forward; Z up; origin at ground.'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'LihuaCat.blend'),compress=True)
report={'blend':str(OUT/'LihuaCat.blend'),'body_vertices':len(body.data.vertices),
        'body_faces':len(body.data.polygons),'bones':len(specs),
        'groom_strands':sum(len(o.data.curves) for o in fur_objects),
        'reference_files':[im.name for im in reference_images]}
(OUT/'model-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
result=report
print('LIHUA_MODEL_CREATED '+json.dumps(report))
