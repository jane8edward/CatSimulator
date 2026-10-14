import bpy, sys, json
from pathlib import Path
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
mode=args[0] if args else 'Overview';samples=int(args[1]) if len(args)>1 else 64;size=int(args[2]) if len(args)>2 else 1600
ROOT=Path(__file__).resolve().parents[4]
out=ROOT/'05_预览与验收/场景预览/H90_v001';out.mkdir(parents=True,exist_ok=True)
s=bpy.data.scenes['H90_Residence_Main']
if bpy.context.window:bpy.context.window.scene=s
s.camera=bpy.data.objects['H90_CAM_'+mode]
overview=mode in ('Overview','Top')
upper=bpy.data.collections['H90_ARCH_Walls_Upper'];upper.hide_viewport=False;upper.hide_render=False
for o in upper.objects:o.hide_render=overview and o.get('wall_id') not in ('outer_north','outer_east')
ceiling=bpy.data.collections['H90_ARCH_Ceilings'];ceiling.hide_viewport=False;ceiling.hide_render=overview
for obj in s.objects:
    if obj.get('asset_id') in ('H90-LGT-001','H90-LGT-002','H90-ARC-011'):
        obj.hide_render=overview
        for child in obj.children_recursive:child.hide_render=overview
    if obj.get('wall_id')=='inner_7':obj.hide_render=(mode=='Balcony') or (overview and obj.name in upper.objects)
    if 'balcony_slider' in obj.name:obj.hide_render=mode=='Balcony'
s.render.resolution_x=size;s.render.resolution_y=round(size*(.8 if overview else 2/3));s.render.resolution_percentage=100
s.render.image_settings.file_format='PNG';s.render.image_settings.color_mode='RGB'
s.render.filepath=str(out/f'H90_{mode}_Cycles_v001.png')
s.render.engine='CYCLES';s.cycles.samples=samples;s.cycles.use_denoising=True;s.cycles.adaptive_threshold=.045 if samples<48 else .025
pref=bpy.context.preferences.addons['cycles'].preferences
try:
    pref.compute_device_type='METAL';pref.refresh_devices()
    for d in pref.devices:d.use=d.type=='METAL'
    s.cycles.device='GPU'
except Exception:s.cycles.device='CPU'
print('H90_RENDER_START',mode,s.render.filepath,flush=True)
presets=ROOT/'04_场景工程/Blender/Layout/Views';presets.mkdir(parents=True,exist_ok=True)
s['renderViewPreset']=mode
bpy.ops.wm.save_as_mainfile(filepath=str(presets/f'H90_{mode}_view_v001.blend'),copy=True)
bpy.ops.render.render(write_still=True,scene=s.name)
print('H90_RENDER_COMPLETE',mode,s.render.filepath,flush=True)
