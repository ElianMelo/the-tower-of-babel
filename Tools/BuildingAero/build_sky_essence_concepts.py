"""Sky essence resource concepts, Round01: ten cloud-and-wind gather nodes for review.

Requested 2026-11-04, alongside the Wind Condenser variants. Like the current SkyCloud node (see
Documentation/FrutigerAero/Resources/ESSENCE_WORKSHOP.md) every design is a grounded natural resource
about 2 m across with no machine parts, split into two groups under its root:
  Essence  - the part that is gathered and hides during cooldown (built from the real SkyCloud tufts)
  Base     - what stays behind
  01 Cumulus Nest      a fat cumulus resting in a grass nest ringed by white pebbles
  02 Dust Devil        a spiral of cloud and wind ribbons rising out of swirled grass
  03 Cloud Dandelions  dandelion clocks whose seed heads are cloud; a leaf rosette stays
  04 Cotton Cloud Bush a pale twig shrub fruiting cloud bolls
  05 Sky Vent          a white stone vent with sky crystals breathing a column of cloud
  06 Mirror Pond       a cloud lying on a sky-mirror pond with reeds
  07 Crystal Geode     sky crystals with cloud wisps and cloud-filled bubbles caught among them
  08 Breeze Sheaf      tall wind-bent grass with cloud streaming through it
  09 Cloud Bubbles     iridescent bubbles each holding a cloud, resting on moss
  10 Wind Arch         a wind-carved stone arch with a cloud stream flowing through
Static concepts, run inside the connected Blender (MCP socket). Scene: Aero_SkyEssenceConcepts.
"""
import bpy
KIT = 'D:/PersonalProjects/the-tower-of-babel/Tools/BuildingAero/sky_concept_kit.py'
SCENE_NAME = 'Aero_SkyEssenceConcepts'
exec(open(KIT, encoding='utf-8').read(), globals())


def groups():
    """Create Base and Essence under the concept root; returns them (switch with into())."""
    return empty('Base', (0, 0, 0)), empty('Essence', (0, 0, 0))


def motes(points, r=.025):
    for p in points: sphere('Mote', p, r, 'SkyGlow', seg=10, rings=6)


def e01_cumulus_nest():
    B, E = groups(); into(B)
    g = bumps((0, 0, .06, .9))
    terrain('MossBed', 1.0, g, 'Moss', rings=12)
    grass_patch('NestGrass', 650, .62, 1.05, h=(.12, .3), ground=g, seed=61, lean=.45)
    pebbles(16, .78, 1.0, (.07, .13), seed=6)
    into(E)
    cloud((0, 0, .02), .72)
    for p, s in [((.18, .1, .62), 1.3), ((-.25, -.05, .55), 1.1), ((.05, -.2, .72), .9)]: tuft(p, s)
    for p, s in [((.85, .3, .9), .7), ((-.8, -.2, 1.05), .6), ((.2, .7, 1.2), .5)]: tuft(p, s)
    motes([(.5, -.5, .7), (-.45, -.55, .9), (.65, .1, 1.2), (-.2, .5, 1.0)])
    return 1.4


def e02_dust_devil():
    B, E = groups(); into(B)
    terrain('Meadow', 1.05, bumps((0, 0, .05, .8)), 'Meadow', rings=12)
    grass_patch('SwirlGrass', 750, .22, 1.02, h=(.12, .28), ground=bumps((0, 0, .05, .8)), seed=71, swirl=.5)
    for a in (.7, 2.6, 4.4):
        sphere('FlatStone', (math.cos(a) * .55, math.sin(a) * .55, .04), .12, 'Stone', scale=(1.3, 1, .35), rot=(0, 0, a))
    into(E)
    pts = spiral((0, 0), .12, .7, .12, 1.55, 2.2, 20)
    for i, p in enumerate(pts): tuft(p, .45 + .045 * i, (0, 0, i * .6))
    for k in range(3):
        wind_ribbon('Gust', spiral((0, 0), .2, .85, .08, 1.65, 1.6, 14, phase=k * TAU / 3), .06)
    cloud((0, 0, 1.45), .32, name='CloudCap')
    return 1.85


