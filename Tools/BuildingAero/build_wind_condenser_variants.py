"""Wind Condenser variants, sky processor Round02: five takes on Round01 concept 03.

Requested 2026-11-04. The user picked 03 Wind Condenser (a turbine over a glass cloud orb on a grass ring)
and asked for a better green base and more cloud effects, in five variants. All keep the turbine and the
cloud orb, take a SkyCloud tuft in and give a SkyBlock out at the front, and keep three status lights:

  03A Bliss Hill        glossy rolling hill (Bliss-like), blades of grass and daisies; a cloud ring orbits
                        the tower, wind ribbons spiral up it and cloud streams off the blade tips
  03B Leaf Rotor Pond   meadow around a mirror pond with reeds and lily pads; four translucent leaf
                        blades trailing cloud arcs; mist pooled round the orb
  03C Helix Vortex      stepped grass terraces; a vertical helix rotor of frosted ribbons with a cloud
                        tornado spinning down into the orb
  03D Twin Rotor Grove  grassy knoll with round trees and bushes; a big and a small rotor, a cumulus cap
                        on the nacelle and a gust ribbon weaving through both
  03E Sky Island        floating grass island above a cloud bed; cloud falls off its rim like waterfalls
Static concepts, run inside the connected Blender (MCP socket). Scene: Aero_WindCondenserVariants.
"""
import bpy
KIT = 'D:/PersonalProjects/the-tower-of-babel/Tools/BuildingAero/sky_concept_kit.py'
SCENE_NAME = 'Aero_WindCondenserVariants'
exec(open(KIT, encoding='utf-8').read(), globals())


def turbine(base_z, tower_h, blades=3, length=.5, width=.16, blade_mat='Frost', hub_mat='Azure', orb_r=.42):
    """Glass cloud orb, tapered tower, nacelle and front rotor. Returns the hub height."""
    sphere('Orb', (0, 0, base_z + orb_r), orb_r, 'SkyGlass', seg=48, rings=24)
    cloud((0, 0, base_z + orb_r * .45), orb_r * .85)
    for p, s in [((.12, .1, base_z + orb_r * 1.2), .55), ((-.14, -.05, base_z + orb_r * 1.05), .45)]: tuft(p, s)
    torus('OrbCollar', (0, 0, base_z + orb_r * 2), .13, .04, hub_mat)
    z0 = base_z + orb_r * 2; hub = z0 + tower_h
    cyl('Tower', (0, 0, z0 + tower_h / 2), .085, tower_h, 'Pearl', top=.05)
    sphere('Nacelle', (0, .02, hub), .13, 'Pearl', scale=(1, 2.4, 1))
    sphere('Hub', (0, -.32, hub), .1, hub_mat)
    for i in range(blades):
        a = i * TAU / blades + .3
        sphere('Blade', (math.sin(a) * length, -.36, hub + math.cos(a) * length), length, blade_mat,
               scale=(width / (2 * length), .015 / length, 1), rot=(0, a, 0))
    return hub


def blade_tips(hub, blades=3, length=.5):
    return [(math.sin(i * TAU / blades + .3) * length * 2, -.38, hub + math.cos(i * TAU / blades + .3) * length * 2)
            for i in range(blades)]


def tray(y, z, stem=True, mat='Pearl'):
    lathe('Tray', [(0, 0), (.25, 0), (.29, .05), (.26, .06)], mat, loc=(0, y, z))
    if stem: rod('TrayStem', (0, y, 0), (0, y, z), .04, 'Chrome')
    block((0, y, z + .27))


def scatter_daisies(n, rmin, rmax, ground, seed, s=1.):
    rng = random.Random(seed)
    for _ in range(n):
        r = rng.uniform(rmin, rmax); a = rng.uniform(0, TAU); x, y = r * math.cos(a), r * math.sin(a)
        daisy((x, y, ground(x, y) + .1 * s), s * rng.uniform(.8, 1.2), (rng.uniform(-.3, .3), rng.uniform(-.3, .3), 0))


def v_a_bliss_hill():
    g = bumps((0, 0, .36, .72), (.62, .45, .12, .42), (-.66, .32, .1, .45), (.1, -.7, .05, .4))
    terrain('BlissHill', 1.2, g, 'Meadow')
    grass_patch('Grass', 900, .45, 1.18, h=(.09, .22), ground=g, seed=11)
    scatter_daisies(14, .45, 1.05, g, 5)
    hub = turbine(g(0, 0) - .06, 1.4)
    for i in range(10):                                  # a ring of cloud orbiting the tower, tilted
        a = i * TAU / 10; r = .78
        tuft((math.cos(a) * r, math.sin(a) * r, 1.95 + math.sin(a) * .18), .55 + .15 * (i % 3), (0, 0, a))
    for k in range(2):
        wind_ribbon('WindSpiral', spiral((0, 0), .55, .32, 1.05, 2.5, 1.25, 10, phase=k * math.pi), .07)
    for tip in blade_tips(hub):
        tuft_trail(tip, (tip[0] * 1.5 + .3, .25, tip[2] - .15), 4, .5, .25, bow=(0, .05, .08))
    tray(-1.32, .22)
    status_lights([(x, -.92, g(x, -.92) + .06) for x in (-.16, 0, .16)], 'Lime', .035)
    return hub + 1.15


