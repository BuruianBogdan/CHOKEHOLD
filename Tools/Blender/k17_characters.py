"""
CHOKEHOLD - personaje (Faza 1: corp de baza si schelet).

Genereaza din cod:
    - scheletul humanoid comun (compatibil Unity Humanoid; oase extra: Root, weapon_R, weapon_L_IK);
    - corpul de baza (1,80 m, A-pose), un singur mesh inchis obtinut prin voxel remesh din volume anatomice,
      decimat la buget si legat de schelet (greutati automate, max. 4 oase / vertex);
    - testul de deformare (ghemuit + tinta in sus + rasucire) si randari de control in Docs/Characters.

Rulare (din radacina repo-ului):
    D:/Blender/blender.exe -b --factory-startup --python Tools/Blender/k17_characters.py

Conventii: vezi k17_common.py (coordonate "de personaj": x dreapta, y sus, z inainte).
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bmesh  # noqa: E402
import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

from k17_common import (REPO, B, activate, apply_modifier, ellipsoid, export_fbx, link, loft,  # noqa: E402
                        material, render_view, reset_scene, triangle_count, workbench_scene)

OUT_FBX = os.path.join(REPO, "Assets", "_Project", "Art", "Characters", "Base", "K17_BaseBody.fbx")
OUT_DOCS = os.path.join(REPO, "Docs", "Characters")

BODY_TRIANGLES = 10000      # corpul e aproape tot acoperit de haine; restul bugetului (25k) e pentru echipament

V = Vector

# ----------------------------------------------------------------------------------------------
# Scheletul (A-pose: bratele la 40 de grade sub orizontala)
# ----------------------------------------------------------------------------------------------

ARM_ANGLE = math.radians(40)


def arm_dir(side):
    return V((side * math.cos(ARM_ANGLE), -math.sin(ARM_ANGLE), 0.0))


def skeleton_layout():
    """Lista de oase: (nume, parinte, cap, coada, deformeaza) in coordonate de personaj."""
    bones = [
        ("Root", None, V((0, 0, 0)), V((0, 0, 0.2)), False),
        ("Hips", "Root", V((0, 0.98, 0)), V((0, 1.08, 0)), True),
        ("Spine", "Hips", V((0, 1.08, 0)), V((0, 1.20, 0.01)), True),
        ("Chest", "Spine", V((0, 1.20, 0.01)), V((0, 1.33, 0.01)), True),
        ("UpperChest", "Chest", V((0, 1.33, 0.01)), V((0, 1.48, 0.0)), True),
        ("Neck", "UpperChest", V((0, 1.50, 0.0)), V((0, 1.585, 0.015)), True),
        ("Head", "Neck", V((0, 1.585, 0.015)), V((0, 1.80, 0.015)), True),
    ]

    for side, prefix in ((1, "Right"), (-1, "Left")):
        d = arm_dir(side)
        shoulder = V((side * 0.03, 1.46, 0.0))
        upper = V((side * 0.17, 1.45, -0.01))
        elbow = upper + d * 0.29 + V((0, 0, -0.012))
        wrist = elbow + d * 0.26 + V((0, 0, 0.012))
        hand_end = wrist + d * 0.09
        bones += [
            (f"{prefix}Shoulder", "UpperChest", shoulder, upper, True),
            (f"{prefix}UpperArm", f"{prefix}Shoulder", upper, elbow, True),
            (f"{prefix}LowerArm", f"{prefix}UpperArm", elbow, wrist, True),
            (f"{prefix}Hand", f"{prefix}LowerArm", wrist, hand_end, True),
        ]

        # degete: 3 falange; latimea palmei e pe Z (fata/spate) in A-pose
        fingers = [
            ("Index", 0.030, (0.045, 0.028, 0.022)),
            ("Middle", 0.010, (0.050, 0.032, 0.024)),
            ("Ring", -0.010, (0.046, 0.030, 0.022)),
            ("Little", -0.030, (0.036, 0.022, 0.020)),
        ]
        for finger, dz, lengths in fingers:
            p = hand_end + V((0, 0, dz)) - d * (0.004 if finger in ("Index", "Little") else 0.0)
            parent = f"{prefix}Hand"
            for phalanx, length in zip(("Proximal", "Intermediate", "Distal"), lengths):
                q = p + d * length
                name = f"{prefix}{finger}{phalanx}"
                bones.append((name, parent, p, q, True))
                parent, p = name, q

        thumb_dir = (d * 0.55 + V((0, -0.25, 0.75))).normalized()
        p = wrist + d * 0.025 + V((0, -0.01, 0.025))
        parent = f"{prefix}Hand"
        for phalanx, length in zip(("Proximal", "Intermediate", "Distal"), (0.040, 0.033, 0.028)):
            q = p + thumb_dir * length
            name = f"{prefix}Thumb{phalanx}"
            bones.append((name, parent, p, q, True))
            parent, p = name, q

        hip = V((side * 0.095, 0.94, 0.0))
        knee = V((side * 0.100, 0.51, 0.015))
        ankle = V((side * 0.105, 0.09, -0.02))
        ball = V((side * 0.110, 0.025, 0.11))
        toe = V((side * 0.110, 0.020, 0.18))
        bones += [
            (f"{prefix}UpperLeg", "Hips", hip, knee, True),
            (f"{prefix}LowerLeg", f"{prefix}UpperLeg", knee, ankle, True),
            (f"{prefix}Foot", f"{prefix}LowerLeg", ankle, ball, True),
            (f"{prefix}Toes", f"{prefix}Foot", ball, toe, True),
        ]

    # Arma: socket in palma dreapta; tinta IK pentru mana stanga (copil al armei, ca sa o urmeze)
    right_wrist = [b for b in bones if b[0] == "RightHand"][0][2]
    left_wrist = [b for b in bones if b[0] == "LeftHand"][0][2]
    grip_r = right_wrist + arm_dir(1) * 0.05 + V((0, -0.02, 0.0))
    grip_l = left_wrist + arm_dir(-1) * 0.05 + V((0, -0.02, 0.0))
    bones += [
        ("weapon_R", "RightHand", grip_r, grip_r + V((0, 0, 0.12)), False),
        ("weapon_L_IK", "weapon_R", grip_l, grip_l + V((0, 0, 0.12)), False),
    ]
    return bones


def build_armature():
    data = bpy.data.armatures.new("K17_Skeleton")
    arm = link(bpy.data.objects.new("K17_Skeleton", data))
    data.display_type = "STICK"
    activate(arm)
    bpy.ops.object.mode_set(mode="EDIT")
    created = {}
    for name, parent, head, tail, deform in skeleton_layout():
        eb = data.edit_bones.new(name)
        eb.head = B(*head)
        eb.tail = B(*tail)
        eb.use_deform = deform
        eb.use_connect = False
        if parent:
            eb.parent = created[parent]
        created[name] = eb
    # rola: axa Z a fiecarui os spre "inainte" (Blender -Y), ca rotatiile sa fie previzibile
    for eb in data.edit_bones:
        eb.align_roll(B(0, 0, 1))
    bpy.ops.object.mode_set(mode="OBJECT")
    return arm


# ----------------------------------------------------------------------------------------------
# Corpul de baza
# ----------------------------------------------------------------------------------------------

def body_volumes(bm):
    seg = 18
    # trunchi (supereliptic, mai "patrat" la piept si bazin)
    torso = [
        (0.84, 0.140, 0.095, 0.000),
        (0.90, 0.170, 0.112, 0.000),
        (0.97, 0.168, 0.110, 0.008),
        (1.06, 0.150, 0.102, 0.012),
        (1.15, 0.158, 0.118, 0.018),
        (1.25, 0.176, 0.135, 0.026),
        (1.34, 0.186, 0.135, 0.020),
        (1.42, 0.180, 0.118, 0.006),
        (1.48, 0.140, 0.085, 0.000),
        (1.51, 0.075, 0.062, 0.000),
    ]
    loft(bm, [(V((0, y, z)), w, d) for y, w, d, z in torso], segments=24, power=2.2)

    # gat si cap
    loft(bm, [(V((0, 1.45, 0.0)), 0.058, 0.055), (V((0, 1.55, 0.008)), 0.055, 0.054),
              (V((0, 1.61, 0.02)), 0.056, 0.056)], segments=seg)
    ellipsoid(bm, V((0, 1.695, 0.012)), (0.080, 0.110, 0.098), segments=20, rings=12)
    ellipsoid(bm, V((0, 1.622, 0.036)), (0.060, 0.048, 0.066), segments=16, rings=8)     # maxilar
    ellipsoid(bm, V((0, 1.680, 0.100)), (0.012, 0.022, 0.016), segments=8, rings=6)      # nas
    ellipsoid(bm, V((0, 1.725, 0.080)), (0.060, 0.018, 0.025), segments=12, rings=6)     # arcade
    for s in (1, -1):
        ellipsoid(bm, V((s * 0.081, 1.688, -0.002)), (0.012, 0.030, 0.020), segments=8, rings=6)  # urechi

    for s in (1, -1):
        d = arm_dir(s)
        upper = V((s * 0.17, 1.45, -0.01))
        elbow = upper + d * 0.29 + V((0, 0, -0.012))
        wrist = elbow + d * 0.26 + V((0, 0, 0.012))
        knuckles = wrist + d * 0.09

        # umar (deltoid), trapez, fese
        ellipsoid(bm, V((s * 0.165, 1.425, -0.006)), (0.066, 0.062, 0.062), segments=16, rings=10)
        ellipsoid(bm, V((s * 0.080, 1.485, -0.012)), (0.075, 0.038, 0.055), segments=12, rings=8)
        ellipsoid(bm, V((s * 0.072, 0.915, -0.055)), (0.080, 0.085, 0.072), segments=16, rings=10)

        # brat
        loft(bm, [
            (upper - d * 0.03, 0.056, 0.056),
            (upper + d * 0.06, 0.054, 0.058),
            (upper + d * 0.15, 0.050, 0.054),
            (elbow - d * 0.03, 0.041, 0.043),
            (elbow + d * 0.00, 0.039, 0.041),
            (elbow + d * 0.06, 0.044, 0.042),
            (elbow + d * 0.14, 0.038, 0.034),
            (wrist - d * 0.01, 0.025, 0.031),
            (wrist + d * 0.01, 0.022, 0.032),
        ], segments=seg)

        # palma (lata pe Z, subtire pe "side")
        loft(bm, [
            (wrist + d * 0.005, 0.020, 0.032),
            (wrist + d * 0.045, 0.017, 0.042),
            (knuckles - d * 0.004, 0.014, 0.043),
            (knuckles + d * 0.006, 0.012, 0.040),
        ], segments=14)

        # degete
        for dz, lengths in ((0.030, (0.045, 0.028, 0.022)), (0.010, (0.050, 0.032, 0.024)),
                            (-0.010, (0.046, 0.030, 0.022)), (-0.030, (0.036, 0.022, 0.020))):
            p = knuckles + V((0, 0, dz))
            total = sum(lengths)
            loft(bm, [(p, 0.0095, 0.0095), (p + d * total * 0.5, 0.0088, 0.0088),
                      (p + d * (total - 0.004), 0.0078, 0.0078), (p + d * total, 0.004, 0.004)], segments=10)
        thumb_dir = (d * 0.55 + V((0, -0.25, 0.75))).normalized()
        tp = wrist + d * 0.025 + V((0, -0.01, 0.025))
        loft(bm, [(tp, 0.014, 0.016), (tp + thumb_dir * 0.05, 0.011, 0.011),
                  (tp + thumb_dir * 0.095, 0.0095, 0.0095), (tp + thumb_dir * 0.101, 0.005, 0.005)], segments=10)

        # picior
        loft(bm, [
            (V((s * 0.090, 0.95, 0.0)), 0.088, 0.090),
            (V((s * 0.095, 0.86, 0.0)), 0.085, 0.088),
            (V((s * 0.097, 0.72, 0.004)), 0.074, 0.078),
            (V((s * 0.099, 0.58, 0.010)), 0.058, 0.060),
            (V((s * 0.100, 0.51, 0.016)), 0.053, 0.056),
            (V((s * 0.101, 0.45, 0.004)), 0.052, 0.058),
            (V((s * 0.103, 0.34, -0.012)), 0.054, 0.062),
            (V((s * 0.104, 0.22, -0.018)), 0.040, 0.044),
            (V((s * 0.105, 0.11, -0.020)), 0.033, 0.038),
            (V((s * 0.105, 0.07, -0.020)), 0.035, 0.040),
        ], segments=seg)

        # laba piciorului (lata pe X, inalta pe Y)
        loft(bm, [
            (V((s * 0.105, 0.050, -0.065)), 0.032, 0.040),
            (V((s * 0.106, 0.052, -0.020)), 0.040, 0.046),
            (V((s * 0.108, 0.045, 0.050)), 0.045, 0.038),
            (V((s * 0.110, 0.030, 0.125)), 0.048, 0.028),
            (V((s * 0.110, 0.024, 0.175)), 0.040, 0.020),
            (V((s * 0.110, 0.022, 0.195)), 0.022, 0.012),
        ], segments=14, prefer_front=V((0.0, 1.0, 0.0)))


def build_body():
    bm = bmesh.new()
    body_volumes(bm)
    mesh = bpy.data.meshes.new("K17_BaseBody")
    bm.to_mesh(mesh)
    bm.free()
    body = link(bpy.data.objects.new("K17_BaseBody", mesh))
    mesh.materials.append(material("Skin", (0.62, 0.47, 0.40), roughness=0.55))

    # Un singur invelis inchis: voxel remesh peste volumele suprapuse, apoi netezire si decimare.
    remesh = body.modifiers.new("Remesh", "REMESH")
    remesh.mode = "VOXEL"
    remesh.voxel_size = 0.0045
    remesh.use_smooth_shade = True
    apply_modifier(body, remesh)

    smooth = body.modifiers.new("Smooth", "SMOOTH")
    smooth.factor = 0.8
    smooth.iterations = 8
    apply_modifier(body, smooth)

    current = triangle_count(body)
    decimate = body.modifiers.new("Decimate", "DECIMATE")
    decimate.ratio = min(1.0, BODY_TRIANGLES / max(current, 1))
    apply_modifier(body, decimate)

    for poly in body.data.polygons:
        poly.use_smooth = True

    # UV pentru atlasul de mai tarziu
    activate(body)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.quads_convert_to_tris()
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.004)
    bpy.ops.object.mode_set(mode="OBJECT")
    return body


def skin(body, arm):
    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")

    activate(body)
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL", lock_active=False)

    # verificare: fiecare vertex trebuie sa aiba cel putin o greutate
    unweighted = sum(1 for v in body.data.vertices if not any(g.weight > 0.001 for g in v.groups))
    return unweighted


# ----------------------------------------------------------------------------------------------
# Test de deformare
# ----------------------------------------------------------------------------------------------

def aim(arm, name, direction):
    """Roteste osul (in jurul capului lui) ca sa indice directia data (coordonate de personaj)."""
    bpy.context.view_layer.update()
    pb = arm.pose.bones[name]
    m = pb.matrix.copy()
    head = m.translation.copy()
    current = (pb.tail - pb.head).normalized()
    wanted = B(*direction).normalized()
    rot = current.rotation_difference(wanted).to_matrix().to_4x4()
    pb.matrix = Matrix.Translation(head) @ rot @ Matrix.Translation(-head) @ m


def turn(arm, name, axis, degrees):
    """Roteste osul in jurul unei axe (coordonate de personaj) care trece prin capul lui."""
    bpy.context.view_layer.update()
    pb = arm.pose.bones[name]
    m = pb.matrix.copy()
    head = m.translation.copy()
    rot = Matrix.Rotation(math.radians(degrees), 4, B(*axis).normalized())
    pb.matrix = Matrix.Translation(head) @ rot @ Matrix.Translation(-head) @ m


def move(arm, name, offset):
    bpy.context.view_layer.update()
    pb = arm.pose.bones[name]
    m = pb.matrix.copy()
    m.translation += B(*offset)
    pb.matrix = m


def deform_pose(arm):
    """Ghemuit adanc + tinta in sus + trunchiul rasucit: cel mai greu caz pentru umeri, solduri, genunchi."""
    move(arm, "Hips", (0, -0.42, -0.05))
    for s, prefix in ((1, "Right"), (-1, "Left")):
        aim(arm, f"{prefix}UpperLeg", (s * 0.30, -0.15, 1.0))
        aim(arm, f"{prefix}LowerLeg", (s * 0.05, -1.0, -0.55))
        aim(arm, f"{prefix}Foot", (s * 0.15, -0.25, 1.0))
    turn(arm, "Spine", (0, 1, 0), 14)
    turn(arm, "Chest", (0, 1, 0), 14)
    turn(arm, "UpperChest", (0, 1, 0), 12)
    turn(arm, "Spine", (1, 0, 0), 10)
    aim(arm, "RightUpperArm", (0.30, 0.55, 0.80))
    aim(arm, "RightLowerArm", (-0.25, 0.55, 0.85))
    aim(arm, "LeftUpperArm", (-0.35, 0.40, 0.85))
    aim(arm, "LeftLowerArm", (0.40, 0.65, 0.70))
    aim(arm, "Head", (0.25, 1.0, 0.55))
    bpy.context.view_layer.update()


def reset_pose(arm):
    for pb in arm.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()


# ----------------------------------------------------------------------------------------------
# Personaje complete: corp + echipament, un singur mesh pe LOD, acelasi schelet
# ----------------------------------------------------------------------------------------------

from k17_gear import Gear, build_ct_operator  # noqa: E402

CHARACTER_TRIANGLES = (25000, 12500, 5000)
CHARACTERS = {
    "CT_Operator": build_ct_operator,
}


def rigid_skin(obj, arm, bone):
    obj.parent = arm
    modifier = obj.modifiers.new("Armature", "ARMATURE")
    modifier.object = arm
    group = obj.vertex_groups.new(name=bone)
    group.add(list(range(len(obj.data.vertices))), 1.0, "REPLACE")


def auto_skin(obj, arm):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")


def fix_unweighted(obj, arm):
    """Vertecsii ramasi fara greutate (bone heat esueaza pe piese mici) merg pe cel mai apropiat os deformant."""
    bones = [(b.name, arm.matrix_world @ b.head_local, arm.matrix_world @ b.tail_local)
             for b in arm.data.bones if b.use_deform]
    groups = {g.name: g for g in obj.vertex_groups}
    fixed = 0
    for v in obj.data.vertices:
        if any(g.weight > 0.001 for g in v.groups):
            continue
        p = obj.matrix_world @ v.co
        best, best_d = None, 1e9
        for name, h, t in bones:
            d = t - h
            length = d.length_squared
            s = 0.0 if length == 0 else max(0.0, min(1.0, (p - h).dot(d) / length))
            dist = (p - (h + d * s)).length
            if dist < best_d:
                best, best_d = name, dist
        if best not in groups:
            groups[best] = obj.vertex_groups.new(name=best)
        groups[best].add([v.index], 1.0, "REPLACE")
        fixed += 1
    return fixed


def build_character(name, builder):
    reset_scene()
    arm = build_armature()
    body = build_body()
    gear = Gear(name)
    builder(gear)

    for obj, bone in gear.pieces:
        if bone:
            rigid_skin(obj, arm, bone)
        else:
            auto_skin(obj, arm)
    auto_skin(body, arm)

    # Un singur mesh: corpul + toate piesele (grupurile de vertecsi se pastreaza la join)
    bpy.ops.object.select_all(action="DESELECT")
    for obj, _ in gear.pieces:
        obj.select_set(True)
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
    body.name = f"{name}_LOD0"
    body.data.name = body.name
    fixed = fix_unweighted(body, arm)

    activate(body)
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL", lock_active=False)
    try:
        bpy.ops.object.shade_auto_smooth(angle=math.radians(40))
    except Exception:
        pass

    lods = [body]
    tris0 = triangle_count(body)
    if tris0 > CHARACTER_TRIANGLES[0]:
        decimate = body.modifiers.new("Decimate", "DECIMATE")
        decimate.ratio = CHARACTER_TRIANGLES[0] / tris0
        apply_modifier(body, decimate)
        tris0 = triangle_count(body)

    for level, budget in ((1, CHARACTER_TRIANGLES[1]), (2, CHARACTER_TRIANGLES[2])):
        lod = body.copy()
        lod.data = body.data.copy()
        lod.name = f"{name}_LOD{level}"
        lod.data.name = lod.name
        link(lod)
        decimate = lod.modifiers.new("Decimate", "DECIMATE")
        decimate.ratio = min(1.0, budget / tris0)
        apply_modifier(lod, decimate)
        lods.append(lod)

    unweighted = sum(1 for v in body.data.vertices if not any(g.weight > 0.001 for g in v.groups))
    print(f"CHARACTER {name}: LOD0 {triangle_count(lods[0])} / LOD1 {triangle_count(lods[1])} / LOD2 {triangle_count(lods[2])} "
          f"triunghiuri, {len(gear.pieces)} piese, {fixed} vertecsi reparati, {unweighted} fara greutate, "
          f"materiale: {', '.join(m.name for m in body.data.materials)}")

    path = os.path.join(REPO, "Assets", "_Project", "Art", "Characters", name, f"{name}.fbx")
    export_fbx(path, [arm] + lods)
    print("FBX", os.path.relpath(path, REPO))

    # Doar LOD0 in randari
    for lod in lods[1:]:
        lod.hide_render = True
    cam = workbench_scene()
    docs = os.path.join(OUT_DOCS, name)
    render_view(cam, (0.9, 1.05, 3.6), (0, 0.92, 0), 40, os.path.join(docs, f"{name}_Front.png"))
    render_view(cam, (-0.9, 1.05, -3.6), (0, 0.92, 0), 40, os.path.join(docs, f"{name}_Back.png"))
    render_view(cam, (3.7, 1.0, 0.3), (0, 0.92, 0), 40, os.path.join(docs, f"{name}_Side.png"))
    render_view(cam, (0.5, 1.75, 1.3), (0, 1.55, 0), 60, os.path.join(docs, f"{name}_Head.png"))
    deform_pose(arm)
    render_view(cam, (1.9, 1.3, 2.9), (0, 0.72, 0.1), 38, os.path.join(docs, f"{name}_Deform.png"))
    reset_pose(arm)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(REPO, "Tools", "Blender", f"K17_{name}.blend"), compress=True)


def build_base():
    reset_scene()
    arm = build_armature()
    body = build_body()
    unweighted = skin(body, arm)
    tris = triangle_count(body)
    print(f"BODY {tris} triunghiuri, {len(body.data.vertices)} vertecsi, {unweighted} fara greutate, "
          f"{len(arm.data.bones)} oase")

    export_fbx(OUT_FBX, [arm, body])
    print("FBX", os.path.relpath(OUT_FBX, REPO))

    cam = workbench_scene()
    render_view(cam, (1.6, 1.1, 3.6), (0, 0.92, 0), 38, os.path.join(OUT_DOCS, "BaseBody_Front.png"))
    render_view(cam, (3.9, 1.0, 0.0), (0, 0.92, 0), 38, os.path.join(OUT_DOCS, "BaseBody_Side.png"))
    deform_pose(arm)
    render_view(cam, (1.9, 1.3, 2.9), (0, 0.72, 0.1), 38, os.path.join(OUT_DOCS, "DeformTest.png"))
    render_view(cam, (-3.0, 0.9, 1.4), (0, 0.72, 0.1), 38, os.path.join(OUT_DOCS, "DeformTest_Side.png"))
    reset_pose(arm)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(REPO, "Tools", "Blender", "K17_Characters.blend"), compress=True)


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    wanted = [a for a in args if a in CHARACTERS]
    if not args or "base" in args:
        build_base()
    for name in (wanted or list(CHARACTERS)):
        build_character(name, CHARACTERS[name])
    print("CHARACTERS_DONE")


main()
