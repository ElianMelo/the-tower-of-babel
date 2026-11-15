"""Cloud Dandelions, production model: the Silk Clock (Round02 design 01), chosen by the user on 2026-11-15.

Improvements requested with the choice:
  - rosette leaves in clearly different sizes: four long, four medium and four young leaves that stand up more;
  - grass in different sizes: short, medium and tall blades, the tall ones clumped, in more shades of green;
  - more colours hand-painted on the green parts: leaves, stems, bracts, bud and crown sample the brush-painted
    atlas Textures/DandelionGreens.png (paint_dandelion_greens.py, run it first).

Hierarchy (front faces -Y, Unity -Z; exported like build_sky_workshop.py):
  SkyDandelions
    SkyEssence                      gathered; hidden by Resource during cooldown
      Bloom, Stalk_1, Stalk_2       one flower each, pivot at its base: painted stem, receptacle and bracts.
                                    Siblings, so each can be picked on its own (EssenceHarvestFeedback, SkyBloom).
        Seeds_<flower>              the clock, origin at the head centre. Workshop_DandelionSeeds (vertex colours:
                                    rgb albedo, alpha emission weight) with each seed's umbrella centre in UV maps
                                    SeedPivot (x, y) and SeedInfo (z, random); the Dandelion Seeds shader flies them.
      Seed_0..4                     loose parachute seeds
    Rosette                         leaves, crown, bud, yellow bloom, pebbles (stays)
    Grass                           wind-shader blades: vertex colour albedo, sway weight in alpha (stays)
Also exports DandelionSeed.fbx, one parachute seed for the particle systems.

Run it in the connected Blender (MCP socket) or headless:
  blender -b --factory-startup --python build_sky_dandelions.py      (REVIEW = True renders a Blender still)
"""
import bpy, bmesh, math, random
from mathutils import Vector, Matrix, Euler

ROOTP = 'D:/PersonalProjects/the-tower-of-babel'
KIT = ROOTP + '/Tools/BuildingAero/sky_concept_kit.py'
DKIT = ROOTP + '/Tools/BuildingAero/dandelion_kit.py'
SCENE_NAME = 'Aero_SkyDandelions'
exec(open(KIT, encoding='utf-8').read(), globals())
exec(open(DKIT, encoding='utf-8').read(), globals())
OUT = ROOTP + '/Assets/_Project/Models/Resources/'
TEX = OUT + 'Textures/DandelionGreens.png'
BLEND = ROOTP + '/ArtSource/Resources/Sky_Dandelions.blend'
REVIEW = globals().get('REVIEW', False)
LEAF_UV = lambda u, v: (u, .30 + .70 * v)          # atlas regions painted by paint_dandelion_greens.py
STEM_UV = lambda u, v: (u, .02 + .24 * v)


# ---------------------------------------------------------------- Workshop materials (names remap in Unity)
def workshop(name):
    m, n, l, b = _mat('Workshop_' + name)
    return m, n, l, b


def painted():
    m, n, l, b = workshop('DandelionGreen'); _set(b, rough=.42, coat=.6, coat_rough=.08)
    tex = n.new('ShaderNodeTexImage'); tex.image = bpy.data.images.load(TEX, check_existing=True)
    l.new(tex.outputs['Color'], b.inputs['Base Color'])
    return m


def vertex_coloured(name, emit=0., **kw):
    """Preview of a Unity material that reads vertex colours (Col): albedo in rgb, emission weight in alpha."""
    m, n, l, b = workshop(name); _set(b, **kw)
    attr = n.new('ShaderNodeVertexColor'); attr.layer_name = 'Col'
    l.new(attr.outputs['Color'], b.inputs['Base Color'])
    if emit:
        l.new(attr.outputs['Color'], b.inputs['Emission Color'])
        k = n.new('ShaderNodeMath'); k.operation = 'MULTIPLY'; k.inputs[1].default_value = emit
        l.new(attr.outputs['Alpha'], k.inputs[0]); l.new(k.outputs[0], b.inputs['Emission Strength'])
    return m