def v_b_leaf_pond():
    g = bumps((0, 0, .12, 1.1))
    terrain('Meadow', 1.25, g, 'Meadow')
    torus('Pond', (0, 0, g(.62, 0) - .02), .62, .13, 'Mirror', scale=(1, 1, .22))
    torus('PondEdge', (0, 0, g(.78, 0) - .02), .78, .04, 'Stone', scale=(1, 1, .5))
    grass_patch('Grass', 800, .84, 1.22, h=(.1, .26), ground=g, seed=21)
    grass_patch('GrassInner', 140, .26, .46, h=(.06, .14), ground=g, seed=22)
    rng = random.Random(4)
    for i in range(7):
        a = rng.uniform(.4, 2.8) if i % 2 else rng.uniform(3.5, 6.0); r = rng.uniform(.62, .74)
        x, y = math.cos(a) * r, math.sin(a) * r
        if y < -.5 and abs(x) < .35: continue
        h = rng.uniform(.35, .55)
        rod('Reed', (x, y, .06), (x + .03, y, .06 + h), .01, 'Reed', seg=6)
        sphere('Cattail', (x + .03, y, .06 + h), .03, 'Twig', scale=(1, 1, 2.6), seg=10, rings=6)
    for a in (.9, 2.5, 4.1):
        cyl('LilyPad', (math.cos(a) * .6, math.sin(a) * .6, g(.6, 0) + .01), .08, .01, 'GrassGel')
    hub = turbine(g(0, 0) - .02, 1.55, blades=4, length=.48, width=.2, blade_mat='Leaf')
    cloud((0, 0, .05), .55, name='OrbMist').scale = (.6, .6, .3)
    for p, s in [((.4, -.25, .2), .6), ((-.42, .2, .18), .5), ((.1, .42, .2), .55)]: tuft(p, s)
    for tip in blade_tips(hub, 4, .48):                  # arcs of cloud trailing behind each leaf tip
        a0 = math.atan2(tip[0], tip[2] - hub)
        arc = [(math.sin(a0 - t) * .96, -.3 + t * .25, hub + math.cos(a0 - t) * .96) for t in (0, .25, .5, .75)]
        wind_ribbon('TipTrail', arc, .05)
        tuft(arc[-1], .45)
    tray(-1.38, .2)
    status_lights([(x, -.95, g(x, -.95) + .05) for x in (-.16, 0, .16)], 'Lime', .035)
    return hub + 1.11


def v_c_helix_vortex():
    steps = [(1.18, .12), (.86, .24), (.56, .36)]
    for i, (r, z) in enumerate(steps):
        lathe('Terrace_%d' % i, [(0, 0), (r, 0), (r + .02, z - .03), (r - .03, z), (0, z)], 'Meadow', seg=72)
        torus('TerraceRim_%d' % i, (0, 0, z - .015), r, .025, 'Pearl', seg=96)
        inner = steps[i + 1][0] + .04 if i + 1 < len(steps) else .44
        grass_patch('Grass_%d' % i, int(420 * (r - inner)) + 40, inner, r - .04, h=(.08, .2), ground=lambda x, y, z=z: z, seed=31 + i)
    scatter_daisies(10, .9, 1.1, lambda x, y: .12, 9, .9)
    base = .36; sphere('Orb', (0, 0, base + .42), .42, 'SkyGlass', seg=48, rings=24); cloud((0, 0, base + .18), .36)
    torus('OrbCollar', (0, 0, base + .84), .13, .04, 'Azure')
    cyl('Mast', (0, 0, 2.2), .05, 2.0, 'Pearl', top=.035)
    for z in (1.45, 3.05):
        torus('HelixRing', (0, 0, z), .44, .025, 'Azure')
        for k in range(3): rod('Spoke', (0, 0, z), (math.cos(k * TAU / 3) * .44, math.sin(k * TAU / 3) * .44, z), .015, 'Chrome')
    for k in range(3):
        o = wind_ribbon('HelixBlade', spiral((0, 0), .44, .44, 1.45, 3.05, .5, 14, phase=k * TAU / 3), .2, mat='Frost')
    pts = spiral((0, 0), .32, .07, 2.95, 1.3, 2.6, 18)  # cloud tornado narrowing into the orb
    for i, p in enumerate(pts): tuft(p, .7 - .025 * i, (0, 0, i * .7))
    wind_ribbon('Vortex', spiral((0, 0), .62, .2, 3.2, 1.25, 1.6, 14), .06)
    sphere('TopGem', (0, 0, 3.22), .05, 'Cyan')
    tray(-1.38, .2)
    status_lights([(x, -1.08, .14) for x in (-.16, 0, .16)], 'Lime', .035)
    return 3.5


