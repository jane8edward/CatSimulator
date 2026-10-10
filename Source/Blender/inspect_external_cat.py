"""Inventory a third-party cat before conversion, without editing its blend file.

Run with Blender --background --disable-autoexec INPUT.blend
    --python-exit-code 1 --python inspect_external_cat.py -- OUTPUT.json
The JSON is also useful when an older rig needs to be baked for Unity.
"""
import hashlib
import json
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def vector(value):
    return [round(float(v), 6) for v in value]


def action_curves(action):
    if hasattr(action, "fcurves"):
        return list(action.fcurves)
    curves = []
    for layer in action.layers:
        for strip in layer.strips:
            for bag in getattr(strip, "channelbags", []):
                curves.extend(bag.fcurves)
    return curves


def object_info(obj):
    result = {
        "name": obj.name, "type": obj.type,
        "parent": obj.parent.name if obj.parent else None,
        "parent_bone": obj.parent_bone,
        "location": vector(obj.location), "rotation": vector(obj.rotation_euler),
        "scale": vector(obj.scale), "dimensions": vector(obj.dimensions),
        "hidden_render": obj.hide_render,
        "modifiers": [{"name": m.name, "type": m.type,
                       "viewport": m.show_viewport, "render": m.show_render,
                       "target": getattr(getattr(m, "object", None), "name", None)}
                      for m in obj.modifiers],
    }
    if obj.type == "MESH":
        result.update({
            "vertices": len(obj.data.vertices), "polygons": len(obj.data.polygons),
            "triangles": sum(max(0, len(p.vertices) - 2) for p in obj.data.polygons),
            "uv_layers": [uv.name for uv in obj.data.uv_layers],
            "vertex_groups": [g.name for g in obj.vertex_groups],
            "materials": [m.name if m else None for m in obj.data.materials],
            "world_bounds": [vector(obj.matrix_world @ Vector(c)) for c in obj.bound_box],
            "shape_keys": [k.name for k in obj.data.shape_keys.key_blocks]
                          if obj.data.shape_keys else [],
            "particles": [],
        })
        for system in getattr(obj, "particle_systems", []):
            settings = system.settings
            result["particles"].append({
                "name": system.name, "type": settings.type,
                "count": settings.count, "hair_length": settings.hair_length,
                "hair_step": settings.hair_step,
                "child_type": settings.child_type,
                "viewport_children": settings.child_percent,
                "render_children": settings.rendered_child_count,
                "material_slot": settings.material,
                "dynamic": system.use_hair_dynamics,
            })
    if obj.type == "ARMATURE":
        result["bones"] = [{
            "name": bone.name, "parent": bone.parent.name if bone.parent else None,
            "deform": bone.use_deform, "head": vector(bone.head_local),
            "tail": vector(bone.tail_local),
            "constraints": [{"name": c.name, "type": c.type,
                             "target": getattr(getattr(c, "target", None), "name", None),
                             "subtarget": getattr(c, "subtarget", None)}
                            for c in obj.pose.bones[bone.name].constraints],
        } for bone in obj.data.bones]
    animation = obj.animation_data
    if animation:
        result["animation"] = {
            "action": animation.action.name if animation.action else None,
            "nla": [{"name": t.name, "muted": t.mute,
                     "strips": [{"name": s.name,
                                 "action": s.action.name if s.action else None,
                                 "range": [s.frame_start, s.frame_end]}
                                for s in t.strips]} for t in animation.nla_tracks],
            "drivers": [{"path": d.data_path, "index": d.array_index,
                         "type": d.driver.type, "expression": d.driver.expression}
                        for d in animation.drivers],
        }
    return result


arguments = sys.argv[sys.argv.index("--") + 1:]
output = Path(arguments[0])
source = Path(bpy.data.filepath)
report = {
    "source": str(source), "source_sha256": hashlib.sha256(source.read_bytes()).hexdigest(),
    "blender_version": bpy.app.version_string,
    "autoexec_disabled": not bpy.context.preferences.filepaths.use_scripts_auto_execute,
    "scenes": [{"name": s.name, "frame_range": [s.frame_start, s.frame_end],
                "fps": s.render.fps / s.render.fps_base,
                "unit_scale": s.unit_settings.scale_length} for s in bpy.data.scenes],
    "objects": [object_info(o) for o in bpy.data.objects],
    "actions": [{"name": a.name, "range": vector(a.frame_range),
                 "channels": [{"path": c.data_path, "index": c.array_index,
                               "keys": len(c.keyframe_points)} for c in action_curves(a)]}
                for a in bpy.data.actions],
    "images": [{"name": i.name, "source": i.source, "path": i.filepath,
                "resolved_path": bpy.path.abspath(i.filepath),
                "exists": Path(bpy.path.abspath(i.filepath)).is_file(),
                "packed": bool(i.packed_file), "size": list(i.size)} for i in bpy.data.images],
    "materials": [{"name": m.name, "color": vector(m.diffuse_color),
                   "nodes": [{"name": n.name, "type": n.bl_idname,
                              "image": getattr(getattr(n, "image", None), "name", None)}
                             for n in m.node_tree.nodes] if m.use_nodes else []}
                  for m in bpy.data.materials],
    "embedded_texts": [{"name": t.name, "lines": len(t.lines)} for t in bpy.data.texts],
}
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
print("CAT_INVENTORY:", output)
print("Objects:", len(report["objects"]), "Actions:", [a["name"] for a in report["actions"]])
