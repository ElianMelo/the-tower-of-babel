"""Stone rework, iron stone, iron ingot and the iron smelter.

Runs headless (blender -b --factory-startup --python build_iron_workshop.py) or through the Blender MCP socket.
Exports to Assets/_Project/Models/Resources and saves ArtSource/Resources/Iron_Workshop.blend.

  StoneCluster      - reworked stone node: chiselled boulders with flat facets, moss on the top faces, quartz shards
  IronStoneCluster  - rust-red boulders studded with dark metallic ore nodules and rust streaks
  IronIngot         - a bevelled trapezoid bar with an azure stamp
  IronSmelter       - processor family member (smelt): a pearl furnace with a sight glass that fills with molten
                      iron, glowing heat coils, a side cooling fan, a temperature dial, and a stamp press over the
                      casting mould on the tray

Metres, Z up, front faces -Y. Animated parts keep their own pivots; fill parts pivot at their base.
"""
import bpy, bmesh, math, os, random
from mathutils import Vector

ROOT = 'D:/PersonalProjects/the-tower-of-babel'
OUT = ROOT + '/Assets/_Project/Models/Resources'
os.makedirs(OUT, exist_ok=True)

if bpy.context.window is not None:
    old = bpy.data.scenes.get('Aero_IronWorkshop')
    if old:
        for o in list(old.objects): bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.scenes.remove(old)
    scene = bpy.data.scenes.new('Aero_IronWorkshop')
    bpy.context.window.scene = scene
else:
    scene = bpy.context.scene
    for o in list(scene.objects): bpy.data.objects.remove(o, do_unlink=True)
    scene.name = 'Aero_IronWorkshop'

# Names match the Unity materials the importer remaps to; colours are only for the Blender preview.
PALETTE = {
    'Workshop_Pearl': ((.84, .93, .98, 1), .18, .26, 0), 'Workshop_Azure': ((.025, .3, .66, 1), .45, .25, 0),
    'Workshop_Steel': ((.12, .19, .23, 1), .7, .32, 0), 'Workshop_ClearGlass': ((.9, .97, 1, 1), 0, .02, 0),
    'Workshop_Heat': ((1, .24, .025, 1), .1, .3, 1.5), 'Workshop_Moss': ((.36, .6, .46, 1), 0, .7, 0),
    'Workshop_IronOre': ((.16, .2, .26, 1), .85, .25, 0), 'Workshop_Rust': ((.62, .27, .1, 1), .1, .75, 0),
    'Workshop_Ingot': ((.62, .7, .78, 1), .92, .2, 0), 'Workshop_Molten': ((1, .5, .08, 1), 0, .3, 3),
    'Resource_Stone': ((.6, .63, .66, 1), 0, .8, 0), 'Resource_IronRock': ((.46, .3, .25, 1), 0, .8, 0),
}
M = {}
for name, (color, metal, rough, emit) in PALETTE.items():
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name); m.use_nodes = True
    n = next(x for x in m.node_tree.nodes if x.type == 'BSDF_PRINCIPLED')
    n.inputs['Base Color'].default_value = color; n.inputs['Metallic'].default_value = metal; n.inputs['Roughness'].default_value = rough
    if emit: n.inputs['Emission Color'].default_value = color; n.inputs['Emission Strength'].default_value = emit
    m.diffuse_color = color
    M[name.split('_', 1)[1] if name.startswith('Workshop_') else name] = m


def link(o):
    if o.name not in scene.collection.objects: scene.collection.objects.link(o)
    return o


def select(o):
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o


def finish(o, name, mat, smooth=True, rot=True):
    o.name = name
    if mat: o.data.materials.append(M[mat])
    select(o); bpy.ops.object.transform_apply(location=False, rotation=rot, scale=True)
    if o.type == 'MESH':
        for p in o.data.polygons: p.use_smooth = smooth and (len(p.vertices) == 4 or smooth == 'all')
    return o


def bevel(o, width, segments=2):
    select(o)
    m = o.modifiers.new('Bevel', 'BEVEL'); m.width = width; m.segments = segments; bpy.ops.object.modifier_apply(modifier=m.name)
    m = o.modifiers.new('Weighted', 'WEIGHTED_NORMAL'); m.keep_sharp = True; bpy.ops.object.modifier_apply(modifier=m.name)
    return o


def box(name, loc, size, mat, bev=.03, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot); o = bpy.context.object; o.scale = size
    finish(o, name, mat, smooth=False)
    return bevel(o, bev) if bev else o


