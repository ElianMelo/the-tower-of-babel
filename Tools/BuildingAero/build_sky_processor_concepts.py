"""Sky essence processor concepts, Round01: ten static Frutiger Aero machines for review.

Requested 2026-11-04. The user wants a replacement for the Sky spinner. Each concept turns the real
Sky Essence (SkyCloud.fbx tufts) into the real Sky Block (SkyBlock.fbx), and differs from the others in
silhouette, shader and colour while keeping the Frutiger Aero look (glossy glass, bubbles, chrome,
translucent candy plastic, gel, aurora, sky blue and grass green). No animation: concepts only.

Run inside the connected Blender (MCP socket). It builds its own scene, 'Aero_SkyProcessorConcepts',
and leaves every other scene alone. Render the review sheet with render_sky_processor_concepts.py.

  01 Bubble Still     iridescent soap bubble over a chrome hourglass
  02 Cloud Lava Lamp  tall tapered lamp, cobalt candy metal, cloud wax in blue liquid
  03 Wind Condenser   white turbine tower, frosted blades, cloud orb on a grass ring
  04 Gel Capsule      horizontal translucent bondi-blue capsule on chrome legs
  05 Sky Balloon      striped balloon envelope tethered over a basket
  06 Gyro Orb         three gel rings around a glowing cloud core
  07 Terrarium Dome   low glass dome over a grassy island with a tree and clouds
  08 Bloom Flower     translucent leaf petals around a glass cloud bulb
  09 Aurora Arch      emissive aurora arch over a faceted prism
  10 Jelly Levitator  glowing jellyfish bell with ribbons over a magnetic pad
Metres, Z up, the front faces -Y. Every concept has an input tuft, three status lights and a BlockSocket.
"""
import bpy
KIT = 'D:/PersonalProjects/the-tower-of-babel/Tools/BuildingAero/sky_concept_kit.py'
SCENE_NAME = 'Aero_SkyProcessorConcepts'
exec(open(KIT, encoding='utf-8').read(), globals())

# ---------------------------------------------------------------- the ten concepts
def c01_bubble_still():
    lathe('HourglassBase', [(0, 0), (.58, 0), (.62, .05), (.56, .13), (.32, .34), (.24, .48), (.3, .6), (.44, .68), (.42, .74), (0, .74)], 'Chrome')
    torus('GlowRing', (0, 0, .7), .43, .025, 'SkyGlow')
    torus('Collar', (0, 0, .78), .27, .05, 'Chrome')
    sphere('Bubble', (0, 0, 1.38), .64, 'Bubble', seg=64, rings=32)
    for i in range(9):                          # cloud spiralling inside the bubble
        a = i * .85; r = .34 - .025 * i
        tuft((math.cos(a) * r, math.sin(a) * r, 1.02 + .085 * i), .55 + .04 * (i % 3), (0, 0, a))
    cyl('TopCap', (0, 0, 2.05), .07, .06, 'Chrome'); sphere('TopGem', (0, 0, 2.13), .05, 'Pink')
    lathe('Intake', [(.05, 0), (.08, .12), (.18, .3), (.2, .32)], 'Chrome', loc=(-.82, 0, 1.18), rot=(0, math.radians(-70), 0))
    rod('IntakePipe', (-.8, 0, 1.18), (-.55, 0, 1.25), .05, 'Chrome')
    tuft((-1.12, -.05, 1.3), .9)
    lathe('Cradle', [(0, 0), (.24, 0), (.3, .05), (.3, .07), (.26, .07)], 'Pearl', loc=(0, -.86, .3))
    rod('CradleStem', (0, -.86, 0), (0, -.86, .3), .05, 'Chrome')
    block((0, -.86, .58))
    status_lights([(x, -.47, .2) for x in (-.14, 0, .14)], 'Pink', .035)
    return 2.2


