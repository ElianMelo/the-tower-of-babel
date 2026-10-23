"""Run inside the project's connected Blender using Blender MCP. Metres, Z up; FBX Y up."""
import bpy, math, random, os
from mathutils import Vector

ROOT = 'D:/PersonalProjects/the-tower-of-babel'
OUT = ROOT + '/Assets/_Project/Models/Resources'
os.makedirs(OUT, exist_ok=True)
os.makedirs(ROOT + '/ArtSource/Resources', exist_ok=True)
scene = bpy.data.scenes.new('Aero_SandGlassWorkshop')
bpy.context.window.scene = scene
random.seed(71)
materials = {}
for name, color, metal, rough in [
    ('Sand', (.76,.51,.19,1),0,.85), ('SandLight', (.81,.57,.24,1),0,.75),
    ('Pearl', (.84,.93,.98,1),.18,.26), ('Azure', (.025,.3,.66,1),.45,.25),
    ('Steel', (.12,.19,.23,1),.7,.32), ('Heat', (1,.24,.025,1),.1,.3),
    ('Glass', (.14,.76,.89,1),.25,.12), ('Chip', (.43,.48,.52,1),0,.85)]:
    m=bpy.data.materials.get('Workshop_'+name) or bpy.data.materials.new('Workshop_'+name); m.use_nodes=True
    node=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    node.inputs['Base Color'].default_value=color
    node.inputs['Metallic'].default_value=metal; node.inputs['Roughness'].default_value=rough
    if name=='Heat':
        node.inputs['Emission Color'].default_value=color; node.inputs['Emission Strength'].default_value=1.5
    materials[name]=m

def finish(o, name, material, parent=None):
    o.name=name; o.data.materials.append(materials[material])
    if parent: o.parent=parent
    bpy.context.view_layer.objects.active=o
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return o

def box(name, loc, size, material, parent, bevel=.045):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc); o=bpy.context.object; o.dimensions=size
    finish(o,name,material,parent)
    if bevel:
        m=o.modifiers.new('Soft edges','BEVEL'); m.width=bevel; m.segments=2
        bpy.ops.object.modifier_apply(modifier=m.name)
        m=o.modifiers.new('Weighted corners','WEIGHTED_NORMAL'); bpy.ops.object.modifier_apply(modifier=m.name)
    return o

def cylinder(name,loc,radius,depth,material,parent,vertices=32):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth,location=loc)
    o=finish(bpy.context.object,name,material,parent)
    for p in o.data.polygons: p.use_smooth=len(p.vertices)==4
    return o

def torus(name,loc,major,minor,material,parent):
    bpy.ops.mesh.primitive_torus_add(major_segments=32,minor_segments=6,location=loc,major_radius=major,minor_radius=minor)
    return finish(bpy.context.object,name,material,parent)

def empty(name):
    o=bpy.data.objects.new(name,None); scene.collection.objects.link(o); return o

def reparent(o,parent):
    bpy.context.view_layer.update()
    world=o.matrix_world.copy(); o.parent=parent; o.matrix_world=world

def mound(name,parent,radius=1.28,height=.77):
    verts=[(0,0,height)]; faces=[]; segments=40; rings=9
    for ring in range(1,rings+1):
        t=ring/rings
        for j in range(segments):
            a=2*math.pi*j/segments
            r=radius*t*(1+.05*math.sin(a*3)+.035*math.sin(a*7+ring*.2))
            z=height*(1-t**1.45)+.025*math.sin(a*5+t*25)*math.sin(t*math.pi)
            verts.append((r*math.cos(a),r*math.sin(a)*.86,z))
    for j in range(segments): faces.append((0,1+j,1+(j+1)%segments))
    for ring in range(rings-1):
        for j in range(segments):
            a=1+ring*segments+j; b=1+ring*segments+(j+1)%segments
            faces.append((a,a+segments,b+segments,b))
    faces.append(tuple(reversed(range(1+(rings-1)*segments,1+rings*segments))))
    mesh=bpy.data.meshes.new(name); mesh.from_pydata(verts,[],faces); mesh.update()
    o=bpy.data.objects.new(name,mesh); scene.collection.objects.link(o); finish(o,name,'Sand',parent)
    for p in mesh.polygons:
        p.use_smooth=True
    uv=mesh.uv_layers.new(name='Sand grain UV')
    for loop in mesh.loops:
        v=mesh.vertices[loop.vertex_index].co
        uv.data[loop.index].uv=(v.x/(radius*2)+.5,v.y/(radius*2)+.5)
    return o

pile=empty('SandPile'); mound('SandMound',pile)

