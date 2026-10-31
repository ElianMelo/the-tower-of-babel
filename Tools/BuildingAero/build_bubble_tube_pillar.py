"""Game-ready Bubble Tube pillar (Round03 design 04) and its construction stages.

Run headless so the user's open Blender scene is untouched:
  blender --background --factory-startup --python Tools/BuildingAero/build_bubble_tube_pillar.py

One solid single-submesh lathe. The glass tube is an opaque cylinder; the water, spine and
bubbles are drawn by TowerOfBabel/Buildings/Bubble Tube Pillar from object-space position.
UV1 (PaletteUV) carries the surface region as x = (id + .5) / 8:
  0 pearl, 1 blue enamel band, 2 chrome collar, 3 aqua glow (blueprint guides), 4 water tube.
Stages: 0 blueprint, 1-9 add eight seeded Voronoi chunks each, 10 the completed model.
Chunk cut faces inside the tube use the water region so a cross-section shows water.
"""
from pathlib import Path
import bpy, bmesh, math, json, random
from mathutils import Vector

ROOT = Path('D:/PersonalProjects/the-tower-of-babel')
SOURCE = ROOT / 'ArtSource/Buildings/FrutigerAero/BubbleTube'
EXPORT = ROOT / 'Assets/_Project/Models/Buildings/FrutigerAero/Construction'
COMPLETE = ROOT / 'Assets/_Project/Models/Buildings/FrutigerAero/BubbleTubePillar.fbx'
TAU = math.tau
R = .41002339          # identical outer bound to the previous pillar (support layout depends on it)
TUBE_R = .362
TUBE_Z = (.485, 3.515)
SEGMENTS = 24
SEED = 712809
PEARL, BLUE, CHROME, GLOW, WATER = range(5)
LABELS = ['Blueprint'] + [f'Random fill {i*10}%' for i in range(1, 10)] + ['Complete']

bpy.ops.wm.read_factory_settings(use_empty=True)
SCENE = bpy.context.scene
COLL = SCENE.collection


def region_uv(region):
    return ((region + .5) / 8, .5)


def fillet(pts, rad=.02, steps=2):
    out = [pts[0]]
    for i in range(1, len(pts) - 1):
        a, p, b = Vector(pts[i - 1]), Vector(pts[i]), Vector(pts[i + 1])
        da, db = a - p, b - p
        if da.length < 1e-6 or db.length < 1e-6 or da.normalized().dot(db.normalized()) < -.995:
            out.append(pts[i]); continue
        d = min(rad, da.length * .5, db.length * .5)
        p1, p2 = p + da.normalized() * d, p + db.normalized() * d
        for k in range(steps + 1):
            t = k / steps
            q = (1 - t) ** 2 * p1 + 2 * (1 - t) * t * p + t * t * p2
            out.append((q.x, q.y))
    out.append(pts[-1])
    clean = [out[0]]
    for p in out[1:]:
        if abs(p[0] - clean[-1][0]) > 1e-5 or abs(p[1] - clean[-1][1]) > 1e-5: clean.append(p)
    return clean


# Hand-placed profile keeps the completed pillar near the previous 764-triangle budget.
HALF = [(0, .395), (.02, R), (.18, R), (.23, .375), (.33, .37), (.40, .372), (.43, .381),
        (.47, .379), (TUBE_Z[0], TUBE_R)]
PROFILE = HALF + [(4 - z, r) for z, r in reversed(HALF)]


def region_at(z):
    e = min(z, 4 - z)
    if e >= TUBE_Z[0] - 1e-4: return WATER
    if e >= .40: return CHROME
    if .23 <= e < .33: return BLUE
    return PEARL


def radius(z):
    for (a, r), (b, s) in zip(PROFILE, PROFILE[1:]):
        if a <= z <= b and b > a: return r + (s - r) * (z - a) / (b - a)
    return PROFILE[-1][1]


def set_loop_uvs(bm, face, region):
    bake = bm.loops.layers.uv['BakeUV']; pal = bm.loops.layers.uv['PaletteUV']
    us = [math.atan2(l.vert.co.y, l.vert.co.x) / TAU + .5 for l in face.loops]
    if max(us) - min(us) > .5: us = [u + 1 if u < .5 else u for u in us]
    for l, u in zip(face.loops, us):
        l[bake].uv = (u, l.vert.co.z / 4)
        l[pal].uv = region_uv(region)


