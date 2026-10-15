from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,math
ROOT=Path(__file__).resolve().parents[1]
META=json.loads((ROOT/'00_管理/资产映射与修正.json').read_text())
MAN=json.loads((ROOT/'00_管理/解压清单.json').read_text())
REPORT=json.loads((ROOT/'00_管理/场景检查.json').read_text())
try:
 font=ImageFont.truetype('Arial.ttf',17);small=ImageFont.truetype('Arial.ttf',14)
except OSError:
 font=ImageFont.load_default(size=17);small=ImageFont.load_default(size=14)
out=ROOT/'05_渲染与检查/04_最终资产对照';out.mkdir(exist_ok=True)
items=list(META.items())
def thumbnail(canvas,path,cell):
 p=Image.open(path).convert('RGB');p.thumbnail((cell[2],cell[3]));canvas.paste(p,(cell[0]+(cell[2]-p.width)//2,cell[1]+(cell[3]-p.height)//2))
for page in range(math.ceil(len(items)/8)):
 im=Image.new('RGB',(1280,1500),'white');draw=ImageDraw.Draw(im)
 for j,(ident,meta) in enumerate(items[page*8:page*8+8]):
  x=(j%2)*640;y=(j//2)*375
  draw.text((x+10,y+5),f'{ident}  {meta["slug"]}',font=font,fill='black')
  draw.text((x+10,y+29),'Blender corrected model',font=small,fill='gray');draw.text((x+330,y+29),'Reference',font=small,fill='gray')
  thumbnail(im,ROOT/f'05_渲染与检查/03_修正后模型/{ident}_ready.png',(x,y+52,320,320))
  thumbnail(im,ROOT/meta['reference'],(x+320,y+52,320,320))
 im.save(out/f'Final_{page+1:02d}.png')
important=['NH010','NH036','NH043','NH049','NH050','NH076']
im=Image.new('RGB',(1440,1080),'white');d=ImageDraw.Draw(im)
for j,k in enumerate(important):
 x=(j%3)*480;y=(j//3)*540;d.text((x+12,y+10),k+' '+META[k]['slug'],font=font,fill='black');thumbnail(im,ROOT/f'05_渲染与检查/03_修正后模型/{k}_ready.png',(x,y+40,480,480))
im.save(ROOT/'05_渲染与检查/03_修正后模型/重点修正对照.png')
# Record the axis correction in the machine-readable log too.
p=ROOT/'03_整理资产/NH036/修正与校准.json';cat=json.loads(p.read_text());note='Corrected canonical forward axis to retain the natural body proportions before metre calibration.'
if note not in cat['corrections']:cat['corrections'].append(note)
p.write_text(json.dumps(cat,ensure_ascii=False,indent=2))
archive_by_id={a['id']:a['archive'] for a in MAN}
fixes={'downlight_cleanup':'清除背景面、碎片；校正灯盘朝向','rug_flatten':'压平不合理厚度和底部突起','bed_cleanup':'去除床垫上方的悬浮布料和背景碎面','wardrobe_back':'修整柜体外表面的误生旋钮，补门缝','basket_color':'补藤编材质','litter_check':'核对圆入口、通风口，纠正文件名映射','shower_glass':'将误生实心面恢复为玻璃','ceramic':'补陶瓷材质','pump_color':'补瓶身与泵头材质','grass_color':'补绿叶与陶瓷盆材质','jar_color':'补木盖、谷物材质及薄玻璃瓶身','board_color':'补橡木材质'}
lines=['# New_House 模型检查与修正记录','', '本批次独立工程。79 个压缩包已全部解压，其中 78 个包含模型，1 个是 Tripo Blender Bridge 插件源码包。插件包仅解压，没有作为场景资产导入。原始压缩包仍在 `../New_House_Items`。','', '## 检查与保留原则','', '- 每个模型均在 Blender 导入，并渲染正面、背面和底部进行检查；对照已复制到本工程的参考图片。','- 原始 FBX 与贴图保留在 `02_解压原文件`。`*_Asset.blend` 是导入检查副本；`*_Ready.blend` 是修正、尺寸校准后的场景资产。','- 场景副本对高密度网格使用约 18 万面预算，保留主轮廓、UV 与材质；没有对主体进行全面重建拓扑。','- 场景尺寸以原设计图纸与 10 × 9 m 外轮廓为准，单位为米；物品尺寸是场景设计标定值。','- 重复物品使用共享 Collection 实例；房间地板以 8 组模块、16 个可编辑 Array 修改器铺设。','', '## 重点修正','', '- 文件名错配：绳球、高窗、猫砂柜、窗边猫窝按照实际网格重新归类。','- 筒灯删除大片背景与游离三角面；地毯改为合理薄度。','- 门窗的 AI 填实玻璃和锯齿框架已修整；仅替换损坏的框架和窗面，保留参考中的轮廓、尺寸与分格。阳台封窗按参考恢复五格分区。','- 主卧床去除悬浮布料、背景碎面；衣柜移除误生旋钮阵列，保留柜体并补门缝。','- 无贴图的罐子、切菜板、猫草、洗衣篮等补基础材质；谷物罐补薄玻璃瓶身。','- 猫模型纠正导入朝向，保留自然长宽比例；阳台洗衣柜按双格结构校准，洗衣机进入开放格。','- 本批没有拖鞋模型，依据单物体参考补建 NH080；小号菜板、叠毛巾使用现有模型的实例变体。','', '- 厨房按实际橱柜开孔重新定位水槽、灶具与油烟机；双灶周围补窄台面收口，避免原 AI 预留大洞裸露。', '', '## 逐项索引','', '下列尺寸为独立资产的宽 × 深 × 高；场景中部分物体为符合布局使用了实例尺寸变体。对照图见 `05_渲染与检查/02_逐项对照` 和 `04_最终资产对照`。','', '|编号 / 模型|来源压缩包|参考图|校准尺寸（m）|处理|整理后工程|','|---|---|---|---|---|---|']
for k,m in items:
 fix=m.get('correction');desc=fixes.get(fix,'保留主体，校准尺寸与原点')
 if fix and (fix.startswith('window') or fix.startswith('glazing')):desc='修整框架边缘，恢复薄玻璃与参考分格'
 if k=='NH036':desc='纠正轴向；保留自然比例、四肢和尾巴'
 if m.get('sourceType')=='supplemental':desc='按缺失参考补建拖鞋'
 dims=' × '.join(f'{x:g}' for x in m['dimensions_m']);ref=Path(m['reference']).name
 lines.append(f'|{k} {m["slug"]}|{archive_by_id.get(k,"补建")}|[{ref}](../{m["reference"]})|{dims}|{desc}|[Ready](../03_整理资产/{k}/{k}_Ready.blend)|')
lines+=['','## 使用范围','', '已完成静态住宅场景、基础照明与渲染。AI 生成资产中部分表面细节属于贴图表达，未逐一改造成制造级结构；猫为静态模型。本次不包含角色动画、碰撞体、Unity 导出和移动端性能优化。','', '主工程的打开状态是便于查看全屋的剖切总览。完整墙体、天花、门窗均保留；查看完整室内时可切换到 Living 或 Balcony 相机，显示被隐藏的建筑对象。渲染脚本会根据视角自动切换展示遮罩。']
(ROOT/'00_管理/模型检查与修正记录.md').write_text('\n'.join(lines)+'\n')
readme='''# New_House · 90㎡养猫之家

这是使用 `New_House_Items` 中新一批 Tripo 模型搭建的独立工程，没有从此前的住宅、猫模型或 Unity 工程导入场景对象，也未改写这些工程。

## 打开工程

[New_House_v001.blend](04_场景工程/New_House_v001.blend) — Blender 5.2.2 LTS，米制单位，Cycles。

默认显示全屋剖切视角。四台相机分别为 `NH_CAM_Overview`、`NH_CAM_Top`、`NH_CAM_Living`、`NH_CAM_Balcony`。完整上墙和天花仍在 Architecture 集合；默认仅为总览隐藏。可以在大纲视图中恢复显示，或在视口按 Alt-H 恢复隐藏对象。

## 交付内容

- [全屋总览](05_渲染与检查/06_最终渲染/New_House_Overview_Cycles.png)
- [俯视布局](05_渲染与检查/06_最终渲染/New_House_Top_Cycles.png)
- [客厅与猫咪活动区](05_渲染与检查/06_最终渲染/New_House_Living_Cycles.png)
- [阳台观景区](05_渲染与检查/06_最终渲染/New_House_Balcony_Cycles.png)
- [逐项模型检查与修正记录](00_管理/模型检查与修正记录.md)
- [最终验收数据](00_管理/最终验收.json)

## 目录

|目录|内容|
|---|---|
|00_管理|解压清单、模型映射、尺寸、修正与场景检查记录|
|01_参考资料|本工程使用的物体参考、平面图、效果图、布局尺寸|
|02_解压原文件|79 个压缩包分别解压，保留原始 FBX、贴图和插件源码|
|03_整理资产|78 个导入检查副本、79 个校准后资产、贴图与逐项记录|
|04_场景工程|本次独立搭建的住宅主工程|
|05_渲染与检查|原始三角度检查图、参考对照页、修正后预览及正式渲染|
|06_脚本|本次工程的导入、修整、搭建、渲染与交付辅助脚本|

## 场景结构与性能

住宅按原设计重建外轮廓 10 × 9 m、层高 2.8 m 的两室一厅一卫、厨房与阳台。全屋使用 78 种本批 Tripo 资产与 1 种参考补建拖鞋，重复物品采用共享 Collection 实例；地板使用 16 个 Array 修改器。

高精度来源保留在原始 FBX 和导入检查副本。场景使用约 18 万面预算的资产副本，常规贴图以 1K、大件以 2K 进入场景；贴图已打包，便于单独打开工程。此文件仍是可编辑的视觉场景，未完成游戏运行时优化。

渲染采用 Cycles、Metal GPU、降噪和基础自然光/室内灯光。正式图为 64 采样，具体尺寸和耗时见每张图旁的 render.json。
'''
(ROOT/'README.md').write_text(readme)
print('Delivery contacts and documentation prepared',len(items))
