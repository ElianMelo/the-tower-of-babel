"""Processor family: five distinct machines on one shared base. Run inside the connected Blender (MCP socket).

Every machine has three status lights, an output point (BlockSocket) and a top at about 0.56 m, but
its base is built from its own material and output: a quarry block with a chute, a brick hearth with a
cooling rack, a riverbed with a flume, a hydraulic platform with a catch basin, and a cloud cushion with a
hovering plate. On top, each has its own silhouette and process:
  StoneProcessor (crush)  - gantry hammer over a hopper, toothed rollers behind a glass window
  GlassFurnace   (melt)   - domed kiln with an amber porthole, chimney, bellows and lid
  RiverMill      (mill)   - side water wheel fed by a glass sluice, gears, and a mould that fills
  WaterPress     (press)  - tall two-pillar hydraulic press, glass cylinder, piston and gauge
  SkySpinner     (spin)   - low drum with a glass dome, a three-arm rotor and a top fan
Metres, Z up, front faces -Y. Exported without baked space transforms (nested empties stay correct).
Animated parts keep their own pivots; fill parts pivot at their base.
"""
import bpy, math, os
from mathutils import Vector

ROOT = 'D:/PersonalProjects/the-tower-of-babel'
OUT = ROOT + '/Assets/_Project/Models/Resources'
os.makedirs(OUT, exist_ok=True)
old = bpy.data.scenes.get('Aero_ProcessorFamily')
if old:
    for o in list(old.objects): bpy.data.objects.remove(o, do_unlink=True)
    bpy.data.scenes.remove(old)
for coll in (bpy.data.meshes, bpy.data.curves):
    for item in list(coll):
        if item.users == 0: coll.remove(item)
scene = bpy.data.scenes.new('Aero_ProcessorFamily')
bpy.context.window.scene = scene

PALETTE = {
    'Workshop_Pearl': ((.84, .93, .98, 1), .18, .26, 0), 'Workshop_Azure': ((.025, .3, .66, 1), .45, .25, 0),
    'Workshop_Steel': ((.12, .19, .23, 1), .7, .32, 0), 'Workshop_Glass': ((.14, .76, .89, 1), .25, .12, 0),
    'Workshop_Heat': ((1, .24, .025, 1), .1, .3, 1.5), 'Workshop_Sand': ((.76, .51, .19, 1), 0, .85, 0),
    'Workshop_RiverGlass': ((.1, .74, .62, 1), .1, .05, 0), 'Workshop_RiverGlow': ((.35, 1, .78, 1), 0, .2, 2),
    'Workshop_WaterGlass': ((.18, .55, .98, 1), .1, .04, 0), 'Workshop_WaterFoam': ((.9, .97, 1, 1), 0, .1, 0),
    'Workshop_SkyGlass': ((.64, .83, 1, 1), .05, .05, 0), 'Workshop_SkyGlow': ((.72, .78, 1, 1), 0, .2, 2),
    'Workshop_Cloud': ((.97, .98, 1, 1), 0, .55, 0),
    'Workshop_RiverStone': ((.58, .64, .68, 1), 0, .45, 0), 'Workshop_ClearGlass': ((.9, .97, 1, 1), 0, .02, 0),
    'Workshop_Brick': ((.78, .44, .3, 1), 0, .8, 0),
    'Resource_Stone': ((.6, .63, .66, 1), 0, .8, 0), 'Resource_Refined': ((.82, .86, .9, 1), .05, .45, 0),
}
M = {}
for name, (color, metal, rough, emit) in PALETTE.items():
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name); m.use_nodes = True
    n = next(x for x in m.node_tree.nodes if x.type == 'BSDF_PRINCIPLED')
    n.inputs['Base Color'].default_value = color; n.inputs['Metallic'].default_value = metal; n.inputs['Roughness'].default_value = rough
    if emit: n.inputs['Emission Color'].default_value = color; n.inputs['Emission Strength'].default_value = emit
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
    m = o.modifiers.new('Weighted', 'WEIGHTED_NORMAL'); bpy.ops.object.modifier_apply(modifier=m.name)
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
    bpy.context.view_layer.objects.active = objs[0]; bpy.ops.object.join(); objs[0].name = name
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


