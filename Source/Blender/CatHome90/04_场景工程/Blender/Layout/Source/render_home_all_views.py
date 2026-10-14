import bpy,sys,runpy
from pathlib import Path
project=Path(__file__).resolve().parent.parent/'H90_Residence_v001.blend'
worker=Path(__file__).resolve().parent/'render_home_scene.py'
for mode in ('Overview','Living','Balcony','Top'):
    bpy.ops.wm.open_mainfile(filepath=str(project))
    sys.argv=['blender','--',mode,'64','1600' if mode!='Top' else '1400']
    runpy.run_path(str(worker),run_name='__main__')
print('H90_ALL_FINAL_RENDERS_COMPLETE',flush=True)
