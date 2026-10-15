"""Adapt the balcony cat's existing skin to the first cat's baked motion library.

Read-only inputs: Cat_Game.blend and the original Tripo FBX. Outputs are separate.
Rotations are transferred as world-space rest deltas; two-bone limb IK fits the
first cat's foot trajectories to this cat's own limb lengths and bind pose.
"""
import bpy, json, math, shutil, hashlib
from pathlib import Path
from mathutils import Matrix, Vector, Quaternion
from mathutils.kdtree import KDTree

ROOT=Path('E:/Game/game_develop/CatSimulator')
OUT=ROOT/'Source/Blender/BalconyCat'; ART=ROOT/'Game/Assets/Art/BalconyCat'
OUT.mkdir(parents=True,exist_ok=True);(ART/'Textures').mkdir(parents=True,exist_ok=True)
source=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
source_rest={b.name:source.matrix_world@b.matrix_local for b in source.data.bones}
motions={}
for name in ['Idle','Walk','Run','Jump','Land','Paw','Scratch']:
    action=bpy.data.actions[name];source.animation_data.action=action
    frames=[]
    for frame in range(1,int(action.frame_range.y)+1):
        bpy.context.scene.frame_set(frame);bpy.context.view_layer.update()
        frames.append({b.name:source.matrix_world@b.matrix for b in source.pose.bones})
    motions[name]=frames
    if name in ['Walk','Run']:motions[name]=frames+[frames[0]]
original=set(bpy.context.scene.objects)
source_fbx=next((ROOT/'Source/Blender/CatHome90/02_资产库/CHR/H90-CHR-001_ReferenceCat/Source/Original').glob('*.fbx'))
bpy.ops.import_scene.fbx(filepath=str(source_fbx))
added=set(bpy.context.scene.objects)-original
rig=next(o for o in added if o.type=='ARMATURE');body=next(o for o in added if o.type=='MESH')
for obj in original:bpy.data.objects.remove(obj,do_unlink=True)
for action in list(bpy.data.actions):bpy.data.actions.remove(action)
rig.name='BalconyCat_Rig';body.name='BalconyCat_Body'
rig.matrix_world=Matrix.Scale(.5,4)
bpy.context.view_layer.update()
rig.animation_data_clear();rig.animation_data_create()
for mod in body.modifiers:
    if mod.type=='ARMATURE':mod.use_deform_preserve_volume=False
# Normalize the supplied skin, removing tiny weights while retaining native UVs.
unweighted=0;max_influences=0;repaired=0
valid=[v for v in body.data.vertices if any(g.weight>1e-5 for g in v.groups)]
tree=KDTree(len(valid))
for i,v in enumerate(valid):tree.insert(v.co,v.index)
tree.balance()
for vertex in body.data.vertices:
    groups=sorted([(g.group,g.weight) for g in vertex.groups if g.weight>1e-5],key=lambda p:-p[1])[:4]
    total=sum(w for _,w in groups)
    if total<=0:
        _,nearest,_=tree.find(vertex.co)
        groups=sorted([(g.group,g.weight) for g in body.data.vertices[nearest].groups if g.weight>1e-5],key=lambda p:-p[1])[:4]
        total=sum(w for _,w in groups);repaired+=1
    if total<=0:unweighted+=1;continue
    for g in list(vertex.groups):body.vertex_groups[g.group].remove([vertex.index])
    for group,weight in groups:body.vertex_groups[group].add([vertex.index],weight/total,'REPLACE')
    max_influences=max(max_influences,len(groups))
