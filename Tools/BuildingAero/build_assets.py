"""Author the project building kit in live Blender via blender_mcp_client.py.

Dimensions below are Unity metres. U(x,y,z) -> Blender(x,z,y); the FBX
export/import pair bakes axes and units, retaining the original mesh pivots.
No external scene is cleared. All authored objects belong to Aero_BuildingKit.
"""
import bpy
import bmesh
import json
import math
from pathlib import Path
from mathutils import Vector
from mathutils.geometry import tessellate_polygon

ROOT = Path("D:/PersonalProjects/the-tower-of-babel")
EXPORT = ROOT / "Assets/_Project/Models/Buildings/FrutigerAero"
SOURCE = ROOT / "ArtSource/Buildings/FrutigerAero"
PREVIEW = ROOT / "Documentation/FrutigerAero/Buildings/Previews"
for folder in (EXPORT, SOURCE, PREVIEW):
    folder.mkdir(parents=True, exist_ok=True)

scene = bpy.data.scenes.get("Aero_BuildingKit") or bpy.data.scenes.new("Aero_BuildingKit")
bpy.context.window.scene = scene
if bpy.context.object and bpy.context.object.mode != 'OBJECT':
    bpy.ops.object.mode_set(mode='OBJECT')
for obj in list(scene.objects):
    bpy.data.objects.remove(obj, do_unlink=True)
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1

# A single atlas/material/submesh keeps all blue trim visible in the project's
# far renderer, which deliberately renders submesh zero only.
SWATCHES = [
    ("Porcelain", (0.93, 0.965, 1.0), 0.13, 0.75, 0.0),
    ("IceBlue", (0.53, 0.77, 1.0), 0.23, 0.86, 0.0),
    ("AzureGlass", (0.035, 0.30, 0.78), 0.30, 0.94, 0.03),
    ("PearlMetal", (0.69, 0.79, 0.93), 0.72, 0.91, 0.0),
    ("BlueLight", (0.16, 0.57, 1.0), 0.08, 0.83, 0.8),
    ("DeepBlue", (0.025, 0.115, 0.31), 0.35, 0.82, 0.0),
    ("WhiteGloss", (0.985, 0.99, 1.0), 0.08, 0.91, 0.0),
    ("BlueCeramic", (0.095, 0.42, 0.91), 0.16, 0.80, 0.0),
]


