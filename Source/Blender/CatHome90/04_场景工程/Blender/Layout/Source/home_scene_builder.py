"""90 m² cat home, Blender 5.x. Metres, SW origin, X east / Y north / Z up."""
import bpy, math, json, random
from pathlib import Path
from mathutils import Vector, Matrix

ROOT=Path(__file__).resolve().parents[4]
WORK=ROOT
LAYOUT=json.loads((ROOT/'01_参考资料/设计数据/90㎡养猫之家_建模尺寸与布局.json').read_text())
SCENE_NAME='H90_Residence_Main'
FILE=ROOT/'04_场景工程/Blender/Layout/H90_Residence_v001.blend'
RENDERS=ROOT/'05_预览与验收/场景预览/H90_v001'
REPORTS=ROOT/'05_预览与验收/检查记录/H90_v001'
for p in (FILE.parent,RENDERS,REPORTS):p.mkdir(parents=True,exist_ok=True)
COL=None
PARENT=None
PART=0
ROOM_CODES={'主卧':'MBR','次卧·书房':'OFF','走廊':'HALL','玄关':'ENT','卫生间':'BTH','厨房':'KIT','客餐厅':'LIV','阳台':'BAL'}

def scene():return bpy.data.scenes[SCENE_NAME]
def coll(name,parent=None,link=True):
    c=bpy.data.collections.get(name) or bpy.data.collections.new(name)
    if link:
        dest=parent or scene().collection
        if c.name not in dest.children:dest.children.link(c)
    return c
def context_collection(c):
    global COL,PARENT,PART
    COL=c;PARENT=None;PART=0
def room(code):return coll('H90_ROOM_'+code)
def material(name):return bpy.data.materials['H90_MAT_'+name]
def linear(c):return c/12.92 if c<=.04045 else ((c+.055)/1.055)**2.4
def color(h):return tuple(linear(int(h[i:i+2],16)/255) for i in (0,2,4))+(1,)
def make_material(name,hexcolor,rough=.5,metal=0,texture=None):
    m=bpy.data.materials.get('H90_MAT_'+name)
    if m and m.get('h90_definition_ready'):return m
    m=m or bpy.data.materials.new('H90_MAT_'+name);m.use_nodes=True
    for node in m.node_tree.nodes:
        if node.type=='BSDF_PRINCIPLED':node.name='Principled BSDF'
        if node.type=='OUTPUT_MATERIAL':node.name='Material Output'
    n=m.node_tree.nodes;l=m.node_tree.links;p=n.get('Principled BSDF')
    p.inputs['Base Color'].default_value=color(hexcolor);p.inputs['Roughness'].default_value=rough;p.inputs['Metallic'].default_value=metal
    if texture:
        t=n.new('ShaderNodeTexCoord');v=n.new('ShaderNodeVectorMath');v.operation='MULTIPLY';l.new(t.outputs['Object'],v.inputs[0])
        v.inputs[1].default_value=(2.5,65,4) if texture=='wood' else (65,65,2.5) if texture=='verticalwood' else (1,1,1)
        noise=n.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=2 if 'wood' in texture else 110 if texture=='fabric' else 48
        noise.inputs['Detail'].default_value=3;l.new(v.outputs[0],noise.inputs['Vector'])
        ramp=n.new('ShaderNodeValToRGB');base=color(hexcolor)
        ramp.color_ramp.elements[0].position=.18;ramp.color_ramp.elements[0].color=tuple(x*.78 for x in base[:3])+(1,)
        ramp.color_ramp.elements[1].position=.82;ramp.color_ramp.elements[1].color=tuple(min(1,x*1.13) for x in base[:3])+(1,)
        l.new(noise.outputs['Fac'],ramp.inputs[0]);l.new(ramp.outputs['Color'],p.inputs['Base Color'])
        bump=n.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.16;bump.inputs['Distance'].default_value=.0016 if 'wood' in texture else .002
        l.new(noise.outputs['Fac'],bump.inputs['Height']);l.new(bump.outputs['Normal'],p.inputs['Normal'])
        if texture=='fabric' and 'Sheen Weight' in p.inputs:p.inputs['Sheen Weight'].default_value=.28
    m['h90_definition_ready']=True
    return m
def materials():
    entries=[('Oak','C49A64',.4,0,'wood'),('OakVertical','C49A64',.4,0,'verticalwood'),('Wall','EFE9DC',.82,0,None),
             ('GreyTile','B8B5AE',.48,0,'stone'),('BathTile','9EADB0',.5,0,'stone'),('Beige','D8D0BC',.87,0,'fabric'),
             ('Sage','929A7C',.88,0,'fabric'),('CreamTextile','E7DFCC',.9,0,'fabric'),('Jute','C5A77A',.95,0,'fabric'),
             ('DarkMetal','444B4A',.28,.78,None),('Steel','A1A6A6',.24,.86,None),('Ceramic','F3F0E7',.27,0,None),
             ('Screen','0C1113',.16,.22,None),('Leaf','547341',.58,0,None),('LeafLight','77915A',.52,0,None),
             ('Stem','6B5A37',.9,0,None),('Stone','E2D8C5',.34,0,'stone'),('Soil','342920',1,0,None),
             ('Rubber','292D2C',.85,0,None),('Paper','F7F2E7',.8,0,None),('CutEdge','4C5252',.85,0,None),('Studio','E9E8E4',.9,0,None)]
    for args in entries:make_material(*args)
    m=make_material('Glass','E6F0EE',.08)
    n=m.node_tree.nodes;l=m.node_tree.links;p=n.get('Principled BSDF');p.inputs['Transmission Weight'].default_value=1;p.inputs['IOR'].default_value=1.45
    tr=n.new('ShaderNodeBsdfTransparent');mix=n.new('ShaderNodeMixShader');mix.inputs[0].default_value=.12
    l.new(tr.outputs[0],mix.inputs[1]);l.new(p.outputs[0],mix.inputs[2]);l.new(mix.outputs[0],n.get('Material Output').inputs['Surface'])
    water=make_material('Water','B4D5D2',.08);wp=water.node_tree.nodes.get('Principled BSDF');wp.inputs['Transmission Weight'].default_value=.75;wp.inputs['IOR'].default_value=1.333
    glow=make_material('WarmDiffuser','FFF0D0',.45);g=glow.node_tree.nodes.get('Principled BSDF');g.inputs['Emission Color'].default_value=color('FFE0A8');g.inputs['Emission Strength'].default_value=2

def object_mesh(label,vertices,faces,mat=None,smooth=False):
    global PART
    PART+=1
    name=(PARENT.name.replace('_ROOT','')+'_P'+str(PART).zfill(2)+'_'+label) if PARENT else 'H90_'+label
    mesh=bpy.data.meshes.new(name+'_Mesh');mesh.from_pydata(vertices,[],faces);mesh.update()
    o=bpy.data.objects.new(name,mesh);COL.objects.link(o)
    if PARENT:o.parent=PARENT
    if mat:o.data.materials.append(material(mat))
    if smooth:
        for p in mesh.polygons:p.use_smooth=True
    o['part_role']=label
    return o
def bevel(o,w=.01,segments=3):
    if not w:return
    m=o.modifiers.new('SoftEdge_Bevel','BEVEL');m.width=w;m.segments=segments
    if hasattr(bpy.types,'WeightedNormalModifier'):
        nm=o.modifiers.new('WeightedNormals','WEIGHTED_NORMAL');nm.keep_sharp=True
def box(label,dims,pos=(0,0,0),mat='Oak',round=.01,rot=(0,0,0)):
    x,y,z=(a/2 for a in dims)
    vertices=[(-x,-y,-z),(-x,-y,z),(-x,y,-z),(-x,y,z),(x,-y,-z),(x,-y,z),(x,y,-z),(x,y,z)]
    faces=[(0,4,6,2),(1,3,7,5),(0,1,5,4),(2,6,7,3),(0,2,3,1),(4,5,7,6)]
    o=object_mesh(label,vertices,faces,mat);o.location=pos;o.rotation_euler=rot;bevel(o,round,5 if round>.04 else 3);return o
def lathe(label,profile,pos=(0,0,0),mat='Ceramic',segments=48,rot=(0,0,0),closed=False):
    vs=[(r*math.cos(2*math.pi*i/segments),r*math.sin(2*math.pi*i/segments),z) for r,z in profile for i in range(segments)]
    fs=[]
    for j in range(len(profile) if closed else len(profile)-1):
        k=(j+1)%len(profile)
        for i in range(segments):a=j*segments+i;b=j*segments+(i+1)%segments;c=k*segments+(i+1)%segments;d=k*segments+i;fs.append((a,b,c,d))
    o=object_mesh(label,vs,fs,mat,True);o.location=pos;o.rotation_euler=rot;return o
def cylinder(label,r,h,pos=(0,0,0),mat='Oak',rot=(0,0,0),radius_top=None):
    rt=r if radius_top is None else radius_top
    return lathe(label,[(0,-h/2),(r,-h/2),(r,-h/2+.005),(rt,h/2-.005),(rt,h/2),(0,h/2)],pos,mat,48,rot)
def torus(label,r,t,pos=(0,0,0),mat='Steel',rot=(0,0,0)):
    return lathe(label,[(r+t*math.cos(i*2*math.pi/16),t*math.sin(i*2*math.pi/16)) for i in range(16)],pos,mat,48,rot,True)
def sphere(label,dims,pos=(0,0,0),mat='Beige',pillow=False):
    vs=[];fs=[];nu=32;nv=16
    def power(a,e):return math.copysign(abs(a)**e,a)
    for j in range(nv+1):
        phi=-math.pi/2+j*math.pi/nv
        for i in range(nu):
            theta=i*2*math.pi/nu
            if pillow:e=.45;vx=power(math.cos(phi),e)*power(math.cos(theta),e);vy=power(math.cos(phi),e)*power(math.sin(theta),e);vz=power(math.sin(phi),e)
            else:vx=math.cos(phi)*math.cos(theta);vy=math.cos(phi)*math.sin(theta);vz=math.sin(phi)
            vs.append((vx*dims[0]/2,vy*dims[1]/2,vz*dims[2]/2))
    for j in range(nv):
        for i in range(nu):a=j*nu+i;b=j*nu+(i+1)%nu;c=(j+1)*nu+(i+1)%nu;d=(j+1)*nu+i;fs.append((a,b,c,d))
    o=object_mesh(label,vs,fs,mat,True);o.location=pos
    return o
