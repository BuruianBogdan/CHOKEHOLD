"""
Blacksite K-17 - texturi PBR procedurale (fara descarcari, totul generat din cod).

Fiecare set are trei imagini de 1024 x 1024, care se repeta fara cusatura:
    <set>_A.png   culoarea (aproape neutra: culoarea finala vine din materialul Unity, ca nuanta)
    <set>_N.png   normal map (OpenGL, Y in sus)
    <set>_M.png   masca URP: R = metalic, A = luciu (smoothness)

Rulare (din radacina repo-ului):
    D:/Blender/blender.exe -b --factory-startup --python Tools/Blender/k17_textures.py

Zgomotul e periodic (grila aleatoare care se inchide pe margini), deci texturile nu au cusaturi.
"""

import os
import sys

import bpy
import numpy as np

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
OUT = os.path.join(REPO, "Assets", "_Project", "Art", "Textures", "K17")
RES = 1024


# ----------------------------------------------------------------------------------------------
# Zgomot periodic
# ----------------------------------------------------------------------------------------------

def smooth(t):
    return t * t * (3 - 2 * t)


def value_noise(period, seed, res=RES, stretch=(1, 1)):
    """Zgomot de valoare periodic: grila period x period, interpolare netezita. stretch alungeste pe axe."""
    rng = np.random.default_rng(seed)
    px, py = max(1, int(period * stretch[0])), max(1, int(period * stretch[1]))
    grid = rng.random((py, px))
    u = np.linspace(0, px, res, endpoint=False)
    v = np.linspace(0, py, res, endpoint=False)
    x0 = np.floor(u).astype(int)
    y0 = np.floor(v).astype(int)
    fx = smooth(u - x0)
    fy = smooth(v - y0)
    x1 = (x0 + 1) % px
    y1 = (y0 + 1) % py
    a = grid[np.ix_(y0, x0)]
    b = grid[np.ix_(y0, x1)]
    c = grid[np.ix_(y1, x0)]
    d = grid[np.ix_(y1, x1)]
    top = a + (b - a) * fx[None, :]
    bottom = c + (d - c) * fx[None, :]
    return top + (bottom - top) * fy[:, None]


def fbm(period, octaves, seed, gain=0.5, stretch=(1, 1)):
    total = np.zeros((RES, RES))
    amp, norm = 1.0, 0.0
    for o in range(octaves):
        total += value_noise(period * (2 ** o), seed + o * 101, stretch=stretch) * amp
        norm += amp
        amp *= gain
    return total / norm


def worley(cells, seed):
    """Celule Voronoi periodice: (F1, F2) in unitati de celula."""
    rng = np.random.default_rng(seed)
    points = rng.random((cells, cells, 2))
    coords = (np.arange(RES) + 0.5) / RES * cells
    gx, gy = np.meshgrid(coords, coords)
    cx, cy = np.floor(gx).astype(int), np.floor(gy).astype(int)
    f1 = np.full((RES, RES), 9.0)
    f2 = np.full((RES, RES), 9.0)
    for oy in (-1, 0, 1):
        for ox in (-1, 0, 1):
            nx, ny = cx + ox, cy + oy
            p = points[ny % cells, nx % cells]
            dx = nx + p[..., 0] - gx
            dy = ny + p[..., 1] - gy
            dist = np.sqrt(dx * dx + dy * dy)
            closer = dist < f1
            f2 = np.where(closer, f1, np.minimum(f2, dist))
            f1 = np.where(closer, dist, f1)
    return f1, f2


def grid_coords(repeat):
    """Coordonate (u, v) in [0, repeat) pe imagine."""
    t = (np.arange(RES) + 0.5) / RES * repeat
    return np.meshgrid(t, t)


