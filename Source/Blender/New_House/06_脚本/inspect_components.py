import bpy,numpy as np,json,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];ids=sys.argv[sys.argv.index('--')+1:]
for ident in ids:
 bpy.ops.wm.open_mainfile(filepath=str(ROOT/f'03_整理资产/{ident}/{ident}_Asset.blend'))
 o=next(o for o in bpy.context.scene.objects if o.type=='MESH');dg=bpy.context.evaluated_depsgraph_get();m=bpy.data.meshes.new_from_object(o.evaluated_get(dg));n=len(m.vertices)
 ed=np.empty(len(m.edges)*2,dtype=np.int32);m.edges.foreach_get('vertices',ed);ed=ed.reshape(-1,2);labels=np.arange(n,dtype=np.int32)
 for i in range(100):
  old=labels.copy();lo=np.minimum(labels[ed[:,0]],labels[ed[:,1]]);np.minimum.at(labels,ed[:,0],lo);np.minimum.at(labels,ed[:,1],lo);labels=labels[labels]
  if np.array_equal(labels,old):break
 co=np.empty(n*3,dtype=np.float32);m.vertices.foreach_get('co',co);co=co.reshape(-1,3)
 u,c=np.unique(labels,return_counts=True);parts=[]
 for label,count in sorted(zip(u,c),key=lambda x:-x[1])[:25]:
  a=co[labels==label];parts.append({'label':int(label),'vertices':int(count),'min':a.min(0).tolist(),'max':a.max(0).tolist(),'mean':a.mean(0).tolist()})
 report={'id':ident,'evaluatedVertices':n,'components':len(u),'iterations':i+1,'parts':parts};(ROOT/f'03_整理资产/{ident}/连通部分检查.json').write_text(json.dumps(report,indent=2));print('COMPONENTS',json.dumps(report),flush=True)