def tube(label,points,r=.01,mat='Steel',smooth=True):
    global PART
    PART+=1;name=(PARENT.name.replace('_ROOT','')+'_P'+str(PART).zfill(2)+'_'+label) if PARENT else 'H90_'+label
    d=bpy.data.curves.new(name+'_Curve','CURVE');d.dimensions='3D';d.resolution_u=12;d.bevel_depth=r;d.bevel_resolution=3
    if smooth:
        s=d.splines.new('BEZIER');s.bezier_points.add(len(points)-1)
        for b,p in zip(s.bezier_points,points):b.co=p;b.handle_left_type='AUTO';b.handle_right_type='AUTO'
    else:
        s=d.splines.new('POLY');s.points.add(len(points)-1)
        for b,p in zip(s.points,points):b.co=(*p,1)
    o=bpy.data.objects.new(name,d);COL.objects.link(o)
    if PARENT:o.parent=PARENT
    d.materials.append(material(mat));o['part_role']=label;return o
def array(o,label,count,offset):
    m=o.modifiers.new(label,'ARRAY');m.count=count;m.use_relative_offset=False;m.use_constant_offset=True;m.constant_offset_displace=offset;return m
def asset(code,slug,roomcode,loc=(0,0,0),rz=0,collection=None,serial='01'):
    global COL,PARENT,PART
    COL=collection or room(roomcode)
    name=f'H90_{roomcode}_{code.replace("-","")}_{slug}_{serial}_ROOT'
    root=bpy.data.objects.new(name,None);COL.objects.link(root);root.empty_display_size=.12
    root.location=loc;root.rotation_euler[2]=rz
    root['asset_id']='H90-'+code;root['room']=roomcode;root['construction']='reference-based procedural model';root['units']='metres'
    root['source_reference']='R04' if roomcode=='BAL' else 'R03' if roomcode=='LIV' else 'R02'
    PARENT=root;PART=0;return root
def prototype(code,slug,builder,params=()):
    c=coll('H90_LIB_'+slug);asset(code,slug,'LIB',collection=c);builder(*params)
    scene().collection.children.unlink(c)
    c['asset_id']='H90-'+code;c['purpose']='linked collection instance prototype';return c
def instance(c,code,slug,roomcode,loc,rz=0,serial='01'):
    o=bpy.data.objects.new(f'H90_{roomcode}_{code.replace("-","")}_{slug}_{serial}_INST',None);room(roomcode).objects.link(o)
    o.instance_type='COLLECTION';o.instance_collection=c;o.location=loc;o.rotation_euler[2]=rz;o.empty_display_size=.10
    o['asset_id']='H90-'+code;o['room']=roomcode;o['construction']='collection instance';o['units']='metres';return o
def bool_cut(target,cutter):
    bpy.context.view_layer.objects.active=target;target.select_set(True)
    m=target.modifiers.new('Opening_Cut','BOOLEAN');m.operation='DIFFERENCE';m.solver='EXACT';m.object=cutter
    bpy.ops.object.modifier_apply(modifier=m.name);target.select_set(False);bpy.data.objects.remove(cutter,do_unlink=True)
def save():
    bpy.ops.wm.save_as_mainfile(filepath=str(FILE));return str(FILE)

def build_structure():
    existing=bpy.data.scenes.get(SCENE_NAME)
    if existing and len(existing.objects):raise RuntimeError('Existing H90 scene found; keep it and use a new version rather than rebuild destructively')
    archive=ROOT/'99_归档/20261014_输入空工程.blend'
    if not archive.exists():bpy.ops.wm.save_as_mainfile(filepath=str(archive),copy=True)
    s=existing or bpy.data.scenes.new(SCENE_NAME);bpy.context.window.scene=s
    s.unit_settings.system='METRIC';s.unit_settings.scale_length=1.0;s.unit_settings.length_unit='METERS'
    s['designFootprint_m']=[10,9];s['clearHeight_m']=2.8;s['layout_source']=str(ROOT/'01_参考资料/设计数据/90㎡养猫之家_建模尺寸与布局.json')
    s['status']='architecture / furniture preparation';materials()
    architecture=coll('H90_ARCHITECTURE');lower=coll('H90_ARCH_Walls_Base',architecture);upper=coll('H90_ARCH_Walls_Upper',architecture);roof=coll('H90_ARCH_Ceilings',architecture)
    floor=coll('H90_ARCH_Floors',architecture)
    context_collection(floor);box('Foundation_10x9m',(10,9,.16),(5,4.5,-.085),'Studio',.025)
    for r in LAYOUT['rooms']:
        code=ROOM_CODES[r['name']];room(code)
        x,y,w,h=r['rect'];mat='Oak' if r['surface']=='wood' else 'BathTile' if r['surface']=='bath' else 'GreyTile'
        asset('ARC-001' if mat=='Oak' else 'ARC-003' if mat=='BathTile' else 'ARC-002','Floor_'+code,code,(x,y,0),collection=floor)
        box('Subfloor',(w,h,.028),(w/2,h/2,-.019),mat,0)
        cols=math.ceil(w/(.95 if mat=='Oak' else .6));rows=math.ceil(h/(.19 if mat=='Oak' else .6));dx=w/cols;dy=h/rows
        b=box('Floor_Module',(dx-.003,dy-.003,.012),(dx/2,dy/2,-.003),mat,.002)
        array(b,'ARRAY_Planks_X' if mat=='Oak' else 'ARRAY_Tiles_X',cols,(dx,0,0));array(b,'ARRAY_Rows_Y',rows,(0,dy,0))
        context_collection(roof);o=box('Ceiling_'+code,(w,h,.06),(x+w/2,y+h/2,2.83),'Wall',.008);o['room']=code
    roof.hide_viewport=True;roof.hide_render=True
    # Openings are assembled as wall cells; their dimensions are not boolean approximations.
    for wall in LAYOUT['wallSegments']:
        a=Vector(wall['start']);b=Vector(wall['end']);d=b-a;length=d.length;u=d.normalized();openings=[]
        horizontal=abs(u.x)>.9;coordinate=a.y if horizontal else a.x
        for opening in LAYOUT['windows']:
            p=Vector(opening['start']);q=Vector(opening['end'])
            if abs((p.y if horizontal else p.x)-coordinate)<.015 and abs((q.y if horizontal else q.x)-coordinate)<.015:
                t0=(p-a).dot(u);t1=(q-a).dot(u)
                if max(t0,t1)>0 and min(t0,t1)<length:openings.append((max(0,min(t0,t1)),min(length,max(t0,t1)),opening['sill_m'],opening['head_m']))
        for door in LAYOUT['doors']:
            p=Vector(door['start'])
            axis_horizontal=door['type'] in ('entry','master','kitchen_slider','balcony_slider')
            if horizontal==axis_horizontal and abs((p.y if horizontal else p.x)-coordinate)<.015:
                q=p+Vector((door['width_m'],0) if horizontal else (0,door['width_m']))
                t0=(p-a).dot(u);t1=(q-a).dot(u)
                if max(t0,t1)>0 and min(t0,t1)<length:openings.append((max(0,min(t0,t1)),min(length,max(t0,t1)),0,door['height_m']))
        for passage in LAYOUT['openPassages']:
            p=Vector(passage['start']);q=Vector(passage['end'])
            if abs((p.y if horizontal else p.x)-coordinate)<.015 and abs((q.y if horizontal else q.x)-coordinate)<.015:
                t0=(p-a).dot(u);t1=(q-a).dot(u)
                if max(t0,t1)>0 and min(t0,t1)<length:openings.append((max(0,min(t0,t1)),min(length,max(t0,t1)),0,2.8))
        xs=sorted(set([0,length]+[x for o in openings for x in o[:2]]));zs=sorted(set([0,1.15,2.8]+[z for o in openings for z in o[2:]]))
        for i in range(len(xs)-1):
            for j in range(len(zs)-1):
                lo,hi=xs[i:i+2];z0,z1=zs[j:j+2];mid=(lo+hi)/2;zm=(z0+z1)/2
                if hi-lo<.0001 or z1-z0<.0001 or any(t0<mid<t1 and h0<zm<h1 for t0,t1,h0,h1 in openings):continue
                context_collection(lower if zm<=1.15 else upper);xy=a+u*mid
                dims=(hi-lo,wall['thickness_m'],z1-z0) if horizontal else (wall['thickness_m'],hi-lo,z1-z0)
                o=box(wall['id']+f'_Cell_{i:02d}_{j:02d}',dims,(*xy,zm),'Wall',.004)
                o['wall_id']=wall['id'];o['height_interval_m']=[z0,z1]
    # Repeatable skirting strips are kept separate for replacing materials.
    context_collection(coll('H90_ARCH_Skirting',architecture))
    for r in LAYOUT['rooms']:
        if r['surface']!='wood':continue
        x,y,w,h=r['rect']
        for suffix,dims,pos in [('S',(w,.012,.07),(x+w/2,y+.006,.035)),('N',(w,.012,.07),(x+w/2,y+h-.006,.035)),('W',(.012,h,.07),(x+.006,y+h/2,.035)),('E',(.012,h,.07),(x+w-.006,y+h/2,.035))]:
            # Crossing door sills is avoided; only strips along continuous outer/back walls.
            if suffix=='N' and r['name'] in ('主卧','玄关'):continue
            if suffix=='E' and r['name'] in ('次卧·书房','玄关'):continue
            if suffix in ('W','N') and r['name']=='客餐厅':continue
            box('Skirting_'+ROOM_CODES[r['name']]+'_'+suffix,dims,pos,'Oak',.003)
    build_doors_windows();s['status']='architecture complete; furnishings pending';save()
    print(json.dumps({'phase':'structure','objects':len(s.objects),'file':str(FILE)},ensure_ascii=False))

