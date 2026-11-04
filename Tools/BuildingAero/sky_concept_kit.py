"""Shared Blender kit for the sky concept rounds (processor and essence). exec() it inside the connected Blender.

The caller sets SCENE_NAME first; the kit rebuilds that scene only, leaving every other scene alone, then
provides the concept materials (MAT), bmesh helpers, the real SkyCloud/SkyBlock meshes and layout().
"""
import bpy, bmesh, math
from mathutils import Vector, Matrix

ROOT = 'D:/PersonalProjects/the-tower-of-babel'
RES = ROOT + '/Assets/_Project/Models/Resources/'
SCENE_NAME = globals().get('SCENE_NAME', 'Aero_SkyProcessorConcepts')
TAU = math.tau

old = bpy.data.scenes.get(SCENE_NAME)
if old:
    for o in list(old.objects): bpy.data.objects.remove(o, do_unlink=True)
    for c in list(old.collection.children_recursive): bpy.data.collections.remove(c)
    bpy.data.scenes.remove(old)
for coll in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
    for item in list(coll):
        if item.users == 0: coll.remove(item)
scene = bpy.data.scenes.new(SCENE_NAME)
bpy.context.window.scene = scene

# ---------------------------------------------------------------- materials
def _mat(name):
    """Fresh node tree; an existing material is rebuilt in place so other concept scenes keep their links."""
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name); m.use_nodes = True
    nodes = m.node_tree.nodes; nodes.clear()
    b = nodes.new('ShaderNodeBsdfPrincipled'); out = nodes.new('ShaderNodeOutputMaterial')
    m.node_tree.links.new(b.outputs[0], out.inputs['Surface'])
    return m, nodes, m.node_tree.links, b


def _set(b, **kw):
    names = {'color': 'Base Color', 'metal': 'Metallic', 'rough': 'Roughness', 'ior': 'IOR', 'alpha': 'Alpha',
             'trans': 'Transmission Weight', 'coat': 'Coat Weight', 'coat_rough': 'Coat Roughness',
             'emit': 'Emission Color', 'es': 'Emission Strength', 'sss': 'Subsurface Weight',
             'film': 'Thin Film Thickness', 'film_ior': 'Thin Film IOR', 'sheen': 'Sheen Weight'}
    for k, v in kw.items():
        sock = b.inputs.get(names[k])
        if sock is not None: sock.default_value = v


def _render(m, method):
    """DITHERED + raytraced refraction for solid glass; BLENDED for thin, see-through shells."""
    for attr, val in (('surface_render_method', method), ('use_raytrace_refraction', method == 'DITHERED'),
                      ('use_transparency_overlap', True), ('use_backface_culling', False)):
        try: setattr(m, attr, val)
        except (AttributeError, TypeError): pass


def plain(name, color, method=None, **kw):
    m, n, l, b = _mat(name); _set(b, color=(*color, 1), **kw)
    if method: _render(m, method)
    return m


def ramp(nodes, stops, interp='LINEAR'):
    r = nodes.new('ShaderNodeValToRGB'); r.color_ramp.interpolation = interp
    els = r.color_ramp.elements
    while len(els) > len(stops): els.remove(els[-1])
    while len(els) < len(stops): els.new(0)
    for e, (p, c) in zip(els, stops): e.position = p; e.color = (*c, 1) if len(c) == 3 else c
    return r


def axis_value(n, l, axis, lo, hi, coord='Object'):
    """Object-space (or UV) coordinate on one axis, mapped lo..hi -> 0..1."""
    tc = n.new('ShaderNodeTexCoord'); sep = n.new('ShaderNodeSeparateXYZ'); mr = n.new('ShaderNodeMapRange')
    l.new(tc.outputs[coord], sep.inputs[0]); l.new(sep.outputs[axis], mr.inputs['Value'])
    mr.inputs['From Min'].default_value = lo; mr.inputs['From Max'].default_value = hi
    return mr.outputs['Result']


