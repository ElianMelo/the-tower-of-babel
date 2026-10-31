"""Game-ready Aurora Curtain arch (Round05 design 10) and its construction stages.

Run headless so the user's open Blender scene is untouched:
  blender --background --factory-startup --python Tools/BuildingAero/build_aurora_arch.py

Changes from the Round05 proposal (2026-10-31 feedback):
  * the opening is a true semicircle on short straight legs (it was a tall half-ellipse),
  * the rounded roll line along the opening edge is gone (a later pass rounds the rim itself: RIM),
  * the aurora is no longer a baked emission texture: TowerOfBabel/Buildings/Aurora Arch draws an
    animated aurora curtain from object-space position, so the mesh carries no texture maps.

Fit contract (fixes the old overlap and misalignment): local X runs exactly pillar axis to pillar axis
(SPAN = 2.0944 m at the base radius) with the opening centred; only a half-round cap of radius 0.2 m
sits behind x = 0; thickness +/-0.2 m, feet at z 0.14, flat top at z 2.15.
The generator scales each arch along X to its ring's exact chord.

UV0 (BakeUV): planar front projection u = (x + .2) / 2.4, v = z / 2.4.
UV1 (PaletteUV): region x = (id + .5) / 8 with 0 glossy white, 1 aurora face (front and back),
3 aqua blueprint glow. Chunk cut faces are glossy white.
Stages: 0 blueprint, 1-9 add eight seeded Voronoi chunks each, 10 the completed model.
"""
from pathlib import Path
import bpy, bmesh, math, json, random
from mathutils import Vector

ROOT = Path('D:/PersonalProjects/the-tower-of-babel')
SOURCE = ROOT / 'ArtSource/Buildings/FrutigerAero/AuroraArch'
EXPORT = ROOT / 'Assets/_Project/Models/Buildings/FrutigerAero/Construction'
COMPLETE = ROOT / 'Assets/_Project/Models/Buildings/FrutigerAero/AuroraArch.fbx'
TAU = math.tau
SPAN = 2 * 96 * math.sin(math.radians(1.25) / 2)
THICK, FOOT, TOP = .2, .14, 2.15
LEG = .30                           # leg width from the pillar axis (pillar radius is .41)
RADIUS = SPAN / 2 - LEG             # half-width of the opening, .7472
SPRING = FOOT                       # the curve starts right on the pillar tops (2026-10-31 red-line sketch)
RISE = .62                          # low, round half-ellipse: crown at z .76
CROWN = SPRING + RISE
CURVE_SEGMENTS = 40
RIM = .10                           # rounded inner rim: quarter-round of this radius on both faces
RIM_STEPS = 5
SEED = 431907
UV_X0, UV_SIZE = -.2, 2.4
PEARL, AURORA, GLOW = 0, 1, 3
LABELS = ['Blueprint'] + [f'Random fill {i*10}%' for i in range(1, 10)] + ['Complete']

bpy.ops.wm.read_factory_settings(use_empty=True)
COLL = bpy.context.scene.collection
MATERIAL = bpy.data.materials.new('Aero_AuroraArch')


def region_uv(region):
    return ((region + .5) / 8, .5)


def opening_path():
    c = SPAN / 2
    return [(c - RADIUS * math.cos(math.pi * i / CURVE_SEGMENTS), SPRING + RISE * math.sin(math.pi * i / CURVE_SEGMENTS))
            for i in range(CURVE_SEGMENTS + 1)]


def opening_sdf(x, z):
    """Signed distance to the opening outline, negative inside the opening (matches the shader)."""
    qx, qz = abs(x - SPAN / 2), z - SPRING
    if qz < 0: return qx - RADIUS
    k0 = math.hypot(qx / RADIUS, qz / RISE); k1 = math.hypot(qx / RADIUS ** 2, qz / RISE ** 2)
    return k0 * (k0 - 1) / max(k1, 1e-9)            # close approximation of the ellipse distance


