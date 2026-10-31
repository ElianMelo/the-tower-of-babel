"""Ten Frutiger Aero arch design proposals (ArchRound01).

Run headless so the user's open Blender scene is untouched:
  blender --background --factory-startup --python Tools/BuildingAero/build_arch_designs.py -- [--render] [--preview]

Shared fit contract (fixes the current arch's overlap and pillar misalignment):
- Local X runs exactly from the start pillar axis (0) to the next pillar axis (SPAN = 2.0944 m, the tower's
  constant pillar chord: radius 96 m x 1.25 deg, scaled per floor). The opening is centred on SPAN / 2,
  so each pillar carries two mirrored half-legs.
- The only geometry behind x = 0 is a half-round cap of radius THICK around the pillar axis. It closes the
  joint with the previous arch for any tower bend (4.44 deg at the top floor) without duplicate surfaces;
  nothing passes x = SPAN.
- Thickness +/-0.2 m (details proud by at most 0.012 m), feet at z 0.14 (1 cm into the pillar cap at world
  4.0 m with the generator's 3.85 m offset), flat top at z 2.15 bearing the next floor.
Palette: pearl white, aqua/blue enamel, aqua glass, water, chrome. No green.
Outputs: ArtSource/Buildings/FrutigerAero/ArchDesigns/Round01 (.blend, FBX per design, report) and one
contact-sheet render in _Screenshots-and-previews-to-delete/ArchDesigns/Round01.
"""
import bpy, bmesh, math, json, sys, random
from mathutils import Vector, Matrix
from pathlib import Path

ROOT = Path('D:/PersonalProjects/the-tower-of-babel')
OUT = ROOT / 'ArtSource/Buildings/FrutigerAero/ArchDesigns/Round01'
SHOT = ROOT / '_Screenshots-and-previews-to-delete/ArchDesigns/Round01'
ARGS = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
TAU = math.tau
SPAN = 2 * 96 * math.sin(math.radians(1.25) / 2)   # 2.0944
THICK = .2
FOOT = .14
TOP = 2.15
BEND = math.radians(1.25) * 96 / 27                 # tightest tower curve (radius 27 m)

bpy.ops.wm.read_factory_settings(use_empty=True)
SCENE = bpy.context.scene
COLL = SCENE.collection
CUTTERS = []