def puff(name, loc, r):
    parts = [sphere(name + str(i), (loc[0] + dx * r, loc[1] + dy * r, loc[2] + dz * r), s * r, 'Cloud', 14, 9)
             for i, (dx, dy, dz, s) in enumerate([(0, 0, 0, 1), (.7, .1, -.15, .72), (-.68, -.05, -.18, .7), (.2, .5, -.1, .62), (.05, 0, .45, .66)])]
    return join(parts, name)


def gear(name, loc, r, teeth, mat='Azure'):
    """Gear whose axle runs along X."""
    parts = [cyl(name, loc, r, .06, mat, 28, rot=(0, math.pi / 2, 0)), cyl(name + 'Hub', loc, r * .35, .1, 'Steel', 16, rot=(0, math.pi / 2, 0))]
    for i in range(teeth):
        a = i * 2 * math.pi / teeth
        parts.append(box(name + 'T', (loc[0], loc[1] + math.cos(a) * (r + .025), loc[2] + math.sin(a) * (r + .025)), (.06, .05, .05), mat, .008, rot=(a, 0, 0)))
    return pivot(join(parts, name), loc)


def lights(root, mat, points, size=(.1, .04, .07), sphere_radius=0):
    for i, loc in enumerate(points):
        o = sphere('StatusLight_' + str(i), loc, sphere_radius, mat, 14, 9) if sphere_radius else box('StatusLight_' + str(i), loc, size, mat, .015)
        parent(o, root)


def ring_of(count, radius, z, make):
    return [make(i, i * 2 * math.pi / count, math.cos(i * 2 * math.pi / count) * radius, math.sin(i * 2 * math.pi / count) * radius, z) for i in range(count)]


def quarry_base(root):
    """Stone: rough quarry block, steel deck, rubble, and a chute that slides the brick out."""
    s = [cyl('QuarryBlock', (0, 0, .12), .95, .24, 'Resource_Stone', 8, rot=(0, 0, math.pi / 8)),
         box('Deck', (0, 0, .4), (1.3, 1.3, .32), 'Steel', .05),
         box('DeckTop', (0, 0, .57), (1.16, 1.16, .03), 'Azure', .01)]
    for x in (-.6, .6):
        for y in (-.6, .6): s.append(box('Bumper', (x, y, .42), (.16, .16, .36), 'Azure', .04))
    for i, (x, y, r) in enumerate([(.86, .55, .11), (-.82, .62, .09), (.95, -.25, .08), (-.92, -.4, .1), (.4, .9, .07)]):
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=r, location=(x, y, r * .6))
        s.append(finish(bpy.context.object, 'Rubble', 'Resource_Stone', smooth=False))
    chute = box('Chute', (0, -.95, .38), (.5, .62, .04), 'Pearl', .01, rot=(math.radians(-18), 0, 0))
    s += [chute, box('ChuteRail', (-.27, -.95, .43), (.04, .64, .1), 'Azure', .01, rot=(math.radians(-18), 0, 0)),
          box('ChuteRail', (.27, -.95, .43), (.04, .64, .1), 'Azure', .01, rot=(math.radians(-18), 0, 0)),
          box('ChuteFoot', (0, -1.22, .14), (.44, .1, .26), 'Steel', .02)]
    lights(root, 'Glass', [(x, -.66, .44) for x in (-.4, -.25, -.1)], sphere_radius=.045)
    empty('BlockSocket', root, (0, -1.05, .48))
    return s


