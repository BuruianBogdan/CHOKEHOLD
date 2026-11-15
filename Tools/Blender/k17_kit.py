"""
Blacksite K-17 - kit modular (v1).

Genereaza piesele kit-ului din cod, le exporta ca FBX pentru Unity si randeaza o previzualizare.
Fiecare piesa are exact dimensiunile obiectului din generatorul hartii pe care il inlocuieste
(colliderul ramane cel din Unity; aici e doar partea vizuala).

Rulare (din radacina repo-ului):
    D:/Blender/blender.exe -b --factory-startup --python Tools/Blender/k17_kit.py

Optiuni dupa "--":
    --no-preview   nu randeaza imaginile din Docs/Kit
    --no-blend     nu salveaza Tools/Blender/K17_Kit.blend

Conventii:
    - coordonatele din script sunt cele din Unity: x dreapta, y sus, z inainte (metri);
    - pivotul fiecarei piese e la baza, in centrul amprentei (exceptie: usa, cu pivotul in balama);
    - UV-urile sunt proiectate pe cutie, 1 unitate UV = 1 m, ca la cutiile generatorului;
    - sloturile de material au nume fixe (Wood, Concrete, Steel...), Unity le leaga de materialele K17_*.
"""

import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
FBX_DIR = os.path.join(REPO, "Assets", "_Project", "Art", "Models", "Kit")
PREVIEW_DIR = os.path.join(REPO, "Docs", "Kit")
BLEND_PATH = os.path.join(REPO, "Tools", "Blender", "K17_Kit.blend")

# Culori de previzualizare pentru sloturi (in Unity fiecare slot are materialul lui).
SLOT_COLORS = {
    "Wood": (0.55, 0.42, 0.28),
    "WoodDark": (0.36, 0.27, 0.18),
    "Concrete": (0.62, 0.62, 0.6),
    "ConcreteDark": (0.45, 0.45, 0.44),
    "Steel": (0.36, 0.37, 0.4),
    "Container": (0.65, 0.22, 0.16),
    "DoorPaint": (0.3, 0.4, 0.38),
    "Glass": (0.08, 0.1, 0.12),
    "BarrelPaint": (0.15, 0.3, 0.55),
    "Hazard": (0.92, 0.75, 0.1),
}


def U(x, y, z):
    """Punct Unity (x, y sus, z inainte) -> Blender (x, y = z Unity, z = y Unity)."""
    return Vector((x, z, y))


# ----------------------------------------------------------------------------------------------
# Constructor de plase
# ----------------------------------------------------------------------------------------------

