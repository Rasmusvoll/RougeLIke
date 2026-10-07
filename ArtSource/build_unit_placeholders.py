"""Builds the low-poly woodland-critter bodies and parts and exports them as FBX into the Unity project.

Run inside Blender 5.x (e.g. `python ArtSource/blender_send.py ArtSource/build_unit_placeholders.py`
with the Blender Lab MCP add-on running). Style rules and palette: ArtSource/STYLE.md.

Each object's origin is its attach point: bodies sit on the ground at their origin, parts attach at
the body's slot position (slot positions live in the BodyDefinition assets). Arm and leg parts are
modelled for the right side (+X); left slots mirror them in Unity.
"""
import math
import os

exec(open(r"D:\RougeLIke\ArtSource\toon_common.py", encoding="utf-8").read())

EXPORT_DIR = os.path.join(PROJECT, "Assets", "Art", "Models", "Units")
BLEND_OUT = os.path.join(PROJECT, "ArtSource", "unit_placeholders.blend")


def eyes(b, x, y, z, size=0.08):
    """A pair of dot eyes with a cream glint, facing +Z."""
    for sx in (-x, x):
        b.blob((sx, y, z), (size, size * 1.15, size * 0.6), "Ink:noline", subdiv=1)
        b.blob((sx - size * 0.15, y + size * 0.22, z + size * 0.25), (size * 0.35,) * 3, "Cream:noline", subdiv=1)


# ---------------------------------------------------------------------------
# Bodies. Origin = ground under the body.

def body_brute():
    """A stout badger-bear: round brown barrel, cream belly, stubby legs with cream paws."""
    b = Builder()
    b.blob((0, 0.82, -0.02), (1.2, 1.0, 1.35), "Bark")
    b.blob((0, 0.98, 0.42), (0.98, 0.78, 0.72), "Bark")              # shoulders
    b.blob((0, 0.68, 0.22), (0.82, 0.72, 1.0), "Cream")              # belly
    b.blob((0, 1.24, -0.12), (0.62, 0.22, 0.95), "Rust", subdiv=1)  # back stripe
    b.blob((0, 1.0, 0.62), (0.52, 0.48, 0.32), "Bark")               # neck
    for x in (-0.36, 0.36):
        for z in (-0.4, 0.36):
            b.blob((x, 0.24, z), (0.34, 0.48, 0.36), "Bark", subdiv=1)
            b.blob((x, 0.07, z + 0.05), (0.34, 0.14, 0.4), "Cream", subdiv=1)
    return b


def body_crawler():
    """A low newt: long moss-green body, ochre belly, dark spots and a stubby tail."""
    b = Builder()
    b.blob((0, 0.36, 0), (0.74, 0.46, 1.28), "Moss")
    b.blob((0, 0.25, 0.04), (0.62, 0.26, 1.1), "Ochre")
    b.blob((0, 0.38, 0.52), (0.46, 0.34, 0.34), "Moss")              # neck
    b.tube((0, 0.33, -0.55), (0, 0.24, -1.0), 0.16, 0.02, "Moss")    # tail stub
    for x, z in ((0.16, -0.3), (-0.13, -0.05), (0.12, 0.2), (-0.15, 0.34), (0.0, -0.5)):
        # Sit each spot on the body's curved back.
        y = 0.36 + 0.23 * math.sqrt(max(0.0, 1 - (x / 0.37) ** 2 - (z / 0.64) ** 2))
        b.blob((x, y - 0.005, z), (0.13, 0.035, 0.13), "Forest:noline", subdiv=1)
    return b


# ---------------------------------------------------------------------------
# Parts. Origin = attach point. Right-side parts extend toward +X.

def part_claw():
    """A furry forearm with a cream paw and dark claws."""
    b = Builder()
    b.blob((0, 0, 0), (0.28, 0.28, 0.28), "Bark")
    b.tube((0, 0, 0), (0.28, -0.12, 0.12), 0.1, 0.09, "Bark")
    b.tube((0.28, -0.12, 0.12), (0.34, -0.24, 0.36), 0.09, 0.1, "Bark")
    b.blob((0.35, -0.27, 0.42), (0.24, 0.17, 0.22), "Cream")
    for dx in (-0.07, 0.0, 0.07):
        b.tube((0.35 + dx, -0.29, 0.5), (0.35 + dx * 1.5, -0.42, 0.66), 0.035, 0.0, "Ink:noline", segs=5)
    return b


def part_pincer():
    """A crayfish claw in brick red."""
    b = Builder()
    b.tube((0, 0, 0), (0.28, -0.05, 0.2), 0.09, 0.08, "Brick")
    b.blob((0.32, -0.05, 0.3), (0.26, 0.24, 0.32), "Brick")
    b.tube((0.32, -0.01, 0.42), (0.3, 0.07, 0.7), 0.08, 0.0, "Brick")  # upper jaw
    b.tube((0.35, -0.11, 0.42), (0.39, -0.17, 0.64), 0.065, 0.0, "Rust")  # lower jaw
    return b


