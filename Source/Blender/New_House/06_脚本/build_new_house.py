"""New_House: a fresh 90m2 residence built from this batch of reviewed Tripo assets.
Only reference JSON/images are shared with the previous project; no earlier scene is opened.
"""
import bpy,math,json,sys,time
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
ROOT=Path(__file__).resolve().parents[1]
LAYOUT=json.loads((ROOT/'01_参考资料/设计数据/90㎡养猫之家_建模尺寸与布局.json').read_text())
META=json.loads((ROOT/'00_管理/资产映射与修正.json').read_text())
OUT=ROOT/'04_场景工程/New_House_v001.blend';REPORT=ROOT/'00_管理/场景检查.json'
CODES={'主卧':'MBR','次卧·书房':'OFF','走廊':'HALL','玄关':'ENT','卫生间':'BTH','厨房':'KIT','客餐厅':'LIV','阳台':'BAL'}
LIB={};PLACEMENTS=[]

def coll(name,parent=None):
 c=bpy.data.collections.new('NH_'+name);(parent or bpy.context.scene.collection).children.link(c);return c

def material(name,color,rough=.6,metal=0,texture=None):
 m=bpy.data.materials.new('NH_MAT_'+name);m.use_nodes=True;n=m.node_tree.nodes;l=m.node_tree.links;p=next(n for n in n if n.type=='BSDF_PRINCIPLED');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Roughness'].default_value=rough;p.inputs['Metallic'].default_value=metal
 if texture:
  tex=n.new('ShaderNodeTexCoord');mul=n.new('ShaderNodeVectorMath');mul.operation='MULTIPLY';mul.inputs[1].default_value=(2,120,5) if texture=='wood' else (50,50,50)
  l.new(tex.outputs['Object'],mul.inputs[0]);noise=n.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=2;noise.inputs['Detail'].default_value=3;l.new(mul.outputs[0],noise.inputs['Vector'])
  ramp=n.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].color=tuple(v*.83 for v in color)+(1,);ramp.color_ramp.elements[1].color=tuple(min(1,v*1.15) for v in color)+(1,);l.new(noise.outputs['Fac'],ramp.inputs[0]);l.new(ramp.outputs[0],p.inputs['Base Color'])
  bump=n.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.12;bump.inputs['Distance'].default_value=.001;l.new(noise.outputs['Fac'],bump.inputs['Height']);l.new(bump.outputs[0],p.inputs['Normal'])
 return m

def box(name,dims,pos,mat,col,bevel=0):
 x,y,z=[a/2 for a in dims];v=[(-x,-y,-z),(-x,-y,z),(-x,y,-z),(-x,y,z),(x,-y,-z),(x,-y,z),(x,y,-z),(x,y,z)];f=[(0,4,6,2),(1,3,7,5),(0,1,5,4),(2,6,7,3),(0,2,3,1),(4,5,7,6)]
 me=bpy.data.meshes.new('NH_'+name+'_Mesh');me.from_pydata(v,[],[tuple(reversed(face)) for face in f]);me.update();o=bpy.data.objects.new('NH_'+name,me);col.objects.link(o);o.location=pos
 if mat:me.materials.append(mat)
 if bevel:
  m=o.modifiers.new('NH_SoftEdges','BEVEL');m.width=bevel;m.segments=3
 return o

def array(obj,name,count,offset):
 m=obj.modifiers.new('NH_ARRAY_'+name,'ARRAY');m.count=count;m.use_relative_offset=False;m.use_constant_offset=True;m.constant_offset_displace=offset;return m

