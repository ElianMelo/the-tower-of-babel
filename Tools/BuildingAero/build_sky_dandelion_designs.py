"""Sky essence Round02: ten Frutiger Aero dandelion clocks for the Cloud Dandelions node, for review.

Requested 2026-11-15. The user supplied a photo of a real dandelion clock and asked for ten proposals that keep
the current node (SkyDandelions.fbx, see Documentation/FrutigerAero/Resources/SKY_WORKSHOP.md) but look more like
a real dandelion and more Frutiger Aero, so a final design can be chosen before the animation work.

Every design keeps the production layout and pivots, so the existing SkyBloom feedback still fits:
  Base     Rosette (toothed leaves, crown, bud, yellow bloom, pebbles) and Grass - stays during cooldown
  Essence  Bloom (centre stalk) with child stalks Stalk_1 and Stalk_2 at the production bases, heights and leans,
           and five drifting seeds Seed_0..4. Each stalk is an empty at its base holding Stem and Head.
What changes is the head: real parachute seeds (achene, beak and a pappus umbrella of fine bristles) instead of
cloud balls, toothed "lion's tooth" leaves instead of ellipses, and a Frutiger finish per design:
  01 Silk Clock         realistic silky clock, aqua achenes, glossy leaves
  02 Dewdrop Clock      glass dew beads on the seeds, the leaves and the drifting seeds
  03 Bubble Clock       each head and drifting seed sealed in an iridescent soap bubble
  04 Fiber Optic Clock  bristles fade to glowing cyan tips, like a fibre-optic lamp; fireflies
  05 Iridescent Clock   pastel thin-film bristle tips that vary seed by seed, pearl achenes
  06 Cloud Halo Clock   keeps a cloud core inside each head, silk seeds and a soft halo rim; cloud seeds
  07 Crystal Clock      sky-crystal achenes, frosted glass fibres, glass stems, crystals in the base
  08 Half-Blown Clock   a full head, a half-blown head with a pitted dome, a closed head; seeds ride a breeze
  09 Jelly Clock        Y2K candy-gel stems and beads, fewer, softer toy-like bristles
  10 Sparkle Clock      silk heads with star glints, bokeh orbs and pollen motes
Static concepts. Builds the scene Aero_SkyDandelionDesigns with the shared sky_concept_kit.py. Run it inside the
connected Blender (MCP socket), or headless (Blender 5.3 has a window in background mode, so the kit works):
  blender -b --factory-startup --python-expr "exec(open(r'<this file>').read()); <render globals>; exec(open(r'render_sky_processor_concepts.py').read())"
with the render globals listed in Documentation/FrutigerAero/Resources/SKY_ESSENCE_CONCEPTS.md (Round02).
"""
import bpy, bmesh, math, random
from mathutils import Vector, Matrix, Euler

KIT = 'D:/PersonalProjects/the-tower-of-babel/Tools/BuildingAero/sky_concept_kit.py'
SCENE_NAME = 'Aero_SkyDandelionDesigns'
exec(open(KIT, encoding='utf-8').read(), globals())

DKIT = 'D:/PersonalProjects/the-tower-of-babel/Tools/BuildingAero/dandelion_kit.py'
exec(open(DKIT, encoding='utf-8').read(), globals())


def node(st=None, bkw=None, per=None, head_fx=None, after_head=None, seed_fx=None, after_seed=None, scene_fx=None,
         seeds=SEEDS, seed_up=(.3, -.1, 1), seed=11):
    """Build one design under the current concept root with the production hierarchy."""
    st = {**BASE_ST, **(st or {})}; rng = random.Random(seed)
    B, E = empty('Base', (0, 0, 0)), empty('Essence', (0, 0, 0))
    base(B, rng, **(bkw or {}))
    bloom = None; b0 = Vector(STALKS[0][1])
    for name, b, h, lean, R in STALKS:
        s = {**st, **(per or {}).get(name, {})}; b = Vector(b)
        pivot = empty(name, tuple(b - (b0 if bloom else Vector())), bloom or E)
        top = Vector((b.x + lean[0], b.y + lean[1], h))
        mid = Vector(((b.x + top.x) / 2 + lean[0] * .2, (b.y + top.y) / 2 + lean[1] * .2, h * .55))
        pts = [tuple(p - b) for p in (b, mid, top - Vector((0, 0, R * .06)))]
        ribbon('Stem', pts, s['stem_r'], s['stem'], parent=pivot)
        if s.get('stem_core'): ribbon('StemCore', pts, s['stem_r'] * .4, s['stem_core'], parent=pivot)
        g = Geo(); sd, tips = s.get('head', clock)(g, top, R, s, rng)
        if s.get('bracts', True): bracts(g, top, R, s, rng, s.get('bract_len', 1.))
        if head_fx: head_fx(g, top, R, sd, tips, rng, name)
        g.done('Head', b, pivot, smooth=s['smooth'])
        if after_head: after_head(pivot, top - b, R)
        bloom = bloom or pivot
    for i, p in enumerate(seeds):
        g = Geo(); up = (Vector(seed_up) + Vector((rng.uniform(-.25, .25), rng.uniform(-.25, .25), 0))).normalized()
        tips = parachute(g, p, up, st, rng)
        if seed_fx: seed_fx(g, Vector(p), up, tips, rng)
        o = g.done('Seed_%d' % i, p, E, loc=p, smooth=st['smooth'])
        if after_seed: after_seed(o, up)
    if scene_fx: scene_fx(B, E, rng)
    return 1.15