furnace=empty('GlassFurnace')
cylinder('Plinth',(0,0,.12),1.04,.24,'Azure',furnace)
cylinder('LowerBand',(0,0,.38),.89,.3,'Pearl',furnace)
cylinder('FurnaceShell',(0,0,1.03),.78,1.05,'Pearl',furnace)
for z in [.61,1.4]: torus('CeramicBand',(0,0,z),.79,.055,'Azure',furnace)
cylinder('TopSteel',(0,0,1.61),.7,.16,'Steel',furnace)
lid=cylinder('Lid',(0,0,1.76),.74,.15,'Azure',furnace)
torus('HeatRing',(0,0,1.58),.67,.045,'Heat',furnace)
# Tall offset chimney, making a round kiln silhouette distinct from the square stone press.
cylinder('Chimney',(.43,.3,2.05),.18,.77,'Steel',furnace,20)
for z in [1.85,2.02,2.19,2.36]: torus('ChimneyRib',(.43,.3,z),.19,.035,'Pearl',furnace)
cylinder('ChimneyCap',(.43,.3,2.49),.28,.1,'Azure',furnace,24)
# Front furnace window: deep dark rim, amber melt pool and protective grille.
window=box('WindowRim',(0,-.755,1.08),(.96,.14,.64),'Steel',furnace,.08)
box('HotChamber',(0,-.835,1.08),(.78,.025,.45),'Heat',furnace,.06)
for x in [-.27,0,.27]: box('WindowBar',(x,-.88,1.08),(.035,.045,.5),'Steel',furnace,.01)
box('OutputTray',(0,-.91,.43),(1.12,.76,.12),'Steel',furnace)
for x in [-.54,.54]: box('TrayRail',(x,-.91,.52),(.07,.8,.16),'Pearl',furnace)
output=box('GlassOutput',(0,-1.03,.59),(.73,.46,.13),'Glass',furnace,.035)
for x in [-.27,-.09,.09,.27]: reparent(box('GlassFacet',(x,-1.03,.665),(.024,.4,.016),'Pearl',furnace,.006),output)
# Right-hand corrugated bellows and hand wheel are independently animated.
bellows=box('Bellows',(.94,0,.93),(.38,.57,.48),'Steel',furnace,.035)
for x in [.81,.9,.99,1.08]:
    b=box('BellowsPleat',(x,0,.93),(.035,.63,.55),'Azure',furnace,.015); reparent(b,bellows)
wheel=cylinder('Flywheel',(-.96,0,1.08),.34,.12,'Azure',furnace,24); wheel.rotation_euler[1]=math.pi/2
cylinder('Axle',(-.8,0,1.08),.08,.4,'Steel',furnace,16).rotation_euler[1]=math.pi/2
for a in [0,math.pi/2,math.pi,3*math.pi/2]:
    spoke=box('Spoke',(-1.04,math.cos(a)*.16,1.08+math.sin(a)*.16),(.04,.07,.3),'Pearl',furnace,.01)
    spoke.rotation_euler[0]=-a; reparent(spoke,wheel)
feed=mound('SandFeed',furnace,.44,.31); feed.location=(0,0,1.85)
pool=cylinder('MoltenPool',(0,-.88,.55),.28,.05,'Heat',furnace,24)
for i,x in enumerate([-.23,0,.23]): box('StatusLight_'+str(i),(x,-.825,.7),(.12,.035,.055),'Glass',furnace,.015)

chiproot=empty('RockChip')
bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=.5)
chip=finish(bpy.context.object,'RockChipMesh','Chip',chiproot)
for v in chip.data.vertices: v.co *= random.uniform(.75,1.18)
brickroot=empty('SpawnerBrick')
box('SpawnerBrickMesh',(0,0,.075),(.36,.2,.15),'Chip',brickroot,.02)

def join_static(root, keep):
    static=[o for o in root.children_recursive if o.type=='MESH' and o.name not in keep and o.parent==root]
    bpy.ops.object.select_all(action='DESELECT')
    for o in static: o.select_set(True)
    if static:
        bpy.context.view_layer.objects.active=static[0]; bpy.ops.object.join(); static[0].name='FurnaceBody'

join_static(furnace,{'Lid','Bellows','Flywheel','SandFeed','GlassOutput','MoltenPool','StatusLight_0','StatusLight_1','StatusLight_2'})
for part in [bellows,wheel,output]:
    bpy.ops.object.select_all(action='DESELECT')
    for child in list(part.children_recursive): child.select_set(True)
    part.select_set(True); bpy.context.view_layer.objects.active=part
    bpy.ops.object.join()
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
for root in [pile,furnace,chiproot,brickroot]:
    bpy.ops.object.select_all(action='DESELECT')
    root.select_set(True)
    for child in root.children_recursive: child.select_set(True)
    bpy.ops.export_scene.fbx(filepath=OUT+'/'+root.name+'.fbx',use_selection=True,object_types={'MESH'},
        bake_space_transform=True,apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False)

# Spread source models for an easy Blender workshop view, after exports.
pile.location.x=-2.7; chiproot.location=(2,0,0); brickroot.location=(2.8,0,0)
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/ArtSource/Resources/Sand_And_Glass.blend')
print('Exported sand pile, round glass furnace, rock chip and spawner brick. '+str(sum(len(o.data.polygons) for o in furnace.children_recursive if o.type=='MESH'))+' furnace faces.')
