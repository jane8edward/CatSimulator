"""Render each view in a fresh Blender process to release GPU memory between views."""
import subprocess,os,shutil
from pathlib import Path
r=Path(__file__).resolve().parents[1];blender=os.environ.get('BLENDER_EXECUTABLE') or shutil.which('blender') or '/Applications/Blender.app/Contents/MacOS/Blender'
for view in ['Balcony','Overview','Top','Living']:
 subprocess.run([blender,'--background',str(r/'04_场景工程/New_House_v001.blend'),'--python-exit-code','1','--python',str(r/'06_脚本/render_new_house.py'),'--',view,'1800','64'],check=True)
subprocess.run([blender,'--background','--python-exit-code','1','--python',str(r/'06_脚本/validate_new_house.py')],check=True)
print('NEW_HOUSE_DELIVERY_VALIDATED',flush=True)
