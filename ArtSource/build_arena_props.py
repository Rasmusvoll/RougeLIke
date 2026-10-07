"""Builds the woodland battle-arena pieces (the clearing board, trees, rocks, mushrooms...) and
exports them as FBX into Assets/Art/Models/Arena.

Run inside Blender 5.x: `python ArtSource/blender_send.py ArtSource/build_arena_props.py` with the
Blender Lab MCP add-on running. Style rules and palette: ArtSource/STYLE.md.
Every prop's origin is on the ground at its base.
"""
import math
import os

exec(open(r"D:\RougeLIke\ArtSource\toon_common.py", encoding="utf-8").read())

EXPORT_DIR = os.path.join(PROJECT, "Assets", "Art", "Models", "Arena")
BLEND_OUT = os.path.join(PROJECT, "ArtSource", "arena_props.blend")

# The clearing covers the 15 x 10 battle field plus a margin; BattleManager's ground collider
# matches the field, so keep these in step with BattleManager.fieldSize.
CLEARING_HALF = (9.2, 6.6)


def clearing():
    """A rounded parchment board, like a clearing on the Root map, with a darker worn rim."""
    b = Builder()
    pts, n, p = [], 56, 4.0
    for i in range(n):
        t = 2 * math.pi * i / n
        c, s = math.cos(t), math.sin(t)
        # Superellipse: a rectangle with soft corners, with a little hand-drawn wobble.
        wobble = 1 + 0.012 * math.sin(t * 7) + 0.008 * math.sin(t * 13 + 1)
        x = CLEARING_HALF[0] * math.copysign(abs(c) ** (2 / p), c) * wobble
        z = CLEARING_HALF[1] * math.copysign(abs(s) ** (2 / p), s) * wobble
        pts.append((x, z))
    b.slab(pts, 0.0, -0.35, "Parchment:paper", "ParchmentDark")
    return b


def pine_tree():
    b = Builder()
    b.tube((0, 0, 0), (0, 0.7, 0), 0.2, 0.15, "Bark")
    for (y0, y1, r), mat in zip(((0.45, 1.75, 1.05), (1.15, 2.45, 0.82), (1.85, 3.15, 0.58)),
                                ("Pine", "Forest", "Pine")):
        b.tube((0, y0, 0), (0, y1, 0), r, 0.0, mat, segs=8)
    return b


def oak_tree():
    b = Builder()
    b.tube((0, 0, 0), (0, 1.4, 0), 0.26, 0.18, "Bark")
    b.tube((0, 1.0, 0), (0.45, 1.6, 0.1), 0.1, 0.06, "Bark")
    for c, s, mat in (((0, 2.1, 0), (1.9, 1.5, 1.8), "Forest"),
                      ((0.65, 1.8, 0.3), (1.1, 0.9, 1.1), "Moss"),
                      ((-0.6, 1.85, -0.2), (1.2, 1.0, 1.1), "Forest"),
                      ((0.1, 2.75, 0.1), (1.1, 0.9, 1.0), "Moss")):
        b.blob(c, s, mat, subdiv=1)
    return b


def birch_tree():
    """A slim pale tree with a rust-orange autumn crown."""
    b = Builder()
    b.tube((0, 0, 0), (0.05, 2.0, 0), 0.14, 0.09, "Cream")
    for y in (0.5, 1.1, 1.6):
        b.blob((0.0, y, 0.12), (0.12, 0.05, 0.03), "Ink:noline", subdiv=1)
    for c, s in (((0.05, 2.3, 0), (1.3, 1.1, 1.2)), ((0.4, 2.0, 0.2), (0.8, 0.7, 0.8)),
                 ((-0.35, 2.1, -0.1), (0.8, 0.7, 0.8))):
        b.blob(c, s, "Rust", subdiv=1)
    return b


def rock():
    b = Builder()
    b.blob((0, 0.25, 0), (1.0, 0.65, 0.85), "Stone", subdiv=1, rot=(0, 20, 8))
    b.blob((0.45, 0.14, 0.25), (0.5, 0.35, 0.5), "StoneLight", subdiv=1, rot=(0, -30, 0))
    return b


def mushrooms():
    b = Builder()
    for (x, z, h, r) in ((0, 0, 0.42, 0.3), (0.32, 0.18, 0.26, 0.19)):
        b.tube((x, 0, z), (x, h, z), r * 0.32, r * 0.26, "Cream")
        b.dome((x, h - 0.04, z), (r * 2, r * 0.9, r * 2), "Rust", segs=10, rings=6)
        for a in range(4):
            t = a * math.pi / 2 + 0.4
            b.blob((x + math.cos(t) * r * 0.55, h + r * 0.45, z + math.sin(t) * r * 0.55),
                   (r * 0.28, r * 0.12, r * 0.28), "Cream:noline", subdiv=1)
    return b


def stump():
    b = Builder()
    b.tube((0, 0, 0), (0, 0.42, 0), 0.42, 0.36, "Bark", segs=9)
    b.blob((0, 0.42, 0), (0.7, 0.06, 0.7), "Wood:noline", subdiv=1)
    for a in range(3):
        t = a * 2.1 + 0.3
        b.tube((math.cos(t) * 0.3, 0.15, math.sin(t) * 0.3), (math.cos(t) * 0.7, 0.0, math.sin(t) * 0.7),
               0.12, 0.04, "Bark", segs=5)
    return b


def bush():
    b = Builder()
    for c, s, mat in (((0, 0.4, 0), (1.1, 0.8, 1.0), "Forest"), ((0.45, 0.3, 0.2), (0.7, 0.55, 0.7), "Moss"),
                      ((-0.4, 0.32, -0.1), (0.75, 0.6, 0.7), "Forest")):
        b.blob(c, s, mat, subdiv=1)
    for x, z in ((0.2, 0.42), (-0.2, 0.38)):
        b.blob((x, 0.55, z), (0.1, 0.1, 0.1), "Brick:noline", subdiv=1)  # berries
    return b


def grass_tuft():
    b = Builder()
    for i, (x, z, lean) in enumerate(((0, 0, 0), (0.08, 0.05, 18), (-0.07, 0.04, -16), (0.02, -0.07, 10))):
        t = math.radians(lean)
        h = 0.32 - i * 0.03
        b.tube((x, 0, z), (x + math.sin(t) * h, h, z + 0.03), 0.045, 0.0, "Moss:noline", segs=4)
    return b


ASSETS = {
    "": {
        "Clearing": clearing, "PineTree": pine_tree, "OakTree": oak_tree, "BirchTree": birch_tree,
        "Rock": rock, "Mushrooms": mushrooms, "Stump": stump, "Bush": bush, "GrassTuft": grass_tuft,
    },
}

result = {"exported": build_all(ASSETS, EXPORT_DIR, "ArenaProps", BLEND_OUT, spacing=3.0), "blend": BLEND_OUT}
