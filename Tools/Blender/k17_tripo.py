"""
Personajele generate in Tripo (Tools/Tripo/Source/<Nume>.zip) -> personaje de joc K17.

Pentru fiecare arhiva:
  - importa FBX-ul si texturile, intoarce modelul cu fata spre +Z si il aduce la 1,80 m;
  - masoara modelul (mainile, umerii, picioarele) si potriveste scheletul K17 pe el, cu aceleasi nume de oase,
    ca animatiile, hitbox-urile, IK-ul si ragdoll-ul sa mearga ca la personajele facute din cod;
  - reduce mesh-ul la buget (LOD0 / LOD1 / LOD2), il leaga de schelet, curata greutatile bratelor de pe corp;
  - exporta FBX-ul, mainile FP, texturile (culoare, normale, metalic + luciu) si randari de control.

Rulare (din radacina repo-ului):
    D:/Blender/blender.exe -b --factory-startup --python Tools/Blender/k17_tripo.py [-- CT_Operator T_Insurgent_A]
"""

import math
import os
import shutil
import sys
import tempfile
import zipfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bmesh  # noqa: E402
import bpy  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

import k17_characters as kc  # noqa: E402
from k17_common import (REPO, B, activate, apply_modifier, export_fbx, link, reset_scene,  # noqa: E402
                        render_view, triangle_count, workbench_scene)

SOURCE = os.path.join(REPO, "Tools", "Tripo", "Source")
CHARACTERS_OUT = os.path.join(REPO, "Assets", "_Project", "Art", "Characters")
DOCS = os.path.join(REPO, "Docs", "Characters")

HEIGHT = 1.80
LOD_TRIANGLES = (22000, 11000, 4400)      # LOD0 sub bugetul de 25k; LOD1 ~50%, LOD2 ~20%
V = Vector
BASE_LAYOUT = kc.skeleton_layout      # scheletul K17 original (build_armature il cere prin kc.skeleton_layout)


# ----------------------------------------------------------------------------------------------
# Import si normalizare
# ----------------------------------------------------------------------------------------------

def import_source(name, work):
    """Despacheteaza arhiva, importa FBX-ul; intoarce mesh-ul (unul singur) si folderul cu texturi."""
    with zipfile.ZipFile(os.path.join(SOURCE, f"{name}.zip")) as archive:
        archive.extractall(work)

    fbx = [os.path.join(root, f) for root, _, files in os.walk(work) for f in files if f.lower().endswith(".fbx")][0]
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=fbx)
    meshes = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    for o in bpy.data.objects:
        if o not in before and o.type != "MESH":
            bpy.data.objects.remove(o)

    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    body = meshes[0]
    body.parent = None

    # Tripo priveste spre -Y (Blender) si are ~1 m; personajele K17 privesc spre +Y (= +Z in Unity) si au 1,80 m
    activate(body)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    lowest = min(v.co.z for v in body.data.vertices)
    tallest = max(v.co.z for v in body.data.vertices)
    scale = HEIGHT / (tallest - lowest)
    body.data.transform(Matrix.Translation((0.0, 0.0, -lowest)))
    body.data.transform(Matrix.Rotation(math.pi, 4, "Z") @ Matrix.Scale(scale, 4))
    xs = [v.co.x for v in body.data.vertices]
    ys = [v.co.y for v in body.data.vertices]
    body.data.transform(Matrix.Translation((-(min(xs) + max(xs)) * 0.5, -(min(ys) + max(ys)) * 0.5, 0.0)))
    body.name = f"{name}_LOD0"
    body.data.name = body.name

    texture_dir = os.path.dirname(fbx)
    for root, dirs, _ in os.walk(work):
        for d in dirs:
            if d.lower().endswith(".fbm"):
                texture_dir = os.path.join(root, d)
    return body, texture_dir


def character_points(body):
    """Vertecsii in coordonate de personaj: x dreapta, y sus, z inainte."""
    co = np.array([v.co[:] for v in body.data.vertices])
    return np.stack([co[:, 0], co[:, 2], co[:, 1]], axis=1)


# ----------------------------------------------------------------------------------------------
# Masuratori si scheletul potrivit pe model
# ----------------------------------------------------------------------------------------------