def dandelion(base, height, lean, r=.17, seeds=26):
    top = (base[0] + lean[0], base[1] + lean[1], height)
    ribbon('Stem', [base, ((base[0] + top[0]) / 2 + lean[0] * .2, (base[1] + top[1]) / 2, height * .55), top], .012, 'Reed')
    sphere('Receptacle', top, .035, 'Twig', seg=12, rings=8)
    sphere('CloudHead', top, r * .82, 'Cloud', seg=24, rings=12)
    gold = math.pi * (3 - math.sqrt(5))
    for i in range(seeds):
        z = 1 - 2 * (i + .5) / seeds; rr = math.sqrt(1 - z * z); a = gold * i
        d = Vector((math.cos(a) * rr, math.sin(a) * rr, z))
        tip = Vector(top) + d * r
        rod('Pappus', top, tip, .003, 'Daisy', seg=4)
        sphere('Fluff', tip, .028, 'Cloud', seg=10, rings=6)
    return top


def e03_dandelions():
    B, E = groups(); into(B)
    for i in range(8):
        a = i * TAU / 8 + .2
        sphere('RosetteLeaf', (math.cos(a) * .38, math.sin(a) * .38, .04), .36, 'Leaf', scale=(1, .42, .07), rot=(0, -.12, a))
    grass_patch('Grass', 300, .7, 1.0, h=(.08, .2), seed=81)
    sphere('Crown', (0, 0, .04), .14, 'GrassGel', scale=(1, 1, .4))
    into(E)
    tops = [dandelion((0, 0, .05), 1.1, (.05, .05)), dandelion((.12, -.05, .05), .82, (.32, -.12), .15),
            dandelion((-.1, .05, .05), .68, (-.3, .1), .14)]
    for i, (p, s) in enumerate([((.6, -.2, 1.15), .4), ((.85, -.3, 1.25), .32), ((1.05, -.35, 1.4), .25)]):
        tuft(p, s)
    wind_ribbon('Breeze', [(-.4, -.3, .9), (.2, -.2, 1.15), (.7, -.25, 1.2), (1.15, -.4, 1.45)], .05)
    return 1.5


def e04_cotton_bush():
    B, E = groups(); into(B)
    terrain('Moss', .9, bumps((0, 0, .05, .7)), 'Moss', rings=10)
    grass_patch('Grass', 300, .5, .95, h=(.08, .2), seed=91)
    rng = random.Random(12); tips = []
    for i in range(7):
        a = i * TAU / 7 + rng.uniform(-.2, .2); r = rng.uniform(.4, .6); h = rng.uniform(.65, 1.0)
        mid = (math.cos(a) * r * .4, math.sin(a) * r * .4, h * .45); tip = (math.cos(a) * r, math.sin(a) * r, h)
        ribbon('Branch', [(0, 0, .02), mid, tip], .025, 'Twig'); tips.append(tip)
        for k in range(3):
            p = Vector(mid).lerp(Vector(tip), .4 + .2 * k) + Vector((rng.uniform(-.06, .06), rng.uniform(-.06, .06), .02))
            sphere('Leaf', p, .07, 'GrassGel', scale=(1, .6, .3), rot=(rng.uniform(0, 1), 0, a))
    into(E)
    for i, t in enumerate(tips): tuft((t[0], t[1], t[2] + .08), .75 + .15 * (i % 3), (0, 0, i))
    tuft((0, 0, 1.05), 1.0)
    return 1.35


def e05_sky_vent():
    B, E = groups(); into(B)
    lathe('VentMound', [(0, .12), (.18, .14), (.24, .34), (.3, .38), (.55, .25), (.85, .08), (1.0, 0)], 'Rock', seg=48)
    cyl('VentGlow', (0, 0, .15), .2, .02, 'SkyGlow')
    rng = random.Random(5)
    for i in range(7):
        a = i * TAU / 7 + rng.uniform(-.3, .3); r = rng.uniform(.5, .85)
        ico('SkyShard', (math.cos(a) * r, math.sin(a) * r, .12), .1, 'SkyCrystal', scale=(.6, .6, rng.uniform(1.6, 2.6)),
            rot=(math.cos(a + 1.57) * .45, math.sin(a + 1.57) * .45, a))
    pebbles(10, .85, 1.05, (.05, .1), seed=15)
    into(E)
    for i in range(11):
        z = .3 + i * .13; s = .5 + i * .08
        tuft((math.sin(i * 1.3) * .06 * i, math.cos(i * 1.1) * .04 * i, z), s, (0, 0, i * 1.7))
    wind_ribbon('Plume', spiral((0, 0), .2, .55, .3, 1.75, 1.2, 12), .05)
    cloud((0, 0, 1.55), .3, name='PlumeHead')
    return 2.0