def cyl(name, loc, r, depth, mat, verts=32, top=None, rot=(0, 0, 0)):
    if top is None: bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth, location=loc, rotation=rot)
    else: bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r, radius2=top, depth=depth, location=loc, rotation=rot)
    return finish(bpy.context.object, name, mat)


def sphere(name, loc, r, mat, seg=24, rings=14):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, radius=r, location=loc)
    return finish(bpy.context.object, name, mat, smooth='all')


def torus(name, loc, major, minor, mat, rot=(0, 0, 0), seg=36):
    bpy.ops.mesh.primitive_torus_add(major_segments=seg, minor_segments=8, location=loc, rotation=rot, major_radius=major, minor_radius=minor)
    return finish(bpy.context.object, name, mat, smooth='all')


def join(objs, name):
    objs = [o for o in objs if o is not None]
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1: bpy.ops.object.join()
    objs[0].name = name; objs[0].data.name = name
    return objs[0]


def pivot(o, point):
    scene.cursor.location = point; select(o); bpy.ops.object.origin_set(type='ORIGIN_CURSOR'); scene.cursor.location = (0, 0, 0)
    return o


def parent(o, root):
    bpy.context.view_layer.update(); w = o.matrix_world.copy(); o.parent = root; o.matrix_world = w
    return o


def empty(name, root=None, loc=(0, 0, 0)):
    o = link(bpy.data.objects.new(name, None)); o.location = loc
    if root: o.parent = root
    return o


# ---------------------------------------------------------------- rocks
def box_uv(bm, scale=1.6):
    """Box projection by dominant face normal: the stone texture tiles evenly over flat facets."""
    uv = bm.loops.layers.uv.verify()
    for f in bm.faces:
        n = f.normal; ax = max(range(3), key=lambda i: abs(n[i]))
        for loop in f.loops:
            c = loop.vert.co
            loop[uv].uv = ((c.y, c.z) if ax == 0 else (c.x, c.z) if ax == 1 else (c.x, c.y))
            loop[uv].uv = (loop[uv].uv[0] * scale, loop[uv].uv[1] * scale)


def chisel(bm, size, rng, cuts, floor):
    """Slices the lumpy ellipsoid with random planes, leaving large flat facets like split stone."""
    a, b, c = size
    for _ in range(cuts):
        d = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-.25, 1))).normalized()
        support = math.sqrt((a * d.x) ** 2 + (b * d.y) ** 2 + (c * d.z) ** 2)
        res = bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=d * support * rng.uniform(.74, .9),
                                     plane_no=d, clear_outer=True)
        edges = [e for e in res['geom_cut'] if isinstance(e, bmesh.types.BMEdge)]
        if edges: bmesh.ops.holes_fill(bm, edges=bm.edges[:], sides=0)
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, 0, -c * floor), plane_no=(0, 0, -1), clear_outer=True)
    bmesh.ops.holes_fill(bm, edges=bm.edges[:], sides=0)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])


def rock(name, loc, size, seed, mat, cuts=7, floor=.55, rust=None):
    """A chiselled boulder. With rust=(material, share) a share of its facets becomes rust streaks."""
    rng = random.Random(seed)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=3, radius=1)
    for v in bm.verts:
        n = v.co.normalized(); wobble = 1 + .07 * math.sin(n.x * 5.1 + seed) * math.cos(n.y * 4.3 - seed * .7) + rng.uniform(-.035, .035)
        v.co = Vector((n.x * size[0], n.y * size[1], n.z * size[2])) * wobble
    chisel(bm, size, rng, cuts, floor)
    box_uv(bm)
    mesh = bpy.data.meshes.new(name); bm.to_mesh(mesh); bm.free()
    o = link(bpy.data.objects.new(name, mesh)); o.location = loc
    o.data.materials.append(M[mat])
    if rust:
        o.data.materials.append(M[rust[0]])
        for p in o.data.polygons:
            c = p.center
            # Rust runs down from the ore in thin bands; the big split facets stay clean stone.
            streak = math.sin(c.x * 4.2 + c.z * 1.5 + seed) * .7 + math.sin(c.y * 5.3 - seed * .3) * .3
            if streak > 1 - rust[1] * 1.6 and p.area < .012 and p.normal.z > -.3: p.material_index = 1
    for p in o.data.polygons: p.use_smooth = False
    return bevel(o, .012, 1)


