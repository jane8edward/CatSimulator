import bpy,runpy,sys
from pathlib import Path
r=Path(__file__).resolve().parents[1]
for mode in ['Balcony','Overview','Top','Living']:
 bpy.ops.wm.open_mainfile(filepath=str(r/'04_场景工程/New_House_v001.blend'))
 sys.argv=['blender','--',mode,'1800','64']
 runpy.run_path(str(r/'06_脚本/render_new_house.py'),run_name='__main__')
