"""Textured resource assets in live Blender; preserves earlier scenes and exports moving pivots."""
import bpy
import bmesh
import math
import random
import json
from pathlib import Path
from mathutils import Vector

ROOT=Path('D:/PersonalProjects/the-tower-of-babel')
EXPORT=ROOT/'Assets/_Project/Models/Resources'
SOURCE=ROOT/'ArtSource/Resources'
scene=bpy.data.scenes.get('Aero_ResourceWorkshop') or bpy.data.scenes.new('Aero_ResourceWorkshop')
bpy.context.window.scene=scene
if bpy.context.object and bpy.context.object.mode!='OBJECT': bpy.ops.object.mode_set(mode='OBJECT')
for obj in list(scene.objects): bpy.data.objects.remove(obj,do_unlink=True)


def enum(owner,prop,value):
    return next(i.identifier for i in owner.bl_rna.properties[prop].enum_items if i.identifier==value)


def U(p): return (p[0],p[2],p[1])


stone_image=bpy.data.images.load(str(EXPORT/'Textures/StoneSurface-Imagegen.png'),check_existing=True)
atlas_image=bpy.data.images.load(str(EXPORT/'Textures/ProcessorAtlas-Imagegen.png'),check_existing=True)
materials={}
for name,region,metal,rough in [('Ceramic',(0,.5),.05,.3),('BlueMetal',(.5,.5),.35,.28),
                               ('Steel',(0,0),.72,.35),('Glow',(.5,0),.1,.22),('Stone',None,.02,.72),('Refined',None,.12,.3)]:
    mat=bpy.data.materials.get('Resource_'+name) or bpy.data.materials.new('Resource_'+name)
    mat.use_nodes=True
    shader=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    shader.inputs['Metallic'].default_value=metal; shader.inputs['Roughness'].default_value=rough
    tex=next((n for n in mat.node_tree.nodes if n.type=='TEX_IMAGE'),None) or mat.node_tree.nodes.new('ShaderNodeTexImage')
    tex.image=stone_image if region is None else atlas_image
    mat.node_tree.links.new(tex.outputs['Color'],shader.inputs['Base Color'])
    if name=='Glow':
        mat.node_tree.links.new(tex.outputs['Color'],shader.inputs['Emission Color'])
        shader.inputs['Emission Strength'].default_value=.2
    mat.diffuse_color=({'Ceramic':(.92,.96,1,1),'BlueMetal':(.03,.4,.8,1),'Steel':(.16,.24,.3,1),
                        'Glow':(.2,.8,1,1),'Stone':(.55,.65,.72,1),'Refined':(.65,.82,.9,1)})[name]
    materials[name]=(mat,region)


def assign(obj,kind):
    mat,region=materials[kind]
    obj.data.materials.clear(); obj.data.materials.append(mat)
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True); bpy.context.view_layer.objects.active=obj
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=1.15,island_margin=.025)
    bpy.ops.object.mode_set(mode='OBJECT')
    if region:
        for uv in obj.data.uv_layers.active.data:
            uv.uv=(region[0]+.035+uv.uv.x*.43,region[1]+.035+uv.uv.y*.43)
    return obj


def box(name,position,size,kind='Ceramic',rounding=.05):
    bpy.ops.mesh.primitive_cube_add(size=1,location=U(position))
    obj=bpy.context.object; obj.name=name; obj.dimensions=U(size)
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if rounding:
        bevel=obj.modifiers.new('Soft manufactured edges',enum(bpy.types.Modifier,'type','BEVEL'))
        bevel.width=rounding; bevel.segments=3
        bpy.ops.object.modifier_apply(modifier=bevel.name)
        weighted=obj.modifiers.new('Clean face normals',enum(bpy.types.Modifier,'type','WEIGHTED_NORMAL'))
        weighted.keep_sharp=True
        bpy.ops.object.modifier_apply(modifier=weighted.name)
    return assign(obj,kind)


def cylinder(name,position,radius,depth,kind='Steel'):
    bpy.ops.mesh.primitive_cylinder_add(vertices=24,radius=radius,depth=depth,location=U(position),rotation=(math.pi/2,0,0))
    obj=bpy.context.object; obj.name=name
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    bevel=obj.modifiers.new('Rounded drum rims',enum(bpy.types.Modifier,'type','BEVEL')); bevel.width=.012; bevel.segments=2
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    for face in obj.data.polygons: face.use_smooth=len(face.vertices)==4
    return assign(obj,kind)


def join(name,objects,pivot=(0,0,0)):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    if len(objects)>1: bpy.ops.object.join()
    obj=bpy.context.object; obj.name=name
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    delta=obj.location-Vector(U(pivot))
    for vertex in obj.data.vertices: vertex.co+=delta
    obj.location=U(pivot)
    return obj