def image_atlas(name, mode):
    image = bpy.data.images.get(name) or bpy.data.images.new(name, 512, 64, alpha=True)
    if mode != 'color':
        image.colorspace_settings.name = 'Non-Color'
    pixels = []
    for y in range(64):
        for x in range(512):
            _, color, metallic, smooth, emission = SWATCHES[x // 64]
            if mode == 'color':
                pixels.extend((*color, 1.0))
            elif mode == 'mask':
                pixels.extend((metallic, 0.0, 0.0, smooth))
            elif mode == 'roughness':
                pixels.extend((1-smooth, 1-smooth, 1-smooth, 1.0))
            else:
                pixels.extend((color[0]*emission, color[1]*emission, color[2]*emission, 1.0))
    image.pixels.foreach_set(pixels)
    image.filepath_raw = str(EXPORT / (name + '.png'))
    image.file_format = 'PNG'
    image.save()
    return image


color_image = image_atlas('Aero_Palette', 'color')
mask_image = image_atlas('Aero_MetallicSmoothness', 'mask')
emission_image = image_atlas('Aero_Emission', 'emission')
rough_image = image_atlas('Aero_Roughness_Source', 'roughness')
palette_materials = []
for name, color, metal, smooth, emission in SWATCHES:
    mat = bpy.data.materials.new('Source_' + name)
    mat.use_nodes = True
    p = next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    p.inputs['Base Color'].default_value = (*color, 1)
    p.inputs['Metallic'].default_value = metal
    p.inputs['Roughness'].default_value = 1-smooth
    p.inputs['Coat Weight'].default_value = 0.45
    p.inputs['Emission Color'].default_value = (*color,1)
    p.inputs['Emission Strength'].default_value = emission
    mat.diffuse_color = (*color,1)
    palette_materials.append(mat)

shared = bpy.data.materials.new('Aero_BuildingAtlas')
shared.use_nodes = True
nodes = shared.node_tree.nodes
links = shared.node_tree.links
p = next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
tex = nodes.new('ShaderNodeTexImage'); tex.image=color_image
links.new(tex.outputs['Color'],p.inputs['Base Color'])
mask = nodes.new('ShaderNodeTexImage'); mask.image=mask_image
sep = nodes.new('ShaderNodeSeparateColor'); links.new(mask.outputs['Color'],sep.inputs['Color'])
links.new(sep.outputs['Red'],p.inputs['Metallic'])
rough = nodes.new('ShaderNodeTexImage'); rough.image=rough_image
links.new(rough.outputs['Color'],p.inputs['Roughness'])
em = nodes.new('ShaderNodeTexImage'); em.image=emission_image
links.new(em.outputs['Color'],p.inputs['Emission Color'])
p.inputs['Emission Strength'].default_value=0.3
p.inputs['Coat Weight'].default_value=0.42


def U(v):
    return (v[0], v[2], v[1])


def mesh_obj(name, verts, faces, swatch=0, bevel=0, smooth=False):
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata([U(v) for v in verts], [], faces); mesh.update()
    obj=bpy.data.objects.new(name, mesh); scene.collection.objects.link(obj)
    obj.data.materials.append(palette_materials[swatch])
    bm=bmesh.new(); bm.from_mesh(mesh); bmesh.ops.recalc_face_normals(bm,faces=bm.faces); bm.to_mesh(mesh);bm.free()
    for poly in mesh.polygons: poly.use_smooth=smooth or bool(bevel)
    if bevel:
        mod=obj.modifiers.new('Soft manufactured edges','BEVEL');mod.width=bevel;mod.segments=5
        mod.affect=next(i.identifier for i in mod.bl_rna.properties['affect'].enum_items if i.identifier=='EDGES')
        mod.harden_normals=True
        mod=obj.modifiers.new('Weighted highlights','WEIGHTED_NORMAL');mod.keep_sharp=True;mod.weight=50
    return obj


def box(name, center, size, swatch=0, bevel=.012):
    x,y,z=center; a,b,c=[s/2 for s in size]
    verts=[(x+dx*a,y+dy*b,z+dz*c) for dx,dy,dz in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
    return mesh_obj(name,verts,[(0,1,2,3),(4,7,6,5),(0,4,5,1),(3,2,6,7),(0,3,7,4),(1,5,6,2)],swatch,bevel)


def rr(cx,cz,w,d,r,n=6):
    pts=[]
    for x,z,start in [(cx+w/2-r,cz+d/2-r,0),(cx-w/2+r,cz+d/2-r,90),(cx-w/2+r,cz-d/2+r,180),(cx+w/2-r,cz-d/2+r,270)]:
        for j in range(n+1):
            a=math.radians(start+90*j/n)
            pts.append((x+r*math.cos(a),z+r*math.sin(a)))
    return pts


def outline_ring(name, points, width, y, swatch=2):
    cx=sum(x for x,z in points)/len(points);cz=sum(z for x,z in points)/len(points)
    # This routine is used for rectangles only; inset each side in its own axis.
    inner=[(x-width*(1 if x>cx else -1),z-width*(1 if z>cz else -1)) for x,z in points]
    verts=[(x,y,z) for x,z in points]+[(x,y,z) for x,z in inner]
    n=len(points)
    return mesh_obj(name,verts,[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],swatch)


def top_plate(name, cx, cz, w, d, y, swatch=2, radius=.015):
    points=rr(cx,cz,w,d,min(radius,w/3,d/3),3)
    return mesh_obj(name,[(x,y,z) for x,z in points],[tuple(range(len(points)))],swatch)


def lathe(name, profile, swatch=0, count=40):
    verts=[]
    for y,r in profile:
        for j in range(count):
            t=2*math.pi*j/count;verts.append((r*math.cos(t),y,r*math.sin(t)))
    faces=[]
    for i in range(len(profile)-1):
        for j in range(count):
            a=i*count+j;b=i*count+(j+1)%count
            faces.append((a,b,b+count,a+count))
    faces.extend([tuple(range(count-1,-1,-1)),tuple((len(profile)-1)*count+j for j in range(count))])
    return mesh_obj(name,verts,faces,swatch,smooth=True)


def ribbon(name, points, width, depth, swatch=2):
    verts=[]
    for i,(x,y) in enumerate(points):
        before=Vector(points[max(0,i-1)]);after=Vector(points[min(len(points)-1,i+1)])
        t=(after-before).normalized();normal=Vector((-t.y,t.x))
        for sign in (-1,1):
            verts.append((x+normal.x*width*sign/2,y+normal.y*width*sign/2,depth))
    faces=[(2*i,2*i+1,2*i+3,2*i+2) for i in range(len(points)-1)]
    return mesh_obj(name,verts,faces,swatch)


def pointed_profile(cx=.981002867, half=.79, height=1.13, count=24):
    def bez(t,a,b,c,d): return (1-t)**3*a+3*(1-t)**2*t*b+3*(1-t)*t*t*c+t**3*d
    left=[]
    for i in range(count+1):
        t=i/count
        left.append((bez(t,cx-half,cx-half,cx-.44,cx),bez(t,0,.54,.90,height)))
    return left+[(2*cx-x,y) for x,y in left[-2::-1]]


def extrude_panel(name,poly,depth,swatch=0,bevel=.018):
    n=len(poly)
    verts=[(x,y,-depth/2) for x,y in poly]+[(x,y,depth/2) for x,y in poly]
    coords=[Vector((x,y,0)) for x,y in poly]
    tris=tessellate_polygon([coords])
    lookup={(round(v.x,7),round(v.y,7)):i for i,v in enumerate(coords)}
    faces=[]
    for tri in tris:
        ids=[v if isinstance(v,int) else lookup[(round(v.x,7),round(v.y,7))] for v in tri]
        faces.extend([tuple(reversed(ids)),tuple(i+n for i in ids)])
    faces.extend((i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n))
    return mesh_obj(name,verts,faces,swatch,bevel)


def finish(name, objects):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True);bpy.context.view_layer.objects.active=obj
        for modifier in list(obj.modifiers): bpy.ops.object.modifier_apply(modifier=modifier.name)
    bpy.context.view_layer.objects.active=objects[0]
    if len(objects)>1:bpy.ops.object.join()
    obj=bpy.context.object;obj.name=name;obj.data.name=name+'_Mesh'
    bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    uv=obj.data.uv_layers.active or obj.data.uv_layers.new(name='PaletteUV')
    for poly in obj.data.polygons:
        src=obj.data.materials[poly.material_index]
        swatch=palette_materials.index(src)
        for li in poly.loop_indices: uv.data[li].uv=((swatch+.5)/8,.5)
    obj.data.materials.clear();obj.data.materials.append(shared)
    for poly in obj.data.polygons:poly.material_index=0
    obj['project']='the-tower-of-babel';obj['palette']='white and blue; no green'
    obj['unity_export']='1 mesh / 1 submesh / 1 shared material; original pivot'
    bpy.ops.export_scene.fbx(filepath=str(EXPORT/(name+'.fbx')),use_selection=True,
        object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_space_transform=True,
        apply_scale_options='FBX_SCALE_ALL',add_leaf_bones=False,bake_anim=False,
        use_mesh_modifiers=True,mesh_smooth_type='FACE',use_triangles=True,
        path_mode='STRIP',embed_textures=False)
    return obj


def rounded_slab(name, step=False):
    # Eight contour rings form a rounded solid rim, not a flat bevel strip.
    # Step corners are precompensated for the prefab's existing Z scale of .35.
    zstretch=1/.35 if step else 1
    verts=[]
    levels=[(-.20,.065),(-.19,.032),(-.17,.010),(-.14,0),
            (-.06,0),(-.03,.010),(-.01,.032),(-.0015,.065)]
    for y,inset in levels:
        points=rr(2,-2,4-2*inset,4-2*inset,.23-inset,6)
        # Stretch just the rounded corner's Z radius; straight edges retain bounds.
        if step:
            points=[]
            rx=.23-inset;rz=.23*zstretch-inset*zstretch
            for x,z,start in [(4-inset-rx,-inset*zstretch-rz,0),
                              (inset+rx,-inset*zstretch-rz,90),
                              (inset+rx,-4+inset*zstretch+rz,180),
                              (4-inset-rx,-4+inset*zstretch+rz,270)]:
                for j in range(7):
                    a=math.radians(start+90*j/6)
                    points.append((x+rx*math.cos(a),z+rz*math.sin(a)))
        verts.extend((x,y,z) for x,z in points)
    count=len(points)
    faces=[tuple(range(count-1,-1,-1))]
    for row in range(len(levels)-1):
        for j in range(count):
            a=row*count+j;b=row*count+(j+1)%count
            faces.append((a,b,b+count,a+count))
    faces.append(tuple((len(levels)-1)*count+j for j in range(count)))
    obj=mesh_obj(name,verts,faces,0,smooth=True)
    obj.data.polygons[0].use_smooth=False;obj.data.polygons[-1].use_smooth=False
    return obj


def smooth_profile(points, samples=2):
    # Cubic radius interpolation removes abrupt breaks in the turned column.
    result=[]
    for i in range(len(points)-1):
        y0,r0=points[max(0,i-1)];y1,r1=points[i];y2,r2=points[i+1];y3,r3=points[min(len(points)-1,i+2)]
        d1=(r2-r0)/max(y2-y0,.001);d2=(r3-r1)/max(y3-y1,.001)
        for j in range(samples):
            t=j/samples
            radius=(2*t**3-3*t*t+1)*r1+(t**3-2*t*t+t)*(y2-y1)*d1+(-2*t**3+3*t*t)*r2+(t**3-t*t)*(y2-y1)*d2
            result.append((y1+(y2-y1)*t,min(.41002339,max(.26,radius))))
    result.append(points[-1]);return result


def rounded_moulding(name, points, side):
    verts=[];width=.10;segments=8
    for i,(x,y) in enumerate(points):
        tangent=(Vector(points[min(len(points)-1,i+1)])-Vector(points[max(0,i-1)])).normalized()
        normal=Vector((-tangent.y,tangent.x))
        for j in range(segments+1):
            a=math.pi*j/segments
            cross=math.cos(a)*width/2
            verts.append((x+normal.x*cross,y+normal.y*cross,side*(.166+.034*math.sin(a))))
    faces=[]
    for i in range(len(points)-1):
        for j in range(segments):
            a=i*(segments+1)+j;faces.append((a,a+1,a+segments+2,a+segments+1))
    return mesh_obj(name,verts,faces,6,smooth=True)


def keystone_lens(name,cx,side):
    verts=[];rows=10;cols=24
    for i in range(rows+1):
        a=math.pi*i/rows
        for j in range(cols):
            b=2*math.pi*j/cols
            verts.append((cx+.12*math.sin(a)*math.cos(b),1.66+.17*math.cos(a),side*(.172+.027*math.sin(a)*math.sin(b))))
    faces=[]
    for i in range(rows):
        for j in range(cols):
            a=i*cols+j;b=i*cols+(j+1)%cols
            faces.append((a,b,b+cols,a+cols))
    obj=mesh_obj(name,verts,faces,1,smooth=True)
    bm=bmesh.new();bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(obj.data);bm.free();return obj


# FLOOR: exact existing corner pivot, 4 x 0.2 x 4 metre envelope.
parts=[rounded_slab('Floor_RoundedMarbleShell')]
parts.append(outline_ring('Floor_BlueInlay',rr(2,-2,3.76,3.76,.16),.035,0,2))
for x in (.27,3.73):
    for z in (-.27,-3.73):
        parts.append(top_plate('Floor_CornerReflector',x,z,.16,.045,0,1,.012))
floor=finish('Floor_Tile',parts)

# STEP: retain the same unscaled envelope; prefab z=0.35 makes it 1.4m deep.
parts=[rounded_slab('Step_RoundedMarbleShell',step=True),
       top_plate('Step_AzureNosing',2,-3.65,3.54,.30,-.001,2,.055),
       top_plate('Step_NosingLight',2,-3.50,3.34,.025,0,4,.008)]
for z in (-3.28,-3.11):
    parts.append(top_plate('Step_AntiSlipGroove',2,z,3.3,.018,0,1,.005))
for x in (.23,3.77):
    parts.append(top_plate('Step_EndCap',x,-1.8,.035,2.6,0,1,.008))
step=finish('Step_Tile',parts)

# PILLAR: a restrained waisted porcelain body with four narrow blue glass fins.
pillar_profile=[(0,.37),(.02,.395),(.06,.41002339),(.12,.41002339),(.18,.393),(.23,.355),
    (.29,.325),(.40,.315),(.7,.300),(1.2,.285),(1.8,.28),(2.4,.292),(3,.315),(3.5,.34),
    (3.61,.352),(3.68,.385),(3.75,.41002339),(3.88,.41002339),(3.95,.40),(4,.37)]
parts=[lathe('Pillar_ContinuousRoundedMarble',smooth_profile(pillar_profile,3),0,48)]
pillar=finish('Pillar',parts)

# ARCH: full rectangular spandrel, an open pointed underside, flat upper bearing.
# Existing yOffset=3.85, level pitch=6, upper floor underside=6 -> local top=2.15.
cx=.981002867;x0=-.194898665;x1=2.15690446
profile=pointed_profile(cx)
poly=[(x0,0),(x0,2.03)]
for i in range(1,9):
    a=math.pi-math.pi*.5*i/8;poly.append((x0+.12+.12*math.cos(a),2.03+.12*math.sin(a)))
for i in range(9):
    a=math.pi*.5-math.pi*.5*i/8;poly.append((x1-.12+.12*math.cos(a),2.03+.12*math.sin(a)))
poly += [(x1,0)]+list(reversed(profile))
parts=[extrude_panel('Arch_RoundedMarbleSpandrel',poly,.34,0,.065)]
for side in (-1,1):
    # Arch-tracing blue inset; all trim is solid geometry, not transparent planes.
    outer=[]
    for i,(x,y) in enumerate(profile):
        a=Vector(profile[max(0,i-1)]);b=Vector(profile[min(len(profile)-1,i+1)])
        t=(b-a).normalized();out=Vector((-t.y,t.x))
        outer.append((x+out.x*.105,y+out.y*.105+.008))
    parts.append(rounded_moulding('Arch_RoundedOpeningMoulding',outer,side))
    parts.append(keystone_lens('Arch_RoundedKeystone',cx,side))
arch=finish('Arch',parts)
arch['upper_floor_connection']='local y=2.15 + tower arch offset 3.85 = floor underside 6.0'

# Separate presentation duplicates from the exported source meshes.
assets={'Floor_Tile':floor,'Step_Tile':step,'Pillar':pillar,'Arch':arch}
report={}
for name,obj in assets.items():
    pts=[obj.matrix_world@Vector(p) for p in obj.bound_box]
    report[name]={'vertices':len(obj.data.vertices),'triangles':sum(len(p.vertices)-2 for p in obj.data.polygons),
        'min':[min(p[i] for p in pts) for i in (0,2,1)],
        'max':[max(p[i] for p in pts) for i in (0,2,1)],'materials':len(obj.data.materials)}
    obj.hide_render=True;obj.hide_set(True)
(SOURCE/'BlenderGeometryReport.json').write_text(json.dumps(report,indent=2))

def display(name, source, position, scale=(1,1,1)):
    obj=bpy.data.objects.new(name,source.data);scene.collection.objects.link(obj)
    obj.location=U(position);obj.scale=(scale[0],scale[2],scale[1]);return obj

# Four-piece review lineup; assembled bay is created separately in Unity.
display('Review_Pillar',pillar,(-4.7,0,0))
display('Review_Arch',arch,(-1.6,1.05,0))
display('Review_Floor',floor,(1.1,.12,1.8))
display('Review_Step',step,(1.1,.26,-3),(1,1,.35))
ground=box('Studio_WhiteFloor',(0,-.16,0),(200,.08,200),6,.01)
ground.data.materials.clear();ground.data.materials.append(palette_materials[0])
world=bpy.data.worlds.new('Aero_StudioWorld');world.use_nodes=True
background=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND')
background.inputs[0].default_value=(.15,.21,.32,1)
background.inputs[1].default_value=.65
scene.world=world

def area(name,pos,power,size,color):
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size;data.color=color
    obj=bpy.data.objects.new(name,data);scene.collection.objects.link(obj);obj.location=U(pos)
    obj.rotation_euler=(Vector(U((0,1,0)))-obj.location).to_track_quat('-Z','Y').to_euler()

area('Key_Softbox',(-4,9,-5),2000,8,(.90,.95,1))
area('Fill_Softbox',(6,5,-2),1400,6,(.65,.80,1))
area('Rim_Softbox',(0,7,6),2600,7,(.82,.91,1))
cam_data=bpy.data.cameras.new('Aero_ReviewCamera');cam=bpy.data.objects.new('Aero_ReviewCamera',cam_data)
scene.collection.objects.link(cam);scene.camera=cam
cam.location=U((10,7.5,-13));target=Vector(U((0,1.4,-.4)))
cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
cam_data.type='ORTHO';cam_data.ortho_scale=13.6;cam_data.lens=55
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.render.resolution_x=1600;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
scene.render.image_settings.file_format='PNG'
scene.render.filepath=str(PREVIEW/'Blender-Kit.png')
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'BuildingKit.blend'))
print('AERO_EXPORT_COMPLETE '+json.dumps(report))
