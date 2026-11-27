"""
Recuzita generata in Tripo (Tools/Tripo/Source/Props/<Nume>.zip) -> piese de harta K17 cu LOD-uri.

Doi pasi:
  cache  - importa FBX-ul HD (sute de mii / milioane de triunghiuri), il reduce la o copie de lucru, copiaza
           texturile si randeaza piesa din patru parti (Docs/Props/Survey/<Nume>.png), ca sa-i aleg fata.
           Copia de lucru: Temp/PropCache/<Nume>.blend (nu intra in repo).
  build  - din copia de lucru: rotire (fata piesei spre +Z in Unity, vezi "front"), scara reala dupa
           dimensiunile din PROPS, originea jos la mijloc, LOD0/1/2, texturi, FBX in
           Assets/_Project/Art/Props/<Nume>/<Nume>.fbx si randare Docs/Props/<Nume>.png.

Rulare:
    D:/Blender/blender.exe -b --factory-startup --python Tools/Blender/k17_tripo_props.py -- cache [Nume ...]
    D:/Blender/blender.exe -b --factory-startup --python Tools/Blender/k17_tripo_props.py -- build [Nume ...]

Conventii: in Blender x dreapta, y adancime, z sus. Fata piesei (ce vede jucatorul: ecranele pupitrului,
usile dulapului) se intoarce spre +Y in Blender; cu exportul din k17_common (axis_forward="-Z") ajunge pe
+Z in Unity, directia "inainte" a prefab-ului (verificat in harta). Dimensiunile din PROPS sunt (latime x, adancime y, inaltime z) in metri.
"""

import json
import math
import os
import shutil
import sys
import tempfile
import zipfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

from k17_common import REPO, apply_modifier, export_fbx, link, reset_scene, triangle_count  # noqa: E402

SOURCE = os.path.join(REPO, "Tools", "Tripo", "Source", "Props")
CACHE = os.path.join(REPO, "Temp", "PropCache")
PROPS_OUT = os.path.join(REPO, "Assets", "_Project", "Art", "Props")
DOCS = os.path.join(REPO, "Docs", "Props")

CACHE_TRIANGLES = 60000

# size = (latime, adancime, inaltime) dupa rotire; fit = dupa ce axa se scaleaza ("height", "width" sau "max");
# front = unde priveste fata piesei in modelul importat ("-Y", "+Y", "-X", "+X"); lod0 = triunghiuri.
PROPS = {
    "Barrier_Concrete":   {"size": (2.0, 0.6, 0.8),  "fit": "width",  "front": "-Y", "lod0": 10000},
    "Cabinet_Electrical": {"size": (1.2, 0.4, 2.0),  "fit": "height", "front": "-Y", "lod0": 15000, "squash": (1.2, 0.6, 2.0)},
    "Cable_Drum":         {"size": (1.4, 0.9, 1.4),  "fit": "height", "front": "+X", "lod0": 15000},
    "Cart_Tools":         {"size": (0.9, 0.5, 1.0),  "fit": "height", "front": "-Y", "lod0": 15000},
    "Console_Control":    {"size": (2.0, 0.9, 1.2),  "fit": "width",  "front": "-X", "lod0": 25000},
    "Crate_Military":     {"size": (1.2, 0.8, 0.8),  "fit": "width",  "front": "-Y", "lod0": 10000},
    "Cryo_Capsule":       {"size": (1.0, 1.0, 2.2),  "fit": "height", "front": "-Y", "lod0": 20000},
    "Desk_Office":        {"size": (1.6, 0.8, 1.2),  "fit": "width",  "front": "-X", "lod0": 20000},
    "Drum_Hazard":        {"size": (0.6, 0.6, 0.9),  "fit": "height", "front": "-Y", "lod0": 8000},
    "Forklift":           {"size": (3.0, 1.2, 2.2),  "fit": "height", "front": "-Y", "lod0": 25000},
    "Generator_Diesel":   {"size": (3.0, 1.2, 1.8),  "fit": "width",  "front": "-X", "lod0": 25000},
    "Light_Floodlight":   {"size": (1.5, 1.5, 3.5),  "fit": "height", "front": "-Y", "lod0": 15000},
    "Locker_Row":         {"size": (1.6, 0.5, 1.9),  "fit": "height", "front": "-Y", "lod0": 15000},
    "Pump_Coolant":       {"size": (1.8, 0.9, 1.2),  "fit": "width",  "front": "-Y", "lod0": 20000},
    "Rack_Server":        {"size": (0.6, 1.0, 2.0),  "fit": "height", "front": "-Y", "lod0": 15000, "squash": (0.8, 1.0, 2.0)},
    "Sandbag_Wall":       {"size": (2.0, 0.6, 1.0),  "fit": "width",  "front": "-Y", "lod0": 12000},
    "Shelf_Storage":      {"size": (2.0, 0.6, 2.2),  "fit": "height", "front": "-Y", "lod0": 20000},
    "Tank_Pressure":      {"size": (1.2, 1.2, 2.5),  "fit": "height", "front": "-Y", "lod0": 15000},
    "Valve_Manifold_A":   {"size": (2.0, 0.6, 1.5),  "fit": "width",  "front": "-X", "lod0": 20000},
    "Valve_Manifold_B":   {"size": (2.0, 0.6, 1.5),  "fit": "width",  "front": "-Y", "lod0": 20000},
    "Ventilation_Unit":   {"size": (2.0, 1.2, 1.4),  "fit": "width",  "front": "-Y", "lod0": 15000},
    "Workbench_Lab":      {"size": (2.0, 0.8, 0.95), "fit": "width",  "front": "-Y", "lod0": 25000},
}