def free(parent, name, fill):
    """A loose decorative mesh (bubbles, bokeh, motes) under parent."""
    g = Geo(); fill(g); g.done(name, parent=parent)


# ---------------------------------------------------------------- the ten designs
def d01_silk():
    return node(bkw=dict(dew=3))


def d02_dewdrop():
    def dew(g, top, R, seeds, tips, rng, name):
        for pb, d, _ in seeds:
            if rng.random() < .35: g.ball(pb + d * .008, .011, 'Dew', seg=14, rings=8)
        big = [t for t in tips if rng.random() < .04]
        for e, d, _ in big: g.ball(e + d * .006, .014, 'Dew', scale=(1, 1, 1.15), seg=14, rings=8)
        for e, d, _ in big[:3]: g.star(e + d * .006 + CAM * .016, .035, rot=rng.uniform(0, .5))

    def seed_dew(g, p, up, tips, rng):
        g.ball(p - up * .13, .014, 'Dew', seg=14, rings=8)
        g.ball(tips[3][0], .01, 'Dew', seg=12, rings=8)
    return node(st=dict(n=90, k=12), bkw=dict(dew=26, dew_mat='Dew', dew_r=(.012, .028), pebble_mats=('AquaPebble', 'Pearl')), head_fx=dew, seed_fx=seed_dew, seed=21)


def d03_bubble():
    def shell(g, top, R, seeds, tips, rng, name): g.ball(top, R * 1.02, 'Bubble', seg=48, rings=24)

    def seed_bubble(g, p, up, tips, rng): g.ball(p - up * .05, .095, 'Bubble', seg=32, rings=16)
    return node(st=dict(n=90, k=10), bkw=dict(bubbles=3), head_fx=shell, seed_fx=seed_bubble, seed=31)


def d04_fiber():
    def glow(g, top, R, seeds, tips, rng, name):
        for e, d, _ in tips: g.octa(e, .005, 'CyanTip')

    def seed_glow(g, p, up, tips, rng):
        for e, d, _ in tips: g.octa(e, .006, 'CyanTip')

    def fireflies(B, E, rng):
        free(E, 'Fireflies', lambda g: [g.ball((rng.uniform(-.8, .8), rng.uniform(-.6, .6), rng.uniform(.25, 1.25)), .016,
                                              'LimeTip' if k % 2 else 'CyanTip', seg=10, rings=6) for k in range(10)])
    return node(st=dict(pap='FiberPappus', rec='CyanTip', rec_r=.04, ach='Azure'), head_fx=glow, seed_fx=seed_glow, scene_fx=fireflies, seed=41)


def d05_iridescent():
    def bubbles(B, E, rng):
        free(E, 'Bubbles', lambda g: [g.ball(p, r, 'Bubble', seg=32, rings=16) for p, r in
                                      (((.55, -.55, .55), .05), ((-.7, -.05, .45), .04), ((.05, -.45, 1.3), .035))])
    return node(st=dict(pap='IriPappus', ach='Pearl', rec='Prism'), scene_fx=bubbles, seed=51)


def d06_cloud_halo():
    def halo(g, top, R, seeds, tips, rng, name): g.ball(top, R * 1.03, 'Halo', seg=40, rings=20)

    def core(pivot, top, R): use_mesh('CloudCore', SKY['Tuft'], tuple(top), (0, 0, R * 9), R * 1.25 / .36, parent=pivot)

    def cloud_seed(o, up): use_mesh('SeedCloud', SKY['Tuft'], tuple(up * .012), (0, 0, 1.1), .16, parent=o)
    return node(st=dict(n=70, k=9, pl=.36), head_fx=halo, after_head=core, after_seed=cloud_seed, seed=61)


def d07_crystal():
    return node(st=dict(ach='Crystal', ach_w=.022, ach_len=.5, beak='FrostFiber', pap='FrostFiber', rec='Crystal', rec_ico=True,
                        rec_r=.05, smooth=False, stem='SkyGlass', stem_core='StemGel', stem_r=.013),
                bkw=dict(crystals=6, pebble_mats=('AquaPebble', 'Pearl')), seed=71)