def glazed_frame(w,h,panels=2,window=False,mesh_ends=False):
    t=.045;d=.07
    for label,dims,pos in [('Header',(w,d,t),(0,0,h-t/2)),('Sill',(w,d,t),(0,0,t/2)),('LeftJamb',(t,d,h),(-w/2+t/2,0,h/2)),('RightJamb',(t,d,h),(w/2-t/2,0,h/2))]:box(label,dims,pos,'DarkMetal' if not window else 'Steel',.004)
    span=(w-2*t)/panels
    if panels>1:
        mullion=box('Mullion',(t,.05,h-2*t),(-w/2+t+span,0,h/2),'DarkMetal' if not window else 'Steel',.003)
        if panels>2:array(mullion,'ARRAY_Frame_Mullions',panels-1,(span,0,0))
    for i in range(panels):
        px=-w/2+t+(i+.5)*span
        box('Glass_'+str(i+1),(span-t,.008,h-2*t),(px,.005,h/2),'Glass',0)
    for hx in (-w/2+.12,w/2-.12):box('Pull_Handle',(.012,.025,.13),(hx,-.055,min(h*.55,1.0)),'DarkMetal',.004)
def build_doors_windows():
    for door in LAYOUT['doors']:
        typ=door['type'];w=door['width_m'];h=door['height_m'];x,y=door['start'];horizontal=typ in ('entry','master','kitchen_slider','balcony_slider')
        midpoint=(x+w/2,y,0) if horizontal else (x,y+w/2,0);rz=0 if horizontal else math.pi/2
        code='ENT' if typ=='entry' else 'MBR' if typ=='master' else 'OFF' if typ=='guest' else 'BTH' if typ=='bath_slider' else 'KIT' if typ=='kitchen_slider' else 'BAL'
        aid='DOR-001' if typ=='entry' else 'DOR-002' if typ in ('master','guest') else 'DOR-003' if typ=='bath_slider' else 'DOR-004' if typ=='kitchen_slider' else 'DOR-005'
        asset(aid,typ,code,midpoint,rz)
        if typ.endswith('slider') and typ!='bath_slider':glazed_frame(w,h,2 if typ=='kitchen_slider' else 4)
        else:
            box('Door_Slab',(w-.06,.04,h-.035),(0,0,(h-.035)/2),'OakVertical',.01)
            for sx in (-1,1):box('Door_Frame',(.05,.12,h),(sx*(w/2-.025),0,h/2),'OakVertical',.006)
            box('Header',(w,.12,.05),(0,0,h-.025),'Oak',.006)
            cylinder('Handle_Rose',.025,.014,(w/2-.14,-.03,1.0),'DarkMetal',(math.pi/2,0,0))
            tube('Lever',[(w/2-.14,-.06,1.0),(w/2-.25,-.06,1.0)],.01,'DarkMetal',False)
        PARENT['opening_width_m']=w;PARENT['opening_height_m']=h
    for win in LAYOUT['windows']:
        a=Vector(win['start']);b=Vector(win['end']);w=(b-a).length;h=win['head_m']-win['sill_m'];mid=(a+b)/2
        horizontal=abs(b.x-a.x)>abs(b.y-a.y);rz=0 if horizontal else math.pi/2
        code='MBR' if win['id'].startswith('master') else 'OFF' if win['id'].startswith('guest') else 'BTH' if win['id'].startswith('bath') else 'KIT' if win['id'].startswith('kitchen') else 'BAL'
        aid='DOR-006' if code in ('MBR','OFF') else 'DOR-007' if code=='KIT' else 'DOR-008' if code=='BTH' else 'DOR-009'
        asset(aid,win['id'],code,(*mid,win['sill_m']),rz)
        glazed_frame(w,h,5 if code=='BAL' else 1 if code=='BTH' else 2,True)
        if code=='BAL':box('Transom',(w,.06,.04),(0,0,.62),'Steel',.003)
    save()

def legs(w,d,h,top=.06,bottom=.04):
    for sx in (-1,1):
        for sy in (-1,1):
            lo=Vector((sx*(w/2-.08),sy*(d/2-.08),.015));hi=Vector((sx*(w/2-.12),sy*(d/2-.12),h))
            vec=hi-lo;o=cylinder('TaperedLeg',bottom,vec.length,(lo+hi)/2,'Oak',radius_top=top)
            o.rotation_mode='QUATERNION';o.rotation_quaternion=Vector((0,0,1)).rotation_difference(vec)
def table(w,d,h):
    box('Top',(w,d,.055),(0,0,h-.0275),'Oak',min(.035,d*.1))
    legs(w,d,h-.045,.034,.026)
    box('Apron_Front',(w-.16,.03,.085),(0,-d/2+.10,h-.1),'Oak',.008)
    box('Apron_Back',(w-.16,.03,.085),(0,d/2-.10,h-.1),'Oak',.008)
def case(w,d,h,doors=3,legheight=.08,drawers=False):
    th=.025
    box('Carcass_Top',(w,d,th),(0,0,h-th/2),'Oak',.008)
    box('Carcass_Bottom',(w,d,th),(0,0,legheight+th/2),'Oak',.006)
    box('Carcass_Back',(w,.018,h-legheight),(0,d/2-.009,(h+legheight)/2),'OakVertical',.003)
    for sx in (-1,1):box('Side',(th,d,h-legheight),(sx*(w/2-th/2),0,(h+legheight)/2),'OakVertical',.005)
    if legheight:
        for sx in (-1,1):
            for sy in (-1,1):cylinder('Foot',.023,legheight,(sx*(w/2-.055),sy*(d/2-.055),legheight/2),'Oak')
    span=(w-.06)/doors
    for i in range(doors):
        px=-w/2+.03+span*(i+.5);lower=h-legheight-.065
        if drawers:
            box(f'Door_{i+1}',(span-.009,.023,lower*.7),(px,-d/2-.003,legheight+.032+lower*.35),'OakVertical',.004)
            box(f'Drawer_{i+1}',(span-.009,.026,lower*.3-.008),(px,-d/2-.006,legheight+.032+lower*.85),'Oak',.004)
        else:box(f'Door_{i+1}',(span-.009,.023,lower),(px,-d/2-.003,legheight+.032+lower/2),'OakVertical',.004)
        if i:
            box('Divider',(.018,d-.045,h-legheight-.05),(-w/2+.03+span*i,0,(h+legheight)/2),'Oak',.003)
def sofa():
    w=2.45;d=1
    box('OakBase',(w,.93,.14),(0,0,.17),'Oak',.025)
    for sx in (-1,1):
        for sy in (-1,1):cylinder('Foot',.034,.14,(sx*1.09,sy*.35,.07),'Oak')
    box('BackFrame',(2.14,.17,.60),(0,.365,.54),'Beige',.075)
    for sx in (-1,1):box('Arm',(.205,1,.67),(sx*1.12,0,.46),'Beige',.085)
    for i,x in enumerate((-.72,0,.72)):
        box('SeatCushion_'+str(i+1),(.71,.74,.205),(x,-.08,.36),'Beige',.065)
        box('BackCushion_'+str(i+1),(.71,.18,.47),(x,.245,.615),'Beige',.065,(-.12,0,0))
def chair(office=False):
    w=.55 if office else .48;d=.55 if office else .5
    if office:
        for sx in (-1,1):
            for sy in (-1,1):cylinder('DarkLeg',.017,.43,(sx*.19,sy*.19,.215),'DarkMetal')
        box('Seat',(.49,.47,.095),(0,-.015,.46),'Sage',.043)
        box('Back',(.50,.10,.36),(0,.22,.65),'Sage',.05,(-.12,0,0))
        for sx in (-1,1):box('Arm',(.06,.40,.20),(sx*.245,.02,.555),'Sage',.028)
    else:
        legs(w,d,.45,.022,.018)
        box('SeatFrame',(.45,.44,.045),(0,0,.43),'Oak',.015)
        box('SeatCushion',(.45,.44,.075),(0,-.005,.48),'Sage',.032)
        for sx in (-1,1):box('BackSupport',(.028,.03,.32),(sx*.19,.17,.60),'Oak',.009,(-.08,0,0))
        box('PaddedBack',(.44,.075,.26),(0,.19,.69),'Sage',.032,(-.10,0,0))
def bench():
    table(1.2,.35,.45)
    for sx in (-1,1):box('BackSupport',(.033,.03,.37),(sx*.45,.115,.585),'Oak',.008)
    box('Backrest',(1.16,.045,.17),(0,.14,.695),'Oak',.035)
def nightstand():
    for sx in (-1,1):
        for sy in (-1,1):cylinder('Leg',.022,.20,(sx*.12,sy*.12,.1),'Oak')
    cylinder('RoundBody',.19,.235,(0,0,.32),'Oak')
    cylinder('RoundTop',.205,.033,(0,0,.457),'Oak')
    tube('DrawerSeam',[(-.14,-.131,.24),(0,-.19,.24),(.14,-.131,.24)],.0015,'DarkMetal')
    cylinder('DrawerPull',.012,.018,(0,-.198,.33),'DarkMetal',(math.pi/2,0,0))
def bed(w,h):
    d=2
    box('OakFrame',(w+.1,d+.08,.18),(0,0,.20),'Oak',.032)
    for sx in (-1,1):
        for sy in (-1,1):box('Foot',(.08,.08,.17),(sx*(w/2-.10),sy*.88,.085),'Oak',.01)
    box('Headboard',(w+.1,.055,1.00),(0,1.005,.50),'OakVertical',.027)
    box('Mattress',(w,1.97,h-.25),(0,-.018,.25+(h-.25)/2),'CreamTextile',.07)
def rug(w,d):box('WovenRug',(w,d,.018),(0,0,.009),'CreamTextile',.013)
def bedding(w,h):
    box('SageQuilt',(w+.035,1.18,.035),(0,-.34,h+.018),'Sage',.015)
    box('FoldedQuiltEdge',(w+.035,.12,.055),(0,.19,h+.031),'Sage',.022)
    # The cream coverlet remains independent of the bed frame and mattress.
    box('CreamSheet',(w+.015,1.93,.012),(0,-.02,h+.006),'CreamTextile',.004)