if unweighted:raise RuntimeError('Unweighted vertices: '+str(unweighted))
mapping={
 'tripo::Root':'Spine','tripo::Spine_0':'Spine','tripo::Spine_1':'Spine.002','tripo::Spine_2':'Spine.004',
 'tripo::Head_0':'Neck','tripo::Head_1':'Neck.002','tripo::Head_2':'Head',
 'tripo::Head_3':'Ear','bone_7':'Ear.001','bone_29':'Shoulder_front_L','tripo::Spine_3':'Shoulder_front_L',
 'bone_34':'Spine.002','bone_39':'Thigh_Back_R','bone_44':'Thigh_Back_L',
 'bone_45':'Tail','tripo::Tail_0':'Tail.001','tripo::Tail_1':'Tail.003','tripo::Tail_2':'Tail.005','bone_49':'Tail.007'
}
for side in ['Left','Right']:
    s='L' if side=='Left' else 'R'
    for index,bone in enumerate(['Thigh_front_'+s,'Calf_Front_'+s,'Foot_front_'+s,'Foot_front_'+s]):
        mapping[f'tripo::0_{side}_Limb_{index}']=bone
    for index,bone in enumerate(['Thigh_Back_'+s,'Foot_Back_'+s,'Foot_Back_L2' if s=='L' else 'Foot_Back_L2.002','Foot_Back_L2' if s=='L' else 'Foot_Back_L2.002']):
        mapping[f'tripo::1_{side}_Limb_{index}']=bone
# Facial helpers follow the head without inheriting incompatible jaw/eye pivots.
for b in rig.data.bones:
    if b.name not in mapping:mapping[b.name]='Head'
rest={b.name:rig.matrix_world@b.matrix_local for b in rig.data.bones}
rest_inverse={n:m.inverted() for n,m in rest.items()}
paw_points={}
for group in ['0','1']:
    for side in ['Left','Right']:
        prefix=f'tripo::{group}_{side}_Limb_'
        paw_points[(group,side)]=[(body.matrix_world@v.co,[(body.vertex_groups[g.group].name,g.weight) for g in v.groups]) for v in body.data.vertices if any(body.vertex_groups[g.group].name.startswith(prefix) and g.weight>.35 for g in v.groups)]
tail_names={'bone_45','tripo::Tail_0','tripo::Tail_1','tripo::Tail_2','bone_49'}
tail_points=[(body.matrix_world@v.co,[(body.vertex_groups[g.group].name,g.weight) for g in v.groups]) for v in body.data.vertices if any(body.vertex_groups[g.group].name in tail_names and g.weight>.5 for g in v.groups)]
inverse=rig.matrix_world.inverted();parents={b.name:b.parent.name if b.parent else None for b in rig.data.bones}
order=list(rig.data.bones)
legs=[('0',s,['Thigh_front_'+('L' if s=='Left' else 'R'),'Calf_Front_'+('L' if s=='Left' else 'R')], 'Foot_front_'+('L' if s=='Left' else 'R')) for s in ['Left','Right']]
legs += [('1',s,['Thigh_Back_'+('L' if s=='Left' else 'R'),'Foot_Back_'+('L' if s=='Left' else 'R')], 'Foot_Back_L2' if s=='Left' else 'Foot_Back_L2.002') for s in ['Left','Right']]
STRIDE_SCALE=.75
def foot_motion(frames,index,foot,loop):
    def value(i):return frames[i][foot].translation-source_rest[foot].translation
    if not loop:return value(index)*.9
    # Periodic filtering removes a one-frame kink in the old sampled IK cycle.
    n=len(frames)-1;i=index%n
    return (value((i-1)%n)+2*value(i)+value((i+1)%n))*(STRIDE_SCALE/4)

def anatomical_knee(a,goal,l1,l2,pole):
    direction=(goal-a).normalized();distance=(goal-a).length
    distance=min(l1+l2-.0003,max(abs(l1-l2)+.0003,distance));goal=a+direction*distance
    bend=pole-direction*pole.dot(direction)
    if bend.length<1e-6:bend=Vector((1,0,0))-direction*direction.x
    bend.normalize();along=(distance*distance+l1*l1-l2*l2)/(2*distance)
    radius=math.sqrt(max(0,l1*l1-along*along));base=a+direction*along
    # Constrain the pole angle continuously; never switch to the opposite elbow.
    up=Vector((0,0,1))-direction*direction.z
    if radius>1e-6 and up.length>1e-5 and (base+radius*bend).z<.018:
        up.normalize();side=direction.cross(up).normalized()
        limit=math.acos(max(-1,min(1,(.018-base.z)/(radius*up.z))))
        angle=math.atan2(bend.dot(side),bend.dot(up));angle=max(-limit,min(limit,angle))
        bend=up*math.cos(angle)+side*math.sin(angle)
    return base+radius*bend,goal

