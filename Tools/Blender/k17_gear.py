"""
CHOKEHOLD - echipament pentru personaje (piese modulare peste corpul de baza).

Fiecare piesa e un obiect separat, construit in coordonate de personaj (x dreapta, y sus, z inainte),
cu un material (slot) si o regula de skinning:
    - "rigid": toate vertecsii pe un singur os (casca, vesta, rucsac, toc, genunchiere);
    - "auto": greutati automate din schelet (haina, pantaloni, manusi, bocanci).
Dimensiunile pornesc de la volumele din k17_characters.body_volumes (A-pose, 1,80 m).
"""

import math

import bmesh
import bpy
from mathutils import Vector

from k17_common import B, activate, ellipsoid, link, loft, material

V = Vector

# Culori de previzualizare (Workbench); in Unity fiecare slot are materialul si textura lui.
SLOT_COLORS = {
    "Skin": (0.62, 0.47, 0.40),
    "CT_Camo": (0.22, 0.26, 0.36),
    "T_Camo": (0.30, 0.34, 0.22),
    "T_CamoDesert": (0.48, 0.42, 0.30),
    "Nylon": (0.08, 0.08, 0.09),
    "NylonOlive": (0.30, 0.32, 0.22),
    "Plastic": (0.06, 0.06, 0.07),
    "Rubber": (0.07, 0.06, 0.06),
    "Leather": (0.28, 0.20, 0.14),
    "Glass": (0.05, 0.07, 0.09),
    "Steel": (0.35, 0.36, 0.38),
    "Maroon": (0.42, 0.10, 0.14),
    "PatchBlue": (0.12, 0.30, 0.75),
    "PatchGrey": (0.45, 0.46, 0.48),
    "Wood": (0.40, 0.26, 0.14),
}


def arm_dir(side, angle=math.radians(40)):
    return V((side * math.cos(angle), -math.sin(angle), 0.0))


class Gear:
    """Colecteaza piesele unui personaj: fiecare build_* adauga (obiect, os sau None)."""

    def __init__(self, name):
        self.name = name
        self.pieces = []      # (obj, bone sau None)

    def piece(self, name, build, slot, bone=None, smooth=True):
        bm = bmesh.new()
        build(bm)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        mesh = bpy.data.meshes.new(f"{self.name}_{name}")
        bm.to_mesh(mesh)
        bm.free()
        obj = link(bpy.data.objects.new(mesh.name, mesh))
        color = SLOT_COLORS.get(slot, (0.5, 0.5, 0.5))
        mesh.materials.append(material(slot, color, roughness=0.8 if slot != "Glass" else 0.2))
        if smooth:
            for poly in mesh.polygons:
                poly.use_smooth = True
        self.pieces.append((obj, bone))
        return obj


# ----------------------------------------------------------------------------------------------
# Primitive (coordonate de personaj)
# ----------------------------------------------------------------------------------------------

def box(bm, x0, y0, z0, x1, y1, z1):
    x0, x1 = min(x0, x1), max(x0, x1)
    y0, y1 = min(y0, y1), max(y0, y1)
    z0, z1 = min(z0, z1), max(z0, z1)
    corners = [(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1), (x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1)]
    v = [bm.verts.new(B(*c)) for c in corners]
    for q in ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)):
        bm.faces.new([v[i] for i in q])


def obox(bm, center, size, euler_deg):
    """Cutie rotita (grade, ordinea Z, X, Y) in jurul centrului."""
    rx, ry, rz = (math.radians(a) for a in euler_deg)
    hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2

    def rot(p):
        x, y, z = p
        x, y = x * math.cos(rz) - y * math.sin(rz), x * math.sin(rz) + y * math.cos(rz)
        y, z = y * math.cos(rx) - z * math.sin(rx), y * math.sin(rx) + z * math.cos(rx)
        x, z = x * math.cos(ry) + z * math.sin(ry), -x * math.sin(ry) + z * math.cos(ry)
        return x + center[0], y + center[1], z + center[2]

    corners = [rot((sx * hx, sy * hy, sz * hz)) for sy in (-1, 1) for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    v = [bm.verts.new(B(*c)) for c in corners]
    for q in ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)):
        bm.faces.new([v[i] for i in q])