def pillow(w=.6,d=.38):sphere('CottonPillow',(w,d,.14),(0,0,.07),'CreamTextile',True)
def decorpillow(sage=True):
    box('DecorCushion',(.4,.11,.40),(0,0,.20),'Sage' if sage else 'CreamTextile',.055,(-.14,0,0))
def cushion_ottoman():
    lathe('KnittedPouf',[(0,0),(.17,0),(.215,.035),(.225,.15),(.215,.29),(.18,.35),(0,.35)],mat='Sage')
def hideout(w=.55,d=.55,h=.55,litter=False):
    th=.026
    for label,dims,pos in [('Top',(w,d,th),(0,0,h-th/2)),('Bottom',(w,d,th),(0,0,.028)),('Back',(w,.025,h),(0,d/2-.0125,h/2)),('SideL',(th,d,h),(-w/2+th/2,0,h/2)),('SideR',(th,d,h),(w/2-th/2,0,h/2))]:box(label,dims,pos,'Stone' if litter and label=='Top' else 'OakVertical',.006)
    front=box('EntrancePanel',(w-.025,.025,h-.04),(0,-d/2+.01,h/2),'OakVertical',.004)
    hole=cylinder('TemporaryOpening',.17 if litter else .185,.18,(-.11 if litter else 0,-d/2,.29 if litter else .285),'Oak',(math.pi/2,0,0));bool_cut(front,hole)
    if litter:
        for x in (.215,.27):
            cut=box('TemporaryVent',(.021,.10,.205),(x,-d/2,.45),'Oak',.009);bool_cut(front,cut)
    for sx in (-1,1):box('SquareStile',(.024,d,.052),(sx*(w/2-.012),0,.026),'Oak',.004)
def scratch_column():
    cylinder('Base',.275,.055,(0,0,.0275),'Oak')
    cylinder('SisalColumn',.115,2.055,(0,0,1.08),'Jute')
    pts=[];turns=132
    for i in range(turns*7+1):
        t=i/(turns*7);ang=t*turns*2*math.pi;pts.append((.116*math.cos(ang),.116*math.sin(ang),.06+t*2.035))
    tube('WoundSisalRope',pts,.005,'Jute',False)
    lathe('RoundSleepingTray',[(0,0),(.275,0),(.285,.025),(.285,.145),(.264,.145),(.26,.04),(0,.04)],(0,0,2.10),'Oak')
    sphere('CreamBedPad',(.49,.49,.065),(0,0,2.16),'CreamTextile',True)
def wallstep():box('FloatingPlatform',(.48,.30,.055),(0,0,-.0275),'Oak',.012)
def feeder():
    top=box('FeederTop',(.58,.30,.04),(0,0,.325),'Oak',.018)
    for sx in (-1,1):
        cut=cylinder('BowlRecess',.093,.15,(sx*.135,0,.325),'Oak');bool_cut(top,cut)
        lathe('CeramicBowl',[(0,0),(.067,0),(.088,.018),(.099,.066),(.091,.070),(.078,.024),(0,.024)],(sx*.135,0,.30),'Ceramic')
    for sx in (-1,1):
        for sy in (-1,1):box('Leg',(.03,.03,.30),(sx*.245,sy*.105,.15),'Oak',.008)
    box('LowerShelf',(.5,.25,.025),(0,0,.115),'Oak',.008)
def fountain():
    lathe('CeramicReservoir',[(0,0),(.17,0),(.20,.025),(.20,.12),(.185,.145),(.165,.145),(.16,.05),(0,.04)],mat='Ceramic')
    cylinder('WaterSurface',.159,.003,(0,0,.12),'Water')
    cylinder('CentralSpout',.013,.065,(0,0,.1475),'Ceramic')
    tube('WaterStream',[(0,0,.18),(.019,0,.185),(.03,0,.16),(.036,0,.124)],.003,'Water')
def balcony_bench():
    case(2.55,.40,.39,3,0)
    for x in (-.85,0,.85):box('SeatPad',(.84,.38,.06),(x,0,.42),'Sage',.018)
def window_perch():
    case(1.60,.35,.26,3,0)
    box('PerchMat',(1.54,.32,.018),(0,0,.27),'Jute',.008)
    lathe('OvalBedRim',[(0,0),(.24,0),(.26,.025),(.26,.10),(.225,.10),(.22,.03),(0,.03)],(0,0,.28),'CreamTextile')
    sphere('BedPad',(.45,.30,.05),(0,0,.31),'CreamTextile',True)
def laundry_storage():
    w=.7;d=.76
    for sx in (-1,1):box('SideSupport',(.035,d,.94),(sx*(w/2-.0175),0,.47),'OakVertical',.004)
    box('Counter',(w+.015,d,.035),(0,0,.945),'Stone',.007)
    box('UpperBack',(w,.02,.68),(0,d/2-.01,2.06),'OakVertical',.004)
    for sx in (-1,1):box('UpperSide',(.025,.33,.68),(sx*(w/2-.0125),d/2-.165,2.06),'OakVertical',.004)
    box('UpperTop',(w,.33,.025),(0,d/2-.165,2.3875),'Oak',.005);box('UpperBottom',(w,.33,.025),(0,d/2-.165,1.73),'Oak',.005)
    for x in (-w/4,w/4):box('UpperDoor',(w/2-.007,.023,.64),(x,d/2-.337,2.06),'OakVertical',.004)

def build_furnishings():
    bpy.context.window.scene=scene()
    if scene().get('furnishings_complete'):raise RuntimeError('Furnishings already built')
    asset('FUR-001','Sofa','LIV',(5,3.625,0),math.pi/2);sofa()
    asset('FUR-002','CoffeeTable','LIV',(6.7,3.325,0));table(1,.65,.38)
    asset('FUR-003','TVConsole','LIV',(9.6,3.55,0),-math.pi/2);case(1.9,.40,.45,3,.075,True)
    asset('FUR-004','DiningTable','LIV',(6.35,5.35,0));table(1.2,.7,.75)
    c=prototype('FUR-005','DiningChair',chair)
    for i,x in enumerate((6.00,6.68)):instance(c,'FUR-005','DiningChair','LIV',(x,4.80,0),math.pi,str(i+1).zfill(2))
    asset('FUR-006','DiningBench','LIV',(6.35,5.73,0));bench()
    asset('FUR-007','MasterBed','MBR',(1.65,2.9,0));bed(1.8,.55)
    asset('FUR-008','GuestBed','OFF',(.85,5.9,0));bed(1.2,.45)
    c=prototype('FUR-009','BedsideTable',nightstand)
    for i,x in enumerate((.44,2.88)):instance(c,'FUR-009','BedsideTable','MBR',(x,3.56,0),0,str(i+1).zfill(2))
    asset('FUR-010','MasterWardrobe','MBR',(3.65,1.725,0),-math.pi/2);case(2.15,.60,2.60,6,0)
    asset('FUR-011','GuestStorage','OFF',(1.02,4.38,0));case(1.55,.45,.66,3,0)
    asset('FUR-012','Desk','OFF',(2.25,6.55,0));table(1.2,.6,.75)
    asset('FUR-013','OfficeChair','OFF',(2.25,5.82,0),math.pi);chair(True)
    asset('FUR-014','EntryStorage','ENT',(.51,7.93,0),math.pi/2);case(1.55,.60,2.4,3,0)
    asset('FUR-015','EntryConsole','ENT',(2.15,7.36,0),math.pi);case(1.25,.5,.85,2,.12,True)
    asset('FUR-016','EntryBench','ENT',(1.80,8.5,0))
    box('Seat',(1.15,.42,.05),(0,0,.425),'Oak',.018)
    box('Shelf',(1.15,.39,.028),(0,0,.11),'Oak',.006)
    for x in (-.553,-.18,.18,.553):box('CubbyDivider',(.024,.39,.38),(x,0,.235),'Oak',.005)
    asset('FUR-020','BalconyBench','BAL',(7.275,.48,0),math.pi);balcony_bench()
    asset('FUR-019','LaundryStorage','BAL',(4.67,.86,0),math.pi);laundry_storage()
    asset('FUR-021','Ottoman','OFF',(2.02,4.97,0));cushion_ottoman()
    asset('CAT-007','WindowPerch','MBR',(1.7,.47,0),math.pi);window_perch()
    asset('CAT-001','Hideout','LIV',(8.65,5.23,0));hideout()
    asset('CAT-006','LitterCabinet','BAL',(9.40,.625,0),math.pi);hideout(.76,.65,.70,True)
    asset('CAT-002','ScratchColumn','LIV',(9.30,5.15,0));scratch_column()
    c=prototype('CAT-003','WallPlatform',wallstep)
    for i,h in enumerate(LAYOUT['catVerticalPlatforms_m']):instance(c,'CAT-003','WallPlatform','LIV',(9.65,4.75 if i%2==0 else 5.70,h),-math.pi/2,str(i+1).zfill(2))
    asset('CAT-004','FeedingStation','LIV',(4.67,5.70,0));feeder()
    asset('CAT-005','WaterFountain','LIV',(9.32,2.0,0));fountain()
    asset('PRP-005','LivingRug','LIV',(6.66,3.32,0));rug(2.30,1.75)
    asset('PRP-005','MasterRug','MBR',(1.65,2.61,0));rug(2.65,2.60)
    asset('PRP-006','EntryMat','ENT',(3.30,8.35,0));box('JuteMat',(.80,.60,.012),(0,0,.006),'Jute',.006)
    for code,loc,w,h in [('MBR',(1.65,2.9,0),1.8,.55),('OFF',(.85,5.9,0),1.2,.45)]:
        asset('PRP-003','Bedding',code,loc);bedding(w,h)
    p=prototype('PRP-002','SleepingPillow',pillow)
    for i,(x,y,z,code) in enumerate([(1.16,3.51,.567,'MBR'),(2.12,3.51,.567,'MBR'),(.85,6.48,.467,'OFF')]):instance(p,'PRP-002','SleepingPillow',code,(x,y,z),0,str(i+1).zfill(2))
    c=prototype('PRP-001','DecorCushionSage',decorpillow,(True,));cream=prototype('PRP-001','DecorCushionCream',decorpillow,(False,))
    for i,(pr,code,loc,rz) in enumerate([(c,'LIV',(4.80,2.95,.455),math.pi/2),(cream,'LIV',(4.80,3.68,.455),math.pi/2),(c,'LIV',(4.80,4.36,.455),math.pi/2),(c,'BAL',(6.45,.46,.45),math.pi),(cream,'BAL',(6.93,.46,.45),math.pi),(c,'MBR',(1.65,3.07,.60),0)]):instance(pr,'PRP-001','DecorCushion','%s'%code,loc,rz,str(i+1).zfill(2))
    scene()['furnishings_complete']=True;scene()['status']='furniture and cat fixtures complete';save()
    print(json.dumps({'phase':'furnishings','objects':len(scene().objects),'collectionPrototypes':len([c for c in bpy.data.collections if c.name.startswith('H90_LIB_')])},ensure_ascii=False))

