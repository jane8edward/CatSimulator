"""Render explicit presentation masks; complete architecture remains in the project."""
import bpy,sys,json,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
mode=args[0] if args else 'Overview';size=int(args[1]) if len(args)>1 else 1800;samples=int(args[2]) if len(args)>2 else 64
s=bpy.context.scene;assert s.get('project')=='New_House'
col=bpy.data.collections
for c in col:c.hide_render=False
for o in s.objects:o.hide_render=False
upper=col['NH_Architecture_Walls_Upper'];ceilings=col['NH_Architecture_Ceilings'];overview=mode in ['Overview','Top']
ceilings.hide_render=overview
for o in upper.objects:o.hide_render=overview and o.get('wall_id') not in ['outer_north','outer_east']
for o in s.objects:
 if o.get('source_id') in ['NH010','NH028'] and o.instance_type=='COLLECTION':o.hide_render=overview
 if mode=='Balcony' and (o.get('wall_id')=='inner_7' or o.get('source_id')=='NH039'):o.hide_render=True
# The exterior base remains; distant buildings are only visible through windows in interiors.
for o in col['NH_Exterior_Context'].objects:
 if 'BackdropBuilding' in o.name:o.hide_render=overview
s.camera=bpy.data.objects['NH_CAM_'+mode]
s.render.engine='CYCLES';s.cycles.samples=samples;s.cycles.use_denoising=True;s.cycles.adaptive_threshold=.045 if samples<=24 else .025
s.render.resolution_x=size;s.render.resolution_y=round(size*(.84 if overview else 2/3));s.render.resolution_percentage=100
s.render.image_settings.file_format='PNG';s.render.image_settings.color_mode='RGB'
pref=bpy.context.preferences.addons['cycles'].preferences
try:
 pref.compute_device_type='METAL';pref.refresh_devices()
 for d in pref.devices:d.use=d.type=='METAL'
 s.cycles.device='GPU'
except Exception:s.cycles.device='CPU'
out=ROOT/'05_渲染与检查'/('05_试渲染' if samples<=24 else '06_最终渲染');out.mkdir(exist_ok=True)
s.render.filepath=str(out/f'New_House_{mode}_Cycles.png')
print('NH_RENDER_START',mode,samples,size,flush=True);start=time.time();bpy.ops.render.render(write_still=True)
assert Path(s.render.filepath).is_file() and Path(s.render.filepath).stat().st_mtime >= start-1, 'Render did not finish writing a fresh image'
report={'view':mode,'engine':'Cycles','device':s.cycles.device,'samples':samples,'resolution':[s.render.resolution_x,s.render.resolution_y],'seconds':round(time.time()-start,2),'sceneModifiedTime':Path(bpy.data.filepath).stat().st_mtime,'image':str(Path(s.render.filepath).relative_to(ROOT))}
(out/f'{mode}_render.json').write_text(json.dumps(report,indent=2));print('NH_RENDER_DONE',json.dumps(report),flush=True)
