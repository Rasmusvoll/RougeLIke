"""Builds the low-poly placeholder bodies and parts and exports them as FBX into the Unity project.

Run inside Blender 5.x (e.g. `python ArtSource/blender_send.py ArtSource/build_unit_placeholders.py`
with the Blender Lab MCP add-on running, or paste into Blender's Text Editor and run).

Shapes are authored in Unity space (X right, Y up, Z forward) and converted to Blender space
so that the FBX imports into Unity with no extra rotation. Each object's origin is its attach
point: bodies sit on the ground at their origin, parts attach at the body's slot position.
Arm and leg parts are modelled for the right side (+X); left slots mirror them in Unity.
"""
import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

PROJECT = r"D:\RougeLIke"
EXPORT_DIR = os.path.join(PROJECT, "Assets", "Art", "Models", "Units")
BLEND_OUT = os.path.join(PROJECT, "ArtSource", "unit_placeholders.blend")
COLLECTION = "UnitPlaceholders"

# Unity (x, y, z) -> Blender (-x, -z, y). Verified against Unity's FBX importer.
TO_BLENDER = Matrix(((-1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))

COLORS = {
    "Bone": (0.93, 0.88, 0.74),
    "BruteHide": (0.55, 0.24, 0.18),
    "BruteBelly": (0.80, 0.55, 0.40),
    "CrawlerHide": (0.30, 0.55, 0.25),
    "CrawlerBelly": (0.70, 0.78, 0.45),
    "Crab": (0.90, 0.40, 0.15),
    "Acid": (0.55, 0.95, 0.10),
    "Skin": (0.45, 0.45, 0.50),
    "Leg": (0.20, 0.45, 0.85),
    "Shell": (0.10, 0.40, 0.42),
    "ShellRim": (0.25, 0.65, 0.62),
    "Tail": (0.45, 0.20, 0.55),
    "Eye": (0.05, 0.05, 0.05),
}


def material(name):
    mat = bpy.data.materials.get("M_" + name)
    if mat is None:
        mat = bpy.data.materials.new("M_" + name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    rgb = COLORS[name]
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.8
    mat.diffuse_color = (*rgb, 1.0)
    return mat


class Builder:
    """Collects primitives in Unity space, each with a material slot."""

    def __init__(self):
        self.bm = bmesh.new()
        self.mats = []
        self._done = set()

    def _slot(self, mat):
        if mat not in self.mats:
            self.mats.append(mat)
        return self.mats.index(mat)

    def _tag(self, mat):
        """Give every face added since the last call this material."""
        idx = self._slot(mat)
        for f in self.bm.faces:
            if f not in self._done:
                f.material_index = idx
        self._done = set(self.bm.faces)

    def blob(self, center, size, mat, subdiv=1, rot=(0, 0, 0)):
        """Faceted ellipsoid; size is full width/height/depth."""
        m = Matrix.Translation(center) @ euler(rot) @ Matrix.Diagonal((*[s / 2 for s in size], 1))
        res = bmesh.ops.create_icosphere(self.bm, subdivisions=subdiv, radius=1.0, matrix=m)
        self._tag(mat)

    def box(self, center, size, mat, rot=(0, 0, 0)):
        m = Matrix.Translation(center) @ euler(rot) @ Matrix.Diagonal((*size, 1))
        res = bmesh.ops.create_cube(self.bm, size=1.0, matrix=m)
        self._tag(mat)

    def tube(self, a, b, r1, r2, mat, segs=6):
        """Tapered prism from point a (radius r1) to point b (radius r2). r2 = 0 makes a spike."""
        a, b = Vector(a), Vector(b)
        d = b - a
        rot = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_matrix().to_4x4()
        m = Matrix.Translation((a + b) / 2) @ rot
        res = bmesh.ops.create_cone(self.bm, cap_ends=True, segments=segs,
                                    radius1=r1, radius2=r2, depth=d.length, matrix=m)
        self._tag(mat)

    def dome(self, center, size, mat):
        """Upper half of a faceted ellipsoid, flat side down."""
        m = Matrix.Translation(center) @ Matrix.Diagonal((size[0] / 2, size[1], size[2] / 2, 1))
        res = bmesh.ops.create_uvsphere(self.bm, u_segments=8, v_segments=6, radius=1.0, matrix=m)
        below = [v for v in res["verts"] if v.co.y < center[1] - 1e-4]
        bmesh.ops.delete(self.bm, geom=below, context="VERTS")
        edges = [e for e in self.bm.edges if e.is_boundary]
        if edges:
            bmesh.ops.holes_fill(self.bm, edges=edges, sides=0)
        self._tag(mat)

    def build(self, name, collection):
        bmesh.ops.transform(self.bm, matrix=TO_BLENDER, verts=self.bm.verts)
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        mesh = bpy.data.meshes.get(name) or bpy.data.meshes.new(name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        mesh.materials.clear()
        for mat_name in self.mats:
            mesh.materials.append(material(mat_name))
        for p in mesh.polygons:
            p.use_smooth = False
        obj = bpy.data.objects.get(name)
        if obj is None:
            obj = bpy.data.objects.new(name, mesh)
        obj.data = mesh
        if obj.name not in collection.objects:
            collection.objects.link(obj)
        return obj


def euler(rot):
    from mathutils import Euler
    return Euler([math.radians(r) for r in rot], "XYZ").to_matrix().to_4x4()


# ---------------------------------------------------------------------------
# Bodies. Origin = ground under the body. Slot positions live in the BodyDefinition assets.

def body_brute():
    b = Builder()
    b.blob((0, 0.85, 0), (1.15, 0.95, 1.35), "BruteHide")
    b.blob((0, 0.62, 0.05), (0.9, 0.55, 1.0), "BruteBelly")
    b.blob((0, 1.1, 0.45), (0.6, 0.45, 0.5), "BruteHide")  # shoulder hump
    for x in (-0.35, 0.35):
        for z in (-0.35, 0.3):
            b.box((x, 0.22, z), (0.26, 0.48, 0.28), "BruteHide")
            b.box((x, 0.04, z + 0.05), (0.3, 0.08, 0.36), "BruteBelly")
    return b


def body_crawler():
    b = Builder()
    b.blob((0, 0.35, 0), (0.75, 0.38, 1.2), "CrawlerHide")
    b.blob((0, 0.24, 0), (0.6, 0.22, 1.05), "CrawlerBelly")
    for i, z in enumerate((-0.35, 0.0, 0.3)):
        b.blob((0, 0.5, z), (0.45 - 0.05 * i, 0.16, 0.3), "CrawlerHide")
    return b


# ---------------------------------------------------------------------------
# Parts. Origin = attach point. Right-side parts extend toward +X.

def part_claw():
    b = Builder()
    b.blob((0, 0, 0), (0.22, 0.22, 0.22), "BruteBelly")
    b.tube((0, 0, 0), (0.3, -0.1, 0.15), 0.09, 0.07, "Skin")
    b.tube((0.3, -0.1, 0.15), (0.35, -0.2, 0.4), 0.07, 0.09, "Skin")
    for dx in (-0.06, 0.0, 0.06):
        b.tube((0.35 + dx, -0.2, 0.42), (0.35 + dx * 1.5, -0.38, 0.62), 0.035, 0.0, "Bone", segs=4)
    return b


def part_pincer():
    b = Builder()
    b.tube((0, 0, 0), (0.28, -0.05, 0.2), 0.08, 0.07, "Crab")
    b.blob((0.32, -0.05, 0.3), (0.2, 0.2, 0.26), "Crab")
    b.tube((0.32, -0.02, 0.4), (0.3, 0.06, 0.68), 0.07, 0.0, "Crab", segs=5)  # upper jaw
    b.tube((0.34, -0.1, 0.4), (0.38, -0.16, 0.62), 0.06, 0.0, "Bone", segs=5)  # lower jaw
    return b


def part_quick_legs():
    b = Builder()
    knee = (0.28, 0.12, 0.05)
    foot = (0.42, -0.32, 0.12)
    b.blob((0, 0, 0), (0.16, 0.16, 0.16), "Leg")
    b.tube((0, 0, 0), knee, 0.05, 0.045, "Leg", segs=5)
    b.tube(knee, foot, 0.045, 0.025, "Leg", segs=5)
    b.tube(foot, (0.46, -0.3, 0.24), 0.03, 0.0, "Bone", segs=4)  # toe spur
    return b


def part_horn():
    b = Builder()
    b.blob((0, 0.05, 0.12), (0.4, 0.34, 0.38), "Skin")
    for x in (-0.1, 0.1):
        b.blob((x, 0.12, 0.29), (0.07, 0.07, 0.05), "Eye")
    b.tube((0, 0.15, 0.2), (0, 0.55, 0.5), 0.1, 0.0, "Bone", segs=6)
    return b


def part_spitter():
    b = Builder()
    b.blob((0, 0.05, 0.12), (0.38, 0.3, 0.36), "Skin")
    b.blob((0, 0.22, 0.02), (0.3, 0.22, 0.3), "Acid")  # acid sac
    for x in (-0.1, 0.1):
        b.blob((x, 0.14, 0.27), (0.07, 0.07, 0.05), "Eye")
    b.tube((0, 0.02, 0.25), (0, 0.04, 0.52), 0.07, 0.1, "Skin", segs=6)
    b.blob((0, 0.04, 0.53), (0.14, 0.14, 0.04), "Acid")
    return b


def part_shell():
    b = Builder()
    b.dome((0, 0, 0), (0.95, 0.42, 1.1), "Shell")
    b.box((0, 0.02, 0), (1.0, 0.06, 1.12), "ShellRim")
    for z in (-0.25, 0.05, 0.3):
        b.tube((0, 0.3, z), (0, 0.5, z - 0.05), 0.07, 0.0, "ShellRim", segs=4)
    return b


def part_spiked_tail():
    b = Builder()
    pts = [(0, 0, 0), (0, 0.05, -0.3), (0, 0.15, -0.6), (0, 0.32, -0.85)]
    radii = [0.16, 0.12, 0.08, 0.05]
    for i in range(len(pts) - 1):
        b.tube(pts[i], pts[i + 1], radii[i], radii[i + 1], "Tail", segs=6)
        p = Vector(pts[i + 1])
        b.tube(p + Vector((0, radii[i + 1] * 0.6, 0)), p + Vector((0, radii[i + 1] + 0.14, 0.04)),
               0.04, 0.0, "Bone", segs=4)
    b.blob(pts[-1], (0.18, 0.18, 0.18), "Tail")
    for d in ((0.12, 0.06, 0), (-0.12, 0.06, 0), (0, 0.16, 0), (0, 0.02, -0.14)):
        b.tube(pts[-1], Vector(pts[-1]) + Vector(d) * 1.6, 0.04, 0.0, "Bone", segs=4)
    return b


ASSETS = {
    "Bodies": {"Brute": body_brute, "Crawler": body_crawler},
    "Parts": {
        "Claw": part_claw, "Pincer": part_pincer, "QuickLegs": part_quick_legs,
        "Horn": part_horn, "Spitter": part_spitter, "Shell": part_shell, "SpikedTail": part_spiked_tail,
    },
}


def main():
    coll = bpy.data.collections.get(COLLECTION)
    if coll is None:
        coll = bpy.data.collections.new(COLLECTION)
        bpy.context.scene.collection.children.link(coll)

    exported = []
    for row, (folder, makers) in enumerate(ASSETS.items()):
        out_dir = os.path.join(EXPORT_DIR, folder)
        os.makedirs(out_dir, exist_ok=True)
        for col, (name, make) in enumerate(makers.items()):
            obj = make().build(name, coll)
            obj.location = (0, 0, 0)
            bpy.ops.object.select_all(action="DESELECT")
            obj.select_set(True)
            bpy.context.view_layer.objects.active = obj
            path = os.path.join(out_dir, name + ".fbx")
            bpy.ops.export_scene.fbx(
                filepath=path, use_selection=True, object_types={"MESH"},
                apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                bake_space_transform=True, mesh_smooth_type="FACE", add_leaf_bones=False,
            )
            exported.append(path)
            # Lay the objects out in a grid in the open Blender scene for viewing.
            obj.location = (col * -1.6, row * 2.0, 0)

    bpy.data.libraries.write(BLEND_OUT, {coll}, fake_user=True)
    return exported


result = {"exported": main(), "blend": BLEND_OUT}