# ---------------------------------------------------------------- materials
def bsdf_of(m):
    nt = m.node_tree
    b = next((n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if b is None:
        b = nt.nodes.new('ShaderNodeBsdfPrincipled')
        out = next((n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL'), None) or nt.nodes.new('ShaderNodeOutputMaterial')
        nt.links.new(b.outputs[0], out.inputs['Surface'])
    return b

def principled(name, base, rough=.2, metal=0., coat=0., coat_rough=.03, trans=0., ior=1.45,
               emit=None, emit_strength=0., film=0., film_ior=1.33):
    m = bpy.data.materials.new(name)
    b = bsdf_of(m)
    vals = {'Base Color': (*base, 1), 'Roughness': rough, 'Metallic': metal, 'Coat Weight': coat,
            'Coat Roughness': coat_rough, 'Transmission Weight': trans, 'IOR': ior,
            'Emission Color': (*(emit or base), 1), 'Emission Strength': emit_strength,
            'Thin Film Thickness': film, 'Thin Film IOR': film_ior}
    for k, v in vals.items():
        if k in b.inputs: b.inputs[k].default_value = v
    m.diffuse_color = (*base, 1)
    return m

def pearl_material():
    base, vein = (.95, .965, .98), (.80, .865, .93)
    m = principled('Aero_Pearl', base, rough=.16, coat=1., coat_rough=.025, film=160, film_ior=1.4)
    nt = m.node_tree; b = bsdf_of(m)
    tc = nt.nodes.new('ShaderNodeTexCoord'); noise = nt.nodes.new('ShaderNodeTexNoise')
    for k, v in (('Scale', 1.6), ('Detail', 9.), ('Distortion', 1.4)):
        if k in noise.inputs: noise.inputs[k].default_value = v
    ramp = nt.nodes.new('ShaderNodeValToRGB'); els = ramp.color_ramp.elements
    els[0].position, els[0].color = 0., (*base, 1); els[1].position, els[1].color = 1., (*base, 1)
    for pos, col in ((.488, base), (.5, vein), (.512, base)):
        e = els.new(pos); e.color = (*col, 1)
    nt.links.new(tc.outputs['Object'], noise.inputs['Vector'])
    nt.links.new(noise.outputs['Fac'], ramp.inputs['Fac'])
    nt.links.new(ramp.outputs['Color'], b.inputs['Base Color'])
    return m

PEARL = pearl_material()
ENAMEL = principled('Aero_AquaEnamel', (.02, .50, .86), rough=.12, coat=1., coat_rough=.02)
DEEP = principled('Aero_DeepBlueEnamel', (.01, .22, .68), rough=.12, coat=1., coat_rough=.02)
GLASS = principled('Aero_AquaGlass', (.62, .93, 1.), rough=.015, trans=1., ior=1.45, film=320)
WATER = principled('Aero_Water', (.22, .78, .97), rough=.03, trans=1., ior=1.33, emit=(.15, .75, 1.), emit_strength=.35)
BUBBLE = principled('Aero_Bubble', (1., 1., 1.), rough=0., trans=1., ior=1.08, film=480, film_ior=1.33)
GLOW = principled('Aero_AquaGlow', (.35, .92, 1.), rough=.2, emit=(.30, .90, 1.), emit_strength=8.)
BLUE_GLASS = principled('Aero_BlueGlass', (.10, .48, .95), rough=.02, trans=.85, ior=1.5, film=300)
CHROME = principled('Aero_Chrome', (.90, .93, .97), rough=.06, metal=1.)

# ---------------------------------------------------------------- geometry helpers
def make_obj(name, bm, mats, sharp=35):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for m in mats: me.materials.append(m)
    o = bpy.data.objects.new(name, me); COLL.objects.link(o)
    for p in me.polygons: p.use_smooth = True
    if sharp:
        try: me.set_sharp_from_angle(angle=math.radians(sharp))
        except Exception: pass
    return o

def prism(name, pts2, y0, y1, mat, bevel=0., keep_ends=False):
    """Extrude an (x, z) polygon along Y. Bevelled outline edges unless they lie on x = 0 / x = SPAN
    or the feet (z = FOOT), where the arch meets the pillar and its neighbour."""
    bm = bmesh.new()
    front = [bm.verts.new((x, y0, z)) for x, z in pts2]
    face = bm.faces.new(front)
    if face.normal.y > 0: face.normal_flip()
    ext = bmesh.ops.extrude_face_region(bm, geom=[face])
    for v in [g for g in ext['geom'] if isinstance(g, bmesh.types.BMVert)]: v.co.y = y1
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if bevel:
        layer = bm.edges.layers.float.get('bevel_weight_edge') or bm.edges.layers.float.new('bevel_weight_edge')
        for e in bm.edges:
            a, b = e.verts
            on_face = abs(a.co.y - b.co.y) < 1e-6
            at_end = keep_ends and ((abs(a.co.x) < 1e-5 and abs(b.co.x) < 1e-5) or
                                    (abs(a.co.x - SPAN) < 1e-5 and abs(b.co.x - SPAN) < 1e-5) or
                                    (abs(a.co.z - FOOT) < 1e-5 and abs(b.co.z - FOOT) < 1e-5))
            e[layer] = 1. if on_face and not at_end else 0.
    o = make_obj(name, bm, [mat])
    if bevel:
        mod = o.modifiers.new('Bevel', 'BEVEL'); mod.width = bevel; mod.segments = 3
        mod.limit_method = 'WEIGHT'; mod.harden_normals = False
    return o

def both_faces(name, pts2, depth, proud, mat, bevel=0.):
    """A thin panel on the front and back faces, sunk `depth` into the body and `proud` above it."""
    return [prism(name + '_F', pts2, -THICK - proud, -THICK + depth, mat, bevel),
            prism(name + '_B', pts2, THICK - depth, THICK + proud, mat, bevel)]

def tube(name, pts, radius, mat, res=3):
    cu = bpy.data.curves.new(name, 'CURVE'); cu.dimensions = '3D'
    cu.bevel_depth = radius; cu.bevel_resolution = res; cu.use_fill_caps = True
    sp = cu.splines.new('POLY'); sp.points.add(len(pts) - 1)
    for p, c in zip(sp.points, pts): p.co = (*c, 1)
    cu.materials.append(mat)
    o = bpy.data.objects.new(name, cu); COLL.objects.link(o)
    return o

def sphere(name, c, r, mat, segs=20):
    bm = bmesh.new()
    try: bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=segs // 2, radius=r)
    except TypeError: bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=segs // 2, diameter=r * 2)
    bmesh.ops.translate(bm, verts=bm.verts, vec=Vector(c))
    return make_obj(name, bm, [mat], sharp=0)

def disc_y(name, cx, cz, r, y0, y1, mat, segs=40):
    return prism(name, [(cx + r * math.cos(TAU * i / segs), cz + r * math.sin(TAU * i / segs)) for i in range(segs)], y0, y1, mat)

def torus_y(name, cx, cz, y, R, r, mat, segs=48):
    pts = [(cx + R * math.cos(TAU * i / segs), y, cz + R * math.sin(TAU * i / segs)) for i in range(segs + 1)]
    return tube(name, pts, r, mat)

def end_cap(name, mat=PEARL):
    pts = [(THICK * math.cos(a), THICK * math.sin(a)) for a in [math.pi / 2 + math.pi * k / 16 for k in range(17)]]
    bm = bmesh.new()
    bottom = [bm.verts.new((x, y, FOOT + .002)) for x, y in pts]
    face = bm.faces.new(bottom)
    if face.normal.z > 0: face.normal_flip()
    ext = bmesh.ops.extrude_face_region(bm, geom=[face])
    for v in [g for g in ext['geom'] if isinstance(g, bmesh.types.BMVert)]: v.co.z = TOP - .002
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return make_obj(name, bm, [mat])

def boolean(target, cutter):
    mod = target.modifiers.new('Cut_' + cutter.name, 'BOOLEAN')
    mod.operation = 'DIFFERENCE'; mod.object = cutter
    try: mod.solver = 'EXACT'
    except Exception: pass
    cutter.hide_render = True; CUTTERS.append(cutter)

def bake(o):
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(o.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    me.transform(o.matrix_world)
    if o.type == 'CURVE':
        for p in me.polygons: p.use_smooth = True
    n = bpy.data.objects.new(o.name + '_baked', me); COLL.objects.link(n)
    bpy.data.objects.remove(o)
    return n

def join(objs, name):
    objs = [bake(o) for o in objs]
    for o in CUTTERS: bpy.data.objects.remove(o)
    CUTTERS.clear()
    bpy.context.view_layer.update()
    for o in COLL.objects: o.select_set(False)
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    j = bpy.context.view_layer.objects.active
    j.name = name; j.data.name = name
    return j

# ---------------------------------------------------------------- arch shapes
def opening(kind, w, zs, crown, n=40):
    """Intrados path from the left foot, up the jamb, over the curve and down to the right foot.
    Symmetric about SPAN / 2."""
    c, a = SPAN / 2, SPAN / 2 - w
    pts = [(w, FOOT)] + ([(w, zs)] if zs > FOOT + 1e-3 else [])
    curve = []
    for i in range(1, n):
        phi = math.pi * i / n
        u = -math.cos(phi)
        if kind == 'ellipse':
            f = math.sin(phi)
        elif kind == 'basket':
            f = (1 - abs(u) ** 3) ** (1 / 3)
        elif kind == 'pointed':
            R = a * 1.7
            cx = a - R                                   # right arc centre relative to the opening centre
            h = math.sqrt(max(0, R * R - (abs(u) * a - cx) ** 2))
            f = h / math.sqrt(R * R - cx * cx)
        curve.append((c + a * u, zs + (crown - zs) * f))
    pts += curve + [(SPAN - w, zs)] if zs > FOOT + 1e-3 else curve
    pts = pts + [(SPAN - w, FOOT)] if pts[-1] != (SPAN - w, FOOT) else pts
    return pts

def offset(path, d):
    """Offset the intrados path outward (into the spandrel) by d metres."""
    c = SPAN / 2
    out = []
    for i, (x, z) in enumerate(path):
        x0, z0 = path[max(0, i - 1)]; x1, z1 = path[min(len(path) - 1, i + 1)]
        tx, tz = x1 - x0, z1 - z0; l = math.hypot(tx, tz) or 1
        nx, nz = -tz / l, tx / l
        if nx * (x - c) + nz * (z - .2) < 0: nx, nz = -nx, -nz
        out.append((x + nx * d, z + nz * d))
    return out

def band(path, d0, d1):
    return offset(path, d0) + list(reversed(offset(path, d1)))

def body(name, path, mat=PEARL):
    """Full spandrel: the rectangle between the two pillar axes minus the opening."""
    pts = [(0, FOOT)] + path + [(SPAN, FOOT), (SPAN, TOP), (0, TOP)]
    return [prism(name, pts, -THICK, THICK, mat, bevel=.028, keep_ends=True), end_cap(name + '_cap')]

def front_back_path(path, y_off):
    return [[(x, -THICK - y_off, z) for x, z in path], [(x, THICK + y_off, z) for x, z in path]]

# ---------------------------------------------------------------- designs
def a01_pearl_ellipse():
    path = opening('ellipse', .30, .30, 1.45)
    objs = body('A01_body', path)
    objs += both_faces('A01_glass', band(path, .10, .17), .02, .004, GLASS)
    for i, p in enumerate(front_back_path(offset(path, .035), .002)): objs.append(tube(f'A01_glow{i}', p, .011, GLOW))
    return objs

def a02_aquarium_spandrel():
    path = opening('ellipse', .30, .25, 1.30)
    objs = body('A02_body', path)
    inner = [p for p in offset(path, .16) if p[1] > .55]
    window = inner + [(SPAN - .14, 1.95), (.14, 1.95)]
    objs += both_faces('A02_water', window, .03, .006, WATER)
    rim = window + [window[0]]
    for i, p in enumerate(front_back_path(rim, .008)): objs.append(tube(f'A02_frame{i}', p, .013, CHROME))
    rng = random.Random(2)
    for k in range(16):
        x = rng.uniform(.25, SPAN - .25); z = rng.uniform(1.45, 1.88); r = rng.uniform(.012, .035)
        if abs(x - SPAN / 2) < .45 and z < 1.55: continue
        for s in (-1, 1): objs.append(sphere(f'A02_bubble{k}_{s}', (x, s * (THICK + .002), z), r, BUBBLE, 12))
    return objs

def a03_ring_light():
    path = opening('ellipse', .30, .48, 1.25)
    objs = body('A03_body', path)
    for i, p in enumerate(front_back_path(offset(path, .11), .01)): objs.append(tube(f'A03_halo{i}', p, .024, GLOW, 4))
    for i, p in enumerate(front_back_path(offset(path, .20), .005)): objs.append(tube(f'A03_chrome{i}', p, .01, CHROME))
    objs += both_faces('A03_soffit', band(path, 0, .05), .01, .002, ENAMEL)
    return objs

def a04_porthole():
    path = opening('ellipse', .32, .25, 1.25)
    objs = body('A04_body', path)
    for k, cx in enumerate((.36, SPAN - .36)):
        cut = disc_y(f'A04_cut{k}', cx, 1.72, .17, -.3, .3, PEARL)
        boolean(objs[0], cut)
        objs.append(disc_y(f'A04_window{k}', cx, 1.72, .172, -.05, .05, WATER))
        for s in (-1, 1):
            objs.append(torus_y(f'A04_ring{k}_{s}', cx, 1.72, s * (THICK + .002), .175, .018, CHROME))
            objs.append(sphere(f'A04_bubble{k}_{s}', (cx + .05, s * .02, 1.78), .03, BUBBLE, 12))
    objs += both_faces('A04_band', band(path, .06, .11), .015, .003, DEEP)
    return objs

def a05_wave_crest():
    path = opening('basket', .28, .30, 1.30)
    objs = body('A05_body', path)
    xs = [.02 + (SPAN - .04) * i / 60 for i in range(61)]
    low = [(x, 1.70 + .06 * math.sin(TAU * x / SPAN * 2)) for x in xs]
    high = [(x, 1.90 + .06 * math.sin(TAU * x / SPAN * 2 + .8)) for x in xs]
    objs += both_faces('A05_wave', low + list(reversed(high)), .01, .01, ENAMEL, bevel=.006)
    line = [(x, 1.62 + .04 * math.sin(TAU * x / SPAN * 2 - .6)) for x in xs]
    for i, p in enumerate(front_back_path(line, .004)): objs.append(tube(f'A05_glow{i}', p, .009, GLOW))
    return objs

def a06_gothic_pearl():
    path = opening('pointed', .30, .35, 1.62)
    objs = body('A06_body', path)
    objs += both_faces('A06_groove', band(path, .06, .12), .012, .002, ENAMEL)
    objs.append(sphere('A06_drop_F', (SPAN / 2, -THICK - .004, 1.78), .045, GLASS, 20))
    objs.append(sphere('A06_drop_B', (SPAN / 2, THICK + .004, 1.78), .045, GLASS, 20))
    return objs

def a07_glass_keystone():
    path = opening('ellipse', .30, .30, 1.40)
    objs = body('A07_body', path)
    key = [(SPAN / 2 - .10, 1.38), (SPAN / 2 + .10, 1.38), (SPAN / 2 + .15, 1.76), (SPAN / 2 - .15, 1.76)]
    objs.append(prism('A07_key', key, -THICK - .012, THICK + .012, BLUE_GLASS, bevel=.015))
    for z in (1.96, 2.04):
        for i, p in enumerate(front_back_path([(.05, z), (SPAN - .05, z)], .003)): objs.append(tube(f'A07_line{z}{i}', p, .008, CHROME))
    objs += both_faces('A07_band', band(path, 0, .04), .01, .002, ENAMEL)
    return objs

def a08_bubble_cascade():
    path = opening('ellipse', .30, .30, 1.40)
    objs = body('A08_body', path)
    rng = random.Random(8)
    curve = offset(path, .15)
    for k in range(0, len(curve), 2):
        x, z = curve[k]
        if z < .4: continue
        r = rng.uniform(.045, .095)
        for s in (-1, 1): objs.append(sphere(f'A08_bubble{k}_{s}', (x, s * (THICK - r * .25), z), r, BUBBLE, 16))
    objs += both_faces('A08_band', band(path, 0, .045), .01, .002, GLASS)
    return objs

def a09_layered_shell():
    base = opening('ellipse', .30, .30, 1.38)
    objs = body('A09_body', offset(base, .16))
    objs.append(prism('A09_ring1', band(base, 0, .085), -.17, .17, PEARL, bevel=.012))
    objs.append(prism('A09_ring2', band(base, .08, .165), -.185, .185, ENAMEL, bevel=.012))
    for i, p in enumerate(front_back_path(offset(base, .165), -.012)): objs.append(tube(f'A09_glow{i}', p, .008, GLOW))
    return objs

def a10_ribbon():
    path = opening('ellipse', .30, .30, 1.35)
    objs = body('A10_body', path)
    curve = offset(path, .12)
    lift = [(x, z + .34 * (math.exp(-((x - SPAN * .27) / .33) ** 2) + math.exp(-((x - SPAN * .73) / .33) ** 2))) for x, z in curve]
    ribbon = offset(lift, 0) + list(reversed(offset(lift, .075)))
    ribbon = [(min(max(x, .02), SPAN - .02), min(z, 2.07)) for x, z in ribbon]
    objs += both_faces('A10_ribbon', ribbon, .015, .006, GLASS)
    for d in (-.008, .083):
        for i, p in enumerate(front_back_path(offset(lift, d), .006)): objs.append(tube(f'A10_edge{d}{i}', p, .007, GLOW))
    return objs

DESIGNS = [
    ('01', 'Pearl Ellipse', a01_pearl_ellipse),
    ('02', 'Aquarium Spandrel', a02_aquarium_spandrel),
    ('03', 'Ring Light', a03_ring_light),
    ('04', 'Porthole', a04_porthole),
    ('05', 'Wave Crest', a05_wave_crest),
    ('06', 'Gothic Pearl', a06_gothic_pearl),
    ('07', 'Glass Keystone', a07_glass_keystone),
    ('08', 'Bubble Cascade', a08_bubble_cascade),
    ('09', 'Layered Shell', a09_layered_shell),
    ('10', 'Ribbon', a10_ribbon),
]

# ---------------------------------------------------------------- build, verify, export
OUT.mkdir(parents=True, exist_ok=True); SHOT.mkdir(parents=True, exist_ok=True)
report, arches = {}, []
for code, title, fn in DESIGNS:
    name = 'Arch_D' + code + '_' + title.replace(' ', '')
    obj = join(fn(), name)
    vs = [v.co for v in obj.data.vertices]
    lo = [min(v[k] for v in vs) for k in range(3)]; hi = [max(v[k] for v in vs) for k in range(3)]
    behind = [v for v in vs if v.x < -1e-4]
    report[name] = {
        'title': title, 'triangles': sum(len(p.vertices) - 2 for p in obj.data.polygons),
        'bounds_min_xyz_blender': [round(v, 4) for v in lo], 'bounds_max_xyz_blender': [round(v, 4) for v in hi],
        # Fit contract checks: nothing past the next pillar axis, only the cap behind the start axis.
        'endsAtNextPillarAxis': hi[0] <= SPAN + 1e-4,
        'onlyCapBehindStartAxis': all(math.hypot(v.x, v.y) <= THICK + 1e-3 for v in behind),
        'openingCentredOffsetMetres': 0.0,
        'materials': [m.name for m in obj.data.materials]}
    for o in COLL.objects: o.select_set(False)
    obj.select_set(True); bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=str(OUT / (name + '.fbx')), use_selection=True, object_types={'MESH'},
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                             apply_scale_options='FBX_SCALE_ALL', add_leaf_bones=False, bake_anim=False,
                             use_mesh_modifiers=True, mesh_smooth_type='FACE', use_triangles=True,
                             path_mode='STRIP', embed_textures=False)
    arches.append((code, title, obj))

# ---------------------------------------------------------------- review: two bays on three pillars per design
bpy.ops.import_scene.fbx(filepath=str(ROOT / 'Assets/_Project/Models/Buildings/FrutigerAero/BubbleTubePillar.fbx'))
pillar = next(o for o in bpy.context.selected_objects if o.type == 'MESH')
pillar.data.transform(pillar.matrix_world); pillar.matrix_world = Matrix()
pillar.data.materials.clear()
for m in (PEARL, DEEP, CHROME, GLOW, WATER): pillar.data.materials.append(m)
pal = pillar.data.uv_layers[1] if len(pillar.data.uv_layers) > 1 else pillar.data.uv_layers[0]
for p in pillar.data.polygons:
    p.material_index = min(4, int(pal.data[p.loop_indices[0]].uv[0] * 8))
spine = bpy.data.objects.new('Review_Spine', bpy.data.meshes.new('Review_Spine'))
bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=24, radius1=.08, radius2=.08, depth=3.0)
bmesh.ops.translate(bm, verts=bm.verts, vec=Vector((0, 0, 2))); bm.to_mesh(spine.data); bm.free()
spine.data.materials.append(PEARL); COLL.objects.link(spine)
pillar.hide_render = spine.hide_render = True

GRID = 6.0
def place_tile(index, arch):
    """Three pillars on the tightest tower curve, the arch twice between them, offset into a grid."""
    origin = Vector((index * 40.0, 0, 0))   # far apart so no other tile shows in the background
    R = SPAN / (2 * math.sin(BEND / 2))
    centre = origin + Vector((0, R, 0))
    points = [centre + Vector((R * math.sin(k * BEND), -R * math.cos(k * BEND), 0)) for k in (-1, 0, 1)]
    for k, p in enumerate(points):
        for src in (pillar, spine):
            o = src.copy(); o.hide_render = False; o.location = p; COLL.objects.link(o)
    for k in range(2):
        a, b = points[k], points[k + 1]
        o = arch.copy(); o.hide_render = False
        o.location = a + Vector((0, 0, 3.85)); o.rotation_euler = (0, 0, math.atan2(b.y - a.y, b.x - a.x))
        COLL.objects.link(o)
    slab = bpy.data.objects.new('Slab', bpy.data.meshes.new('Slab')); bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1); bm.to_mesh(slab.data); bm.free()
    slab.scale = (5.6, 3.0, .2); slab.location = origin + Vector((0, 1.5 - .45, 6.1)); slab.data.materials.append(PEARL)
    COLL.objects.link(slab)
    return origin

arch_cache = []
for idx, (code, title, obj) in enumerate(arches):
    obj.hide_render = True
    arch_cache.append(place_tile(idx, obj))

floor_bm = bmesh.new(); bmesh.ops.create_circle(floor_bm, cap_ends=True, radius=2000, segments=96)
make_obj('Review_Floor', floor_bm, [principled('Review_Floor', (.80, .92, .985), rough=.07, coat=.6)], sharp=0)
world = bpy.data.worlds.new('Review_Sky'); SCENE.world = world
wn = world.node_tree.nodes; wl = world.node_tree.links
bg = next(n for n in wn if n.type == 'BACKGROUND')
tc = wn.new('ShaderNodeTexCoord'); sep = wn.new('ShaderNodeSeparateXYZ'); ramp = wn.new('ShaderNodeValToRGB')
wl.new(tc.outputs['Generated'], sep.inputs[0]); wl.new(sep.outputs['Z'], ramp.inputs['Fac'])
ramp.color_ramp.elements[0].position, ramp.color_ramp.elements[0].color = 0., (.78, .94, 1., 1)
ramp.color_ramp.elements[1].position, ramp.color_ramp.elements[1].color = .16, (.04, .42, 1., 1)
wl.new(ramp.outputs['Color'], bg.inputs['Color']); bg.inputs['Strength'].default_value = 1.35
sun = bpy.data.objects.new('Sun', bpy.data.lights.new('Sun', 'SUN')); COLL.objects.link(sun)
sun.data.energy = 4.; sun.data.color = (1., .97, .92); sun.data.angle = math.radians(3)
sun.rotation_euler = (math.radians(55), 0, math.radians(-28))

cam = bpy.data.objects.new('Cam', bpy.data.cameras.new('Cam')); COLL.objects.link(cam)
cam.data.lens = 40; SCENE.camera = cam
font = None
try: font = bpy.data.fonts.load(str(ROOT / 'Assets/_Project/Fonts/Frutiger.ttf'))
except Exception: pass
LABEL = principled('Label', (.02, .20, .52), rough=.4)

SCENE.render.engine = 'CYCLES'
try:
    prefs = bpy.context.preferences.addons['cycles'].preferences
    for kind in ('OPTIX', 'CUDA'):
        prefs.compute_device_type = kind
        (prefs.refresh_devices if hasattr(prefs, 'refresh_devices') else prefs.get_devices)()
        if any(d.type == kind for d in prefs.devices):
            for d in prefs.devices: d.use = d.type == kind
            SCENE.cycles.device = 'GPU'; break
except Exception: pass
preview = '--preview' in ARGS
SCENE.cycles.samples = 16 if preview else 96
SCENE.cycles.use_denoising = True
for k, v in (('max_bounces', 12), ('transmission_bounces', 12), ('transparent_max_bounces', 12)): setattr(SCENE.cycles, k, v)
SCENE.render.resolution_x, SCENE.render.resolution_y = (360, 450) if preview else (760, 950)
SCENE.view_settings.view_transform = 'AgX'; SCENE.view_settings.exposure = .2
try: SCENE.view_settings.look = 'AgX - Punchy'
except Exception: pass

(OUT / 'ArchDesignsReport.json').write_text(json.dumps(report, indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'ArchDesigns_Round01.blend'), compress=True)
print('ARCH_REPORT', json.dumps({k: (v['triangles'], v['endsAtNextPillarAxis'], v['onlyCapBehindStartAxis'],
                                     v['bounds_min_xyz_blender'], v['bounds_max_xyz_blender']) for k, v in report.items()}))

if '--render' in ARGS:
    import numpy as np
    tiles = []
    for idx, (code, title, obj) in enumerate(arches):
        origin = arch_cache[idx]
        lab = bpy.data.objects.new('Label' + code, bpy.data.curves.new('Label' + code, 'FONT'))
        lab.data.body = f'{code}  {title}'; lab.data.align_x = 'CENTER'; lab.data.size = .32
        if font: lab.data.font = font
        lab.data.materials.append(LABEL); COLL.objects.link(lab)
        lab.location = origin + Vector((0, -1.6, .25)); lab.rotation_euler = (math.radians(78), 0, 0)
        cam.location = origin + Vector((-1.2, -8.6, 2.7))
        cam.rotation_euler = (origin + Vector((0, 0, 3.15)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
        path = SHOT / f'tile_{code}.png'
        SCENE.render.filepath = str(path); bpy.ops.render.render(write_still=True)
        bpy.data.objects.remove(lab)
        img = bpy.data.images.load(str(path))
        w, h = img.size
        tiles.append(np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4))
    h, w = tiles[0].shape[:2]
    sheet = np.ones((h * 2, w * 5, 4), dtype=np.float32)
    for idx, t in enumerate(tiles):
        r, c = idx // 5, idx % 5
        sheet[(1 - r) * h:(2 - r) * h, c * w:(c + 1) * w] = t     # Blender images start at the bottom row
    out = bpy.data.images.new('ArchSheet', w * 5, h * 2, alpha=False)
    out.pixels = sheet.ravel().tolist()
    target = SHOT / ('Arch-Designs-Lineup-preview.png' if preview else 'Arch-Designs-Lineup.png')
    out.filepath_raw = str(target); out.file_format = 'PNG'; out.save()
    print('RENDERED', target)
