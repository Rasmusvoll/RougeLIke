"""Shared helpers for the ArtSource build scripts: the palette, toon-ready materials, a primitive
mesh builder and FBX export. The build scripts exec() this file inside Blender.

Shapes are authored in Unity space (X right, Y up, Z forward) and converted to Blender space so
the FBX imports into Unity with no extra rotation. Each mesh stores its smoothed normals (in Unity
space) in a colour attribute so the toon outline hull doesn't crack on faceted shapes.

Material keys are palette names, optionally tagged: "Cream:noline" turns the outline off for that
material, "Parchment:paper" adds the painted paper grain. See ArtSource/STYLE.md.
"""
import math
import os

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

PROJECT = r"D:\RougeLIke"

# Unity (x, y, z) -> Blender (-x, -z, y). Verified against Unity's FBX importer.
TO_BLENDER = Matrix(((-1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))

# Mirrors the table in STYLE.md (sRGB hex).
PALETTE = {
    "Ink": "2B1F1A",
    "Parchment": "EAD9B0",
    "ParchmentDark": "CDB582",
    "Cream": "F2E4C4",
    "Forest": "4F6B3A",
    "Pine": "2F4A33",
    "Moss": "8A9A4B",
    "Ochre": "D19A3A",
    "Rust": "C2562B",
    "Brick": "9E3B2A",
    "Bark": "6B4A2F",
    "Wood": "A87A4C",
    "Stone": "8C8577",
    "StoneLight": "B5AD9A",
    "Plum": "6E4A6E",
    "Acid": "A8C23A",
}

TAG_SUFFIX = {"noline": "_NoLine", "paper": "_Paper"}


def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def hex_rgb(h):
    return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


def material(key):
    """Material for a palette key such as "Bark" or "Cream:noline"."""
    name, *tags = key.split(":")
    mat_name = "M_" + name + "".join(TAG_SUFFIX[t] for t in tags)
    mat = bpy.data.materials.get(mat_name) or bpy.data.materials.new(mat_name)
    mat.use_nodes = True
    rgb = tuple(srgb_to_linear(c) for c in hex_rgb(PALETTE[name]))
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.9
    mat.diffuse_color = (*rgb, 1.0)
    return mat


def euler(rot):
    return Euler([math.radians(r) for r in rot], "XYZ").to_matrix().to_4x4()


class Builder:
    """Collects primitives in Unity space, each with a material."""

    def __init__(self):
        self.bm = bmesh.new()
        self.mats = []
        self._done = set()

    def _tag(self, mat):
        """Give every face added since the last call this material."""
        if mat not in self.mats:
            self.mats.append(mat)
        idx = self.mats.index(mat)
        for f in self.bm.faces:
            if f not in self._done:
                f.material_index = idx
        self._done = set(self.bm.faces)

    def blob(self, center, size, mat, subdiv=2, rot=(0, 0, 0)):
        """Faceted ellipsoid; size is full width/height/depth."""
        m = Matrix.Translation(center) @ euler(rot) @ Matrix.Diagonal((*[s / 2 for s in size], 1))
        bmesh.ops.create_icosphere(self.bm, subdivisions=subdiv, radius=1.0, matrix=m)
        self._tag(mat)

    def box(self, center, size, mat, rot=(0, 0, 0)):
        m = Matrix.Translation(center) @ euler(rot) @ Matrix.Diagonal((*size, 1))
        bmesh.ops.create_cube(self.bm, size=1.0, matrix=m)
        self._tag(mat)

    def tube(self, a, b, r1, r2, mat, segs=7):
        """Tapered prism from point a (radius r1) to point b (radius r2). r2 = 0 makes a spike."""
        a, b = Vector(a), Vector(b)
        d = b - a
        rot = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_matrix().to_4x4()
        m = Matrix.Translation((a + b) / 2) @ rot
        bmesh.ops.create_cone(self.bm, cap_ends=True, segments=segs,
                              radius1=r1, radius2=r2, depth=d.length, matrix=m)
        self._tag(mat)

    def dome(self, center, size, mat, segs=12, rings=8):
        """Upper half of an ellipsoid, flat side down."""
        m = Matrix.Translation(center) @ Matrix.Diagonal((size[0] / 2, size[1], size[2] / 2, 1))
        res = bmesh.ops.create_uvsphere(self.bm, u_segments=segs, v_segments=rings, radius=1.0, matrix=m)
        below = [v for v in res["verts"] if v.co.y < center[1] - 1e-4]
        bmesh.ops.delete(self.bm, geom=below, context="VERTS")
        edges = [e for e in self.bm.edges if e.is_boundary]
        if edges:
            bmesh.ops.holes_fill(self.bm, edges=edges, sides=0)
        self._tag(mat)

    def slab(self, outline, y_top, y_bottom, top_mat, side_mat):
        """Flat-topped prism from a closed outline of (x, z) points (counter-clockwise from above)."""
        bm = self.bm
        top = [bm.verts.new((x, y_top, z)) for x, z in outline]
        bot = [bm.verts.new((x, y_bottom, z)) for x, z in outline]
        centre = bm.verts.new((0, y_top, 0))
        n = len(outline)
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((centre, top[j], top[i]))
        self._tag(top_mat)
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((top[i], top[j], bot[j], bot[i]))
        bm.faces.new(bot)
        self._tag(side_mat)

    def build(self, name, collection):
        bm = self.bm
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.normal_update()
        # Smoothed normals for the outline: each vertex averages its faces' normals (vertices are
        # shared within a primitive), so every primitive gets one closed hull.
        col = bm.loops.layers.float_color.new("OutlineNormal")
        for v in bm.verts:
            n = Vector((0, 0, 0))
            for f in v.link_faces:
                n += f.normal * f.calc_area()
            n = n.normalized() if n.length > 1e-8 else Vector((0, 1, 0))
            for loop in v.link_loops:
                loop[col] = (n.x * 0.5 + 0.5, n.y * 0.5 + 0.5, n.z * 0.5 + 0.5, 1.0)

        bmesh.ops.transform(bm, matrix=TO_BLENDER, verts=bm.verts)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        mesh = bpy.data.meshes.get(name) or bpy.data.meshes.new(name)
        # Set the material slots before writing faces: clearing slots afterwards resets every
        # face's material index.
        mesh.materials.clear()
        for key in self.mats:
            mesh.materials.append(material(key))
        bm.to_mesh(mesh)
        bm.free()
        for p in mesh.polygons:
            p.use_smooth = False
        obj = bpy.data.objects.get(name) or bpy.data.objects.new(name, mesh)
        obj.data = mesh
        if obj.name not in collection.objects:
            collection.objects.link(obj)
        return obj


def collection(name):
    coll = bpy.data.collections.get(name)
    if coll is None:
        coll = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(coll)
    return coll


def export_fbx(obj, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"MESH"},
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
        bake_space_transform=True, mesh_smooth_type="FACE", add_leaf_bones=False,
        colors_type="LINEAR",
    )


def build_all(assets, export_root, coll_name, blend_out, spacing=1.6):
    """assets: {subfolder: {name: maker}}. Exports each to export_root/subfolder/name.fbx."""
    coll = collection(coll_name)
    exported = []
    for row, (folder, makers) in enumerate(assets.items()):
        for col_i, (name, make) in enumerate(makers.items()):
            obj = make().build(name, coll)
            obj.location = (0, 0, 0)
            path = os.path.join(export_root, folder, name + ".fbx") if folder else os.path.join(export_root, name + ".fbx")
            export_fbx(obj, path)
            exported.append(path)
            # Lay the objects out in a grid in the open Blender scene for viewing.
            obj.location = (col_i * -spacing, row * spacing * 1.25, 0)
    bpy.data.libraries.write(blend_out, {coll}, fake_user=True)
    return exported