class Piece:
    def __init__(self, name, bevel=0.012):
        self.name = name
        self.bevel = bevel
        self.bm = bmesh.new()
        self.slots = []

    def slot(self, name):
        if name not in self.slots:
            self.slots.append(name)
        return self.slots.index(name)

    def prism(self, base, extrude, slot, smooth_sides=False):
        """Prisma: poligonul `base` (puncte Unity) impins cu vectorul `extrude` (Unity)."""
        index = self.slot(slot)
        ex = U(*extrude)
        bottom = [self.bm.verts.new(U(*p)) for p in base]
        top = [self.bm.verts.new(U(*p) + ex) for p in base]

        faces = [self.bm.faces.new(bottom), self.bm.faces.new(list(reversed(top)))]
        n = len(base)

        for k in range(n):
            side = self.bm.faces.new((bottom[k], bottom[(k + 1) % n], top[(k + 1) % n], top[k]))
            side.smooth = smooth_sides
            faces.append(side)

        for face in faces:
            face.material_index = index

    def box(self, x0, y0, z0, x1, y1, z1, slot):
        """Cutie aliniata pe axe, intre doua colturi (Unity)."""
        x0, x1 = min(x0, x1), max(x0, x1)
        y0, y1 = min(y0, y1), max(y0, y1)
        z0, z1 = min(z0, z1), max(z0, z1)
        self.prism([(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)], (0, y1 - y0, 0), slot)

    def box_c(self, cx, cy, cz, sx, sy, sz, slot):
        """Cutie data prin centru si dimensiuni."""
        self.box(cx - sx / 2, cy - sy / 2, cz - sz / 2, cx + sx / 2, cy + sy / 2, cz + sz / 2, slot)

    def bar(self, a, b, width, depth_axis, depth, slot):
        """Bara dreapta intre punctele a si b dintr-un plan (pentru contravantuiri in diagonala).
        depth_axis: axa Unity perpendiculara pe plan ('x' sau 'z'); depth = (min, max) pe acea axa."""
        a, b = Vector(a), Vector(b)
        d = (b - a).normalized()
        if depth_axis == "z":
            n = Vector((-d.y, d.x, 0)) * (width / 2)
            corners = [a - n, b - n, b + n, a + n]
            base = [(p.x, p.y, depth[0]) for p in corners]
            self.prism(base, (0, 0, depth[1] - depth[0]), slot)
        else:
            # a, b sunt (z, y) in planul x = const
            n = Vector((-d.y, d.x, 0)) * (width / 2)
            corners = [a - n, b - n, b + n, a + n]
            base = [(depth[0], p.y, p.x) for p in corners]
            self.prism(base, (depth[1] - depth[0], 0, 0), slot)

    def cylinder(self, center, axis, radius, length, slot, segments=20):
        """Cilindru cu baza in `center` (Unity), pe axa 'x', 'y' sau 'z'."""
        cx, cy, cz = center
        base = []

        for k in range(segments):
            a = 2 * math.pi * k / segments
            u, v = math.cos(a) * radius, math.sin(a) * radius
            if axis == "y":
                base.append((cx + u, cy, cz + v))
            elif axis == "x":
                base.append((cx, cy + u, cz + v))
            else:
                base.append((cx + u, cy + v, cz))

        ext = {"x": (length, 0, 0), "y": (0, length, 0), "z": (0, 0, length)}[axis]
        self.prism(base, ext, slot, smooth_sides=True)

    def build(self):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)

        mesh = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(mesh)
        self.bm.free()

        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.scene.collection.objects.link(obj)

        for name in self.slots:
            mesh.materials.append(get_material(name))

        if self.bevel > 0:
            modifier = obj.modifiers.new("Bevel", "BEVEL")
            modifier.width = self.bevel
            modifier.segments = 1
            modifier.limit_method = "ANGLE"
            modifier.angle_limit = math.radians(35)
            modifier.harden_normals = False

            bpy.context.view_layer.objects.active = obj
            obj.select_set(True)
            bpy.ops.object.modifier_apply(modifier=modifier.name)
            obj.select_set(False)

        box_project_uv(obj)
        return obj


def get_material(name):
    material = bpy.data.materials.get(name)
    if material is None:
        material = bpy.data.materials.new(name)
        r, g, b = SLOT_COLORS.get(name, (0.6, 0.6, 0.6))
        material.diffuse_color = (r, g, b, 1)
        material.roughness = 0.2 if name == "Glass" else 0.7
        material.metallic = 0.8 if name == "Steel" else 0.0
    return material