def inside_body(p):
    x, y, z = p
    if not (FOOT < z < TOP) or abs(y) > THICK: return False
    if x < 0: return math.hypot(x, y) < THICK
    return x <= SPAN and opening_sdf(x, z) > RIM


def set_uvs(bm, face, region):
    bake = bm.loops.layers.uv['BakeUV']; pal = bm.loops.layers.uv['PaletteUV']
    for l in face.loops:
        p = l.vert.co
        l[bake].uv = ((p.x - UV_X0) / UV_SIZE, p.z / UV_SIZE)
        l[pal].uv = region_uv(region)


def opening_rings():
    """Rounded inner rim: at every opening point, a cross-section that curls from the front face into the
    opening and back out to the back face (quarter-round fillets of radius RIM with a flat middle)."""
    c = SPAN / 2
    path = []
    for i in range(CURVE_SEGMENTS + 1):
        phi = math.pi * i / CURVE_SEGMENTS
        nx, nz = -math.cos(phi) / RADIUS, math.sin(phi) / RISE      # half-ellipse normal, into the wall
        n = math.hypot(nx, nz)
        path.append(((c - RADIUS * math.cos(phi), SPRING + RISE * math.sin(phi)), (nx / n, nz / n)))
    front = [(RIM * (1 - math.sin(t)), -THICK + RIM * (1 - math.cos(t)))
             for t in (math.pi / 2 * k / RIM_STEPS for k in range(RIM_STEPS + 1))]
    profile = front + [(o, -y) for o, y in reversed(front)]
    return [[(x + nx * o, y, z + nz * o) for o, y in profile] for (x, z), (nx, nz) in path]


def body_object():
    """Front and back faces around a rounded opening rim, with a half-round joint cap behind x = 0."""
    bm = bmesh.new()
    bm.loops.layers.uv.new('BakeUV'); bm.loops.layers.uv.new('PaletteUV')
    rings = [[bm.verts.new(p) for p in ring] for ring in opening_rings()]
    V = lambda x, y, z: bm.verts.new((x, y, z))
    f0, b0, f0t, b0t = V(0, -THICK, FOOT), V(0, THICK, FOOT), V(0, -THICK, TOP), V(0, THICK, TOP)
    fs, bs, fst, bst = V(SPAN, -THICK, FOOT), V(SPAN, THICK, FOOT), V(SPAN, -THICK, TOP), V(SPAN, THICK, TOP)
    front_face = bm.faces.new([f0] + [r[0] for r in rings] + [fs, fst, f0t])
    back_face = bm.faces.new(list(reversed([b0] + [r[-1] for r in rings] + [bs, bst, b0t])))
    faces = [(front_face, AURORA), (back_face, AURORA)]
    for a, b in zip(rings, rings[1:]):
        for k in range(len(a) - 1):
            faces.append((bm.faces.new((a[k], a[k + 1], b[k + 1], b[k])), PEARL))
    faces += [(bm.faces.new([f0] + rings[0] + [b0]), PEARL),             # left foot
              (bm.faces.new(rings[-1] + [bs, fs]), PEARL),               # right foot
              (bm.faces.new((fs, bs, bst, fst)), PEARL),                 # end on the next pillar axis
              (bm.faces.new((f0t, fst, bst, b0t)), PEARL)]               # flat top bearing
    arc = [math.pi / 2 + math.pi * k / 12 for k in range(1, 12)]
    bottom = [b0] + [V(THICK * math.cos(a), THICK * math.sin(a), FOOT) for a in arc] + [f0]
    top = [b0t] + [V(THICK * math.cos(a), THICK * math.sin(a), TOP) for a in arc] + [f0t]
    for k in range(len(bottom) - 1):
        # Cap faces that look along the wall carry the curtain so it runs through the pillar joint.
        side = abs(math.sin(math.pi / 2 + math.pi * (k + .5) / 12)) > .7
        faces.append((bm.faces.new((bottom[k], bottom[k + 1], top[k + 1], top[k])), AURORA if side else PEARL))
    faces += [(bm.faces.new(bottom), PEARL), (bm.faces.new(list(reversed(top))), PEARL)]
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f, region in faces: set_uvs(bm, f, region)
    # Smooth rim and cap; the flat faces keep crisp shading (their big triangles must not bend toward the rim).
    flat = {front_face, back_face}
    for e in bm.edges:
        fs_ = e.link_faces
        e.smooth = not (len(fs_) == 2 and (fs_[0] in flat or fs_[1] in flat or fs_[0].normal.angle(fs_[1].normal) > math.radians(30)))
    me = bpy.data.meshes.new('Arch_AuroraCurtain'); bm.to_mesh(me); bm.free()
    return new_obj('Arch_AuroraCurtain', me)