def top_cap(source, name, mat, lift=.012, threshold=.72):
    """Moss on the upward facets of a boulder: those faces copied a little above the stone and shrunk inward."""
    bm = bmesh.new(); bm.from_mesh(source.data)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.normal.z < threshold], context='FACES')
    for f in bm.faces:
        centre = f.calc_center_median()
        for v in f.verts: v.co = centre + (v.co - centre) * .86 + Vector((0, 0, lift))
    if not bm.faces: bm.free(); return None
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=.0005)
    box_uv(bm)
    mesh = bpy.data.meshes.new(name); bm.to_mesh(mesh); bm.free()
    o = link(bpy.data.objects.new(name, mesh)); o.matrix_world = source.matrix_world.copy()
    o.data.materials.clear(); o.data.materials.append(M[mat])
    select(o); m = o.modifiers.new('Solid', 'SOLIDIFY'); m.thickness = .012; bpy.ops.object.modifier_apply(modifier=m.name)
    return o


def shard(name, loc, r, length, tilt, mat):
    """A six-sided crystal: a prism with a pointed tip."""
    body = cyl(name, (0, 0, length * .4), r, length * .8, mat, 6)
    tip = cyl(name + 'Tip', (0, 0, length * .9), r, length * .2, mat, 6, top=0)
    o = join([body, tip], name)
    o.rotation_euler = tilt; o.location = loc
    select(o); bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    for p in o.data.polygons: p.use_smooth = False
    return o


def nodule(name, loc, r, seed, mat):
    rng = random.Random(seed)
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=r, location=loc)
    o = bpy.context.object
    for v in o.data.vertices: v.co *= rng.uniform(.8, 1.18)
    return finish(o, name, mat, smooth=False)


def stone_cluster():
    """Reworked stone: three split boulders with flat facets, a few pebbles, moss on the tops and quartz shards."""
    main = rock('Boulder', (0, .04, .3), (.56, .48, .52), 11, 'Resource_Stone', cuts=9, floor=.6)
    left = rock('BoulderLeft', (-.48, -.16, .17), (.34, .3, .3), 23, 'Resource_Stone', cuts=6, floor=.55)
    right = rock('BoulderRight', (.44, -.2, .14), (.3, .27, .26), 37, 'Resource_Stone', cuts=6, floor=.55)
    pebbles = [rock('Pebble', (x, y, r * .5), (r, r * .9, r * .7), 50 + i, 'Resource_Stone', cuts=3, floor=.5)
               for i, (x, y, r) in enumerate([(.2, -.5, .09), (-.18, -.48, .07), (.62, .2, .08), (-.6, .26, .06)])]
    moss = [top_cap(o, o.name + 'Moss', 'Moss') for o in (main, left, right)]
    quartz = [shard('Quartz', (x, y, z), r, l, t, 'Pearl') for x, y, z, r, l, t in
              [(-.24, -.3, .3, .035, .2, (.5, -.3, 0)), (-.17, -.33, .27, .025, .14, (.2, .5, 0)), (.3, -.28, .27, .028, .16, (-.3, .6, 0))]]
    return pivot(join([main, left, right] + pebbles + moss + quartz, 'StoneRock'), (0, 0, 0))


def iron_stone_cluster():
    """Iron stone: rust-red boulders with dark metallic ore nodules pushing out of them and rust streaks."""
    main = rock('IronBoulder', (0, .02, .3), (.54, .5, .5), 71, 'Resource_IronRock', cuts=10, floor=.6, rust=('Rust', .22))
    left = rock('IronBoulderLeft', (-.46, -.18, .16), (.32, .3, .28), 83, 'Resource_IronRock', cuts=6, floor=.55, rust=('Rust', .2))
    right = rock('IronBoulderRight', (.46, -.14, .18), (.3, .28, .3), 97, 'Resource_IronRock', cuts=7, floor=.55, rust=('Rust', .2))
    ore = [nodule('Ore', loc, r, 120 + i, 'IronOre') for i, (loc, r) in enumerate([
        ((-.22, -.36, .42), .1), ((.12, -.4, .5), .08), ((.3, -.18, .66), .09), ((-.1, -.2, .74), .07), ((.0, .34, .62), .09),
        ((-.52, -.36, .2), .07), ((-.38, -.06, .34), .06), ((.52, -.34, .22), .07), ((.6, -.02, .3), .06), ((.22, .3, .24), .06)])]
    pebbles = [nodule('OrePebble', (x, y, .04), .05, 140 + i, 'IronOre') for i, (x, y) in enumerate([(.2, -.56), (-.24, -.52), (.66, .2)])]
    return pivot(join([main, left, right] + ore + pebbles, 'IronRock'), (0, 0, 0))