def box_project_uv(obj):
    """UV pe cutie in metri (aceeasi regula ca CreateUVBox din generator)."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    layer = bm.loops.layers.uv.verify()

    for face in bm.faces:
        n = face.normal  # Blender: x, y = z Unity, z = sus
        ax, ay, az = abs(n.x), abs(n.y), abs(n.z)

        for loop in face.loops:
            p = loop.vert.co
            if az >= ax and az >= ay:          # sus / jos: (x, z Unity)
                uv = (p.x, p.y if n.z > 0 else -p.y)
            elif ax >= ay:                     # fata pe x: (z Unity, sus)
                uv = (-p.y if n.x > 0 else p.y, p.z)
            else:                              # fata pe z Unity: (x, sus)
                uv = (p.x if n.y > 0 else -p.x, p.z)
            loop[layer].uv = uv

    bm.to_mesh(obj.data)
    bm.free()


# ----------------------------------------------------------------------------------------------
# Piese
# ----------------------------------------------------------------------------------------------

def crate_into(p, cx, y0, cz, w, h, d, panel="Wood", frame="WoodDark", braces=True):
    """Lada cu rama de scanduri; ocupa exact cutia (cx +- w/2, y0..y0+h, cz +- d/2)."""
    t = 0.12      # latimea ramei
    inset = 0.03  # cat de adanc sta panoul fata de rama

    x0, x1 = cx - w / 2, cx + w / 2
    z0, z1 = cz - d / 2, cz + d / 2
    y1 = y0 + h

    p.box(x0 + inset, y0 + inset, z0 + inset, x1 - inset, y1 - inset, z1 - inset, panel)

    # muchii pe X (sus/jos, fata/spate)
    for yy in ((y0, y0 + t), (y1 - t, y1)):
        for zz in ((z0, z0 + t), (z1 - t, z1)):
            p.box(x0, yy[0], zz[0], x1, yy[1], zz[1], frame)
    # muchii pe Z (intre cele de pe X)
    for yy in ((y0, y0 + t), (y1 - t, y1)):
        for xx in ((x0, x0 + t), (x1 - t, x1)):
            p.box(xx[0], yy[0], z0 + t, xx[1], yy[1], z1 - t, frame)
    # stalpi verticali
    for xx in ((x0, x0 + t), (x1 - t, x1)):
        for zz in ((z0, z0 + t), (z1 - t, z1)):
            p.box(xx[0], y0 + t, zz[0], xx[1], y1 - t, zz[1], frame)

    mid = y0 + h / 2
    # scandura orizontala la mijloc pe fiecare fata laterala
    for zz in ((z0, z0 + inset), (z1 - inset, z1)):
        p.box(x0 + t, mid - t / 2, zz[0], x1 - t, mid + t / 2, zz[1], frame)
    for xx in ((x0, x0 + inset), (x1 - inset, x1)):
        p.box(xx[0], mid - t / 2, z0 + t, xx[1], mid + t / 2, z1 - t, frame)

    if not braces:
        return

    # contravantuiri in diagonala, in fiecare jumatate de fata
    bw = t * 0.8
    for low, high in ((y0 + t, mid - t / 2), (mid + t / 2, y1 - t)):
        for zz in ((z0, z0 + inset), (z1 - inset, z1)):
            p.bar((x0 + t, low, 0), (x1 - t, high, 0), bw, "z", zz, frame)
        for xx in ((x0, x0 + inset), (x1 - inset, x1)):
            p.bar((z0 + t, low, 0), (z1 - t, high, 0), bw, "x", xx, frame)


def piece_crate_big():
    p = Piece("K17_Crate_Big")
    crate_into(p, 0, 0, 0, 2, 2, 2)
    return p.build()


def piece_crate_stack():
    p = Piece("K17_Crate_Stack")
    crate_into(p, 0, 0, 0, 2, 2, 2)

    # lada de sus: militara, cu colturi de otel, putin mai mica
    w, h = 1.9, 1.5
    y0 = 2.0
    crate_into(p, 0, y0, 0, w, h, w, panel="WoodDark", frame="Wood", braces=False)

    c = 0.2
    for sx in (-1, 1):
        for sz in (-1, 1):
            for yy in ((y0, y0 + c), (y0 + h - c, y0 + h)):
                x_out = sx * (w / 2 + 0.01)
                z_out = sz * (w / 2 + 0.01)
                p.box(x_out, yy[0], z_out, x_out - sx * c, yy[1], z_out - sz * c, "Steel")
    # manere pe laterale
    for sx in (-1, 1):
        x = sx * (w / 2 + 0.035)
        p.box(x, y0 + 0.95, -0.25, x - sx * 0.045, y0 + 1.0, 0.25, "Steel")
    return p.build()


def piece_barrier_low():
    """Bariera de beton de 2 m (cover de crouch, 1,1 x 0,6). Se insiruie pe lungime."""
    p = Piece("K17_Barrier_Low", bevel=0.015)
    half = 0.98  # rost de 4 cm intre segmente

    profile_zy = [(-0.3, 0.0), (0.3, 0.0), (0.3, 0.12), (0.24, 0.4), (0.22, 1.06),
                  (0.18, 1.1), (-0.18, 1.1), (-0.22, 1.06), (-0.24, 0.4), (-0.3, 0.12)]
    p.prism([(-half, y, z) for z, y in profile_zy], (half * 2, 0, 0), "Concrete")

    # locasuri de ridicare pe muchia de sus si o banda reflectorizanta pe ambele fete
    for x in (-0.55, 0.55):
        p.box(x - 0.12, 1.1, -0.06, x + 0.12, 1.102, 0.06, "ConcreteDark")
    for s in (-1, 1):
        z = s * 0.229
        p.box(-0.5, 0.62, z, 0.5, 0.72, z + s * 0.012, "Hazard")
    return p.build()


def piece_barrier_high():
    """Blocuri de beton stivuite, 2 x 2 x 0,8 (cover in picioare). Se insiruie pe lungime."""
    p = Piece("K17_Barrier_High", bevel=0.03)
    d = 0.4
    p.box(-0.995, 0.0, -d, -0.015, 0.985, d, "Concrete")
    p.box(0.015, 0.0, -d, 0.995, 0.985, d, "Concrete")
    p.box(-0.98, 1.0, -d, 0.98, 2.0, d, "ConcreteDark")

    # urechi de ridicare (otel) inglobate in fata blocurilor
    for x in (-0.5, 0.5):
        p.box(x - 0.08, 0.86, -d - 0.015, x + 0.08, 0.94, -d + 0.01, "Steel")
        p.box(x - 0.08, 0.86, d - 0.01, x + 0.08, 0.94, d + 0.015, "Steel")
    return p.build()


def piece_container(length, name):
    """Container ISO; lung pe Z, 2,6 x 2,6. Usile sunt pe capatul +Z."""
    p = Piece(name, bevel=0.01)
    W, H = 2.6, 2.6
    hx, hz = W / 2, length / 2
    post = 0.16
    e = 0.01  # rama sta la 1 cm in interior; doar piesele de colt ating marginea

    # stalpi de colt
    for sx in (-1, 1):
        for sz in (-1, 1):
            p.box(sx * (hx - e), 0.0, sz * (hz - e), sx * (hx - post), H, sz * (hz - post), "Container")
    # piese de colt (otel)
    for sx in (-1, 1):
        for sz in (-1, 1):
            for yy in ((0.0, 0.12), (H - 0.12, H)):
                p.box(sx * hx, yy[0], sz * hz, sx * (hx - 0.18), yy[1], sz * (hz - 0.18), "Steel")
    # lonjeroane pe laturile lungi
    for sx in (-1, 1):
        p.box(sx * (hx - e), 0.0, -(hz - post), sx * (hx - post), 0.24, hz - post, "Container")
        p.box(sx * (hx - e), H - 0.16, -(hz - post), sx * (hx - post), H, hz - post, "Container")
    # traverse la capete
    for sz in (-1, 1):
        p.box(-(hx - post), 0.0, sz * (hz - e), hx - post, 0.24, sz * (hz - post), "Container")
        p.box(-(hx - post), H - 0.22, sz * (hz - e), hx - post, H, sz * (hz - post), "Container")

    # pereti laterali ondulati
    pitch, rib = 0.3, 0.15
    ribs = int((length - 2 * post) / pitch)
    start = -(ribs * pitch) / 2
    for sx in (-1, 1):
        p.box(sx * (hx - 0.08), 0.24, -(hz - post), sx * (hx - 0.06), H - 0.16, hz - post, "Container")
        for k in range(ribs):
            z = start + k * pitch + (pitch - rib) / 2
            p.box(sx * (hx - 0.06), 0.24, z, sx * (hx - 0.025), H - 0.16, z + rib, "Container")

    # acoperis cu nervuri
    p.box(-(hx - post), H - 0.07, -(hz - post), hx - post, H - 0.05, hz - post, "Container")
    roof_ribs = int((length - 2 * post) / 0.6)
    roof_start = -(roof_ribs * 0.6) / 2
    for k in range(roof_ribs):
        z = roof_start + k * 0.6 + 0.2
        p.box(-(hx - post), H - 0.05, z, hx - post, H - 0.03, z + 0.2, "Container")

    # capatul din spate (-Z): ondulat vertical
    end_ribs = int((W - 2 * post) / pitch)
    end_start = -(end_ribs * pitch) / 2
    p.box(-(hx - post), 0.24, -(hz - 0.08), hx - post, H - 0.22, -(hz - 0.06), "Container")
    for k in range(end_ribs):
        x = end_start + k * pitch + (pitch - rib) / 2
        p.box(x, 0.24, -(hz - 0.06), x + rib, H - 0.22, -(hz - 0.025), "Container")

    # usile (+Z): doua canate, bare de inchidere, manere
    zf = hz - 0.04
    for sx in (-1, 1):
        x_in, x_out = sx * 0.008, sx * (hx - post)
        p.box(x_in, 0.24, hz - 0.09, x_out, H - 0.22, zf, "Container")
        # doua panouri in relief pe fiecare canat
        for yy in ((0.4, 1.25), (1.4, 2.25)):
            p.box(sx * 0.12, yy[0], zf, sx * (hx - post - 0.1), yy[1], zf + 0.012, "Container")
        for bx in (0.3, 0.85):
            x = sx * bx
            p.cylinder((x, 0.26, hz - 0.02), "y", 0.025, H - 0.52, "Steel", segments=10)
            for yb in (0.3, H - 0.32):
                p.box(x - 0.06, yb, zf, x + 0.06, yb + 0.06, hz - 0.004, "Steel")
            p.box(x - 0.03, 1.15, zf, x + 0.03, 1.4, hz - 0.002, "Steel")
        # balamale
        for yh in (0.5, 1.3, 2.1):
            p.box(sx * (hx - post), yh, zf, sx * (hx - post - 0.12), yh + 0.1, hz - 0.01, "Steel")
    return p.build()


def piece_door_leaf():
    """Canat de usa metalica 1,4 x 2,4. Pivot in balama; canatul se intinde pe +Z, grosimea pe X."""
    p = Piece("K17_Door_Leaf", bevel=0.006)
    t = 0.045
    W, H = 1.4, 2.4
    z0, z1 = 0.02, W - 0.01

    # foaia usii, cu gol pentru vizor
    wy0, wy1, wz0, wz1 = 1.55, 2.1, 0.85, 1.22
    p.box(-t, 0.01, z0, t, wy0, z1, "DoorPaint")
    p.box(-t, wy1, z0, t, H - 0.01, z1, "DoorPaint")
    p.box(-t, wy0, z0, t, wy1, wz0, "DoorPaint")
    p.box(-t, wy0, wz1, t, wy1, z1, "DoorPaint")

    # vizor: geam si rama pe ambele fete
    p.box(-0.008, wy0, wz0, 0.008, wy1, wz1, "Glass")
    f = 0.035
    for s in (-1, 1):
        x0, x1 = s * t, s * (t + 0.01)
        p.box(x0, wy0 - f, wz0 - f, x1, wy0, wz1 + f, "Steel")
        p.box(x0, wy1, wz0 - f, x1, wy1 + f, wz1 + f, "Steel")
        p.box(x0, wy0, wz0 - f, x1, wy1, wz0, "Steel")
        p.box(x0, wy0, wz1, x1, wy1, wz1 + f, "Steel")

        # panou in relief jos, tabla de protectie, maner
        p.box(x0, 0.4, 0.15, s * (t + 0.008), 1.3, 1.25, "DoorPaint")
        p.box(x0, 0.03, 0.06, s * (t + 0.006), 0.3, 1.34, "Steel")
        p.box(x0, 0.9, 1.2, s * (t + 0.02), 1.1, 1.3, "Steel")
        p.box(s * (t + 0.02), 0.98, 1.06, s * (t + 0.05), 1.02, 1.3, "Steel")

    # balamale
    for y0 in (0.25, 1.15, 2.0):
        p.cylinder((0.0, y0, 0.0), "y", 0.028, 0.15, "Steel", segments=12)
    return p.build()


def piece_pillar_bunker():
    """Stalp de buncar 1,2 x 1,2, de la podea (-6 m) la tavan (-0,5 m): 5,5 m."""
    p = Piece("K17_Pillar_Bunker", bevel=0.02)
    H = 5.5
    p.box(-0.6, 0.0, -0.6, 0.6, 0.35, 0.6, "ConcreteDark")
    p.box(-0.55, 0.35, -0.55, 0.55, H - 0.4, 0.55, "Concrete")
    p.box(-0.6, H - 0.4, -0.6, 0.6, H, 0.6, "ConcreteDark")

    # banda de avertizare si corniere de protectie pe colturi
    p.box(-0.556, 1.9, -0.556, 0.556, 2.2, 0.556, "Hazard")
    a, th = 0.12, 0.012
    for sx in (-1, 1):
        for sz in (-1, 1):
            p.box(sx * 0.55, 0.35, sz * 0.55, sx * (0.55 + th), 1.85, sz * (0.55 - a), "Steel")
            p.box(sx * 0.55, 0.35, sz * 0.55, sx * (0.55 - a), 1.85, sz * (0.55 + th), "Steel")
    return p.build()


def piece_barrel():
    """Butoi de 200 l (decor)."""
    p = Piece("K17_Barrel", bevel=0.004)
    r, H = 0.29, 0.88
    p.cylinder((0, 0.0, 0), "y", r - 0.004, H, "BarrelPaint", segments=24)
    for y in (0.0, H - 0.035):
        p.cylinder((0, y, 0), "y", r, 0.035, "BarrelPaint", segments=24)
    for y in (0.28, 0.58):
        p.cylinder((0, y, 0), "y", r + 0.006, 0.03, "BarrelPaint", segments=24)
    for x, z in ((0.17, 0.05), (-0.16, -0.08)):
        p.cylinder((x, H, z), "y", 0.032, 0.012, "Steel", segments=10)
    return p.build()


def piece_pallet():
    """Palet de lemn 1,2 x 1,0 (decor)."""
    p = Piece("K17_Pallet", bevel=0.004)
    for z in (-0.45, 0.0, 0.45):
        p.box(-0.6, 0.0, z - 0.05, 0.6, 0.022, z + 0.05, "WoodDark")
    for x in (-0.55, 0.0, 0.55):
        for z in (-0.45, 0.0, 0.45):
            p.box(x - 0.05, 0.022, z - 0.05, x + 0.05, 0.1, z + 0.05, "WoodDark")
        p.box(x - 0.05, 0.1, -0.5, x + 0.05, 0.122, 0.5, "Wood")
    for k in range(7):
        z = -0.5 + 0.05 + k * (0.9 / 6)
        p.box(-0.6, 0.122, z - 0.05, 0.6, 0.144, z + 0.05, "Wood")
    return p.build()


PIECES = [
    piece_crate_big,
    piece_crate_stack,
    piece_barrier_low,
    piece_barrier_high,
    lambda: piece_container(6.0, "K17_Container_20"),
    lambda: piece_container(12.0, "K17_Container_40"),
    piece_door_leaf,
    piece_pillar_bunker,
    piece_barrel,
    piece_pallet,
]


# ----------------------------------------------------------------------------------------------
# Export, previzualizare
# ----------------------------------------------------------------------------------------------

def export_fbx(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

    path = os.path.join(FBX_DIR, obj.name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        bake_space_transform=True,
        axis_forward="Z",
        axis_up="Y",
        mesh_smooth_type="FACE",
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="STRIP",
        embed_textures=False,
    )
    obj.select_set(False)
    return path


# Pozitii in previzualizare (Unity x, z, rotatie in grade in jurul verticalei).
LAYOUT = {
    "K17_Container_40": (-4.0, 5.0, 90),
    "K17_Container_20": (7.5, 5.0, 90),
    "K17_Crate_Big": (-9.0, -1.0, 0),
    "K17_Crate_Stack": (-6.2, -1.0, 0),
    "K17_Barrier_Low": (-2.5, -1.5, 0),
    "K17_Barrier_High": (1.0, -1.0, 0),
    "K17_Pillar_Bunker": (4.0, -1.0, 0),
    "K17_Door_Leaf": (6.0, -1.7, 0),
    "K17_Barrel": (8.6, -1.4, 0),
    "K17_Pallet": (10.2, -1.2, 0),
}


def arrange(objects):
    for obj in objects:
        x, z, rot = LAYOUT.get(obj.name, (0, 0, 0))
        obj.location = U(x, 0, z)
        obj.rotation_euler = (0, 0, math.radians(rot))

    # al doilea segment de bariera joasa, ca sa se vada insiruirea
    low = next((o for o in objects if o.name == "K17_Barrier_Low"), None)
    if low is not None:
        twin = low.copy()
        twin.name = "Preview_Barrier_Low_2"
        twin.location = U(LAYOUT["K17_Barrier_Low"][0] - 2.0, 0, LAYOUT["K17_Barrier_Low"][1])
        bpy.context.scene.collection.objects.link(twin)


def setup_preview_scene():
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = "BOTH"
    scene.display.shading.show_shadows = True
    scene.display.shading.shadow_intensity = 0.6
    scene.display.render_aa = "16"
    scene.render.resolution_x = 1920
    scene.render.resolution_y = 1080
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "Standard"

    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=40)
    mesh = bpy.data.meshes.new("Preview_Ground")
    bm.to_mesh(mesh)
    bm.free()
    ground = bpy.data.objects.new("Preview_Ground", mesh)
    ground_mat = bpy.data.materials.new("Preview_Ground")
    ground_mat.diffuse_color = (0.3, 0.31, 0.33, 1)
    mesh.materials.append(ground_mat)
    scene.collection.objects.link(ground)

    camera_data = bpy.data.cameras.new("Preview_Camera")
    camera = bpy.data.objects.new("Preview_Camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    return camera


def render(camera, eye, target, lens, path):
    camera.location = U(*eye)
    direction = U(*target) - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    camera.data.lens = lens
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0

    os.makedirs(FBX_DIR, exist_ok=True)

    objects = []
    for make in PIECES:
        obj = make()
        path = export_fbx(obj)
        dims = obj.dimensions
        print(f"KIT {obj.name}: {len(obj.data.polygons)} fete, "
              f"{dims.x:.2f} x {dims.z:.2f} x {dims.y:.2f} m (x, y, z Unity) -> {os.path.relpath(path, REPO)}")
        objects.append(obj)

    if "--no-preview" not in ARGS or "--no-blend" not in ARGS:
        arrange(objects)
        camera = setup_preview_scene()

        if "--no-preview" not in ARGS:
            os.makedirs(PREVIEW_DIR, exist_ok=True)
            render(camera, (-17, 9, -19), (0.5, 1.0, 1.5), 32, os.path.join(PREVIEW_DIR, "K17_Kit_Overview.png"))
            render(camera, (-4.5, 3.2, -9.5), (1.0, 1.2, -1.0), 30, os.path.join(PREVIEW_DIR, "K17_Kit_Props.png"))
            render(camera, (-14.5, 3.0, 9.0), (-8.0, 1.3, 5.0), 35, os.path.join(PREVIEW_DIR, "K17_Kit_ContainerDoors.png"))

        if "--no-blend" not in ARGS:
            bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH, compress=True)

    print("KIT_DONE", len(objects))


main()
