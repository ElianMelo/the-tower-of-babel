"""Review renders for build_sky_processor_concepts.py (run inside the connected Blender, after the build).

Adds a sky-gradient world, sun and fill lights, a soft floor and cameras to 'Aero_SkyProcessorConcepts',
renders one 3/4 close-up per concept plus a lineup of all ten, and writes the scene's data to
ArtSource/Resources/SkyProcessorConcepts/Round01.blend without changing the file open in Blender.
Stills go to _Screenshots-and-previews-to-delete/SkyProcessorConcepts/Round01 (contact sheet:
assemble_sky_processor_concepts.py).
"""
import bpy, math, os
from mathutils import Vector

ROOT = 'D:/PersonalProjects/the-tower-of-babel'
G = globals()
SHOT = ROOT + G.get('SHOT_DIR', '/_Screenshots-and-previews-to-delete/SkyProcessorConcepts/Round01')
BLEND = ROOT + G.get('BLEND_PATH', '/ArtSource/Resources/SkyProcessorConcepts/Round01.blend')
os.makedirs(SHOT, exist_ok=True); os.makedirs(os.path.dirname(BLEND), exist_ok=True)
scene = bpy.data.scenes[G.get('SCENE_NAME', 'Aero_SkyProcessorConcepts')]
bpy.context.window.scene = scene
top = bpy.data.collections[G.get('TOP_NAME', 'SkyProcessorConcepts')]
PREFIX = G.get('PREFIX', 'SkyProcessor') + '_'

# ---------------------------------------------------------------- stage (rebuilt on every run)
STAGE = 'ReviewStage_' + scene.name
stage = bpy.data.collections.get(STAGE)
if stage:
    for o in list(stage.objects): bpy.data.objects.remove(o, do_unlink=True)
else:
    stage = bpy.data.collections.new(STAGE); scene.collection.children.link(stage)

world = bpy.data.worlds.get('Concept_SkyWorld') or bpy.data.worlds.new('Concept_SkyWorld')
world.use_nodes = True; nt = world.node_tree; nt.nodes.clear(); n, l = nt.nodes, nt.links
out = n.new('ShaderNodeOutputWorld'); mix = n.new('ShaderNodeMixShader'); lp = n.new('ShaderNodeLightPath')
light_bg = n.new('ShaderNodeBackground'); light_bg.inputs['Strength'].default_value = .75
# Lighting sky: deep blue overhead, bright white horizon band, blue ground, so chrome and glass get readable reflections.
ltc = n.new('ShaderNodeTexCoord'); lsep = n.new('ShaderNodeSeparateXYZ'); lmr = n.new('ShaderNodeMapRange')
lmr.inputs['From Min'].default_value = -1; lmr.inputs['From Max'].default_value = 1
lr = n.new('ShaderNodeValToRGB'); le = lr.color_ramp.elements
le[0].position = .3; le[0].color = (.12, .32, .7, 1); le[1].position = 1; le[1].color = (.1, .38, .95, 1)
for pos, col in ((.5, (1.4, 1.45, 1.5, 1)), (.56, (.55, .78, 1, 1))): e = le.new(pos); e.color = col
l.new(ltc.outputs['Generated'], lsep.inputs[0]); l.new(lsep.outputs['Z'], lmr.inputs['Value']); l.new(lmr.outputs['Result'], lr.inputs[0])
l.new(lr.outputs[0], light_bg.inputs['Color'])
cam_bg = n.new('ShaderNodeBackground'); cam_bg.inputs['Strength'].default_value = 1
tc = n.new('ShaderNodeTexCoord'); sep = n.new('ShaderNodeSeparateXYZ'); rmp = n.new('ShaderNodeValToRGB')
rmp.color_ramp.elements[0].position = 0; rmp.color_ramp.elements[0].color = (.62, .82, 1, 1)
rmp.color_ramp.elements[1].position = 1; rmp.color_ramp.elements[1].color = (.12, .42, .9, 1)
l.new(tc.outputs['Window'], sep.inputs[0]); l.new(sep.outputs['Y'], rmp.inputs[0]); l.new(rmp.outputs[0], cam_bg.inputs['Color'])
l.new(lp.outputs['Is Camera Ray'], mix.inputs[0]); l.new(light_bg.outputs[0], mix.inputs[1]); l.new(cam_bg.outputs[0], mix.inputs[2])
l.new(mix.outputs[0], out.inputs['Surface'])
scene.world = world


def add(o):
    stage.objects.link(o); return o