def gradient(name, axis, lo, hi, stops, emit=0, method=None, coord='Object', **kw):
    """Base colour (and optionally emission) follows an object-space axis."""
    m, n, l, b = _mat(name); _set(b, **kw)
    r = ramp(n, stops); l.new(axis_value(n, l, axis, lo, hi, coord), r.inputs[0])
    l.new(r.outputs[0], b.inputs['Base Color'])
    if emit:
        l.new(r.outputs[0], b.inputs['Emission Color']); _set(b, es=emit)
    if method: _render(m, method)
    return m


def fresnel_shell(name, body, rim, rim_alpha, body_alpha, glow=0, film=0, **kw):
    """See-through shell whose rim thickens: bubble, jelly, dome sheen."""
    m, n, l, b = _mat(name); _set(b, film=film, **kw)
    lw = n.new('ShaderNodeLayerWeight'); lw.inputs['Blend'].default_value = .35
    col = ramp(n, [(0, body), (1, rim)]); l.new(lw.outputs['Facing'], col.inputs[0])
    al = n.new('ShaderNodeMapRange'); al.inputs['To Min'].default_value = body_alpha; al.inputs['To Max'].default_value = rim_alpha
    l.new(lw.outputs['Facing'], al.inputs['Value'])
    l.new(col.outputs[0], b.inputs['Base Color']); l.new(al.outputs['Result'], b.inputs['Alpha'])
    if glow:
        l.new(col.outputs[0], b.inputs['Emission Color']); b.inputs['Emission Strength'].default_value = glow
    _render(m, 'BLENDED')
    return m


def gores(name, count, a, c, **kw):
    """Alternating vertical gores around Z (balloon envelope)."""
    m, n, l, b = _mat(name); _set(b, **kw)
    tc = n.new('ShaderNodeTexCoord'); g = n.new('ShaderNodeTexGradient'); g.gradient_type = 'RADIAL'
    mul = n.new('ShaderNodeMath'); mul.operation = 'MULTIPLY'; mul.inputs[1].default_value = count
    fr = n.new('ShaderNodeMath'); fr.operation = 'FRACT'
    r = ramp(n, [(0, a), (.5, c)], 'CONSTANT')
    l.new(tc.outputs['Object'], g.inputs['Vector']); l.new(g.outputs['Fac'], mul.inputs[0]); l.new(mul.outputs[0], fr.inputs[0])
    l.new(fr.outputs[0], r.inputs[0]); l.new(r.outputs[0], b.inputs['Base Color'])
    return m


def leaf(name):
    """Translucent leaf: deep green base to lime tip, darker wavy veins, soft glow through."""
    m, n, l, b = _mat(name); _set(b, rough=.22, coat=1, coat_rough=.05, trans=.25, sss=.2)
    r = ramp(n, [(0, (.02, .32, .08)), (.6, (.25, .78, .12)), (1, (.72, 1, .35))])
    l.new(axis_value(n, l, 'Z', -1, 1), r.inputs[0])
    tc = n.new('ShaderNodeTexCoord'); w = n.new('ShaderNodeTexWave'); w.bands_direction = 'X'
    w.inputs['Scale'].default_value = 3.5; w.inputs['Distortion'].default_value = 1.5
    l.new(tc.outputs['Object'], w.inputs['Vector'])
    vr = ramp(n, [(0, (.55, .7, .55)), (.12, (1, 1, 1))]); l.new(w.outputs['Fac'], vr.inputs[0])
    mul = n.new('ShaderNodeVectorMath'); mul.operation = 'MULTIPLY'
    l.new(r.outputs[0], mul.inputs[0]); l.new(vr.outputs[0], mul.inputs[1]); l.new(mul.outputs[0], b.inputs['Base Color'])
    l.new(r.outputs[0], b.inputs['Emission Color']); b.inputs['Emission Strength'].default_value = .25
    _render(m, 'DITHERED')
    return m