def e06_mirror_pond():
    B, E = groups(); into(B)
    cyl('Pond', (0, 0, .02), .72, .04, 'Mirror', seg=64)
    pebbles(22, .7, .9, (.06, .12), seed=22)
    grass_patch('Grass', 500, .86, 1.08, h=(.1, .28), seed=101)
    rng = random.Random(8)
    for i in range(6):
        a = rng.uniform(.3, 3.0); r = rng.uniform(.76, .9); x, y = math.cos(a) * r, math.sin(a) * r; h = rng.uniform(.4, .65)
        rod('Reed', (x, y, 0), (x + .02, y, h), .01, 'Reed', seg=6)
        sphere('Cattail', (x + .02, y, h), .03, 'Twig', scale=(1, 1, 2.6), seg=10, rings=6)
    for a in (3.6, 4.6, 5.5): cyl('LilyPad', (math.cos(a) * .5, math.sin(a) * .5, .045), .09, .01, 'GrassGel')
    into(E)
    cloud((0, .05, .06), .55, name='RestingCloud').scale = (.6, .55, .38)
    for p, s in [((.25, .1, .38), .9), ((-.22, .15, .33), .8), ((.0, -.15, .42), .7)]: tuft(p, s)
    wind_ribbon('Mist', [(math.cos(a) * .62, math.sin(a) * .62, .12 + .04 * math.sin(3 * a)) for a in [k * TAU / 9 for k in range(10)]], .05)
    motes([(.45, -.35, .5), (-.5, -.2, .6), (.3, .45, .7)])
    return 1.2


def e07_crystal_geode():
    B, E = groups(); into(B)
    for p, s in [((0, 0, .1), (.75, .6, .35)), ((.5, .3, .08), (.4, .35, .25)), ((-.55, .2, .07), (.35, .4, .2))]:
        sphere('Rock', p, 1, 'Rock', scale=s, seg=20, rings=10)
    rng = random.Random(9)
    for i in range(8):
        a = i * TAU / 8 + rng.uniform(-.2, .2); tilt = rng.uniform(.3, .7)
        ico('SkyCrystal', (math.cos(a) * .28, math.sin(a) * .22, .4), .21, 'SkyCrystal', scale=(.55, .55, rng.uniform(2.2, 3.4)),
            rot=(-math.sin(a) * tilt, math.cos(a) * tilt, a))
    ico('CoreCrystal', (0, 0, .6), .26, 'SkyCrystal', scale=(.6, .6, 3.2), rot=(0, 0, .3))
    pebbles(8, .75, .95, (.05, .1), seed=19)
    into(E)
    for p, s in [((.35, -.3, .55), .9), ((-.4, -.25, .7), .8), ((.1, .4, .85), .7), ((-.15, -.1, 1.1), .6)]: tuft(p, s)
    for p, r in [((.55, -.1, .9), .16), ((-.6, .05, 1.0), .13), ((.25, -.45, 1.15), .11)]:
        sphere('CloudBubble', p, r, 'Bubble', seg=32, rings=16); tuft(p, r * 3.2)
    wind_ribbon('Wisp', spiral((0, 0), .55, .35, .4, 1.25, 1.1, 12), .05)
    return 1.6