sun = add(bpy.data.objects.new('Sun', bpy.data.lights.get('ConceptSun') or bpy.data.lights.new('ConceptSun', 'SUN')))
sun.data.energy = 3.6; sun.data.angle = math.radians(8); sun.data.color = (1, .97, .92)
sun.rotation_euler = (math.radians(50), 0, math.radians(-35))
fill = add(bpy.data.objects.new('Fill', bpy.data.lights.get('ConceptFill') or bpy.data.lights.new('ConceptFill', 'AREA')))
fill.data.energy = 500; fill.data.size = 8; fill.data.specular_factor = 0; fill.data.color = (.8, .9, 1)
rim = add(bpy.data.objects.new('Rim', bpy.data.lights.get('ConceptRim') or bpy.data.lights.new('ConceptRim', 'AREA')))
rim.data.energy = 700; rim.data.size = 6; rim.data.specular_factor = .3; rim.data.color = (.85, .92, 1)

floor_mat = bpy.data.materials.get('Concept_Floor') or bpy.data.materials.new('Concept_Floor'); floor_mat.use_nodes = True
fb = next(x for x in floor_mat.node_tree.nodes if x.type == 'BSDF_PRINCIPLED')
fb.inputs['Base Color'].default_value = (.32, .58, .9, 1); fb.inputs['Roughness'].default_value = .3
fb.inputs['Coat Weight'].default_value = .15
me = bpy.data.meshes.get('ConceptFloor')
if not me:
    import bmesh
    bm = bmesh.new(); bmesh.ops.create_circle(bm, cap_ends=True, segments=96, radius=60)
    me = bpy.data.meshes.new('ConceptFloor'); bm.to_mesh(me); bm.free(); me.materials.append(floor_mat)
add(bpy.data.objects.new('Floor', me))

cam = add(bpy.data.objects.new('ReviewCamera', bpy.data.cameras.get('ConceptCam') or bpy.data.cameras.new('ConceptCam')))
cam.data.lens = 85; scene.camera = cam

# ---------------------------------------------------------------- render settings
r = scene.render
r.engine = 'BLENDER_EEVEE'; r.film_transparent = False; r.image_settings.file_format = 'PNG'
ee = scene.eevee
for attr, val in (('taa_render_samples', 96), ('use_raytracing', True), ('use_shadows', True), ('shadow_ray_count', 2),
                  ('shadow_step_count', 8), ('use_fast_gi', True)):
    try: setattr(ee, attr, val)
    except AttributeError: pass
try: ee.ray_tracing_options.resolution_scale = '1'
except (AttributeError, TypeError): pass
scene.view_settings.view_transform = 'Standard'
scene.view_settings.look = 'None'
scene.view_settings.exposure = -.15


def aim(target, azimuth, elevation, distance):
    t = Vector(target); a, e = math.radians(azimuth), math.radians(elevation)
    cam.location = t + Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e))) * distance
    cam.rotation_euler = (t - cam.location).to_track_quat('-Z', 'Y').to_euler()


def place_lights(center):
    c = Vector(center)
    fill.location = c + Vector((4, -5, 3)); fill.rotation_euler = (c - fill.location).to_track_quat('-Z', 'Y').to_euler()
    rim.location = c + Vector((-3, 5, 4)); rim.rotation_euler = (c - rim.location).to_track_quat('-Z', 'Y').to_euler()


def render(path):
    r.filepath = path; bpy.ops.render.render(write_still=True, scene=scene.name)


# One 3/4 close-up per concept, others hidden.
roots = sorted([o for o in scene.objects if o.name.startswith(PREFIX) and 'height' in o], key=lambda o: o.name)
r.resolution_x, r.resolution_y, r.resolution_percentage = 800, 1000, 100
for root in roots:
    for c in top.children: c.hide_render = c is not root.users_collection[0] and True
    h = root['height']; size = max(h * 1.15, 3.0)
    centre = root.location + Vector((0, -.25, h * .46))
    place_lights(root.location)
    aim(centre, -34, 16, size / 2 / math.tan(math.radians(12)) + .6)
    render('%s/%s.png' % (SHOT, root.name))

# The lineup: every concept, two rows.
for c in top.children: c.hide_render = False
r.resolution_x, r.resolution_y = 2400, 1200
place_lights((0, 2, 0)); cam.data.lens = 35
aim(*G.get('LINEUP', ((0, 2.2, 1.1), -8, 20, 21)))
render(SHOT + '/Lineup.png')
cam.data.lens = 85

bpy.data.libraries.write(BLEND, {scene}, fake_user=True, compress=True)
print('Rendered', len(roots) + 1, 'stills to', SHOT, '; saved', BLEND)