def washer():
    box('ApplianceBody',(.60,.60,.85),(0,0,.425),'Ceramic',.024)
    box('TopPanel',(.595,.595,.02),(0,0,.85),'Ceramic',.008)
    box('ControlPanel',(.575,.018,.115),(0,-.31,.773),'Ceramic',.008)
    box('DetergentDrawer',(.17,.026,.055),(-.185,-.325,.778),'Ceramic',.007)
    box('Display',(.19,.004,.065),(.17,-.328,.773),'Screen',.004)
    cylinder('Dial',.031,.026,(-.002,-.34,.775),'Steel',(math.pi/2,0,0))
    cylinder('DoorDarkInterior',.206,.025,(0,-.322,.417),'Screen',(math.pi/2,0,0))
    torus('DoorOuterTrim',.229,.025,(0,-.327,.417),'Steel',(math.pi/2,0,0))
    torus('DoorRubberSeal',.208,.009,(0,-.345,.417),'Rubber',(math.pi/2,0,0))
    box('AccessCover',(.105,.014,.10),(.21,-.308,.10),'Ceramic',.015)
def refrigerator():
    box('RefrigeratorBody',(.68,.70,1.80),(0,0,.9),'DarkMetal',.018)
    box('UpperDoor',(.674,.046,1.19),(0,-.366,1.197),'Steel',.014)
    box('FreezerDrawer',(.674,.047,.577),(0,-.367,.291),'Steel',.013)
    for z,length in [(1.12,.34),(.31,.14)]:tube('VerticalHandle',[(-.24,-.397,z-length/2),(-.24,-.405,z+length/2)],.009,'DarkMetal',False)
def television():
    box('TVBack', (1.2,.048,.72),(0,0,.45),'DarkMetal',.012)
    box('DisplayPanel',(1.166,.003,.680),(0,-.026,.45),'Screen',.008)
    box('StandStem',(.055,.045,.11),(0,.007,.10),'DarkMetal',.008)
    box('StandBase',(.40,.19,.018),(0,-.02,.011),'DarkMetal',.008)
def cooktop():
    box('GlassCooktop',(.60,.45,.035),(0,0,.0175),'Screen',.007)
    for x in (-.145,.145):
        torus('BurnerOuterRing',.112,.0018,(x,.017,.037),'Steel')
        torus('BurnerInnerRing',.090,.001,(x,.017,.037),'Steel')
        cylinder('ControlKnob',.018,.016,(x,-.17,.044),'DarkMetal')
def sink():
    # Open rectangular basin and integral left drain board, .70 × .38 m.
    box('DrainBoard',(.265,.38,.014),(-.205,0,.004),'Steel',.009)
    for dims,pos in [((.40,.018,.014),(.145,-.181,.004)),((.40,.018,.014),(.145,.181,.004)),((.018,.345,.014),(.335,0,.004))]:box('Flange',dims,pos,'Steel',.004)
    box('BasinBottom',(.365,.318,.014),(.145,0,-.137),'Steel',.024)
    for dims,pos in [((.365,.015,.143),(.145,-.167,-.07)),((.365,.015,.143),(.145,.167,-.07)),((.015,.33,.143),(-.04,0,-.07)),((.015,.33,.143),(.329,0,-.07))]:box('BasinWall',dims,pos,'Steel',.01)
    cylinder('Drain',.025,.006,(.145,0,-.126),'DarkMetal');torus('DrainRim',.023,.002,(.145,0,-.12),'Steel')
    o=box('DrainBoardGroove',(.19,.003,.002),(-.21,-.115,.013),'DarkMetal',0);array(o,'ARRAY_DrainBoard_Grooves',6,(0,.046,0))
    tube('Faucet',[(.20,.16,.006),(.20,.16,.18),(.20,.15,.30),(.20,.03,.32),(.20,-.04,.24)],.012,'Steel')
    tube('FaucetLever',[(.24,.16,.015),(.24,.16,.08)],.004,'Steel',False)
def hood():
    box('ExtractorBody',(.66,.43,.12),(0,0,.06),'Steel',.014)
    box('FilterMesh',(.55,.31,.005),(0,0,-.002),'DarkMetal',.004)
    for x in (-.22,.22):box('UnderLight',(.055,.035,.004),(x,-.14,-.007),'WarmDiffuser',.004)
def vanity():
    box('FloatingCabinet',(.99,.48,.53),(0,0,.545),'GreyTile',.012)
    for z in (.415,.675):box('DrawerFace',(.97,.018,.247),(0,-.252,z),'GreyTile',.006)
    top=box('StoneCounter',(1,.50,.035),(0,0,.833),'Stone',.008)
    cut=box('TemporaryBasin',(.55,.34,.16),(0,0,.825),'Stone',.02);bool_cut(top,cut)
    for dims,pos in [((.57,.035,.025),(0,-.172,.852)),((.57,.035,.025),(0,.172,.852)),((.035,.31,.025),(-.28,0,.852)),((.035,.31,.025),(.28,0,.852))]:box('BasinRim',dims,pos,'Ceramic',.013)
    box('BasinBottom',(.515,.27,.018),(0,0,.755),'Ceramic',.025)
    for dims,pos in [((.515,.018,.094),(0,-.15,.808)),((.515,.018,.094),(0,.15,.808)),((.018,.28,.094),(-.26,0,.808)),((.018,.28,.094),(.26,0,.808))]:box('BasinWall',dims,pos,'Ceramic',.013)
    tube('MixerTap',[(0,.20,.85),(0,.20,1.04),(0,.11,1.04)],.014,'Steel')
    cylinder('Drain',.015,.005,(0,0,.768),'Steel')
def toilet():
    sphere('Pedestal',(.30,.40,.40),(0,-.03,.205),'Ceramic')
    sphere('BowlShell',(.5,.64,.29),(0,-.07,.365),'Ceramic')
    sphere('SeatLid',(.48,.60,.037),(0,-.09,.515),'Ceramic')
    box('Tank',(.44,.18,.40),(0,.238,.574),'Ceramic',.067)
    box('TankLid',(.445,.185,.027),(0,.238,.783),'Ceramic',.017)
    cylinder('FlushButton',.018,.008,(0,.238,.800),'Steel')
def shower():
    w=1.2;d=1.15;h=2.1
    box('NorthGlass',(w,.008,h),(0,d/2,h/2),'Glass',0)
    box('WestReturn',(.008,d,h),(-w/2,0,h/2),'Glass',0)
    for dims,pos in [((w,.018,.022),(0,d/2,.011)),((.018,d,.022),(-w/2,0,.011)),((.018,d,.022),(-w/2,0,h-.011)),((.018,.018,h),(-w/2,d/2,h/2)),((.018,.018,h),(-w/2,-d/2,h/2))]:box('MetalEdge',dims,pos,'Steel',.003)
    tube('Riser',[(.53,.05,.82),(.53,.05,1.98),(.35,.05,2.03),(.23,.05,2.0)],.012,'Steel')
    cylinder('RainHead',.095,.014,(.23,.05,1.985),'Steel')
    tube('Mixer',[(.53,-.10,.90),(.53,.15,.90)],.022,'Steel',False)
    tube('FlexibleHose',[(.53,.15,.90),(.50,.18,.53),(.45,.10,.47),(.43,.02,1.28)],.005,'Steel')
    cylinder('HandShower',.033,.013,(.43,.02,1.29),'Steel',(0,math.pi/2,0))
    box('DrainGrate',(.12,.12,.006),(.30,-.25,.01),'Steel',.003)
def laptop():
    box('KeyboardBase',(.35,.25,.011),(0,0,.007),'Steel',.006)
    box('KeyboardInset',(.285,.11,.002),(0,.008,.014),'DarkMetal',.003)
    k=box('Key',(.017,.013,.0015),(-.125,-.029,.016),'Rubber',.001)
    array(k,'ARRAY_Keys_Columns',12,(.023,0,0));array(k,'ARRAY_Keys_Rows',4,(0,.023,0))
    box('Touchpad',(.095,.053,.001),(0,-.073,.014),'Steel',.004)
    box('Lid',(.347,.008,.238),(0,.124,.132),'DarkMetal',.004,(-.18,0,0))
    box('Screen',(.327,.002,.215),(0,.119,.130),'Screen',.002,(-.18,0,0))

