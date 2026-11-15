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
    "Black": (0.05, 0.05, 0.05),
    "SteelDark": (0.2, 0.21, 0.23),
    "Hesco": (0.58, 0.52, 0.4),
    "Soil": (0.33, 0.27, 0.2),
    "Gravel": (0.4, 0.39, 0.37),
    "Rust": (0.45, 0.25, 0.15),
    "PipePaintGrey": (0.48, 0.5, 0.5),
    "LampGlow": (1.0, 0.95, 0.85),
    "MilitaryPaint": (0.32, 0.36, 0.26),
    "Canvas": (0.42, 0.4, 0.3),
    "Ceramic": (0.55, 0.38, 0.28),
    "Rubber": (0.06, 0.06, 0.06),
    "Paint": (0.5, 0.52, 0.5),
    "ReactorPaint": (0.82, 0.8, 0.72),
    "TankPaint": (0.45, 0.55, 0.5),
    "MotorPaint": (0.2, 0.35, 0.55),
    "TurbinePaint": (0.55, 0.6, 0.62),
    "PipePaint": (0.55, 0.2, 0.12),
    "ReactorGlow": (0.25, 0.65, 1.0),
    "PoolGlow": (0.1, 0.55, 0.95),
    "BeaconGlow": (1.0, 0.5, 0.05),
    "EmergencyGlow": (1.0, 0.1, 0.05),
    "ScreenGlow": (0.25, 0.95, 0.55),
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

    def lathe(self, center, profile, slot, segments=32, axis="y"):
        """Corp de revolutie: profile = [(raza, inaltime)] de jos in sus, rotit in jurul axei
        (y vertical sau z orizontal) prin `center`. Capetele cu raza > 0 se inchid."""
        index = self.slot(slot)
        cx, cy, cz = center
        rings = []

        for r, h in profile:
            ring = []
            for k in range(segments):
                a = 2 * math.pi * k / segments
                u, v = math.cos(a) * r, math.sin(a) * r
                if axis == "y":
                    ring.append(self.bm.verts.new(U(cx + u, cy + h, cz + v)))
                elif axis == "x":
                    ring.append(self.bm.verts.new(U(cx + h, cy + u, cz + v)))
                else:
                    ring.append(self.bm.verts.new(U(cx + u, cy + v, cz + h)))
            rings.append(ring)

        faces = []
        for a, b in zip(rings, rings[1:]):
            for k in range(segments):
                f = self.bm.faces.new((a[k], a[(k + 1) % segments], b[(k + 1) % segments], b[k]))
                f.smooth = True
                faces.append(f)

        for ring, (r, _) in ((rings[0], profile[0]), (rings[-1], profile[-1])):
            if r > 0.001:
                faces.append(self.bm.faces.new(ring))

        for face in faces:
            face.material_index = index

    def sector(self, cx, cy, cz, r0, r1, a0, a1, thickness, slot, plane="xz", steps=10):
        """Sector de inel (palete de trifoi). plane 'xz' = culcat pe podea, 'xy' = pe un perete (fata spre +Z)."""
        pts = []
        for k in range(steps + 1):
            a = math.radians(a0 + (a1 - a0) * k / steps)
            pts.append((math.cos(a) * r1, math.sin(a) * r1))
        for k in range(steps, -1, -1):
            a = math.radians(a0 + (a1 - a0) * k / steps)
            pts.append((math.cos(a) * r0, math.sin(a) * r0))

        if plane == "xz":
            base = [(cx + u, cy, cz + v) for u, v in pts]
            self.prism(base, (0, thickness, 0), slot)
        else:
            base = [(cx + u, cy + v, cz) for u, v in pts]
            self.prism(base, (0, 0, thickness), slot)

    def trefoil(self, cx, cy, cz, scale, thickness, slot, plane="xz"):
        """Simbolul de radiatii: trei palete de 60 de grade si un disc in centru."""
        for k in range(3):
            a0 = 90 + k * 120 - 30
            self.sector(cx, cy, cz, 0.3 * scale, 1.0 * scale, a0, a0 + 60, thickness, slot, plane)
        if plane == "xz":
            self.cylinder((cx, cy, cz), "y", 0.2 * scale, thickness, slot, segments=20)
        else:
            self.cylinder((cx, cy, cz), "z", 0.2 * scale, thickness, slot, segments=20)

    def obox(self, center, size, euler_deg, slot):
        """Cutie rotita: centru si dimensiuni Unity, rotatie (x, y, z) in grade aplicata in ordinea Z, X, Y."""
        index = self.slot(slot)
        rx, ry, rz = (math.radians(a) for a in euler_deg)
        hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2

        def rot(v):
            x, y, z = v
            # Z
            x, y = x * math.cos(rz) - y * math.sin(rz), x * math.sin(rz) + y * math.cos(rz)
            # X
            y, z = y * math.cos(rx) - z * math.sin(rx), y * math.sin(rx) + z * math.cos(rx)
            # Y
            x, z = x * math.cos(ry) + z * math.sin(ry), -x * math.sin(ry) + z * math.cos(ry)
            return x + center[0], y + center[1], z + center[2]

        corners = [rot((sx * hx, sy * hy, sz * hz)) for sy in (-1, 1) for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        verts = [self.bm.verts.new(U(*c)) for c in corners]
        quads = [(0, 1, 2, 3), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
        for q in quads:
            self.bm.faces.new([verts[i] for i in q]).material_index = index

    def tube(self, points, radius, slot, segments=12, caps=True):
        """Teava de-a lungul unei linii frante (puncte Unity); inelele sunt perpendiculare pe directie."""
        index = self.slot(slot)
        pts = [Vector(p) for p in points]
        rings = []

        for i, p in enumerate(pts):
            if i == 0:
                t = (pts[1] - pts[0]).normalized()
            elif i == len(pts) - 1:
                t = (pts[-1] - pts[-2]).normalized()
            else:
                t = ((pts[i + 1] - pts[i]).normalized() + (pts[i] - pts[i - 1]).normalized()).normalized()

            ref = Vector((0, 1, 0)) if abs(t.y) < 0.9 else Vector((1, 0, 0))
            n = t.cross(ref).normalized()
            b = t.cross(n).normalized()
            ring = []
            for k in range(segments):
                a = 2 * math.pi * k / segments
                q = p + (n * math.cos(a) + b * math.sin(a)) * radius
                ring.append(self.bm.verts.new(U(q.x, q.y, q.z)))
            rings.append(ring)

        faces = []
        for r0, r1 in zip(rings, rings[1:]):
            for k in range(segments):
                f = self.bm.faces.new((r0[k], r0[(k + 1) % segments], r1[(k + 1) % segments], r1[k]))
                f.smooth = True
                faces.append(f)
        if caps:
            faces.append(self.bm.faces.new(rings[0]))
            faces.append(self.bm.faces.new(list(reversed(rings[-1]))))
        for f in faces:
            f.material_index = index

    def rod(self, start, end, radius, slot):
        self.tube([start, end], radius, slot, segments=6)

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
        if name.endswith("Glow"):
            material.diffuse_color = (min(r * 1.4, 1), min(g * 1.4, 1), min(b * 1.4, 1), 1)
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
    """Bastion de protectie (cosuri de plasa de otel cu geotextil, umplute cu pamant), 2 x 2 x 0,8.
    Doua randuri de cate doua cosuri de 1 x 1 m; se insiruie pe lungime (cover in picioare)."""
    p = Piece("K17_Barrier_High", bevel=0.01)
    d = 0.4
    wire = 0.006

    for row in range(2):
        y0 = row * 1.0
        for col in range(2):
            x0 = -1.0 + col * 1.0
            # sacul de geotextil, putin bombat (inset fata de plasa)
            p.box(x0 + 0.03, y0 + 0.005, -d + 0.02, x0 + 0.97, y0 + 0.97, d - 0.02, "Hesco")
            # pamantul de deasupra (vizibil doar la randul de sus)
            if row == 1:
                p.box(x0 + 0.06, y0 + 0.97, -d + 0.06, x0 + 0.94, y0 + 0.985, d - 0.06, "Soil")
            # plasa: verticale si orizontale pe fata si pe spate
            for s in (-1, 1):
                zf = s * d
                for k in range(5):
                    x = x0 + 0.02 + k * 0.24
                    p.box(x - wire, y0, zf - s * 0.025, x + wire, y0 + 1.0, zf, "Steel")
                for k in range(5):
                    y = y0 + 0.02 + k * 0.24
                    p.box(x0, y - wire, zf - s * 0.025, x0 + 1.0, y + wire, zf, "Steel")
            # plasa pe capete (doar la marginea sirului)
            for s in (-1, 1):
                xe = x0 + (1.0 if s > 0 else 0.0)
                if (col == 0 and s < 0) or (col == 1 and s > 0):
                    for k in range(4):
                        z = -d + 0.04 + k * 0.24
                        p.box(xe - s * 0.025, y0, z - wire, xe, y0 + 1.0, z + wire, "Steel")
                    for k in range(5):
                        y = y0 + 0.02 + k * 0.24
                        p.box(xe - s * 0.025, y - wire, -d, xe, y + wire, d, "Steel")
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


# ----------------------------------------------------------------------------------------------
# Instalatia nucleara (v2)
# ----------------------------------------------------------------------------------------------

def piece_reactor_vessel():
    """Vasul reactorului: incape exact in rezervorul A_Tank_Main (cilindru 4 m diametru, 4 m inalt)."""
    p = Piece("K17_Reactor_Vessel", bevel=0.01)
    # fusta de sprijin, corp, flansa, capac bombat
    p.lathe((0, 0, 0), [(1.95, 0.0), (1.95, 0.3), (1.75, 0.3), (1.75, 0.45)], "Steel", 40)
    p.lathe((0, 0, 0), [(1.8, 0.4), (1.8, 3.15)], "ReactorPaint", 40)
    p.lathe((0, 0, 0), [(1.98, 3.15), (1.98, 3.42)], "Steel", 40)
    p.lathe((0, 0, 0), [(1.78, 3.42), (1.7, 3.6), (1.45, 3.78), (1.0, 3.92), (0.0, 3.98)], "ReactorPaint", 40)
    # prezoane pe flansa
    for k in range(28):
        a = 2 * math.pi * k / 28
        p.cylinder((math.cos(a) * 1.88, 3.42, math.sin(a) * 1.88), "y", 0.045, 0.14, "Steel", segments=8)
    # benzi de ranforsare si o fanta cu lumina Cerenkov
    for y in (1.0, 2.2):
        p.lathe((0, 0, 0), [(1.84, y), (1.84, y + 0.12)], "Steel", 40)
    p.lathe((0, 0, 0), [(1.815, 1.45), (1.815, 1.75)], "ReactorGlow", 40)
    # stuturi de racire (radiale)
    for k in range(4):
        a = math.radians(45 + 90 * k)
        dx, dz = math.cos(a), math.sin(a)
        for r0, r1, rad in ((1.75, 1.97, 0.32), (1.9, 1.98, 0.38)):
            pts = []
            for s in range(16):
                b = 2 * math.pi * s / 16
                u, v = math.cos(b) * rad, math.sin(b) * rad
                # cerc perpendicular pe directia radiala
                pts.append((dx * r0 - dz * u, 2.6 + v, dz * r0 + dx * u))
            p.prism(pts, (dx * (r1 - r0), 0, dz * (r1 - r0)), "Steel")
    # stuturi pentru barele de control pe capac
    for x, z in ((0, 0), (0.55, 0), (-0.55, 0), (0, 0.55), (0, -0.55), (0.4, 0.4), (-0.4, 0.4), (0.4, -0.4), (-0.4, -0.4)):
        p.cylinder((x, 3.7, z), "y", 0.08, 0.29, "Steel", segments=10)
    return p.build()


def piece_control_rod_drive():
    """Mecanismul barelor de control: atarna din tavan deasupra reactorului (decor, 2,1 m)."""
    p = Piece("K17_Control_Rod_Drive", bevel=0.006)
    p.lathe((0, 0, 0), [(1.5, 0.0), (1.5, 0.18)], "Steel", 32)
    p.lathe((0, 0, 0), [(1.2, 0.18), (1.2, 0.3)], "Hazard", 32)
    for x, z in ((0, 0), (0.55, 0), (-0.55, 0), (0, 0.55), (0, -0.55), (0.4, 0.4), (-0.4, 0.4), (0.4, -0.4), (-0.4, -0.4)):
        p.cylinder((x, 0.3, z), "y", 0.07, 1.5, "Steel", segments=10)
        p.box(x - 0.09, 1.1, z - 0.09, x + 0.09, 1.3, z + 0.09, "ReactorPaint")
    p.lathe((0, 0, 0), [(1.1, 1.8), (1.1, 1.95)], "Steel", 32)
    p.box(-1.3, 1.95, -0.15, 1.3, 2.1, 0.15, "Steel")
    p.box(-0.15, 1.95, -1.3, 0.15, 2.1, 1.3, "Steel")
    # cabluri spre tavan
    for k in range(6):
        a = 2 * math.pi * k / 6
        p.cylinder((math.cos(a) * 0.9, 0.3, math.sin(a) * 0.9), "y", 0.025, 1.8, "Rubber", segments=6)
    return p.build()


def piece_pressurizer():
    """Presurizor: incape in A_Tank_Small (3,5 m diametru, 3,4 m)."""
    p = Piece("K17_Pressurizer", bevel=0.008)
    for k in range(4):
        a = math.radians(45 + 90 * k)
        x, z = math.cos(a) * 1.25, math.sin(a) * 1.25
        p.box(x - 0.12, 0.0, z - 0.12, x + 0.12, 0.7, z + 0.12, "Steel")
    p.lathe((0, 0, 0), [(0.0, 0.45), (0.9, 0.5), (1.2, 0.65), (1.3, 0.85), (1.3, 2.75),
                        (1.2, 2.98), (0.9, 3.15), (0.0, 3.22)], "ReactorPaint", 36)
    p.lathe((0, 0, 0), [(1.34, 1.6), (1.34, 1.72)], "Hazard", 36)
    p.cylinder((0, 3.15, 0), "y", 0.22, 0.25, "Steel", segments=12)
    # scara pe o parte
    for x in (-0.25, 0.25):
        p.box(x - 0.03, 0.7, 1.42, x + 0.03, 3.0, 1.48, "Steel")
    for k in range(8):
        y = 0.9 + k * 0.28
        p.box(-0.25, y, 1.43, 0.25, y + 0.03, 1.47, "Steel")
    for x in (-0.25, 0.25):
        p.box(x - 0.02, 0.9, 1.3, x + 0.02, 0.94, 1.45, "Steel")
        p.box(x - 0.02, 2.7, 1.3, x + 0.02, 2.74, 1.45, "Steel")
    return p.build()


def piece_coolant_tank():
    """Rezervor de racire: incape in Pumps_Tank_* (3 m diametru, 3,2 m)."""
    p = Piece("K17_Coolant_Tank", bevel=0.008)
    p.lathe((0, 0, 0), [(1.5, 0.0), (1.5, 0.15), (1.42, 0.15), (1.42, 2.9), (1.2, 3.1), (0.0, 3.18)], "TankPaint", 36)
    for y in (0.6, 1.5, 2.4):
        p.lathe((0, 0, 0), [(1.46, y), (1.46, y + 0.08)], "Steel", 36)
    # semn de radiatii pe fata (+Z)
    p.box(-0.3, 1.7, 1.38, 0.3, 2.3, 1.44, "Hazard")
    p.trefoil(0, 2.0, 1.44, 0.22, 0.012, "Black", plane="xy")
    # vana si teava scurta
    p.cylinder((0.8, 0.5, 1.15), "z", 0.12, 0.3, "Steel", segments=12)
    p.cylinder((0.8, 0.5, 1.45), "z", 0.18, 0.04, "Hazard", segments=16)
    return p.build()


def piece_coolant_pump():
    """Pompa centrifuga de racire pe sasiu, 4 x 2,5 x 4 (A_Generator, Pumps_Machine, Yard_Generator).
    Motorul si pompa sunt pe acelasi ax (X); aspiratia intra prin podea, refularea urca si se termina
    intr-o vana cu roata de manevra; dulapul de comanda e legat de motor prin tub de cablu."""
    p = Piece("K17_Coolant_Pump", bevel=0.008)
    axis_y = 0.95

    # sasiu pe toata amprenta: rama, tabla striata, borduri galbene
    p.box(-2.0, 0.0, -2.0, 2.0, 0.12, 2.0, "Steel")
    for z in (-2.0, 1.88):
        p.box(-2.0, 0.12, z, 2.0, 0.24, z + 0.12, "Hazard")
    p.box(-2.0, 0.12, -1.88, 2.0, 0.2, 1.88, "SteelDark")

    def pump_set(dz):
        """Motor + cuplaj + pompa pe acelasi ax (X), centrat pe z = dz."""
        # motorul electric: carcasa cu nervuri, ventilatorul in spate, cutia de borne, picioare
        p.cylinder((0.15, axis_y, dz), "x", 0.44, 1.35, "MotorPaint", segments=28)
        for k in range(12):
            a = 2 * math.pi * k / 12
            cy, cz = axis_y + math.sin(a) * 0.45, dz + math.cos(a) * 0.45
            p.box(0.2, cy - 0.025, cz - 0.025, 1.45, cy + 0.025, cz + 0.025, "MotorPaint")
        p.cylinder((1.5, axis_y, dz), "x", 0.4, 0.22, "SteelDark", segments=24)
        for x0 in (0.3, 1.1):
            p.box(x0, 0.2, dz - 0.38, x0 + 0.18, axis_y - 0.3, dz + 0.38, "Steel")
        p.box(0.55, axis_y + 0.4, dz - 0.18, 0.95, axis_y + 0.62, dz + 0.18, "MotorPaint")

        # cuplajul sub aparatoare galbena
        p.box(-0.45, axis_y - 0.22, dz - 0.22, 0.15, axis_y + 0.22, dz + 0.22, "Hazard")

        # lagarul si corpul spiral al pompei, pe postament
        p.cylinder((-0.85, axis_y, dz), "x", 0.2, 0.4, "Steel", segments=16)
        p.box(-1.05, 0.2, dz - 0.3, -0.5, axis_y - 0.15, dz + 0.3, "Steel")
        p.cylinder((-1.35, axis_y, dz), "x", 0.62, 0.5, "TankPaint", segments=32)
        p.cylinder((-1.4, axis_y, dz), "x", 0.68, 0.08, "Steel", segments=32)
        p.box(-1.55, 0.2, dz - 0.4, -1.0, axis_y - 0.45, dz + 0.4, "Steel")

        # aspiratia: din fata pompei spre -X, apoi in jos prin podea (cu flansa la podea)
        p.cylinder((-1.85, axis_y, dz), "x", 0.18, 0.5, "PipePaint", segments=16)
        p.cylinder((-1.75, 0.0, dz), "y", 0.18, axis_y + 0.18, "PipePaint", segments=16)
        p.cylinder((-1.75, 0.2, dz), "y", 0.27, 0.06, "Steel", segments=16)
        p.cylinder((-1.75, axis_y - 0.32, dz), "y", 0.25, 0.06, "Steel", segments=16)

        # refularea: urca din varful pompei pana in colector; vana cu roata de manevra laterala
        top = axis_y + 0.62
        p.cylinder((-1.35, top, dz), "y", 0.13, header_y - top, "PipePaint", segments=16)
        p.cylinder((-1.35, top + 0.08, dz), "y", 0.2, 0.05, "Steel", segments=16)
        p.box(-1.52, top + 0.18, dz - 0.15, -1.18, top + 0.36, dz + 0.15, "Steel")
        p.cylinder((-1.72, top + 0.27, dz), "x", 0.025, 0.2, "Steel", segments=8)
        p.lathe((-1.76, top + 0.27, dz), [(0.18, 0.0), (0.18, 0.03), (0.14, 0.03), (0.14, 0.0)], "Hazard", 20, axis="x")

    header_y = 2.12
    pump_set(-0.95)
    pump_set(0.95)

    # colectorul comun de refulare: leaga cele doua pompe si coboara prin podea la capatul sasiului
    p.cylinder((-1.35, header_y, -1.1), "z", 0.14, 2.8, "PipePaint", segments=16)
    p.cylinder((-1.35, 0.0, 1.7), "y", 0.14, header_y, "PipePaint", segments=16)
    p.lathe((-1.35, header_y, 1.7), [(0.0, 0.0), (0.15, 0.0), (0.15, 0.08), (0.0, 0.15)], "PipePaint", 16)
    p.cylinder((-1.35, 0.2, 1.7), "y", 0.22, 0.05, "Steel", segments=16)
    p.cylinder((-1.35, header_y, -1.18), "z", 0.19, 0.06, "Steel", segments=16)

    # dulapul de comanda intre cele doua motoare, cu tuburi de cablu spre ele
    p.box(1.25, 0.2, -0.32, 1.95, 2.2, 0.32, "Paint")
    p.box(1.96, 1.65, -0.22, 1.98, 1.95, 0.22, "ScreenGlow")
    for k in range(3):
        p.box(1.96, 1.35, -0.18 + k * 0.13, 1.99, 1.45, -0.1 + k * 0.13, "Hazard" if k == 0 else "Black")
    # tuburi de cablu de la dulap la cutiile de borne ale motoarelor (pe sasiu)
    for dz in (-0.95, 0.95):
        p.box(0.72, 0.2, min(dz, 0.0) - 0.03, 0.78, 0.26, max(dz, 0.0) + 0.03, "Black")
    p.box(0.72, 0.2, -0.03, 1.25, 0.26, 0.03, "Black")
    return p.build()


def piece_turbine():
    """Turbina cu abur si generator, 6 x 3,6 x 8 (Hangar_Machine), lunga pe Z."""
    p = Piece("K17_Turbine", bevel=0.012)
    p.box(-3.0, 0.0, -4.0, 3.0, 0.7, 4.0, "Concrete")
    p.box(-2.0, 0.7, -3.8, 2.0, 0.95, 3.8, "Steel")
    # carcasa turbinei: trepte de joasa / inalta presiune
    p.lathe((0.0, 2.0, 0.0), [(0.0, -3.8), (0.9, -3.8), (1.1, -3.6), (1.45, -3.2), (1.55, -1.2), (1.3, -0.9),
                               (1.3, -0.6)], "TurbinePaint", 36, axis="z")
    p.lathe((0.0, 2.0, 0.0), [(1.0, -0.6), (1.0, 0.0)], "Steel", 36, axis="z")
    # generatorul
    p.lathe((0.0, 2.0, 0.0), [(1.2, 0.0), (1.35, 0.15), (1.35, 3.4), (1.1, 3.6), (0.0, 3.6)], "MotorPaint", 36, axis="z")
    for k in range(10):
        z = 0.4 + k * 0.3
        p.lathe((0.0, 2.0, 0.0), [(1.4, z), (1.4, z + 0.06)], "Steel", 36, axis="z")
    # flanse
    for z in (-3.2, -1.2):
        p.lathe((0.0, 2.0, 0.0), [(1.62, z), (1.62, z + 0.12)], "Steel", 36, axis="z")
    # conducte de abur care intra in carcasa de sus
    for z in (-2.6, -1.8):
        p.cylinder((0.0, 3.4, z), "y", 0.28, 0.2, "Steel", segments=16)
        p.cylinder((-1.6, 3.35, z), "x", 0.22, 1.6, "Steel", segments=16)
        p.cylinder((-2.6, 0.95, z), "y", 0.22, 2.62, "Steel", segments=16)
    # pasarela si balustrada pe o parte
    p.box(1.9, 0.95, -3.6, 2.9, 1.0, 3.6, "Steel")
    for z in (-3.5, -1.0, 1.5, 3.5):
        p.box(2.84, 1.0, z - 0.03, 2.9, 2.0, z + 0.03, "Hazard")
    p.box(2.84, 1.95, -3.5, 2.9, 2.0, 3.5, "Hazard")
    return p.build()


def piece_control_panels():
    """Pupitre de comanda si dulapuri cu ecrane, 6 x 2,2 x 3 (Lab_ServerRack); ecranele spre -Z si +Z."""
    p = Piece("K17_Control_Panels", bevel=0.008)
    # rand de dulapuri in mijloc
    for k in range(6):
        x0 = -3.0 + k * 1.0
        p.box(x0 + 0.02, 0.0, -0.45, x0 + 0.98, 2.2, 0.45, "Paint")
        for s in (-1, 1):
            z = s * 0.45
            p.box(x0 + 0.15, 1.4, z, x0 + 0.85, 1.95, z + s * 0.015, "ScreenGlow" if k % 2 == 0 else "Glass")
            for r in range(4):
                p.box(x0 + 0.15 + r * 0.18, 1.1, z, x0 + 0.27 + r * 0.18, 1.2, z + s * 0.02,
                      "Hazard" if (k + r) % 3 == 0 else "Steel")
    # pupitre inclinate pe ambele parti
    for s in (-1, 1):
        z0, z1 = s * 0.45, s * 1.5
        prof = [(z0, 0.0), (z1, 0.0), (z1, 0.75), (s * 1.3, 0.95), (z0, 1.0)]
        p.prism([(-2.9, y, z) for z, y in prof], (5.8, 0, 0), "Paint")
        for k in range(5):
            x = -2.6 + k * 1.15
            p.box(x, 0.86, s * 1.22, x + 0.7, 0.9, s * 1.42, "ScreenGlow")
    return p.build()


def piece_hazmat_lockers():
    """Dulapuri cu costume de protectie, 1 x 2,2 x 4 (Security_Lockers); usile spre +X."""
    p = Piece("K17_Hazmat_Lockers", bevel=0.006)
    for k in range(4):
        z0 = -2.0 + k * 1.0
        p.box(-0.5, 0.0, z0 + 0.01, 0.42, 2.2, z0 + 0.99, "Paint")
        p.box(0.42, 0.05, z0 + 0.05, 0.47, 2.15, z0 + 0.95, "Hazard" if k == 0 else "Paint")
        for v in range(5):
            y = 1.7 + v * 0.06
            p.box(0.47, y, z0 + 0.3, 0.48, y + 0.025, z0 + 0.7, "Steel")
        p.box(0.47, 1.0, z0 + 0.82, 0.5, 1.2, z0 + 0.86, "Steel")
        # eticheta galbena pe fiecare usa
        p.box(0.47, 1.3, z0 + 0.35, 0.48, 1.45, z0 + 0.65, "Hazard")
    # casti de protectie pe dulapuri
    for k in range(4):
        z = -1.5 + k * 1.0
        p.lathe((0.0, 2.2, z), [(0.0, -0.001), (0.16, 0.0), (0.17, 0.06), (0.14, 0.16), (0.0, 0.19)], "Hazard", 16)
    return p.build()


def piece_shelf():
    """Raft industrial de 2 m cu butoaie si cutii (Depot_Shelf, Office_Shelf; 1 x 2,2, insirat pe Z)."""
    p = Piece("K17_Shelf", bevel=0.005)
    for x in (-0.5, 0.45):
        for z in (-0.98, 0.93):
            p.box(x, 0.0, z, x + 0.05, 2.2, z + 0.05, "Hazard")
    for y in (0.08, 0.8, 1.5, 2.12):
        p.box(-0.5, y, -0.98, 0.5, y + 0.06, 0.98, "Steel")
    # pe rafturi: butoaie mici si cutii
    for y, items in ((0.14, "bb"), (0.86, "cc"), (1.56, "bc")):
        for k, kind in enumerate(items):
            z = -0.45 + k * 0.9
            if kind == "b":
                for dz in (-0.2, 0.2):
                    p.cylinder((0.0, y, z + dz), "y", 0.17, 0.55, "BarrelPaint", segments=16)
            else:
                p.box(-0.35, y, z - 0.35, 0.3, y + 0.42, z + 0.35, "WoodDark")
    return p.build()


def piece_rubble():
    """Stiva de placi de beton prefabricate, depozitata pe santier, 1 x 1 x 1 (se intinde pe cutia
    obiectului din generator, inclusiv cand e rotita): placi pe distantiere de lemn, chingi, urechi de ridicare."""
    p = Piece("K17_Rubble", bevel=0.01)
    layers = 4
    gap = 0.03
    h = (1.0 - gap * layers) / layers
    y = 0.0
    for k in range(layers):
        # distantiere de lemn sub fiecare placa
        for x in (-0.32, 0.32):
            p.box(x - 0.05, y, -0.48, x + 0.05, y + gap, 0.48, "WoodDark")
        y += gap
        inset = 0.0 if k % 2 == 0 else 0.02
        p.box(-0.5 + inset, y, -0.5 + inset, 0.5 - inset, y + h, 0.5 - inset, "Concrete" if k % 2 == 0 else "ConcreteDark")
        # urechi de ridicare pe placa de sus
        if k == layers - 1:
            for x, z in ((-0.3, -0.3), (0.3, -0.3), (0.3, 0.3), (-0.3, 0.3)):
                p.lathe((x, y + h, z), [(0.035, 0.0), (0.035, 0.03), (0.0, 0.03)], "Rust", 8)
        y += h
    # chingi galbene peste stiva
    for x in (-0.18, 0.18):
        p.box(x - 0.03, 0.0, -0.505, x + 0.03, 1.0, -0.495, "Hazard")
        p.box(x - 0.03, 0.0, 0.495, x + 0.03, 1.0, 0.505, "Hazard")
        p.box(x - 0.03, 0.995, -0.5, x + 0.03, 1.003, 0.5, "Hazard")
    return p.build()


def piece_razor_wire():
    """Sarma ghimpata in spirala (concertina), segment de 2 m pe X, pe coronamentul zidului de incinta.
    Pivot pe varful zidului, la mijloc; stalpii in V tin spirala."""
    p = Piece("K17_Razor_Wire", bevel=0.0)
    turns = 10
    r = 0.28
    pts = []
    for k in range(turns * 12 + 1):
        a = 2 * math.pi * k / 12
        x = -1.0 + 2.0 * k / (turns * 12)
        pts.append((x, 0.42 + math.sin(a) * r, math.cos(a) * r))
    p.tube(pts, 0.008, "Steel", segments=4, caps=False)
    for x in (-0.9, 0.9):
        p.rod((x, 0.0, 0.0), (x, 0.7, -0.25), 0.015, "SteelDark")
        p.rod((x, 0.0, 0.0), (x, 0.7, 0.25), 0.015, "SteelDark")
    return p.build()


def piece_truck():
    """Camion militar cu prelata, 2,5 x 3 x 6,5 (lung pe Z, cabina spre +Z)."""
    p = Piece("K17_Truck", bevel=0.02)
    # sasiu
    p.box(-0.55, 0.55, -3.2, 0.55, 0.8, 3.1, "SteelDark")
    # roti (3 axe) cu jante
    for z in (2.2, -0.9, -2.3):
        for x0 in (-1.25, 0.85):
            p.cylinder((x0, 0.55, z), "x", 0.55, 0.4, "Rubber", segments=18)
            hub = x0 - 0.02 if x0 < 0 else x0 + 0.18
            p.cylinder((hub, 0.55, z), "x", 0.3, 0.24, "SteelDark", segments=12)
    # cabina
    p.box(-1.2, 0.8, 1.4, 1.2, 2.7, 3.2, "MilitaryPaint")
    p.box(-1.15, 1.85, 3.19, 1.15, 2.55, 3.22, "Glass")
    for s in (-1, 1):
        p.box(s * 1.2, 1.85, 1.6, s * 1.22, 2.5, 2.8, "Glass")
        p.box(s * 1.21, 0.9, 1.5, s * 1.23, 1.75, 2.9, "MilitaryPaint")
    p.box(-1.2, 0.8, 3.2, 1.2, 1.3, 3.35, "SteelDark")           # bara de protectie
    for s in (-1, 1):
        p.box(s * 0.9, 1.0, 3.33, s * 0.6, 1.2, 3.36, "LampGlow")
    # bena cu prelata
    p.box(-1.25, 0.8, -3.25, 1.25, 1.4, 1.3, "MilitaryPaint")
    p.box(-1.25, 1.4, -3.25, 1.25, 2.6, 1.3, "Canvas")
    p.prism([(-1.25, 2.6, -3.25), (1.25, 2.6, -3.25), (0.9, 3.0, -3.25), (-0.9, 3.0, -3.25)], (0, 0, 4.55), "Canvas")
    for z in (-2.6, -1.4, -0.2, 1.0):
        p.box(-1.27, 1.4, z - 0.04, 1.27, 2.62, z + 0.04, "Canvas")
    return p.build()

def piece_sign_radiation():
    """Semn de radiatii pe perete, 0,5 x 0,6; pivot pe perete, fata spre +Z."""
    p = Piece("K17_Sign_Radiation", bevel=0.003)
    p.box(-0.25, -0.3, 0.0, 0.25, 0.3, 0.015, "Hazard")
    p.box(-0.25, -0.3, 0.015, 0.25, -0.2, 0.02, "Black")
    p.trefoil(0.0, 0.05, 0.015, 0.2, 0.006, "Black", plane="xy")
    return p.build()


def piece_floor_trefoil():
    """Marcaj de radiatii pe podea, 3 m diametru (decor, 5 mm)."""
    p = Piece("K17_Floor_Trefoil", bevel=0.0)
    p.lathe((0, 0, 0), [(0.0, 0.0), (1.5, 0.0), (1.5, 0.004), (0.0, 0.004)], "Hazard", 48)
    p.trefoil(0, 0.004, 0, 1.15, 0.003, "Black")
    return p.build()


def piece_beacon():
    """Girofar portocaliu pe consola de perete; pivot pe perete, iese spre +Z (lumina rotitoare in Unity)."""
    p = Piece("K17_Beacon", bevel=0.004)
    p.box(-0.1, -0.12, 0.0, 0.1, 0.12, 0.02, "Steel")
    p.box(-0.03, -0.03, 0.02, 0.03, 0.03, 0.2, "Steel")
    p.cylinder((0.0, 0.03, 0.2), "y", 0.1, 0.06, "Steel", segments=16)
    p.lathe((0.0, 0.09, 0.2), [(0.085, 0.0), (0.085, 0.12), (0.06, 0.17), (0.0, 0.19)], "BeaconGlow", 16)
    return p.build()


def piece_emergency_light():
    """Lampa de urgenta rosie cu gratar, pe perete; pivot pe perete, fata spre +Z."""
    p = Piece("K17_Emergency_Light", bevel=0.004)
    p.box(-0.18, -0.1, 0.0, 0.18, 0.1, 0.06, "Steel")
    p.lathe((0.0, 0.0, 0.06), [(0.0, 0.0), (0.14, 0.0), (0.14, 0.04)], "Steel", 16, axis="z")
    p.lathe((0.0, 0.0, 0.1), [(0.11, 0.0), (0.11, 0.05), (0.07, 0.09), (0.0, 0.1)], "EmergencyGlow", 16, axis="z")
    for x in (-0.06, 0.0, 0.06):
        p.box(x - 0.008, -0.12, 0.1, x + 0.008, 0.12, 0.2, "Steel")
    return p.build()


def piece_pipe_run():
    """Trei conducte pe console, segment de 2 m pe X; pivot pe perete (jos), iese spre +Z."""
    p = Piece("K17_Pipe_Run", bevel=0.0)
    p.cylinder((-1.0, 0.18, 0.2), "x", 0.14, 2.0, "PipePaint", segments=16)
    p.cylinder((-1.0, 0.5, 0.18), "x", 0.1, 2.0, "TankPaint", segments=14)
    p.cylinder((-1.0, 0.75, 0.15), "x", 0.07, 2.0, "Hazard", segments=12)
    for x in (-0.6, 0.6):
        p.box(x - 0.04, 0.0, 0.0, x + 0.04, 0.9, 0.06, "Steel")
        for y, d in ((0.05, 0.38), (0.4, 0.32), (0.66, 0.25)):
            p.box(x - 0.03, y, 0.0, x + 0.03, y + 0.04, d, "Steel")
    # manson la mijloc
    p.cylinder((-0.06, 0.18, 0.2), "x", 0.165, 0.12, "Steel", segments=16)
    return p.build()


def piece_decon_arch():
    """Cadru de decontaminare pentru o usa de 1,4 x 2,4: dusuri pe bara de sus, stalpi pe margini.
    Latimea pe X (se scaleaza dupa usa), adancimea pe Z."""
    p = Piece("K17_Decon_Arch", bevel=0.004)
    w = 0.7
    for s in (-1, 1):
        x = s * (w + 0.06)
        p.box(x - 0.06, 0.0, -0.06, x + 0.06, 2.55, 0.06, "Hazard")
        p.box(x - 0.065, 0.3, -0.065, x + 0.065, 0.45, 0.065, "Paint")
        p.box(x - 0.065, 1.2, -0.065, x + 0.065, 1.35, 0.065, "Paint")
        # duze laterale
        for y in (0.6, 1.1, 1.6, 2.1):
            start = x + 0.06 if s < 0 else x - 0.11
            p.cylinder((start, y, 0.0), "x", 0.02, 0.05, "Steel", segments=6)
    p.box(-(w + 0.12), 2.45, -0.08, w + 0.12, 2.6, 0.08, "Hazard")
    for k in range(5):
        x = -0.5 + k * 0.25
        p.cylinder((x, 2.36, 0.0), "y", 0.03, 0.09, "Steel", segments=8)
    p.cylinder((-(w + 0.12), 2.62, 0.0), "x", 0.03, 2 * (w + 0.12), "PipePaint", segments=8)
    # gratar pe podea
    p.box(-w, 0.0, -0.5, w, 0.025, 0.5, "Steel")
    for k in range(9):
        z = -0.45 + k * 0.1125
        p.box(-w, 0.025, z, w, 0.035, z + 0.03, "Paint")
    return p.build()


def piece_fuel_pool():
    """Bazin de combustibil uzat sub gratar, 6 x 4 (decor la nivelul podelii, lumina albastra)."""
    p = Piece("K17_Fuel_Pool", bevel=0.0)
    p.box(-3.0, 0.0, -2.0, 3.0, 0.004, 2.0, "PoolGlow")
    p.box(-3.0, 0.004, -2.0, 3.0, 0.03, -1.9, "Hazard")
    p.box(-3.0, 0.004, 1.9, 3.0, 0.03, 2.0, "Hazard")
    p.box(-3.0, 0.004, -1.9, -2.9, 0.03, 1.9, "Hazard")
    p.box(2.9, 0.004, -1.9, 3.0, 0.03, 1.9, "Hazard")
    for k in range(27):
        x = -2.85 + k * 0.22
        p.box(x, 0.004, -1.9, x + 0.04, 0.03, 1.9, "Steel")
    for z in (-0.95, 0.0, 0.95):
        p.box(-2.9, 0.004, z - 0.03, 2.9, 0.03, z + 0.03, "Steel")
    # elementele de combustibil vazute prin gratar
    for i in range(6):
        for j in range(3):
            x, z = -2.2 + i * 0.88, -1.0 + j * 1.0
            p.box(x - 0.2, 0.002, z - 0.2, x + 0.2, 0.003, z + 0.2, "Steel")
    return p.build()


# ----------------------------------------------------------------------------------------------
# v3: interioare, acoperisuri, conducte cu capete logice, fatade
# ----------------------------------------------------------------------------------------------

def piece_console():
    """Pupitru de comanda inclinat, segment de 2 m pe X (1,1 x 1,2); operatorul sta pe -Z.
    Inlocuieste mesele (Crate_*) din laborator, birouri si camera de control."""
    p = Piece("K17_Console", bevel=0.006)
    # dulapul de jos, retras la picioare
    p.box(-0.98, 0.0, -0.45, 0.98, 0.08, 0.6, "SteelDark")
    p.box(-0.98, 0.08, -0.4, 0.98, 0.72, 0.6, "Paint")
    # blatul inclinat: de la 0,78 m (fata) la 1,1 m (spate)
    prof = [(-0.6, 0.72), (0.6, 0.72), (0.6, 1.1), (0.25, 1.1), (-0.6, 0.8)]
    p.prism([(-0.98, y, z) for z, y in prof], (1.96, 0, 0), "Paint")
    # ecrane in panta si un rand de butoane
    import math as _m
    slope = _m.degrees(_m.atan2(1.1 - 0.8, 0.25 + 0.6))
    for x in (-0.55, 0.0, 0.55):
        p.obox((x, 0.97, -0.12), (0.42, 0.012, 0.3), (-slope, 0.0, 0.0), "ScreenGlow")
    for k in range(8):
        x = -0.8 + k * 0.23
        p.obox((x, 0.83, -0.48), (0.08, 0.02, 0.05), (-slope, 0.0, 0.0), "Hazard" if k % 3 == 0 else "Black")
    # rama metalica pe muchii
    p.box(-0.99, 0.0, -0.46, -0.96, 1.1, 0.61, "Steel")
    p.box(0.96, 0.0, -0.46, 0.99, 1.1, 0.61, "Steel")
    return p.build()


def piece_hvac():
    """Unitate de ventilatie pe acoperis, 3 x 1,6 x 2 (cover): carcasa cu lamele, doua ventilatoare sus."""
    p = Piece("K17_HVAC", bevel=0.01)
    for x in (-1.4, 1.3):
        p.box(x, 0.0, -1.0, x + 0.1, 0.12, 1.0, "SteelDark")
    p.box(-1.5, 0.12, -0.98, 1.5, 1.45, 0.98, "Paint")
    p.box(-1.5, 1.45, -1.0, 1.5, 1.5, 1.0, "Steel")
    # lamele pe laturile lungi
    for s in (-1, 1):
        z = s * 0.98
        for k in range(9):
            y = 0.25 + k * 0.12
            p.box(-1.35, y, z, 1.35, y + 0.04, z + s * 0.025, "SteelDark")
    # ventilatoare cu grila
    for x in (-0.72, 0.72):
        p.lathe((x, 1.5, 0.0), [(0.62, 0.0), (0.62, 0.08), (0.56, 0.08), (0.56, 0.02)], "Steel", 24)
        for k in range(5):
            a = k * 36
            p.obox((x, 1.56, 0.0), (1.1, 0.02, 0.03), (0.0, a, 0.0), "SteelDark")
        p.cylinder((x, 1.5, 0.0), "y", 0.12, 0.08, "SteelDark", segments=12)
    # tub de cablu care coboara in acoperis
    p.tube([(1.5, 0.9, 0.5), (1.62, 0.9, 0.5), (1.62, 0.0, 0.5)], 0.03, "Black", segments=6)
    return p.build()


def piece_pipe_big():
    """Tronson de conducta mare, 3 m pe Y, diametru 1 (se scaleaza dupa teava din generator);
    pivot in centrul tronsonului; flanse cu suruburi la ambele capete."""
    p = Piece("K17_Pipe_Big", bevel=0.0)
    p.cylinder((0.0, -1.5, 0.0), "y", 0.5, 3.0, "PipePaint", segments=24)
    for y0 in (-1.5, 1.42):
        p.cylinder((0.0, y0, 0.0), "y", 0.6, 0.08, "Steel", segments=24)
        for k in range(10):
            a = 2 * math.pi * k / 10
            p.cylinder((math.cos(a) * 0.55, y0 - 0.02, math.sin(a) * 0.55), "y", 0.025, 0.12, "SteelDark", segments=6)
    # banda de identificare (sageata de sens) la mijloc
    p.cylinder((0.0, -0.15, 0.0), "y", 0.505, 0.3, "Hazard", segments=24)
    return p.build()


def piece_pipe_cap():
    """Flansa oarba la capatul liber al unei conducte mari (diametru 1); pivot pe axa, normala +Y."""
    p = Piece("K17_Pipe_Cap", bevel=0.0)
    p.cylinder((0.0, 0.0, 0.0), "y", 0.62, 0.1, "Steel", segments=24)
    for k in range(10):
        a = 2 * math.pi * k / 10
        p.cylinder((math.cos(a) * 0.55, 0.08, math.sin(a) * 0.55), "y", 0.03, 0.05, "SteelDark", segments=6)
    return p.build()


def piece_pipe_collar():
    """Trecere prin perete pentru o conducta mare (diametru 1): placa patrata pe perete si manson;
    pivot pe axa conductei, la fata peretelui, conducta vine din +Y."""
    p = Piece("K17_Pipe_Collar", bevel=0.004)
    p.box(-0.8, 0.0, -0.8, 0.8, 0.04, 0.8, "SteelDark")
    p.cylinder((0.0, 0.0, 0.0), "y", 0.6, 0.18, "Steel", segments=24)
    for x, z in ((-0.7, -0.7), (0.7, -0.7), (0.7, 0.7), (-0.7, 0.7)):
        p.cylinder((x, 0.04, z), "y", 0.04, 0.03, "Black", segments=8)
    return p.build()


# Cele trei conducte ale K17_Pipe_Run: (inaltime, distanta de perete, raza, slot)
PIPE_RUN = [(0.18, 0.2, 0.14, "PipePaint"), (0.5, 0.18, 0.1, "TankPaint"), (0.75, 0.15, 0.07, "Hazard")]


def piece_pipe_run_end():
    """Capatul unui sir de conducte de perete: cele trei tevi fac cot de 90 de grade si intra in perete
    (cu rozeta). Sirul vine din -X; pivot pe perete (jos), peretele e planul z = 0."""
    p = Piece("K17_Pipe_Run_End", bevel=0.0)
    for y, d, r, slot in PIPE_RUN:
        arc = [(d * math.sin(math.radians(a)), y, d * math.cos(math.radians(a))) for a in range(0, 91, 10)]
        p.tube([(-0.02, y, d)] + arc, r, slot, segments=16, caps=False)
        p.cylinder((d, y, 0.0), "z", r + 0.04, 0.04, "Steel", segments=16)
    p.box(-0.04, 0.0, 0.0, 0.04, 0.9, 0.06, "Steel")
    return p.build()


def piece_pipe_run_collar():
    """Capatul unui sir de conducte de perete care intra drept intr-un perete perpendicular (planul x = 0):
    placa pe perete si mansoane. Pivot pe peretele lung, la colt."""
    p = Piece("K17_Pipe_Run_Collar", bevel=0.0)
    p.box(-0.03, 0.0, 0.0, 0.0, 0.92, 0.45, "SteelDark")
    for y, d, r, slot in PIPE_RUN:
        p.cylinder((-0.1, y, d), "x", r + 0.035, 0.08, "Steel", segments=16)
    return p.build()


def piece_ladder_section():
    """Tronson de 1 m de scara de mana, pe perete (pivot pe perete, jos; iese spre +Z)."""
    p = Piece("K17_Ladder_Section", bevel=0.0)
    for x in (-0.25, 0.25):
        p.cylinder((x, 0.0, 0.18), "y", 0.024, 1.0, "Hazard", segments=8)
        p.box(x - 0.02, 0.45, 0.0, x + 0.02, 0.5, 0.18, "Steel")
    for k in range(4):
        p.cylinder((-0.25, 0.125 + k * 0.25, 0.18), "x", 0.016, 0.5, "Steel", segments=6)
    return p.build()


def piece_ladder_top():
    """Manerele de sus ale scarii: urca 1 m peste marginea acoperisului si coboara pe acoperis."""
    p = Piece("K17_Ladder_Top", bevel=0.0)
    for x in (-0.25, 0.25):
        p.tube([(x, -0.02, 0.18), (x, 0.95, 0.18), (x, 1.05, 0.08), (x, 1.05, -0.35), (x, 0.95, -0.45), (x, 0.0, -0.45)],
               0.024, "Hazard", segments=8)
    p.cylinder((-0.25, 1.05, -0.15), "x", 0.016, 0.5, "Steel", segments=6)
    p.box(-0.3, 0.0, -0.55, 0.3, 0.02, -0.35, "Steel")
    return p.build()


def piece_downpipe_section():
    """Burlan de 1 m pe perete (pivot pe perete, jos)."""
    p = Piece("K17_Downpipe_Section", bevel=0.0)
    p.cylinder((0.0, 0.0, 0.1), "y", 0.055, 1.0, "PipePaintGrey", segments=12)
    p.box(-0.07, 0.45, 0.0, 0.07, 0.5, 0.1, "Steel")
    return p.build()


def piece_downpipe_head():
    """Palnia de sus a burlanului: colecteaza apa de pe acoperis (iese din perete sub coronament)."""
    p = Piece("K17_Downpipe_Head", bevel=0.004)
    prof = [(0.06, 0.0), (0.06, 0.08), (0.16, 0.28), (0.16, 0.36)]
    p.lathe((0.0, -0.36, 0.1), [(r, h) for r, h in prof], "PipePaintGrey", 12)
    p.box(-0.12, -0.08, 0.0, 0.12, 0.0, 0.26, "PipePaintGrey")
    return p.build()


def piece_downpipe_shoe():
    """Cotul de jos al burlanului, care varsa intr-o rigola cu gratar."""
    p = Piece("K17_Downpipe_Shoe", bevel=0.0)
    p.tube([(0.0, 0.45, 0.1), (0.0, 0.2, 0.1), (0.0, 0.1, 0.2), (0.0, 0.08, 0.32)], 0.055, "PipePaintGrey", segments=12)
    p.box(-0.25, 0.0, 0.0, 0.25, 0.015, 0.55, "SteelDark")
    for k in range(6):
        p.box(-0.22, 0.015, 0.05 + k * 0.08, 0.22, 0.025, 0.08 + k * 0.08, "Steel")
    return p.build()


def piece_electrical_box():
    """Tablou electric pe perete (0,7 x 0,9 x 0,25), cu tub de cablu care urca si intra in perete."""
    p = Piece("K17_Electrical_Box", bevel=0.006)
    p.box(-0.35, 0.0, 0.0, 0.35, 0.9, 0.22, "Paint")
    p.box(-0.33, 0.02, 0.22, 0.33, 0.88, 0.25, "Paint")
    p.box(0.25, 0.4, 0.25, 0.29, 0.5, 0.27, "Steel")
    # eticheta de inalta tensiune (triunghi galben)
    p.prism([(-0.1, 0.62, 0.25), (0.1, 0.62, 0.25), (0.0, 0.79, 0.25)], (0, 0, 0.006), "Hazard")
    # tub de cablu: iese prin capac, urca 1,1 m si intra in perete
    p.tube([(0.0, 0.9, 0.11), (0.0, 1.9, 0.11), (0.0, 1.98, 0.06), (0.0, 1.99, -0.02)], 0.03, "Black", segments=8)
    return p.build()


def piece_wall_vent():
    """Grila de ventilatie cu lamele, 1,0 x 0,6 (pivot pe perete, jos, centru)."""
    p = Piece("K17_Wall_Vent", bevel=0.004)
    p.box(-0.5, 0.0, 0.0, 0.5, 0.06, 0.08, "Steel")
    p.box(-0.5, 0.54, 0.0, 0.5, 0.6, 0.08, "Steel")
    p.box(-0.5, 0.06, 0.0, -0.44, 0.54, 0.08, "Steel")
    p.box(0.44, 0.06, 0.0, 0.5, 0.54, 0.08, "Steel")
    p.box(-0.44, 0.06, 0.0, 0.44, 0.54, 0.01, "Black")
    for k in range(6):
        p.obox((0.0, 0.1 + k * 0.08, 0.04), (0.88, 0.012, 0.07), (35.0, 0.0, 0.0), "SteelDark")
    return p.build()


def piece_wall_lamp():
    """Lampa de exterior cu gratar, pe perete (pivot pe perete, centru)."""
    p = Piece("K17_Wall_Lamp", bevel=0.004)
    p.box(-0.12, -0.16, 0.0, 0.12, 0.16, 0.05, "SteelDark")
    p.box(-0.04, 0.02, 0.05, 0.04, 0.08, 0.25, "SteelDark")
    p.box(-0.2, -0.02, 0.15, 0.2, 0.06, 0.4, "SteelDark")
    p.box(-0.18, -0.03, 0.17, 0.18, -0.02, 0.38, "LampGlow")
    return p.build()


def piece_floodlight():
    """Stalp cu proiectoare (decor), 7 m; pivot la baza."""
    p = Piece("K17_Floodlight", bevel=0.006)
    p.box(-0.4, 0.0, -0.4, 0.4, 0.3, 0.4, "ConcreteDark")
    p.cylinder((0.0, 0.3, 0.0), "y", 0.09, 6.5, "SteelDark", segments=12)
    p.box(-0.9, 6.7, -0.08, 0.9, 6.8, 0.08, "SteelDark")
    for x in (-0.6, 0.0, 0.6):
        p.obox((x, 6.55, 0.18), (0.42, 0.32, 0.18), (25.0, 0.0, 0.0), "SteelDark")
        p.obox((x, 6.5, 0.28), (0.36, 0.26, 0.02), (25.0, 0.0, 0.0), "LampGlow")
    p.box(-0.18, 1.2, 0.09, 0.18, 1.7, 0.3, "Paint")          # cutia de alimentare
    return p.build()


def piece_transformer():
    """Transformator de putere pe fundatie, 10 x 4 x 8 (Yard_Generator): cuva cu radiatoare,
    izolatoare de inalta tensiune, dulap de comanda, tava de cabluri care intra in pamant."""
    p = Piece("K17_Transformer", bevel=0.01)
    p.box(-5.0, 0.0, -4.0, 5.0, 0.35, 4.0, "Concrete")
    p.box(-4.9, 0.35, -3.9, 4.9, 0.45, 3.9, "Gravel")
    # cuva
    p.box(-2.4, 0.45, -1.6, 2.4, 3.0, 1.6, "TankPaint")
    p.box(-2.5, 3.0, -1.7, 2.5, 3.15, 1.7, "TankPaint")
    # radiatoare pe laturile lungi
    for s in (-1, 1):
        for k in range(10):
            x = -2.2 + k * 0.48
            p.box(x, 0.8, s * 1.6, x + 0.06, 2.7, s * 2.6, "TankPaint")
        p.box(-2.3, 2.7, s * 1.6, 2.3, 2.78, s * 2.6, "SteelDark")
        p.box(-2.3, 0.72, s * 1.6, 2.3, 0.8, s * 2.6, "SteelDark")
    # izolatoare (inalta tensiune) si conservator
    for x in (-1.4, 0.0, 1.4):
        p.lathe((x, 3.15, -0.6), [(0.16, 0.0), (0.16, 0.1)] + [(r, 0.1 + k * 0.1) for k, r in
                                                                 enumerate([0.22, 0.14, 0.22, 0.14, 0.22, 0.14, 0.12])],
                "Ceramic", 12)
        p.cylinder((x, 3.95, -0.6), "y", 0.05, 0.05, "Steel", segments=8)
    p.cylinder((-2.0, 3.6, 0.8), "x", 0.35, 4.0, "TankPaint", segments=16)
    for x in (-1.5, 1.5):
        p.box(x - 0.08, 3.15, 0.7, x + 0.08, 3.3, 0.9, "SteelDark")
    # dulapul de comanda si tava de cabluri spre pamant
    p.box(3.0, 0.45, -1.0, 4.2, 2.4, 0.2, "Paint")
    p.box(4.2, 1.6, -0.8, 4.22, 2.0, -0.2, "ScreenGlow")
    p.box(2.4, 0.45, -0.25, 3.0, 0.55, 0.25, "Black")
    p.box(4.2, 0.35, -0.25, 4.9, 0.45, 0.25, "Black")
    # semne de pericol pe cuva
    for s in (-1, 1):
        p.box(-0.3, 1.8, s * 1.601, 0.3, 2.4, s * 1.61, "Hazard")
    # gard de protectie pe fundatie
    for x in (-4.8, 4.8):
        p.box(x - 0.04, 0.45, -3.8, x + 0.04, 2.2, 3.8, "SteelDark")
    for z in (-3.8, 3.8):
        p.box(-4.8, 0.45, z - 0.04, 4.8, 2.2, z + 0.04, "SteelDark")
    return p.build()


def piece_guard_hut():
    """Ghereta de paza prefabricata, 4 x 3 x 5: usa, ferestre pe toate laturile, acoperis cu streasina."""
    p = Piece("K17_Guard_Hut", bevel=0.01)
    p.box(-2.0, 0.0, -2.5, 2.0, 0.15, 2.5, "ConcreteDark")
    p.box(-1.9, 0.15, -2.4, 1.9, 2.7, 2.4, "Paint")
    p.box(-2.0, 2.7, -2.5, 2.0, 2.85, 2.5, "SteelDark")
    p.box(-1.95, 2.85, -2.45, 1.95, 3.0, 2.45, "SteelDark")
    # usa (+Z) si ferestre
    p.box(-0.5, 0.15, 2.4, 0.5, 2.2, 2.45, "DoorPaint")
    p.box(0.3, 1.0, 2.45, 0.38, 1.2, 2.48, "Steel")
    for s in (-1, 1):
        p.box(s * 1.9, 1.2, -1.6, s * 1.92, 2.2, 1.0, "Glass")
        p.box(s * 1.91, 1.12, -1.7, s * 1.95, 1.2, 1.1, "Steel")
    p.box(-1.4, 1.2, -2.42, 1.4, 2.2, -2.4, "Glass")
    p.box(-1.5, 1.12, -2.46, 1.5, 1.2, -2.42, "Steel")
    # aparatul de aer conditionat si lampa
    p.box(1.0, 2.0, 2.45, 1.7, 2.5, 2.75, "Steel")
    p.box(-1.2, 2.45, 2.45, -0.9, 2.6, 2.6, "LampGlow")
    return p.build()


def piece_door_panel():
    """Usa metalica falsa pe un perete plin (decor pe cladirile pline), 1,2 x 2,2; pivot pe perete, jos."""
    p = Piece("K17_Door_Panel", bevel=0.004)
    p.box(-0.72, 0.0, 0.0, 0.72, 2.38, 0.06, "SteelDark")
    p.box(-0.6, 0.0, 0.06, 0.6, 2.2, 0.08, "DoorPaint")
    p.box(0.42, 1.0, 0.08, 0.5, 1.1, 0.12, "Steel")
    p.box(-0.3, 1.5, 0.08, 0.3, 1.9, 0.085, "Glass")
    p.box(-0.25, 2.25, 0.06, 0.25, 2.33, 0.1, "LampGlow")
    return p.build()


def piece_window_panel():
    """Fereastra falsa (geam inchis cu rama) pe un perete plin, 1,4 x 1,0; pivot pe perete, jos."""
    p = Piece("K17_Window_Panel", bevel=0.004)
    p.box(-0.76, 0.0, 0.0, 0.76, 1.08, 0.06, "SteelDark")
    p.box(-0.7, 0.04, 0.02, 0.7, 1.04, 0.065, "Glass")
    p.box(-0.02, 0.04, 0.02, 0.02, 1.04, 0.07, "SteelDark")
    p.box(-0.82, -0.06, 0.0, 0.82, 0.0, 0.14, "ConcreteDark")
    return p.build()


def piece_roll_door():
    """Usa rulanta de garaj, 3 x 3 (decor); pivot pe perete, jos."""
    p = Piece("K17_Roll_Door", bevel=0.004)
    p.box(-1.6, 0.0, 0.0, -1.5, 3.2, 0.12, "SteelDark")
    p.box(1.5, 0.0, 0.0, 1.6, 3.2, 0.12, "SteelDark")
    p.box(-1.6, 3.0, 0.0, 1.6, 3.4, 0.3, "SteelDark")
    for k in range(15):
        y = k * 0.2
        p.box(-1.5, y + 0.01, 0.03, 1.5, y + 0.19, 0.07, "Paint")
    for x in (-1.0, 1.0):
        p.box(x - 0.6, 0.0, 0.07, x + 0.6, 0.12, 0.09, "Hazard")
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
    piece_reactor_vessel,
    piece_control_rod_drive,
    piece_pressurizer,
    piece_coolant_tank,
    piece_coolant_pump,
    piece_turbine,
    piece_control_panels,
    piece_hazmat_lockers,
    piece_shelf,
    piece_rubble,
    piece_sign_radiation,
    piece_floor_trefoil,
    piece_beacon,
    piece_emergency_light,
    piece_pipe_run,
    piece_decon_arch,
    piece_fuel_pool,
    piece_console,
    piece_hvac,
    piece_pipe_big,
    piece_pipe_cap,
    piece_pipe_collar,
    piece_pipe_run_end,
    piece_pipe_run_collar,
    piece_ladder_section,
    piece_ladder_top,
    piece_downpipe_section,
    piece_downpipe_head,
    piece_downpipe_shoe,
    piece_electrical_box,
    piece_wall_vent,
    piece_wall_lamp,
    piece_razor_wire,
    piece_truck,
    piece_floodlight,
    piece_transformer,
    piece_guard_hut,
    piece_door_panel,
    piece_window_panel,
    piece_roll_door,
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
    "K17_Reactor_Vessel": (-10.0, -9.0, 0),
    "K17_Pressurizer": (-5.0, -9.0, 0),
    "K17_Coolant_Tank": (-1.0, -9.0, 0),
    "K17_Coolant_Pump": (3.0, -9.0, 0),
    "K17_Turbine": (9.5, -11.0, 0),
    "K17_Control_Panels": (-9.0, -16.0, 0),
    "K17_Hazmat_Lockers": (-4.5, -16.0, 0),
    "K17_Shelf": (-2.8, -16.0, 0),
    "K17_Rubble": (-0.8, -15.5, 0),
    "K17_Fuel_Pool": (3.5, -16.5, 0),
    "K17_Floor_Trefoil": (-6.0, -12.5, 0),
    "K17_Decon_Arch": (0.8, -16.0, 0),
    "K17_Pipe_Run": (6.5, -16.0, 0),
    "K17_Sign_Radiation": (6.5, -15.5, 0),
    "K17_Beacon": (8.0, -15.5, 0),
    "K17_Emergency_Light": (9.0, -15.5, 0),
    "K17_Console": (-9.0, -22.0, 0),
    "K17_HVAC": (-5.5, -22.0, 0),
    "K17_Pipe_Big": (-2.0, -22.0, 0),
    "K17_Pipe_Cap": (-1.0, -22.0, 0),
    "K17_Pipe_Collar": (0.0, -22.0, 0),
    "K17_Pipe_Run_End": (2.0, -21.5, 0),
    "K17_Pipe_Run_Collar": (3.5, -21.5, 0),
    "K17_Ladder_Section": (5.0, -21.5, 0),
    "K17_Ladder_Top": (6.0, -21.5, 0),
    "K17_Downpipe_Section": (7.0, -21.5, 0),
    "K17_Downpipe_Head": (7.5, -21.5, 0),
    "K17_Downpipe_Shoe": (8.0, -21.5, 0),
    "K17_Electrical_Box": (9.0, -21.5, 0),
    "K17_Wall_Vent": (10.2, -21.5, 0),
    "K17_Wall_Lamp": (11.2, -21.0, 0),
    "K17_Razor_Wire": (-9.0, -28.0, 0),
    "K17_Truck": (-5.0, -29.0, 90),
    "K17_Floodlight": (0.0, -28.0, 0),
    "K17_Transformer": (7.0, -29.0, 0),
    "K17_Guard_Hut": (-12.5, -29.0, 0),
    "K17_Door_Panel": (2.0, -27.0, 0),
    "K17_Window_Panel": (3.5, -27.0, 0),
    "K17_Roll_Door": (13.5, -28.0, 0),
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
            render(camera, (-15.0, 9.0, -28.0), (0.0, 1.5, -12.5), 30, os.path.join(PREVIEW_DIR, "K17_Kit_Nuclear.png"))
            render(camera, (-4.0, 3.0, -23.0), (3.0, 1.0, -16.0), 32, os.path.join(PREVIEW_DIR, "K17_Kit_NuclearDetail.png"))
            render(camera, (-6.0, 4.0, -31.0), (1.5, 0.8, -21.5), 30, os.path.join(PREVIEW_DIR, "K17_Kit_V3.png"))
            render(camera, (-8.0, 7.0, -42.0), (1.0, 1.5, -28.5), 30, os.path.join(PREVIEW_DIR, "K17_Kit_Exterior.png"))
            render(camera, (-6.5, 2.4, -14.5), (0.0, 0.9, -8.0), 34, os.path.join(PREVIEW_DIR, "K17_Kit_Pump_Barrier.png"))

        if "--no-blend" not in ARGS:
            bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH, compress=True)

    print("KIT_DONE", len(objects))


main()