def hearth_base(root):
    """Sand to glass: a round brick hearth with glowing vents, and a cooling rack for the slab."""
    s = [cyl('HearthFoot', (0, 0, .06), .98, .12, 'Steel', 40),
         cyl('HearthCap', (0, 0, .53), .86, .06, 'Pearl', 40)]
    s += ring_of(14, .82, .3, lambda i, a, x, y, z: box('Brick', (x, y, .3), (.34, .18, .2 if i % 2 else .32), 'Brick', .02, rot=(0, 0, a + math.pi / 2)))
    s += ring_of(14, .8, .2, lambda i, a, x, y, z: box('BrickLow', (x, y, .17), (.34, .18, .1), 'Brick', .015, rot=(0, 0, a + math.pi / 2 + .22)))
    s.append(cyl('HearthCore', (0, 0, .3), .72, .5, 'Steel', 32))
    rack = [box('RackFrame', (0, -1.0, .4), (.66, .5, .04), 'Steel', .01)]
    for x in (-.24, -.12, 0, .12, .24): rack.append(box('RackBar', (x, -1.0, .43), (.03, .48, .03), 'Pearl', .006))
    for x in (-.3, .3):
        for y in (-.8, -1.2): rack.append(cyl('RackLeg', (x, y, .2), .025, .4, 'Steel', 8))
    s += rack
    lights(root, 'Heat', [(x, -.74, .3) for x in (-.18, 0, .18)], (.1, .05, .05))
    empty('BlockSocket', root, (0, -1.0, .5))
    return s


def riverbed_base(root):
    """River essence: a ring of river stones holding a shallow stream, a pearl island, and a flume out."""
    s = [cyl('BedFloor', (0, 0, .05), 1.0, .1, 'RiverStone', 36),
         cyl('Stream', (0, 0, .14), .92, .04, 'RiverGlass', 36),
         cyl('Island', (0, 0, .35), .7, .42, 'Pearl', 36, top=.64)]
    s += ring_of(16, .96, .14, lambda i, a, x, y, z: finish(
        (bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=.1 + .03 * (i % 3), location=(x, y, .12)), bpy.context.object)[1],
        'RimStone', 'RiverStone'))
    s += [box('Flume', (0, -.98, .3), (.34, .66, .06), 'Pearl', .015, rot=(math.radians(-12), 0, 0)),
          box('FlumeWall', (-.17, -.98, .35), (.03, .66, .1), 'Azure', .008, rot=(math.radians(-12), 0, 0)),
          box('FlumeWall', (.17, -.98, .35), (.03, .66, .1), 'Azure', .008, rot=(math.radians(-12), 0, 0)),
          box('FlumeWater', (0, -.98, .335), (.3, .64, .015), 'RiverGlow', .004, rot=(math.radians(-12), 0, 0))]
    lights(root, 'RiverGlow', [(x, -.62, .42) for x in (-.18, 0, .18)], sphere_radius=.04)
    empty('BlockSocket', root, (0, -1.08, .44))
    return s


def platform_base(root):
    """Water essence: a heavy hydraulic platform with bolts and a drain, and a glass catch basin."""
    s = [box('Footing', (0, 0, .15), (1.5, 1.5, .3), 'Steel', .06),
         box('Platform', (0, 0, .43), (1.3, 1.3, .26), 'Azure', .05),
         box('Drain', (0, -.4, .565), (.5, .16, .01), 'Steel', .003)]
    for x in (-.55, .55):
        for y in (-.55, .55): s.append(cyl('Bolt', (x, y, .58), .05, .04, 'Pearl', 12))
    for x in (-.18, -.06, .06, .18): s.append(box('DrainSlot', (x, -.4, .572), (.04, .12, .01), 'WaterGlass', 0))
    s += [box('BasinFloor', (0, -1.0, .3), (.62, .52, .04), 'WaterFoam', .01),
          box('BasinWall', (0, -1.25, .38), (.62, .03, .16), 'ClearGlass', .006), box('BasinWall', (0, -.75, .38), (.62, .03, .16), 'ClearGlass', .006),
          box('BasinWall', (-.3, -1.0, .38), (.03, .52, .16), 'ClearGlass', .006), box('BasinWall', (.3, -1.0, .38), (.03, .52, .16), 'ClearGlass', .006),
          box('BasinWater', (0, -1.0, .34), (.58, .48, .05), 'WaterGlass', .01),
          box('BasinStand', (0, -.9, .14), (.5, .3, .28), 'Steel', .03)]
    lights(root, 'WaterFoam', [(x, -.655, .43) for x in (-.18, 0, .18)], (.05, .03, .16))
    empty('BlockSocket', root, (0, -1.0, .46))  # the block settles half into the basin water
    return s