# ---------------------------------------------------------------- ingot
def ingot_mesh(name, loc=(0, 0, 0), scale=1.0):
    """Trapezoid bar, wider at the base, with soft bevels."""
    bottom, top, h = (.42 * scale, .2 * scale), (.34 * scale, .13 * scale), .1 * scale
    verts = [(-bottom[0] / 2, -bottom[1] / 2, 0), (bottom[0] / 2, -bottom[1] / 2, 0), (bottom[0] / 2, bottom[1] / 2, 0), (-bottom[0] / 2, bottom[1] / 2, 0),
             (-top[0] / 2, -top[1] / 2, h), (top[0] / 2, -top[1] / 2, h), (top[0] / 2, top[1] / 2, h), (-top[0] / 2, top[1] / 2, h)]
    faces = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    mesh = bpy.data.meshes.new(name); mesh.from_pydata([Vector(v) + Vector(loc) for v in verts], [], faces); mesh.update()
    o = link(bpy.data.objects.new(name, mesh)); o.data.materials.append(M['Ingot'])
    return bevel(o, .012 * scale, 2)


def iron_ingot():
    root = empty('IronIngot')
    bar = ingot_mesh('Ingot', (0, 0, -.05))
    stamp = box('Stamp', (0, 0, .052), (.09, .06, .008), 'Azure', .003, rot=(0, 0, math.pi / 4))
    parent(join([bar, stamp], 'Ingot'), root)
    return root


# ---------------------------------------------------------------- smelter
def foundry_base(root):
    """Iron: a cast-iron plinth with azure trim, a glowing runner, and a casting mould on a steel table."""
    s = [cyl('Plinth', (0, 0, .09), .95, .18, 'Steel', 40),
         cyl('Deck', (0, 0, .33), .84, .3, 'IronOre', 40),
         cyl('DeckTop', (0, 0, .53), .8, .06, 'Azure', 40)]
    for i in range(8):
        a = i * math.tau / 8 + math.tau / 16
        s.append(cyl('FootBolt', (math.cos(a) * .88, math.sin(a) * .88, .2), .045, .05, 'Pearl', 12))
    # The runner carries the melt from the furnace tap down to the mould.
    tilt = (math.radians(-14), 0, 0)
    s += [box('Runner', (0, -.8, .47), (.14, .44, .05), 'IronOre', .01, rot=tilt),
          box('RunnerGlow', (0, -.8, .497), (.06, .42, .012), 'Molten', .003, rot=tilt),
          box('CastingTable', (0, -1.0, .35), (.72, .56, .06), 'Steel', .015),
          box('CastingStand', (0, -.95, .16), (.5, .32, .3), 'Steel', .03)]
    for x, y, w, d in [(0, -1.16, .5, .04), (0, -.84, .5, .04), (-.23, -1.0, .04, .32), (.23, -1.0, .04, .32)]:
        s.append(box('MouldWall', (x, y, .41), (w, d, .06), 'IronOre', .006))
    lights = [(.3, -.78, .44), (.43, -.71, .44), (.55, -.62, .44)]
    for i, loc in enumerate(lights):
        parent(sphere('StatusLight_' + str(i), loc, .045, 'Molten', 14, 9), root)
    empty('BlockSocket', root, (0, -1.0, .43))  # 3 cm under the press's socket, so each machine's output has its own spot
    return s


