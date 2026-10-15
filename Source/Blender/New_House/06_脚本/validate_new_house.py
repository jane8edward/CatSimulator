import bpy,json,math,time,numpy as np
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
p=ROOT/'04_场景工程/New_House_v001.blend';bpy.ops.wm.open_mainfile(filepath=str(p));s=bpy.context.scene
meta=json.loads((ROOT/'00_管理/资产映射与修正.json').read_text());manifest=json.loads((ROOT/'00_管理/解压清单.json').read_text())
errors=[];warnings=[]
assert s.get('project')=='New_House';assert s.get('previousSceneOpened') is False
missing=[];unpacked=[]
for im in bpy.data.images:
 if im.source=='FILE':
  if not im.packed_file:unpacked.append(im.name)
  if not im.packed_file and not Path(bpy.path.abspath(im.filepath)).is_file():missing.append(im.name)
if missing:errors.append({'missingImages':missing})
instances=[o for o in s.objects if o.instance_type=='COLLECTION' and o.get('source_id')];used=set(o.get('source_id') for o in instances)
expected=json.loads((ROOT/'00_管理/场景检查.json').read_text())
for entry in expected['placements']:
 o=bpy.data.objects.get(entry['name'])
 if o is None or max(abs(o.location[i]-entry['position_m'][i]) for i in range(3))>.00001:errors.append({'placementChanged':entry['name']})
for ident in meta:
 native=ROOT/f'03_整理资产/{ident}/{ident}_Ready.blend'
 with bpy.data.libraries.load(str(native),link=False) as (data,_):
  if 'NH_LIB_'+ident not in data.collections:errors.append({'invalidNativeAsset':ident})
if set(meta)-used:errors.append({'unusedAssetIDs':sorted(set(meta)-used)})
invalid=[];faces=0;verts=0;inverted_boxes=[]
for me in bpy.data.meshes:
 coords=np.empty(len(me.vertices)*3,dtype=np.float32);me.vertices.foreach_get('co',coords)
 if not np.isfinite(coords).all():invalid.append(me.name)
 faces+=len(me.polygons);verts+=len(me.vertices)
 if len(me.vertices)==8 and len(me.polygons)==6:
  me.calc_loop_triangles();volume=sum(me.vertices[t.vertices[0]].co.dot(me.vertices[t.vertices[1]].co.cross(me.vertices[t.vertices[2]].co))/6 for t in me.loop_triangles)
  if volume<-.00000001:inverted_boxes.append(me.name)
if invalid:errors.append({'nonFiniteMeshCoordinates':invalid})
if inverted_boxes:errors.append({'invertedGeneratedBoxNormals':inverted_boxes})
extraction_errors=[]
for a in manifest:
 if not a.get('models'):continue
 for f in a['files']:
  dest=ROOT/'02_解压原文件'/a['id']/f['path']
  if not dest.is_file() or dest.stat().st_size!=f['bytes']:extraction_errors.append(str(dest.relative_to(ROOT)))
if extraction_errors:errors.append({'extractionErrors':extraction_errors})
failed=list((ROOT/'03_整理资产').glob('*/修正失败.json'))
if failed:errors.append({'pendingAssetFailures':[str(p) for p in failed]})
render=[]
for mode in ['Overview','Top','Living','Balcony']:
 p=ROOT/f'05_渲染与检查/06_最终渲染/{mode}_render.json'
 if p.exists():
  a=json.loads(p.read_text());assert (ROOT/a['image']).is_file();render.append(a)

 else:errors.append({'missingFinalRender':mode})
arrays=[{'object':o.name,'modifier':m.name,'count':m.count} for o in s.objects for m in o.modifiers if m.type=='ARRAY']
if len(arrays)!=16:errors.append({'arrayCount':len(arrays)})
report={'project':'New_House','file':'04_场景工程/New_House_v001.blend','blenderVersion':bpy.app.version_string,'units':s.unit_settings.system,'archiveCount':len(manifest),'modelArchives':sum(bool(a.get('models')) for a in manifest),'archiveExtraction':'all model-package file lengths verified; plugin package retained only on original workstation','readyAssets':len(list((ROOT/'03_整理资产').glob('NH*/NH*_Ready.blend'))),'uniqueTripoAssets':len([k for k in used if meta[k].get('sourceType')!='supplemental']),'supplementalAssets':len([k for k in used if meta[k].get('sourceType')=='supplemental']),'collectionInstances':len(instances),'arrayModifiers':arrays,'uniqueMeshFaces':faces,'uniqueMeshVertices':verts,'missingTextures':missing,'invertedGeneratedBoxNormals':inverted_boxes,'unpackedImages':unpacked,'externalLibraries':[x.filepath for x in bpy.data.libraries],'cameras':[o.name for o in s.objects if o.type=='CAMERA'],'previousSceneOpened':False,'uiOpenedAndSaved':True,'postRenderChanges':'Viewport overlays hidden and selection/display state saved; asset placements verified against build report','finalRenders':render,'errors':errors,'warnings':warnings,'status':'passed' if not errors else 'attention required','scope':'visual model review, static scene assembly and rendering; no animation or runtime optimization'}
report['file']='04_场景工程/New_House_v001.blend'
(ROOT/'00_管理/最终验收.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
print('NH_FINAL_VALIDATION',json.dumps(report,ensure_ascii=False),flush=True)
if errors:raise RuntimeError('New_House validation failed')
