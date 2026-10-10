"""Reusable furniture layout, applied before exporting the living room."""
import bpy
from mathutils import Vector

def apply_room_layout(room_collection):
    objects = room_collection.all_objects
    objects['Sofa'].location.y = 1.02
    objects['COL_Sofa'].location.y = .98
    objects['COL_SofaBack'].location.y = 1.37
    objects['CoffeeTable'].location.y = -.90
    objects['COL_TableTop'].location.y = -.90
    objects['Cup'].location.y = -.63
    objects['CupActionPosition'].location.y = -1.23
    objects['SpawnPoint'].location = Vector((.65, -.8, .04))
    bpy.context.view_layer.update()

