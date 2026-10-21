"""Low-poly architecture with high-detail bake sources. Run through Blender MCP.

Call build(), then bake_one(name) for each model, then finish(). Existing scenes
are retained. The exported meshes have UV0 for baked maps and UV1 for palette.
"""
import ast
import bpy
import bmesh
import math
import json
from pathlib import Path
from mathutils import Vector
from mathutils.geometry import tessellate_polygon

ROOT=Path('D:/PersonalProjects/the-tower-of-babel')
EXPORT=ROOT/'Assets/_Project/Models/Buildings/FrutigerAero'
BAKED=EXPORT/'BakedAero'
SOURCE=ROOT/'ArtSource/Buildings/FrutigerAero'
SCENE_NAME='Aero_LowPolyBuildingKit'
PAIRS={}
PROFILE=[(0,.37),(.045,.41002339),(.17,.41002339),(.24,.34),(.34,.285),(.43,.27),
         (1.7,.25),(3.27,.265),(3.42,.29),(3.55,.33),(3.72,.38),(3.88,.40),
         (3.95,.41002339),(4,.37)]

def enum(owner,prop,value):
    return next(i.identifier for i in owner.bl_rna.properties[prop].enum_items if i.identifier==value)

def source_material(name,color):
    m=bpy.data.materials.new(name);m.use_nodes=True
    p=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Metallic'].default_value=0;p.inputs['Roughness'].default_value=.25
    p.inputs['Coat Weight'].default_value=.3
    p.inputs['Emission Color'].default_value=(*color,1);p.inputs['Emission Strength'].default_value=1
    return m

def collect(name,objects,triangulate=False):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        o.select_set(True);bpy.context.view_layer.objects.active=o
        for mod in list(o.modifiers):bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.context.view_layer.objects.active=objects[0]
    if len(objects)>1:bpy.ops.object.join()
    o=bpy.context.object;o.name=name
    scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    if triangulate:
        mod=o.modifiers.new('Fixed export triangles','TRIANGULATE')
        mod.quad_method=enum(mod,'quad_method','FIXED')
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return o

def radius(y):
    for (a,r),(b,s) in zip(PROFILE,PROFILE[1:]):
        if a<=y<=b:return r+(s-r)*(y-a)/(b-a)
    return PROFILE[-1][1]

def tube(name,points,r=.01,swatch=0,sides=8):
    vertices=[]
    for i,p in enumerate(points):
        p=Vector(p);t=(Vector(points[min(i+1,len(points)-1)])-Vector(points[max(i-1,0)])).normalized()
        ref=Vector((0,0,1)) if abs(t.z)<.9 else Vector((0,1,0))
        a=t.cross(ref).normalized();b=t.cross(a).normalized()
        for j in range(sides):
            q=p+r*(a*math.cos(j*math.tau/sides)+b*math.sin(j*math.tau/sides));vertices.append(tuple(q))
    faces=[]
    for i in range(len(points)-1):
        for j in range(sides):
            a=i*sides+j;b=i*sides+(j+1)%sides;faces.append((a,b,b+sides,a+sides))
    return mesh_obj(name,vertices,faces,swatch,smooth=True)

def pillar_high():
    ys=sorted(set([i/48*4 for i in range(49)]+[p[0] for p in PROFILE]))
    verts=[];sides=128
    for y in ys:
        fade=min(1,max(0,(y-.43)/.12),max(0,(3.27-y)/.12))
        for j in range(sides):
            angle=j*math.tau/sides
            groove=.018*(.5+.5*math.cos(angle*20))**1.6*fade
            r=radius(y)-groove;verts.append((r*math.cos(angle),y,r*math.sin(angle)))
    faces=[]
    for i in range(len(ys)-1):
        for j in range(sides):
            a=i*sides+j;b=i*sides+(j+1)%sides;faces.append((a,b,b+sides,a+sides))
    faces.extend([tuple(range(sides-1,-1,-1)),tuple((len(ys)-1)*sides+j for j in range(sides))])
    parts=[mesh_obj('Sculpt_FlutedShaft',verts,faces,0,smooth=True)]
    # Rounded marine-petal relief replaces literal botanical green ornament.
    for leaf in range(12):
        center=leaf*math.tau/12;v=[];f=[]
        for row in range(17):
            t=row/16;y=3.43+t*.34
            width=.21*(.22+.78*math.sin(math.pi*t)**.7)
            for col in range(9):
                u=col/4-1;angle=center+u*width
                relief=.035*math.sin(math.pi*t)**.7*(1-u*u)
                relief+=.007*math.sin(math.pi*t)*math.exp(-u*u*36)
                r=radius(y)+relief+.001
                v.append((r*math.cos(angle),y,r*math.sin(angle)))
        for row in range(16):
            for col in range(8):
                a=row*9+col;f.append((a,a+1,a+10,a+9))
        petal=mesh_obj('Sculpt_PearlPetal',v,f,1 if leaf%3==0 else 0,smooth=True)
        for polygon in petal.data.polygons:
            if polygon.normal.dot(Vector((polygon.center.x,polygon.center.y,0)))<0:polygon.flip()
        petal.data.update();parts.append(petal)
    # Paired classical volutes, reduced to soft blue enamel spirals.
    for face in range(4):
        angle=face*math.pi/2;radial=Vector((math.cos(angle),0,math.sin(angle)))
        tangent=Vector((-math.sin(angle),0,math.cos(angle)))
        for sign in (-1,1):
            points=[]
            for j in range(65):
                t=j/64;a=sign*t*math.tau*1.3;r=.075*(1-t)+.008
                u=sign*.082+r*math.cos(a);y=3.80+r*math.sin(a)
                p=radial*(radius(y)+.012)+tangent*u;p.y=y;points.append(tuple(p))
            parts.append(tube('Sculpt_EnamelVolute',points,.012,2))
    for y in (.205,.335,3.36,3.93):
        points=[((radius(y)+.004)*math.cos(j*math.tau/96),y,(radius(y)+.004)*math.sin(j*math.tau/96)) for j in range(97)]
        parts.append(tube('Sculpt_IceCollar',points,.008,1))
    return parts