def aurora(name):
    """Emissive veil: green, teal, blue, violet, pink around the arch, streaked and fading at the feet."""
    m, n, l, b = _mat(name); _set(b, rough=.1, color=(1, 1, 1, 1))
    tc = n.new('ShaderNodeTexCoord'); sep = n.new('ShaderNodeSeparateXYZ'); l.new(tc.outputs['Object'], sep.inputs[0])
    at = n.new('ShaderNodeMath'); at.operation = 'ARCTAN2'; l.new(sep.outputs['Z'], at.inputs[0]); l.new(sep.outputs['X'], at.inputs[1])
    mr = n.new('ShaderNodeMapRange'); mr.inputs['From Min'].default_value = 0; mr.inputs['From Max'].default_value = math.pi
    l.new(at.outputs[0], mr.inputs['Value'])
    r = ramp(n, [(0, (.25, 1, .55)), (.25, (.1, .95, .85)), (.5, (.15, .5, 1)), (.75, (.6, .35, 1)), (1, (1, .45, .8))])
    l.new(mr.outputs['Result'], r.inputs[0])
    noise = n.new('ShaderNodeTexWave'); noise.bands_direction = 'Z'; noise.inputs['Scale'].default_value = 6
    noise.inputs['Distortion'].default_value = 4; l.new(tc.outputs['Object'], noise.inputs['Vector'])
    streak = ramp(n, [(0, (.35, .35, .35)), (1, (1, 1, 1))]); l.new(noise.outputs['Fac'], streak.inputs[0])
    mul = n.new('ShaderNodeVectorMath'); mul.operation = 'MULTIPLY'
    l.new(r.outputs[0], mul.inputs[0]); l.new(streak.outputs[0], mul.inputs[1])
    l.new(mul.outputs[0], b.inputs['Emission Color']); l.new(r.outputs[0], b.inputs['Base Color'])
    b.inputs['Emission Strength'].default_value = 2.4
    lw = n.new('ShaderNodeLayerWeight'); lw.inputs['Blend'].default_value = .4
    al = n.new('ShaderNodeMapRange'); al.inputs['To Min'].default_value = .95; al.inputs['To Max'].default_value = .5
    l.new(lw.outputs['Facing'], al.inputs['Value']); l.new(al.outputs['Result'], b.inputs['Alpha'])
    _render(m, 'BLENDED')
    return m