def tube(bm, points, radius, segments=8):
    pts = [V(p) for p in points]
    rings = []
    for i, p in enumerate(pts):
        if i == 0:
            t = (pts[1] - pts[0]).normalized()
        elif i == len(pts) - 1:
            t = (pts[-1] - pts[-2]).normalized()
        else:
            t = ((pts[i + 1] - pts[i]).normalized() + (pts[i] - pts[i - 1]).normalized()).normalized()
        ref = V((0, 1, 0)) if abs(t.y) < 0.9 else V((1, 0, 0))
        n = t.cross(ref).normalized()
        b = t.cross(n).normalized()
        ring = []
        for k in range(segments):
            a = 2 * math.pi * k / segments
            q = p + (n * math.cos(a) + b * math.sin(a)) * radius
            ring.append(bm.verts.new(B(q.x, q.y, q.z)))
        rings.append(ring)
    for r0, r1 in zip(rings, rings[1:]):
        for k in range(segments):
            bm.faces.new((r0[k], r0[(k + 1) % segments], r1[(k + 1) % segments], r1[k]))
    bm.faces.new(rings[0])
    bm.faces.new(list(reversed(rings[-1])))


def cylinder(bm, center, axis, radius, length, segments=16):
    """Cilindru de la center pe axa ('x', 'y', 'z') cu lungimea data."""
    c = V(center)
    d = {"x": V((1, 0, 0)), "y": V((0, 1, 0)), "z": V((0, 0, 1))}[axis] * length
    tube(bm, [c, c + d], radius, segments)


def ring(bm, y, rw, rd, z, height, segments=24, power=2.2):
    """Banda orizontala (centura, bratara) in jurul trunchiului sau unui membru."""
    loft(bm, [(V((0, y, z)), rw, rd), (V((0, y + height, z)), rw, rd)], segments=segments, power=power)


def limb_ring(bm, center, direction, rw, rd, length, segments=14):
    c = V(center)
    d = V(direction).normalized()
    loft(bm, [(c, rw, rd), (c + d * length, rw, rd)], segments=segments)


# ----------------------------------------------------------------------------------------------
# Piese comune (dimensiunile din corpul de baza)
# ----------------------------------------------------------------------------------------------

HEAD = V((0, 1.695, 0.012))
ARM = {1: V((0.17, 1.45, -0.01)), -1: V((-0.17, 1.45, -0.01))}


def arm_points(s):
    d = arm_dir(s)
    upper = ARM[s]
    elbow = upper + d * 0.29 + V((0, 0, -0.012))
    wrist = elbow + d * 0.26 + V((0, 0, 0.012))
    return d, upper, elbow, wrist


def build_jacket(gear, slot, hood=True, collar_slot=None):
    def torso(bm):
        sections = [(0.86, 0.168, 0.122, 0.000), (0.95, 0.194, 0.140, 0.008), (1.06, 0.178, 0.130, 0.012),
                    (1.15, 0.184, 0.144, 0.018), (1.25, 0.200, 0.160, 0.026), (1.34, 0.210, 0.160, 0.020),
                    (1.42, 0.205, 0.144, 0.006), (1.48, 0.174, 0.114, 0.000), (1.52, 0.142, 0.098, 0.002), (1.548, 0.098, 0.082, 0.006)]
        loft(bm, [(V((0, y, z)), w, d) for y, w, d, z in sections], segments=24, power=2.2)
        for s in (1, -1):
            ellipsoid(bm, V((s * 0.165, 1.432, -0.006)), (0.084, 0.082, 0.080), segments=16, rings=10)   # umerii
        if hood:
            ellipsoid(bm, V((0, 1.505, -0.115)), (0.115, 0.075, 0.075), segments=14, rings=8)

    gear.piece("Jacket", torso, slot)

    for s in (1, -1):
        d, upper, elbow, wrist = arm_points(s)

        def sleeve(bm, d=d, upper=upper, elbow=elbow, wrist=wrist):
            loft(bm, [
                (upper - d * 0.06, 0.080, 0.080),
                (upper + d * 0.08, 0.072, 0.074),
                (upper + d * 0.17, 0.066, 0.068),
                (elbow - d * 0.03, 0.058, 0.060),
                (elbow + d * 0.03, 0.057, 0.058),
                (elbow + d * 0.14, 0.052, 0.050),
                (wrist - d * 0.005, 0.040, 0.046),
            ], segments=18)

        gear.piece(f"Sleeve_{'R' if s > 0 else 'L'}", sleeve, slot)