def opening_list(wall):
 a=Vector(wall['start']);b=Vector(wall['end']);u=(b-a).normalized();length=(b-a).length;horizontal=abs(u.x)>.9;coordinate=a.y if horizontal else a.x;out=[]
 candidates=[(Vector(x['start']),Vector(x['end']),x['sill_m'],x['head_m']) for x in LAYOUT['windows']]
 for d in LAYOUT['doors']:
  axis=d['type'] in ['entry','master','kitchen_slider','balcony_slider']
  if axis==horizontal:
   p=Vector(d['start']);candidates.append((p,p+Vector((d['width_m'],0) if axis else (0,d['width_m'])),0,d['height_m']))
 candidates += [(Vector(p['start']),Vector(p['end']),0,2.8) for p in LAYOUT['openPassages']]
 for p,q,z0,z1 in candidates:
  if abs((p.y if horizontal else p.x)-coordinate)<.015 and abs((q.y if horizontal else q.x)-coordinate)<.015:
   t0,t1=sorted([(p-a).dot(u),(q-a).dot(u)]);t0=max(0,t0);t1=min(length,t1)
   if t1>t0:out.append((t0,t1,z0,z1))
 return a,u,length,out

def wall_mesh(wall,zmin,zmax,collection,mat):
 a,u,length,holes=opening_list(wall);xs=sorted(set([0,length]+[x for h in holes for x in h[:2]]));zs=sorted(set([zmin,zmax]+[z for h in holes for z in h[2:] if zmin<z<zmax]));filled={}
 for i in range(len(xs)-1):
  for j in range(len(zs)-1):
   tx=(xs[i]+xs[i+1])/2;tz=(zs[j]+zs[j+1])/2
   if not any(x0<tx<x1 and zz0<tz<zz1 for x0,x1,zz0,zz1 in holes):filled[i,j]=True
 verts=[];faces=[];lookup={};v=Vector((-u.y,u.x));th=wall['thickness_m']/2
 def point(t,k,z):
  p=a+u*t+v*k;key=tuple(round(q,6) for q in (p.x,p.y,z))
  if key not in lookup:lookup[key]=len(verts);verts.append(key)
  return lookup[key]
 def quad(points):faces.append(tuple(point(*p) for p in points))
 for i,j in filled:
  x0,x1=xs[i:i+2];z0,z1=zs[j:j+2]
  quad([(x0,-th,z0),(x1,-th,z0),(x1,-th,z1),(x0,-th,z1)])
  quad([(x1,th,z0),(x0,th,z0),(x0,th,z1),(x1,th,z1)])
  if (i-1,j) not in filled:quad([(x0,th,z0),(x0,-th,z0),(x0,-th,z1),(x0,th,z1)])
  if (i+1,j) not in filled:quad([(x1,-th,z0),(x1,th,z0),(x1,th,z1),(x1,-th,z1)])
  if (i,j-1) not in filled:quad([(x0,th,z0),(x1,th,z0),(x1,-th,z0),(x0,-th,z0)])
  if (i,j+1) not in filled:quad([(x0,-th,z1),(x1,-th,z1),(x1,th,z1),(x0,th,z1)])
 me=bpy.data.meshes.new('NH_Wall_'+wall['id']);me.from_pydata(verts,[],faces);me.update();o=bpy.data.objects.new('NH_Wall_'+wall['id']+('_Lower' if zmin==0 else '_Upper'),me);collection.objects.link(o);me.materials.append(mat);o['wall_id']=wall['id'];return o