def flat_colour(name, color, **kw):
    m, n, l, b = workshop(name); _set(b, color=(*color, 1), **kw)
    return m


green = painted()
MAT.update({
    'DGreen': green, 'StemGel': green, 'Bract': green,                      # every green part samples the atlas
    'Seeds': vertex_coloured('DandelionSeeds', emit=1.2, rough=.4, sss=.15),
    'Pappus': None, 'AcheneAqua': None, 'Beak': None,                       # set below: colour keys of 'Seeds'
    'Yellow': flat_colour('Pollen', (1, .78, .06), rough=.3, emit=(1, .65, 0, 1), es=.25),
    'WhitePebble': flat_colour('WhitePebble', (.88, .94, .97), rough=.2, coat=1),        # natural nodes avoid the machine Pearl
    'AquaPebble': flat_colour('AquaPebble', (.55, .86, .95), rough=.12, coat=1, coat_rough=.02),
    'GrassBlade': vertex_coloured('GrassBlade', rough=.5),
})
MAT['FloretCentre'] = MAT['Yellow']
for key in ('Pappus', 'AcheneAqua', 'Beak'): MAT[key] = MAT['Seeds']
# Game-distance tuning: the silk is thicker and glows more than the concept, the achenes are a light aqua.
SEED_COLORS = {'Pappus': (1, 1, 1, .75), 'AcheneAqua': (.22, .64, .92, .12), 'Beak': (.92, .97, 1, .45)}
ST = {**BASE_ST, 'n': 84, 'k': 10, 'no_rec': True, 'bract': 'DGreen', 'bw': .0024, 'beak_w': .002, 'ach_w': .012}


# ---------------------------------------------------------------- assembly
def export(root, path, types={'MESH', 'EMPTY'}):
    bpy.ops.object.select_all(action='DESELECT'); root.select_set(True)
    for c in root.children_recursive: c.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types=types, use_mesh_modifiers=True,
                             bake_space_transform=False, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', bake_anim=False, add_leaf_bones=False, colors_type='SRGB')


def bezier(p0, p1, p2, n):
    return [p0 * (1 - t) ** 2 + p1 * 2 * t * (1 - t) + p2 * t * t for t in [i / n for i in range(n + 1)]]


TALL_CLUMPS = [(math.cos(a) * r, math.sin(a) * r) for a, r in ((.4, .82), (1.6, .9), (2.7, .78), (3.9, .88), (5.0, .8), (5.9, .92))]
TIPS = [((.62, 1, .3), 5), ((.82, .96, .26), 3), ((.28, .86, .34), 3), ((.32, .9, .62), 2), ((.86, .86, .32), 1),
        ((.42, .84, .76), 1), ((.52, .95, .18), 3)]