LOD_RATIOS = (1.0, 0.35, 0.1)


# ----------------------------------------------------------------------------------------------
# Pasul 1: cache
# ----------------------------------------------------------------------------------------------

def classify_textures(texture_dir):
    """Rolurile hartilor din Tripo, dupa nume: basecolor / _0_0 = culoare, normal, roughness, metallic.
    Harta "_rm" (rugozitate + metal impachetate) se sare: avem hartile separate."""
    roles = {}
    for f in sorted(os.listdir(texture_dir)):
        lower = f.lower()
        if not lower.endswith((".jpg", ".jpeg", ".png")) or lower.rsplit(".", 1)[0].endswith("_rm"):
            continue
        if "normal" in lower:
            role = "normal"
        elif "rough" in lower:
            role = "roughness"
        elif "metal" in lower:
            role = "metallic"
        else:
            role = "base"
        roles.setdefault(role, os.path.join(texture_dir, f))
    return roles


def import_source(name, work):
    with zipfile.ZipFile(os.path.join(SOURCE, f"{name}.zip")) as archive:
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
    body.name = name

    texture_dir = os.path.dirname(fbx)
    for root, dirs, _ in os.walk(work):
        for d in dirs:
            if d.lower().endswith(".fbm"):
                texture_dir = os.path.join(root, d)
    return body, texture_dir


def cache(name):
    reset_scene()
    work = tempfile.mkdtemp(prefix=f"k17_prop_{name}_")
    body, texture_dir = import_source(name, work)
    source_tris = triangle_count(body)
    if source_tris > CACHE_TRIANGLES:
        decimate = body.modifiers.new("Decimate", "DECIMATE")
        decimate.ratio = CACHE_TRIANGLES / source_tris
        apply_modifier(body, decimate)

    # texturile langa copia de lucru
    out = os.path.join(CACHE, name)
    os.makedirs(out, exist_ok=True)
    roles = {}
    for role, path in classify_textures(texture_dir).items():
        target = os.path.join(out, f"{role}{os.path.splitext(path)[1].lower()}")
        shutil.copyfile(path, target)
        roles[role] = target

    material = bpy.data.materials.new(f"{name}_Body")
    material.use_nodes = True
    if "base" in roles:
        tex = material.node_tree.nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(roles["base"])
        material.node_tree.links.new(tex.outputs["Color"], material.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
    body.data.materials.clear()
    body.data.materials.append(material)

    co = np.array([v.co[:] for v in body.data.vertices])
    dims = (co.max(axis=0) - co.min(axis=0)).tolist()
    info = {"source_triangles": source_tris, "cache_triangles": triangle_count(body), "dims": dims, "roles": sorted(roles)}
    with open(os.path.join(out, "info.json"), "w") as f:
        json.dump(info, f, indent=1)

    survey(name, body)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(CACHE, f"{name}.blend"), compress=True)
    shutil.rmtree(work, ignore_errors=True)
    print(f"PROP_CACHE {name}: {source_tris} -> {info['cache_triangles']} triunghiuri, dims {['%.2f' % d for d in dims]}")