def cushion_base(root):
    """Sky essence: a pearl disc resting on cloud puffs, a glow ring, and a hovering glass plate."""
    s = [cyl('Cushion', (0, 0, .48), .86, .16, 'Pearl', 40), torus('CushionGlow', (0, 0, .4), .86, .03, 'SkyGlow')]
    s += ring_of(7, .72, .2, lambda i, a, x, y, z: puff('BasePuff', (x, y, .22), .2 + .03 * (i % 2)))
    s.append(puff('CentrePuff', (0, 0, .2), .3))
    s += [cyl('Plate', (0, -.95, .44), .3, .03, 'SkyGlass', 32),
          torus('PlateGlow', (0, -.95, .44), .3, .015, 'SkyGlow')]
    lights(root, 'SkyGlow', [(x, -.78, .62 + .05 * (i % 2)) for i, x in enumerate((-.22, 0, .22))], sphere_radius=.04)
    empty('BlockSocket', root, (0, -.95, .56))
    return s


def machine(name, base, build):
    root = empty(name)
    parent(join(base(root), 'Base'), root)
    parent(join(build(root), 'MachineBody'), root)
    return root


# ---------------------------------------------------------------- Stone crusher (crush)
def crusher(root):
    s = [box('BackWall', (0, .34, .98), (1.1, .12, .84), 'Pearl', .04),
         box('SideWall', (-.55, 0, .98), (.12, .8, .84), 'Pearl', .04), box('SideWall', (.55, 0, .98), (.12, .8, .84), 'Pearl', .04),
         box('Window', (0, -.36, .98), (.98, .03, .7), 'Glass', .01),
         box('TopRim', (0, 0, 1.42), (1.16, .86, .08), 'Azure', .03),
         cyl('Hopper', (0, 0, 1.64), .46, .4, 'Steel', 4, top=.18, rot=(math.pi, 0, math.pi / 4)),
         box('HopperLip', (0, 0, 1.85), (.7, .7, .05), 'Azure', .02)]
    for x in (-.42, .42):
        s.append(cyl('GantryPost', (x, .32, 1.95), .045, 1.0, 'Steel', 12))
    s += [box('GantryBeam', (0, .32, 2.45), (1.0, .13, .13), 'Azure', .03),
          box('HammerArm', (0, .16, 2.45), (.1, .34, .1), 'Steel', .02),
          box('Anvil', (0, 0, 1.8), (.26, .26, .03), 'Steel', .01)]
    for i, y in enumerate((-.13, .14)):
        roller = [cyl('Roller', (0, y, .92), .13, .86, 'Steel', 24, rot=(0, math.pi / 2, 0))]
        for t in range(8):
            a = t * math.pi / 4
            roller.append(box('Tooth', (0, y + math.cos(a) * .135, .92 + math.sin(a) * .135), (.84, .035, .035), 'Pearl', .006, rot=(a, 0, 0)))
        for x in (-.44, .44): roller.append(cyl('Cap', (x, y, .92), .15, .04, 'Azure', 24, rot=(0, math.pi / 2, 0)))
        parent(pivot(join(roller, 'Roller_' + str(i)), (0, y, .92)), root)
    hammer = join([cyl('Shaft', (0, 0, 2.2), .04, .5, 'Steel', 12), box('HammerHead', (0, 0, 1.95), (.28, .28, .18), 'Azure', .04),
                   box('HammerFace', (0, 0, 1.85), (.22, .22, .03), 'Steel', .01)], 'Hammer')
    parent(pivot(hammer, (0, 0, 1.95)), root)
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=.15, location=(0, 0, 1.66))
    chunk = bpy.context.object
    for v in chunk.data.vertices: v.co *= 1 + .18 * math.sin(v.co.x * 31 + v.co.y * 17) * math.cos(v.co.z * 23)
    finish(chunk, 'Feed', 'Resource_Stone', smooth=False); parent(chunk, root)
    return s