def painted_grass(name, count, rmin, rmax, seed, parent):
    """Wind-shader blades in three sizes (short, medium, tall in clumps), each blade its own shade.
    Vertex colour: dark root to the blade's tip colour (rgb) and the sway weight (alpha, 0 at the root)."""
    rng = random.Random(seed); bm = bmesh.new(); col = bm.loops.layers.color.new('Col'); uv = bm.loops.layers.uv.new('UVMap')
    made = 0
    while made < count:
        r = math.sqrt(rng.uniform(rmin * rmin, rmax * rmax)); a = rng.uniform(0, TAU)
        x, y = r * math.cos(a), r * math.sin(a)
        near = min(math.hypot(x - cx, y - cy) for cx, cy in TALL_CLUMPS)
        roll = rng.random()
        if near < .16 and roll < .8: hh = rng.uniform(.3, .45)                  # tall clump
        elif roll < .5: hh = rng.uniform(.05, .12)                               # short
        elif roll < .85: hh = rng.uniform(.12, .24)                              # medium
        else: hh = rng.uniform(.24, .34)
        made += 1
        width = .012 + hh * .036 + rng.uniform(-.002, .003); lean = .25 + hh * .6
        face = rng.uniform(0, TAU); lx, ly = math.cos(face) * lean * rng.uniform(.3, 1), math.sin(face) * lean * rng.uniform(.3, 1)
        side = Vector((-ly, lx, 0)).normalized()
        tip = rng.choices([c for c, _ in TIPS], [w for _, w in TIPS])[0]
        tip = tuple(min(1, max(0, c + rng.uniform(-.06, .06))) for c in tip)
        root = (rng.uniform(.03, .07), rng.uniform(.24, .34), rng.uniform(.04, .09))
        rows = []
        for t in (0, .3, .62, 1):
            c = Vector((x + lx * hh * t * t, y + ly * hh * t * t, -.01 + hh * t * (1 - .25 * t * t)))
            w = width * (1 - t) ** .8
            rows.append((bm.verts.new(c - side * w), bm.verts.new(c + side * w), t))
        for (a0, b0, t0), (a1, b1, t1) in zip(rows[:-1], rows[1:]):
            f = bm.faces.new((a0, b0, b1, a1))
            for loop, t, u in zip(f.loops, (t0, t0, t1, t1), (0, 1, 1, 0)):
                k = t ** .9
                loop[col] = (root[0] + (tip[0] - root[0]) * k, root[1] + (tip[1] - root[1]) * k, root[2] + (tip[2] - root[2]) * k, t ** 1.5)
                loop[uv].uv = (u, t)
    return make(name, bm, 'GrassBlade', parent=parent)


def rosette(parent, rng):
    """Twelve toothed leaves in three sizes, the crown, the closed bud, the yellow bloom and pebbles."""
    g = Geo(); g.region = LEAF_UV
    sizes = [('long', 4, (.8, .9), .115, 5, 0., 1.), ('medium', 4, (.55, .66), .09, 4, .006, 1.25), ('young', 4, (.28, .4), .062, 3, .012, 1.9)]
    offsets = {'long': .25, 'medium': 1.05, 'young': .62}
    for kind, count, (lmin, lmax), width, teeth, z0, lift in sizes:
        for i in range(count):
            yaw = offsets[kind] + i * TAU / count + rng.uniform(-.14, .14)
            leaf(g, yaw, rng.uniform(lmin, lmax), width * rng.uniform(.92, 1.08), rng, teeth=teeth, z0=z0,
                 mat='DGreen', rib='DGreen', nt=80, lift=lift)
    g.region = STEM_UV
    g.ball((0, 0, .045), .095, 'DGreen', scale=(1, 1, .45), seg=20, rings=10)
    bud(g); yellow_bloom(g, rng)
    g.region = None
    for k in range(10):
        r = rng.uniform(.72, .96); a = rng.uniform(0, TAU); s = rng.uniform(.045, .085)
        g.ball((r * math.cos(a), r * math.sin(a), s * .22), s, ('WhitePebble', 'WhitePebble', 'AquaPebble')[k % 3],
               scale=(1, rng.uniform(.7, 1), .5), rot=(0, 0, a), seg=16, rings=8)
    return g.done('Rosette', parent=parent)


def flower(name, base, height, lean, R, parent, rng):
    """One stalk (pivot at its base) and its clock (Seeds_<name>, origin at the head centre)."""
    base = Vector(base); top = Vector((base.x + lean[0], base.y + lean[1], height))
    mid = Vector(((base.x + top.x) / 2 + lean[0] * .2, (base.y + top.y) / 2 + lean[1] * .2, height * .55))
    g = Geo(); g.region = STEM_UV
    g.tube(bezier(base, mid, top - Vector((0, 0, R * .06)), 14), .0105, .0085, 'DGreen')
    g.ball(top, .17 * R, 'DGreen', seg=16, rings=10)                     # receptacle, painted like the stem
    bracts(g, top, R, ST, rng)
    stalk = g.done(name, base, parent, loc=tuple(base))
    sg = Geo(colors=SEED_COLORS, seed_data=True)
    clock(sg, top, R, ST, rng)
    sg.done('Seeds_' + name, top, stalk, loc=tuple(top - base))
    return stalk