def c02_lava_lamp():
    lathe('ConeBase', [(0, 0), (.56, 0), (.58, .04), (.44, .3), (.3, .6), (.34, .64), (0, .64)], 'Cobalt')
    torus('BaseBand', (0, 0, .62), .33, .025, 'Chrome')
    vessel = [(.31, .64), (.38, .95), (.43, 1.35), (.4, 1.75), (.3, 2.05), (.2, 2.3)]
    lathe('Liquid', [(0, .64)] + vessel + [(0, 2.3)], 'LampLiquid', seg=64)
    for i, (z, s, x) in enumerate([(.8, (.22, .22, .16), 0), (1.15, (.12, .12, .2), .12), (1.5, (.16, .16, .24), -.08),
                                   (1.92, (.1, .1, .13), .06), (1.3, (.07, .07, .07), -.22), (.95, (.08, .08, .1), .2)]):
        sphere('CloudWax', (x, 0, z), 1, 'Wax', scale=s)
    lathe('Cap', [(0, 2.28), (.22, 2.28), (.2, 2.34), (.07, 2.62), (0, 2.66)], 'Cobalt')
    sphere('CapTip', (0, 0, 2.68), .045, 'Chrome')
    tuft((0, .05, 2.9), .8)
    rbox('Drawer', (0, -.48, .22), (.5, .3, .26), 'Pearl', .06)
    rbox('DrawerLip', (0, -.7, .1), (.56, .32, .06), 'Chrome', .025)
    block((0, -.7, .34))
    status_lights([(x, -.64, .32) for x in (-.15, 0, .15)], 'Cyan', .03)
    return 2.95


def c03_wind_condenser():
    lathe('Pedestal', [(0, 0), (.72, 0), (.78, .07), (.72, .2), (.56, .27), (0, .27)], 'Pearl')
    torus('GrassRing', (0, 0, .06), .8, .09, 'GrassGel', scale=(1, 1, .7))
    for i in range(10):
        a = i * TAU / 10
        sphere('Leaf', (math.cos(a) * .86, math.sin(a) * .86, .1), .12, 'GrassGel', scale=(1.6, .7, .35), rot=(0, 0, a + .6))
    sphere('Orb', (0, 0, .72), .46, 'SkyGlass', seg=48, rings=24)
    cloud((0, 0, .44), .38)
    torus('OrbCollar', (0, 0, 1.13), .14, .04, 'Azure')
    cyl('Tower', (0, 0, 1.9), .09, 1.5, 'Pearl', top=.05)
    sphere('Nacelle', (0, .02, 2.68), .13, 'Pearl', scale=(1, 2.4, 1))
    sphere('Hub', (0, -.32, 2.68), .1, 'Azure')
    for i in range(3):
        a = i * TAU / 3 + .3
        sphere('Blade', (math.sin(a) * .5, -.36, 2.68 + math.cos(a) * .5), .5, 'Frost',
               scale=(.16, .03, 1), rot=(0, a, 0))
    for p, s in [((.85, -.3, 2.3), .8), ((-.7, -.25, 2.0), .6), ((.55, -.1, 1.75), .5)]: tuft(p, s)
    rod('Chute', (0, -.4, .55), (0, -.88, .34), .06, 'Azure')
    lathe('Tray', [(0, 0), (.25, 0), (.29, .05), (.26, .06)], 'Pearl', loc=(0, -.95, .28))
    block((0, -.95, .55))
    status_lights([(x, -.74, .2) for x in (-.15, 0, .15)], 'Lime', .035)
    return 3.7


