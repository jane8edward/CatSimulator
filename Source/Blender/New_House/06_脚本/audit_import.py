"""Import and render the new Tripo batch in isolated native Blender asset files."""
import bpy,sys,json,math,time,traceback,gc
from pathlib import Path
from mathutils import Vector,Matrix
ROOT=Path(__file__).resolve().parents[1]
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
worker=int(args[0]) if args else 0;workers=int(args[1]) if len(args)>1 else 1
items=json.loads((ROOT/'00_管理/解压清单.json').read_text())
out=ROOT/'05_渲染与检查/01_导入检查';out.mkdir(exist_ok=True)

def camera_at(s,cam,angle,bounds):
 center,dims=bounds;radius=max(dims.length/2,.1)
 if angle=='front':direction=Vector((4,-6,3.8))
 elif angle=='rear':direction=Vector((-4,6,3.3))
 else:direction=Vector((3,-5,-3.8))
 cam.location=center+direction.normalized()*radius*3.8;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=radius*2.6;s.camera=cam

def render_setup(s):
 s.render.engine='CYCLES';s.cycles.samples=12;s.cycles.use_denoising=True
 s.render.resolution_x=400;s.render.resolution_y=400;s.render.resolution_percentage=100
 s.render.image_settings.file_format='PNG';s.render.image_settings.color_mode='RGB';s.render.film_transparent=False
 s.view_settings.view_transform='AgX';s.world=bpy.data.worlds.new('NH_AuditWorld');s.world.use_nodes=True;next(n for n in s.world.node_tree.nodes if n.type=='BACKGROUND').inputs[0].default_value=(1,1,1,1);next(n for n in s.world.node_tree.nodes if n.type=='BACKGROUND').inputs[1].default_value=.7
 pref=bpy.context.preferences.addons['cycles'].preferences
 try:
  pref.compute_device_type='METAL';pref.refresh_devices()
  for d in pref.devices:d.use=d.type=='METAL'
  s.cycles.device='GPU'
 except Exception:s.cycles.device='CPU'
 for name,pos,energy,size in [('Key',(3,-4,5),400,4),('Fill',(-4,-1,2),180,4),('Rim',(2,4,4),300,3)]:
  d=bpy.data.lights.new('NH_Audit_'+name,'AREA');d.energy=energy;d.shape='DISK';d.size=size;o=bpy.data.objects.new(d.name,d);s.collection.objects.link(o);o.location=pos;o.rotation_euler=(Vector((0,0,.45))-o.location).to_track_quat('-Z','Y').to_euler()
 d=bpy.data.cameras.new('NH_Audit_Camera');cam=bpy.data.objects.new(d.name,d);s.collection.objects.link(cam)
 return cam

for ordinal,item in enumerate(x for x in items if x.get('models')):
 if ordinal%workers!=worker:continue
 ident=item['id'];asset_dir=ROOT/'03_整理资产'/ident;asset_dir.mkdir(exist_ok=True)
 if (asset_dir/'导入检查.json').exists() and (asset_dir/f'{ident}_Asset.blend').exists():continue
 start=time.time();report={'id':ident,'archive':item['archive'],'source':item['models'][0]}
 try:
  bpy.ops.wm.read_factory_settings(use_empty=True);s=bpy.context.scene;s.name='NH_Asset_'+ident;bpy.context.preferences.filepaths.save_version=0
  source=ROOT/item['models'][0]
  bpy.ops.import_scene.fbx(filepath=str(source),use_image_search=True)
  meshes=[o for o in s.objects if o.type=='MESH'];assert meshes,'No mesh imported'
  original_bound=[o.matrix_world@Vector(c) for o in meshes for c in o.bound_box]
  lo=Vector(tuple(min(v[k] for v in original_bound) for k in range(3)));hi=Vector(tuple(max(v[k] for v in original_bound) for k in range(3)));dims=hi-lo
  report['importBounds']={'min':list(lo),'max':list(hi),'dimensions':list(dims)}
  factor=1/max(dims);center=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
  asset=bpy.data.collections.new('NH_LIB_'+ident);s.collection.children.link(asset)
  root=bpy.data.objects.new('NH_'+ident+'_ROOT',None);asset.objects.link(root);root['source_archive']=item['archive'];root['asset_id']=ident
  countv=countf=countloose=0
  for i,o in enumerate(meshes,1):
   matrix=Matrix.Scale(factor,4)@Matrix.Translation(-center)@o.matrix_world
   o.data.transform(matrix);o.matrix_world=Matrix.Identity(4);o.parent=root
   for col in list(o.users_collection):col.objects.unlink(o)
   asset.objects.link(o);o.name=f'NH_{ident}_Mesh_{i:02d}';o.data.name=o.name+'_Data'
   countv+=len(o.data.vertices);countf+=len(o.data.polygons)
   for face in o.data.polygons:face.use_smooth=True
   # Keep source geometry; only reduce evaluation cost in the review render.
   if len(o.data.polygons)>180000:
    mod=o.modifiers.new('NH_Review_DetailBudget','DECIMATE');mod.ratio=180000/len(o.data.polygons);mod.use_collapse_triangulate=True
  for o in list(s.objects):
   if o not in meshes and o!=root:bpy.data.objects.remove(o,do_unlink=True)
  image_report=[]
  for im in bpy.data.images:
   if im.type!='IMAGE' or im.source!='FILE':continue
   im.filepath=bpy.path.abspath(im.filepath);before=list(im.size)
   available=bool(im.packed_file) or Path(im.filepath).is_file()
   if available and max(im.size)>2048:
    scale=2048/max(im.size);im.scale(round(im.size[0]*scale),round(im.size[1]*scale))
   if available:im.pack()
   image_report.append({'name':im.name,'originalSize':before,'reviewSize':list(im.size),'available':available})
  report.update(meshObjects=len(meshes),sourceVertices=countv,sourceFaces=countf,reviewLongestDimension_m=1,images=image_report,missingImages=[im['name'] for im in image_report if not im['available']],status='imported; visual review pending')
  cam=render_setup(s);bpy.context.view_layer.update();points=[o.matrix_world@Vector(c) for o in meshes for c in o.bound_box]
  lo=Vector(tuple(min(v[k] for v in points) for k in range(3)));hi=Vector(tuple(max(v[k] for v in points) for k in range(3)))
  bounds=((lo+hi)/2,hi-lo)
  camera_at(s,cam,'front',bounds)
  asset.asset_mark();asset.asset_data.description=item['archive']+' | Tripo source; visual QA pending'
  bpy.ops.wm.save_as_mainfile(filepath=str(asset_dir/f'{ident}_Asset.blend'),compress=True)
  for angle in ['front','rear','underside']:
   camera_at(s,cam,angle,bounds);s.render.filepath=str(out/f'{ident}_{angle}.png');bpy.ops.render.render(write_still=True)
  report['reviewDimensions']=list(hi-lo);report['seconds']=round(time.time()-start,1)
  (asset_dir/'导入检查.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
  print('NH_AUDIT_COMPLETE',ident,report['sourceVertices'],report['sourceFaces'],report['seconds'],flush=True)
 except Exception as e:
  report['error']=str(e);report['traceback']=traceback.format_exc();(asset_dir/'导入失败.json').write_text(json.dumps(report,ensure_ascii=False,indent=2));print('NH_AUDIT_FAILED',ident,str(e),flush=True)
 gc.collect()
print('NH_AUDIT_WORKER_DONE',worker,flush=True)