MAT = {
    'Chrome': plain('Concept_Chrome', (.92, .95, .98), metal=1, rough=.06),
    'Pearl': plain('Concept_Pearl', (.93, .96, 1), rough=.3, coat=1, coat_rough=.03, sheen=.2),
    'Azure': plain('Concept_Azure', (.02, .3, .72), rough=.2, coat=1, coat_rough=.02),
    'Cloud': plain('Concept_Cloud', (.97, .98, 1), rough=.65, sss=.35, emit=(.85, .9, 1, 1), es=.12),
    'SkyGlass': plain('Concept_SkyGlass', (.72, .88, 1), 'DITHERED', rough=0, trans=1, ior=1.45),
    'ClearGlass': plain('Concept_ClearGlass', (.95, .99, 1), 'DITHERED', rough=0, trans=1, ior=1.3),
    'SkyGlow': plain('Concept_SkyGlow', (.6, .82, 1), emit=(.5, .75, 1, 1), es=2.5),
    'Cyan': plain('Concept_CyanGlow', (.3, 1, 1), emit=(.2, .9, 1, 1), es=2.2),
    'Pink': plain('Concept_PinkGlow', (1, .55, .9), emit=(1, .4, .85, 1), es=2),
    'Lime': plain('Concept_LimeGlow', (.55, 1, .3), emit=(.55, 1, .3, 1), es=3),
    # 01 Bubble Still: thin-film soap bubble
    'Bubble': fresnel_shell('Concept_Bubble', (1, 1, 1, 1), (1, .9, 1, 1), .55, .05, film=520, rough=0, ior=1.05, film_ior=1.33),
    # 02 Cloud Lava Lamp: cobalt candy metal and glowing blue liquid
    'Cobalt': plain('Concept_Cobalt', (.03, .16, .75), metal=.9, rough=.14, coat=1, coat_rough=.02),
    'LampLiquid': gradient('Concept_LampLiquid', 'Z', .6, 2.3, [(0, (.05, .35, 1)), (1, (.5, .85, 1))], emit=.15,
                           method='BLENDED', rough=0, alpha=.5, ior=1.33),
    'Wax': plain('Concept_CloudWax', (1, 1, 1), rough=.35, sss=.6, emit=(.9, .95, 1, 1), es=.5),
    # 03 Wind Condenser: frosted blades and grass gel
    'Frost': plain('Concept_FrostBlade', (.82, .95, 1), 'DITHERED', rough=.38, trans=.92, ior=1.4, coat=1),
    'GrassGel': gradient('Concept_GrassGel', 'Z', -.05, .12, [(0, (.05, .45, .05)), (1, (.45, .95, .2))], rough=.15, coat=1, coat_rough=.02),
    # 04 Gel Capsule: translucent bondi-blue candy plastic
    'Candy': plain('Concept_BondiCandy', (0, .62, .74), 'DITHERED', rough=.08, trans=.7, ior=1.5, coat=1, coat_rough=0),
    # 05 Sky Balloon: striped gores and woven basket
    'Gores': gores('Concept_BalloonGores', 12, (.98, .99, 1), (.2, .58, 1), rough=.18, coat=1, coat_rough=.02),
    'Wicker': gores('Concept_PearlWicker', 40, (.95, .9, .75), (.8, .7, .5), rough=.5, coat=.5),
    # 06 Gyro Orb: deep gel gradient and a glowing core
    'GelDeep': gradient('Concept_GelDeep', 'Z', 0, .45, [(0, (.01, .06, .35)), (1, (.1, .6, 1))], rough=.12, coat=1, coat_rough=0),
    'GelRing': gradient('Concept_GelRing', 'X', -.6, .6, [(0, (.0, .85, 1)), (.5, (.1, .3, 1)), (1, (.0, .85, 1))], emit=.4,
                        rough=.1, coat=1, coat_rough=0),
    'Core': fresnel_shell('Concept_CloudCore', (.9, .95, 1, 1), (.2, .6, 1, 1), 1, .9, glow=.7, rough=.5),
    # 07 Terrarium Dome: clear dome sheen, grass, bark, pond
    'Dome': fresnel_shell('Concept_DomeGlass', (.9, 1, 1, 1), (.55, .9, 1, 1), .45, .04, film=300, rough=0, ior=1.2),
    'Grass': gradient('Concept_IslandGrass', 'Z', -.2, .2, [(0, (.1, .35, .05)), (1, (.4, .9, .18))], rough=.4, sheen=.6),
    'Bark': plain('Concept_WhiteBark', (.9, .88, .82), rough=.4, coat=.6),
    'Pond': plain('Concept_PondGlass', (.15, .7, 1), rough=0, coat=1, emit=(.1, .6, 1, 1), es=.6),
    # 08 Bloom Flower: translucent leaves
    'Leaf': leaf('Concept_LeafPetal'),
    'PetalAqua': gradient('Concept_PetalAqua', 'Z', -1, 1, [(0, (.6, .95, 1)), (1, (1, 1, 1))], method='DITHERED',
                          rough=.15, trans=.6, coat=1),
    # 09 Aurora Arch
    'Aurora': aurora('Concept_Aurora'),
    'Prism': plain('Concept_Prism', (.92, .97, 1), 'DITHERED', rough=0, trans=1, ior=1.6, film=380, film_ior=1.5),
    # 10 Jelly Levitator
    'Jelly': fresnel_shell('Concept_Jelly', (.75, .6, 1, 1), (1, .45, .85, 1), .8, .15, glow=.45, film=450, rough=.15, ior=1.1),
}

# ---------------------------------------------------------------- mesh helpers (bmesh, no operators)
COLL = None
ROOTOBJ = None