def build_pants(gear, slot, cargo=True):
    for s in (1, -1):
        def leg(bm, s=s):
            loft(bm, [
                (V((s * 0.088, 0.91, -0.004)), 0.112, 0.126),
                (V((s * 0.097, 0.72, 0.004)), 0.094, 0.098),
                (V((s * 0.099, 0.58, 0.010)), 0.078, 0.080),
                (V((s * 0.100, 0.51, 0.016)), 0.073, 0.076),
                (V((s * 0.101, 0.45, 0.004)), 0.072, 0.078),
                (V((s * 0.103, 0.34, -0.012)), 0.074, 0.082),
                (V((s * 0.104, 0.24, -0.018)), 0.062, 0.066),
                (V((s * 0.105, 0.20, -0.019)), 0.058, 0.062),
            ], segments=18)
            ellipsoid(bm, V((s * 0.072, 0.905, -0.055)), (0.090, 0.094, 0.084), segments=14, rings=8)   # sezutul
            if cargo:
                x = s * (0.097 + 0.094)
                box(bm, x - s * 0.005, 0.60, -0.045, x + s * 0.028, 0.80, 0.065)
                box(bm, x + s * 0.028, 0.74, -0.05, x + s * 0.032, 0.80, 0.07)   # clapa

        gear.piece(f"Pants_{'R' if s > 0 else 'L'}", leg, slot)


def build_boots(gear, slot):
    for s in (1, -1):
        def boot(bm, s=s):
            loft(bm, [
                (V((s * 0.104, 0.27, -0.018)), 0.056, 0.060),
                (V((s * 0.105, 0.16, -0.020)), 0.048, 0.054),
                (V((s * 0.105, 0.09, -0.022)), 0.046, 0.052),
                (V((s * 0.105, 0.06, -0.030)), 0.046, 0.055),
            ], segments=16)
            loft(bm, [
                (V((s * 0.105, 0.055, -0.078)), 0.042, 0.050),
                (V((s * 0.106, 0.058, -0.020)), 0.052, 0.058),
                (V((s * 0.108, 0.052, 0.050)), 0.056, 0.050),
                (V((s * 0.110, 0.040, 0.125)), 0.058, 0.040),
                (V((s * 0.110, 0.034, 0.185)), 0.050, 0.030),
                (V((s * 0.110, 0.032, 0.212)), 0.028, 0.016),
            ], segments=14, prefer_front=V((0.0, 1.0, 0.0)))
            # talpa
            box(bm, s * 0.105 - 0.052, 0.0, -0.085, s * 0.105 + 0.052, 0.022, 0.215)
            # sireturi (benzi orizontale pe fata)
            for k in range(5):
                y = 0.10 + k * 0.032
                box(bm, s * 0.105 - 0.022, y, 0.028 + k * 0.004, s * 0.105 + 0.022, y + 0.010, 0.040 + k * 0.004)

        gear.piece(f"Boot_{'R' if s > 0 else 'L'}", boot, slot)