def smelter(root):
    s = [cyl('FurnaceShell', (0, 0, 1.11), .5, 1.1, 'Pearl', 40),
         sphere('FurnaceDome', (0, 0, 1.66), .5, 'Pearl', 40, 20),
         torus('BandLow', (0, 0, .62), .5, .04, 'Azure'), torus('BandHigh', (0, 0, 1.66), .5, .04, 'Azure'),
         # Sight glass: a steel housing standing out of the shell, a dark crucible plate, clear glass in a frame.
         box('SightHousing', (0, -.5, 1.1), (.36, .1, .94), 'Steel', .02),
         box('Crucible', (0, -.556, 1.1), (.24, .01, .8), 'IronOre', 0),
         box('SightGlass', (0, -.582, 1.1), (.26, .012, .84), 'ClearGlass', .004),
         cyl('Hopper', (0, 0, 2.26), .12, .3, 'Steel', 24, top=.3),
         torus('HopperLip', (0, 0, 2.41), .3, .03, 'Azure'),
         cyl('Chimney', (-.28, .3, 2.2), .09, .72, 'Steel', 20),
         cyl('ChimneyCap', (-.28, .3, 2.58), .14, .05, 'Azure', 24),
         torus('FanHousing', (.64, 0, 1.05), .26, .035, 'Azure', rot=(0, math.pi / 2, 0)),
         box('FanMount', (.56, 0, 1.05), (.14, .12, .1), 'Steel', .01),
         cyl('Dial', (-.38, -.42, 1.4), .12, .04, 'Pearl', 28, rot=(math.pi / 2, 0, 0)),
         torus('DialRim', (-.38, -.44, 1.4), .12, .016, 'Azure', rot=(math.pi / 2, 0, 0)),
         box('DialStem', (-.36, -.36, 1.4), (.06, .1, .06), 'Steel', .01),
         box('Tap', (0, -.6, .66), (.12, .12, .08), 'Steel', .015)]
    for z in (.67, 1.53):
        s.append(box('SightFrame', (0, -.588, z), (.32, .02, .04), 'Azure', .005))
    for x in (-.15, .15):
        s.append(box('SightFrame', (x, -.588, 1.1), (.03, .02, .9), 'Azure', .005))
    # Stamp gantry over the casting mould.
    for x in (-.36, .36):
        s.append(cyl('GantryPost', (x, -1.0, .75), .035, .78, 'Steel', 12))
    s.append(box('GantryBeam', (0, -1.0, 1.16), (.84, .1, .1), 'Azure', .025))

    coils = [torus('HeatCoil', (0, 0, z), .53, .022, 'Heat', seg=40) for z in (.86, 1.1, 1.34)]
    parent(join(coils, 'HeatCoil'), root)
    melt = box('MeltColumn', (0, -.568, 1.1), (.2, .012, .76), 'Molten', .002)
    parent(pivot(melt, (0, -.568, .72)), root)
    mould = box('MouldFill', (0, -1.0, .415), (.4, .26, .03), 'Molten', .004)
    parent(pivot(mould, (0, -1.0, .4)), root)
    fan = [cyl('FanHub', (.64, 0, 1.05), .06, .08, 'Steel', 16, rot=(0, math.pi / 2, 0))]
    for i in range(5):
        a = i * math.tau / 5
        fan.append(box('Blade', (.64, math.cos(a) * .12, 1.05 + math.sin(a) * .12), (.02, .2, .07), 'Pearl', .006, rot=(a + math.pi / 2, math.radians(20), 0)))
    parent(pivot(join(fan, 'Fan'), (.64, 0, 1.05)), root)
    needle = box('Needle', (-.38, -.465, 1.445), (.02, .01, .09), 'Molten', 0)
    parent(pivot(needle, (-.38, -.465, 1.4)), root)
    stamp = join([cyl('StampRod', (0, -1.0, .98), .03, .32, 'Steel', 12), box('StampHead', (0, -1.0, .8), (.3, .2, .08), 'IronOre', .015),
                  box('StampFace', (0, -1.0, .755), (.26, .16, .01), 'Ingot', .003)], 'Stamp')
    parent(pivot(stamp, (0, -1.0, .8)), root)
    rng = random.Random(5)
    chunk = nodule('Feed', (0, 0, 2.32), .13, 5, 'Resource_IronRock')
    bits = [nodule('FeedOre', (rng.uniform(-.06, .06), rng.uniform(-.06, .06), 2.32 + rng.uniform(.03, .08)), .04, 6 + i, 'IronOre') for i in range(3)]
    parent(pivot(join([chunk] + bits, 'Feed'), (0, 0, 2.32)), root)
    return s


def machine(name, base, build):
    root = empty(name)
    parent(join(base(root), 'Base'), root)
    parent(join(build(root), 'MachineBody'), root)
    return root


stone = empty('StoneCluster'); parent(stone_cluster(), stone)
iron_stone = empty('IronStoneCluster'); parent(iron_stone_cluster(), iron_stone)
ingot = iron_ingot()
furnace = machine('IronSmelter', foundry_base, smelter)

exports = [stone, iron_stone, ingot, furnace]
# The rock nodes reuse only the mesh, so their FBX holds just the mesh with Y-up vertices (space transform baked).
# The ingot and smelter are instanced whole; their empties keep the axis conversion, so nested pivots stay correct.
single_mesh = {stone, iron_stone}
for root in exports:
    bpy.ops.object.select_all(action='DESELECT')
    if root not in single_mesh: root.select_set(True)
    for c in root.children_recursive: c.select_set(True)
    bpy.ops.export_scene.fbx(filepath=OUT + '/' + root.name + '.fbx', use_selection=True,
                             object_types={'MESH'} if root in single_mesh else {'MESH', 'EMPTY'},
                             bake_space_transform=root in single_mesh, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', bake_anim=False, add_leaf_bones=False, mesh_smooth_type='FACE')
for i, root in enumerate(exports): root.location.x = (i - len(exports) / 2) * 2.6
bpy.ops.wm.save_as_mainfile(filepath=ROOT + '/ArtSource/Resources/Iron_Workshop.blend')
print('IRON_WORKSHOP', {r.name: sum(len(o.data.polygons) for o in r.children_recursive if o.type == 'MESH') for r in exports})