def complete_mesh():
    bm = bmesh.new()
    bm.loops.layers.uv.new('BakeUV'); bm.loops.layers.uv.new('PaletteUV')
    rings = [[bm.verts.new((r * math.cos(TAU * i / SEGMENTS), r * math.sin(TAU * i / SEGMENTS), z))
              for i in range(SEGMENTS)] for z, r in PROFILE]
    for a in range(len(rings) - 1):
        zmid = .5 * (PROFILE[a][0] + PROFILE[a + 1][0])
        for i in range(SEGMENTS):
            j = (i + 1) % SEGMENTS
            f = bm.faces.new((rings[a][i], rings[a][j], rings[a + 1][j], rings[a + 1][i]))
            set_loop_uvs(bm, f, region_at(zmid))
    for ring, rev in ((rings[0], True), (rings[-1], False)):
        f = bm.faces.new(list(reversed(ring)) if rev else ring)
        set_loop_uvs(bm, f, PEARL)
        bake = bm.loops.layers.uv['BakeUV']
        for l in f.loops: l[bake].uv = (.5 + l.vert.co.x, .5 + l.vert.co.y)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new('Pillar_BubbleTube'); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = True
    me.set_sharp_from_angle(angle=math.radians(40))
    return me


MATERIAL = bpy.data.materials.new('Aero_BubbleTube_Pillar')


def new_obj(name, me):
    me.materials.clear(); me.materials.append(MATERIAL)
    o = bpy.data.objects.new(name, me); COLL.objects.link(o)
    return o


def solid_piece(source, index, seeds):
    obj = new_obj(f'Pillar_Piece_{index:02}', source.data.copy())
    bm = bmesh.new(); bm.from_mesh(obj.data)
    bake = bm.loops.layers.uv['BakeUV']; pal = bm.loops.layers.uv['PaletteUV']
    seed = seeds[index]
    neighbors = sorted((p for i, p in enumerate(seeds) if i != index), key=lambda p: (p - seed).length_squared)
    for p in neighbors:
        center, normal = (seed + p) * .5, (p - seed).normalized()
        bmesh.ops.bisect_plane(bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces), dist=1e-6,
                               plane_co=center, plane_no=normal, clear_outer=True, clear_inner=False)
        cut = [e for e in bm.edges if e.is_boundary and all(abs((v.co - center).dot(normal)) < 1e-5 for v in e.verts)]
        for face in bmesh.ops.holes_fill(bm, edges=cut, sides=0)['faces']:
            face.smooth = False
            zc = face.calc_center_median().z
            region = WATER if TUBE_Z[0] < zc < TUBE_Z[1] else PEARL
            for l in face.loops:
                q = l.vert.co
                l[bake].uv = (.5 + q.x * .4, .5 + (q.y if abs(normal.z) > .5 else q.z * .2) * .4)
                l[pal].uv = region_uv(region)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data); bm.free()
    return obj


def blueprint(stage):
    verts, faces = [], []
    def tube(points, thickness=.009):
        start = len(verts)
        for i, p in enumerate(points):
            p = Vector(p)
            tangent = (Vector(points[min(i + 1, len(points) - 1)]) - Vector(points[max(0, i - 1)])).normalized()
            axis = Vector((0, 0, 1)) if abs(tangent.z) < .9 else Vector((0, 1, 0))
            a = tangent.cross(axis).normalized(); b = tangent.cross(a).normalized()
            for j in range(3):
                verts.append(tuple(p + thickness * (a * math.cos(j * TAU / 3) + b * math.sin(j * TAU / 3))))
        for i in range(len(points) - 1):
            for j in range(3):
                a = start + i * 3 + j; b = start + i * 3 + (j + 1) % 3
                faces.append((a, b, b + 3, a + 3))
        faces.append(tuple(start + j for j in (2, 1, 0)))
        faces.append(tuple(start + (len(points) - 1) * 3 + j for j in range(3)))
    ys = [.012] + [z for z, r in PROFILE if .013 < z < 3.987] + [3.988]
    for rail in range(4):
        ang = TAU * rail / 4 + math.pi / 4
        tube([((radius(y) - .012) * math.cos(ang), (radius(y) - .012) * math.sin(ang), y) for y in ys])
    for y in (.012, .18, .40, TUBE_Z[0], 2., TUBE_Z[1], 3.60, 3.82, 3.988):
        r = radius(y) - .012
        tube([(r * math.cos(i * TAU / 12), r * math.sin(i * TAU / 12), y) for i in range(13)])
    me = bpy.data.meshes.new(f'Pillar_Blueprint_{stage:02}')
    me.from_pydata(verts, [], faces)
    bake = me.uv_layers.new(name='BakeUV'); pal = me.uv_layers.new(name='PaletteUV')
    for poly in me.polygons:
        poly.use_smooth = True
        for li in poly.loop_indices:
            bake.data[li].uv = (.5, .5); pal.data[li].uv = region_uv(GLOW)
    me.update()
    return new_obj(f'Pillar_Guides_{stage:02}', me)


