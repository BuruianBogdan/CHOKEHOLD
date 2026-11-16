"""
Blacksite K-17 / CHOKEHOLD - utilitare comune pentru scripturile Blender ale personajelor si armelor.

Conventia personajelor (diferita de k17_kit.py, care exporta piese statice):
    - coordonatele din scripturi sunt "de personaj": x = dreapta personajului, y = sus, z = inainte (metri);
    - in Blender personajul priveste spre +Y, cu Z in sus: B(x, y, z) -> (x, z, y), ca la k17_kit.py;
    - exportul FBX e cel standard pentru Unity (Forward -Z, Up Y, fara "Apply Transform"), iar Unity
      importa cu "Bake Axis Conversion": personajul priveste spre +Z, dreapta lui e +X, Y in sus.
"""

import math
import os

import bmesh
import bpy
from mathutils import Vector

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))


def B(x, y, z):
    """Punct "de personaj" (x dreapta, y sus, z inainte) -> coordonate Blender (Unity face aceeasi conversie la import)."""
    return Vector((x, z, y))


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    return scene


def material(name, color, roughness=0.6, metallic=0.0):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
        mat.diffuse_color = (color[0], color[1], color[2], 1.0)
        mat.roughness = roughness
        mat.metallic = metallic
    return mat


def link(obj):
    bpy.context.scene.collection.objects.link(obj)
    return obj


def activate(obj):
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def apply_modifier(obj, modifier):
    activate(obj)
    bpy.ops.object.modifier_apply(modifier=modifier.name)


def triangle_count(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


# ----------------------------------------------------------------------------------------------
# Geometrie: "loft" - inele eliptice de-a lungul unei linii (membre, trunchi), inchise la capete
# ----------------------------------------------------------------------------------------------

def ring_frame(tangent, prefer_front=Vector((0.0, 0.0, 1.0))):
    """Doua axe perpendiculare pe tangent: 'side' (latime) si 'front' (adancime), in coordonate de personaj."""
    t = tangent.normalized()
    front = prefer_front
    if abs(t.dot(front)) > 0.95:
        front = Vector((0.0, 1.0, 0.0))
    side = t.cross(front).normalized()
    front = side.cross(t).normalized()
    return side, front


def loft(bm, sections, segments=16, power=2.0, prefer_front=Vector((0.0, 0.0, 1.0)), cap=True):
    """
    sections: [(centru(Vector personaj), raza_lat, raza_adancime)] in ordine.
    power: 2 = elipsa; > 2 = forma mai "patrata" (supereliptica), buna pentru trunchi.
    """
    rings = []
    count = len(sections)
    for i, (center, rw, rd) in enumerate(sections):
        if i == 0:
            tangent = sections[1][0] - center
        elif i == count - 1:
            tangent = center - sections[i - 1][0]
        else:
            tangent = sections[i + 1][0] - sections[i - 1][0]
        side, front = ring_frame(tangent, prefer_front)
        ring = []
        for k in range(segments):
            a = 2 * math.pi * k / segments
            c, s = math.cos(a), math.sin(a)
            # supereliptic: |c|^(2/p) * semn
            cx = math.copysign(abs(c) ** (2.0 / power), c)
            sy = math.copysign(abs(s) ** (2.0 / power), s)
            p = center + side * (cx * rw) + front * (sy * rd)
            ring.append(bm.verts.new(B(p.x, p.y, p.z)))
        rings.append(ring)

    for r0, r1 in zip(rings, rings[1:]):
        for k in range(segments):
            bm.faces.new((r0[k], r0[(k + 1) % segments], r1[(k + 1) % segments], r1[k]))
    if cap:
        bm.faces.new(rings[0])
        bm.faces.new(list(reversed(rings[-1])))
    return rings


def ellipsoid(bm, center, radii, segments=16, rings=10):
    """Elipsoid in coordonate de personaj: radii = (x, y, z)."""
    verts = []
    top = bm.verts.new(B(center.x, center.y + radii[1], center.z))
    bottom = bm.verts.new(B(center.x, center.y - radii[1], center.z))
    for i in range(1, rings):
        phi = math.pi * i / rings
        ring = []
        for k in range(segments):
            th = 2 * math.pi * k / segments
            x = center.x + radii[0] * math.sin(phi) * math.cos(th)
            y = center.y + radii[1] * math.cos(phi)
            z = center.z + radii[2] * math.sin(phi) * math.sin(th)
            ring.append(bm.verts.new(B(x, y, z)))
        verts.append(ring)
    for k in range(segments):
        bm.faces.new((top, verts[0][(k + 1) % segments], verts[0][k]))
        bm.faces.new((bottom, verts[-1][k], verts[-1][(k + 1) % segments]))
    for r0, r1 in zip(verts, verts[1:]):
        for k in range(segments):
            bm.faces.new((r0[k], r0[(k + 1) % segments], r1[(k + 1) % segments], r1[k]))


# ----------------------------------------------------------------------------------------------
# Export si randare
# ----------------------------------------------------------------------------------------------

def export_fbx(path, objects, with_armature=True, bake_anim=False):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"ARMATURE", "MESH", "EMPTY"} if with_armature else {"MESH", "EMPTY"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        bake_space_transform=False,     # cu armaturi e experimental; Unity coace conversia de axe la import
        axis_forward="-Z",
        axis_up="Y",
        mesh_smooth_type="FACE",
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        use_armature_deform_only=False,
        armature_nodetype="NULL",
        bake_anim=bake_anim,
        bake_anim_use_all_actions=False,
        bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True,
        path_mode="STRIP",
    )


def workbench_scene(resolution=(1600, 1000), background=(0.16, 0.17, 0.19)):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = "BOTH"
    scene.display.shading.show_shadows = True
    scene.display.shading.shadow_intensity = 0.5
    scene.display.render_aa = "16"
    scene.render.resolution_x, scene.render.resolution_y = resolution
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("K17_World")
    world.color = background
    scene.world = world

    cam_data = bpy.data.cameras.new("K17_Cam")
    cam = link(bpy.data.objects.new("K17_Cam", cam_data))
    scene.camera = cam
    return cam


def render_view(cam, eye, target, lens, path):
    cam.location = B(*eye)
    direction = B(*target) - cam.location
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = lens
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
