"""Supplement the one missing shoe reference with a simple fabric slipper prototype."""
import bpy,math,json
from pathlib import Path
from mathutils import Vector
R=Path(__file__).resolve().parents[1];bpy.ops.wm.read_factory_settings(use_empty=True);bpy.context.preferences.filepaths.save_version=0;s=bpy.context.scene;s.name='NH080_Slipper';s.unit_settings.system='METRIC';s.unit_settings.scale_length=1
col=bpy.data.collections.new('NH_LIB_NH080');s.collection.children.link(col);col.asset_mark();col.asset_data.description='Supplemental fabric slipper from provided shoe reference; no Tripo shoe file supplied.'
root=bpy.data.objects.new('NH_ASSET_NH080_Slipper',None);col.objects.link(root)
mat=bpy.data.materials.new('NH_MAT_SlipperFabric');mat.use_nodes=True;p=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED');p.inputs['Base Color'].default_value=(.66,.56,.41,1);p.inputs['Roughness'].default_value=.92
vs=[];N=64
for z in [0,.012]:
 for i in range(N):
  t=i*2*math.pi/N;y=.125*math.sin(t);w=.048*(.91+.09*math.sin(t));vs.append((w*math.cos(t),y,z))
fs=[tuple(range(N-1,-1,-1)),tuple(range(N,2*N))]+[(i,(i+1)%N,(i+1)%N+N,i+N) for i in range(N)]
me=bpy.data.meshes.new('NH_SlipperSoleMesh');me.from_pydata(vs,[],fs);me.materials.append(mat);o=bpy.data.objects.new('NH_NH080_Slipper_Sole',me);col.objects.link(o);o.parent=root;bev=o.modifiers.new('SoftFabricEdge','BEVEL');bev.width=.004;bev.segments=3
vs=[];fs=[];ny=20;nx=24
for j in range(ny+1):
 y=-.015+j*(.139/ny);r=math.sqrt(max(.002,1-(y/.126)**2));w=.047*r;h=.050*math.sqrt(max(.003,1-(max(y,0)/.125)**2))
 for i in range(nx+1):
  t=math.pi*i/nx;vs.append((w*math.cos(t),y,.014+h*math.sin(t)))
for j in range(ny):
 for i in range(nx):a=j*(nx+1)+i;fs.append((a,a+1,a+1+nx+1,a+nx+1))
me=bpy.data.meshes.new('NH_SlipperUpperMesh');me.from_pydata(vs,[],fs);me.materials.append(mat)
for f in me.polygons:f.use_smooth=True
o=bpy.data.objects.new('NH_NH080_Slipper_Upper',me);col.objects.link(o);o.parent=root;solid=o.modifiers.new('FabricThickness','SOLIDIFY');solid.thickness=.002;sub=o.modifiers.new('SoftFabricShape','SUBSURF');sub.levels=1
root['source_type']='supplemental reference modelling';root['source_id']='NH080';root['dimensions_m']=[.10,.25,.065]
s.world=bpy.data.worlds.new('NH_SlipperWorld');s.world.use_nodes=True;next(n for n in s.world.node_tree.nodes if n.type=='BACKGROUND').inputs[1].default_value=.7
ld=bpy.data.lights.new('NH_SlipperKey','AREA');ld.energy=10;ld.size=.6;l=bpy.data.objects.new(ld.name,ld);s.collection.objects.link(l);l.location=(.3,-.4,.5);l.rotation_euler=(-l.location).to_track_quat('-Z','Y').to_euler()
cd=bpy.data.cameras.new('NH_SlipperCamera');cam=bpy.data.objects.new(cd.name,cd);s.collection.objects.link(cam);cam.location=(.3,-.4,.35);cam.rotation_euler=(Vector((0,0,.03))-cam.location).to_track_quat('-Z','Y').to_euler();cd.type='ORTHO';cd.ortho_scale=.34;s.camera=cam
out=R/'03_整理资产/NH080';out.mkdir(exist_ok=True);s.render.engine='CYCLES';s.cycles.samples=24;s.cycles.use_denoising=True;s.render.resolution_x=400;s.render.resolution_y=400;s.render.resolution_percentage=100;s.render.image_settings.file_format='PNG';s.render.filepath=str(R/'05_渲染与检查/03_修正后模型/NH080_ready.png')
bpy.ops.wm.save_as_mainfile(filepath=str(out/'NH080_Ready.blend'),compress=True);bpy.ops.render.render(write_still=True)
ref=next((R/'01_参考资料/物体生成参考_v1/02_单物体图').glob('*ShoePairKit*'));mapping=R/'00_管理/资产映射与修正.json';d=json.loads(mapping.read_text());d['NH080']={'slug':'Slipper','sourceType':'supplemental','reference':str(ref.relative_to(R)),'dimensions_m':[.10,.25,.065],'correction':'missing source shoe model: supplemented from provided reference'};mapping.write_text(json.dumps(d,ensure_ascii=False,indent=2))
(out/'修正与校准.json').write_text(json.dumps({'id':'NH080','status':'supplemental reference model','reference':str(ref.relative_to(R)),'dimensions_m':[.10,.25,.065],'sourcePreserved':True,'corrections':['No shoe supplied in the 78 Tripo model packages; modelled slipper sole and fabric upper from the reference.']},ensure_ascii=False,indent=2))
print('NH080_SUPPLEMENT_CREATED',flush=True)