def measure(points):
    """Puncte de reper, mediate stanga / dreapta (modelele sunt simetrice in A-pose)."""
    P = points
    hands = []
    for s in (1, -1):
        arm = P[(P[:, 0] * s > 0.30) & (P[:, 1] > 0.55) & (P[:, 1] < 1.45)]
        tip = arm[arm[:, 1].argmin()]
        hand = arm[arm[:, 1] < tip[1] + 0.20].mean(axis=0)
        hands.append(np.array([hand[0] * s, hand[1], hand[2]]))
    hand = np.mean(hands, axis=0)

    shoulder_slice = P[np.abs(P[:, 1] - 1.40) < 0.006]
    shoulder_half = (shoulder_slice[:, 0].max() - shoulder_slice[:, 0].min()) * 0.5

    def leg_mid(y):
        values = []
        for s in (1, -1):
            sl = P[(np.abs(P[:, 1] - y) < 0.006) & (P[:, 0] * s > 0.0) & (P[:, 0] * s < 0.24)]
            values.append((sl[:, 0].min() + sl[:, 0].max()) * 0.5 * s)
        return float(np.mean(values))

    toes = []
    for s in (1, -1):
        foot = P[(P[:, 1] < 0.06) & (P[:, 0] * s > 0.0)]
        toes.append(foot[:, 2].max())

    return {
        "hand": hand,
        "shoulder_x": float(shoulder_half - 0.09),
        "knee_x": leg_mid(0.50),
        "ankle_x": leg_mid(0.15),
        "toe_z": float(np.mean(toes)),
    }


def fitted_layout(fit):
    """Scheletul K17 (aceleasi oase si parinti) cu bratele si picioarele mutate pe model."""
    base = {b[0]: b for b in BASE_LAYOUT()}
    bones = []
    for name in ("Root", "Hips", "Spine", "Chest", "UpperChest", "Neck", "Head"):
        bones.append(base[name])

    hand_center = V(fit["hand"].tolist())
    grips = {}
    for side, prefix in ((1, "Right"), (-1, "Left")):
        mirror = V((side, 1.0, 1.0))
        upper = V((side * fit["shoulder_x"], 1.43, -0.05))
        hc = V((hand_center.x * mirror.x, hand_center.y, hand_center.z))
        wrist = hc + (upper - hc).normalized() * 0.085
        d = (wrist - upper).normalized()
        elbow = upper + (wrist - upper) * 0.53 + V((0.0, 0.0, -0.012))
        hand_end = wrist + d * 0.09
        shoulder = V((side * 0.03, 1.46, 0.0))
        bones += [
            (f"{prefix}Shoulder", "UpperChest", shoulder, upper, True),
            (f"{prefix}UpperArm", f"{prefix}Shoulder", upper, elbow, True),
            (f"{prefix}LowerArm", f"{prefix}UpperArm", elbow, wrist, True),
            (f"{prefix}Hand", f"{prefix}LowerArm", wrist, hand_end, True),
        ]
        for finger, dz, lengths in (("Index", 0.030, (0.045, 0.028, 0.022)), ("Middle", 0.010, (0.050, 0.032, 0.024)),
                                    ("Ring", -0.010, (0.046, 0.030, 0.022)), ("Little", -0.030, (0.036, 0.022, 0.020))):
            p = hand_end + V((0, 0, dz)) - d * (0.004 if finger in ("Index", "Little") else 0.0)
            parent = f"{prefix}Hand"
            for phalanx, length in zip(("Proximal", "Intermediate", "Distal"), lengths):
                q = p + d * length
                bones.append((f"{prefix}{finger}{phalanx}", parent, p, q, True))
                parent, p = f"{prefix}{finger}{phalanx}", q
        thumb_dir = (d * 0.55 + V((0, -0.25, 0.75))).normalized()
        p = wrist + d * 0.025 + V((0, -0.01, 0.025))
        parent = f"{prefix}Hand"
        for phalanx, length in zip(("Proximal", "Intermediate", "Distal"), (0.040, 0.033, 0.028)):
            q = p + thumb_dir * length
            bones.append((f"{prefix}Thumb{phalanx}", parent, p, q, True))
            parent, p = f"{prefix}Thumb{phalanx}", q
        grips[side] = wrist + d * 0.05 + V((0, -0.02, 0.0))

        hip = V((side * 0.095, 0.93, 0.0))
        knee = V((side * fit["knee_x"], 0.51, 0.015))
        ankle = V((side * fit["ankle_x"], 0.10, -0.03))
        ball = V((side * (fit["ankle_x"] + 0.005), 0.025, fit["toe_z"] - 0.08))
        toe = V((side * (fit["ankle_x"] + 0.005), 0.020, fit["toe_z"] - 0.01))
        bones += [
            (f"{prefix}UpperLeg", "Hips", hip, knee, True),
            (f"{prefix}LowerLeg", f"{prefix}UpperLeg", knee, ankle, True),
            (f"{prefix}Foot", f"{prefix}LowerLeg", ankle, ball, True),
            (f"{prefix}Toes", f"{prefix}Foot", ball, toe, True),
        ]

    bones += [
        ("weapon_R", "RightHand", grips[1], grips[1] + V((0, 0, 0.12)), False),
        ("weapon_L_IK", "weapon_R", grips[-1], grips[-1] + V((0, 0, 0.12)), False),
    ]
    return bones


