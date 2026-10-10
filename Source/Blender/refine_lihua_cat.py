"""Non-destructive asset iteration after inspecting all actual render views."""
import bpy
import math
import numpy as np
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Source/Blender/LihuaCat'
scene=bpy.data.scenes['LihuaCat_ReferenceWorkshop']
if bpy.context.window:bpy.context.window.scene=scene
if scene.get('anatomy_revision',0)>=4:
    result={'status':'already refined'}
else:
    model=bpy.data.collections['LihuaCat_Model']
    rig=bpy.data.objects['LihuaCat_Rig']
    body=bpy.data.objects['LihuaCat_Body']
    coat=bpy.data.materials['Lihua_Coat']
    # Both pinnae face +Y. Solidify goes behind the skin, including the mirrored ear.
    for obj in model.objects:
        if obj.name.startswith(('Ear.','EarInterior.')) and obj.type=='MESH':
            for v in obj.data.vertices:v.co.z+=.025
            if sum(p.normal.y for p in obj.data.polygons)<0:
                for p in obj.data.polygons:p.flip()
            for mod in obj.modifiers:
                if mod.type=='SOLIDIFY':mod.offset=-1
            obj.data.update()
        elif obj.name.startswith(('Ear.','EarInterior.')) and obj.type=='CURVES':
            values=np.empty(len(obj.data.points)*3,dtype=np.float32)
            obj.data.position_data.foreach_get('vector',values)
            coords=values.reshape(-1,3)
            coords[:,2]+=.025
            if obj.name.startswith('EarInterior.L'):
                strands=coords.reshape(-1,3,3)
                for strand in strands:
                    strand[:,1]=strand[0,1]+np.abs(strand[:,1]-strand[0,1])+.00018
            obj.data.position_data.foreach_set('vector',values)
    # Shorter raised tail and a less protruding muzzle follow the supplied side silhouettes.
    for obj in model.objects:
        if obj.type=='MESH':
            for v in obj.data.vertices:
                if obj.name.startswith('LihuaCat_Tail'):
                    v.co.z=.278+(v.co.z-.278)*.82
                elif obj.name in {'LihuaCat_Body','Nose'} or obj.name.startswith(('WhiskerFollicle','Pupil.','Eye.')):
                    if v.co.y>.362:v.co.y=.362+(v.co.y-.362)*.82
            if obj.name.startswith('WhiskerFollicle') and obj.location.y>.362:
                obj.location.y=.362+(obj.location.y-.362)*.82
            obj.data.update()
        elif obj.type=='CURVES':
            values=np.empty(len(obj.data.points)*3,dtype=np.float32)
            obj.data.position_data.foreach_get('vector',values)
            coords=values.reshape(-1,3)
            if obj.name.startswith('LihuaCat_Tail'):
                coords[:,2]=.278+(coords[:,2]-.278)*.82
            elif obj.name.startswith('LihuaCat_Body'):
                mask=coords[:,1]>.362
                coords[mask,1]=.362+(coords[mask,1]-.362)*.82
            obj.data.position_data.foreach_set('vector',values)
        elif obj.type=='CURVE':
            for spline in obj.data.splines:
                for p in spline.bezier_points:
                    for attr in ('co','handle_left','handle_right'):
                        value=getattr(p,attr)
                        if value.y>.362:value.y=.362+(value.y-.362)*.82
    # Real eyes read mostly dark under this indoor-size pupil; avoid toy-like white glints.
    for name in ('Eye_Iris_OliveAmber','Eye_Pupil'):
        bsdf=bpy.data.materials[name].node_tree.nodes.get('Principled BSDF')
        bsdf.inputs['Roughness'].default_value=.33
        bsdf.inputs['Specular IOR Level'].default_value=.075
        bsdf.inputs['Coat Weight'].default_value=.025
    # Flesh-coloured eyelid annuli attach the opening to the orbital surface.
    bvh=BVHTree.FromPolygons([v.co for v in body.data.vertices],[p.vertices[:] for p in body.data.polygons])
    front=next(im for im in bpy.data.images if '181403625' in im.name)
    w,h=front.size
    pixels=np.empty(w*h*4,dtype=np.float32);front.pixels.foreach_get(pixels)
    pixels=pixels.reshape(h,w,4)
    for side,label in ((-1,'L'),(1,'R')):
        center=Vector((side*.0345,.351,.334))
        normal=Vector((side*.43,.90,.06)).normalized()
        right=Vector((.90,-side*.43,0)).normalized()
        up=Vector((-.0258,-.054,.995)).normalized()
        verts=[];faces=[];cols=[]
        for k in range(4):
            t=k/3
            for j in range(64):
                a=j*math.tau/64
                r=1.04+.27*t
                u=.0165*math.cos(a)*r
                v=.0095*math.sin(a)*r+side*u*.10
                p=center+right*u+up*v
                nearest=bvh.find_nearest(p)
                if nearest[0] is not None:
                    p=p.lerp(nearest[0]+normal*.0005,t)
                verts.append(tuple(p))
                ix=min(w-1,max(0,int((.499+.795*p.x)*w)))
                iy=min(h-1,max(0,int(h-1-(.832-1.255*p.z)*h)))
                c=np.power(np.maximum(pixels[iy,ix,:3],.01),2.2)
                cols.append((*c,1))
        for k in range(3):
            for j in range(64):
                faces.append((k*64+j,(k+1)*64+j,(k+1)*64+(j+1)%64,k*64+(j+1)%64))
        data=bpy.data.meshes.new('EyelidSkin.'+label+'_Mesh');data.from_pydata(verts,[],faces);data.update()
        obj=bpy.data.objects.new('EyelidSkin.'+label,data);model.objects.link(obj);data.materials.append(coat)
        color=data.color_attributes.new(name='CoatColor',type='FLOAT_COLOR',domain='POINT')
        color.data.foreach_set('color',np.array(cols,dtype=np.float32).ravel())
        for p in data.polygons:p.use_smooth=True
        vg=obj.vertex_groups.new(name='Head');vg.add(list(range(len(data.vertices))),1,'REPLACE')
        obj.parent=rig
        mod=obj.modifiers.new('Head skin','ARMATURE');mod.object=rig
    # Fine toe grooves stay separate and editable, and follow the paw bones.
    toe_mat=bpy.data.materials.new('Paw_ToeCrease');toe_mat.diffuse_color=(.045,.035,.027,1);toe_mat.use_nodes=True
    p=toe_mat.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(.045,.035,.027,1);p.inputs['Roughness'].default_value=.9
    for side,label in ((-1,'L'),(1,'R')):
        for leg,ycoords,zcoords in [('Fore',[.208,.195,.184],[.020,.032,.039]),('Hind',[-.168,-.181,-.194],[.018,.028,.034])]:
            for offset in (-.0105,0,.0105):
                x=side*(.058 if leg=='Fore' else .062)+offset
                curve=bpy.data.curves.new('ToeCrease','CURVE');curve.dimensions='3D';curve.bevel_depth=.00036;curve.bevel_resolution=1
                spline=curve.splines.new('BEZIER');spline.bezier_points.add(2)
                for bp,y,z in zip(spline.bezier_points,ycoords,zcoords):
                    bp.co=(x,y,z);bp.handle_left_type=bp.handle_right_type='AUTO'
                obj=bpy.data.objects.new('ToeCrease.'+leg+'.'+label,curve);model.objects.link(obj);curve.materials.append(toe_mat)
                obj.parent=rig;obj['deform_bone']=leg+'Paw.'+label
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True);bpy.context.view_layer.objects.active=rig
    bpy.ops.object.mode_set(mode='EDIT')
    for bone in rig.data.edit_bones:
        if bone.name.startswith('Ear.'):
            bone.head.z+=.025;bone.tail.z+=.025
        elif bone.name.startswith('Tail.'):
            bone.head.z=.278+(bone.head.z-.278)*.82
            bone.tail.z=.278+(bone.tail.z-.278)*.82
    bpy.ops.object.mode_set(mode='OBJECT')
    body.select_set(True);bpy.context.view_layer.objects.active=body;rig.select_set(False)
    scene['anatomy_revision']=4
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'LihuaCat.blend'),compress=True)
    result={'status':'refined','revision':4}
print(result)