def load_asset(ident):
 if ident in LIB:return LIB[ident]
 path=ROOT/f'03_整理资产/{ident}/{ident}_Ready.blend';assert path.exists(),path
 with bpy.data.libraries.load(str(path),link=False) as (data,loaded):loaded.collections=['NH_LIB_'+ident]
 c=loaded.collections[0];assert c,c
 # Scene copies use an appropriate texture budget; the reviewed native assets keep 2K maps.
 limit=2048 if ident in {'NH007','NH036','NH047','NH050','NH051','NH066','NH074','NH076'} else 1024
 for obj in c.objects:
  if obj.type!='MESH':continue
  if len(obj.data.vertices)==8 and len(obj.data.polygons)==6:
   obj.data.calc_loop_triangles();volume=sum(obj.data.vertices[t.vertices[0]].co.dot(obj.data.vertices[t.vertices[1]].co.cross(obj.data.vertices[t.vertices[2]].co))/6 for t in obj.data.loop_triangles)
   if volume<0:obj.data.flip_normals();obj.data.update()
  for mat in obj.data.materials:
   if not mat or not mat.use_nodes:continue
   if 'ClearGlass' in mat.name and not mat.get('NH_presentation_glass'):
    nodes=mat.node_tree.nodes;links=mat.node_tree.links;bsdf=next(n for n in nodes if n.type=='BSDF_PRINCIPLED');output=next(n for n in nodes if n.type=='OUTPUT_MATERIAL');transparent=nodes.new('ShaderNodeBsdfTransparent');mix=nodes.new('ShaderNodeMixShader');mix.inputs[0].default_value=.18;links.new(transparent.outputs[0],mix.inputs[1]);links.new(bsdf.outputs[0],mix.inputs[2]);links.new(mix.outputs[0],output.inputs['Surface']);mat['NH_presentation_glass']=True
   for node in mat.node_tree.nodes:
    if node.type=='TEX_IMAGE' and node.image and max(node.image.size)>limit:
     image=node.image;factor=limit/max(image.size);image.scale(round(image.size[0]*factor),round(image.size[1]*factor))
     textures=ROOT/'03_整理资产/Scene_Textures';textures.mkdir(exist_ok=True)
     image.filepath_raw=str(textures/(image.name.replace('/','_')+'.png'));image.file_format='PNG';image.save();image.pack()
 LIB[ident]=c;return c

def place(ident,room,loc,angle=0,dims=None,tag=None,rot=None):
 c=load_asset(ident);n=len([p for p in PLACEMENTS if p['id']==ident])+1
 o=bpy.data.objects.new(f'NH_{room}_{ident}_{tag or META[ident]["slug"]}_{n:02d}',None);ROOMS[room].objects.link(o);o.instance_type='COLLECTION';o.instance_collection=c;o.location=loc;o.rotation_euler=rot or (0,0,math.radians(angle));o.empty_display_size=.08
 if dims:o.scale=[dims[i]/META[ident]['dimensions_m'][i] for i in range(3)]
 o['source_id']=ident;o['reference']=META[ident]['reference'];o['room']=room
 PLACEMENTS.append({'name':o.name,'id':ident,'room':room,'position_m':list(loc),'rotationZ_deg':angle,'dimensions_m':dims or META[ident]['dimensions_m']})
 return o

def surface_height(ident,points=((0,0),)):
 c=load_asset(ident);heights=[]
 for o in c.objects:
  if o.type!='MESH':continue
  tree=BVHTree.FromPolygons([v.co for v in o.data.vertices],[p.vertices[:] for p in o.data.polygons])
  for x,y in points:
   hit,normal,index,distance=tree.ray_cast(Vector((x,y,10)),Vector((0,0,-1)),20)
   if hit:heights.append(hit.z)
 return max(heights)

def area(name,pos,target,power,size,color=(1,.93,.82),shape='DISK',size_y=None):
 d=bpy.data.lights.new('NH_'+name,'AREA');d.energy=power;d.shape=shape;d.size=size;d.color=color
 if size_y and shape=='RECTANGLE':d.size_y=size_y
 o=bpy.data.objects.new(d.name,d);LIGHTS.objects.link(o);o.location=pos;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
 o.visible_camera=False;o.visible_glossy=False;o.visible_transmission=False
 return o

def camera(name,pos,target,lens=28,ortho=None):
 d=bpy.data.cameras.new('NH_CAM_'+name);o=bpy.data.objects.new(d.name,d);CAMERAS.objects.link(o);o.location=pos;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler();d.lens=lens;d.clip_start=.03;d.clip_end=200
 if ortho:d.type='ORTHO';d.ortho_scale=ortho
 return o

