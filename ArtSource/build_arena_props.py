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


# Battle-arena features. These carry colliders in the game (ArenaDefinition pieces), so keep each
# prop's footprint close to its description.

def crate():
    """A 0.8 m wooden crate with darker plank bands and a cross brace."""
    b = Builder()
    b.box((0, 0.4, 0), (0.8, 0.8, 0.8), "Wood")
    for y in (0.06, 0.74):
        b.box((0, y, 0), (0.84, 0.1, 0.84), "Bark")
    for side in (-1, 1):
        b.box((0, 0.4, side * 0.41), (0.1, 0.75, 0.04), "Bark:noline", rot=(0, 0, 45))
    return b


def log_pile():
    """Three logs stacked into a low wall, 3 m long along X, about 0.9 m high."""
    b = Builder()
    for x, y, z in ((-0.05, 0.25, -0.27), (0.08, 0.25, 0.27), (0.0, 0.68, 0.0)):
        b.tube((x - 1.5, y, z), (x + 1.5, y, z), 0.27, 0.25, "Bark", segs=8)
        for end, r in ((-1.5, 0.27), (1.5, 0.25)):
            b.tube((x + end * 1.005, y, z), (x + end * 1.02, y, z), r * 0.85, r * 0.8, "Wood:noline", segs=8)
    for x in (-1.25, 1.25):
        b.box((x, 0.45, 0), (0.12, 0.9, 0.9), "Wood")  # stakes holding the stack
    return b


def plank_wall():
    """A broken sawmill wall: two posts and uneven planks, 2.4 m wide along X, 1.1 m high."""
    b = Builder()
    for x in (-1.15, 1.15):
        b.box((x, 0.6, 0), (0.18, 1.2, 0.18), "Bark")
    for i, (y, w, off, tilt) in enumerate(((0.2, 2.4, 0, 0), (0.45, 2.3, -0.05, 2), (0.7, 1.6, -0.4, -4), (0.95, 1.0, 0.6, 6))):
        b.box((off, y, 0.11), (w, 0.22, 0.06), "Wood" if i % 2 else "Ochre", rot=(0, 0, tilt))
    return b


def log_bridge():
    """Two logs along Z with planks across them: a footbridge over the brook, deck top at about 0.08."""
    b = Builder()
    for x in (-0.55, 0.55):
        b.tube((x, -0.05, -2.2), (x, -0.05, 2.2), 0.13, 0.12, "Bark", segs=7)
    z = -2.1
    i = 0
    while z <= 2.1:
        b.box((0.03 * ((i % 3) - 1), 0.05, z), (1.55 + 0.1 * (i % 2), 0.07, 0.24), "Wood", rot=(0, 2 * ((i % 3) - 1), 0))
        z += 0.3
        i += 1
    return b


def reeds():
    """A clump of reeds with cattail heads, for brook banks."""
    b = Builder()
    for i, (x, z, h, lean) in enumerate(((0, 0, 0.8, 0), (0.12, 0.06, 0.65, 12), (-0.1, 0.05, 0.7, -10),
                                         (0.05, -0.1, 0.55, 8), (-0.06, -0.08, 0.6, -6))):
        t = math.radians(lean)
        top = (x + math.sin(t) * h, h, z)
        b.tube((x, 0, z), top, 0.03, 0.01, "Moss:noline", segs=4)
        if i < 3:
            b.tube((top[0] - math.sin(t) * 0.16, h - 0.16, z), (top[0] - math.sin(t) * 0.02, h - 0.02, z), 0.045, 0.04, "Bark", segs=6)
    return b


def lily_pads():
    b = Builder()
    for x, z, r in ((0, 0, 0.22), (0.35, 0.15, 0.15), (-0.2, 0.3, 0.13)):
        b.tube((x, 0, z), (x, 0.02, z), r, r, "Moss:noline", segs=9)
    b.blob((0.05, 0.05, 0.02), (0.1, 0.07, 0.1), "Cream:noline", subdiv=1)
    return b


def boulder():
    """A big craggy boulder, about 1.6 m across and 1.1 m high: cover on the hillside."""
    b = Builder()
    b.blob((0, 0.5, 0), (1.6, 1.15, 1.4), "Stone", subdiv=1, rot=(4, 30, -6))
    b.blob((-0.4, 0.95, 0.1), (0.8, 0.5, 0.8), "StoneLight", subdiv=1, rot=(0, -20, 10))
    b.blob((0.55, 0.2, 0.35), (0.6, 0.45, 0.55), "Stone", subdiv=1, rot=(0, 40, 0))
    b.blob((0.2, 0.08, -0.6), (0.5, 0.25, 0.4), "Moss:noline", subdiv=1)
    return b


ASSETS = {
    "": {
        "Clearing": clearing, "PineTree": pine_tree, "OakTree": oak_tree, "BirchTree": birch_tree,
        "Rock": rock, "Mushrooms": mushrooms, "Stump": stump, "Bush": bush, "GrassTuft": grass_tuft,
        "Crate": crate, "LogPile": log_pile, "PlankWall": plank_wall, "LogBridge": log_bridge,
        "Reeds": reeds, "LilyPads": lily_pads, "Boulder": boulder,
    },
}

result = {"exported": build_all(ASSETS, EXPORT_DIR, "ArenaProps", BLEND_OUT, spacing=3.0), "blend": BLEND_OUT}