def survey(name, body):
    """Patru vederi (dinspre -Y, +X, +Y, -X) intr-o singura imagine, ca sa aleg fata piesei."""
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "TEXTURE"
    scene.render.resolution_x = scene.render.resolution_y = 500
    cam = link(bpy.data.objects.new("SurveyCam", bpy.data.cameras.new("SurveyCam")))
    scene.camera = cam
    cam.data.type = "ORTHO"
    co = np.array([body.matrix_world @ v.co for v in body.data.vertices])
    lo, hi = co.min(axis=0), co.max(axis=0)
    center = Vector(((lo + hi) / 2).tolist())
    radius = float(np.linalg.norm(hi - lo)) / 2
    cam.data.ortho_scale = radius * 2.2
    tiles = []
    labels = [("-Y", Vector((0, -1, 0.35))), ("+X", Vector((1, 0, 0.35))), ("+Y", Vector((0, 1, 0.35))), ("-X", Vector((-1, 0, 0.35)))]
    tmp = tempfile.mkdtemp(prefix="k17_survey_")
    for label, direction in labels:
        cam.location = center + direction.normalized() * radius * 4
        cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
        path = os.path.join(tmp, f"{label}.png")
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        img = bpy.data.images.load(path)
        tiles.append(np.array(img.pixels[:], dtype=np.float32).reshape(500, 500, 4))
    # 2 x 2: sus -Y, +X; jos +Y, -X (pixelii Blender incep de jos)
    top = np.concatenate([tiles[0], tiles[1]], axis=1)
    bottom = np.concatenate([tiles[2], tiles[3]], axis=1)
    sheet = np.concatenate([bottom, top], axis=0)
    out = bpy.data.images.new(f"{name}_survey", 1000, 1000, alpha=True)
    out.pixels.foreach_set(sheet.ravel())
    os.makedirs(os.path.join(DOCS, "Survey"), exist_ok=True)
    out.filepath_raw = os.path.join(DOCS, "Survey", f"{name}.png")
    out.file_format = "PNG"
    out.save()
    bpy.data.objects.remove(cam)
    shutil.rmtree(tmp, ignore_errors=True)


# ----------------------------------------------------------------------------------------------
# Pasul 2: build
# ----------------------------------------------------------------------------------------------

FRONT_ROTATION = {"-Y": 0.0, "+X": -90.0, "+Y": 180.0, "-X": 90.0}


def write_textures(name):
    src = os.path.join(CACHE, name)
    out = os.path.join(PROPS_OUT, name, "Textures")
    os.makedirs(out, exist_ok=True)
    files = {os.path.splitext(f)[0]: os.path.join(src, f) for f in os.listdir(src) if f.lower().endswith((".jpg", ".jpeg", ".png"))}
    if "base" in files:
        shutil.copyfile(files["base"], os.path.join(out, f"{name}_BaseColor.jpg"))
    if "normal" in files:
        shutil.copyfile(files["normal"], os.path.join(out, f"{name}_Normal.jpg"))

    base = bpy.data.images.load(files["base"])
    size = base.size[:]
    count = size[0] * size[1]

    def channel(role, default):
        if role not in files:
            return np.full(count, default, dtype=np.float32)
        img = bpy.data.images.load(files[role])
        if tuple(img.size) != tuple(size):
            img.scale(*size)
        return np.array(img.pixels[:], dtype=np.float32).reshape(-1, 4)[:, 0]

    packed = np.zeros((count, 4), dtype=np.float32)
    packed[:, 0] = channel("metallic", 0.0)
    packed[:, 3] = 1.0 - channel("roughness", 0.7)
    image = bpy.data.images.new(f"{name}_MetallicSmoothness", size[0], size[1], alpha=True)
    image.pixels.foreach_set(packed.ravel())
    image.filepath_raw = os.path.join(out, f"{name}_MetallicSmoothness.png")
    image.file_format = "PNG"
    image.save()


def reduce_to(obj, budget):
    """Reducere pana la buget, fara sa se treaca peste granitele de textura (altfel texturile se intind).
    Cand colapsarea se blocheaza (modele din multe bucati), se unesc fetele aproape plane si se reincearca."""
    angle = 2.0
    for _ in range(8):
        current = triangle_count(obj)
        if current <= budget * 1.05:
            break
        decimate = obj.modifiers.new("Decimate", "DECIMATE")
        decimate.ratio = budget / current
        decimate.use_collapse_triangulate = True
        apply_modifier(obj, decimate)
        if triangle_count(obj) > current * 0.97:
            # colapsarea s-a blocat: se unesc fetele aproape plane, apoi se reincearca
            planar = obj.modifiers.new("Planar", "DECIMATE")
            planar.decimate_type = "DISSOLVE"
            planar.angle_limit = math.radians(angle)
            planar.delimit = {"UV", "SEAM", "SHARP", "MATERIAL"}     # texturile raman pe loc
            apply_modifier(obj, planar)
            triangulate = obj.modifiers.new("Tri", "TRIANGULATE")
            apply_modifier(obj, triangulate)
            angle *= 2.0