bpy.ops.wm.read_factory_settings(use_empty=True);bpy.context.preferences.filepaths.save_version=0;s=bpy.context.scene;s.name='New_House_Tripo_Residence';s.unit_settings.system='METRIC';s.unit_settings.scale_length=1
ARCH=coll('Architecture');LOWER=coll('Architecture_Walls_Lower',ARCH);UPPER=coll('Architecture_Walls_Upper',ARCH);FLOORS=coll('Architecture_Floors',ARCH);CEILING=coll('Architecture_Ceilings',ARCH);TRIM=coll('Architecture_Trim',ARCH)
ROOMS={code:coll('ROOM_'+code) for code in CODES.values()};LIGHTS=coll('Lighting');CAMERAS=coll('Cameras');EXTERIOR=coll('Exterior_Context')
WALL=material('WarmWhite',(.81,.77,.68),.85);OAK=material('OakFloor',(.53,.37,.21),.45,texture='wood');TILE=material('GreyTile',(.42,.43,.40),.58,texture='stone');BATH=material('BathTile',(.40,.49,.50),.57,texture='stone');CREAM=material('CreamStone',(.70,.66,.55),.48);SAGE=material('SageTextile',(.25,.31,.22),.85,texture='fabric');GROUND=material('StudioGround',(.82,.80,.74),.9)
box('Foundation',(10,9,.14),(5,4.5,-.08),CREAM,FLOORS,.015)
for room in LAYOUT['rooms']:
 code=CODES[room['name']];x,y,w,h=room['rect'];mat=OAK if room['surface']=='wood' else BATH if room['surface']=='bath' else TILE
 box('Subfloor_'+code,(w,h,.025),(x+w/2,y+h/2,-.016),mat,FLOORS)
 nx=math.ceil(w/(.95 if mat==OAK else .6));ny=math.ceil(h/(.19 if mat==OAK else .6));dx=w/nx;dy=h/ny
 plank=box('FloorModule_'+code,(dx-.002,dy-.002,.009),(x+dx/2,y+dy/2,-.002),mat,FLOORS,.001)
 array(plank,'Columns_'+code,nx,(dx,0,0));array(plank,'Rows_'+code,ny,(0,dy,0))
 box('Ceiling_'+code,(w,h,.08),(x+w/2,y+h/2,2.84),WALL,CEILING)