def make(name, bm, mat, loc=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1), smooth=True, subsurf=0, parent=None):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    if isinstance(mat, (list, tuple)):
        for mm in mat: me.materials.append(MAT[mm])
    elif mat: me.materials.append(MAT[mat])
    if smooth: me.shade_smooth()
    o = bpy.data.objects.new(name, me); COLL.objects.link(o)
    o.parent = parent or ROOTOBJ; o.location = loc; o.rotation_euler = rot; o.scale = scale
    if subsurf:
        s = o.modifiers.new('Subsurf', 'SUBSURF'); s.levels = s.render_levels = subsurf
    return o


def use_mesh(name, me, loc, rot=(0, 0, 0), scale=1, parent=None):
    o = bpy.data.objects.new(name, me); COLL.objects.link(o)
    o.parent = parent or ROOTOBJ; o.location = loc; o.rotation_euler = rot; o.scale = (scale,) * 3 if isinstance(scale, (int, float)) else scale
    return o


def lathe_bm(prof, seg=48, closed=False):
    bm = bmesh.new(); rings = []
    for r, z in prof:
        rings.append([bm.verts.new((r * math.cos(i * TAU / seg), r * math.sin(i * TAU / seg), z)) for i in range(seg)])
    pairs = list(zip(rings[:-1], rings[1:])) + ([(rings[-1], rings[0])] if closed else [])
    for a, b in pairs:
        for i in range(seg):
            bm.faces.new((a[i], a[(i + 1) % seg], b[(i + 1) % seg], b[i]))
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def lathe(name, prof, mat, loc=(0, 0, 0), seg=48, subsurf=1, rot=(0, 0, 0), scale=(1, 1, 1), closed=False, parent=None):
    return make(name, lathe_bm(prof, seg, closed), mat, loc, rot, scale, subsurf=subsurf, parent=parent)


def sphere(name, loc, r, mat, scale=(1, 1, 1), rot=(0, 0, 0), seg=32, rings=16, parent=None):
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=r)
    return make(name, bm, mat, loc, rot, scale, parent=parent)


def torus(name, loc, R, r, mat, rot=(0, 0, 0), seg=64, minor=14, scale=(1, 1, 1), parent=None):
    prof = [(R + r * math.cos(i * TAU / minor), r * math.sin(i * TAU / minor)) for i in range(minor)]
    return make(name, lathe_bm(prof, seg, closed=True), mat, loc, rot, scale, parent=parent)


def cyl(name, loc, r, depth, mat, top=None, seg=32, rot=(0, 0, 0), parent=None):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r, radius2=r if top is None else top, depth=depth)
    return make(name, bm, mat, loc, rot, parent=parent, smooth=True)


def rod(name, p0, p1, r, mat, seg=12, parent=None):
    p0, p1 = Vector(p0), Vector(p1); d = p1 - p0
    q = Vector((0, 0, 1)).rotation_difference(d.normalized())
    o = cyl(name, (p0 + p1) / 2, r, d.length, mat, seg=seg, parent=parent)
    o.rotation_mode = 'QUATERNION'; o.rotation_quaternion = q
    return o


def rbox(name, loc, size, mat, bev=.04, rot=(0, 0, 0), parent=None):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1)
    for v in bm.verts: v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
    bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bev, offset_type='OFFSET', segments=4, profile=.5, affect='EDGES', clamp_overlap=True)
    return make(name, bm, mat, loc, rot, parent=parent)


def ico(name, loc, r, mat, scale=(1, 1, 1), rot=(0, 0, 0), sub=1, parent=None):
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=r)
    return make(name, bm, mat, loc, rot, scale, smooth=False, parent=parent)


def ribbon(name, pts, depth, mat, parent=None):
    cu = bpy.data.curves.new(name, 'CURVE'); cu.dimensions = '3D'; cu.bevel_depth = depth; cu.bevel_resolution = 3
    cu.resolution_u = 16
    sp = cu.splines.new('BEZIER'); sp.bezier_points.add(len(pts) - 1)
    for bp, p in zip(sp.bezier_points, pts):
        bp.co = p; bp.handle_left_type = bp.handle_right_type = 'AUTO'
    cu.materials.append(MAT[mat])
    o = bpy.data.objects.new(name, cu); COLL.objects.link(o); o.parent = parent or ROOTOBJ
    return o