max_stretch=0
for clip,frames in motions.items():
    action=bpy.data.actions.new(clip);action.use_fake_user=True;rig.animation_data.action=action;previous={}
    for index,pose in enumerate(frames):
        deltas={n:(pose[n].to_quaternion()@source_rest[n].to_quaternion().inverted()) for n in source_rest}
        locomotion=clip in ['Walk','Run']
        root_delta=(pose['Spine'].translation-source_rest['Spine'].translation)*.9
        if locomotion:root_delta.z*=.45 if clip=='Walk' else .65
        wanted={}
        for b in order:
            n=b.name;p=parents[n]
            location=(wanted[p]@rest[p].inverted()@rest[n]).translation if p else rest[n].translation+root_delta
            rotation=deltas[mapping[n]]@rest[n].to_quaternion()
            if locomotion and n in ['tripo::Root','tripo::Spine_0','tripo::Spine_1','tripo::Spine_2']:
                rotation=Quaternion().slerp(deltas[mapping[n]],.45 if clip=='Walk' else .65)@rest[n].to_quaternion()
            wanted[n]=Matrix.LocRotScale(location,rotation,Vector((.5,.5,.5)))
        # Fit native limb lengths to the original animation's foot arc.
        for group,side,_,foot in legs:
            names=[f'tripo::{group}_{side}_Limb_{i}' for i in range(4)]
            upper,lower,wrist,toe=names
            a=wanted[upper].translation.copy();c0=rest[wrist].translation
            shift=foot_motion(frames,index,foot,locomotion)
            goal=c0+shift
            goal.z=max(c0.z,goal.z)
            l1=(rest[lower].translation-rest[upper].translation).length
            l2=(rest[wrist].translation-rest[lower].translation).length
            rest_axis=(rest[wrist].translation-rest[upper].translation).normalized()
            pole=rest[lower].translation-rest[upper].translation
            pole-=rest_axis*pole.dot(rest_axis);pole.normalize()
            pole=Quaternion().slerp(deltas['Spine'],.45 if clip=='Walk' else .65)@pole
            for correction in range(5):
                max_stretch=max(max_stretch,(goal-a).length/max(.001,l1+l2))
                knee,goal=anatomical_knee(a,goal,l1,l2,pole)
                for n,start,end in [(upper,a,knee),(lower,knee,goal)]:
                    axis=rest[n].to_quaternion()@Vector((0,1,0));rotation=axis.rotation_difference((end-start).normalized())@rest[n].to_quaternion()
                    wanted[n]=Matrix.LocRotScale(start,rotation,Vector((.5,.5,.5)))
                if group=='0':
                    # Toe banking in the original rig is not the new cat's
                    # carpal joint. Keep planted paws level and remove axial twist.
                    forward=deltas[foot]@Vector((0,1,0))
                    pitch=max(math.radians(-30),min(math.radians(25),math.atan2(forward.z,forward.y)))
                    lift=max(0,min(1,(goal.z-c0.z)/.025));lift=lift*lift*(3-2*lift)
                    paw_rotation=Quaternion((1,0,0),pitch*lift)
                    wanted[wrist]=Matrix.LocRotScale(goal,paw_rotation@rest[wrist].to_quaternion(),Vector((.5,.5,.5)))
                    wanted[toe]=Matrix.LocRotScale(wanted[toe].translation,paw_rotation@rest[toe].to_quaternion(),Vector((.5,.5,.5)))
                wanted[wrist].translation=goal
                wanted[toe].translation=(wanted[wrist]@rest[wrist].inverted()@rest[toe]).translation
                if correction==4:break
                deform={n:wanted[n]@rest_inverse[n] for n in wanted}
                low=min(sum((deform[n]@point).z*w for n,w in weights) for point,weights in paw_points[(group,side)])
                if low>=.0005 and clip!='Idle':break
                goal.z+=.001-low
        solved={f'tripo::{g}_{s}_Limb_{i}' for g,s,_,_ in legs for i in range(4)}
        for b in order:
            if b.parent and b.name not in solved:
                wanted[b.name].translation=(wanted[b.parent.name]@rest_inverse[b.parent.name]@rest[b.name]).translation
        # Articulate each tail segment: raised with a curled tip when curious,
        # trailing behind for a run, rather than rotating the entire curled tail.
        phase=index/max(1,len(frames)-1);lift={'Idle':.9,'Walk':1,'Run':.12,'Paw':.85,'Scratch':.18,'Land':.45}.get(clip,.35+.3*math.sin(math.pi*phase))
        chain=['bone_45','tripo::Tail_0','tripo::Tail_1','tripo::Tail_2','bone_49']
        cursor=wanted[chain[0]].translation.copy()
        for j,n in enumerate(chain):
            low=[0,5,12,20,32][j];high=[15,45,75,95,110][j]
            angle=math.radians(low+(high-low)*lift+3*math.sin(2*math.pi*phase-j*.45))
            sway=.09*math.sin(2*math.pi*phase-j*.5)
            direction=Vector((sway,-math.cos(angle),math.sin(angle))).normalized()
            axis=rest[n].to_quaternion()@Vector((0,1,0))
            rotation=axis.rotation_difference(direction)@rest[n].to_quaternion()
            wanted[n]=Matrix.LocRotScale(cursor,rotation,Vector((.5,.5,.5)))
            cursor+=direction*rig.data.bones[n].length*.5
        for b in order:
            n=b.name;pb=rig.pose.bones[n];target=inverse@wanted[n]
            args={'parent_matrix':inverse@wanted[b.parent.name],'parent_matrix_local':b.parent.matrix_local} if b.parent else {}
            basis=b.convert_local_to_pose(target,b.matrix_local,invert=True,**args)
            location,rotation,scale=basis.decompose()
            if n in previous and previous[n].dot(rotation)<0:rotation.negate()
            previous[n]=rotation.copy();pb.rotation_mode='QUATERNION';pb.location=location;pb.rotation_quaternion=rotation;pb.scale=(1,1,1)
            for path in ['location','rotation_quaternion','scale']:pb.keyframe_insert(data_path=path,frame=index+1,group=n)
    print('RETARGETED',clip,len(frames),flush=True)
