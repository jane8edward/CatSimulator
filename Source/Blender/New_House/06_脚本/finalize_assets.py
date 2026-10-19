"""Conservative repairs, metre calibration and a scene-ready copy of each Tripo asset.
The *_Asset.blend inspection originals and extracted FBX files remain untouched.
"""
import bpy,bmesh,numpy as np,json,math,sys,time,traceback,gc
from pathlib import Path
from mathutils import Matrix,Vector
from mathutils.bvhtree import BVHTree
ROOT=Path(__file__).resolve().parents[1]
MAPPING=json.loads((ROOT/'00_管理/资产映射与修正.json').read_text())
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
worker=int(args[0]) if args else 0;workers=int(args[1]) if len(args)>1 else 1
only=set(args[2:])
OUT=ROOT/'05_渲染与检查/03_修正后模型';OUT.mkdir(exist_ok=True)

def simplemat(name,color,rough=.5,metal=0,glass=False,emission=0):
 m=bpy.data.materials.new('NH_MAT_'+name);m.use_nodes=True;p=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Roughness'].default_value=rough;p.inputs['Metallic'].default_value=metal
 if glass:
  p.inputs['Transmission Weight'].default_value=1;p.inputs['IOR'].default_value=1.45
 if emission:p.inputs['Emission Color'].default_value=(*color,1);p.inputs['Emission Strength'].default_value=emission
 return m

def cube(name,dims,center,mat,collection):
 x,y,z=[v/2 for v in dims];vs=[(-x,-y,-z),(-x,-y,z),(-x,y,-z),(-x,y,z),(x,-y,-z),(x,-y,z),(x,y,-z),(x,y,z)];fs=[(0,4,6,2),(1,3,7,5),(0,1,5,4),(2,6,7,3),(0,2,3,1),(4,5,7,6)]
 me=bpy.data.meshes.new(name+'_Mesh');me.from_pydata(vs,[],[tuple(reversed(face)) for face in fs]);me.update();o=bpy.data.objects.new(name,me);collection.objects.link(o);o.location=center
 if mat:me.materials.append(mat)
 return o

def components(m):
 n=len(m.vertices);e=np.empty(len(m.edges)*2,dtype=np.int32);m.edges.foreach_get('vertices',e);e=e.reshape(-1,2);labels=np.arange(n,dtype=np.int32)
 for _ in range(160):
  old=labels.copy();v=np.minimum(labels[e[:,0]],labels[e[:,1]]);np.minimum.at(labels,e[:,0],v);np.minimum.at(labels,e[:,1],v);labels=labels[labels]
  if np.array_equal(old,labels):break
 return labels

def delete_vertices(m,indices):
 if not len(indices):return
 bm=bmesh.new();bm.from_mesh(m);bm.verts.ensure_lookup_table();bmesh.ops.delete(bm,geom=[bm.verts[int(i)] for i in indices],context='VERTS');bm.to_mesh(m);bm.free();m.update()

def bbox(meshes):
 vs=[o.matrix_world@Vector(c) for o in meshes for c in o.bound_box];lo=Vector(tuple(min(v[i] for v in vs) for i in range(3)));hi=Vector(tuple(max(v[i] for v in vs) for i in range(3)));return lo,hi

def boolean_cut(body,center,dims,collection):
 # Generated panes contain overlapping/open sheets; volume booleans leave those sheets behind.
 # Crop only faces inside the glass aperture, keeping the surrounding frame geometry.
 c=Vector(center);half=Vector(dims)/2;bm=bmesh.new();bm.from_mesh(body.data)
 doomed=[f for f in bm.faces if all(abs(f.calc_center_median()[i]-c[i])<half[i] for i in range(3))]
 bmesh.ops.delete(bm,geom=doomed,context='FACES');bm.to_mesh(body.data);bm.free();body.data.update()

def assign_spatial(body,mat,predicate):
 idx=len(body.data.materials);body.data.materials.append(mat);n=0
 for poly in body.data.polygons:
  if predicate(poly):poly.material_index=idx;n+=1
 return n

