import bpy,sys,runpy
from pathlib import Path
root=Path(__file__).resolve().parents[1];project=root/'04_场景工程/New_House_v001.blend'
for mode in ['Overview','Living','Balcony','Top']:
 bpy.ops.wm.open_mainfile(filepath=str(project));sys.argv=['blender','--',mode,'1100','16'];runpy.run_path(str(root/'06_脚本/render_new_house.py'),run_name='__main__')