scene=bpy.context.scene;scene.render.fps=24;scene.frame_start=1;scene.frame_end=49
rig.animation_data.action=bpy.data.actions['Idle'];scene.frame_set(1)
for image in bpy.data.images:
    if image.name.startswith('tripo_image'):
        shutil.copyfile(bpy.path.abspath(image.filepath),ART/'Textures/CalicoCoat.jpg');image.filepath=str(ART/'Textures/CalicoCoat.jpg');image.pack()
for material in body.data.materials:
    material.name='BalconyCat_Coat'
    shader=next(n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED');shader.inputs['Roughness'].default_value=.72
scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);body.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'BalconyCat_Rigged.blend'),compress=True)
bpy.ops.export_scene.fbx(filepath=str(ART/'BalconyCat.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,use_armature_deform_only=True,armature_nodetype='NULL',bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=True,bake_anim_force_startend_keying=True,bake_anim_step=1,bake_anim_simplify_factor=0,mesh_smooth_type='FACE',use_mesh_modifiers=True,path_mode='COPY',embed_textures=False)
report={'source':str(source_fbx.relative_to(ROOT)),'sourceSHA256':hashlib.sha256(source_fbx.read_bytes()).hexdigest(),'motionSource':'Source/Blender/BlendSwap18519/Cat_Game.blend','retarget':'world rest rotation deltas with two-bone foot IK','nativeSkinAdapted':True,'geometryAndUVPreserved':True,'vertices':len(body.data.vertices),'triangles':sum(len(p.vertices)-2 for p in body.data.polygons),'bones':len(rig.data.bones),'unweightedVertices':unweighted,'repairedUnweightedVertices':repaired,'maxInfluences':max_influences,'clips':{n:len(f) for n,f in motions.items()},'boneMapping':mapping,'maxRequestedLegReachRatio':max_stretch}
report.update({'strideScale':STRIDE_SCALE,'elbowConstraint':'continuous anatomical pole','tail':'raised idle/walk, trailing run; articulated five-segment curl'})
(OUT/'rig-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('BALCONY_CAT_EXPORTED',json.dumps({k:v for k,v in report.items() if k!='boneMapping'}),flush=True)