def build_appliances():
    bpy.context.window.scene=scene()
    if scene().get('appliances_complete'):raise RuntimeError('Appliances already built')
    north=asset('FUR-017','KitchenBase_North','KIT',(8.33,8.50,0));case(2.94,.6,.90,5,0)
    north_top=next(o for o in north.children if o.get('part_role')=='Carcass_Top');north_top.data.materials.clear();north_top.data.materials.append(material('Stone'))
    cut=box('TemporarySinkRecess',(.70,.38,.15),(-.03,0,.88),'Stone',.006);bool_cut(north_top,cut)
    east=asset('FUR-017','KitchenBase_East','KIT',(9.50,7.13,0),-math.pi/2);case(2.14,.60,.90,4,0)
    next(o for o in east.children if o.get('part_role')=='Carcass_Top').data.materials[0]=material('Stone')
    asset('FUR-018','KitchenUpper','KIT',(9.64,7.13,1.83),-math.pi/2);case(2.14,.32,.70,4,0)
    asset('APP-006','KitchenSink','KIT',(8.30,8.50,.901));sink()
    asset('APP-004','Cooktop','KIT',(9.50,7.17,.905),-math.pi/2);cooktop()
    asset('APP-005','ExtractorHood','KIT',(9.56,7.17,1.69),-math.pi/2);hood()
    asset('APP-002','Refrigerator','KIT',(7.25,6.63,0));refrigerator()
    asset('APP-003','WashingMachine','BAL',(4.67,.82,0),math.pi);washer()
    asset('APP-001','Television','LIV',(9.62,3.55,.456),-math.pi/2);television()
    asset('APP-010','Laptop','OFF',(2.16,6.51,.755));laptop()
    asset('APP-007','BathVanity','BTH',(4.99,8.535,0));vanity()
    asset('APP-008','Toilet','BTH',(4.83,6.77,0),math.pi);toilet()
    asset('APP-009','ShowerKit','BTH',(6.10,6.73,0));shower()
    context_collection(coll('H90_ARCH_WetroomDetails',coll('H90_ARCHITECTURE')))
    box('KitchenBacksplash_North',(2.94,.012,.65),(8.33,8.791,1.22),'Stone',.003)
    box('KitchenBacksplash_East',(.012,2.14,.65),(9.791,7.13,1.22),'Stone',.003)
    scene()['appliances_complete']=True;scene()['status']='architecture / furnishings / appliances complete';save()
    print(json.dumps({'phase':'appliances','objects':len(scene().objects)},ensure_ascii=False))

def pot(r=.12,h=.20):
    lathe('CeramicPot',[(0,0),(r*.82,0),(r,h*.08),(r,h),(r-.012,h),(r-.015,h*.11),(0,h*.10)],mat='Stone')
    cylinder('Soil',r-.018,.006,(0,0,h-.022),'Soil')
def leaf(label,start,direction,length,width,mat='Leaf',roll=0):
    direction=Vector(direction).normalized();q=Vector((0,0,1)).rotation_difference(direction);rz=Matrix.Rotation(roll,3,'Z')
    vs=[];faces=[];rows=12
    for j in range(rows+1):
        t=j/rows;span=width*.5*math.sin(math.pi*t)**.75
        for side in (-1,0,1):
            v=Vector((side*span,length*.10*math.sin(math.pi*t)+(1-abs(side))*.015*length,length*t))
            vs.append(tuple(Vector(start)+q@(rz@v)))
    for j in range(rows):
        for k in range(2):a=j*3+k;faces.append((a,a+1,a+4,a+3))
    o=object_mesh(label,vs,faces,mat,True);solid=o.modifiers.new('Leaf_Thickness','SOLIDIFY');solid.thickness=.0006
def broadleaf():
    rng=random.Random(915);pot(.125,.205)
    for i in range(18):
        theta=i*2.399;rad=.08+rng.random()*.12;z=.28+rng.random()*.19
        start=(math.cos(theta)*rad,math.sin(theta)*rad,z)
        tube('PlantStem',[(0,0,.18),(start[0]*.35,start[1]*.35,z*.73),start],.0024,'Stem')
        direction=(math.cos(theta)*.8,math.sin(theta)*.8,.7+rng.random()*.6)
        leaf('BroadLeaf',start,direction,.18+rng.random()*.10,.06+rng.random()*.027,'LeafLight' if i%4==0 else 'Leaf',theta)
def smallplant(trailing=False):
    rng=random.Random(671);pot(.065,.115)
    for i in range(12):
        a=i*2.399;r=.045+.015*rng.random();base=(math.cos(a)*r,math.sin(a)*r,.17)
        tube('Stem',[(0,0,.105),base],.0016,'Stem')
        leaf('SmallLeaf',base,(math.cos(a),math.sin(a),.50),.08,.046,'LeafLight' if i%3==0 else 'Leaf',a)
    if trailing:
        for i in range(3):
            x=(i-1)*.025
            pts=[(x,0,.16),(x,-.10,.15),(x+.015,-.16,.02),(x-.008,-.19,-.15),(x+.03,-.21,-.29)]
            tube('TrailingStem',pts,.0017,'Stem')
            for j in range(5):
                t=j/4;yy=-.10-t*.11;zz=.14-t*.43
                leaf('TrailingLeaf',(x,yy,zz),((1 if j%2 else -1)*.85,-.15,-.4),.075,.05,'Leaf',.2*j)
def branches():
    rng=random.Random(337)
    lathe('CreamVase',[(0,0),(.057,0),(.076,.03),(.084,.09),(.070,.145),(.032,.17),(.034,.18),(.026,.18),(.028,.14)],mat='Ceramic')
    for i in range(6):
        a=i*2.399;end=Vector((.14*math.cos(a),.14*math.sin(a),.35+.06*rng.random()))
        tube('Branch',[(0,0,.15),(.03*math.cos(a),.03*math.sin(a),.26),end],.0023,'Stem')
        for j in range(5):
            t=.4+j*.14;p=Vector((0,0,.15)).lerp(end,t);side=(a+(.9 if j%2 else -.9))
            leaf('OvalLeaf',p,(math.cos(side),math.sin(side),.5),.053,.024,'LeafLight' if j%3==0 else 'Leaf',a)
def grasspot():
    rng=random.Random(543);pot(.095,.125);vs=[];fs=[]
    for i in range(90):
        a=rng.random()*2*math.pi;r=rng.random()*.063;start=Vector((r*math.cos(a),r*math.sin(a),.105));length=.17+rng.random()*.11;bend=.05+.06*rng.random()
        side=Vector((-math.sin(a),math.cos(a),0))*.0028;base=len(vs)
        for j in range(7):
            t=j/6;point=start+Vector((math.cos(a)*bend*t*t,math.sin(a)*bend*t*t,length*t));width=(1-t)*side
            vs.extend([tuple(point-width),tuple(point+width)])
        for j in range(6):fs.append((base+j*2,base+j*2+1,base+j*2+3,base+j*2+2))
    object_mesh('GrassCanopy',vs,fs,'LeafLight',True)
def mug():
    lathe('MugBody',[(0,0),(.037,0),(.043,.006),(.044,.092),(.043,.101),(.037,.101),(.036,.013),(0,.013)],mat='Ceramic')
    tube('Handle',[(.040+.029*math.sin(i*math.pi/20),0,.055+.032*math.cos(i*math.pi/20)) for i in range(21)],.0055,'Ceramic')
def tray():
    o=lathe('OvalTray',[(0,0),(.085,0),(.090,.005),(.090,.021),(.082,.025),(.078,.008),(0,.007)],mat='Oak')
    o.data.transform(Matrix.Diagonal((1.6667,1,1,1)))
def book():
    box('Pages',(.29,.21,.021),(0,0,.013),'Paper',.002)
    for z in (.001,.026):box('Hardcover',(.31,.225,.003),(0,0,z),'CreamTextile',.002)
    box('Spine',(.006,.225,.027),(-.15,0,.0135),'CreamTextile',.002)
def bottle():
    lathe('Bottle',[(0,0),(.035,0),(.043,.012),(.043,.17),(.035,.19),(.018,.21),(.018,.23),(0,.23)],mat='Ceramic')
    cylinder('PumpCollar',.021,.020,(0,0,.227),'DarkMetal')
    cylinder('PumpHead',.022,.012,(0,0,.263),'DarkMetal')
    tube('PumpSpout',[(0,0,.259),(-.045,0,.259),(-.049,0,.247)],.007,'DarkMetal')
def towel():
    box('FoldedTowel',(.30,.20,.055),(0,0,.0275),'CreamTextile',.024)
    box('TowelHem',(.292,.024,.005),(0,-.082,.055),'CreamTextile',.002)
def wicker_basket():
    lathe('BasketBody',[(.13,0),(.143,.015),(.153,.36),(.145,.385),(.132,.385),(.136,.034),(.12,.015)],mat='Jute')
    cylinder('BasketBottom',.13,.015,(0,0,.016),'Jute')
    for j in range(26):torus('HorizontalWeave',.142+j*.0003,.0035,(0,0,.035+j*.0125),'Jute')
    torus('TopRim',.147,.009,(0,0,.379),'Jute')
    for i in range(24):
        a=i*2*math.pi/24;tube('VerticalWeave',[(.141*math.cos(a),.141*math.sin(a),.025),(.15*math.cos(a),.15*math.sin(a),.37)],.0027,'Jute',False)
def slipper():
    sphere('Sole',(.10,.265,.025),(0,0,.014),'Jute',True)
    sphere('SoftUpper',(.097,.16,.068),(0,-.047,.045),'Beige',True)
def cuttingboard(w=.23,h=.27):
    outline=[(-w/2,0),(w/2,0),(w/2,h*.83),(w*.37,h),(w*.14,h),(w*.14,h+.09),(.012,h+.115),(-.012,h+.115),(-w*.14,h+.09),(-w*.14,h),(-w*.37,h),(-w/2,h*.83)]
    n=len(outline);vs=[(x,y,z) for y in (-.0085,.0085) for x,z in outline];fs=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]
    fs += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    body=object_mesh('ChoppingBoard',vs,fs,'OakVertical');bevel(body,.005)
    cut=cylinder('TemporaryHandleHole',.010,.07,(0,0,h+.07),'Oak',(math.pi/2,0,0));bool_cut(body,cut)
def kitchenjar(ceramic=False):
    r=.045;h=.095
    lathe('Jar',[(0,0),(r,0),(r,h),(r-.006,h),(r-.006,.008),(0,.008)],mat='Ceramic' if ceramic else 'Glass')
    if not ceramic:cylinder('SaltContents',.038,.078,(0,0,.043),'Stone')
    cylinder('Lid',.046,.012,(0,0,h+.005),'Ceramic' if ceramic else 'Oak')
    if ceramic:sphere('LidKnob',(.025,.025,.02),(0,0,h+.02),'Ceramic')