def slab_low(step=False):
    verts=[];stretch=1/.35 if step else 1
    levels=[(-.20,.06),(-.145,0),(-.055,0),(0,.06)]
    for y,inset in levels:
        rx=.23-inset;rz=rx*stretch;points=[]
        for x,z,start in [(4-inset-rx,-inset*stretch-rz,0),(inset+rx,-inset*stretch-rz,90),
                          (inset+rx,-4+inset*stretch+rz,180),(4-inset-rx,-4+inset*stretch+rz,270)]:
            for j in range(3):
                a=math.radians(start+45*j);points.append((x+rx*math.cos(a),z+rz*math.sin(a)))
        verts.extend((x,y,z) for x,z in points)
    count=len(points);faces=[tuple(range(count-1,-1,-1))]
    for row in range(3):
        for j in range(count):
            a=row*count+j;b=row*count+(j+1)%count;faces.append((a,b,b+count,a+count))
    faces.append(tuple(3*count+j for j in range(count)))
    obj=mesh_obj('Low_RoundedTile',verts,faces,0,smooth=True)
    obj.data.polygons[0].use_smooth=False;obj.data.polygons[-1].use_smooth=False
    return obj

def slab_high(step=False):
    parts=[rounded_slab('Sculpt_RoundedSlab',step)]
    # Raised contour inlay and flowing corner relief, baked onto the plain tile.
    outline=rr(2,-2,3.68,3.68,.27,12)
    parts.append(tube('Sculpt_AzurePerimeter',[(x,.009,z) for x,z in outline+[outline[0]]],.014,2))
    for cx,cz,sign in [(.45,-.45,1),(3.55,-3.55,-1)]:
        for k in range(3):
            points=[]
            for j in range(33):
                a=(j/32)*math.pi/2;r=.18+k*.085
                points.append((cx+sign*r*math.cos(a),.008,cz-sign*r*math.sin(a)))
            parts.append(tube('Sculpt_RippleCorner',points,.008,1))
    if step:
        parts.append(top_plate('Sculpt_EnamelNosing',2,-3.55,3.25,.19,.006,1,.06))
        for z in (-3.15,-3.0):parts.append(tube('Sculpt_TreadRib',[(.4,.008,z),(3.6,.008,z)],.008,0))
    return parts

def arch_outline(count=12,corners=2):
    x0=-.194898665;x1=2.15690446
    points=[(x0,0),(x0,2.03)]
    for i in range(1,corners+1):
        a=math.pi-math.pi*.5*i/corners;points.append((x0+.12+.12*math.cos(a),2.03+.12*math.sin(a)))
    for i in range(corners+1):
        a=math.pi*.5-math.pi*.5*i/corners;points.append((x1-.12+.12*math.cos(a),2.03+.12*math.sin(a)))
    return points+[(x1,0)]+list(reversed(pointed_profile(count=count)))