def c04_gel_capsule():
    L, R = .62, .46
    prof = [(math.sin(t) * R, -L - math.cos(t) * R) for t in [i * (math.pi / 2) / 8 for i in range(9)]]
    prof += [(math.sin(t) * R, L + math.cos(t) * R) for t in [math.pi / 2 - i * (math.pi / 2) / 8 for i in range(9)]]
    lathe('CandyShell', [(0, -L - R)] + prof + [(0, L + R)], 'Candy', loc=(0, 0, 1.0), rot=(0, math.pi / 2, 0), seg=64)
    cyl('InnerDrum', (0, 0, 1.0), .28, 1.05, 'Pearl', rot=(0, math.pi / 2, 0))
    for x in (-.4, -.2, 0, .2, .4): torus('DrumRib', (x, 0, 1.0), .29, .025, 'Azure', rot=(0, math.pi / 2, 0))
    for x in (-.62, .62): torus('EndRing', (x, 0, 1.0), .44, .035, 'Chrome', rot=(0, math.pi / 2, 0))
    for sx in (-1, 1):
        for sy in (-1, 1):
            rod('Leg', (sx * .45, sy * .2, .7), (sx * .7, sy * .42, .06), .035, 'Chrome')
            sphere('Foot', (sx * .7, sy * .42, .06), .07, 'Chrome')
    sphere('IntakeDome', (.3, 0, 1.44), .17, 'SkyGlass', scale=(1, 1, .8))
    torus('IntakeRing', (.3, 0, 1.42), .17, .025, 'Chrome')
    tuft((.32, 0, 1.75), .8)
    rbox('Chute', (0, -.62, .62), (.36, .5, .05), 'Pearl', .02, rot=(math.radians(-38), 0, 0))
    lathe('Tray', [(0, 0), (.26, 0), (.3, .05), (.27, .06)], 'Pearl', loc=(0, -.92, .2))
    rod('TrayStem', (0, -.92, 0), (0, -.92, .2), .04, 'Chrome')
    block((0, -.92, .47))
    status_lights([(x, -.47, 1.0) for x in (-.15, 0, .15)], 'Cyan', .03)
    return 1.95


def c05_sky_balloon():
    lathe('Platform', [(0, 0), (.78, 0), (.82, .06), (.76, .12), (0, .12)], 'Pearl')
    torus('PlatformBand', (0, 0, .06), .8, .025, 'Azure')
    lathe('Basket', [(0, .12), (.46, .12), (.5, .3), (.54, .55), (.58, .58), (.5, .58), (.46, .2), (0, .2)], 'Wicker', seg=64)
    torus('BasketRim', (0, 0, .58), .56, .04, 'Pearl')
    env = [(0, 1.35), (.16, 1.35), (.3, 1.55), (.58, 1.95), (.76, 2.35), (.78, 2.65), (.66, 2.95), (.38, 3.15), (0, 3.22)]
    lathe('Envelope', env, 'Gores', seg=64, subsurf=2)
    torus('Burner', (0, 0, 1.3), .17, .035, 'Chrome'); sphere('Flame', (0, 0, 1.22), .07, 'SkyGlow', scale=(1, 1, 1.6))
    for i in range(4):
        a = i * TAU / 4 + TAU / 8
        rod('Rope', (math.cos(a) * .15, math.sin(a) * .15, 1.28), (math.cos(a) * .52, math.sin(a) * .52, .58), .012, 'Chrome')
    for p, s in [((.75, -.3, 1.4), .8), ((-.8, -.15, 1.6), .7), ((.4, .6, 1.2), .6)]: tuft(p, s)
    rbox('Shelf', (0, -.66, .5), (.42, .26, .05), 'Pearl', .02)
    block((0, -.68, .74))
    status_lights([(x, -.56, .32) for x in (-.16, 0, .16)], 'SkyGlow', .035)
    return 3.25


def c06_gyro_orb():
    lathe('GelPedestal', [(0, 0), (.62, 0), (.68, .06), (.64, .3), (.48, .38), (.3, .43), (0, .43)], 'GelDeep')
    torus('ChromeRim', (0, 0, .41), .33, .03, 'Chrome')
    rod('Post', (0, 0, .43), (0, 0, .84), .035, 'Chrome')
    cloud((0, 0, .9), .3, name='CoreCloud')
    sphere('Core', (0, 0, 1.16), .3, 'Core', seg=48, rings=24)
    for i, (R, rot) in enumerate([(.66, (math.radians(70), 0, 0)), (.57, (0, math.radians(70), math.radians(30))),
                                  (.48, (math.radians(25), math.radians(-40), 0))]):
        ring = torus('GelRing_%d' % i, (0, 0, 1.16), R, .038, 'GelRing', rot=rot)
        for k in range(3):
            a = k * TAU / 3 + i
            sphere('Bead', (math.cos(a) * R, math.sin(a) * R, 0), .045, 'Cyan', parent=ring)
    tuft((.85, .3, 1.55), .8)
    cyl('OutputPlate', (0, -.9, .42), .3, .03, 'SkyGlass'); torus('PlateGlow', (0, -.9, .42), .3, .016, 'Cyan')
    block((0, -.9, .65))
    status_lights([(x, -.58, .22) for x in (-.15, 0, .15)], 'Cyan', .035)
    return 1.9