def throw():
    vs=[];fs=[];nx=24;ny=32
    for j in range(ny+1):
        t=j/ny
        if t<.57:y=-.19+t*.70;z=.466
        else:p=(t-.57)/.43;y=.209+.032*math.sin(p*math.pi/2);z=.466-.30*p
        for i in range(nx+1):
            x=(i/nx-.5)*.58;wrinkle=.006*math.cos(i*math.pi/3.7)*math.sin(math.pi*t)
            vs.append((x,y+wrinkle,z+wrinkle))
    for j in range(ny):
        for i in range(nx):a=j*(nx+1)+i;fs.append((a,a+1,a+nx+2,a+nx+1))
    o=object_mesh('DrapedKnittedThrow',vs,fs,'CreamTextile',True);solid=o.modifiers.new('TextileThickness','SOLIDIFY');solid.thickness=.004
    for i in range(23):
        x=-.275+i*.025;tube('Fringe',[(x,.241,.166),(x+.003,.243,.14),(x-.001,.244,.123)],.0015,'CreamTextile')
def roundcatmat():
    cylinder('SisalMat',.33,.014,(0,0,.007),'Jute')
    pts=[]
    for i in range(500):
        t=i/499;a=t*16*2*math.pi;r=.015+t*.31;pts.append((r*math.cos(a),r*math.sin(a),.016))
    tube('BraidedSpiral',pts,.0058,'Jute',False)
def toyball():sphere('SisalBall',(.072,.072,.072),(0,0,.036),'Jute')
def scratchball():
    box('Base',(.17,.17,.025),(0,0,.0125),'Oak',.015);box('UpperBase',(.13,.13,.025),(0,0,.0375),'Oak',.013)
    cylinder('SisalStem',.015,.17,(0,0,.13),'Jute');sphere('SisalTopBall',(.09,.09,.09),(0,0,.245),'Jute')

def build_props():
    bpy.context.window.scene=scene()
    if scene().get('props_complete'):raise RuntimeError('Props already built')
    big=prototype('PRP-011','BroadleafPlant',broadleaf)
    for i,(code,loc,rz) in enumerate([('BAL',(5.46,.62,0),0),('LIV',(9.27,4.72,0),.5),('MBR',(3.70,.42,0),1),('ENT',(1.0,7.36,0),2)]):instance(big,'PRP-011','BroadleafPlant',code,loc,rz,str(i+1).zfill(2))
    small=prototype('PRP-012','TablePlant',smallplant)
    for i,(code,loc) in enumerate([('LIV',(6.38,3.43,.412)),('LIV',(9.59,4.30,.458)),('OFF',(2.65,6.61,.758)),('BAL',(4.85,.65,.973))]):instance(small,'PRP-012','TablePlant',code,loc,0,str(i+1).zfill(2))
    hanging=prototype('PRP-012','TrailingPlant',smallplant,(True,));instance(hanging,'PRP-012','TrailingPlant','LIV',(9.65,4.75,2.155),-math.pi/2)
    branch=prototype('PRP-013','BranchingPlant',branches);instance(branch,'PRP-013','BranchingPlant','LIV',(6.33,5.39,.759))
    asset('PRP-015','GrassPot','BAL',(9.40,.60,.714));grasspot()
    for name,loc in [('CoffeeMug',(6.95,3.46,.410)),('DeskMug',(2.67,6.34,.765))]:
        asset('PRP-009',name,'LIV' if name=='CoffeeMug' else 'OFF',loc);mug()
    b=prototype('PRP-008','Book',book)
    for i,loc in enumerate([(6.91,3.17,.41),(6.91,3.17,.438)]):instance(b,'PRP-008','Book','LIV',loc,.03*i,str(i+1).zfill(2))
    asset('PRP-010','OvalTray','LIV',(6.91,3.17,.466));tray()
    asset('PRP-014','SparePlanter','ENT',(2.65,7.37,.858));pot(.065,.12)
    asset('PRP-004','DrapedThrow','BAL',(8.02,.48,0));throw()
    bot=prototype('PRP-017','LaundryBottle',bottle)
    for i,x in enumerate((4.42,4.55)):instance(bot,'PRP-017','LaundryBottle','BAL',(x,1.05,.973),math.pi*.15*i,str(i+1).zfill(2))
    tw=prototype('PRP-018','FoldedTowel',towel)
    for i in range(3):instance(tw,'PRP-018','FoldedTowel','BAL',(4.82,.99,.976+i*.055),0,str(i+1).zfill(2))
    asset('PRP-019','WickerBasket','BAL',(5.23,1.15,0));wicker_basket()
    sp=prototype('PRP-016','HouseSlipper',slipper)
    for i,x in enumerate((1.42,1.54)):instance(sp,'PRP-016','HouseSlipper','ENT',(x,8.46,.126),0,str(i+1).zfill(2))
    asset('PRP-020','ChoppingBoardLarge','KIT',(9.74,7.77,.916),-math.pi/2);cuttingboard()
    asset('PRP-020','ChoppingBoardSmall','KIT',(9.70,8.04,.916),-math.pi/2);cuttingboard(.18,.22)
    asset('PRP-020','SeasoningJar','KIT',(9.36,7.85,.916));kitchenjar()
    asset('PRP-020','CeramicCanister','KIT',(9.04,8.51,.916));kitchenjar(True)
    asset('PRP-007','RoundCatMat','LIV',(9.30,5.15,.012));roundcatmat()
    asset('PRP-007','ScratchPad','LIV',(8.29,4.90,0))
    box('OakBase',(.22,.17,.025),(0,0,.0125),'Oak',.015);box('SisalPad',(.21,.16,.025),(0,0,.0375),'Jute',.014)
    t=prototype('CAT-008','SisalBall',toyball)
    for i,loc in enumerate([(9.06,4.64,.005),(8.71,4.78,.005)]):instance(t,'CAT-008','SisalBall','LIV',loc,0,str(i+1).zfill(2))
    asset('CAT-008','ScratchBall','LIV',(8.25,5.10,0));scratchball()
    scene()['props_complete']=True;scene()['status']='full-house geometry and props complete';save()
    print(json.dumps({'phase':'props','objects':len(scene().objects)},ensure_ascii=False))

def area(name,loc,target,power,size,color_hex='FFF1D8',collection=None):
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size;d.color=color(color_hex)[:3]
    o=bpy.data.objects.new(name,d);(collection or coll('H90_LIGHTS')).objects.link(o);o.location=loc
    o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler();return o
def pendant():
    cylinder('CeilingRose',.044,.015,(0,0,2.745),'DarkMetal')
    tube('Suspension',[(0,0,2.738),(0,0,1.975)],.0023,'DarkMetal',False)
    cylinder('OakNeck',.018,.065,(0,0,1.95),'Oak')
    lathe('CreamDomeShade',[(.014,1.93),(.060,1.925),(.115,1.88),(.163,1.82),(.177,1.76),(.174,1.753),(.158,1.818),(.108,1.87),(.053,1.912),(.014,1.912)],mat='Ceramic')
    cylinder('Diffuser',.156,.003,(0,0,1.759),'WarmDiffuser')
def bedside_lamp():
    cylinder('RoundBase',.070,.018,(0,0,.009),'Steel')
    cylinder('Stem',.013,.17,(0,0,.10),'Steel')
    lathe('Shade',[(.055,.16),(.076,.19),(.085,.27),(.063,.345),(0,.35)],mat='Ceramic')
def downlight():
    cylinder('Trim',.050,.016,(0,0,0),'Ceramic');cylinder('Lens',.040,.003,(0,0,-.01),'WarmDiffuser')
def camera(name,loc,target,lens=22,ortho=None):
    d=bpy.data.cameras.new(name);o=bpy.data.objects.new(name,d);coll('H90_CAMERAS').objects.link(o);o.location=loc;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
    d.clip_start=.03;d.clip_end=250;d.lens=lens;d.sensor_width=36
    if ortho:d.type='ORTHO';d.ortho_scale=ortho
    return o
