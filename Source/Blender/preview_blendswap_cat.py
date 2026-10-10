import bpy
from pathlib import Path
from mathutils import Vector

ROOT = Path('E:/Game/game_develop/CatSimulator')
OUT = ROOT / 'Source/Blender/BlendSwap18519'
ART = ROOT / 'Game/Assets/Art/BlendSwap18519'
OUT.mkdir(parents=True, exist_ok=True)
(ART / 'Textures').mkdir(parents=True, exist_ok=True)
for name, filename in [('Untitled', 'CatCoat.png'), ('11967-diffuse.jpg.001', 'EyeOriginal.jpg'),
                       ('green_eye_diff.jpg.001', 'EyeAlternative.jpg')]:
    img = bpy.data.images[name]
    (ART / 'Textures' / filename).write_bytes(img.packed_file.data)
rig = bpy.data.objects['Armature']
for track in rig.animation_data.nla_tracks:
    track.mute = True
rig.animation_data.action = bpy.data.actions['Walk']
cat = bpy.data.objects['Cat']
for ps in cat.particle_systems:
    ps.use_hair_dynamics = False
    ps.settings.child_percent = 20
    ps.settings.rendered_child_count = 40
bpy.context.scene.frame_set(1)
scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.samples = 24
scene.cycles.use_denoising = True
try:
    prefs = bpy.context.preferences.addons['cycles'].preferences
    prefs.compute_device_type = 'CUDA'
    prefs.get_devices()
    for device in prefs.devices:
        device.use = device.type == 'CUDA'
    if any(device.use for device in prefs.devices):
        scene.cycles.device = 'GPU'
except Exception:
    scene.cycles.device = 'CPU'
scene.render.resolution_x = 1000
scene.render.resolution_y = 800
scene.render.resolution_percentage = 100
scene.render.image_settings.media_type = 'IMAGE'
scene.render.image_settings.file_format = 'PNG'
scene.use_nodes = False
scene.view_settings.view_transform = 'AgX'
camera = bpy.data.objects['Camera']
camera.location = (14, -19, 8)
target = Vector((0, -.3, 1))
camera.rotation_euler = (target - camera.location).to_track_quat('-Z', 'Y').to_euler()
camera.data.type = 'ORTHO'
camera.data.ortho_scale = 14
scene.camera = camera
for name, loc, energy, size in [('PreviewKey', (3, -8, 10), 1800, 8),
                                 ('PreviewFill', (-8, -4, 4), 1200, 6)]:
    data = bpy.data.lights.new(name, 'AREA')
    data.energy = energy
    data.shape = 'DISK'
    data.size = size
    obj = bpy.data.objects.new(name, data)
    scene.collection.objects.link(obj)
    obj.location = loc
    obj.rotation_euler = (target - obj.location).to_track_quat('-Z', 'Y').to_euler()
scene.render.filepath = str(OUT / 'original-preview.png')
bpy.ops.render.render(write_still=True)
print('PREVIEW_COMPLETE', scene.render.filepath)