def rock_cluster():
    objects=[]; rng=random.Random(147)
    for i,(location,scale) in enumerate([((0,.03,.12),(.57,.52,.48)),((-.38,-.22,-.13),(.32,.28,.30)),((.37,-.22,-.17),(.34,.26,.32))]):
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3,radius=1,location=U(location))
        obj=bpy.context.object; obj.name='StoneLobe_'+str(i)
        for vertex in obj.data.vertices:
            noise=rng.uniform(.91,1.08)
            vertex.co=Vector((vertex.co.x*scale[0],vertex.co.y*scale[2],vertex.co.z*scale[1]))*noise
            vertex.co.z=max(-.27 if i else -.48,vertex.co.z)
        assign(obj,'Stone'); objects.append(obj)
    return join('StoneCluster',objects)


stone=rock_cluster()
static=[box('Foundation',(0,-.89,.03),(1.5,.22,1.18),rounding=.10),
        box('LowerCabinet',(0,-.46,.17),(1.22,.70,.82),rounding=.10),
        box('RearFrame',(0,.25,.45),(1.4,1.40,.22),rounding=.08),
        box('LeftPylon',(-.64,.20,0),(.18,1.30,.92),rounding=.07),
        box('RightPylon',(.64,.20,0),(.18,1.30,.92),rounding=.07),
        box('AquaBackplate',(0,.25,.315),(1.13,1.02,.05),'BlueMetal',.04),
        box('FrontApron',(0,-.35,-.30),(1.08,.28,.09),'BlueMetal',.045),
        box('OutputTray',(0,-.62,-.61),(1.10,.11,.61),'Steel',.04),
        box('LeftTrayLip',(-.52,-.55,-.64),(.065,.16,.63),'Ceramic',.025),
        box('RightTrayLip',(.52,-.55,-.64),(.065,.16,.63),'Ceramic',.025)]
# A genuinely open hopper: four connected wall profiles, no opaque top lid.
vertices=[]; faces=[]
for y,rx,rz in [(1.0,.71,.50),(.57,.36,.27),(.57,.30,.21),(.99,.62,.41)]:
    for i in range(24):
        a=math.tau*i/24; vertices.append(U((math.cos(a)*rx,y,math.sin(a)*rz)))
for row in range(4):
    for i in range(24):
        a=row*24+i; b=row*24+(i+1)%24; c=((row+1)%4)*24+(i+1)%24; d=((row+1)%4)*24+i
        faces.append((a,b,c,d))
mesh=bpy.data.meshes.new('HopperShell'); mesh.from_pydata(vertices,[],faces); mesh.update()
obj=bpy.data.objects.new('HopperShell',mesh); scene.collection.objects.link(obj)
bm=bmesh.new(); bm.from_mesh(mesh); bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(mesh); bm.free()
static.append(assign(obj,'Ceramic'))
for i in range(4): static.append(box('CabinetVent_'+str(i),(-.3+i*.2,-.49,-.265),(.10,.27,.035),'Steel',.016))
housing=join('Housing',static)
moving=[]
for side,x in [('Left',-.29),('Right',.29)]:
    pivot=(x,.28,-.045)
    parts=[cylinder('Drum',pivot,.24,.72),cylinder('BlueHub',(x,.28,-.435),.18,.07,'BlueMetal')]
    for i in range(8):
        a=i*math.tau/8
        fin=box('RoundedCrushingRib',(x+math.cos(a)*.235,.28+math.sin(a)*.235,-.045),(.075,.075,.67),'BlueMetal',.02)
        fin.rotation_euler.y=-a
        parts.append(fin)
    moving.append(join('Roller'+side,parts,pivot))
press=box('PressHead',(0,.57,-.08),(.90,.12,.62),'Steel',.035)
feed=bpy.data.objects.new('FeedStone',stone.data.copy()); scene.collection.objects.link(feed)
feed.location=U((0,.95,0)); feed.scale=(.27,.27,.27)
output=box('OutputStone',(0,-.43,-.68),(.26,.25,.25),'Refined',.035)
lights=[box('StatusLight_'+str(i),(-.22+i*.22,-.27,-.375),(.14,.065,.035),'Glow',.018) for i in range(3)]
processor=[housing]+moving+[press,feed,output]+lights


def export(name,objects):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=str(EXPORT/(name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',
        bake_space_transform=True,apply_scale_options='FBX_SCALE_ALL',add_leaf_bones=False,bake_anim=False,
        use_mesh_modifiers=True,mesh_smooth_type='FACE',use_triangles=True,path_mode='STRIP',embed_textures=False)


export('StoneCluster',[stone]); export('StoneProcessor',processor)
stone.location=U((-2,0,0))
for obj in processor: obj.location.x+=1.2
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.shading.type=enum(area.spaces.active.shading,'type','MATERIAL')
        region=next(r for r in area.regions if r.type=='WINDOW')
        with bpy.context.temp_override(area=area,region=region): bpy.ops.view3d.view_all(center=False)
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Stone_And_Processor.blend'))
print(json.dumps([{'name':o.name,'vertices':len(o.data.vertices),'triangles':sum(len(p.vertices)-2 for p in o.data.polygons),
                   'materials':[m.name for m in o.data.materials]} for o in [stone]+processor]))