def empty(name, loc, parent=None):
    o = bpy.data.objects.new(name, None); COLL.objects.link(o); o.parent = parent or ROOTOBJ; o.location = loc
    o.empty_display_size = .15
    return o


def status_lights(points, mat='SkyGlow', r=.04):
    for i, p in enumerate(points): sphere('StatusLight_%d' % i, p, r, mat, seg=16, rings=8)


# ---------------------------------------------------------------- the real sky essence and sky block
def import_meshes():
    """Bake SkyCloud.fbx and SkyBlock.fbx into standalone, centred meshes with concept materials."""
    tmp = bpy.data.scenes.new('_SkyImport'); win = bpy.context.window; win.scene = tmp
    for f in ('SkyCloud', 'SkyBlock'):
        bpy.ops.import_scene.fbx(filepath=RES + f + '.fbx')
    out = {}
    swap = {'Workshop_Cloud': 'Cloud', 'Workshop_SkyGlass': 'SkyGlass', 'Workshop_SkyGlow': 'SkyGlow', 'Workshop_Pearl': 'Pearl'}
    for o in tmp.objects:
        if o.type != 'MESH' or not (o.name.startswith(('Cloud', 'Tuft_1', 'Block'))): continue
        key = 'Block' if o.name.startswith('Block') else 'Tuft' if o.name.startswith('Tuft') else 'Cloud'
        me = o.data.copy(); me.transform(o.matrix_world)
        vs = [v.co for v in me.vertices]
        lo = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
        hi = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
        c = (lo + hi) / 2
        if key == 'Cloud': c.z = lo.z            # cloud mound sits on its base
        me.transform(Matrix.Translation(-c))
        for i, m in enumerate(me.materials):
            base = m.name.split('.')[0] if m else ''
            me.materials[i] = MAT[swap.get(base, 'Cloud')]
        me.name = 'Sky' + key + '_Mesh'; out[key] = me
    for o in list(tmp.objects): bpy.data.objects.remove(o, do_unlink=True)
    win.scene = scene; bpy.data.scenes.remove(tmp)
    return out


SKY = import_meshes()      # Block 0.42 m cube, Tuft ~0.36 m, Cloud 1.54 m mound


def block(loc, rot=(0, 0, 0)):
    use_mesh('SkyBlock', SKY['Block'], loc, rot)
    empty('BlockSocket', loc)


def tuft(loc, s=1., rot=(0, 0, 0)):
    return use_mesh('SkyEssence_Input', SKY['Tuft'], loc, rot, s)


def cloud(loc, s, rot=(0, 0, 0), name='CloudMass'):
    return use_mesh(name, SKY['Cloud'], loc, rot, s)