# ---------------------------------------------------------------- build
top_coll = bpy.data.collections.new('SkyDandelions'); scene.collection.children.link(top_coll)
COLL = top_coll
root = bpy.data.objects.new('SkyDandelions', None); COLL.objects.link(root); ROOTOBJ = root
rng = random.Random(2026_11_15)
container = empty('SkyEssence', (0, 0, 0), root)
for name, base, height, lean, R in STALKS:
    flower(name, base, height, lean, R, container, rng)
for i, p in enumerate(SEEDS):
    g = Geo(colors=SEED_COLORS, seed_data=True)
    up = (Vector((.3, -.1, 1)) + Vector((rng.uniform(-.25, .25), rng.uniform(-.25, .25), 0))).normalized()
    parachute(g, p, up, ST, rng)
    g.done('Seed_%d' % i, p, container, loc=p)
rosette(root, rng)
painted_grass('Grass', 520, .5, 1.02, 23, root)
export(root, OUT + 'SkyDandelions.fbx')

# One loose seed for the particle systems, origin at the umbrella centre. Particles draw the raw mesh, and Unity's
# mesh space is Blender's (x, y, -z), so the pappus points along Blender +Y to come out pointing up (+Y) in Unity.
pcoll = bpy.data.collections.new('DandelionSeed'); scene.collection.children.link(pcoll); COLL = pcoll
proot = bpy.data.objects.new('DandelionSeed', None); COLL.objects.link(proot); ROOTOBJ = proot
g = Geo(colors=SEED_COLORS); parachute(g, (0, 0, 0), (0, 1, 0), ST, random.Random(7)); g.done('DandelionSeedMesh', parent=proot)
export(proot, OUT + 'DandelionSeed.fbx')
proot.location.x = 3

bpy.data.libraries.write(BLEND, {scene}, fake_user=True, compress=True)
faces = {o.name: len(o.data.polygons) for o in root.children_recursive if o.type == 'MESH'}
print('SkyDandelions faces', sum(faces.values()), faces)
print('UV maps on Seeds_Bloom:', [l.name for l in bpy.data.objects['Seeds_Bloom'].data.uv_layers],
      'colours:', [a.name for a in bpy.data.objects['Seeds_Bloom'].data.color_attributes])

if REVIEW:                                                   # a quick Blender still of the exported model
    SHOT = ROOTP + '/_Screenshots-and-previews-to-delete/SkyDandelions/Blender-Review.png'
    import os; os.makedirs(os.path.dirname(SHOT), exist_ok=True)
    world = bpy.data.worlds.new('Review'); world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (.55, .75, 1, 1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value = .9
    scene.world = world
    sun = bpy.data.objects.new('Sun', bpy.data.lights.new('Sun', 'SUN')); scene.collection.objects.link(sun)
    sun.data.energy = 3.5; sun.rotation_euler = (math.radians(50), 0, math.radians(-35))
    cam = bpy.data.objects.new('Cam', bpy.data.cameras.new('Cam')); scene.collection.objects.link(cam); scene.camera = cam
    cam.data.lens = 50; target = Vector((0, 0, .5)); a, e = math.radians(-34), math.radians(18)
    cam.location = target + Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e))) * 3.6
    cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.engine = 'BLENDER_EEVEE'; scene.eevee.taa_render_samples = 48
    scene.render.resolution_x, scene.render.resolution_y = 1200, 1000
    scene.view_settings.view_transform = 'Standard'; scene.view_settings.exposure = -.3
    scene.render.filepath = SHOT; bpy.ops.render.render(write_still=True, scene=scene.name)
    print('Review', SHOT)