# ---------------------------------------------------------------- Glass kiln (melt)
def kiln(root):
    s = [cyl('KilnBody', (0, 0, .82), .62, .52, 'Pearl', 40),
         sphere('Dome', (0, 0, 1.06), .62, 'Pearl', 40, 20),
         torus('DomeBand', (0, 0, 1.08), .62, .045, 'Azure'),
         torus('PortRim', (0, -.6, 1.0), .22, .05, 'Steel', rot=(math.pi / 2, 0, 0)),
         cyl('PortGlass', (0, -.64, 1.0), .2, .02, 'Glass', 28, rot=(math.pi / 2, 0, 0)),
         cyl('Chimney', (.32, .3, 1.9), .13, .85, 'Steel', 20),
         cyl('ChimneyCap', (.32, .3, 2.36), .2, .08, 'Azure', 24),
         torus('LidRing', (0, 0, 1.67), .24, .03, 'Steel')]
    for z in (1.7, 1.86, 2.02, 2.18): s.append(torus('ChimneyRib', (.32, .3, z), .14, .03, 'Pearl'))
    heat = cyl('HeatGlow', (0, -.59, 1.0), .19, .02, 'Heat', 28, rot=(math.pi / 2, 0, 0)); parent(heat, root)
    pool = box('MeltPool', (0, -.62, .86), (.26, .03, .12), 'Heat', .01); parent(pivot(pool, (0, -.62, .8)), root)
    bellows = [box('BellowsBody', (.8, 0, .95), (.34, .42, .34), 'Steel', .03)]
    for x in (.69, .78, .87, .96): bellows.append(box('Pleat', (x, 0, .95), (.03, .48, .4), 'Azure', .01))
    parent(pivot(join(bellows, 'Bellows'), (.62, 0, .95)), root)
    parent(cyl('Lid', (0, 0, 1.7), .22, .08, 'Azure', 28), root)
    feed = cyl('Feed', (0, 0, 1.8), .18, .12, 'Sand', 20, top=.03); parent(feed, root)
    return s


# ---------------------------------------------------------------- River mill (mill)
def mill(root):
    s = [box('MillHouse', (-.05, .05, .92), (.9, .8, .72), 'Pearl', .06),
         box('MillRoof', (-.05, .05, 1.32), (1.0, .9, .08), 'Azure', .03),
         box('Trough', (.22, .15, 1.62), (1.12, .2, .1), 'RiverGlass', .02),
         box('TroughWater', (.22, .15, 1.645), (1.06, .14, .03), 'RiverGlow', .008),
         cyl('TroughPost', (-.3, .15, 1.46), .03, .3, 'Steel', 10), cyl('TroughPost', (.62, .15, 1.46), .03, .3, 'Steel', 10),
         cyl('Funnel', (-.36, .15, 1.8), .08, .26, 'Steel', 20, top=.24),
         cyl('Axle', (.5, 0, 1.0), .05, .4, 'Steel', 12, rot=(0, math.pi / 2, 0)),
         box('Mould', (0, -.47, 1.0), (.42, .3, .38), 'Glass', .02)]
    wheel = [torus('Rim', (.72, 0, 1.0), .48, .035, 'Azure', rot=(0, math.pi / 2, 0)),
             torus('InnerRim', (.72, 0, 1.0), .34, .025, 'Pearl', rot=(0, math.pi / 2, 0)),
             cyl('Hub', (.72, 0, 1.0), .08, .18, 'Steel', 16, rot=(0, math.pi / 2, 0))]
    for i in range(10):
        a = i * 2 * math.pi / 10
        wheel.append(box('Paddle', (.72, math.cos(a) * .44, 1.0 + math.sin(a) * .44), (.16, .04, .15), 'Pearl', .01, rot=(a, 0, 0)))
    for i in range(5):
        a = i * 2 * math.pi / 5
        wheel.append(box('Spoke', (.72, math.cos(a) * .2, 1.0 + math.sin(a) * .2), (.03, .32, .03), 'Steel', .005, rot=(a + math.pi / 2, 0, 0)))
    parent(pivot(join(wheel, 'WaterWheel'), (.72, 0, 1.0)), root)
    parent(gear('Gear_0', (-.52, -.1, 1.0), .16, 12), root)
    parent(gear('Gear_1', (-.52, .2, 1.14), .11, 9, 'Pearl'), root)
    fill = box('MouldFill', (0, -.47, 1.0), (.36, .24, .32), 'RiverGlow', .02); parent(pivot(fill, (0, -.47, .84)), root)
    feed = join([sphere('Feed', (-.36, .15, 1.9), .11, 'RiverGlass', 18, 10), sphere('FeedGlow', (-.36, .15, 1.9), .055, 'RiverGlow', 10, 6)], 'Feed')
    parent(pivot(feed, (-.36, .15, 1.9)), root)
    return s