for w in LAYOUT['wallSegments']:wall_mesh(w,0,1.15,LOWER,WALL);wall_mesh(w,1.15,2.8,UPPER,WALL)
# Skirtings follow continuous wall spans without closing any door passage.
for name,dims,pos in [('MBR_W',(.015,3.74,.07),(.207,2.07,.035)),('MBR_N',(2.7,.015,.07),(1.62,3.933,.035)),('LIV_E',(.015,4.28,.07),(9.793,3.80,.035)),('LIV_N',(1.45,.015,.07),(5.05,5.933,.035)),('OFF_W',(.015,2.88,.07),(.207,5.5,.035)),('OFF_S',(2.74,.015,.07),(1.57,4.067,.035)),('ENT_W',(.015,1.74,.07),(.207,7.93,.035))]:box('Skirting_'+name,dims,pos,OAK,TRIM,.002)
# Reviewed door/window instances occupy exact apertures from the design.
place('NH065','ENT',(3.35,8.9,0))
place('NH064','MBR',(3.60,4.0,0),dims=[.90,.095,2.20])
place('NH064','OFF',(3.0,5.575,0),90,dims=[.85,.095,2.20])
place('NH038','BTH',(4.20,7.825,0),90,dims=[.85,.085,2.20],tag='BathSlider')
place('NH038','KIT',(8.30,6.0,0),dims=[1.1,.085,2.2])
place('NH039','LIV',(7.05,1.6,0))
place('NH041','MBR',(2.05,.1,.9))
place('NH041','OFF',(.1,5.6,.9),90,dims=[1.50,.075,1.50])
place('NH026','BTH',(5.5,8.9,1.5))
place('NH040','KIT',(8.35,8.9,1.0))
place('NH049','BAL',(7.1,.1,0))
# Main bedroom and office. All furniture is from the new Tripo batch.
master_surface=surface_height('NH050');guest_surface=surface_height('NH051')
place('NH050','MBR',(1.65,2.88,0));place('NH076','MBR',(3.65,1.73,0),-90)
for x in [.49,2.81]:place('NH067','MBR',(x,3.57,0));place('NH044','MBR',(x,3.57,.48))
place('NH005','MBR',(1.65,2.22,.003),dims=[2.60,2.15,.012])
for x in [1.22,2.05]:place('NH048','MBR',(x,3.25,master_surface+.08),rot=(math.pi/2,0,0))
place('NH033','MBR',(1.85,2.02,master_surface+.01));place('NH016','MBR',(1.72,3.17,master_surface+.02),dims=[.37,.14,.37])
place('NH073','MBR',(2.0,.46,.50),180);place('NH032','MBR',(.48,.60,0),dims=[.40,.36,.57])
box('WindowPerch_Top',(.95,.46,.025),(2.0,.46,.4875),OAK,ROOMS['MBR'],.005)
for x in [1.54,2.46]:box('WindowPerch_Side',(.025,.46,.48),(x,.46,.24),OAK,ROOMS['MBR'],.005)
place('NH051','OFF',(.87,5.90,0));place('NH063','OFF',(2.23,6.53,0));place('NH003','OFF',(2.23,5.79,0),180)
place('NH056','OFF',(1.57,4.30,0));place('NH024','OFF',(2.24,6.53,.75));place('NH048','OFF',(.87,6.46,guest_surface+.075),dims=[.55,.15,.36],rot=(math.pi/2,0,0));place('NH033','OFF',(.87,5.38,guest_surface+.01));place('NH023','OFF',(1.77,5.11,0),dims=[.41,.41,.27]);place('NH031','OFF',(2.75,4.32,.58),dims=[.27,.25,.33]);place('NH008','OFF',(1.88,6.5,.75))
# Entry and circulation.
for y in [7.52,8.18]:place('NH055','ENT',(.49,y,0),90)
place('NH058','ENT',(1.77,8.59,0));place('NH053','ENT',(1.82,7.35,0),180);place('NH078','ENT',(3.35,8.13,.002));place('NH032','ENT',(.57,7.21,0),dims=[.30,.28,.43]);place('NH046','ENT',(1.52,8.59,.92));place('NH013','ENT',(2.09,8.56,.92))
place('NH080','ENT',(1.78,7.67,.003),8);place('NH080','ENT',(1.91,7.67,.003),-6)
# Living room, dining and the cat zone.
place('NH007','LIV',(5.0,3.625,0),90)
place('NH006','LIV',(6.72,3.33,.003));place('NH060','LIV',(6.7,3.325,0));place('NH071','LIV',(9.58,3.55,0),-90);place('NH027','LIV',(9.55,3.58,.45),-90)
for loc,ang in [((5.05,2.85,.46),90),((5.05,4.36,.46),90)]:place('NH016','LIV',loc,ang)
place('NH022','LIV',(5.11,3.08,.46),90);place('NH021','LIV',(5.05,4.55,.69),90)
place('NH062','LIV',(6.35,5.35,0));place('NH052','LIV',(6.35,5.80,0))
# Linked collection instances share the reviewed dining-chair mesh.
chair=place('NH059','LIV',(5.99,4.68,0),180);chair['repeat_method']='linked collection instances'
place('NH059','LIV',(6.71,4.68,0),180)
place('NH068','LIV',(4.69,5.68,0));place('NH011','LIV',(9.32,1.99,0));place('NH057','LIV',(8.55,5.30,0),0);place('NH009','LIV',(9.30,5.32,0));place('NH035','LIV',(8.98,4.90,.003));place('NH070','LIV',(8.09,5.09,0));place('NH015','LIV',(8.28,4.87,.004));place('NH079','LIV',(8.14,5.48,0))
for j,(x,y,z) in enumerate([(9.64,4.79,.35),(9.64,5.69,.75),(9.64,4.79,1.15),(9.64,5.69,1.65),(9.64,4.79,2.15)]):place('NH054','LIV',(x,y,z),-90,tag=f'CatPlatform_{j+1}')
place('NH029','LIV',(9.51,5.15,2.18),dims=[.44,.38,.60]);place('NH032','LIV',(8.76,5.68,0));place('NH031','LIV',(9.57,2.86,.45),dims=[.26,.24,.33]);place('NH023','LIV',(8.35,2.15,0))
place('NH031','LIV',(6.35,5.35,.75),dims=[.26,.24,.33]);place('NH014','LIV',(6.68,5.43,.75));place('NH029','LIV',(6.48,3.43,.38),dims=[.27,.25,.34]);place('NH012','LIV',(6.90,3.21,.38));place('NH008','LIV',(6.94,3.50,.38));place('NH072','LIV',(6.99,3.49,.414),dims=[.26,.15,.028])
# Kitchen L unit and sanitary fixtures.
place('NH066','KIT',(8.33,7.46,0));place('NH034','KIT',(7.22,6.51,0),0)
sink_rim=surface_height('NH020',((-.25,0),(.25,0)))
place('NH020','KIT',(7.79,8.33,.905-sink_rim),dims=[1.08,.62,.37]);place('NH017','KIT',(9.39,7.06,.912));place('NH019','KIT',(9.43,7.06,2.0),90)
# Close the surplus AI cooktop recess around the correctly sized two-burner appliance.
stone=material('CountertopInfill',(.80,.78,.71),.45)
for name,ds,loc in [
 ('CooktopInfill_W',(.145,1.08,.014),(9.1575,7.06,.901)),
 ('CooktopInfill_E',(.145,1.08,.014),(9.6225,7.06,.901)),
 ('CooktopInfill_N',(.32,.25,.014),(9.39,7.475,.901)),
 ('CooktopInfill_S',(.32,.25,.014),(9.39,6.645,.901))]:box(name,ds,loc,stone,ROOMS['KIT'],.001)