def arch_high():
    parts=[extrude_panel('Sculpt_ArchPanel',arch_outline(32,8),.36,0,.04)]
    cx=.981002867
    for side in (-1,1):
        profile=pointed_profile(count=48);mould=[]
        for i,(x,y) in enumerate(profile):
            t=(Vector(profile[min(i+1,len(profile)-1)])-Vector(profile[max(i-1,0)])).normalized()
            out=Vector((-t.y,t.x));mould.append((x+out.x*.095,y+out.y*.095+.008,side*.195))
        parts.append(tube('Sculpt_ArchPearlMoulding',mould,.024,0))
        parts.append(tube('Sculpt_ArchIceLine',[(x,y+.048,z) for x,y,z in mould],.009,2))
        # A shell/sunburst medallion, with raised pearl rays and an aqua oval.
        for ray in range(9):
            angle=math.pi*.15+ray*math.pi*.7/8;points=[]
            for j in range(12):
                t=j/11;r=.07+t*.23
                points.append((cx+math.cos(angle)*r,1.64+math.sin(angle)*r*.75,side*(.19+.008*math.sin(t*math.pi))))
            parts.append(tube('Sculpt_ShellRay',points,.011,0))
        oval=[(cx+.075*math.cos(j*math.tau/64),1.65+.105*math.sin(j*math.tau/64),side*.195) for j in range(65)]
        parts.append(tube('Sculpt_AquaMedallion',oval,.019,2))
    return parts

def prepare_low(name,obj):
    obj.data.materials.clear()
    target=source_material('AeroLP_'+name+'_Material',(1,1,1))
    next(n for n in target.node_tree.nodes if n.type=='BSDF_PRINCIPLED').inputs['Emission Strength'].default_value=0
    obj.data.materials.append(target)
    for p in obj.data.polygons:p.material_index=0
    bpy.context.view_layer.objects.active=obj;obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.018,correct_aspect=True,scale_to_bounds=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    obj.data.uv_layers[0].name='BakeUV'
    palette=obj.data.uv_layers.new(name='PaletteUV')
    for loop in palette.data:loop.uv=(.0625,.5)
    obj.data.uv_layers.active_index=0;obj.data.uv_layers[0].active_render=True
    obj['project']='the-tower-of-babel';obj['style']='Frutiger Aero classical relief; white/blue only'
    obj['texture_layout']='UV0: unique baked maps; UV1: legacy palette'
    return target

def build():
    global scene,palette_materials,SCENE
    BAKED.mkdir(parents=True,exist_ok=True)
    SCENE=bpy.data.scenes.get(SCENE_NAME) or bpy.data.scenes.new(SCENE_NAME)
    scene=SCENE;bpy.context.window.scene=scene
    if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
    for obj in list(scene.objects):bpy.data.objects.remove(obj,do_unlink=True)
    original=bpy.data.scenes.get('Aero_BuildingKit')
    scene.render.engine=original.render.engine
    scene.cycles.samples=32;scene.cycles.use_denoising=False
    scene.unit_settings.system=enum(scene.unit_settings,'system','METRIC');scene.unit_settings.scale_length=1
    palette_materials=[source_material('AeroSculpt_Pearl',(.96,.975,1)),
                       source_material('AeroSculpt_Ice',(.62,.80,.98)),
                       source_material('AeroSculpt_Azure',(.20,.56,.92))]
    tree=ast.parse((ROOT/'Tools/BuildingAero/build_assets.py').read_text(encoding='utf-8-sig'))
    wanted={'U','mesh_obj','rr','lathe','top_plate','pointed_profile','extrude_panel','rounded_slab'}
    exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in wanted],type_ignores=[]),'geometry_helpers','exec'),globals())
    for name in ('Pillar','Arch','Floor_Tile','Step_Tile'):
        if name=='Pillar':
            low=lathe('Low_Pillar',PROFILE,0,16);highparts=pillar_high()
        elif name=='Arch':
            low=extrude_panel('Low_Arch',arch_outline(),.40,0,.025)
            for mod in low.modifiers:
                if mod.type=='BEVEL':mod.segments=1
            highparts=arch_high()
        else:
            low=slab_low(name=='Step_Tile');highparts=slab_high(name=='Step_Tile')
        high=collect('HP_'+name,highparts)
        low=collect('LP_'+name,[low],True)
        material=prepare_low(name,low)
        PAIRS[name]=(low,high,material)
        low.hide_render=True;high.hide_render=True
    finish(save_only=True)
    print('LOW_POLY_BUILT',json.dumps({n:{'lowTriangles':len(l.data.polygons),'highTriangles':sum(len(p.vertices)-2 for p in h.data.polygons)} for n,(l,h,m) in PAIRS.items()}))