# ---------------------------------------------------------------- Water press (press)
def press(root):
    s = [cyl('Platen', (0, 0, .6), .45, .08, 'Steel', 32),
         cyl('Chamber', (0, 0, 1.0), .32, .72, 'ClearGlass', 36),
         cyl('Intake', (0, -.36, 1.5), .06, .2, 'Steel', 16, top=.15, rot=(math.radians(-30), 0, 0)),
         cyl('IntakePipe', (0, -.27, 1.38), .04, .16, 'Steel', 12, rot=(math.radians(-30), 0, 0)),
         torus('ChamberBand', (0, 0, 1.36), .32, .03, 'Steel'), torus('ChamberBand', (0, 0, .65), .32, .03, 'Steel'),
         box('Crossbeam', (0, 0, 2.38), (1.16, .3, .2), 'Pearl', .05),
         cyl('Hydraulic', (0, 0, 2.66), .16, .4, 'Azure', 24), cyl('HydraulicCap', (0, 0, 2.88), .19, .05, 'Steel', 24),
         cyl('Dial', (.48, -.1, 1.72), .13, .04, 'Pearl', 28, rot=(math.pi / 2, 0, 0)),
         torus('DialRim', (.48, -.12, 1.72), .13, .018, 'Azure', rot=(math.pi / 2, 0, 0))]
    for x in (-.48, .48): s.append(cyl('Pillar', (x, 0, 1.48), .06, 1.8, 'Steel', 16))
    column = cyl('WaterColumn', (0, 0, .99), .29, .66, 'WaterGlass', 32); parent(pivot(column, (0, 0, .66)), root)
    surface = join([cyl('SurfaceSheet', (0, 0, 1.32), .285, .012, 'WaterGlass', 32),
                    torus('Meniscus', (0, 0, 1.325), .275, .014, 'WaterFoam'),
                    torus('Ripple', (0, 0, 1.33), .16, .006, 'WaterFoam'), torus('Ripple', (0, 0, 1.33), .08, .005, 'WaterFoam')], 'WaterSurface')
    parent(pivot(surface, (0, 0, 1.32)), root)
    piston = join([cyl('Rod', (0, 0, 1.92), .05, .8, 'Steel', 14), cyl('Head', (0, 0, 1.47), .29, .08, 'Pearl', 32),
                   torus('HeadRing', (0, 0, 1.47), .29, .02, 'Azure')], 'Piston')
    parent(pivot(piston, (0, 0, 1.47)), root)
    needle = box('Needle', (.48, -.135, 1.77), (.02, .01, .1), 'Azure', 0); parent(pivot(needle, (.48, -.135, 1.72)), root)
    feed = join([sphere('Feed', (0, 0, 1.2), .11, 'WaterGlass', 18, 10), sphere('FeedCore', (0, 0, 1.2), .05, 'WaterFoam', 10, 6)], 'Feed')
    parent(pivot(feed, (0, 0, 1.2)), root)
    return s