def part_quick_legs():
    """A springy hare's leg."""
    b = Builder()
    knee = (0.27, 0.1, 0.04)
    foot = (0.42, -0.3, 0.1)
    b.blob((0, 0, 0), (0.22, 0.24, 0.24), "Wood")
    b.tube((0, 0, 0), knee, 0.08, 0.06, "Wood")
    b.tube(knee, foot, 0.055, 0.04, "Wood")
    b.blob((foot[0] + 0.01, foot[1], foot[2] + 0.08), (0.13, 0.08, 0.28), "Cream", subdiv=1)
    return b


def part_horn():
    """A badger head with a cream stripe and a bone horn on its snout."""
    b = Builder()
    b.blob((0, 0.08, 0.16), (0.5, 0.42, 0.44), "Bark")
    b.blob((0, 0.2, 0.2), (0.14, 0.24, 0.42), "Cream", subdiv=1)      # face stripe
    b.blob((0, 0.0, 0.36), (0.26, 0.2, 0.24), "Cream")                 # muzzle
    b.blob((0, 0.05, 0.48), (0.1, 0.07, 0.06), "Ink:noline", subdiv=1)  # nose
    eyes(b, 0.13, 0.15, 0.34)
    for x in (-0.18, 0.18):
        b.blob((x, 0.3, 0.06), (0.15, 0.17, 0.09), "Bark", subdiv=1)
        b.blob((x, 0.3, 0.1), (0.08, 0.1, 0.03), "Rust:noline", subdiv=1)
    b.tube((0, 0.1, 0.42), (0, 0.5, 0.62), 0.09, 0.0, "Cream")
    return b


def part_spitter():
    """A frog head with bulging eyes and a swollen acid throat."""
    b = Builder()
    b.blob((0, 0.06, 0.16), (0.52, 0.32, 0.44), "Moss")
    b.blob((0, -0.06, 0.22), (0.38, 0.22, 0.34), "Acid")
    for x in (-0.15, 0.15):
        b.blob((x, 0.22, 0.22), (0.16, 0.16, 0.16), "Moss", subdiv=1)
    eyes(b, 0.15, 0.24, 0.3, size=0.085)
    b.tube((0, 0.04, 0.3), (0, 0.06, 0.52), 0.07, 0.1, "Moss")
    b.blob((0, 0.06, 0.53), (0.16, 0.16, 0.05), "Acid", subdiv=1)
    return b


def part_shell():
    """A tortoise shell: ochre dome, wooden rim, bark plates and bone studs."""
    b = Builder()
    b.dome((0, 0, 0), (0.95, 0.46, 1.1), "Ochre")
    b.blob((0, 0.03, 0), (1.04, 0.12, 1.18), "Wood")
    b.blob((0, 0.42, 0), (0.34, 0.12, 0.38), "Bark:noline", subdiv=1)
    for x, z in ((0, 0.34), (0, -0.34), (0.3, 0), (-0.3, 0)):
        b.blob((x, 0.3, z), (0.24, 0.1, 0.22), "Bark:noline", subdiv=1)
    for z in (-0.22, 0.06, 0.3):
        b.tube((0, 0.4, z), (0, 0.58, z - 0.05), 0.06, 0.0, "Cream", segs=5)
    return b


def part_spiked_tail():
    """A plum lizard tail with bone spines and a spiked club."""
    b = Builder()
    pts = [(0, 0, 0), (0, 0.05, -0.3), (0, 0.15, -0.6), (0, 0.32, -0.85)]
    radii = [0.17, 0.13, 0.09, 0.06]
    for i in range(len(pts) - 1):
        b.tube(pts[i], pts[i + 1], radii[i], radii[i + 1], "Plum")
        p = Vector(pts[i + 1])
        b.tube(p + Vector((0, radii[i + 1] * 0.6, 0)), p + Vector((0, radii[i + 1] + 0.14, 0.04)),
               0.045, 0.0, "Cream", segs=5)
    b.blob(pts[-1], (0.22, 0.22, 0.22), "Plum")
    for d in ((0.12, 0.06, 0), (-0.12, 0.06, 0), (0, 0.16, 0), (0, 0.02, -0.14)):
        b.tube(pts[-1], Vector(pts[-1]) + Vector(d) * 1.6, 0.045, 0.0, "Cream", segs=5)
    return b


ASSETS = {
    "Bodies": {"Brute": body_brute, "Crawler": body_crawler},
    "Parts": {
        "Claw": part_claw, "Pincer": part_pincer, "QuickLegs": part_quick_legs,
        "Horn": part_horn, "Spitter": part_spitter, "Shell": part_shell, "SpikedTail": part_spiked_tail,
    },
}

result = {"exported": build_all(ASSETS, EXPORT_DIR, "UnitPlaceholders", BLEND_OUT), "blend": BLEND_OUT}
