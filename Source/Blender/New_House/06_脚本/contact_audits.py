from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,sys
ROOT=Path(__file__).resolve().parents[1];items=[x for x in json.loads((ROOT/'00_管理/解压清单.json').read_text()) if x.get('models')]
OUT=ROOT/'05_渲染与检查/02_逐项对照';OUT.mkdir(exist_ok=True)
try:font=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',17)
except:font=ImageFont.load_default()
for page in range((len(items)+5)//6):
 selected=items[page*6:page*6+6]
 if not all((ROOT/'05_渲染与检查/01_导入检查'/f"{x['id']}_underside.png").exists() for x in selected):continue
 im=Image.new('RGB',(1280,6*350),(248,248,248));d=ImageDraw.Draw(im)
 for i,x in enumerate(selected):
  label=x['id']+'  '+x['archive'].replace('+',' ');d.text((8,i*350+3),label,font=font,fill=(15,15,15))
  for j,angle in enumerate(['front','rear','underside']):
   p=ROOT/'05_渲染与检查/01_导入检查'/f"{x['id']}_{angle}.png";pic=Image.open(p).convert('RGB').resize((320,320));im.paste(pic,(j*320,i*350+27))
  mapping=ROOT/'00_管理/资产映射与修正.json'
  if mapping.exists():
   a=json.loads(mapping.read_text()).get(x['id'],{})
   if a.get('reference'):
    pic=Image.open(ROOT/a['reference']).convert('RGB');pic.thumbnail((310,310));im.paste(pic,(960+(320-pic.width)//2,i*350+27+(320-pic.height)//2))
 im.save(OUT/f'P{page+1:02d}_models.png')
print('Contact sheets ready',len(list(OUT.glob('P*.png'))))
