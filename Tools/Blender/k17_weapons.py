"""
CHOKEHOLD - arme (persoana intai si a treia), generate din cod.

    M4A4-M Vandal   pusca CT: frana de gura, lanterna, PEQ, holo HV-1 + magnifier, pat PDW cu banda camuflaj
    M4-SD           pusca CT: amortizor, grip vertical, pat telescopic, holo + magnifier, inaltator fix
    M15-T Breacher  pistol T: compensator, red dot RD-3, lanterna
    P-SD            pistol CT: amortizor lung (otel ars), red dot, lanterna, banda camuflaj pe maner

Coordonate de arma: originea in palma dreapta (osul weapon_R), x dreapta, y sus, z = directia tevii.
Piese separate (animabile): Magazine, Bolt/Slide, ChargingHandle, Trigger, Selector, DustCover.
Socket-uri (Empty): Muzzle, Eject, Sight, Grip_R, Grip_L.
Persoana a treia: aceeasi constructie cu mai putine segmente si fara detalii mici (tp=True).

Rulare: D:/Blender/blender.exe -b --factory-startup --python Tools/Blender/k17_weapons.py
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

from k17_common import REPO, B, export_fbx, link, render_view, reset_scene, triangle_count, workbench_scene  # noqa: E402
from k17_kit import SLOT_COLORS, Piece  # noqa: E402

SLOT_COLORS.update({
    "GunMetal": (0.16, 0.17, 0.18),
    "Polymer": (0.09, 0.09, 0.10),
    "CamoTape": (0.32, 0.34, 0.24),
    "BluedSteel": (0.22, 0.26, 0.34),
    "Brass": (0.72, 0.55, 0.25),
    "MagPolymer": (0.35, 0.37, 0.36),
    "Glass": (0.08, 0.1, 0.12),
})

OUT = os.path.join(REPO, "Assets", "_Project", "Art", "Weapons")
DOCS = os.path.join(REPO, "Docs", "Characters")


class Weapon:
    def __init__(self, name, tp=False):
        self.name = name
        self.tp = tp
        self.seg = 8 if tp else 16
        self.root = link(bpy.data.objects.new(name + ("_TP" if tp else "_FP"), None))
        self.root.empty_display_size = 0.05
        self.objects = [self.root]

    def part(self, name, build, pivot=(0, 0, 0), bevel=0.0):
        p = Piece(f"{self.name}_{name}", bevel=0.0 if self.tp else bevel)
        build(p)
        obj = p.build()
        obj.name = name
        obj.data.transform(Matrix.Translation(-B(*pivot)))
        obj.location = B(*pivot)
        obj.parent = self.root
        self.objects.append(obj)
        return obj

    def socket(self, name, position):
        e = link(bpy.data.objects.new(name, None))
        e.empty_display_type = "ARROWS"
        e.empty_display_size = 0.03
        e.location = B(*position)
        e.parent = self.root
        self.objects.append(e)
        return e

    def triangles(self):
        return sum(triangle_count(o) for o in self.objects if o.type == "MESH")


# ----------------------------------------------------------------------------------------------
# Elemente comune
# ----------------------------------------------------------------------------------------------

def holo_sight(w, z0, y_rail):
    """Holo HV-1 pe sina: cutie cu fereastra; axa optica la y_rail + 0.045."""
    axis = y_rail + 0.045

    def holo(p):
        p.box(-0.018, y_rail, z0, 0.018, y_rail + 0.012, z0 + 0.075, "GunMetal")            # baza
        p.box(-0.019, y_rail + 0.012, z0, 0.019, y_rail + 0.040, z0 + 0.045, "Polymer")      # corpul cu bateria
        for s in (-1, 1):                                                                       # hood
            p.box(s * 0.016, y_rail + 0.012, z0 + 0.045, s * 0.020, axis + 0.022, z0 + 0.075, "GunMetal")
        p.box(-0.020, axis + 0.018, z0 + 0.045, 0.020, axis + 0.023, z0 + 0.075, "GunMetal")
        if not w.tp:
            p.box(-0.012, y_rail + 0.040, z0 + 0.010, -0.004, y_rail + 0.046, z0 + 0.018, "Polymer")   # butoane
            p.box(0.004, y_rail + 0.040, z0 + 0.010, 0.012, y_rail + 0.046, z0 + 0.018, "Polymer")

    w.part("Holo", holo)

    def window(p):
        p.box(-0.015, y_rail + 0.015, z0 + 0.072, 0.015, axis + 0.018, z0 + 0.074, "Glass")

    w.part("HoloWindow", window)
    return axis


def magnifier(w, z0, y_rail, axis):
    def mag(p):
        p.box(-0.012, y_rail, z0, 0.012, y_rail + 0.015, z0 + 0.055, "GunMetal")
        p.cylinder((0.0, axis, z0 - 0.005), "z", 0.017, 0.065, "Polymer", segments=w.seg)
        p.cylinder((0.0, axis, z0 + 0.052), "z", 0.019, 0.010, "GunMetal", segments=w.seg)
        p.cylinder((0.0, axis, z0 - 0.012), "z", 0.019, 0.008, "GunMetal", segments=w.seg)

    w.part("Magnifier", mag)

    def lens(p):
        p.cylinder((0.0, axis, z0 + 0.061), "z", 0.015, 0.002, "Glass", segments=w.seg)

    w.part("MagnifierLens", lens)


def red_dot(w, z0, y_top):
    axis = y_top + 0.018

    def dot(p):
        p.box(-0.011, y_top, z0, 0.011, y_top + 0.010, z0 + 0.045, "GunMetal")
        p.box(-0.012, y_top + 0.010, z0, 0.012, y_top + 0.020, z0 + 0.020, "Polymer")       # corp / baterie
        for s in (-1, 1):
            p.box(s * 0.010, y_top + 0.010, z0 + 0.020, s * 0.012, axis + 0.014, z0 + 0.045, "Polymer")
        p.box(-0.012, axis + 0.012, z0 + 0.020, 0.012, axis + 0.015, z0 + 0.045, "Polymer")

    w.part("RedDot", dot)

    def lens(p):
        p.box(-0.010, y_top + 0.012, z0 + 0.042, 0.010, axis + 0.012, z0 + 0.044, "Glass")

    w.part("RedDotLens", lens)
    return axis


def flashlight(w, name, center, length, radius=0.013):
    cx, cy, cz = center

    def light(p):
        p.cylinder((cx, cy, cz), "z", radius, length, "GunMetal", segments=w.seg)
        p.cylinder((cx, cy, cz + length - 0.012), "z", radius + 0.003, 0.012, "GunMetal", segments=w.seg)
        p.cylinder((cx, cy, cz + length - 0.001), "z", radius - 0.002, 0.001, "Glass", segments=w.seg)
        p.cylinder((cx, cy, cz - 0.010), "z", radius - 0.002, 0.010, "Polymer", segments=w.seg)   # butonul din coada

    w.part(name, light)


def camo_wrap(w, name, center, radius, length, axis="z"):
    def wrap(p):
        p.cylinder(center, axis, radius, length, "CamoTape", segments=w.seg)

    w.part(name, wrap)


# ----------------------------------------------------------------------------------------------
# Pusca (platforma AR): variantele Vandal si SD
# ----------------------------------------------------------------------------------------------

def rifle(name, variant, tp):
    w = Weapon(name, tp)
    rail_y = 0.115                 # fata de sus a sinei
    bore_y = 0.093                 # axa tevii
    sd = variant == "SD"

    def body(p):
        # lower receiver, magwell, tragaci-garda
        p.box(-0.019, 0.025, -0.085, 0.019, 0.078, 0.095, "Polymer")
        p.box(-0.017, 0.000, 0.025, 0.017, 0.078, 0.085, "Polymer")
        p.box(-0.006, -0.022, -0.012, 0.006, -0.018, 0.028, "Polymer")       # garda de tragaci
        p.box(-0.006, -0.022, 0.024, 0.006, 0.002, 0.028, "Polymer")
        # upper receiver + sina continua
        p.box(-0.020, 0.078, -0.090, 0.020, 0.112, 0.090, "GunMetal")
        p.box(-0.011, 0.105, -0.085, 0.011, rail_y, 0.330, "GunMetal")
        if not tp:
            for k in range(14):                                               # crestaturile sinei
                z = -0.08 + k * 0.03
                p.box(-0.012, rail_y, z, 0.012, rail_y + 0.003, z + 0.006, "GunMetal")
        # handguard cu fante
        p.box(-0.021, 0.060, 0.090, 0.021, 0.105, 0.330, "GunMetal")
        if not tp:
            for k in range(5):
                z = 0.12 + k * 0.04
                for s in (-1, 1):
                    p.box(s * 0.021, 0.075, z, s * 0.023, 0.090, z + 0.025, "Polymer")
            p.box(-0.023, 0.060, 0.270, 0.023, 0.064, 0.330, "Polymer")       # sina de jos (grip / lanterna)
        # teava si gas block
        p.cylinder((0.0, bore_y, 0.330), "z", 0.009, 0.085, "GunMetal", segments=w.seg)
        if sd:
            p.box(-0.012, 0.105, 0.290, 0.012, 0.150, 0.310, "GunMetal")     # inaltator fix (A-frame)
            p.box(-0.002, 0.150, 0.298, 0.002, 0.165, 0.302, "GunMetal")
        # manerul pistol
        p.obox((0.0, -0.055, -0.018), (0.032, 0.120, 0.046), (-16.0, 0.0, 0.0), "Polymer")
        # buffer tube + pat
        p.cylinder((0.0, 0.095, -0.090), "z", 0.016, -0.230, "GunMetal", segments=w.seg)
        if sd:
            p.box(-0.022, 0.040, -0.340, 0.022, 0.120, -0.200, "Polymer")       # pat telescopic
            p.box(-0.024, 0.030, -0.350, 0.024, 0.135, -0.335, "Rubber")        # talpa
            p.box(-0.018, 0.120, -0.280, 0.018, 0.130, -0.205, "Polymer")       # obraz
        else:
            for s in (-1, 1):                                                   # pat PDW: doua tije
                p.cylinder((s * 0.024, 0.085, -0.090), "z", 0.006, -0.230, "GunMetal", segments=w.seg)
            p.box(-0.030, 0.035, -0.335, 0.030, 0.140, -0.315, "Polymer")       # talpa
            p.box(-0.030, 0.120, -0.315, 0.030, 0.135, -0.300, "Rubber")
        # PEQ (laser) pe sina, stanga-fata
        p.box(-0.016, rail_y, 0.150, 0.016, rail_y + 0.032, 0.215, "Polymer")
        if not tp:
            p.cylinder((-0.008, rail_y + 0.020, 0.215), "z", 0.004, 0.004, "Glass", segments=8)
            p.cylinder((0.008, rail_y + 0.020, 0.215), "z", 0.004, 0.004, "Glass", segments=8)
        # emblema K-17 pe lower (relief fin)
        if not tp:
            p.box(-0.0195, 0.040, -0.060, -0.0215, 0.060, -0.030, "GunMetal")

    w.part("Body", body, bevel=0.0015)

    # Incarcator STANAG (Vandal: translucid, cu banda camuflaj)
    mag_slot = "MagPolymer" if variant == "Vandal" else "Polymer"

    def magazine(p):
        p.obox((0.0, -0.085, 0.060), (0.024, 0.185, 0.066), (-8.0, 0.0, 0.0), mag_slot)
        p.obox((0.0, -0.180, 0.073), (0.028, 0.012, 0.072), (-8.0, 0.0, 0.0), "Polymer")     # talpa
        if variant == "Vandal":
            p.obox((0.0, -0.120, 0.065), (0.026, 0.040, 0.068), (-8.0, 0.0, 0.0), "CamoTape")
            if not tp:
                for k in range(6):                                                                 # cartuse
                    p.cylinder((0.0, -0.010 - k * 0.014, 0.034), "z", 0.004, 0.052, "Brass", segments=6)

    w.part("Magazine", magazine, pivot=(0.0, 0.0, 0.055))

    def bolt(p):
        p.box(-0.019, 0.080, -0.085, 0.019, 0.104, -0.060, "GunMetal")            # portinchizatorul (vizibil prin port)

    w.part("Bolt", bolt, pivot=(0.0, 0.092, -0.060))

    def charging(p):
        p.box(-0.020, 0.100, -0.112, 0.020, 0.110, -0.088, "GunMetal")
        p.box(-0.030, 0.100, -0.112, -0.020, 0.108, -0.098, "GunMetal")          # latch

    w.part("ChargingHandle", charging, pivot=(0.0, 0.105, -0.090))

    def dust(p):
        p.box(0.019, 0.082, -0.010, 0.0215, 0.104, 0.050, "GunMetal")

    w.part("DustCover", dust, pivot=(0.020, 0.104, -0.010))

    def trigger(p):
        p.box(-0.004, -0.018, 0.000, 0.004, 0.008, 0.008, "GunMetal")

    w.part("Trigger", trigger, pivot=(0.0, 0.008, 0.004))

    def selector(p):
        p.box(-0.024, 0.058, -0.045, -0.019, 0.064, -0.015, "GunMetal")
        p.cylinder((-0.019, 0.061, -0.040), "x", 0.005, -0.004, "GunMetal", segments=8)

    w.part("Selector", selector, pivot=(-0.019, 0.061, -0.040))

    # Gura tevii: frana sau amortizor
    if sd:
        def suppressor(p):
            p.cylinder((0.0, bore_y, 0.380), "z", 0.021, 0.200, "GunMetal", segments=w.seg)
            p.cylinder((0.0, bore_y, 0.376), "z", 0.017, 0.006, "GunMetal", segments=w.seg)
            if not tp:
                p.cylinder((0.0, bore_y, 0.450), "z", 0.0215, 0.040, "Rubber", segments=w.seg)     # heat cover
        w.part("MuzzleDevice", suppressor)
        muzzle = (0.0, bore_y, 0.580)
    else:
        def brake(p):
            p.cylinder((0.0, bore_y, 0.415), "z", 0.014, 0.062, "GunMetal", segments=w.seg)
            if not tp:
                for k in range(3):
                    z = 0.425 + k * 0.016
                    p.box(-0.016, bore_y - 0.004, z, 0.016, bore_y + 0.004, z + 0.008, "GunMetal")
        w.part("MuzzleDevice", brake)
        muzzle = (0.0, bore_y, 0.477)

    # Optica
    axis = holo_sight(w, -0.010, rail_y)
    magnifier(w, -0.085, rail_y, axis)

    # Lanterna si grip
    if sd:
        def vgrip(p):
            p.cylinder((0.0, 0.060, 0.195), "y", 0.014, -0.062, "Polymer", segments=w.seg)
            p.box(-0.016, -0.004, 0.180, 0.016, 0.002, 0.210, "Polymer")
        w.part("VerticalGrip", vgrip)
        grip_l = (0.0, 0.025, 0.195)
    else:
        flashlight(w, "Flashlight", (-0.034, 0.072, 0.235), 0.075)
        def agrip(p):
            p.obox((0.0, 0.035, 0.215), (0.030, 0.055, 0.050), (35.0, 0.0, 0.0), "Polymer")
        w.part("AngledGrip", agrip)
        camo_wrap(w, "StockWrap", (0.0, 0.095, -0.230), 0.021, 0.070)
        camo_wrap(w, "GuardWrap", (0.0, 0.0825, 0.115), 0.0245, 0.040)
        grip_l = (0.0, 0.030, 0.225)

    # Socket-uri
    w.socket("Muzzle", muzzle)
    w.socket("Eject", (0.030, 0.095, 0.020))
    w.socket("Sight", (0.0, axis, -0.100))
    w.socket("Grip_R", (0.0, 0.0, 0.0))
    w.socket("Grip_L", grip_l)
    return w


# ----------------------------------------------------------------------------------------------
# Pistol: variantele Breacher si SD
# ----------------------------------------------------------------------------------------------

def pistol(name, variant, tp):
    w = Weapon(name, tp)
    sd = variant == "SD"
    bore_y = 0.046

    def frame(p):
        p.obox((0.0, -0.052, -0.008), (0.031, 0.110, 0.048), (18.0, 0.0, 0.0), "Polymer")     # manerul
        p.box(-0.016, 0.000, -0.050, 0.016, 0.030, 0.095, "Polymer")                          # frame + sina
        p.box(-0.006, -0.020, 0.000, 0.006, -0.016, 0.040, "Polymer")                         # garda
        p.box(-0.006, -0.020, 0.036, 0.006, 0.000, 0.040, "Polymer")
        if not tp:
            p.box(-0.017, 0.010, 0.030, 0.017, 0.016, 0.090, "GunMetal")                      # sina de accesorii
            p.box(-0.0165, -0.040, -0.030, -0.0185, -0.020, -0.010, "GunMetal")               # emblema K-17
        if sd:
            p.obox((0.0, -0.060, -0.010), (0.034, 0.045, 0.052), (18.0, 0.0, 0.0), "CamoTape")

    w.part("Frame", frame, bevel=0.0015)

    def slide(p):
        p.box(-0.015, 0.030, -0.050, 0.015, 0.062, 0.100, "GunMetal")
        if not tp:
            for k in range(6):                                                                 # serrations spate
                z = -0.045 + k * 0.006
                p.box(-0.016, 0.034, z, 0.016, 0.058, z + 0.002, "Polymer")
            for k in range(4):                                                                 # serrations fata
                z = 0.060 + k * 0.007
                p.box(-0.016, 0.034, z, 0.016, 0.058, z + 0.002, "Polymer")
            p.box(-0.004, 0.062, -0.048, 0.004, 0.070, -0.040, "GunMetal")                    # inaltator spate
        p.box(0.012, 0.040, 0.020, 0.0155, 0.058, 0.060, "GunMetal")                          # portul de ejectie (relief)

    w.part("Slide", slide, pivot=(0.0, 0.030, -0.050), bevel=0.0012)

    def barrel(p):
        p.cylinder((0.0, bore_y, 0.095), "z", 0.0075, 0.040, "GunMetal", segments=w.seg)

    w.part("Barrel", barrel)

    def magazine(p):
        p.obox((0.0, -0.070, -0.004), (0.022, 0.120, 0.032), (18.0, 0.0, 0.0), "GunMetal")
        p.obox((0.0, -0.128, 0.004), (0.030, 0.012, 0.050), (18.0, 0.0, 0.0), "Polymer")      # talpa

    w.part("Magazine", magazine, pivot=(0.0, -0.010, 0.0))

    def trigger(p):
        p.box(-0.004, -0.014, 0.012, 0.004, 0.004, 0.018, "GunMetal")

    w.part("Trigger", trigger, pivot=(0.0, 0.004, 0.015))

    def selector(p):                                                                            # piedica / slide release
        p.box(-0.020, 0.024, -0.030, -0.015, 0.030, -0.010, "GunMetal")

    w.part("Selector", selector, pivot=(-0.015, 0.027, -0.020))

    if sd:
        def suppressor(p):
            p.cylinder((0.0, bore_y, 0.135), "z", 0.016, 0.195, "BluedSteel", segments=w.seg)
            p.cylinder((0.0, bore_y, 0.128), "z", 0.011, 0.008, "GunMetal", segments=w.seg)
            if not tp:
                for k in range(6):                                                             # fante la baza
                    a = 2 * math.pi * k / 6
                    p.box(math.cos(a) * 0.014 - 0.002, bore_y + math.sin(a) * 0.014 - 0.002, 0.140,
                          math.cos(a) * 0.014 + 0.002, bore_y + math.sin(a) * 0.014 + 0.002, 0.160, "Polymer")
        w.part("MuzzleDevice", suppressor)
        camo_wrap(w, "SuppressorWrap", (0.0, bore_y, 0.230), 0.0165, 0.040)
        muzzle = (0.0, bore_y, 0.330)
    else:
        def comp(p):
            p.box(-0.014, bore_y - 0.013, 0.128, 0.014, bore_y + 0.013, 0.175, "GunMetal")
            if not tp:
                for k in range(2):
                    z = 0.138 + k * 0.016
                    p.box(-0.009, bore_y + 0.013, z, 0.009, bore_y + 0.016, z + 0.008, "GunMetal")
        w.part("MuzzleDevice", comp)
        muzzle = (0.0, bore_y, 0.175)

    axis = red_dot(w, -0.030, 0.062)
    flashlight(w, "Flashlight", (0.0, 0.004, 0.040), 0.050, radius=0.012)

    w.socket("Muzzle", muzzle)
    w.socket("Eject", (0.025, 0.050, 0.040))
    w.socket("Sight", (0.0, axis, -0.090))
    w.socket("Grip_R", (0.0, 0.0, 0.0))
    w.socket("Grip_L", (-0.012, -0.045, 0.030))
    return w


WEAPONS = [
    ("M4A4M_Vandal", lambda tp: rifle("M4A4M_Vandal", "Vandal", tp)),
    ("M4_SD", lambda tp: rifle("M4_SD", "SD", tp)),
    ("M15T_Breacher", lambda tp: pistol("M15T_Breacher", "Breacher", tp)),
    ("P_SD", lambda tp: pistol("P_SD", "SD", tp)),
]


def main():
    # Fiecare arma in scena ei: numele socket-urilor raman curate (fara .001) in FBX.
    for name, make in WEAPONS:
        for tp in (False, True):
            reset_scene()
            w = make(tp)
            path = os.path.join(OUT, name, f"{name}_{'TP' if tp else 'FP'}.fbx")
            export_fbx(path, w.objects, with_armature=False)
            print(f"WEAPON {w.root.name}: {w.triangles()} triunghiuri -> {os.path.relpath(path, REPO)}")

    reset_scene()
    preview = [make(False).root for _, make in WEAPONS]

    # Previzualizare: armele FP una sub alta
    for k, root in enumerate(preview):
        root.location = B(0.0, 0.9 - k * 0.3, 0.0)
    cam = workbench_scene(resolution=(1800, 1000))
    render_view(cam, (-1.25, 0.55, 0.12), (0.0, 0.48, 0.12), 45, os.path.join(DOCS, "Weapons_Side.png"))
    render_view(cam, (-0.75, 0.95, -0.45), (0.0, 0.42, 0.10), 40, os.path.join(DOCS, "Weapons_Persp.png"))
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(REPO, "Tools", "Blender", "K17_Weapons.blend"), compress=True)
    print("WEAPONS_DONE")


main()
