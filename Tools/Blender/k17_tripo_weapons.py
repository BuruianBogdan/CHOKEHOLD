"""
Armele generate in Tripo (Tools/Tripo/Source/Weapons/<Arhiva>.zip) -> armele de joc K17 (FP si TP).

Pentru fiecare arma din WEAPONS:
  - importa FBX-ul si texturile, intoarce arma cu teava spre +Y (Blender; +Z in Unity), o aduce la lungimea reala;
  - muta originea in palma dreapta (Grip_R), ca la armele facute din cod;
  - FP: reduce la buget, desprinde incarcatorul (si inchizatorul la pistol) ca piese animabile, deschide
    sticla opticilor red dot / holo (raze de-a lungul axei optice) si pune reticulul luminos;
  - TP: aceeasi arma, mult redusa, fara piese separate;
  - socket-uri: Muzzle, Eject, Sight, Grip_R, Grip_L (Grip_L lipseste la cutite);
  - texturi in Assets/_Project/Art/Weapons/<Id>/Textures, material <Id>_Body.

Coordonatele din tabel sunt citite pe randari la scara reala (k17 "grid"): y = inainte de la capatul din spate,
z = in sus de la cel mai jos punct, x = 0 pe mijlocul armei. Toate in metri.

Rulare:
    D:/Blender/blender.exe -b --factory-startup --python Tools/Blender/k17_tripo_weapons.py [-- AK_K17 ...]
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
from mathutils.bvhtree import BVHTree  # noqa: E402

from k17_common import REPO, apply_modifier, export_fbx, link, reset_scene, triangle_count  # noqa: E402

SOURCE = os.path.join(REPO, "Tools", "Tripo", "Source", "Weapons")
WEAPONS_OUT = os.path.join(REPO, "Assets", "_Project", "Art", "Weapons")
DOCS = os.path.join(REPO, "Docs", "Characters", "Weapons")

FP_TRIANGLES = 14000
TP_TRIANGLES = 4500
KNIFE_FP_TRIANGLES = 8000
KNIFE_TP_TRIANGLES = 2000

# id: arhiva, lungime reala, capatul din fata in Tripo (+X / -X), repere (y, z) in metri
WEAPONS = {
    "AK_K17": dict(
        source="AK_K17", length=0.88, front="-X",
        grip=(0.250, 0.136), grip_l=(0.5665, 0.144), muzzle=(0.880, 0.222), eject=(0.417, 0.236),
        sight=(0.319, 0.2967), optic=dict(kind="reddot", y=(0.322, 0.386), radius=0.016),
        magazine=dict(y=(0.368, 0.53), z_below=0.175)),
    "Galil_K": dict(
        source="Galil_K", length=0.90, front="-X",
        grip=(0.247, 0.135), grip_l=(0.567, 0.185), muzzle=(0.905, 0.2316), eject=(0.4265, 0.240),
        sight=(0.32, 0.3138), optic=dict(kind="reddot", y=(0.33, 0.395), radius=0.011),
        magazine=dict(y=(0.378, 0.54), z_below=0.185)),
    "M4A1_S": dict(
        source="M4A1_S", length=0.92, front="-X",
        grip=(0.253, 0.116), grip_l=(0.541, 0.197), muzzle=(0.92, 0.227), eject=(0.37, 0.224),
        sight=(0.34, 0.321), optic=dict(kind="holo", y=(0.342, 0.402), radius=0.014),
        magazine=dict(y=(0.36, 0.45), z_below=0.145)),
    "M4A4": dict(
        source="M4A4", length=0.84, front="-X",
        grip=(0.2385, 0.1134), grip_l=(0.541, 0.133), muzzle=(0.838, 0.2207), eject=(0.365, 0.21),
        sight=(0.244, 0.3114), optic=dict(kind="scope"),
        magazine=dict(y=(0.36, 0.45), z_below=0.13)),
    "AW_K17": dict(
        source="AW_K17", length=1.18, front="-X",
        grip=(0.2925, 0.1078), grip_l=(0.632, 0.12), muzzle=(1.177, 0.17), eject=(0.468, 0.165),
        sight=(0.281, 0.249), optic=dict(kind="scope"),
        magazine=dict(y=(0.405, 0.51), z_below=0.105)),
    "SSG_K": dict(
        source="SSG_K", length=1.06, front="+X",
        grip=(0.310, 0.097), grip_l=(0.599, 0.124), muzzle=(1.059, 0.1795), eject=(0.42, 0.176),
        sight=(0.2517, 0.2277), optic=dict(kind="scope"),
        magazine=dict(y=(0.375, 0.475), z_below=0.112)),
    "P17_K": dict(
        source="P17_K", length=0.19, front="-X",
        grip=(0.0318, 0.0546), grip_l=(0.045, 0.035, -0.022), muzzle=(0.1884, 0.1135), eject=(0.0993, 0.1207),
        sight=(0.0188, 0.134), optic=dict(kind="iron"),
        magazine=dict(y=(0.0, 0.07), z_below=0.013), slide=dict(z_above=0.1045)),
    "Knife_Combat": dict(
        source="Knife_Combat", length=0.30, front="-X", knife=True,
        grip=(0.065, 0.032), muzzle=(0.299, 0.03), sight=(0.065, 0.032)),
    "Knife_Karambit": dict(
        source="Knife_Karambit", length=0.20, front="-X", knife=True,
        grip=(0.070, 0.051), muzzle=(0.199, 0.0), sight=(0.070, 0.051)),
}


# ----------------------------------------------------------------------------------------------
# Import si normalizare
# ----------------------------------------------------------------------------------------------

def import_weapon(spec, work):
    with zipfile.ZipFile(os.path.join(SOURCE, f"{spec['source']}.zip")) as archive:
        archive.extractall(work)
    fbx = [os.path.join(r, f) for r, _, fs in os.walk(work) for f in fs if f.lower().endswith(".fbx")][0]
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=fbx)
    meshes = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    for o in list(bpy.data.objects):
        if o not in before and o.type != "MESH":
            bpy.data.objects.remove(o)
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    body = meshes[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    # teava spre +Y, lungimea reala, apoi cadrul de masurare: y de la 0 (spate), z de la 0 (jos), x centrat
    body.data.transform(Matrix.Rotation(math.radians(-90 if spec["front"] == "-X" else 90), 4, "Z"))
    co = np.array([v.co[:] for v in body.data.vertices])
    body.data.transform(Matrix.Scale(spec["length"] / (co[:, 1].max() - co[:, 1].min()), 4))
    co = np.array([v.co[:] for v in body.data.vertices])
    body.data.transform(Matrix.Translation((-(co[:, 0].min() + co[:, 0].max()) / 2, -co[:, 1].min(), -co[:, 2].min())))

    # originea in palma dreapta
    gy, gz = spec["grip"]
    body.data.transform(Matrix.Translation((0.0, -gy, -gz)))

    texture_dir = os.path.dirname(fbx)
    for root, dirs, _ in os.walk(work):
        for d in dirs:
            if d.lower().endswith(".fbm"):
                texture_dir = os.path.join(root, d)
    return body, texture_dir


def local(spec, point):
    """Reper din tabel (y, z[, x]) -> coordonate fata de palma (x, y, z) in Blender."""
    gy, gz = spec["grip"]
    x = point[2] if len(point) > 2 else 0.0
    return Vector((x, point[0] - gy, point[1] - gz))


# ----------------------------------------------------------------------------------------------
# Texturi si material (aceleasi reguli ca la personaje)
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
    out = os.path.join(WEAPONS_OUT, name, "Textures")
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

    packed = np.zeros((count, 4), dtype=np.float32)
    packed[:, 0] = channel("metallic", 0.0)
    packed[:, 3] = 1.0 - channel("roughness", 0.6)
    image = bpy.data.images.new(f"{name}_MetallicSmoothness", size[0], size[1], alpha=True)
    image.pixels.foreach_set(packed.ravel())
    image.filepath_raw = os.path.join(out, f"{name}_MetallicSmoothness.png")
    image.file_format = "PNG"
    image.save()


def body_material(obj, name, roles):
    mat = bpy.data.materials.get(f"{name}_Body") or bpy.data.materials.new(f"{name}_Body")
    mat.use_nodes = True
    tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
    tex.image = roles["base"]
    mat.node_tree.links.new(tex.outputs["Color"], mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
    obj.data.materials.clear()
    obj.data.materials.append(mat)


# ----------------------------------------------------------------------------------------------
# Optice: sticla deschisa + reticul
# ----------------------------------------------------------------------------------------------

def open_optic(obj, spec):
    """Sterge fetele lovite de raze paralele cu axa optica, in interiorul opticii (sticla opaca din Tripo)."""
    optic = spec["optic"]
    sy, sz = spec["sight"]
    gy, gz = spec["grip"]
    y0, y1 = optic["y"][0] - gy, optic["y"][1] - gy
    axis_z = sz - gz
    radius = optic["radius"]
    removed = 0
    for _ in range(8):
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        bm.faces.ensure_lookup_table()
        tree = BVHTree.FromBMesh(bm)
        doomed = set()
        for r in np.linspace(0.0, radius, 4):
            for a in np.linspace(0.0, 2 * math.pi, 12, endpoint=False):
                origin = Vector((r * math.cos(a), y0 - 0.002, axis_z + r * math.sin(a)))
                hit = tree.ray_cast(origin, Vector((0, 1, 0)), (y1 - y0) + 0.004)
                if hit[0] is not None:
                    doomed.add(hit[2])
                origin_back = Vector((r * math.cos(a), y1 + 0.002, axis_z + r * math.sin(a)))
                hit = tree.ray_cast(origin_back, Vector((0, -1, 0)), (y1 - y0) + 0.004)
                if hit[0] is not None:
                    doomed.add(hit[2])
        if not doomed:
            bm.free()
            break
        bmesh.ops.delete(bm, geom=[bm.faces[i] for i in doomed], context="FACES")
        bm.to_mesh(obj.data)
        bm.free()
        removed += len(doomed)
    return removed


def add_reticle(name, spec):
    """Punct rosu (red dot) sau inel cu punct (holo), luminos, pe axa optica in fata ochiului."""
    sy, sz = spec["sight"]
    gy, gz = spec["grip"]
    optic = spec["optic"]
    y = optic["y"][1] - gy - 0.004
    z = sz - gz
    bm = bmesh.new()

    def quad(cx, cz, hw, hh):
        # doua fete (fata / spate) pe vertecsi separati: se vede din ambele parti
        for dy, flip in ((0.0, False), (0.0002, True)):
            verts = [bm.verts.new((cx - hw, y + dy, cz - hh)), bm.verts.new((cx + hw, y + dy, cz - hh)),
                     bm.verts.new((cx + hw, y + dy, cz + hh)), bm.verts.new((cx - hw, y + dy, cz + hh))]
            bm.faces.new(list(reversed(verts)) if flip else verts)

    quad(0.0, z, 0.0008, 0.0008)
    if optic["kind"] == "holo":
        for k in range(16):
            a = 2 * math.pi * k / 16
            quad(0.0075 * math.cos(a), z + 0.0075 * math.sin(a), 0.0005, 0.0005)
    mesh = bpy.data.meshes.new("Reticle")
    bm.to_mesh(mesh)
    bm.free()
    obj = link(bpy.data.objects.new("Reticle", mesh))
    mat = bpy.data.materials.get("EmergencyGlow") or bpy.data.materials.new("EmergencyGlow")
    mesh.materials.append(mat)
    return obj


# ----------------------------------------------------------------------------------------------
# Piese si socket-uri
# ----------------------------------------------------------------------------------------------

def separate(obj, test, name):
    """Fetele cu toti vertecsii in regiunea data devin obiect separat (piesa animabila)."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    faces = [f for f in bm.faces if all(test(v.co) for v in f.verts)]
    if not faces:
        bm.free()
        return None
    part_mesh = bpy.data.meshes.new(name)
    part_bm = bmesh.new()
    uv_src = bm.loops.layers.uv.active
    uv_dst = part_bm.loops.layers.uv.new() if uv_src is not None else None
    vmap = {}
    for f in faces:
        verts = []
        for v in f.verts:
            if v not in vmap:
                vmap[v] = part_bm.verts.new(v.co)
            verts.append(vmap[v])
        try:
            nf = part_bm.faces.new(verts)
        except ValueError:
            continue
        nf.material_index = f.material_index
        nf.smooth = f.smooth
        if uv_dst is not None:
            for l_src, l_dst in zip(f.loops, nf.loops):
                l_dst[uv_dst].uv = l_src[uv_src].uv
    bmesh.ops.delete(bm, geom=faces, context="FACES")
    bm.to_mesh(obj.data)
    bm.free()
    part_bm.to_mesh(part_mesh)
    part_bm.free()
    for m in obj.data.materials:
        part_mesh.materials.append(m)
    part = link(bpy.data.objects.new(name, part_mesh))
    # pivotul piesei pe centrul ei (animatia o muta din pozitia de repaus)
    center = sum((v.co for v in part_mesh.vertices), Vector()) / len(part_mesh.vertices)
    part_mesh.transform(Matrix.Translation(-center))
    part.location = center
    return part