def build_gloves(gear, slot, fingerless=False):
    for s in (1, -1):
        d, upper, elbow, wrist = arm_points(s)
        knuckles = wrist + d * 0.09

        def glove(bm):
            loft(bm, [
                (wrist - d * 0.03, 0.030, 0.038),
                (wrist + d * 0.005, 0.025, 0.037),
                (wrist + d * 0.045, 0.021, 0.047),
                (knuckles - d * 0.004, 0.018, 0.047),
                (knuckles + d * 0.004, 0.015, 0.043),
            ], segments=14)
            if not fingerless:
                for dz, lengths in ((0.030, (0.045, 0.028, 0.022)), (0.010, (0.050, 0.032, 0.024)),
                                    (-0.010, (0.046, 0.030, 0.022)), (-0.030, (0.036, 0.022, 0.020))):
                    p = knuckles + V((0, 0, dz))
                    total = sum(lengths)
                    loft(bm, [(p - d * 0.004, 0.0125, 0.0125), (p + d * total * 0.5, 0.0115, 0.0115),
                              (p + d * (total - 0.004), 0.0105, 0.0105), (p + d * (total + 0.002), 0.006, 0.006)], segments=8)
                thumb_dir = (d * 0.55 + V((0, -0.25, 0.75))).normalized()
                tp = wrist + d * 0.025 + V((0, -0.01, 0.025))
                loft(bm, [(tp - thumb_dir * 0.004, 0.017, 0.019), (tp + thumb_dir * 0.05, 0.014, 0.014),
                          (tp + thumb_dir * 0.095, 0.012, 0.012), (tp + thumb_dir * 0.103, 0.006, 0.006)], segments=8)
            # bratara cu velcro
            limb_ring(bm, wrist - d * 0.035, d, 0.034, 0.041, 0.025, segments=14)

        gear.piece(f"Glove_{'R' if s > 0 else 'L'}", glove, slot)


def build_kneepads(gear, slot):
    for s in (1, -1):
        def pad(bm, s=s):
            ellipsoid(bm, V((s * 0.100, 0.50, 0.072)), (0.066, 0.080, 0.038), segments=14, rings=8)
            # curele
            limb_ring(bm, V((s * 0.100, 0.555, 0.012)), V((0, -1, 0)), 0.078, 0.082, 0.018, segments=16)
            limb_ring(bm, V((s * 0.101, 0.455, 0.004)), V((0, -1, 0)), 0.076, 0.082, 0.018, segments=16)

        gear.piece(f"Kneepad_{'R' if s > 0 else 'L'}", pad, slot, bone=f"{'Right' if s > 0 else 'Left'}LowerLeg")


def build_elbowpads(gear, slot):
    for s in (1, -1):
        d, upper, elbow, wrist = arm_points(s)
        back = V((0, 0, -1))

        def pad(bm):
            c = elbow + back * 0.045
            ellipsoid(bm, c, (0.050, 0.050, 0.030), segments=12, rings=6)
            limb_ring(bm, elbow - d * 0.05, d, 0.060, 0.062, 0.015, segments=14)
            limb_ring(bm, elbow + d * 0.05, d, 0.058, 0.060, 0.015, segments=14)

        gear.piece(f"Elbowpad_{'R' if s > 0 else 'L'}", pad, slot, bone=f"{'Right' if s > 0 else 'Left'}LowerArm")


def build_holster(gear, slot, side=1):
    s = side
    x = s * 0.097

    def holster(bm):
        box(bm, x + s * 0.070, 0.60, 0.03, x + s * 0.120, 0.82, 0.115)      # corpul tocului
        box(bm, x + s * 0.075, 0.82, 0.06, x + s * 0.110, 0.90, 0.10)       # manerul pistolului
        box(bm, x + s * 0.060, 0.84, 0.00, x + s * 0.125, 0.86, 0.12)       # placa de coapsa
        limb_ring(bm, V((x, 0.80, 0.004)), V((0, -1, 0)), 0.100, 0.104, 0.025, segments=18)
        limb_ring(bm, V((x, 0.66, 0.008)), V((0, -1, 0)), 0.092, 0.098, 0.025, segments=18)
        # curea spre centura
        box(bm, x + s * 0.080, 0.86, 0.02, x + s * 0.110, 0.98, 0.05)

    gear.piece("Holster", holster, slot, bone=f"{'Right' if s > 0 else 'Left'}UpperLeg")