place('NH075','KIT',(9.64,7.23,1.83),-90)
place('NH061','KIT',(9.61,8.35,.90),-90);place('NH043','KIT',(9.46,7.96,.90));place('NH014','KIT',(9.49,8.17,.90));place('NH030','KIT',(7.38,8.52,.90),dims=[.18,.18,.27]);place('NH025','KIT',(9.43,7.23,1.81),90,dims=[1.68,.035,.025])
place('NH061','KIT',(9.58,8.54,.90),-90,dims=[.145,.022,.26],tag='SmallCuttingBoard')
place('NH004','BTH',(4.94,8.52,.22));place('NH045','BTH',(6.05,7.90,0));place('NH037','BTH',(6.10,6.68,0));place('NH042','BTH',(5.27,8.49,.94));place('NH046','BTH',(4.69,8.48,.94))
# Long south balcony: washing/storage west, window bench centre, litter east.
place('NH018','BAL',(4.67,.87,0),90)
place('NH047','BAL',(4.68,.545,0),90)
place('NH074','BAL',(7.275,.48,0),180);place('NH069','BAL',(9.40,.64,0),180);place('NH077','BAL',(5.34,.57,0));place('NH032','BAL',(5.62,.41,0),dims=[.38,.35,.54]);place('NH030','BAL',(9.40,.64,.70));place('NH036','BAL',(7.14,.48,.455),180)
place('NH016','BAL',(6.45,.53,.45),dims=[.40,.16,.33]);place('NH022','BAL',(6.84,.54,.45),dims=[.36,.15,.36]);place('NH021','BAL',(7.88,.48,.45),dims=[.46,.32,.11]);place('NH029','BAL',(8.34,.54,.45),dims=[.29,.26,.39]);place('NH042','BAL',(4.66,.36,.93),90);place('NH046','BAL',(4.68,1.14,.93),90)
place('NH046','BAL',(4.68,1.14,.995),90,tag='TowelStack_Top')
# Countertop over the washer adds the thin continuous stone surface visible in the concept.
box('LaundryStoneTop',(.72,1.30,.035),(4.67,.87,.912),CREAM,ROOMS['BAL'],.008)
# Supplemental green bed covers are simple cloth planes; supplied folded blanket is retained on top.
box('MasterBedCover',(1.72,.76,.025),(1.65,2.30,master_surface+.0125),SAGE,ROOMS['MBR'],.012);box('GuestBedCover',(1.12,.66,.023),(.87,5.41,guest_surface+.0115),SAGE,ROOMS['OFF'],.01)
# Basic ceiling and accent fixtures, instanced from this batch.
place('NH028','LIV',(6.35,5.35,1.94));place('NH002','LIV',(7.03,5.91,2.62));box('LivingBulkhead',(5.54,.26,.20),(7.03,5.80,2.70),WALL,CEILING)
for room,points in {'LIV':[(5.3,2.2),(8.6,2.2),(7.6,4.3)],'MBR':[(1.2,1.2),(2.8,2.4)],'OFF':[(2,5.6)],'KIT':[(8.6,7.5)],'ENT':[(2.3,7.9)],'BTH':[(5.4,7.4)],'BAL':[(7.0,.9)]}.items():
 for x,y in points:place('NH010',room,(x,y,2.775))