def add_socket(name, position, parent):
    e = link(bpy.data.objects.new(name, None))
    e.empty_display_type = "ARROWS"
    e.empty_display_size = 0.03
    e.location = position
    e.parent = parent
    return e


def reduce(obj, budget):
    tris = triangle_count(obj)
    if tris > budget:
        decimate = obj.modifiers.new("Decimate", "DECIMATE")
        decimate.ratio = budget / tris
        apply_modifier(obj, decimate)


def build_variant(name, spec, source, roles, fp):
    """O varianta (FP sau TP) intr-o scena curata, ca numele socket-urilor sa nu primeasca sufixe."""
    knife = spec.get("knife", False)
    root = link(bpy.data.objects.new(f"{name}_{'FP' if fp else 'TP'}", None))
    body = source.copy()
    body.data = source.data.copy()
    body.name = "Body"
    link(body)
    body.parent = root
    budget = (KNIFE_FP_TRIANGLES if fp else KNIFE_TP_TRIANGLES) if knife else (FP_TRIANGLES if fp else TP_TRIANGLES)
    reduce(body, budget)

    objects = [root, body]
    opened = 0
    if fp and spec.get("optic", {}).get("kind") in ("reddot", "holo"):
        opened = open_optic(body, spec)
        reticle = add_reticle(name, spec)
        reticle.parent = root
        objects.append(reticle)

    if fp and "magazine" in spec:
        mag = spec["magazine"]
        gy, gz = spec["grip"]
        y0, y1, zb = mag["y"][0] - gy, mag["y"][1] - gy, mag["z_below"] - gz
        part = separate(body, lambda c: y0 <= c.y <= y1 and c.z <= zb, "Magazine")
        if part:
            part.parent = root
            objects.append(part)
    if fp and "slide" in spec:
        za = spec["slide"]["z_above"] - spec["grip"][1]
        part = separate(body, lambda c: c.z >= za, "Slide")
        if part:
            part.parent = root
            objects.append(part)

    objects.append(add_socket("Grip_R", Vector((0, 0, 0)), root))
    objects.append(add_socket("Muzzle", local(spec, spec["muzzle"]), root))
    objects.append(add_socket("Sight", local(spec, spec["sight"]), root))
    if "eject" in spec:
        objects.append(add_socket("Eject", local(spec, spec["eject"]) + Vector((0.02, 0, 0)), root))
    if "grip_l" in spec:
        objects.append(add_socket("Grip_L", local(spec, spec["grip_l"]), root))
    return objects, triangle_count(body) + sum(triangle_count(o) for o in objects if o.type == "MESH" and o is not body), opened