def normal_from_height(h, strength):
    dx = (np.roll(h, -1, axis=1) - np.roll(h, 1, axis=1)) * 0.5 * strength * RES / 256
    dy = (np.roll(h, -1, axis=0) - np.roll(h, 1, axis=0)) * 0.5 * strength * RES / 256
    n = np.stack([-dx, -dy, np.ones_like(h)], axis=-1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return n * 0.5 + 0.5


def save(name, rgb, alpha=None, linear=False):
    os.makedirs(OUT, exist_ok=True)
    rgba = np.ones((RES, RES, 4), dtype=np.float32)
    rgba[..., :3] = np.clip(rgb, 0, 1)
    if alpha is not None:
        rgba[..., 3] = np.clip(alpha, 0, 1)
    img = bpy.data.images.new(name, RES, RES, alpha=True, float_buffer=False)
    img.colorspace_settings.name = "Non-Color" if linear else "sRGB"
    # Blender: randul 0 e jos; imaginile noastre au randul 0 sus
    img.pixels.foreach_set(rgba[::-1].ravel())
    img.filepath_raw = os.path.join(OUT, name + ".png")
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


def gray(v, tint=(1.0, 1.0, 1.0)):
    return np.stack([v * tint[0], v * tint[1], v * tint[2]], axis=-1)


def texture_set(name, albedo, height, strength, metallic, smoothness):
    save(f"{name}_A", albedo)
    save(f"{name}_N", normal_from_height(height, strength), linear=True)
    m = np.zeros((RES, RES, 3))
    m[..., 0] = metallic
    save(f"{name}_M", m, alpha=smoothness, linear=True)
    print(f"TEX {name}")


# ----------------------------------------------------------------------------------------------
# Seturi (textura acopera 2 m pentru beton / podele / tencuiala / acoperis / lemn; 1 m pentru metale, textile)
# ----------------------------------------------------------------------------------------------

def concrete(name="Concrete", seed=1, base=0.8):
    large = fbm(3, 4, seed)
    fine = fbm(24, 4, seed + 7)
    f1, _ = worley(40, seed + 3)
    pores = np.clip(1 - f1 * 9, 0, 1) ** 2                      # gauri mici de aer
    stains = np.clip((fbm(2, 3, seed + 11) - 0.55) * 3, 0, 1)    # pete de apa / murdarie
    # rosturi de cofraj la fiecare 1 m pe orizontala (pe 2 m: doua randuri)
    _, v = grid_coords(2)
    seams = np.exp(-((v % 1.0) - 0.0) ** 2 / 0.00004) + np.exp(-((v % 1.0) - 1.0) ** 2 / 0.00004)
    a = base + (large - 0.5) * 0.12 + (fine - 0.5) * 0.08 - pores * 0.1 - stains * 0.1 - seams * 0.12
    h = fine * 0.6 + large * 0.2 - pores * 0.4 - seams * 0.5
    texture_set(name, gray(a, (1.0, 0.99, 0.97)), h, 2.0, 0.0, 0.12 + stains * 0.15)


def concrete_floor(name="ConcreteFloor", seed=20, base=0.78):
    large = fbm(3, 4, seed)
    fine = fbm(32, 3, seed + 5)
    f1, f2 = worley(5, seed + 9)
    cracks = np.clip(1 - (f2 - f1) * 40, 0, 1) * np.clip((fbm(4, 2, seed + 13) - 0.45) * 4, 0, 1)
    oil = np.clip((fbm(2, 4, seed + 17) - 0.6) * 4, 0, 1)
    # rosturi de dilatatie la 2 m (marginea texturii)
    u, v = grid_coords(1)
    joints = np.maximum(np.exp(-(np.minimum(u, 1 - u)) ** 2 / 0.00002), np.exp(-(np.minimum(v, 1 - v)) ** 2 / 0.00002))
    a = base + (large - 0.5) * 0.1 + (fine - 0.5) * 0.06 - cracks * 0.15 - oil * 0.12 - joints * 0.2
    h = fine * 0.3 + large * 0.2 - cracks - joints
    texture_set(name, gray(a, (1.0, 0.99, 0.98)), h, 1.6, 0.0, 0.25 + oil * 0.35)


def asphalt(name="Asphalt", seed=40):
    f1, _ = worley(90, seed)
    stones = np.clip(1 - f1 * 3, 0, 1)
    fine = fbm(40, 3, seed + 3)
    large = fbm(3, 3, seed + 5)
    a = 0.6 + stones * 0.12 + (fine - 0.5) * 0.08 + (large - 0.5) * 0.04
    h = stones * 0.6 + fine * 0.4
    texture_set(name, gray(a, (1.0, 0.99, 0.97)), h, 2.5, 0.0, 0.07 + (fine - 0.5) * 0.04)


def plaster(name="Plaster", seed=60):
    fine = fbm(30, 4, seed)
    large = fbm(3, 3, seed + 4)
    scuffs = np.clip((fbm(6, 3, seed + 8, stretch=(4, 1)) - 0.65) * 4, 0, 1)
    a = 0.86 + (fine - 0.5) * 0.05 + (large - 0.5) * 0.05 - scuffs * 0.12
    texture_set(name, gray(a), fine, 0.8, 0.0, 0.08 + scuffs * 0.1)


def painted_metal(name="PaintedMetal", seed=80):
    large = fbm(3, 4, seed)
    chips_mask = fbm(10, 5, seed + 3)
    chips = np.clip((chips_mask - 0.76) * 14, 0, 1)                       # vopsea sarita
    scratches = np.clip((fbm(4, 4, seed + 7, stretch=(16, 1)) - 0.62) * 8, 0, 1)
    grime = np.clip((fbm(2, 3, seed + 11) - 0.5) * 2, 0, 1)
    paint = 0.9 + (large - 0.5) * 0.06 - grime * 0.15
    metal = 0.55 + (fbm(30, 2, seed + 13) - 0.5) * 0.1
    a = paint * (1 - chips) + metal * chips - scratches * 0.12
    h = -chips * 0.6 - scratches * 0.3 + large * 0.1
    texture_set(name, gray(a), h, 1.4, chips * 0.9, 0.45 * (1 - chips) + 0.35 * chips - grime * 0.15)


def steel(name="Steel", seed=100):
    brushed = fbm(6, 5, seed, stretch=(40, 1))
    grime = np.clip((fbm(3, 4, seed + 5) - 0.45) * 2, 0, 1)
    rust = np.clip((fbm(6, 4, seed + 9) - 0.7) * 6, 0, 1)
    a = gray(0.62 + (brushed - 0.5) * 0.15 - grime * 0.18)
    a = a * (1 - rust[..., None]) + np.array([0.42, 0.24, 0.13]) * rust[..., None]
    texture_set(name, a, brushed * 0.3 - rust * 0.2, 0.8, 1.0 - rust * 0.8, 0.55 - grime * 0.25 - rust * 0.3)


def wood(name="Wood", seed=120):
    u, v = grid_coords(1)
    boards = 8                                                    # scanduri de 25 cm pe 2 m
    row = np.floor(v * boards)
    # fibrele merg pe lungimea scandurii (u); se unduiesc lent de-a lungul ei
    warp = fbm(3, 3, seed, stretch=(1, 6))
    rng = np.random.default_rng(seed)
    offsets = rng.random(boards) * 10
    local = (v * boards) % 1.0
    grain = np.sin((local * 5 + warp * 2.5 + offsets[row.astype(int) % boards]) * 2 * np.pi) * 0.5 + 0.5
    grain = grain ** 3
    fine = fbm(8, 4, seed + 3, stretch=(1, 16))
    gaps = np.exp(-((v * boards) % 1.0) ** 2 / 0.0015) + np.exp(-(1 - (v * boards) % 1.0) ** 2 / 0.0015)
    board_tone = rng.random(boards)[row.astype(int) % boards] * 0.12
    a = 0.82 - grain * 0.12 - (fine - 0.5) * 0.15 - board_tone - gaps * 0.5
    h = grain * 0.2 + fine * 0.2 - gaps
    texture_set(name, gray(a, (1.0, 0.93, 0.82)), h, 1.8, 0.0, 0.15)


def roof(name="RoofMembrane", seed=140):
    grains = fbm(60, 2, seed)
    large = fbm(3, 3, seed + 3)
    u, v = grid_coords(2)
    laps = np.exp(-((v % 1.0) - 0.02) ** 2 / 0.0002)              # suprapunerea membranei la 1 m
    puddles = np.clip((fbm(2, 4, seed + 7) - 0.62) * 5, 0, 1)
    a = 0.7 + (grains - 0.5) * 0.25 + (large - 0.5) * 0.1 - laps * 0.15 - puddles * 0.1
    texture_set(name, gray(a), grains * 0.5 + laps * 0.6, 1.5, 0.0, 0.1 + puddles * 0.5)


def woven(name, seed, threads, base, dirt):
    u, v = grid_coords(threads)
    warp = (np.sin(u * 2 * np.pi) * 0.5 + 0.5)
    weft = (np.sin(v * 2 * np.pi) * 0.5 + 0.5)
    checker = ((np.floor(u) + np.floor(v)) % 2)
    weave = np.where(checker > 0, warp, weft)
    stains = np.clip((fbm(3, 4, seed) - 0.5) * 2, 0, 1)
    a = base + (weave - 0.5) * 0.1 - stains * dirt
    texture_set(name, gray(a), weave, 1.2, 0.0, 0.05)


def rubber(name="Rubber", seed=180):
    fine = fbm(50, 3, seed)
    a = 0.55 + (fine - 0.5) * 0.1
    texture_set(name, gray(a), fine, 1.0, 0.0, 0.2)


def tiles(name="LabTiles", seed=200):
    u, v = grid_coords(4)                                         # dale de 50 cm pe 2 m
    gu, gv = u % 1.0, v % 1.0
    grout = np.maximum(np.exp(-(np.minimum(gu, 1 - gu)) ** 2 / 0.0002), np.exp(-(np.minimum(gv, 1 - gv)) ** 2 / 0.0002))
    rng = np.random.default_rng(seed)
    tone = rng.random((4, 4))[(np.floor(v).astype(int)) % 4, (np.floor(u).astype(int)) % 4] * 0.05
    fine = fbm(20, 3, seed + 3)
    scuffs = np.clip((fbm(5, 4, seed + 5) - 0.6) * 4, 0, 1)
    a = 0.88 - tone - (fine - 0.5) * 0.04 - grout * 0.35 - scuffs * 0.08
    texture_set(name, gray(a), -grout, 2.0, 0.0, 0.55 - grout * 0.45 - scuffs * 0.2)


def checker_plate(name="CheckerPlate", seed=220):
    u, v = grid_coords(10)                                        # lacrimi la 10 cm pe 1 m
    gu, gv = u % 1.0 - 0.5, v % 1.0 - 0.5
    alt = ((np.floor(u) + np.floor(v)) % 2) > 0
    # lacrima alungita, orientata pe diagonale alternante
    a1 = (gu + gv) / np.sqrt(2)
    a2 = (gu - gv) / np.sqrt(2)
    along = np.where(alt, a1, a2)
    across = np.where(alt, a2, a1)
    bump = np.clip(1 - (along / 0.32) ** 2 - (across / 0.08) ** 2, 0, 1)
    grime = np.clip((fbm(3, 4, seed) - 0.45) * 2, 0, 1)
    a = gray(0.62 - grime * 0.2 + bump * 0.06)
    texture_set(name, a, bump, 3.0, 1.0, 0.5 - grime * 0.25)


def gravel(name="Gravel", seed=240):
    f1, f2 = worley(30, seed)
    stones = np.clip(1 - f1 * 2.2, 0, 1)
    edges = np.clip((f2 - f1) * 6, 0, 1)
    rng = np.random.default_rng(seed)
    tone = fbm(30, 1, seed + 3)
    a = 0.55 + stones * 0.2 + (tone - 0.5) * 0.2 - (1 - edges) * 0.1
    texture_set(name, gray(a, (1.0, 0.98, 0.95)), stones, 3.0, 0.0, 0.1)


# ----------------------------------------------------------------------------------------------
# Personaje: camuflaj si tesaturi (culoarea e in textura; materialul Unity ramane alb)
# ----------------------------------------------------------------------------------------------

def camo(name, seed, colors, scale=3):
    """Camuflaj cu pete: colors = [baza, pata1, pata2, pata3] (r, g, b); textura acopera 1 m."""
    base = np.array(colors[0])
    out = np.ones((RES, RES, 3)) * base
    for k, color in enumerate(colors[1:]):
        field = fbm(scale * (1 + k * 0.6), 3, seed + 17 * k)
        edge = fbm(scale * 6, 2, seed + 101 * k)
        mask = np.clip((field + (edge - 0.5) * 0.12 - (0.56 - k * 0.02)) * 18, 0, 1)
        out = out * (1 - mask[..., None]) + np.array(color) * mask[..., None]
    # tesatura fina si uzura
    u, v = grid_coords(400)
    weave = (np.sin(u * 2 * np.pi) * 0.5 + 0.5) * (np.sin(v * 2 * np.pi) * 0.5 + 0.5)
    wear = np.clip((fbm(2, 4, seed + 7) - 0.5) * 1.5, 0, 1)
    out = out * (0.92 + weave[..., None] * 0.1) * (1 - wear[..., None] * 0.15)
    texture_set(name, out, weave * 0.4 + fbm(20, 3, seed + 9) * 0.3, 0.8, 0.0, 0.08 + wear * 0.06)


def fabric(name, seed, color, threads=300, sheen=0.18):
    """Tesatura uniforma (nylon, cordura, cagula)."""
    u, v = grid_coords(threads)
    weave = (np.sin(u * 2 * np.pi) * 0.5 + 0.5) * (np.sin(v * 2 * np.pi) * 0.5 + 0.5)
    dirt = np.clip((fbm(3, 4, seed) - 0.5) * 2, 0, 1)
    out = np.ones((RES, RES, 3)) * np.array(color) * (0.9 + weave[..., None] * 0.2) * (1 - dirt[..., None] * 0.2)
    texture_set(name, out, weave * 0.6 + fbm(30, 2, seed + 3) * 0.2, 1.0, 0.0, sheen - dirt * 0.08)


def plastic(name, seed, color, smooth=0.38):
    """Plastic mat cu granulatie fina si zgarieturi (casca, genunchiere, rucsac rigid)."""
    grain = fbm(60, 3, seed)
    scratches = np.clip((fbm(5, 4, seed + 7, stretch=(12, 1)) - 0.66) * 8, 0, 1)
    out = np.ones((RES, RES, 3)) * np.array(color) * (0.96 + (grain[..., None] - 0.5) * 0.08) + scratches[..., None] * 0.12
    texture_set(name, out, grain * 0.15 - scratches * 0.3, 0.6, 0.0, smooth - scratches * 0.15)


def main():
    camo("CT_Camo", 300, [(0.20, 0.24, 0.34), (0.11, 0.13, 0.20), (0.36, 0.40, 0.47), (0.05, 0.06, 0.09)])
    camo("T_Camo", 320, [(0.30, 0.34, 0.20), (0.17, 0.21, 0.12), (0.44, 0.42, 0.28), (0.10, 0.12, 0.07)])
    camo("T_CamoDesert", 340, [(0.52, 0.45, 0.32), (0.36, 0.30, 0.20), (0.62, 0.56, 0.42), (0.30, 0.32, 0.22)])
    fabric("Nylon", 360, (0.09, 0.09, 0.10))
    fabric("NylonOlive", 370, (0.30, 0.32, 0.22))
    plastic("Plastic", 380, (0.07, 0.07, 0.08))
    concrete("Concrete", 1, 0.82)
    concrete("ConcreteDark", 5, 0.72)
    concrete_floor()
    asphalt()
    plaster()
    painted_metal()
    steel()
    wood()
    roof()
    woven("Hesco", 160, 60, 0.85, 0.25)
    woven("Canvas", 170, 90, 0.86, 0.18)
    rubber()
    tiles()
    checker_plate()
    gravel()
    print("TEXTURES_DONE")


main()
