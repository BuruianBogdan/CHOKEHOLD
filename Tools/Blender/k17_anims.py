"""
CHOKEHOLD - animatii pentru persoana a treia, generate din cod pe scheletul K17 (Unity Humanoid).

Fiecare clip e o actiune Blender creata cadru cu cadru dintr-o functie de poza f(t) si exportata ca FBX
(doar armatura) in Assets/_Project/Art/Animations/<Hold>/<Hold>_<Grup>_<Actiune>.fbx.
Toate clipurile sunt pe loc (fara root motion): miscarea vine din FpsController.

    Hold:   Rifle, Pistol (pozitia bratelor pe arma)
    Loco:   Idle, Walk_<dir>, Run_<dir>, Sprint, CrouchIdle, CrouchWalk_<dir>, TurnL, TurnR   (dir: F B L R FL FR BL BR)
    Air:    JumpStart, Fall, Land
    Aim:    <U|C|D>_<L|C|R> (9 poze, 1 cadru), LeanL, LeanR
    Weapon: Fire, Reload, ReloadEmpty, Draw, Holster
    Action: Plant, Defuse (bucla), Door, Ladder (bucla), Barricade          (doar Rifle; mainile lasa arma)
    React:  HitF, HitB, HitL, HitR

Rulare: D:/Blender/blender.exe -b --factory-startup --python Tools/Blender/k17_anims.py [-- Rifle|Pistol] [-- quick]
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

from k17_characters import build_armature, deform_pose, reset_pose  # noqa: E402
from k17_common import REPO, B, export_fbx, reset_scene  # noqa: E402

V = Vector
FPS = 30
OUT = os.path.join(REPO, "Assets", "_Project", "Art", "Animations")

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
QUICK = "quick" in ARGS

DIRECTIONS = {
    "F": V((0, 0, 1)), "B": V((0, 0, -1)), "L": V((-1, 0, 0)), "R": V((1, 0, 0)),
    "FL": V((-1, 0, 1)).normalized(), "FR": V((1, 0, 1)).normalized(),
    "BL": V((-1, 0, -1)).normalized(), "BR": V((1, 0, -1)).normalized(),
}


# ----------------------------------------------------------------------------------------------
# Unelte de poza (coordonate de personaj: x dreapta, y sus, z inainte)
# ----------------------------------------------------------------------------------------------

def rot(v, axis, degrees):
    return Matrix.Rotation(math.radians(degrees), 3, V(axis).normalized()) @ V(v)


class Rig:
    def __init__(self, arm):
        self.armature = arm
        self.pb = arm.pose.bones

    def update(self):
        bpy.context.view_layer.update()

    def aim(self, name, direction):
        self.update()
        pb = self.pb[name]
        m = pb.matrix.copy()
        head = m.translation.copy()
        current = (pb.tail - pb.head).normalized()
        wanted = B(*direction).normalized()
        r = current.rotation_difference(wanted).to_matrix().to_4x4()
        pb.matrix = Matrix.Translation(head) @ r @ Matrix.Translation(-head) @ m

    def turn(self, name, axis, degrees):
        if abs(degrees) < 1e-4:
            return
        self.update()
        pb = self.pb[name]
        m = pb.matrix.copy()
        head = m.translation.copy()
        r = Matrix.Rotation(math.radians(degrees), 4, B(*axis).normalized())
        pb.matrix = Matrix.Translation(head) @ r @ Matrix.Translation(-head) @ m

    def move(self, name, offset):
        self.update()
        pb = self.pb[name]
        m = pb.matrix.copy()
        m.translation += B(*offset)
        pb.matrix = m

    def reset(self):
        reset_pose(self.armature)

    # ---- blocuri de poza ----

    def leg(self, side, swing_axis, swing, knee, foot_pitch=0.0, spread=0.0):
        """Picior: unghi de balans (grade, pozitiv = inainte pe axa data), indoire genunchi, talpa."""
        s = 1 if side == "Right" else -1
        down = V((s * 0.02 + s * spread, -1.0, 0.02))
        thigh = rot(down, swing_axis, swing)
        self.aim(f"{side}UpperLeg", thigh)
        shin = rot(thigh, swing_axis, -knee)
        self.aim(f"{side}LowerLeg", shin)
        foot = rot(V((0, -0.2, 1.0)), swing_axis, swing - knee + foot_pitch)
        self.aim(f"{side}Foot", foot)

    def spine(self, pitch=0.0, yaw=0.0, roll=0.0):
        for bone, f in (("Spine", 0.4), ("Chest", 0.35), ("UpperChest", 0.25)):
            self.turn(bone, (1, 0, 0), pitch * f)
            self.turn(bone, (0, 1, 0), yaw * f)
            self.turn(bone, (0, 0, 1), roll * f)

    def head(self, pitch=0.0, yaw=0.0):
        self.turn("Neck", (1, 0, 0), pitch * 0.4)
        self.turn("Neck", (0, 1, 0), yaw * 0.4)
        self.turn("Head", (1, 0, 0), pitch * 0.6)
        self.turn("Head", (0, 1, 0), yaw * 0.6)

    def arm(self, side, upper, lower, hand=None):
        self.aim(f"{side}UpperArm", upper)
        self.aim(f"{side}LowerArm", lower)
        if hand is not None:
            self.aim(f"{side}Hand", hand)

    def fingers_curl(self, side, amount=1.0):
        s = 1 if side == "Right" else -1
        for finger in ("Index", "Middle", "Ring", "Little"):
            for phalanx, deg in (("Proximal", 55), ("Intermediate", 60), ("Distal", 40)):
                self.turn(f"{side}{finger}{phalanx}", (0, 0, 1), -s * deg * amount)
        for phalanx, deg in (("Proximal", 25), ("Intermediate", 30), ("Distal", 25)):
            self.turn(f"{side}Thumb{phalanx}", (0, 1, 0), s * deg * amount)

    def hold(self, kind, pull=0.0, lift=0.0, left_free=False):
        """Bratele pe arma. pull = recul (0..1), lift = ridicare la tinta sus; left_free = mana stanga libera."""
        if kind == "Rifle":
            self.arm("Right", (0.40, -0.55 + lift * 0.3, 0.72 - pull * 0.15), (-0.40, 0.15 + lift * 0.2, 0.90 - pull * 0.2),
                     (0.0, 0.05, 1.0))
            if not left_free:
                self.arm("Left", (-0.30, -0.50 + lift * 0.3, 0.80 - pull * 0.1), (0.55, 0.05 + lift * 0.2, 0.83 - pull * 0.2),
                         (0.15, 0.0, 1.0))
        else:
            self.arm("Right", (0.18, -0.20 + lift * 0.4, 0.96 - pull * 0.25), (-0.05, 0.05 + lift * 0.3, 1.0 - pull * 0.3),
                     (0.0, 0.0, 1.0))
            if not left_free:
                self.arm("Left", (-0.22, -0.25 + lift * 0.4, 0.94 - pull * 0.25), (0.20, 0.05 + lift * 0.3, 0.97 - pull * 0.3),
                         (0.05, 0.0, 1.0))
        self.fingers_curl("Right", 0.9)
        if not left_free:
            self.fingers_curl("Left", 0.8)

    def arms_down(self):
        self.arm("Right", (0.12, -0.98, 0.1), (0.05, -0.95, 0.3), (0, -0.9, 0.4))
        self.arm("Left", (-0.12, -0.98, 0.1), (-0.05, -0.95, 0.3), (0, -0.9, 0.4))
        self.fingers_curl("Right", 0.3)
        self.fingers_curl("Left", 0.3)

    def crouch(self, amount=1.0):
        self.move("Hips", (0, -0.42 * amount, -0.06 * amount))
        for side in ("Right", "Left"):
            s = 1 if side == "Right" else -1
            self.leg(side, (1, 0, 0), 62 * amount, 105 * amount, foot_pitch=-15 * amount, spread=0.12 * amount)
        self.spine(pitch=12 * amount)


# ----------------------------------------------------------------------------------------------
# Functii de poza f(rig, t) cu t in [0, 1) pentru bucle, [0, 1] pentru actiuni
# ----------------------------------------------------------------------------------------------

def breathe(rig, t, amp=1.0):
    rig.spine(pitch=1.2 * amp * math.sin(2 * math.pi * t))
    rig.move("Hips", (0, 0.004 * amp * math.sin(2 * math.pi * t), 0))


def idle(kind):
    def f(rig, t):
        breathe(rig, t)
        rig.leg("Right", (1, 0, 0), 2, 4, spread=0.05)
        rig.leg("Left", (1, 0, 0), -2, 4, spread=0.05)
        rig.hold(kind)
    return f


def gait(kind, direction, speed, crouch=0.0):
    """Mers / alergare: picioarele baleaza pe directia miscarii; trunchiul se apleaca spre ea."""
    m = DIRECTIONS[direction]
    axis = V((0, 1, 0)).cross(m)                      # axa in jurul careia piciorul merge "inainte" spre m
    swing_amp = {"walk": 22, "run": 38, "sprint": 48}[speed] * (0.6 if crouch else 1.0)
    knee_amp = {"walk": 45, "run": 70, "sprint": 85}[speed]
    lean = {"walk": 4, "run": 10, "sprint": 16}[speed]
    bob = {"walk": 0.02, "run": 0.035, "sprint": 0.045}[speed]

    def f(rig, t):
        if crouch:
            rig.crouch(crouch)
        for side, phase in (("Right", 0.0), ("Left", 0.5)):
            p = (t + phase) % 1.0
            swing = swing_amp * math.sin(2 * math.pi * p)
            knee = knee_amp * max(0.0, math.sin(2 * math.pi * p + 0.9)) ** 1.5 + (105 * crouch if crouch else 6)
            rig.leg(side, axis, swing, knee, foot_pitch=-8 * max(0.0, math.sin(2 * math.pi * p)), spread=0.12 * crouch)
        rig.move("Hips", (0.012 * math.sin(2 * math.pi * t), -bob * abs(math.sin(2 * math.pi * t)) , 0))
        rig.spine(pitch=lean * m.z, roll=-lean * 0.5 * m.x, yaw=-4 * math.sin(2 * math.pi * t))
        rig.hold(kind, pull=0.0, lift=0.0)
        if speed == "sprint":
            # la sprint arma e tinuta jos, in diagonala
            rig.arm("Right", (0.35, -0.75, 0.55), (-0.30, -0.30, 0.90), (0, -0.1, 1.0))
            rig.arm("Left", (-0.30, -0.65, 0.65), (0.55, -0.15, 0.80), (0.1, -0.1, 1.0))
    return f


def crouch_idle(kind):
    def f(rig, t):
        rig.crouch(1.0)
        breathe(rig, t, 0.7)
        rig.hold(kind)
    return f


def turn_in_place(kind, side):
    s = 1 if side == "R" else -1

    def f(rig, t):
        a = math.sin(math.pi * t)
        rig.move("Hips", (0, -0.015 * a, 0))
        rig.turn("Hips", (0, 1, 0), s * 20 * a)
        rig.leg("Right", (0, 0, 1), s * 10 * a, 15 * a, spread=0.05)
        rig.leg("Left", (0, 0, 1), s * 10 * a, 15 * a, spread=0.05)
        rig.spine(yaw=-s * 12 * a)
        rig.hold(kind)
    return f


def jump_start(kind):
    def f(rig, t):
        c = 0.5 * (1 - math.cos(math.pi * min(1.0, t * 1.4)))
        rig.crouch(0.45 * (1 - c))
        rig.spine(pitch=8 * (1 - c))
        rig.hold(kind)
    return f


def fall(kind):
    def f(rig, t):
        rig.leg("Right", (1, 0, 0), 10, 35, spread=0.08)
        rig.leg("Left", (1, 0, 0), -5, 25, spread=0.08)
        rig.spine(pitch=-4)
        rig.hold(kind, lift=0.1)
    return f


def land(kind):
    def f(rig, t):
        c = math.sin(math.pi * min(1.0, t))
        rig.crouch(0.5 * c)
        rig.hold(kind)
    return f


def aim_pose(kind, pitch, yaw):
    def f(rig, t):
        rig.spine(pitch=-pitch * 0.55, yaw=yaw * 0.6)
        rig.head(pitch=-pitch * 0.45, yaw=yaw * 0.4)
        rig.hold(kind, lift=max(0.0, pitch) / 60)
    return f


def lean_pose(kind, side):
    s = 1 if side == "R" else -1

    def f(rig, t):
        rig.move("Hips", (s * 0.06, 0, 0))
        rig.spine(roll=-s * 22)
        rig.head(yaw=0)
        rig.turn("Neck", (0, 0, 1), s * 8)
        rig.hold(kind)
    return f


def fire(kind):
    def f(rig, t):
        pull = math.exp(-t * 6.0) * math.sin(math.pi * min(1.0, t * 4.0 + 0.0)) if t < 0.5 else 0.0
        pull = max(0.0, math.exp(-t * 5.0) - math.exp(-5.0)) * 1.2
        rig.spine(pitch=-3 * pull)
        rig.hold(kind, pull=pull)
    return f


def reload(kind, empty=False):
    """Mana stanga scoate incarcatorul (jos), aduce altul, il baga; la gol trage si maneta."""
    def f(rig, t):
        rig.spine(pitch=6)
        rig.head(pitch=12)
        rig.hold(kind, left_free=True)
        if t < 0.25:                                           # apasa eliberarea, scoate incarcatorul
            k = t / 0.25
            rig.arm("Left", (-0.25, -0.70 + 0.2 * (1 - k), 0.65), (0.45, -0.35 * k, 0.85), (0.1, -0.3, 1.0))
        elif t < 0.55:                                         # mana jos, la punga
            k = (t - 0.25) / 0.30
            rig.arm("Left", (-0.20, -0.95, 0.25), (0.40, -0.70 + 0.3 * math.sin(math.pi * k), 0.60), (0.1, -0.5, 0.8))
        elif t < 0.85:                                         # incarcatorul nou urca si intra
            k = (t - 0.55) / 0.30
            rig.arm("Left", (-0.25, -0.80 + 0.3 * k, 0.55), (0.50, -0.25 + 0.25 * k, 0.80), (0.1, -0.2, 1.0))
        else:                                                  # mana revine pe handguard / sub pistol
            k = (t - 0.85) / 0.15
            if empty and kind == "Rifle":
                rig.arm("Left", (-0.20, -0.45, 0.80), (0.70, 0.15, 0.60 + 0.2 * k), (0.2, 0.0, 1.0))
            else:
                rig.arm("Left", (-0.30, -0.50, 0.80), (0.55, 0.05, 0.83), (0.15, 0.0, 1.0))
        rig.fingers_curl("Left", 0.7)
    return f


def draw(kind, reverse=False):
    def f(rig, t):
        k = 1 - t if reverse else t
        c = 0.5 * (1 - math.cos(math.pi * k))
        rig.arms_down()
        if kind == "Rifle":
            rig.arm("Right", (0.40 * c + 0.12 * (1 - c), -0.55 * c - 0.98 * (1 - c), 0.72 * c + 0.1 * (1 - c)),
                    (-0.40 * c + 0.05 * (1 - c), 0.15 * c - 0.95 * (1 - c), 0.90 * c + 0.3 * (1 - c)))
            rig.arm("Left", (-0.30 * c - 0.12 * (1 - c), -0.50 * c - 0.98 * (1 - c), 0.80 * c + 0.1 * (1 - c)),
                    (0.55 * c - 0.05 * (1 - c), 0.05 * c - 0.95 * (1 - c), 0.83 * c + 0.3 * (1 - c)))
        else:
            rig.arm("Right", (0.18 * c + 0.12 * (1 - c), -0.20 * c - 0.98 * (1 - c), 0.96 * c + 0.1 * (1 - c)),
                    (-0.05 * c + 0.05 * (1 - c), 0.05 * c - 0.95 * (1 - c), 1.0 * c + 0.3 * (1 - c)))
            rig.arm("Left", (-0.22 * c - 0.12 * (1 - c), -0.25 * c - 0.98 * (1 - c), 0.94 * c + 0.1 * (1 - c)),
                    (0.20 * c - 0.05 * (1 - c), 0.05 * c - 0.95 * (1 - c), 0.97 * c + 0.3 * (1 - c)))
        rig.fingers_curl("Right", 0.9)
        rig.fingers_curl("Left", 0.8)
    return f


def plant():
    def f(rig, t):
        rig.crouch(1.0)
        rig.spine(pitch=18)
        rig.head(pitch=25)
        rig.arm("Left", (-0.30, -0.80, 0.55), (0.30, -0.55, 0.80), (0.0, -0.6, 0.8))
        k = math.sin(math.pi * t)
        rig.arm("Right", (0.30, -0.85, 0.45), (-0.10, -0.55 - 0.15 * k, 0.80), (0.0, -0.6, 0.8))
        rig.fingers_curl("Right", 0.5)
        rig.fingers_curl("Left", 0.5)
    return f


def defuse():
    def f(rig, t):
        rig.crouch(1.0)
        rig.spine(pitch=16)
        rig.head(pitch=28)
        j = 0.03 * math.sin(2 * math.pi * 2 * t)
        rig.arm("Right", (0.25, -0.80, 0.55), (-0.15 + j, -0.50, 0.85), (0.0, -0.5, 0.85))
        rig.arm("Left", (-0.25, -0.80, 0.55), (0.15 - j, -0.50, 0.85), (0.0, -0.5, 0.85))
        rig.fingers_curl("Right", 0.4)
        rig.fingers_curl("Left", 0.4)
    return f


def door():
    def f(rig, t):
        rig.hold("Rifle", left_free=True)
        k = math.sin(math.pi * t)
        rig.arm("Left", (-0.30, -0.45, 0.85), (0.20, 0.25 * k, 0.95), (0.0, 0.0, 1.0))
        rig.fingers_curl("Left", 0.2)
        rig.spine(pitch=3 * k)
    return f


def ladder():
    def f(rig, t):
        rig.spine(pitch=-6)
        rig.head(pitch=-20)
        for side, phase in (("Right", 0.0), ("Left", 0.5)):
            p = (t + phase) % 1.0
            lift = max(0.0, math.sin(2 * math.pi * p))
            rig.leg(side, (1, 0, 0), 25 + 40 * lift, 70 + 50 * lift, foot_pitch=-20)
            up = 0.75 + 0.35 * lift
            rig.arm(side, ((0.15 if side == "Right" else -0.15), up, 0.55), (0.0, 0.95, 0.30), (0, 1.0, 0.2))
            rig.fingers_curl(side, 0.9)
        rig.move("Hips", (0, 0.03 * math.sin(2 * math.pi * 2 * t), 0.05))
    return f


def barricade():
    def f(rig, t):
        k = 0.5 * (1 - math.cos(2 * math.pi * t))
        rig.spine(pitch=8 * k)
        rig.arm("Right", (0.30, -0.35, 0.88), (-0.15, 0.10, 0.98), (0, 0, 1))
        rig.arm("Left", (-0.30, -0.35, 0.88), (0.15, 0.10, 0.98), (0, 0, 1))
        rig.move("Hips", (0, -0.03 * k, 0.03 * k))
        rig.fingers_curl("Right", 0.3)
        rig.fingers_curl("Left", 0.3)
    return f


def hit_react(kind, direction):
    pitch, roll = {"F": (-14, 0), "B": (12, 0), "L": (0, 16), "R": (0, -16)}[direction]

    def f(rig, t):
        a = math.sin(math.pi * min(1.0, t)) * math.exp(-t * 2.0)
        rig.spine(pitch=pitch * a, roll=roll * a)
        rig.head(pitch=pitch * 0.8 * a)
        rig.move("Hips", (0.02 * roll / 16 * a, -0.02 * a, 0.03 * pitch / 14 * a))
        rig.hold(kind, pull=0.4 * a)
    return f


# ----------------------------------------------------------------------------------------------
# Lista de clipuri: (nume, durata s, functie, bucla)
# ----------------------------------------------------------------------------------------------

def clip_list(kind):
    clips = [
        (f"{kind}_Loco_Idle", 2.0, idle(kind), True),
        (f"{kind}_Loco_Sprint", 0.6, gait(kind, "F", "sprint"), True),
        (f"{kind}_Loco_CrouchIdle", 2.0, crouch_idle(kind), True),
        (f"{kind}_Loco_TurnL", 0.6, turn_in_place(kind, "L"), False),
        (f"{kind}_Loco_TurnR", 0.6, turn_in_place(kind, "R"), False),
        (f"{kind}_Air_JumpStart", 0.3, jump_start(kind), False),
        (f"{kind}_Air_Fall", 1.0, fall(kind), True),
        (f"{kind}_Air_Land", 0.4, land(kind), False),
        (f"{kind}_Aim_LeanL", 0.0, lean_pose(kind, "L"), False),
        (f"{kind}_Aim_LeanR", 0.0, lean_pose(kind, "R"), False),
        (f"{kind}_Weapon_Fire", 0.25, fire(kind), False),
        (f"{kind}_Weapon_Reload", 2.2 if kind == "Rifle" else 1.8, reload(kind), False),
        (f"{kind}_Weapon_ReloadEmpty", 2.8 if kind == "Rifle" else 2.1, reload(kind, empty=True), False),
        (f"{kind}_Weapon_Draw", 0.6, draw(kind), False),
        (f"{kind}_Weapon_Holster", 0.6, draw(kind, reverse=True), False),
    ]
    for d in DIRECTIONS:
        clips.append((f"{kind}_Loco_Walk_{d}", 1.1, gait(kind, d, "walk"), True))
        clips.append((f"{kind}_Loco_Run_{d}", 0.75, gait(kind, d, "run"), True))
        clips.append((f"{kind}_Loco_CrouchWalk_{d}", 1.3, gait(kind, d, "walk", crouch=1.0), True))
    for pname, pitch in (("U", 60), ("C", 0), ("D", -60)):
        for yname, yaw in (("L", -45), ("C", 0), ("R", 45)):
            clips.append((f"{kind}_Aim_{pname}_{yname}", 0.0, aim_pose(kind, pitch, yaw), False))
    for d in ("F", "B", "L", "R"):
        clips.append((f"{kind}_React_Hit{d}", 0.5, hit_react(kind, d), False))
    if kind == "Rifle":
        clips += [
            ("Rifle_Action_Plant", 1.2, plant(), True),
            ("Rifle_Action_Defuse", 1.0, defuse(), True),
            ("Rifle_Action_Door", 0.8, door(), False),
            ("Rifle_Action_Ladder", 1.0, ladder(), True),
            ("Rifle_Action_Barricade", 1.0, barricade(), True),
        ]
    return clips


# ----------------------------------------------------------------------------------------------
# Creare si export
# ----------------------------------------------------------------------------------------------

def bake_clip(arm, rig, name, duration, pose, loop):
    frames = max(1, int(round(duration * FPS)))
    action = bpy.data.actions.new(name)
    arm.animation_data_create()
    arm.animation_data.action = action
    scene = bpy.context.scene
    scene.frame_start = 0
    scene.frame_end = frames
    scene.render.fps = FPS

    for f in range(frames + 1):
        scene.frame_set(f)
        t = f / frames if frames > 0 else 0.0
        if loop and frames > 0:
            t = (f % frames) / frames       # ultimul cadru = primul: bucla se inchide exact
        rig.reset()
        pose(rig, t)
        rig.update()
        for pb in arm.pose.bones:
            pb.keyframe_insert("rotation_quaternion", frame=f)
            if pb.name == "Hips":
                pb.keyframe_insert("location", frame=f)
    return action


def main():
    kinds = [a for a in ARGS if a in ("Rifle", "Pistol")] or ["Rifle", "Pistol"]
    total = 0

    for kind in kinds:
        clips = clip_list(kind)
        if QUICK:
            clips = clips[:6]
        for name, duration, pose, loop in clips:
            reset_scene()
            arm = build_armature()
            rig = Rig(arm)
            bake_clip(arm, rig, name, duration, pose, loop)
            path = os.path.join(OUT, kind, f"{name}.fbx")
            export_fbx(path, [arm], with_armature=True, bake_anim=True)
            total += 1
            print(f"CLIP {name} ({duration:.2f}s{' loop' if loop else ''})")

    print(f"ANIMS_DONE {total}")


if __name__ == "__main__":
    main()