def build(name):
    spec = WEAPONS[name]
    results = {}
    for fp in (True, False):
        reset_scene()
        work = tempfile.mkdtemp(prefix=f"k17_tw_{name}_")
        source, texture_dir = import_weapon(spec, work)
        roles = classify_textures(texture_dir)
        body_material(source, name, roles)
        if fp:
            write_textures(name, roles)
        source.name = "Source"
        objects, tris, opened = build_variant(name, spec, source, roles, fp)
        bpy.data.objects.remove(source)
        path = os.path.join(WEAPONS_OUT, name, f"{name}_{'FP' if fp else 'TP'}.fbx")
        export_fbx(path, objects, with_armature=False)
        results["FP" if fp else "TP"] = (tris, opened)
        if fp:
            render_preview(name, objects)
            bpy.ops.wm.save_as_mainfile(filepath=os.path.join(REPO, "Tools", "Blender", f"K17_{name}.blend"), compress=True)
        shutil.rmtree(work, ignore_errors=True)
    print(f"WEAPON {name}: FP {results['FP'][0]} triunghiuri (sticla deschisa: {results['FP'][1]} fete), TP {results['TP'][0]}")


def render_preview(name, objects):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "TEXTURE"
    scene.render.resolution_x, scene.render.resolution_y = 1400, 700
    cam = link(bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")))
    scene.camera = cam
    meshes = [o for o in objects if o.type == "MESH"]
    pts = [o.matrix_world @ v.co for o in meshes for v in o.data.vertices]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    center = (lo + hi) / 2
    size = max(hi.y - lo.y, (hi.z - lo.z) * 2)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = size * 1.15
    cam.location = center + Vector((2.0, 0, 0))
    cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
    os.makedirs(DOCS, exist_ok=True)
    scene.render.filepath = os.path.join(DOCS, f"{name}_Side.png")
    bpy.ops.render.render(write_still=True)
    # in lungul axei optice, din spate (se vede daca sticla e deschisa)
    cam.data.type = "PERSP"
    cam.data.lens = 35
    sight = [o for o in objects if o.name == "Sight"]
    if sight:
        cam.location = sight[0].matrix_world.translation + Vector((0, -0.12, 0))
        cam.rotation_euler = Vector((0, 1, 0)).to_track_quat("-Z", "Z").to_euler()
        scene.render.filepath = os.path.join(DOCS, f"{name}_Sight.png")
        bpy.ops.render.render(write_still=True)


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    for name in (args or list(WEAPONS)):
        build(name)
    print("TRIPO_WEAPONS_DONE")


if __name__ == "__main__":
    main()
