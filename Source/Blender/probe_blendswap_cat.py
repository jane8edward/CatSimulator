import bpy
import json
rig=bpy.data.objects['Armature']
rig.data.pose_position='REST'
cat=bpy.data.objects['Cat']
for ps in cat.particle_systems:
    ps.use_hair_dynamics=False
    ps.settings.child_percent=8
bpy.context.scene.frame_set(1)
bpy.context.view_layer.update()
deps=bpy.context.evaluated_depsgraph_get()
obj=cat.evaluated_get(deps)
for ps in obj.particle_systems:
    print('HAIR',ps.name,len(ps.particles),len(ps.child_particles),ps.settings.display_step)
    for i in [0,1,1000,1001]:
        print('PATH',i,[list(ps.co_hair(obj,particle_no=i,step=k)) for k in [0,1,2,4,8,16,32]])
for name in ['skin','hair','PBR Dielectric']:
    m=bpy.data.materials[name]
    print('MAT',name)
    print('LINKS',[(l.from_node.name,l.from_socket.name,l.to_node.name,l.to_socket.name) for l in m.node_tree.links])
    for n in m.node_tree.nodes:
        if n.type in ['MAPPING','HUE_SAT','TEX_IMAGE']:
            print('INPUTS',n.name,[(x.name, str(x.default_value)) for x in n.inputs if hasattr(x,'default_value')])
            if n.type=='TEX_IMAGE':print('Projection',n.projection)
print('REST_BOUNDS',list(obj.dimensions))