def build_belt(gear, slot, pouches=True, tools=True):
    def belt(bm):
        ring(bm, 0.955, 0.205, 0.152, 0.004, 0.055, segments=24, power=2.4)
        box(bm, -0.035, 0.95, 0.150, 0.035, 1.015, 0.168)                   # catarama
        if pouches:
            box(bm, -0.075, 0.86, -0.195, 0.075, 1.00, -0.148)             # punga la spate
            box(bm, 0.12, 0.84, -0.14, 0.17, 0.99, -0.09)                   # punga pe soldul drept (spate)
        if tools:
            box(bm, -0.19, 0.78, -0.03, -0.16, 0.96, 0.02)                  # cleste in teaca
            tube(bm, [(-0.16, 0.90, 0.07), (-0.19, 0.80, 0.05), (-0.17, 0.72, 0.09)], 0.008, segments=6)  # cordon
            box(bm, 0.17, 0.86, 0.03, 0.20, 0.92, 0.09)                     # carabiniera

    gear.piece("Belt", belt, slot, bone="Hips")


def build_balaclava(gear, slot):
    def mask(bm):
        ellipsoid(bm, HEAD, (0.085, 0.114, 0.103), segments=20, rings=12)
        ellipsoid(bm, V((0, 1.622, 0.036)), (0.066, 0.054, 0.072), segments=14, rings=8)   # maxilarul
        loft(bm, [(V((0, 1.46, 0.0)), 0.070, 0.066), (V((0, 1.56, 0.008)), 0.063, 0.062),
                  (V((0, 1.63, 0.02)), 0.070, 0.070)], segments=16)

    gear.piece("Balaclava", mask, slot)


def build_goggles(gear, slot_frame, slot_lens, y=1.705):
    def goggles(bm):
        box(bm, -0.080, y - 0.028, HEAD.z + 0.088, 0.080, y + 0.028, HEAD.z + 0.108)
        limb_ring(bm, V((0, y, HEAD.z - 0.002)), V((0, 1, 0)), 0.092, 0.108, 0.018, segments=20)   # curea

    def lens(bm):
        box(bm, -0.070, y - 0.020, HEAD.z + 0.108, 0.070, y + 0.020, HEAD.z + 0.114)

    gear.piece("Goggles", goggles, slot_frame, bone="Head")
    gear.piece("GogglesLens", lens, slot_lens, bone="Head")


# ----------------------------------------------------------------------------------------------
# CT_Operator
# ----------------------------------------------------------------------------------------------

