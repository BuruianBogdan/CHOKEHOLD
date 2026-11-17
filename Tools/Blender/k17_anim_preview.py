"""
CHOKEHOLD - previzualizarea pozelor din clipuri pe corpul de baza (Docs/Characters/Anims/*.png).

Rulare: D:/Blender/blender.exe -b --factory-startup --python Tools/Blender/k17_anim_preview.py
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from k17_anims import Rig, clip_list  # noqa: E402
from k17_characters import build_armature, build_body, skin  # noqa: E402
from k17_common import REPO, render_view, reset_scene, workbench_scene  # noqa: E402

OUT = os.path.join(REPO, "Docs", "Characters", "Anims")

# (clip, t) de verificat
SHOTS = [
    ("Rifle_Loco_Idle", 0.0), ("Rifle_Loco_Walk_F", 0.25), ("Rifle_Loco_Run_F", 0.25), ("Rifle_Loco_Run_L", 0.25),
    ("Rifle_Loco_Sprint", 0.25), ("Rifle_Loco_CrouchIdle", 0.0), ("Rifle_Loco_CrouchWalk_F", 0.25),
    ("Rifle_Aim_U_C", 0.0), ("Rifle_Aim_D_R", 0.0), ("Rifle_Aim_LeanR", 0.0), ("Rifle_Weapon_Reload", 0.45),
    ("Rifle_Action_Plant", 0.5), ("Rifle_Action_Ladder", 0.25), ("Rifle_React_HitF", 0.2),
    ("Pistol_Loco_Idle", 0.0), ("Pistol_Weapon_Fire", 0.1),
]


def main():
    reset_scene()
    arm = build_armature()
    body = build_body()
    skin(body, arm)
    rig = Rig(arm)
    cam = workbench_scene(resolution=(800, 800))
    poses = {name: pose for kind in ("Rifle", "Pistol") for name, _, pose, _ in clip_list(kind)}

    for name, t in SHOTS:
        rig.reset()
        poses[name](rig, t)
        rig.update()
        render_view(cam, (1.9, 1.3, 2.6), (0, 0.95, 0.1), 42, os.path.join(OUT, f"{name}_{int(t * 100):02d}.png"))
        print("SHOT", name)

    print("PREVIEW_DONE")


main()