def d08_half_blown():
    def half(g, top, R, s, rng):
        sd, tips = clock(g, top, R, {**s, 'n': 110}, rng, keep=lambda d, r: d.dot(BLOWN) < -.2 or r.random() < .05)
        g.ball(top + Vector((0, 0, .004)), .046, 'Pits', scale=(1, 1, .78), seg=28, rings=14)
        return sd, tips

    def closed(g, top, R, s, rng):
        c = top + Vector((0, 0, -.012)); tips = []
        g.ball(c, .036, 'StemGel', scale=(1, 1, 1.9), seg=16, rings=10)
        for j in range(10):
            o = Vector((math.cos(j * TAU / 10), math.sin(j * TAU / 10), 0))
            g.strand([c + o * .03 + Vector((0, 0, -.062)), c + o * .04 + Vector((0, 0, -.01)), c + o * .03 + Vector((0, 0, .045)),
                      c + o * .016 + Vector((0, 0, .07))], (.008, .009, .006, .002), 'Bract')
            g.strand([c + o * .02 + Vector((0, 0, -.068)), c + o * .045 + Vector((0, 0, -.075)), c + o * .055 + Vector((0, 0, -.1))],
                     (.006, .005, .001), 'Bract')
        for j in range(48):
            a = rng.uniform(0, TAU); sp = rng.uniform(0, .5); dv = Vector((math.cos(a) * sp, math.sin(a) * sp, 1)).normalized()
            p0 = c + Vector((0, 0, .058)); L = rng.uniform(.05, .09)
            g.strand([p0, p0 + dv * L * .5, p0 + (dv + Vector((math.cos(a), math.sin(a), 0)) * .35).normalized() * L],
                     (.0018, .0013, .0005), s['pap'])
        return [], tips

    def breeze(B, E, rng):
        wind_ribbon('Breeze', [(.3, -.12, .78), (.58, -.26, .9), (.8, -.2, 1.08), (.98, -.32, 1.28)], .025, parent=E)
    return node(st=dict(n=125), per={'Stalk_1': dict(head=half, bract_len=1.3), 'Stalk_2': dict(head=closed, bracts=False)},
                scene_fx=breeze, seeds=[(.6, -.22, 1.02), (.78, -.18, 1.16), (.92, -.3, 1.34), (.5, -.32, .86), (-.45, .4, 1.12)],
                seed_up=(.45, -.15, 1), seed=81)


def d09_jelly():
    def beads(g, top, R, seeds, tips, rng, name):
        for e, d, _ in tips: g.ball(e, .0085, 'Pearl', seg=6, rings=4)

    def seed_beads(g, p, up, tips, rng):
        for e, d, _ in tips[::2]: g.ball(e, .0075, 'Pearl', seg=6, rings=4)
    return node(st=dict(n=56, k=7, theta=58, pl=.42, pap='PappusSoft', bw=.0034, ach='Candy', ach_sides=6, ach_w=.016,
                        rec='Candy', rec_r=.05, beak='PappusSoft', beak_w=.0034, stem='LimeJelly', stem_r=.016),
                bkw=dict(leaf_mat='GelLeaf', pebble_mats=('Candy', 'Pearl', 'Azure')), head_fx=beads, seed_fx=seed_beads, seed=91)


def d10_sparkle():
    def glints(g, top, R, seeds, tips, rng, name):
        lit = [t for t in tips if t[1].dot(CAM) > .45 and t[1].z > -.2]
        for e, d, _ in rng.sample(lit, min(4, len(lit))): g.star(e + d * .01 + CAM * .02, rng.uniform(.05, .08), rot=rng.uniform(0, .5))
        if name == 'Bloom': g.star(top + R * (CAM * .6 + Vector((0, 0, .7))).normalized(), .15, rot=.25)

    def seed_glint(g, p, up, tips, rng):
        if rng.random() < .7: g.star(p + up * .01 + CAM * .02, .05, rot=rng.uniform(0, .5))

    def bokeh(B, E, rng):
        def fill(g):
            for k in range(13):
                g.disc((rng.uniform(-.85, .85), rng.uniform(-.5, .7), rng.uniform(.3, 1.45)), rng.uniform(.03, .09),
                       ('BokehCyan', 'BokehLime', 'BokehWhite')[k % 3])
            for k in range(12):
                g.ball((rng.uniform(-.7, .7), rng.uniform(-.5, .5), rng.uniform(.25, 1.3)), .006, 'Pollen', seg=8, rings=5)
        free(E, 'Bokeh', fill)
    return node(st=dict(n=110), head_fx=glints, seed_fx=seed_glint, scene_fx=bokeh, seed=101)


DESIGNS = [('01', 'Silk Clock', d01_silk), ('02', 'Dewdrop Clock', d02_dewdrop), ('03', 'Bubble Clock', d03_bubble),
           ('04', 'Fiber Optic Clock', d04_fiber), ('05', 'Iridescent Clock', d05_iridescent),
           ('06', 'Cloud Halo Clock', d06_cloud_halo), ('07', 'Crystal Clock', d07_crystal),
           ('08', 'Half Blown Clock', d08_half_blown), ('09', 'Jelly Clock', d09_jelly), ('10', 'Sparkle Clock', d10_sparkle)]
ONLY = globals().get('ONLY')
layout([d for d in DESIGNS if not ONLY or d[0] in ONLY], 'SkyDandelionDesigns', 'SkyDandelion', spacing=3.2)