def smooth(obj):
    for p in obj.data.polygons: p.use_smooth = True


def new_obj(name, me):
    me.materials.clear(); me.materials.append(MATERIAL)
    o = bpy.data.objects.new(name, me); COLL.objects.link(o)
    return o


def solid_piece(source, index, seeds):
    obj = new_obj(f'Arch_Piece_{index:02}', source.data.copy())
    bm = bmesh.new(); bm.from_mesh(obj.data)
    seed = seeds[index]
    neighbors = sorted((p for i, p in enumerate(seeds) if i != index), key=lambda p: (p - seed).length_squared)
    for p in neighbors:
        center, normal = (seed + p) * .5, (p - seed).normalized()
        if not bm.verts: break
        if all((v.co - center).dot(normal) < -1e-6 for v in bm.verts): continue
        bmesh.ops.bisect_plane(bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces), dist=1e-6,
                               plane_co=center, plane_no=normal, clear_outer=True, clear_inner=False)
        cut = [e for e in bm.edges if e.is_boundary and all(abs((v.co - center).dot(normal)) < 1e-5 for v in e.verts)]
        for face in bmesh.ops.holes_fill(bm, edges=cut, sides=0)['faces']:
            face.smooth = False
            set_uvs(bm, face, PEARL)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data); bm.free()
    for poly in obj.data.polygons:
        pal = obj.data.uv_layers['PaletteUV'].data[poly.loop_indices[0]].uv
        if int(pal[0] * 8) == PEARL: poly.use_smooth = False
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

    inset = .012
    c = SPAN / 2
    e = RIM + inset                      # front opening edge sits RIM outside the rim surface
    arc = [(c - (RADIUS + e) * math.cos(math.pi * i / 24), FOOT + inset + (RISE + e - inset) * math.sin(math.pi * i / 24))
           for i in range(25)]
    outline = ([(inset, FOOT + inset)] + arc +
               [(SPAN - inset, FOOT + inset),
                (SPAN - inset, TOP - inset), (inset, TOP - inset), (inset, FOOT + inset)])
    for y in (-THICK + inset, THICK - inset):
        tube([(x, y, z) for x, z in outline])
        tube([(inset, y, 1.72), (SPAN - inset, y, 1.72)])             # aurora band guide
    for x, z in ((inset, FOOT + inset), (inset, TOP - inset), (SPAN - inset, FOOT + inset), (SPAN - inset, TOP - inset),
                 (c, CROWN + RIM + inset), (LEG - RIM - inset, FOOT + inset), (SPAN - LEG + RIM + inset, FOOT + inset)):
        tube([(x, -THICK + inset, z), (x, THICK - inset, z)])
    tube([(-THICK + inset, 0, FOOT + inset), (-THICK + inset, 0, TOP - inset)])
    me = bpy.data.meshes.new(f'Arch_Blueprint_{stage:02}')
    me.from_pydata(verts, [], faces)
    bake = me.uv_layers.new(name='BakeUV'); pal = me.uv_layers.new(name='PaletteUV')
    for poly in me.polygons:
        poly.use_smooth = True
        for li in poly.loop_indices:
            bake.data[li].uv = (.5, .5); pal.data[li].uv = region_uv(GLOW)
    me.update()
    return new_obj(f'Arch_Guides_{stage:02}', me)