# Neutral daylight through real glazing plus restrained warm practical lighting.
s.world=bpy.data.worlds.new('NH_Daylight');s.world.use_nodes=True;nodes=s.world.node_tree.nodes;links=s.world.node_tree.links;bg=next(n for n in nodes if n.type=='BACKGROUND');bg.inputs[0].default_value=(.77,.85,1,1);bg.inputs[1].default_value=.40
sun_data=bpy.data.lights.new('NH_AfternoonSun','SUN');sun=bpy.data.objects.new(sun_data.name,sun_data);LIGHTS.objects.link(sun);sun.rotation_euler=(math.radians(32),math.radians(-26),math.radians(-125));sun_data.energy=1.55;sun_data.angle=math.radians(5);sun_data.color=(1,.87,.68)
area('South_Living',(7.0,-.4,2.7),(7.0,3.9,1),850,5,(1,.93,.81),'RECTANGLE',2)
area('South_Bedroom',(2.0,-.4,2.0),(2.0,2.8,1),420,2.5,(1,.93,.84),'RECTANGLE',1.5)
area('North_Kitchen',(8.3,9.2,2.1),(8.3,7.3,1),260,2,(.83,.91,1),'RECTANGLE',1)
for room,loc,power in [('Living',(7.0,3.8,2.65),110),('Dining',(6.35,5.35,2.0),40),('Kitchen',(8.5,7.5,2.65),75),('Bath',(5.5,7.4,2.65),55),('Master',(2.0,2.0,2.65),60),('Office',(1.9,5.6,2.65),65),('Entry',(2.0,7.9,2.65),60),('Balcony',(7.1,.9,2.65),35)]:area('CeilingFill_'+room,loc,(loc[0],loc[1],0),power,1.8,(1,.90,.76))
for x in [.49,2.81]:area('BedsideGlow_'+str(x),(x,3.57,.84),(x,3.8,1.1),3,.16,(1,.75,.44))
area('KitchenStrip',(9.38,7.23,1.79),(9.35,7.23,.9),18,1.55,(1,.87,.66),'RECTANGLE',.08)
# Distant simple city silhouettes, kept outside the residential asset collections.
far=material('DistantBuildings',(.40,.45,.48),.9)
for i,(x,w,h) in enumerate([(-8,2.5,5),(-4,2.1,3.8),(0,2.8,5.6),(4,2.1,4.5),(8,3.1,6),(13,2.4,4.2),(17,2.6,5.3)]):box('BackdropBuilding_'+str(i),(w,2,h),(x,-21,h/2-1.8),far,EXTERIOR)
box('StudioBase',(200,200,.08),(5,4.5,-.20),GROUND,EXTERIOR)
camera('Overview',(-9.0,-13.0,18.5),(5,4.5,.6),ortho=15.9)
camera('Living',(5.87,2.10,1.44),(8.07,5.45,1.15),lens=22)
camera('Balcony',(7.12,2.26,1.37),(7.12,.20,1.30),lens=17)
camera('Top',(5,4.5,21),(5,4.5,0),ortho=11.5)
s.camera=bpy.data.objects['NH_CAM_Overview'];s.render.engine='CYCLES';s.cycles.samples=64;s.cycles.use_denoising=True;s.cycles.max_bounces=8;s.cycles.transmission_bounces=6
s.render.resolution_x=1800;s.render.resolution_y=1500;s.render.resolution_percentage=100;s.view_settings.view_transform='AgX';s.view_settings.look='AgX - Medium High Contrast';s.view_settings.exposure=-.2
s.render.image_settings.file_format='PNG';s.render.image_settings.color_mode='RGB'
CEILING.hide_render=True
for o in UPPER.objects:o.hide_render=o.get('wall_id') not in ['outer_north','outer_east']
for o in s.objects:
 if o.get('source_id') in ['NH010','NH028'] and o.instance_type=='COLLECTION':o.hide_render=True
 o.hide_set(o.hide_render or o.name.startswith('NH_BackdropBuilding_') or o.name.startswith('NH_Ceiling_') or o.name=='NH_LivingBulkhead')