def triangulate(obj):
    bm = bmesh.new(); bm.from_mesh(obj.data)
    bmesh.ops.triangulate(bm, faces=bm.faces, quad_method='FIXED', ngon_method='BEAUTY')
    bm.to_mesh(obj.data); bm.free()


def join(parts, name):
    for o in COLL.objects: o.select_set(False)
    for p in parts: p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    if len(parts) > 1: bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = name; obj.data.name = name
    return obj


def export(objs, path):
    for o in COLL.objects: o.select_set(False)
    for o in objs: o.select_set(True); o.location = (0, 0, 0)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={'MESH'},
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                             apply_scale_options='FBX_SCALE_ALL', add_leaf_bones=False, bake_anim=False,
                             use_mesh_modifiers=True, mesh_smooth_type='FACE', use_triangles=True,
                             path_mode='STRIP', embed_textures=False)


def topology(obj):
    bm = bmesh.new(); bm.from_mesh(obj.data)
    report = {'triangles': len(bm.faces), 'boundaryEdges': sum(e.is_boundary for e in bm.edges),
              'nonManifoldEdges': sum(not e.is_manifold for e in bm.edges)}
    bm.free(); return report


SOURCE.mkdir(parents=True, exist_ok=True); EXPORT.mkdir(parents=True, exist_ok=True)
source = new_obj('Pillar_BubbleTube', complete_mesh())
rng = random.Random(SEED)
seeds = []
while len(seeds) < 80:
    a = rng.uniform(0, TAU); rr = .27 * math.sqrt(rng.random())
    p = Vector((rr * math.cos(a), rr * math.sin(a), rng.uniform(.015, 3.985)))
    if all((p - o).length > .105 for o in seeds): seeds.append(p)
pieces = [solid_piece(source, i, seeds) for i in range(len(seeds))]
order = list(range(len(pieces))); rng.shuffle(order)

report = {'seed': SEED, 'complete': None, 'stages': [], 'chunks': []}
stages = []
for stage in range(10):
    parts = []
    for pid in order[:len(pieces) * stage // 10]:
        parts.append(new_obj(f'Pillar_Solid_{stage:02}_{pid}', pieces[pid].data.copy()))
    parts.append(blueprint(stage))
    obj = join(parts, f'Pillar_Stage_{stage:02}')
    triangulate(obj); stages.append(obj)
    report['stages'].append({'stage': stage, 'phase': LABELS[stage], 'filledPieceIds': order[:len(pieces) * stage // 10],
                             **topology(obj)})
for index, piece in enumerate(pieces):
    piece.name = f'Pillar_Chunk_{order.index(index) // 8 + 1:02}_{index:02}'
    triangulate(piece)
    report['chunks'].append({'name': piece.name, **topology(piece)})
triangulate(source)
report['complete'] = {'name': source.name, **topology(source),
                      'bounds': [[round(min(v.co[k] for v in source.data.vertices), 5) for k in range(3)],
                                 [round(max(v.co[k] for v in source.data.vertices), 5) for k in range(3)]]}

export([source], COMPLETE)
export(stages, EXPORT / 'BubbleTubeStages.fbx')
export(pieces, EXPORT / 'BubbleTubeChunks.fbx')

# Review lineup in the .blend only (exports above are at the origin).
for i, o in enumerate(stages + [source]): o.location = (i * 1.35, 0, 0)
for i, p in enumerate(pieces): p.location = (i * .6, 3, 0); p.hide_render = True
(SOURCE / 'BubbleTubePillarReport.json').write_text(json.dumps(report, indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / 'BubbleTubePillar.blend'), compress=True)
print('BUBBLE_TUBE', json.dumps({'complete': report['complete'],
                                 'stageTris': [s['triangles'] for s in report['stages']],
                                 'openStages': [s['stage'] for s in report['stages'] if s['nonManifoldEdges']],
                                 'openChunks': sum(1 for c in report['chunks'] if c['nonManifoldEdges'])}))