def build_lighting():
    bpy.context.window.scene=scene();s=scene()
    if s.get('lighting_complete'):raise RuntimeError('Lighting already built')
    asset('LGT-002','DiningPendant','LIV',(6.35,5.35,0));pendant()
    lp=prototype('LGT-003','BedsideLamp',bedside_lamp)
    for i,x in enumerate((.44,2.88)):
        instance(lp,'LGT-003','BedsideLamp','MBR',(x,3.56,.478),0,str(i+1).zfill(2))
        area('H90_Light_Bedside_'+str(i),(x,3.56,.735),(x,3.56,.48),5,.11,'FFE1B2')
    dl=prototype('LGT-001','Downlight',downlight)
    # Irregular room positions use collection instances, not a forced uniform array.
    positions=[('LIV',5.8,2.35),('LIV',8.3,2.35),('LIV',5.8,4.45),('LIV',8.3,4.45),('MBR',1.7,1.4),('OFF',1.9,5.45),('ENT',2.65,7.9),('KIT',8.2,7.5),('BTH',5.2,7.6),('BAL',7.2,.9)]
    for i,(rc,x,y) in enumerate(positions):
        instance(dl,'LGT-001','Downlight',rc,(x,y,2.772),0,str(i+1).zfill(2));area('H90_Light_Downlight_'+str(i),(x,y,2.69),(x,y,0),12,.17,'FFF0DC')
    area('H90_Light_Pendant',(6.35,5.35,1.735),(6.35,5.35,.74),18,.25,'FFE3B7')
    for code,loc,w,rz in [('KIT',(9.58,7.13,1.805),2.05,-math.pi/2),('BAL',(4.67,.86,1.715),.64,math.pi)]:
        asset('LGT-004','CabinetStrip',code,loc,rz)
        box('AluminumChannel',(w,.023,.013),(0,0,0),'Steel',.003);box('Diffuser',(w-.012,.019,.003),(0,0,-.008),'WarmDiffuser',.001)
    area('H90_Light_KitchenTask',(9.39,7.3,1.74),(9.5,7.3,.9),10,.5,'FFE7C5')
    area('H90_Light_LaundryTask',(4.67,.80,1.68),(4.67,.82,.95),8,.4,'FFE7C5')
    # Window-facing broad emitters establish readable daylight with the roof in place.
    area('H90_Daylight_Balcony',(7.2,-.8,3.05),(7.2,3.7,.7),650,4.4,'FFF2DF')
    area('H90_Daylight_Master',(1.9,-.7,2.9),(1.9,2.6,.6),180,2.2,'FFF1DE')
    area('H90_Daylight_Office',(-.65,5.6,2.9),(1.4,5.6,.8),130,1.7,'F2F4F1')
    area('H90_StudioFill',(4.5,3.8,8),(5,4.5,0),450,8,'FFF4E7')
    d=bpy.data.lights.new('H90_Sun_Daylight','SUN');d.energy=1.5;d.angle=math.radians(5);d.color=color('FFF0D5')[:3]
    o=bpy.data.objects.new('H90_Sun_Daylight',d);coll('H90_LIGHTS').objects.link(o);o.rotation_euler=Vector((.3,.75,-1.1)).to_track_quat('-Z','Y').to_euler()
    world=bpy.data.worlds.new('H90_World_Daylight');world.use_nodes=True
    bg=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs['Color'].default_value=(.60,.70,.78,1);bg.inputs['Strength'].default_value=.35;s.world=world
    studio=coll('H90_PRESENTATION_Studio');context_collection(studio);box('StudioGround',(200,200,.08),(5,4.5,-.20),'Studio',.01);studio.hide_viewport=True
    asset('ARC-011','VentGrille','LIV',(7.4,5.95,2.63))
    box('GrilleFrame',(1.85,.024,.14),(0,0,0),'Wall',.008)
    slat=box('Louver',(1.80,.012,.006),(0,-.019,-.05),'DarkMetal',.001);array(slat,'ARRAY_Grille_Louvers',8,(0,0,.014))
    cams=[camera('H90_CAM_Overview',(1,-11,16.8),(5,4.5,.52),ortho=12.8),
          camera('H90_CAM_Living',(4.46,1.88,1.50),(7.70,4.55,1.19),20),
          camera('H90_CAM_Balcony',(7.07,3.35,1.42),(7.10,.48,1.03),18),
          camera('H90_CAM_Top',(5,4.5,20),(5,4.5,0),ortho=11.3)]
    s.camera=cams[0];s.render.resolution_x=1600;s.render.resolution_y=1280;s.render.resolution_percentage=100
    s.render.image_settings.file_format='PNG';s.render.image_settings.color_mode='RGB';s.render.film_transparent=False
    s.view_settings.view_transform='AgX';s.view_settings.exposure=.55
    try:
        s.render.engine='CYCLES';s.cycles.samples=64;s.cycles.use_denoising=True;s.cycles.adaptive_threshold=.035
        pref=bpy.context.preferences.addons['cycles'].preferences;pref.compute_device_type='METAL';pref.refresh_devices()
        for dev in pref.devices:dev.use=dev.type=='METAL'
        s.cycles.device='GPU';s.cycles.max_bounces=8;s.cycles.transparent_max_bounces=12
    except Exception as exc:
        s.render.engine='BLENDER_EEVEE';s['render_setup_note']=str(exc)
    bpy.data.collections['H90_ARCH_Walls_Upper'].hide_render=True
    s['lighting_complete']=True;s['status']='full scene, lighting ready; render QA pending';save()
    print(json.dumps({'phase':'lighting','engine':s.render.engine,'objects':len(s.objects),'cameras':[o.name for o in cams]},ensure_ascii=False))

def add_reference_cat():
    bpy.context.window.scene=scene()
    if scene().get('reference_cat_added'):return
    source=ROOT/'02_资产库/CHR/H90-CHR-001_ReferenceCat/Source/Original/tripo_convert_83705d6a-4c92-49aa-bb88-feae40d01349.fbx'
    before=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath=str(source));new=[o for o in bpy.data.objects if o not in before]
    bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get();meshes=[o for o in new if o.type=='MESH']
    if not meshes:raise RuntimeError('Reference-cat FBX contains no mesh')
    points=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
    lo=Vector([min(p[i] for p in points) for i in range(3)]);hi=Vector([max(p[i] for p in points) for i in range(3)]);dims=hi-lo
    factor=min(.32/max(dims.z,.001),.48/max(dims.x,dims.y,.001));centre=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
    root=asset('CHR-001','ReferenceCat_Static','BAL',(7.47,.49,.458),math.pi)
    root['source_fbx']=str(source);root['character_scope']='static scene placement; animation rig not modified or certified'
    for i,o in enumerate(meshes):
        mesh=bpy.data.meshes.new_from_object(o.evaluated_get(dg),preserve_all_data_layers=True,depsgraph=dg)
        mesh.transform(Matrix.Scale(factor,4)@Matrix.Translation(-centre)@o.matrix_world)
        obj=bpy.data.objects.new(root.name.replace('_ROOT',f'_P{i+1:02d}_Body'),mesh);COL.objects.link(obj);obj.parent=root
        obj['part_role']='StaticCharacterMesh'
        for mat in mesh.materials:
            if not mat:continue
            for n in mat.node_tree.nodes if mat.node_tree else []:
                if n.type=='BSDF_PRINCIPLED':n.inputs['Roughness'].default_value=.74;n.inputs['Metallic'].default_value=0
    for o in new:bpy.data.objects.remove(o,do_unlink=True)
    texture_root=source.parent
    images=[p for p in texture_root.rglob('*') if p.suffix.lower() in ('.png','.jpg','.jpeg')]
    if images:
        tex=bpy.data.images.load(str(images[0]),check_existing=True)
        for obj in root.children:
            for mat in obj.data.materials:
                if not mat:continue
                mat.use_nodes=True;bs=next((n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
                if bs and not bs.inputs['Base Color'].is_linked:
                    n=mat.node_tree.nodes.new('ShaderNodeTexImage');n.image=tex;mat.node_tree.links.new(n.outputs['Color'],bs.inputs['Base Color'])
    bpy.ops.file.pack_all();scene()['reference_cat_added']=True;save()
    print(json.dumps({'phase':'cat','source_dimensions':list(dims),'uniform_scale':factor,'static_mesh_parts':len(meshes),'images_available':len(images)},ensure_ascii=False))

def refine_scene():
    bpy.context.window.scene=scene();s=scene()
    s.view_settings.exposure=0.0
    next(n for n in s.world.node_tree.nodes if n.type=='BACKGROUND').inputs['Strength'].default_value=.25
    for name,power in [('H90_Daylight_Balcony',450),('H90_Daylight_Master',150),('H90_StudioFill',280)]:bpy.data.objects[name].data.energy=power
    bpy.data.objects['H90_Sun_Daylight'].data.energy=1.25
    cam=bpy.data.objects['H90_CAM_Overview'];cam.data.ortho_scale=14.2;cam.rotation_euler=(Vector((5,4.5,.88))-cam.location).to_track_quat('-Z','Y').to_euler()
    for o in s.objects:
        if o.get('asset_id')=='H90-ARC-011':o.location.y=5.65
    if not s.get('refinement_geometry_done'):
        for o in list(s.objects):
            if o.name in ('H90_Skirting_LIV_S','H90_Skirting_ENT_S','H90_Skirting_HALL_N','H90_Skirting_HALL_W','H90_Skirting_HALL_E'):bpy.data.objects.remove(o,do_unlink=True)
        context_collection(bpy.data.collections['H90_ARCH_Skirting'])
        for name,x0,x1,y in [('LIV_S_W',4.26,5.41,1.666),('LIV_S_E',8.69,9.80,1.666),('ENT_S',.2,2.94,7.066)]:box('Skirting_'+name,(x1-x0,.012,.07),((x0+x1)/2,y,.035),'Oak',.003)
        for name,x,y0,y1 in [('HALL_W_S',3.066,4.06,5.11),('HALL_W_N',3.066,6.04,6.94),('HALL_E_S',4.134,4.06,4.41),('HALL_E_N',4.134,5.76,6.94)]:box('Skirting_'+name,(.012,y1-y0,.07),(x,(y0+y1)/2,.035),'Oak',.003)
        for root in [r for r in s.objects if r.get('asset_id')=='H90-CAT-007']:
            for child in root.children:
                if child.get('part_role')=='OvalBedRim':child.data.transform(Matrix.Diagonal((1.35,.58,1,1)))
                if child.get('part_role')=='BedPad':child.data.transform(Matrix.Diagonal((1.33,.90,1,1)))
        ceiling=coll('H90_ARCH_Ceilings')
        asset('ARC-010','LivingBulkhead','LIV',(7.03,5.80,2.69),collection=ceiling)
        box('Bulkhead',(5.54,.28,.22),(0,0,0),'Wall',.008)
        lower=bpy.data.collections['H90_ARCH_Walls_Base'];upper=bpy.data.collections['H90_ARCH_Walls_Upper']
        asset('ARC-008','BathTileFace','BTH',collection=lower)
        o=box('EastBathTile_Lower',(.008,2.74,1.15),(6.735,7.43,.575),'BathTile',.001);o['wall_id']='inner_2'
        o=box('NorthBathTile_Lower',(2.48,.008,1.15),(5.5,8.795,.575),'BathTile',.001);o['wall_id']='outer_north'
        root=PARENT;COL_saved=COL
        context_collection(upper)
        o=box('EastBathTile_Upper',(.008,2.74,1.65),(6.735,7.43,1.975),'BathTile',.001);o['wall_id']='inner_2';o['asset_id']='H90-ARC-008'
        o=box('NorthBathTile_WindowSill',(2.48,.008,.35),(5.5,8.795,1.325),'BathTile',.001);o['wall_id']='outer_north';o['asset_id']='H90-ARC-008'
        s['refinement_geometry_done']=True
    for o in s.objects:
        if o.get('wall_id') and not o.get('asset_id'):o['asset_id']='H90-ARC-004' if o['wall_id'].startswith('outer_') else 'H90-ARC-005'
        if o.name.startswith('H90_Ceiling_'):o['asset_id']='H90-ARC-006'
        if o.name.startswith('H90_Skirting_'):o['asset_id']='H90-ARC-007'
        if o.name.startswith('H90_KitchenBacksplash'):o['asset_id']='H90-ARC-009'
    s['status']='geometry refined; final render QA in progress';save()
    print('H90_REFINEMENTS_APPLIED')