def bake_one(name):
    global scene
    scene=bpy.data.scenes[SCENE_NAME];bpy.context.window.scene=scene
    low,high,mat=PAIRS[name]
    low.location=(0,0,0);low.scale=(1,1,1);high.location=(0,0,0)
    for o in scene.objects:o.hide_render=True;o.select_set(False)
    low.hide_render=False;high.hide_render=False;low.hide_set(False);high.hide_set(False)
    low.select_set(True);high.select_set(True);bpy.context.view_layer.objects.active=low
    scene.render.bake.use_selected_to_active=True;scene.render.bake.use_cage=False
    scene.render.bake.cage_extrusion=.09;scene.render.bake.max_ray_distance=.18
    scene.render.bake.margin=12
    scene.render.bake.normal_space=enum(scene.render.bake,'normal_space','TANGENT')
    size=2048 if name=='Pillar' else 1024
    nodes=mat.node_tree.nodes;images={}
    p=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
    for socket in ('Base Color','Normal'):
        for link in list(p.inputs[socket].links):mat.node_tree.links.remove(link)
    for suffix,kind in [('Normal','NORMAL'),('Surface','EMIT'),('AO','AO')]:
        image=bpy.data.images.new('AeroLP_'+name+'_'+suffix,width=size,height=size,is_data=suffix!='Surface')
        if suffix!='Surface':image.colorspace_settings.name='Non-Color'
        node=nodes.new('ShaderNodeTexImage');node.image=image;node.label='Baked '+suffix
        for n in nodes:n.select=False
        node.select=True;nodes.active=node
        bake_type=next(i.identifier for i in bpy.ops.object.bake.get_rna_type().properties['type'].enum_items if i.identifier==kind)
        bpy.ops.object.bake(type=bake_type)
        image.filepath_raw=str(BAKED/('AeroLP_'+name+'_'+suffix+'.png'))
        image.file_format=enum(image,'file_format','PNG');image.save();image.use_fake_user=True
        images[suffix]=(image,node)
    p=next(n for n in nodes if n.type=='BSDF_PRINCIPLED');links=mat.node_tree.links
    links.new(images['Surface'][1].outputs['Color'],p.inputs['Base Color'])
    normal=nodes.new('ShaderNodeNormalMap');links.new(images['Normal'][1].outputs['Color'],normal.inputs['Color']);links.new(normal.outputs['Normal'],p.inputs['Normal'])
    low.hide_render=True;high.hide_render=True
    export_low(name)
    print('BAKED_AND_EXPORTED',name,size,len(low.data.polygons))

def export_low(name):
    low,high,mat=PAIRS[name]
    bpy.ops.object.select_all(action='DESELECT');low.hide_set(False);low.select_set(True);bpy.context.view_layer.objects.active=low
    previous=bpy.data.objects.get(name);oldmat=bpy.data.materials.get('Aero_BuildingAtlas')
    low_name=low.name;mat_name=mat.name
    if previous:previous.name='Original_'+name
    if oldmat:oldmat.name='Original_Aero_BuildingAtlas'
    try:
        low.name=name;mat.name='Aero_BuildingAtlas'
        bpy.ops.export_scene.fbx(filepath=str(EXPORT/(name+'.fbx')),use_selection=True,object_types={'MESH'},
            axis_forward='-Z',axis_up='Y',bake_space_transform=True,apply_scale_options='FBX_SCALE_ALL',
            add_leaf_bones=False,bake_anim=False,use_mesh_modifiers=True,mesh_smooth_type='FACE',use_triangles=True,
            path_mode='STRIP',embed_textures=False)
    finally:
        low.name=low_name;mat.name=mat_name
        if previous:previous.name=name
        if oldmat:oldmat.name='Aero_BuildingAtlas'

def finish(save_only=False):
    report={}
    for name,(low,high,mat) in PAIRS.items():
        pts=[Vector(p) for p in low.bound_box]
        report[name]={'triangles':len(low.data.polygons),'vertices':len(low.data.vertices),
            'highTriangles':sum(len(p.vertices)-2 for p in high.data.polygons),
            'min':[min(p[i] for p in pts) for i in (0,2,1)],'max':[max(p[i] for p in pts) for i in (0,2,1)],
            'uvLayers':[u.name for u in low.data.uv_layers]}
        if not save_only:
            low.hide_render=False;low.hide_set(False);high.hide_render=True;high.hide_set(True)
            low.location=U({'Pillar':(-4.7,0,0),'Arch':(-1.6,0,0),'Floor_Tile':(1.1,.12,1.8),'Step_Tile':(1.1,.26,-3)}[name])
            if name=='Step_Tile':low.scale=(1,.35,1)
    (SOURCE/'LowPolyBakeReport.json').write_text(json.dumps(report,indent=2))
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'BuildingKit_LowPoly.blend'))
    print('SOURCE_SAVED',json.dumps(report))