def triangulate(obj):
    bm = bmesh.new(); bm.from_mesh(obj.data)
    bmesh.ops.triangulate(bm, faces=bm.faces, quad_method='BEAUTY', ngon_method='BEAUTY')
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
              'nonManifoldEdges': sum(not e.is_manifold for e in bm.edges),
              'volume': round(bm.calc_volume(signed=True), 6)}
    bm.free(); return report


SOURCE.mkdir(parents=True, exist_ok=True); EXPORT.mkdir(parents=True, exist_ok=True)
source = body_object()
smooth(source)
rng = random.Random(SEED)
seeds = []
while len(seeds) < 80:
    p = Vector((rng.uniform(-THICK, SPAN), rng.uniform(-.15, .15), rng.uniform(FOOT + .02, TOP - .02)))
    if inside_body(p) and all((p - o).length > .16 for o in seeds): seeds.append(p)
pieces = [solid_piece(source, i, seeds) for i in range(len(seeds))]
order = list(range(len(pieces))); rng.shuffle(order)

report = {'seed': SEED, 'span': SPAN, 'opening': {'legWidth': LEG, 'halfWidth': RADIUS, 'rise': RISE, 'springing': SPRING, 'crown': CROWN},
          'complete': None, 'stages': [], 'chunks': []}
stages = []
for stage in range(10):
    parts = [new_obj(f'Arch_Solid_{stage:02}_{pid}', pieces[pid].data.copy()) for pid in order[:len(pieces) * stage // 10]]
    parts.append(blueprint(stage))
    obj = join(parts, f'Arch_Stage_{stage:02}')
    triangulate(obj); stages.append(obj)
    report['stages'].append({'stage': stage, 'phase': LABELS[stage], 'filledPieceIds': order[:len(pieces) * stage // 10],
                             **topology(obj)})
for index, piece in enumerate(pieces):
    piece.name = f'Arch_Chunk_{order.index(index) // 8 + 1:02}_{index:02}'
    triangulate(piece)
    report['chunks'].append({'name': piece.name, **topology(piece)})
triangulate(source)
vs = [v.co for v in source.data.vertices]
report['complete'] = {'name': source.name, **topology(source),
                      'bounds': [[round(min(v[k] for v in vs), 5) for k in range(3)], [round(max(v[k] for v in vs), 5) for k in range(3)]],
                      'endsAtNextPillarAxis': max(v.x for v in vs) <= SPAN + 1e-4,
                      'onlyCapBehindStartAxis': all(math.hypot(v.x, v.y) <= THICK + 1e-3 for v in vs if v.x < -1e-4)}
report['chunkVolumeSum'] = round(sum(c['volume'] for c in report['chunks']), 6)

export([source], COMPLETE)
export(stages, EXPORT / 'AuroraArchStages.fbx')
export(pieces, EXPORT / 'AuroraArchChunks.fbx')

for i, o in enumerate(stages + [source]): o.location = (i * 2.8, 0, 0)
for i, p in enumerate(pieces): p.location = (i * .6, 3, 0); p.hide_render = True
(SOURCE / 'AuroraArchReport.json').write_text(json.dumps(report, indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / 'AuroraArch.blend'), compress=True)
print('AURORA_ARCH', json.dumps({'complete': report['complete'], 'chunkVolumeSum': report['chunkVolumeSum'],
                                 'stageTris': [s['triangles'] for s in report['stages']],
                                 'openStages': [s['stage'] for s in report['stages'] if s['nonManifoldEdges']],
                                 'openChunks': [c['name'] for c in report['chunks'] if c['nonManifoldEdges']],
                                 'emptyChunks': [c['name'] for c in report['chunks'] if c['triangles'] == 0]}))