def c07_terrarium():
    lathe('Plinth', [(0, 0), (.95, 0), (1.0, .06), (.98, .24), (.92, .3), (0, .3)], 'Pearl', seg=64)
    torus('PlinthBand', (0, 0, .16), .99, .03, 'Azure', seg=96)
    dome = [(math.cos(t) * .9, .3 + math.sin(t) * .82) for t in [i * (math.pi / 2) / 16 for i in range(17)]]
    lathe('Dome', dome, 'Dome', seg=96, subsurf=0)
    torus('DomeSeat', (0, 0, .31), .9, .035, 'Chrome', seg=96)
    sphere('Island', (0, 0, .3), .72, 'Grass', scale=(1, 1, .3))
    cyl('Pond', (.32, -.22, .46), .2, .02, 'Pond')
    rod('Trunk', (-.15, .1, .4), (-.12, .1, .82), .045, 'Bark')
    for p, r in [((-.12, .1, .92), .2), ((-.26, .05, .86), .14), ((.0, .16, .86), .15)]: sphere('Canopy', p, r, 'Grass')
    for p, s in [((.3, .2, .85), .55), ((-.45, -.25, .7), .45), ((.15, -.35, .95), .4)]: tuft(p, s)
    cyl('Vent', (0, 0, 1.16), .1, .08, 'Chrome'); lathe('VentFunnel', [(.04, 0), (.06, .06), (.16, .16)], 'Chrome', loc=(0, 0, 1.2))
    tuft((0, 0, 1.55), .8)
    rbox('Ramp', (0, -1.0, .2), (.48, .36, .06), 'Pearl', .025)
    block((0, -1.02, .44))
    status_lights([(x, -.97, .12) for x in (-.25, -.12, .01)], 'Lime', .028)
    return 1.6


def c08_bloom():
    lathe('Pot', [(0, 0), (.34, 0), (.48, .34), (.55, .5), (.52, .54), (.44, .52), (0, .52)], 'Pearl')
    torus('PotBand', (0, 0, .4), .52, .03, 'Azure')
    cyl('Stem', (0, 0, .72), .07, .4, 'GrassGel')
    for i in range(6):
        a = i * TAU / 6
        sphere('Petal', (math.cos(a) * .32, math.sin(a) * .32, 1.12), .55, 'Leaf', scale=(.32, .07, 1),
               rot=(0, math.radians(38), a))
    for i in range(5):
        a = i * TAU / 5 + .3
        sphere('InnerPetal', (math.cos(a) * .18, math.sin(a) * .18, 1.08), .32, 'PetalAqua', scale=(.35, .08, 1),
               rot=(0, math.radians(22), a))
    sphere('Bulb', (0, 0, 1.3), .27, 'SkyGlass', seg=48, rings=24)
    tuft((0, 0, 1.3), .75)
    for p in [(.55, .25, 1.45), (-.4, -.45, 1.35), (.1, .62, 1.5)]: sphere('Dew', p, .045, 'ClearGlass')
    for a in (2.2, -.9):
        sphere('BaseLeaf', (math.cos(a) * .55, math.sin(a) * .55, .45), .4, 'Leaf', scale=(.4, .08, 1),
               rot=(0, math.radians(65), a))
    tuft((.05, .1, 1.95), .7)
    cyl('LilyPad', (0, -.88, .3), .32, .03, 'GrassGel'); rod('PadStem', (0, -.88, 0), (0, -.88, .3), .04, 'GrassGel')
    block((0, -.88, .53))
    status_lights([(x, -.42, .28) for x in (-.14, 0, .14)], 'Lime', .032)
    return 2.1