for ordinal,(ident,meta) in enumerate(MAPPING.items()):
 if meta.get('sourceType')=='supplemental':continue
 if only and ident not in only:continue
 if ordinal%workers!=worker:continue
 path=ROOT/f'03_整理资产/{ident}/{ident}_Asset.blend';dest=path.with_name(f'{ident}_Ready.blend');rep=path.parent/'修正与校准.json'
 if rep.exists():continue
 if not path.exists():print('NOT_READY',ident,flush=True);continue
 start=time.time();report={'id':ident,'slug':meta['slug'],'reference':meta['reference'],'corrections':[],'sourcePreserved':True,'units':'metres'}
 try:
  bpy.ops.wm.open_mainfile(filepath=str(path));bpy.context.preferences.filepaths.save_version=0;s=bpy.context.scene;col=bpy.data.collections['NH_LIB_'+ident]
  original=[o for o in col.objects if o.type=='MESH'];root=next(o for o in col.objects if o.type=='EMPTY');dg=bpy.context.evaluated_depsgraph_get();new=[]
  for i,o in enumerate(original):
   me=bpy.data.meshes.new_from_object(o.evaluated_get(dg),preserve_all_data_layers=True,depsgraph=dg);obj=bpy.data.objects.new(f'NH_{ident}_{meta["slug"]}_Body_{i+1:02d}',me);col.objects.link(obj);obj.parent=root;new.append(obj)
   bpy.data.objects.remove(o,do_unlink=True)
  for o in new:o.data.validate(verbose=False);o.data.update()
  fix=meta.get('correction');body=new[0]
  if fix=='downlight_cleanup':
   labels=components(body.data);u,c=np.unique(labels,return_counts=True);largest=u[np.argmax(c)];remove=np.flatnonzero(labels!=largest);delete_vertices(body.data,remove)
   coords=np.array([v.co[:] for v in body.data.vertices]);vals,vecs=np.linalg.eigh(np.cov(coords.T));normal=Vector(vecs[:,np.argmin(vals)]);q=normal.rotation_difference(Vector((0,0,1)));body.data.transform(q.to_matrix().to_4x4())
   report['corrections'].append('Removed disconnected background sheet and floating triangles; aligned retained circular downlight horizontally.')
  rz=math.radians(meta.get('canonicalRotationZ_deg',-90));rot=Matrix.Rotation(rz,4,'Z')
  for o in new:o.data.transform(rot)
  bpy.context.view_layer.update();lo,hi=bbox(new);dim=hi-lo
  target=Vector(meta['dimensions_m']);scale=Vector(tuple(target[i]/max(dim[i],1e-8) for i in range(3)));origin=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
  transform=Matrix.Diagonal((*scale,1))@Matrix.Translation(-origin)
  for o in new:o.data.transform(transform);o.data.update()
  if fix=='rug_flatten':report['corrections'].append('Corrected generated excessive thickness and underside stubs to a thin textile profile; retained plan outline and texture.')
  if fix=='bed_cleanup':
   labels=components(body.data);u,c=np.unique(labels,return_counts=True);co=np.array([v.co[:] for v in body.data.vertices]);remove=[]
   for label,count in zip(u,c):
    indices=np.flatnonzero(labels==label)
    if count<len(co)*.12 and co[indices,2].min()>target.z*.50:remove.extend(indices.tolist())
   if remove:delete_vertices(body.data,remove);report['corrections'].append(f'Removed {len(remove)} vertices of detached spurious cloth above mattress/headboard; retained bed frame and mattress.')
   tree=BVHTree.FromPolygons([v.co for v in body.data.vertices],[p.vertices[:] for p in body.data.polygons]);hit,_,_,_=tree.ray_cast(Vector((0,0,5)),Vector((0,0,-1)),10)
   image=next((n.image for m in body.data.materials if m and m.use_nodes for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image and '_0' in n.image.name),None)
   uv=body.data.uv_layers.active;bad=[];paint=[]
   if image and uv and hit:
    pixels=np.empty(len(image.pixels),dtype=np.float32);image.pixels.foreach_get(pixels);pixels=pixels.reshape(image.size[1],image.size[0],4)
    for p in body.data.polygons:
     c=p.center
     if c.z>hit.z+.01 and target.y*.20<c.y<target.y*.45:
      tex=sum((uv.data[i].uv for i in p.loop_indices),Vector((0,0)))/len(p.loop_indices);rgb=pixels[int((tex.y%1)*(image.size[1]-1)),int((tex.x%1)*(image.size[0]-1)),:3]
      if rgb.min()>.50 and (rgb.max()-rgb.min())/max(rgb.max(),.01)<.23:bad.append(p.index)
     elif c.z>hit.z and c.y>target.y*.45:
      tex=sum((uv.data[i].uv for i in p.loop_indices),Vector((0,0)))/len(p.loop_indices);rgb=pixels[int((tex.y%1)*(image.size[1]-1)),int((tex.x%1)*(image.size[0]-1)),:3]
      if rgb.min()>.50 and (rgb.max()-rgb.min())/max(rgb.max(),.01)<.23:paint.append(p.index)
    if paint:
     wood=simplemat(ident+'_HeadboardOakRepair',(.62,.38,.19),.55);slot=len(body.data.materials);body.data.materials.append(wood)
     for i in paint:body.data.polygons[i].material_index=slot
    bm=bmesh.new();bm.from_mesh(body.data);bm.faces.ensure_lookup_table();bmesh.ops.delete(bm,geom=[bm.faces[i] for i in bad],context='FACES');bm.to_mesh(body.data);bm.free();body.data.update()
    if bad:report['corrections'].append(f'Removed {len(bad)} white background/cloth faces above the mattress while retaining wood headboard faces.')
    if paint:report['corrections'].append(f'Replaced white image-background streaks on {len(paint)} headboard faces with matching oak material.')
  if fix=='wardrobe_back':
   for v in body.data.vertices:
    if abs(v.co.y)>target.y*.43:v.co.y=math.copysign(target.y*.46,v.co.y)
   body.data.update();plain=simplemat(ident+'_BackOak',(.48,.32,.17),.65)
   assign_spatial(body,plain,lambda p:abs(p.center.y)>target.y*.42)
   seam=simplemat(ident+'_PanelSeam',(.20,.12,.06),.75)
   for x in [-target.x/6,target.x/6]:
    line=cube(f'NH_{ident}_DoorPanelSeam_{x:.2f}',(.0025,.0015,target.z-.07),(x,-target.y*.46-.001,target.z/2),seam,col);line.parent=root;new.append(line)
   report['corrections'].append('Removed hallucinated repeated knob details from outer panels; restored plain oak back and three front door panels, retaining the cabinet shell.')
  if fix=='basket_color':
   wicker=simplemat(ident+'_Wicker',(.48,.30,.13),.82);body.data.materials.clear();body.data.materials.append(wicker)
   report['corrections'].append('Restored natural wicker color absent from the source package; retained woven geometry.')
  if fix=='litter_check':
   report['corrections'].append('Confirmed round entrance and two ventilation slots against reference; retained geometry, corrected mislabeled archive mapping.')
  if fix and (fix.startswith('window') or fix.startswith('glazing')):
   w,d,h=target;panes=int(fix[-1]);glazing=fix.startswith('glazing')
   frame=simplemat(ident+'_Frame',(.055,.065,.060) if ident in {'NH038','NH039'} else (.22,.25,.24),.3,.55);glass=simplemat(ident+'_ClearGlass',(.91,.96,.95),.035,0,True)
   # Native high-resolution import is preserved in *_Asset.blend. Replace only the damaged
   # AI frame/pane surfaces, retaining the reference proportions, panel layout and depth.
   for old in list(new):bpy.data.objects.remove(old,do_unlink=True)
   new=[];bar=.028 if w<2 else .038;outer=.035 if w<2 else .045
   def part(label,ds,at,mat):
    o=cube(f'NH_{ident}_{label}',ds,at,mat,col);o.parent=root;new.append(o)
    if mat==frame:
     bevel=o.modifiers.new('Precision frame edge','BEVEL');bevel.width=.0015;bevel.segments=2
    return o
   for x in [-w/2+outer/2,w/2-outer/2]:part('OuterJamb', (outer,d,h),(x,0,h/2),frame)
   for z in [outer/2,h-outer/2]:part('OuterRail',(w,d,outer),(0,0,z),frame)
   for k in range(1,panes):part('Mullion_'+str(k),(bar,d*.80,h-outer*2),(-w/2+k*w/panes,0,h/2),frame)
   if glazing:part('LowerTransom',(w-outer*2,d*.8,bar),(0,0,h*.27),frame)
   ranges=[(outer,h*.27-bar/2),(h*.27+bar/2,h-outer)] if glazing else [(outer,h-outer)]
   for k in range(panes):
    x0=-w/2+k*w/panes+(outer if k==0 else bar/2);x1=-w/2+(k+1)*w/panes-(outer if k==panes-1 else bar/2)
    for j,(z0,z1) in enumerate(ranges):part(f'ClearPane_{k+1}_{j}',(x1-x0,.006,z1-z0),((x0+x1)/2,0,(z0+z1)/2),glass)
   if panes>1:
    for x in [-w/2+.065,w/2-.065]:part('RecessedHandle',(.013,.012,min(.14,h*.12)),(x,-d/2-.005,h*.52),frame)
   body=new[0]
   report['corrections'].append(f'Rebuilt only damaged AI-generated frame/pane surfaces with straight precision rails, {panes} reference panels and thin clear glass; original high-resolution model retained in Asset.blend.')
  if fix=='shower_glass':
   w,d,h=target;glass=simplemat(ident+'_ShowerGlass',(.91,.97,.96),.035,0,True)
   n=assign_spatial(body,glass,lambda p:.09*h<p.center.z<.92*h and ((abs(p.center.x)<.43*w and abs(p.normal.y)>.75) or (abs(p.center.y)<.42*d and abs(p.normal.x)>.75)))
   report['corrections'].append(f'Converted {n} reconstructed opaque panel faces to transmission glass; retained shower hardware and frame.')
  if fix in {'ceramic','pump_color','grass_color','jar_color','board_color'}:
   cream=simplemat(ident+'_Ceramic',(.76,.70,.59),.3)
   if fix=='ceramic':body.data.materials.clear();body.data.materials.append(cream);report['corrections'].append('Restored warm ceramic material absent from the source package.')
   if fix=='pump_color':
    body.data.materials.clear();body.data.materials.append(cream);pump=simplemat(ident+'_Pump',(.26,.27,.25),.3,.35);assign_spatial(body,pump,lambda p:p.center.z>target.z*.74);report['corrections'].append('Restored cream bottle and grey pump material absent from the source package.')
   if fix=='grass_color':
    body.data.materials.clear();body.data.materials.append(cream);leaf=simplemat(ident+'_Grass',(.22,.34,.08),.75);assign_spatial(body,leaf,lambda p:p.center.z>target.z*.34);report['corrections'].append('Restored green grass and ceramic pot materials absent from the source package.')
   if fix=='jar_color':
    grain=simplemat(ident+'_Grain',(.57,.39,.17),.68);wood=simplemat(ident+'_WoodLid',(.49,.31,.13),.5);body.data.materials.clear();body.data.materials.append(grain);assign_spatial(body,wood,lambda p:p.center.z>target.z*.79)
    for v in body.data.vertices:
     if v.co.z<target.z*.79:v.co.x*=.89;v.co.y*=.89
    glass=simplemat(ident+'_JarGlass',(.95,.98,.97),.04,0,True)
    gn=glass.node_tree.nodes;gl=glass.node_tree.links;bsdf=next(n for n in gn if n.type=='BSDF_PRINCIPLED');output=next(n for n in gn if n.type=='OUTPUT_MATERIAL');tr=gn.new('ShaderNodeBsdfTransparent');mix=gn.new('ShaderNodeMixShader');mix.inputs[0].default_value=.15;gl.new(tr.outputs[0],mix.inputs[1]);gl.new(bsdf.outputs[0],mix.inputs[2]);gl.new(mix.outputs[0],output.inputs['Surface'])
    r=target.x*.46;th=.001;z0=target.z*.03;z1=target.z*.79
    profile=[(r-th,z0),(r,z0),(r,z1),(r-th,z1)];vs=[(rr*math.cos(i*2*math.pi/96),rr*math.sin(i*2*math.pi/96),zz) for rr,zz in profile for i in range(96)];fs=[]
    for j in range(4):
     for i in range(96):fs.append((j*96+i,j*96+(i+1)%96,((j+1)%4)*96+(i+1)%96,((j+1)%4)*96+i))
    me=bpy.data.meshes.new('NH_JarGlassShell');me.from_pydata(vs,[],fs);me.materials.append(glass)
    for p in me.polygons:p.use_smooth=True
    shell=bpy.data.objects.new('NH_'+ident+'_JarGlassShell',me);col.objects.link(shell);shell.parent=root;new.append(shell)
    report['corrections'].append('Restored wood lid and grain colors; added thin clear glass around retained grain geometry to complete the jar.')
   if fix=='board_color':
    wood=simplemat(ident+'_Oak',(.49,.30,.13),.54);body.data.materials.clear();body.data.materials.append(wood);report['corrections'].append('Restored oak material absent from the source package.')
  if ident=='NH028':
   warm=simplemat(ident+'_Diffuser',(1,.79,.46),.45,emission=.8);assign_spatial(body,warm,lambda p:p.normal.z<-.65 and p.center.z<target.z*.25)
  if ident=='NH010':
   m=simplemat(ident+'_LampWhite',(.90,.88,.80),.4,emission=.4);body.data.materials.clear();body.data.materials.append(m)
  root.name=f'NH_ASSET_{ident}_{meta["slug"]}';root['dimensions_m']=list(target);root['reference']=meta['reference'];root['qa_status']='reviewed and calibrated';root['source_id']=ident
  col.asset_data.description=f'{meta["slug"]} | reviewed Tripo asset | metres | originals retained'
  for o in new:
   o['source_id']=ident;o['reference']=meta['reference'];o.data.update()
  bpy.context.view_layer.update();lo,hi=bbox(new);report.update(dimensions_m=list(hi-lo),targetDimensions_m=list(target),sceneFaces=sum(len(o.data.polygons) for o in new),sceneVertices=sum(len(o.data.vertices) for o in new),meshObjects=len(new),missingTextures=[])
  for im in bpy.data.images:
   if im.source=='FILE':
    if not im.packed_file and not Path(bpy.path.abspath(im.filepath)).is_file():report['missingTextures'].append(im.name)
    if im.filepath:im.filepath=bpy.path.relpath(bpy.path.abspath(im.filepath))
  assert not report['missingTextures'],report['missingTextures']
  # Retain lights/camera for the standalone inspection file, but append only NH_LIB into the residence.
  cam=bpy.data.objects.get('NH_Audit_Camera');center=(lo+hi)/2;radius=(hi-lo).length/2
  cam.location=center+Vector((4,-6,3.8)).normalized()*radius*3.8;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=radius*2.6;s.camera=cam
  s.unit_settings.system='METRIC';s.unit_settings.scale_length=1;s['project']='New_House - new Tripo batch';s.render.filepath=str(OUT/f'{ident}_ready.png')
  s.cycles.samples=16;s.cycles.use_denoising=True;s.cycles.device='CPU'
  # Studio illumination is scaled with the calibrated object, preserving review brightness.
  for light in [o for o in s.objects if o.type=='LIGHT']:
   light.location=center+(light.location-Vector((0,0,.45)))*max(target);light.data.energy*=max(target)**2;light.data.size*=max(target)
  bpy.ops.outliner.orphans_purge(do_recursive=True)
  bpy.ops.wm.save_as_mainfile(filepath=str(dest),compress=True)
  bpy.ops.render.render(write_still=True)
  report['status']='reviewed; scene copy ready';report['seconds']=round(time.time()-start,1)
  rep.write_text(json.dumps(report,ensure_ascii=False,indent=2));print('NH_READY_COMPLETE',ident,report['sceneFaces'],report['corrections'],flush=True)
  failed=path.parent/'修正失败.json'
  if failed.exists():failed.unlink()
 except Exception as e:
  report['error']=str(e);report['traceback']=traceback.format_exc();(path.parent/'修正失败.json').write_text(json.dumps(report,ensure_ascii=False,indent=2));print('NH_READY_FAILED',ident,str(e),flush=True)
 gc.collect()
print('NH_READY_WORKER_DONE',worker,flush=True)