def build(name):
    spec = PROPS[name]
    bpy.ops.wm.open_mainfile(filepath=os.path.join(CACHE, f"{name}.blend"))
    body = bpy.data.objects[name]
    for o in list(bpy.data.objects):
        if o is not body:
            bpy.data.objects.remove(o)

    # fata spre +Y (exportul o duce pe +Z in Unity, directia "inainte" a prefab-ului), apoi scara reala si originea
    body.data.transform(Matrix.Rotation(math.radians(FRONT_ROTATION[spec["front"]] + 180.0 + spec.get("yaw", 0.0)), 4, "Z"))
    co = np.array([v.co[:] for v in body.data.vertices])
    dims = co.max(axis=0) - co.min(axis=0)
    width, depth, height = spec["size"]
    if spec["fit"] == "height":
        scale = height / dims[2]
    elif spec["fit"] == "width":
        scale = width / dims[0]
    else:
        scale = min(width / dims[0], depth / dims[1], height / dims[2])
    if "squash" in spec:
        # piesele cu proportii gresite din Tripo: fiecare axa la dimensiunea reala
        sx, sy, sz = spec["squash"]
        body.data.transform(Matrix.Diagonal((sx / dims[0], sy / dims[1], sz / dims[2], 1.0)))
    else:
        body.data.transform(Matrix.Scale(scale, 4))
    co = np.array([v.co[:] for v in body.data.vertices])
    body.data.transform(Matrix.Translation((-(co[:, 0].min() + co[:, 0].max()) / 2, -(co[:, 1].min() + co[:, 1].max()) / 2, -co[:, 2].min())))
    co = np.array([v.co[:] for v in body.data.vertices])
    final = (co.max(axis=0) - co.min(axis=0)).tolist()

    body.data.materials[0].name = f"{name}_Body"
    root = link(bpy.data.objects.new(name, None))
    body.name = f"{name}_Source"
    objects = [root]
    tris = []
    for level, ratio in enumerate(LOD_RATIOS):
        lod = body.copy()
        lod.data = body.data.copy()
        lod.name = f"{name}_LOD{level}"
        link(lod)
        lod.parent = root
        budget = int(spec["lod0"] * ratio)
        reduce_to(lod, budget)
        tris.append(triangle_count(lod))
        objects.append(lod)
    bpy.data.objects.remove(body)

    write_textures(name)
    export_fbx(os.path.join(PROPS_OUT, name, f"{name}.fbx"), objects, with_armature=False)
    render(name, [o for o in objects if o.name.endswith("LOD0")])
    print(f"PROP {name}: {final[0]:.2f} x {final[1]:.2f} x {final[2]:.2f} m, LOD {tris}")
    return {"size": final, "lods": tris}


def render(name, meshes):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "TEXTURE"
    scene.render.resolution_x, scene.render.resolution_y = 900, 700
    cam = link(bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")))
    scene.camera = cam
    co = np.array([o.matrix_world @ v.co for o in meshes for v in o.data.vertices])
    lo, hi = co.min(axis=0), co.max(axis=0)
    center = Vector(((lo + hi) / 2).tolist())
    radius = float(np.linalg.norm(hi - lo)) / 2
    cam.data.lens = 50
    # din fata-stanga, de la inaltimea ochilor (fata piesei e spre +Y)
    cam.location = center + Vector((0.55, 1.0, 0.45)).normalized() * radius * 3.4
    cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
    os.makedirs(DOCS, exist_ok=True)
    scene.render.filepath = os.path.join(DOCS, f"{name}.png")
    bpy.ops.render.render(write_still=True)


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    mode = args[0] if args else "build"
    names = args[1:] or list(PROPS)
    report = {}
    for name in names:
        if mode == "cache":
            cache(name)
        else:
            report[name] = build(name)
    if mode == "build":
        os.makedirs(DOCS, exist_ok=True)
        with open(os.path.join(DOCS, "props.json"), "w") as f:
            json.dump(report, f, indent=1)
    print("TRIPO_PROPS_DONE")


if __name__ == "__main__":
    main()