# ---------------------------------------------------------------- Sky spinner (spin)
def spinner(root):
    s = [cyl('Drum', (0, 0, .7), .7, .28, 'Pearl', 40), torus('DrumBand', (0, 0, .8), .7, .035, 'Azure'),
         sphere('Dome', (0, 0, .84), .36, 'SkyGlass', 32, 16),
         cyl('Spindle', (0, 0, 1.17), .03, .66, 'Steel', 10),
         cyl('Mast', (0, 0, 1.82), .05, .62, 'Steel', 12),
         cyl('Intake', (-.82, 0, 1.0), .2, .3, 'Steel', 20, top=.08, rot=(0, -math.pi / 2, 0)),
         cyl('Intake', (.82, 0, 1.0), .2, .3, 'Steel', 20, top=.08, rot=(0, math.pi / 2, 0))]
    cloud = puff('CloudMass', (0, 0, 1.02), .16); parent(pivot(cloud, (0, 0, .86)), root)
    rotor = [cyl('RotorHub', (0, 0, 1.5), .12, .1, 'Azure', 24)]
    for i in range(3):
        a = i * 2 * math.pi / 3
        rotor.append(box('Arm', (math.cos(a) * .4, math.sin(a) * .4, 1.5), (.72, .06, .05), 'Pearl', .015, rot=(0, 0, a)))
        rotor.append(puff('Tuft', (math.cos(a) * .78, math.sin(a) * .78, 1.5), .09))
        rotor.append(sphere('Bead', (math.cos(a) * .6, math.sin(a) * .6, 1.53), .035, 'SkyGlow', 10, 6))
    parent(pivot(join(rotor, 'Rotor'), (0, 0, 1.5)), root)
    fan = [cyl('FanHub', (0, 0, 2.15), .06, .06, 'Azure', 16)]
    for i in range(3):
        a = i * 2 * math.pi / 3
        fan.append(box('Blade', (math.cos(a) * .2, math.sin(a) * .2, 2.15), (.32, .08, .012), 'SkyGlow', .004, rot=(math.radians(25), 0, a)))
    parent(pivot(join(fan, 'Fan'), (0, 0, 2.15)), root)
    feed = puff('Feed', (0, 0, 1.72), .08); parent(pivot(feed, (0, 0, 1.72)), root)
    return s


machines = [machine('StoneProcessor', quarry_base, crusher), machine('GlassFurnace', hearth_base, kiln),
            machine('RiverMill', riverbed_base, mill), machine('WaterPress', platform_base, press),
            machine('SkySpinner', cushion_base, spinner)]

# Standalone processed-material models for stone and glass (the essence blocks come from build_essence_workshop.py).
slab = empty('GlassSlab')
parent(join([box('Slab', (0, 0, 0), (.5, .34, .1), 'Glass', .03)] +
            [box('Facet', (x, 0, .052), (.02, .3, .012), 'Pearl', .004) for x in (-.15, -.05, .05, .15)], 'Slab'), slab)
brick = empty('StoneBrick')
parent(box('Brick', (0, 0, 0), (.42, .3, .2), 'Resource_Refined', .035), brick)

exports = machines + [slab, brick]
for root in exports:
    bpy.ops.object.select_all(action='DESELECT'); root.select_set(True)
    for c in root.children_recursive: c.select_set(True)
    bpy.ops.export_scene.fbx(filepath=OUT + '/' + root.name + '.fbx', use_selection=True, object_types={'MESH', 'EMPTY'},
                             bake_space_transform=False, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', bake_anim=False, add_leaf_bones=False)
for i, root in enumerate(exports): root.location.x = (i - len(exports) / 2) * 2.8
bpy.ops.wm.save_as_mainfile(filepath=ROOT + '/ArtSource/Resources/Processor_Family.blend')
print('Exported', {r.name: sum(len(o.data.polygons) for o in r.children_recursive if o.type == 'MESH') for r in exports})