# ----------------------------------------------------------------------------------------------
# Texturi: culoare, normale, metalic (R) + luciu (A) pentru URP
# ----------------------------------------------------------------------------------------------

def classify_textures(texture_dir):
    """Rolul fiecarei imagini: intai dupa numele dat de Tripo (_0_0 = culoare, roughness, metallic), apoi dupa
    continut (albastru dominant = normale, gri inchis = metalic, gri = luciu). FBX-ul nu leaga mereu corect hartile."""
    roles = {}
    for f in sorted(os.listdir(texture_dir)):
        if not f.lower().endswith((".jpg", ".jpeg", ".png")):
            continue
        img = bpy.data.images.load(os.path.join(texture_dir, f))
        lower = f.lower()
        px = np.array(img.pixels[:]).reshape(-1, 4)[::97, :3]
        mean, spread = px.mean(axis=0), np.abs(px[:, 0] - px[:, 2]).mean()
        if "rough" in lower:
            role = "roughness"
        elif "metal" in lower:
            role = "metallic"
        elif mean[2] > 0.85 and mean[0] < 0.65:
            role = "normal"
        elif lower.endswith("_0_0.jpg") or lower.endswith("_0_0.png"):
            role = "base"
        elif spread < 0.01 and mean[0] < 0.15:
            role = "metallic"
        elif spread < 0.01:
            role = "roughness"
        else:
            role = "base"
        roles.setdefault(role, img)
    return roles


def write_textures(name, roles):
    out = os.path.join(CHARACTERS_OUT, name, "Textures")
    os.makedirs(out, exist_ok=True)
    for role, suffix in (("base", "BaseColor"), ("normal", "Normal")):
        if role in roles:
            shutil.copyfile(bpy.path.abspath(roles[role].filepath), os.path.join(out, f"{name}_{suffix}.jpg"))

    size = roles["base"].size[:]
    count = size[0] * size[1]

    def channel(role, default):
        img = roles.get(role)
        if img is None:
            return np.full(count, default, dtype=np.float32)
        if tuple(img.size) != tuple(size):
            img.scale(*size)
        return np.array(img.pixels[:], dtype=np.float32).reshape(-1, 4)[:, 0]

    metal = channel("metallic", 0.0)
    smooth = 1.0 - channel("roughness", 0.75)
    packed = np.zeros((count, 4), dtype=np.float32)
    packed[:, 0] = metal
    packed[:, 3] = smooth
    image = bpy.data.images.new(f"{name}_MetallicSmoothness", size[0], size[1], alpha=True)
    image.pixels.foreach_set(packed.ravel())
    image.filepath_raw = os.path.join(out, f"{name}_MetallicSmoothness.png")
    image.file_format = "PNG"
    image.save()
    return out


def setup_material(body, name, roles):
    """Un singur material cu numele <Nume>_Body; in Unity il face CharacterPrefabBuilder din texturi."""
    mat = bpy.data.materials.new(f"{name}_Body")
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = nodes["Principled BSDF"]
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = roles["base"]
    links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    body.data.materials.clear()
    body.data.materials.append(mat)


# ----------------------------------------------------------------------------------------------
# Skinning
# ----------------------------------------------------------------------------------------------

PROXY_VOXEL = 0.012


def skin_via_proxy(body, arm):
    """
    Mesh-urile Tripo nu sunt mereu inchise (fete suprapuse, margini libere), iar bone heat esueaza pe ele.
    Greutatile se calculeaza pe o copie refacuta din voxeli (inchisa, curata) si se transfera pe model
    de pe fata cea mai apropiata, interpolat.
    """
    proxy = body.copy()
    proxy.data = body.data.copy()
    proxy.name = "K17_SkinProxy"
    link(proxy)
    remesh = proxy.modifiers.new("Remesh", "REMESH")
    remesh.mode = "VOXEL"
    remesh.voxel_size = PROXY_VOXEL
    apply_modifier(proxy, remesh)
    tris = triangle_count(proxy)
    if tris > 30000:
        decimate = proxy.modifiers.new("Decimate", "DECIMATE")
        decimate.ratio = 30000 / tris
        apply_modifier(proxy, decimate)
    kc.auto_skin(proxy, arm)
    kc.fix_unweighted(proxy, arm)

    for group in proxy.vertex_groups:
        if group.name not in body.vertex_groups:
            body.vertex_groups.new(name=group.name)
    transfer = body.modifiers.new("Weights", "DATA_TRANSFER")
    transfer.object = proxy
    transfer.use_vert_data = True
    transfer.data_types_verts = {"VGROUP_WEIGHTS"}
    transfer.vert_mapping = "POLYINTERP_NEAREST"
    transfer.layers_vgroup_select_src = "ALL"
    transfer.layers_vgroup_select_dst = "NAME"
    apply_modifier(body, transfer)
    bpy.data.objects.remove(proxy)

    body.parent = arm
    modifier = body.modifiers.new("Armature", "ARMATURE")
    modifier.object = arm