def v_d_twin_grove():
    g = bumps((0, 0, .26, .8), (.72, -.25, .14, .35), (-.6, .5, .12, .4))
    terrain('Knoll', 1.22, g, 'Meadow')
    grass_patch('Grass', 750, .42, 1.18, h=(.09, .24), ground=g, seed=41)
    scatter_daisies(10, .5, 1.05, g, 13)
    for x, y, s in [(-.82, .38, 1), (.78, .5, .8), (-.5, -.72, .65)]:
        z = g(x, y)
        rod('Trunk', (x, y, z - .02), (x, y, z + .38 * s), .035 * s, 'Bark')
        for dx, dy, dz, r in [(0, 0, .48, .2), (-.12, .02, .4, .14), (.11, -.03, .42, .15), (0, .1, .58, .12)]:
            sphere('Canopy', (x + dx * s, y + dy * s, z + dz * s), r * s, 'GrassGel')
    for x, y in [(.95, -.05), (-.95, -.25), (.35, .9)]:
        for dx, r in [(0, .11), (.09, .08), (-.08, .085)]: sphere('Bush', (x + dx, y, g(x, y) + r * .6), r, 'GrassGel')
    hub = turbine(g(0, 0) - .06, 1.45, length=.55)
    rod('SideArm', (0, 0, 1.85), (.42, -.05, 1.85), .03, 'Pearl')
    sphere('SmallNacelle', (.42, -.02, 1.85), .07, 'Pearl', scale=(1, 2, 1)); sphere('SmallHub', (.42, -.2, 1.85), .055, 'Azure')
    for i in range(5):
        a = i * TAU / 5
        sphere('SmallBlade', (.42 + math.sin(a) * .2, -.22, 1.85 + math.cos(a) * .2), .2, 'Frost', scale=(.12, .03, 1), rot=(0, a, 0))
    cloud((0, .32, hub - .12), .42, name='CumulusCap')
    for p, s in [((-.25, .38, hub + .22), .8), ((.28, .35, hub + .18), .7)]: tuft(p, s)
    wind_ribbon('Gust', [(-1.1, -.5, 1.5), (-.4, -.45, 1.95), (.42, -.35, 1.75), (.9, -.3, 2.5), (1.15, -.1, 2.9)], .08)
    tuft_trail((-1.0, -.45, 1.55), (1.05, -.2, 2.75), 6, .35, .55, bow=(0, -.05, .1))
    tray(-1.3, .22)
    status_lights([(x, -.95, g(x, -.95) + .06) for x in (-.16, 0, .16)], 'Lime', .035)
    return hub + 1.25


def v_e_sky_island():
    cloud((0, 0, 0), .85, name='CloudBed').scale = (.95, .95, .45)
    for i in range(9):
        a = i * TAU / 9; tuft((math.cos(a) * 1.0, math.sin(a) * .95, .12), 1.0 + .2 * (i % 2))
    lift = .78
    lathe('IslandRock', [(0, lift - .55), (.3, lift - .45), (.62, lift - .25), (.92, lift - .06), (1.0, lift)], 'Stone', seg=40)
    g0 = bumps((0, 0, .16, .6), (.5, .4, .06, .3))
    g = lambda x, y: lift + g0(x, y)
    terrain('IslandTop', 1.0, g0, 'Meadow', loc=(0, 0, lift))
    grass_patch('Grass', 600, .4, .98, h=(.08, .2), ground=g, seed=51)
    scatter_daisies(9, .45, .9, g, 17, .9)
    for a in (2.4, 3.6, .3):                             # cloud pouring off the rim
        x, y = math.cos(a), math.sin(a)
        tuft_trail((x * .98, y * .98, lift + .02), (x * 1.15, y * 1.15, .25), 5, .55, .9, bow=(x * .05, y * .05, 0))
    hub = turbine(g(0, 0) - .05, 1.25)
    for p, s in [((.7, -.2, hub - .2), .7), ((-.75, -.1, hub + .1), .6), ((.45, .2, hub + .45), .55)]: tuft(p, s)
    wind_ribbon('Updraft', spiral((0, 0), 1.05, .5, .35, hub - .2, .9, 10), .07)
    lathe('Tray', [(0, 0), (.25, 0), (.29, .05), (.26, .06)], 'Pearl', loc=(0, -1.25, .35))
    tuft((0, -1.25, .22), 1.1); block((0, -1.25, .62))
    status_lights([(x, -.86, lift + .06) for x in (-.16, 0, .16)], 'Lime', .035)
    return hub + 1.15


CONCEPTS = [('03A', 'Bliss Hill', v_a_bliss_hill), ('03B', 'Leaf Rotor Pond', v_b_leaf_pond),
            ('03C', 'Helix Vortex', v_c_helix_vortex), ('03D', 'Twin Rotor Grove', v_d_twin_grove),
            ('03E', 'Sky Island', v_e_sky_island)]
layout(CONCEPTS, 'WindCondenserVariants', 'WindCondenser', spacing=3.6)