def e08_breeze_sheaf():
    B, E = groups(); into(B)
    terrain('Meadow', 1.0, bumps((0, 0, .06, .8)), 'Meadow', rings=12)
    grass_patch('Sheaf', 700, .05, .75, h=(.45, .95), ground=bumps((0, 0, .06, .8)), seed=111, wind=(.55, .12), width=.026)
    grass_patch('Short', 300, .6, 1.0, h=(.1, .22), seed=112)
    for x, y in [(-.6, -.45), (.7, .5)]: sphere('Stone', (x, y, .05), .14, 'Stone', scale=(1.2, 1, .45))
    into(E)
    tuft_trail((-.9, -.1, .45), (1.15, .1, .85), 7, .9, .45, bow=(0, -.08, .08))
    tuft_trail((-.7, .35, .7), (1.05, .45, 1.05), 6, .7, .35, bow=(0, .05, .05))
    for z, dy in [(.55, -.1), (.85, .3), (1.05, .05)]:
        wind_ribbon('Gust', [(-1.0, dy, z), (-.3, dy - .05, z + .08), (.4, dy + .05, z), (1.1, dy, z + .12)], .05)
    return 1.3


def e09_cloud_bubbles():
    B, E = groups(); into(B)
    g = bumps((0, 0, .14, .7), (.5, .3, .06, .35))
    terrain('MossMound', 1.0, g, 'Moss', rings=14)
    grass_patch('Grass', 350, .65, 1.0, h=(.08, .2), ground=g, seed=121)
    pebbles(10, .7, .95, (.05, .1), seed=23)
    for a in (1.2, 3.0, 5.0):
        sphere('Leaf', (math.cos(a) * .7, math.sin(a) * .7, g(0, 0) * .3), .2, 'Leaf', scale=(1, .4, .1), rot=(0, 0, a))
    into(E)
    for p, r in [((0, 0, .5), .36), ((.42, -.18, .35), .24), ((-.38, -.12, .32), .22), ((.15, .38, .38), .25),
                 ((-.12, -.36, .25), .15), ((.32, .12, .74), .16), ((-.25, .2, .72), .13)]:
        sphere('Bubble', p, r, 'Bubble', seg=40, rings=20); tuft(p, r * 2.6, (0, 0, r * 9))
    motes([(.6, -.4, .8), (-.6, .1, .9), (.0, -.5, .7)])
    return 1.15


def e10_wind_arch():
    B, E = groups(); into(B)
    bm = lathe_bm([(.62 + .17 * math.cos(i * TAU / 12), .17 * math.sin(i * TAU / 12) * 1.2) for i in range(12)], 48, closed=True)
    for v in bm.verts:                                   # wind-worn: thinner at the crown, lumpy
        a = math.atan2(v.co.y, v.co.x); k = .82 + .18 * abs(math.cos(a)) + .05 * math.sin(a * 7)
        c = Vector((math.cos(a) * .62, math.sin(a) * .62, 0)); v.co = c + (v.co - c) * k
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.y < -.05], context='VERTS')
    make('StoneArch', bm, 'Rock', (0, 0, .02), (math.pi / 2, 0, 0), subsurf=1)
    for x in (-.62, .62): sphere('ArchFoot', (x, 0, .08), .26, 'Rock', scale=(1, .9, .5))
    grass_patch('Grass', 450, .55, 1.0, h=(.08, .22), seed=131)
    pebbles(8, .75, 1.0, (.05, .1), seed=29)
    into(E)
    tuft_trail((0, .95, .3), (0, -.95, .45), 8, .7, 1.0, bow=(0, 0, .12))
    for x in (-.2, .15):
        wind_ribbon('Flow', [(x, 1.0, .25), (x * .6, .4, .45), (x * .4, -.2, .5), (x, -1.0, .4)], .05)
    cloud((0, 0, .02), .38, name='CloudUnderArch').scale = (.38, .5, .4)
    return 1.1


CONCEPTS = [('01', 'Cumulus Nest', e01_cumulus_nest), ('02', 'Dust Devil', e02_dust_devil),
            ('03', 'Cloud Dandelions', e03_dandelions), ('04', 'Cotton Cloud Bush', e04_cotton_bush),
            ('05', 'Sky Vent', e05_sky_vent), ('06', 'Mirror Pond', e06_mirror_pond),
            ('07', 'Crystal Geode', e07_crystal_geode), ('08', 'Breeze Sheaf', e08_breeze_sheaf),
            ('09', 'Cloud Bubbles', e09_cloud_bubbles), ('10', 'Wind Arch', e10_wind_arch)]
layout(CONCEPTS, 'SkyEssenceConcepts', 'SkyEssence', spacing=3.2)