# ----------------------------------------------------------------------------------------------
# Personajul
# ----------------------------------------------------------------------------------------------

def build(name):
    reset_scene()
    work = tempfile.mkdtemp(prefix=f"k17_tripo_{name}_")
    body, texture_dir = import_source(name, work)
    roles = classify_textures(texture_dir)
    fit = measure(character_points(body))
    print(f"TRIPO {name}: maini {np.round(fit['hand'], 3)} umar x {fit['shoulder_x']:.3f} "
          f"genunchi x {fit['knee_x']:.3f} glezna x {fit['ankle_x']:.3f} varf {fit['toe_z']:.3f}, texturi {sorted(roles)}")

    # Buget: LOD0 inainte de skinning (bone heat e mult mai stabil pe un mesh redus)
    tris = triangle_count(body)
    if tris > LOD_TRIANGLES[0]:
        decimate = body.modifiers.new("Decimate", "DECIMATE")
        decimate.ratio = LOD_TRIANGLES[0] / tris
        apply_modifier(body, decimate)

    kc.skeleton_layout = lambda: fitted_layout(fit)
    arm = kc.build_armature()
    kc.skeleton_layout = BASE_LAYOUT

    setup_material(body, name, roles)
    skin_via_proxy(body, arm)
    # Sub umeri, tot ce e mai aproape de trunchi decat axa bratului (minus grosimea bratului) nu are ce cauta pe
    # oasele bratelor: altfel poalele unei geci deschise sau buzunarele vestei se intind dupa brat.
    upper = arm.data.bones["RightUpperArm"].head_local
    wrist = arm.data.bones["RightHand"].head_local

    def torso_side(x, y):
        if y > upper.z - 0.10:
            return False
        t = min(1.0, max(0.0, (upper.z - y) / (upper.z - wrist.z)))
        axis_x = upper.x + (wrist.x - upper.x) * t
        return abs(x) < axis_x - 0.075

    kc.strip_arm_weights(body, arm, torso_side)
    fixed = kc.fix_unweighted(body, arm)
    activate(body)
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL", lock_active=False)

    lods = [body]
    tris0 = triangle_count(body)
    for level, budget in ((1, LOD_TRIANGLES[1]), (2, LOD_TRIANGLES[2])):
        lod = body.copy()
        lod.data = body.data.copy()
        lod.name = f"{name}_LOD{level}"
        lod.data.name = lod.name
        link(lod)
        decimate = lod.modifiers.new("Decimate", "DECIMATE")
        decimate.ratio = min(1.0, budget / tris0)
        apply_modifier(lod, decimate)
        lods.append(lod)

    kc.export_fp_arms(name, body, arm)
    write_textures(name, roles)
    unweighted = sum(1 for v in body.data.vertices if not any(g.weight > 0.001 for g in v.groups))
    print(f"CHARACTER {name}: LOD0 {triangle_count(lods[0])} / LOD1 {triangle_count(lods[1])} / LOD2 {triangle_count(lods[2])} "
          f"triunghiuri, {fixed} vertecsi reparati, {unweighted} fara greutate")

    path = os.path.join(CHARACTERS_OUT, name, f"{name}.fbx")
    export_fbx(path, [arm] + lods)
    print("FBX", os.path.relpath(path, REPO))

    for lod in lods[1:]:
        lod.hide_render = True
    cam = workbench_scene()
    bpy.context.scene.display.shading.color_type = "TEXTURE"
    docs = os.path.join(DOCS, name)
    render_view(cam, (0.9, 1.05, 3.6), (0, 0.92, 0), 40, os.path.join(docs, f"{name}_Front.png"))
    render_view(cam, (-0.9, 1.05, -3.6), (0, 0.92, 0), 40, os.path.join(docs, f"{name}_Back.png"))
    render_view(cam, (3.7, 1.0, 0.3), (0, 0.92, 0), 40, os.path.join(docs, f"{name}_Side.png"))
    render_view(cam, (0.5, 1.75, 1.3), (0, 1.55, 0), 60, os.path.join(docs, f"{name}_Head.png"))
    kc.deform_pose(arm)
    render_view(cam, (1.9, 1.3, 2.9), (0, 0.72, 0.1), 38, os.path.join(docs, f"{name}_Deform.png"))
    kc.reset_pose(arm)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(REPO, "Tools", "Blender", f"K17_{name}.blend"), compress=True)
    shutil.rmtree(work, ignore_errors=True)


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    available = sorted(os.path.splitext(f)[0] for f in os.listdir(SOURCE) if f.lower().endswith(".zip"))
    for name in (args or available):
        build(name)
    print("TRIPO_DONE")


if __name__ == "__main__":
    main()
