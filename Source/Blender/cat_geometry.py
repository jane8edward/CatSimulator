# Executed by generate_assets.py with its scene/material helpers in scope.
active_collection = cat_collection
coat_mat = material('Cat_Coat',(.5,.3,.13),.76)
coat_mat.node_tree.nodes.new('ShaderNodeVertexColor').layer_name='CoatColor'
vertex_node=next(n for n in coat_mat.node_tree.nodes if n.type=='VERTEX_COLOR')
coat_mat.node_tree.links.new(vertex_node.outputs['Color'],coat_mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
pink = material('Cat_Nose',(.45,.23,.21),.45)
inner = material('Cat_InnerEar',(.46,.27,.24),.8)
eye_socket = material('Cat_EyeRim',(.105,.072,.044),.45)
iris = material('Cat_Iris',(.39,.43,.17),.18)
pupil = material('Cat_Pupil',(.006,.009,.008),.14)
whisker_mat=material('Cat_Whiskers',(.83,.79,.70),.42)

parts=[]
for name,location,scale in [
    ('Torso',(0,-.015,.285),(.159,.267,.172)),
    ('Haunch',(0,-.17,.253),(.169,.135,.159)),
    ('Neck',(0,.205,.303),(.113,.14,.133)),
    ('Head',(0,.309,.389),(.145,.127,.124)),
    ('CheekL',(-.075,.362,.343),(.081,.074,.075)),
    ('CheekR',(.075,.362,.343),(.081,.074,.075)),
    ('Muzzle',(0,.420,.331),(.066,.045,.049)),
    ('Chin',(0,.4,.3),(.057,.051,.034))]:
    parts.append(ellipsoid(name,location,scale,coat_mat))
for side in (-1,1):
    parts.append(ellipsoid('FrontUpper',(side*.102,.16,.174),(.052,.055,.119),coat_mat))
    parts.append(ellipsoid('FrontPaw',(side*.102,.199,.037),(.047,.075,.034),coat_mat))
    parts.append(ellipsoid('HindUpper',(side*.113,-.195,.172),(.067,.087,.119),coat_mat))
    parts.append(ellipsoid('HindPaw',(side*.113,-.12,.036),(.053,.083,.033),coat_mat))
bpy.ops.object.select_all(action='DESELECT')
for obj in parts: obj.select_set(True)
bpy.context.view_layer.objects.active=parts[0]
bpy.ops.object.join()
body=bpy.context.object; body.name='CatBody'
body.data.remesh_voxel_size=.009
bpy.ops.object.voxel_remesh()
smooth=body.modifiers.new('Sculpt smoothing','SMOOTH'); smooth.factor=1.2; smooth.iterations=4
bpy.ops.object.modifier_apply(modifier=smooth.name)
decimate=body.modifiers.new('Game topology','DECIMATE'); decimate.ratio=.55
bpy.ops.object.modifier_apply(modifier=decimate.name)
for face in body.data.polygons: face.use_smooth=True
body.data.materials.clear(); body.data.materials.append(coat_mat)

def coat_color(point):
    x,y,z=point
    white = z < .18 or (y>.10 and z<.345) or (y>.30 and z<.365) or (y>.37 and abs(x)<.028) or (abs(x)>.08 and z<.335 and y>-.26)
    if white: return (.80,.77,.69,1)
    stripe = math.sin(y*62 + math.sin(z*35)*1.4 + abs(x)*12)
    dark = stripe>.72
    hairs = .018*math.sin(x*1800+z*2100)*math.sin(y*1200)
    return (.10+hairs,.09+hairs,.071+hairs,1) if dark else (.34+hairs,.265+hairs,.175+hairs,1)

def color_mesh(obj, color_function=coat_color):
    colors=obj.data.color_attributes.new(name='CoatColor',type='FLOAT_COLOR',domain='POINT')
    for vertex in obj.data.vertices: colors.data[vertex.index].color=color_function(obj.matrix_world @ vertex.co)

color_mesh(body)
cat_objects=[body]
rig_meshes=[body]
head_details=[]

for side in (-1,1):
    x=side*.102
    verts=[(x-side*.054,.277,.464),(x+side*.040,.297,.457),(x+side*.024,.278,.586),
           (x-side*.046,.335,.459),(x+side*.032,.34,.453),(x+side*.020,.306,.581)]
    faces=[(0,1,2),(5,4,3),(0,3,4,1),(1,4,5,2),(2,5,3,0)]
    mesh=bpy.data.meshes.new('EarMesh');mesh.from_pydata(verts,[],faces);mesh.update()
    obj=bpy.data.objects.new('CatEar',mesh);cat_collection.objects.link(obj);obj.data.materials.append(coat_mat)
    bevel=obj.modifiers.new('Soft ear edges','BEVEL');bevel.width=.008;bevel.segments=3
    bpy.context.view_layer.objects.active=obj;obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    color_mesh(obj);head_details.append(obj)
    verts=[(x-side*.032,.343,.469),(x+side*.023,.346,.465),(x+side*.020,.313,.563)]
    mesh=bpy.data.meshes.new('InnerEarMesh');mesh.from_pydata(verts,[],[(0,1,2)]);mesh.update()
    obj=bpy.data.objects.new('InnerEar',mesh);cat_collection.objects.link(obj);obj.data.materials.append(inner);head_details.append(obj)
    center=(side*.067,.421,.391)
    head_details.append(ellipsoid('EyeRim',center,(.039,.018,.029),eye_socket,32,16))
    head_details.append(ellipsoid('Iris',(side*.067,.434,.391),(.028,.009,.024),iris,40,20))
    head_details.append(ellipsoid('Pupil',(side*.067,.443,.391),(.0045,.003,.022),pupil,24,16))
    head_details.append(ellipsoid('EyeGlint',(side*.067-.008,.445,.400),(.005,.003,.006),whisker_mat,16,12))

head_details.append(ellipsoid('Nose',(0,.461,.338),(.020,.011,.012),pink,24,16))
for side in (-1,1):
    for index in range(4):
        curve=bpy.data.curves.new('WhiskerCurve','CURVE');curve.dimensions='3D';curve.bevel_depth=.00065;curve.bevel_resolution=1
        spline=curve.splines.new('POLY');spline.points.add(3)
        for step,point in enumerate(spline.points):
            t=step/3;point.co=(side*(.048+t*.115),.425+t*.015,.328+(index-1.5)*.009+t*(index-1.5)*.010,1)
        obj=bpy.data.objects.new('Whisker',curve);cat_collection.objects.link(obj);curve.materials.append(whisker_mat)
        bpy.context.view_layer.objects.active=obj;bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.ops.object.convert(target='MESH')
        head_details.append(bpy.context.object)

tail_points=[Vector((0,-.265,.270)),Vector((.015,-.37,.34)),Vector((.025,-.42,.46)),Vector((.015,-.46,.58)),Vector((0,-.52,.67)),Vector((-.02,-.59,.69))]
verts,faces=[],[]
for index,point in enumerate(tail_points):
    tangent=(tail_points[min(index+1,5)]-tail_points[max(index-1,0)]).normalized()
    right=tangent.cross(Vector((1,0,0))).normalized();up=tangent.cross(right).normalized()
    radius=.037*(1-index/6)**.6
    for j in range(20):
        a=j*math.tau/20;verts.append(tuple(point+(right*math.cos(a)+up*math.sin(a))*radius))
for index in range(5):
    for j in range(20):a=index*20+j;faces.append((a,index*20+(j+1)%20,(index+1)*20+(j+1)%20,a+20))
faces.append(tuple(range(100,120)))
mesh=bpy.data.meshes.new('TailMesh');mesh.from_pydata(verts,[],faces);mesh.update()
tail=bpy.data.objects.new('CatTail',mesh);cat_collection.objects.link(tail);mesh.materials.append(coat_mat)
for face in mesh.polygons:face.use_smooth=True
color_mesh(tail,lambda p:(.15,.09,.035,1) if math.sin(p.z*63)>.0 else (.48,.30,.14,1))
rig_meshes.append(tail)

# Sparse real geometry breaks the silhouette without transparent fur overdraw.
random.seed(13)
furverts,furfaces,furcolors=[],[],[]
for vertex in random.sample(list(body.data.vertices),min(2300,len(body.data.vertices))):
    p=body.matrix_world@vertex.co;n=vertex.normal.normalized()
    if p.z<.10:continue
    axis=n.cross(Vector((0,0,1)))
    if axis.length<.1:axis=Vector((1,0,0))
    axis.normalize();length=random.uniform(.004,.009);width=.0007
    start=len(furverts);furverts.extend([tuple(p-axis*width),tuple(p+axis*width),tuple(p+n*length+axis*.001)])
    furfaces.append((start,start+1,start+2));furcolors.extend([coat_color(p)]*3)
mesh=bpy.data.meshes.new('ShortFurMesh');mesh.from_pydata(furverts,[],furfaces);mesh.update()
fur=bpy.data.objects.new('CatShortFur',mesh);cat_collection.objects.link(fur);mesh.materials.append(coat_mat)
attribute=mesh.color_attributes.new(name='CoatColor',type='FLOAT_COLOR',domain='POINT')
for index,color in enumerate(furcolors):attribute.data[index].color=color
rig_meshes.append(fur)

# Generic skeletal rig; all exported meshes share one avatar for later cat variants.
armature=bpy.data.armatures.new('CatSkeleton')
rig=bpy.data.objects.new('CatRig',armature);cat_collection.objects.link(rig)
bpy.context.view_layer.objects.active=rig;bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
bone_specs=[('Root',(0,0,.03),(0,0,.13),None),('Pelvis',(0,-.18,.28),(0,-.04,.29),'Root'),('Spine',(0,-.04,.29),(0,.2,.32),'Pelvis'),('Head',(0,.2,.32),(0,.38,.39),'Spine')]
for side,name in [(-1,'L'),(1,'R')]:
    bone_specs.extend([(f'Front.{name}',(side*.102,.145,.28),(side*.102,.17,.135),'Spine'),(f'FrontPaw.{name}',(side*.102,.17,.135),(side*.102,.21,.036),f'Front.{name}'),(f'Hind.{name}',(side*.113,-.195,.27),(side*.113,-.21,.13),'Pelvis'),(f'HindPaw.{name}',(side*.113,-.21,.13),(side*.113,-.12,.035),f'Hind.{name}')])
for index in range(5):bone_specs.append((f'Tail{index}',tail_points[index],tail_points[index+1],'Pelvis' if index==0 else f'Tail{index-1}'))
for name,head,end,parent in bone_specs:
    bone=armature.edit_bones.new(name);bone.head=head;bone.tail=end
    if parent:bone.parent=armature.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT')

def weights_for(point,tail_mode=False):
    x,y,z=point
    if tail_mode:
        index=min(4,max(0,int((z-.27)/.085)));return {f'Tail{index}':1}
    if y>.255 and z>.26:return {'Head':1}
    if z<.23 and abs(x)>.063:
        side='R' if x>0 else 'L';leg='Front' if y>.005 else 'Hind';t=max(0,min(1,(z-.075)/.13))
        return {f'{leg}.{side}':t,f'{leg}Paw.{side}':1-t}
    if y<-.105:return {'Pelvis':1}
    if y>.16:return {'Spine':.65,'Head':.35}
    return {'Spine':.7,'Pelvis':.3}

def skin(obj,head=False,tail_mode=False):
    groups={name:obj.vertex_groups.new(name=name) for name,_,_,_ in bone_specs}
    for vertex in obj.data.vertices:
        weights={'Head':1} if head else weights_for(obj.matrix_world@vertex.co,tail_mode)
        for name,value in weights.items():
            if value>.001:groups[name].add([vertex.index],value,'REPLACE')
    modifier=obj.modifiers.new('Cat skin','ARMATURE');modifier.object=rig
    parent_keep(obj,rig)

for obj in rig_meshes:skin(obj,tail_mode=obj==tail)
for obj in head_details:skin(obj,head=True)
cat_objects=list(cat_collection.all_objects)

rig.animation_data_create()
for action_name,length in [('Idle',60),('Walk',32),('Run',22),('Jump',22),('Land',14),('Paw',36),('Scratch',66)]:
    action=bpy.data.actions.new(action_name);rig.animation_data.action=action
    for frame in range(0,length+1,2):
        t=frame/length;wave=math.sin(t*math.tau)
        for bone in rig.pose.bones:bone.rotation_mode='XYZ';bone.rotation_euler=(0,0,0);bone.location=(0,0,0)
        rig.pose.bones['Head'].rotation_euler.x=.035*wave
        for index in range(5):rig.pose.bones[f'Tail{index}'].rotation_euler.z=.035*math.sin(t*math.tau+index*.5)
        if action_name in ('Walk','Run'):
            amplitude=.32 if action_name=='Walk' else .52
            for index,name in enumerate(('L','R')):
                phase=t*math.tau+index*math.pi
                for leg,offset in [('Front',0),('Hind',math.pi)]:
                    swing=math.sin(phase+offset)
                    rig.pose.bones[f'{leg}.{name}'].rotation_euler.x=swing*amplitude
                    rig.pose.bones[f'{leg}Paw.{name}'].rotation_euler.x=-max(0,swing)*amplitude*.75
            rig.pose.bones['Spine'].location.y=abs(wave)*.007
        elif action_name in ('Jump','Land'):
            stretch=math.sin(t*math.pi)
            for name in ('L','R'):
                rig.pose.bones[f'Front.{name}'].rotation_euler.x=-stretch*.62
                rig.pose.bones[f'Hind.{name}'].rotation_euler.x=stretch*.55
                rig.pose.bones[f'FrontPaw.{name}'].rotation_euler.x=stretch*.28
            rig.pose.bones['Spine'].rotation_euler.x=stretch*.10
        elif action_name=='Paw':
            stretch=math.sin(t*math.pi)
            rig.pose.bones['Front.R'].rotation_euler.x=-stretch*1.1
            rig.pose.bones['Front.R'].rotation_euler.z=stretch*.18
            rig.pose.bones['Head'].rotation_euler.x=stretch*.12
        elif action_name=='Scratch':
            rig.pose.bones['Spine'].rotation_euler.x=-.18*math.sin(t*math.pi)
            for name,phase in [('L',0),('R',math.pi)]:
                rig.pose.bones[f'Front.{name}'].rotation_euler.x=-.7*math.sin(t*math.pi)+.22*math.sin(t*math.tau*4+phase)
        for bone in rig.pose.bones:
            bone.keyframe_insert(data_path='rotation_euler',frame=frame,group=bone.name)
            bone.keyframe_insert(data_path='location',frame=frame,group=bone.name)
    action.use_fake_user=True
rig.animation_data.action=bpy.data.actions.get('Idle')
scene.frame_set(0)
scene.render.fps=30
CAT_OBJECTS=list(cat_collection.all_objects)