for screen in bpy.data.screens:
 for area_ in screen.areas:
  if area_.type=='VIEW_3D':
   area_.spaces.active.shading.type='MATERIAL';area_.spaces.active.shading.use_scene_lights=False;area_.spaces.active.shading.use_scene_world=False;area_.spaces.active.region_3d.view_perspective='CAMERA'
for im in bpy.data.images:
 if im.source=='FILE' and im.filepath:im.filepath=bpy.path.relpath(bpy.path.abspath(im.filepath),start=str(OUT.parent))
arrays=[{'object':o.name,'modifier':m.name,'count':m.count} for o in s.objects for m in o.modifiers if m.type=='ARRAY']
report={'project':'New_House','sourceBatch':'New_House_Items','footprint_m':[10,9],'units':'metres','uniqueTripoModels':sum(META[k].get('sourceType')!='supplemental' for k in LIB),'supplementalModels':sum(META[k].get('sourceType')=='supplemental' for k in LIB),'collectionInstances':len(PLACEMENTS),'arrayModifiers':arrays,'placements':PLACEMENTS,'previousSceneOpened':False,'architecture':'new meshes from reference layout JSON','supportHeights_m':{'masterMattress':master_surface,'guestMattress':guest_surface,'sinkRimLocal':sink_rim},'missingTextures':[im.name for im in bpy.data.images if im.source=='FILE' and not im.packed_file and not Path(bpy.path.abspath(im.filepath)).exists()]}
assert not report['missingTextures'],report['missingTextures']
REPORT.write_text(json.dumps(report,ensure_ascii=False,indent=2))
s['project']='New_House';s['sourceBatch']='New_House_Items';s['referenceLayout']='01_参考资料/设计数据/90㎡养猫之家_建模尺寸与布局.json';s['previousSceneOpened']=False
s['defaultView']='Overview cutaway; Alt-H restores viewport-hidden architecture'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT),compress=True)
print('NEW_HOUSE_BUILT',len(LIB),len(PLACEMENTS),len(arrays),flush=True)