def build_ct_operator(gear):
    build_balaclava(gear, "Nylon")
    build_jacket(gear, "CT_Camo", hood=True)
    build_pants(gear, "CT_Camo")
    build_boots(gear, "Rubber")
    build_gloves(gear, "Nylon")
    build_kneepads(gear, "Plastic")
    build_goggles(gear, "Plastic", "Glass")
    build_belt(gear, "Nylon")
    build_holster(gear, "Nylon", side=1)

    # Casca balistica high-cut cu sine laterale si montura NVG
    def helmet(bm):
        hz = HEAD.z - 0.004
        loft(bm, [(V((0, 1.655, hz)), 0.100, 0.114), (V((0, 1.700, hz)), 0.104, 0.120),
                  (V((0, 1.750, hz)), 0.100, 0.116), (V((0, 1.795, hz)), 0.084, 0.098),
                  (V((0, 1.825, hz)), 0.050, 0.060), (V((0, 1.836, hz)), 0.012, 0.014)], segments=22, power=2.1)
        for s in (1, -1):
            box(bm, s * 0.098, 1.70, hz - 0.07, s * 0.110, 1.73, hz + 0.06)             # sine
        box(bm, -0.035, 1.745, hz + 0.112, 0.035, 1.790, hz + 0.132)                    # placa NVG
        box(bm, -0.015, 1.790, hz + 0.118, 0.015, 1.810, hz + 0.150)                    # bratul monturii
        box(bm, -0.055, 1.660, hz - 0.125, 0.055, 1.690, hz - 0.112)                    # contragreutate spate

    gear.piece("Helmet", helmet, "Plastic", bone="Head")

    # Casti de comunicatie cu microfon
    def headset(bm):
        for s in (1, -1):
            cylinder(bm, (s * 0.086, 1.690, HEAD.z - 0.004), "x", 0.044, s * 0.034, segments=16)
            cylinder(bm, (s * 0.120, 1.690, HEAD.z - 0.004), "x", 0.030, s * 0.012, segments=12)
        tube(bm, [(-0.118, 1.670, 0.0), (-0.118, 1.630, 0.07), (-0.075, 1.625, 0.125), (-0.030, 1.630, 0.135)], 0.006, segments=6)
        ellipsoid(bm, V((-0.030, 1.630, 0.138)), (0.012, 0.010, 0.012), segments=8, rings=5)

    gear.piece("Headset", headset, "Plastic", bone="Head")

    # Plate carrier: placa fata, placa spate, cummerbund, bretele, 3 pungi de incarcatoare, panou velcro
    def carrier(bm):
        box(bm, -0.155, 1.19, 0.175, 0.155, 1.47, 0.215)
        box(bm, -0.165, 1.17, -0.185, 0.165, 1.47, -0.145)
        ring(bm, 1.19, 0.214, 0.172, 0.022, 0.12, segments=24, power=2.6)
        for s in (1, -1):
            box(bm, s * 0.06, 1.455, -0.14, s * 0.12, 1.49, 0.19)
        for x0 in (-0.125, -0.040, 0.045):
            box(bm, x0, 1.18, 0.215, x0 + 0.080, 1.33, 0.262)
            box(bm, x0 + 0.005, 1.33, 0.215, x0 + 0.075, 1.345, 0.262)   # clapa
        box(bm, -0.09, 1.36, 0.215, 0.09, 1.44, 0.225)                      # panou velcro
        box(bm, 0.11, 1.22, 0.215, 0.165, 1.40, 0.245)                     # punga utilitara dreapta

    gear.piece("Carrier", carrier, "Nylon", bone="UpperChest")

    # Radio pe bretea stanga, antena scurta, cablu spre casca
    def radio(bm):
        box(bm, -0.125, 1.34, 0.19, -0.075, 1.45, 0.235)
        cylinder(bm, (-0.112, 1.45, 0.205), "y", 0.006, 0.09, segments=8)
        tube(bm, [(-0.09, 1.45, 0.21), (-0.10, 1.52, 0.10), (-0.112, 1.60, 0.02)], 0.004, segments=5)

    gear.piece("Radio", radio, "Plastic", bone="UpperChest")

    # Rucsac rigid cu fatete
    def backpack(bm):
        box(bm, -0.16, 1.06, -0.30, 0.16, 1.46, -0.19)
        box(bm, -0.13, 1.12, -0.33, 0.13, 1.42, -0.30)                      # fateta centrala
        box(bm, -0.14, 1.46, -0.29, 0.14, 1.49, -0.20)                      # capac
        for s in (1, -1):
            box(bm, s * 0.08, 1.44, -0.19, s * 0.12, 1.47, -0.14)           # prinderea bretelelor
        box(bm, -0.10, 0.98, -0.26, 0.10, 1.06, -0.18)                      # punga de jos

    gear.piece("Backpack", backpack, "Plastic", bone="UpperChest")

    # Patch "CT" pe bratul stang, patch de unitate pe dreptul, benzi negre pe ambele
    for s, slot in ((-1, "PatchBlue"), (1, "PatchGrey")):
        d, upper, elbow, wrist = arm_points(s)
        n = V((-s * 0.643, 0.766, 0.0))
        p = upper + d * 0.10 + n * 0.074

        def patch(bm, p=p, s=s):
            obox(bm, (p.x, p.y, p.z), (0.070, 0.008, 0.060), (0.0, 0.0, s * 40.0))

        gear.piece(f"Patch_{'R' if s > 0 else 'L'}", patch, slot, bone=f"{'Right' if s > 0 else 'Left'}UpperArm")

        def band(bm, s=s, d=d, upper=upper):
            limb_ring(bm, upper + d * 0.19, d, 0.067, 0.069, 0.030, segments=16)

        gear.piece(f"Band_{'R' if s > 0 else 'L'}", band, "Nylon", bone=f"{'Right' if s > 0 else 'Left'}UpperArm")