def c09_aurora_arch():
    rbox('Plinth', (0, 0, .09), (2.0, .72, .18), 'Pearl', .08)
    rbox('Channel', (0, 0, .18), (1.6, .3, .03), 'SkyGlow', .012)
    bm = lathe_bm([(.82 + .12 * math.cos(i * TAU / 14), .12 * math.sin(i * TAU / 14) * 1.4) for i in range(14)], 96, closed=True)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.y < -.02], context='VERTS')   # keep the upper half
    make('AuroraArch', bm, 'Aurora', (0, 0, .18), (math.pi / 2, 0, 0))
    bm = lathe_bm([(.62 + .03 * math.cos(i * TAU / 10), .03 * math.sin(i * TAU / 10)) for i in range(10)], 96, closed=True)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.y < -.02], context='VERTS')
    make('InnerRibbon', bm, 'Chrome', (0, .05, .18), (math.pi / 2, 0, 0))
    for a in (math.radians(30), math.radians(90), math.radians(150)):
        sphere('Bead', (math.cos(a) * .62, .05, .18 + math.sin(a) * .62), .045, 'Pink')
    for x in (-.82, .82): lathe('Foot', [(0, 0), (.2, 0), (.17, .12), (0, .12)], 'Chrome', loc=(x, 0, .18))
    ico('Prism', (0, 0, .62), .24, 'Prism', scale=(.75, .75, 1.5), rot=(0, 0, .4))
    cyl('PrismSeat', (0, 0, .22), .14, .08, 'Chrome')
    for p, s in [((-1.2, -.05, .45), .9), ((-1.0, .15, .75), .6)]: tuft(p, s)
    cyl('OutputPad', (.0, -.62, .2), .26, .04, 'Chrome'); torus('PadGlow', (0, -.62, .22), .26, .015, 'Pink')
    block((0, -.62, .43))
    status_lights([(x, -.37, .19) for x in (-.6, -.45, -.3)], 'Pink', .03)
    return 1.3


def c10_jelly():
    lathe('MagPad', [(0, 0), (.72, 0), (.76, .05), (.7, .14), (.3, .18), (0, .18)], 'Chrome')
    for R in (.6, .42, .24): torus('MagRing', (0, 0, .16), R, .018, 'Cyan')
    bell = [(0, 1.82), (.25, 1.78), (.45, 1.66), (.56, 1.48), (.58, 1.28), (.54, 1.12), (.5, 1.06)]
    b = lathe('Bell', bell, 'Jelly', seg=64)
    s = b.modifiers.new('Solidify', 'SOLIDIFY'); s.thickness = .04; b.modifiers.move(1, 0)
    lathe('InnerBell', [(0, 1.6), (.22, 1.55), (.32, 1.4), (.3, 1.25)], 'Jelly', seg=48)
    cloud((0, 0, 1.15), .32, name='CoreCloud')
    for i in range(10):
        a = i * TAU / 10; r = .5
        pts = [(math.cos(a) * r, math.sin(a) * r, 1.08)]
        for k in range(1, 5):
            w = .08 * math.sin(k * 1.7 + i)
            pts.append((math.cos(a) * (r - .06 * k) + w, math.sin(a) * (r - .06 * k) - w, 1.08 - .17 * k))
        ribbon('Tendril', pts, .014, 'Pink' if i % 2 else 'Cyan')
    tuft((0, .1, 2.08), .8)
    cyl('Stub', (0, -.9, .12), .05, .24, 'Chrome'); torus('StubGlow', (0, -.9, .3), .22, .016, 'Cyan')
    block((0, -.9, .5))
    status_lights([(x, -.62, .1) for x in (-.14, 0, .14)], 'Pink', .03)
    return 2.1


CONCEPTS = [('01', 'Bubble Still', c01_bubble_still), ('02', 'Cloud Lava Lamp', c02_lava_lamp),
            ('03', 'Wind Condenser', c03_wind_condenser), ('04', 'Gel Capsule', c04_gel_capsule),
            ('05', 'Sky Balloon', c05_sky_balloon), ('06', 'Gyro Orb', c06_gyro_orb),
            ('07', 'Terrarium Dome', c07_terrarium), ('08', 'Bloom Flower', c08_bloom),
            ('09', 'Aurora Arch', c09_aurora_arch), ('10', 'Jelly Levitator', c10_jelly)]

layout(CONCEPTS, 'SkyProcessorConcepts', 'SkyProcessor')