def layout(concepts, top_name, prefix, spacing=3.4, cols=5, row_gap=4.2):
    """Build each concept under its own root and collection, in a grid. Each builder returns its height."""
    global COLL, ROOTOBJ
    top = bpy.data.collections.new(top_name); scene.collection.children.link(top)
    for i, (num, title, build) in enumerate(concepts):
        COLL = bpy.data.collections.new('%s_%s' % (prefix, num)); top.children.link(COLL)
        ROOTOBJ = bpy.data.objects.new('%s_%s_%s' % (prefix, num, title.replace(' ', '')), None); COLL.objects.link(ROOTOBJ)
        ROOTOBJ.location = ((i % cols - (cols - 1) / 2) * spacing, (i // cols) * row_gap, 0)
        root = ROOTOBJ; root['concept'] = '%s %s' % (num, title)
        root['height'] = build()
    print('Built', {c.name: sum(len(o.data.polygons) for o in c.objects if o.type == 'MESH') for c in top.children})


# ---------------------------------------------------------------- nature and wind (Round02+)
import random
MAT.update({
    'GrassBlade': gradient('Concept_GrassBlade', 'Y', 0, 1, [(0, (.03, .28, .05)), (.55, (.2, .7, .1)), (1, (.75, 1, .35))],
                           coord='UV', rough=.3, coat=.6, sss=.15),
    'Meadow': gradient('Concept_MeadowGloss', 'Z', 0, .5, [(0, (.04, .36, .06)), (.6, (.22, .72, .1)), (1, (.5, .95, .25))],
                       rough=.18, coat=1, coat_rough=.03),
    'Moss': plain('Concept_Moss', (.07, .36, .05), rough=.6, sheen=.25),
    'Rock': plain('Concept_SkyRock', (.5, .58, .7), rough=.45, coat=.35, coat_rough=.1),
    'Wind': fresnel_shell('Concept_WindRibbon', (1, 1, 1, 1), (.55, .9, 1, 1), .75, .12, glow=.6, rough=.2),
    'Daisy': plain('Concept_DaisyPetal', (1, 1, 1), rough=.3, sss=.4),
    'Pollen': plain('Concept_Pollen', (1, .8, .1), rough=.4, emit=(1, .75, .1, 1), es=.3),
    'Stone': plain('Concept_WindStone', (.86, .88, .9), rough=.55, coat=.3),
    'Mirror': plain('Concept_MirrorPond', (.18, .5, .95), metal=.4, rough=.02, coat=1, coat_rough=0),
    'SkyCrystal': plain('Concept_SkyCrystal', (.65, .85, 1), 'DITHERED', rough=0, trans=.9, ior=1.55, film=320,
                        emit=(.5, .75, 1, 1), es=.35),
    'Reed': plain('Concept_Reed', (.35, .6, .2), rough=.4),
    'Twig': plain('Concept_PaleTwig', (.88, .84, .76), rough=.45, coat=.5),
})


def into(o):
    """Following helpers parent to o (e.g. the Essence or Base group of a resource node)."""
    global ROOTOBJ
    ROOTOBJ = o
    return o


def terrain(name, radius, hfn, mat, loc=(0, 0, 0), rings=22, seg=72, skirt=True, subsurf=1):
    """Polar ground patch whose height is hfn(x, y); a skirt drops the rim to z = 0."""
    bm = bmesh.new(); centre = bm.verts.new((0, 0, hfn(0, 0))); prev = None
    for i in range(1, rings + 1):
        r = radius * i / rings
        ring = [bm.verts.new((r * math.cos(a), r * math.sin(a), hfn(r * math.cos(a), r * math.sin(a))))
                for a in [k * TAU / seg for k in range(seg)]]
        for k in range(seg):
            if prev is None: bm.faces.new((centre, ring[k], ring[(k + 1) % seg]))
            else: bm.faces.new((prev[k], ring[k], ring[(k + 1) % seg], prev[(k + 1) % seg]))
        prev = ring
    if skirt:
        low = [bm.verts.new((v.co.x * 1.02, v.co.y * 1.02, 0)) for v in prev]
        for k in range(seg): bm.faces.new((prev[k], low[k], low[(k + 1) % seg], prev[(k + 1) % seg]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return make(name, bm, mat, loc, subsurf=subsurf)


def bumps(*hills):
    """Height function from gaussian hills: (x, y, height, radius)."""
    return lambda x, y: sum(h * math.exp(-((x - cx) ** 2 + (y - cy) ** 2) / (r * r)) for cx, cy, h, r in hills)


def grass_patch(name, count, rmin, rmax, h=(.14, .32), ground=lambda x, y: 0, seed=1, centre=(0, 0), width=.022,
                lean=.35, swirl=0., wind=None, mat='GrassBlade', loc=(0, 0, 0), parent=None):
    """Curved, tapered blades in one mesh. swirl leans them around the centre, wind=(dx, dy) leans them one way."""
    rng = random.Random(seed); bm = bmesh.new(); uv = bm.loops.layers.uv.new('UVMap')
    for _ in range(count):
        r = math.sqrt(rng.uniform(rmin * rmin, rmax * rmax)); a = rng.uniform(0, TAU)
        x, y = centre[0] + r * math.cos(a), centre[1] + r * math.sin(a); z = ground(x, y) - .01
        hh = rng.uniform(*h); face = rng.uniform(0, TAU)
        if wind: lx, ly = wind[0] + rng.uniform(-.15, .15), wind[1] + rng.uniform(-.15, .15)
        elif swirl: lx, ly = -math.sin(a) * swirl + math.cos(a) * .1, math.cos(a) * swirl + math.sin(a) * .1
        else: lx, ly = math.cos(face) * lean * rng.uniform(.3, 1), math.sin(face) * lean * rng.uniform(.3, 1)
        side = Vector((-ly, lx, 0)).normalized() if (lx or ly) else Vector((1, 0, 0))
        rows = []
        for t in (0, .35, .7, 1):
            c = Vector((x + lx * hh * t * t, y + ly * hh * t * t, z + hh * t * (1 - .25 * t * t)))
            w = width * (1 - t) ** .8
            rows.append((bm.verts.new(c - side * w), bm.verts.new(c + side * w), t))
        for (a0, b0, t0), (a1, b1, t1) in zip(rows[:-1], rows[1:]):
            f = bm.faces.new((a0, b0, b1, a1))
            for loop, u, v in zip(f.loops, (0, 1, 1, 0), (t0, t0, t1, t1)): loop[uv].uv = (u, v)
    return make(name, bm, mat, loc, parent=parent)


def wind_ribbon(name, pts, width, mat='Wind', parent=None):
    """Flat translucent ribbon along a smooth curve (a gust of wind)."""
    o = ribbon(name, pts, 0, mat, parent)
    o.data.extrude = width / 2; o.data.twist_mode = 'MINIMUM'
    return o


def spiral(centre, r0, r1, z0, z1, turns, n, phase=0.):
    return [(centre[0] + (r0 + (r1 - r0) * i / (n - 1)) * math.cos(phase + TAU * turns * i / (n - 1)),
             centre[1] + (r0 + (r1 - r0) * i / (n - 1)) * math.sin(phase + TAU * turns * i / (n - 1)),
             z0 + (z1 - z0) * i / (n - 1)) for i in range(n)]


def pebbles(count, rmin, rmax, size=(.06, .14), seed=3, mat='Stone', ground=lambda x, y: 0, flat=.55):
    rng = random.Random(seed)
    for _ in range(count):
        r = rng.uniform(rmin, rmax); a = rng.uniform(0, TAU); s = rng.uniform(*size)
        x, y = r * math.cos(a), r * math.sin(a)
        sphere('Pebble', (x, y, ground(x, y) + s * flat * .4), s, mat, scale=(1, rng.uniform(.7, 1), flat),
               rot=(0, 0, rng.uniform(0, TAU)), seg=12, rings=8)


def daisy(loc, s=1., tilt=(0, 0, 0)):
    rod('DaisyStem', (loc[0], loc[1], loc[2] - .1 * s), loc, .006 * s, 'Reed', seg=6)
    head = empty('Daisy', loc); head.rotation_euler = tilt
    for i in range(7):
        a = i * TAU / 7
        sphere('Petal', (math.cos(a) * .028 * s, math.sin(a) * .028 * s, 0), .026 * s, 'Daisy', scale=(1, .45, .18),
               rot=(0, 0, a), seg=10, rings=6, parent=head)
    sphere('Pollen', (0, 0, .006 * s), .013 * s, 'Pollen', scale=(1, 1, .6), seg=10, rings=6, parent=head)


def tuft_trail(p0, p1, n, s0, s1, bow=(0, 0, 0)):
    """A stream of shrinking cloud tufts from p0 to p1 bowed sideways: wind carrying cloud."""
    p0, p1, bow = Vector(p0), Vector(p1), Vector(bow)
    for i in range(n):
        t = i / max(n - 1, 1)
        tuft(p0.lerp(p1, t) + bow * 4 * t * (1 - t), s0 + (s1 - s0) * t, (0, 0, t * 2.3))
