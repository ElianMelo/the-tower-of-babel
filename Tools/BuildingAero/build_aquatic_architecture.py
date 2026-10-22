"""Architectural white/aqua kit guided by frutiger-building-sample-2.jpg.

Run in live Blender MCP: build(); bake_one(name) for each model; finish().
Geometric seams and inset glazing replace the older overlaid ornament.
"""
from pathlib import Path
exec(compile(Path('D:/PersonalProjects/the-tower-of-babel/Tools/BuildingAero/build_lowpoly_baked.py').read_text(encoding='utf-8-sig'),'lowpoly_pipeline','exec'),globals())
SCENE_NAME='Aero_AquaticArchitecture'
PROFILE=[(0,.37),(.025,.395),(.06,.41002339),(.16,.41002339),(.205,.39),(.24,.34),
         (.32,.31),(.45,.296),(2.9,.286),(3.40,.30),(3.50,.34),(3.60,.38),
         (3.70,.397),(3.86,.41002339),(3.97,.41002339),(4,.385)]

def color_bands(obj,kind):
    obj.data.materials.clear()
    for m in palette_materials:obj.data.materials.append(m)
    for p in obj.data.polygons:
        y=sum(obj.data.vertices[v].co.z for v in p.vertices)/len(p.vertices)
        if kind=='Pillar':p.material_index=2 if .16<y<.205 or 3.50<y<3.60 else 0
        else:p.material_index=2 if -.15<y<-.08 else 0
    return obj

def pillar(count=24,sculpt=False):
    obj=lathe('White_ContinuousSupport',PROFILE,0,count)
    if sculpt:
        for v in obj.data.vertices:
            y=v.co.z
            fade=min(1,max(0,(y-.48)/.20),max(0,(3.32-y)/.20))
            angle=math.atan2(v.co.y,v.co.x)
            r=math.hypot(v.co.x,v.co.y)
            inset=.0045*(.5+.5*math.cos(angle*12))**3*fade
            if r>0:v.co.x*=1-inset/r;v.co.y*=1-inset/r
        obj.data.update()
    for p in obj.data.polygons:
        if len(p.vertices)>4:p.use_smooth=False
    return color_bands(obj,'Pillar')

def slab(step=False):
    if not step:
        exec(compile((ROOT/'Tools/BuildingAero/radial_floor_patch.py').read_text(),'radial_patch','exec'),globals())
        verts,faces=radial_floor_patch()
        return color_bands(mesh_obj('White_RadialDeckPatch',verts,faces,0),'Tile')
    verts=[];stretch=1/.35 if step else 1
    levels=[(-.20,.055),(-.18,.015),(-.15,0),(-.08,0),(-.03,.018),(0,.055)]
    for y,inset in levels:
        rx=.30-inset;rz=rx*stretch;points=[]
        for x,z,start in [(4-inset-rx,-inset*stretch-rz,0),(inset+rx,-inset*stretch-rz,90),
                          (inset+rx,-4+inset*stretch+rz,180),(4-inset-rx,-4+inset*stretch+rz,270)]:
            for j in range(6):
                a=math.radians(start+18*j);points.append((x+rx*math.cos(a),z+rz*math.sin(a)))
        verts.extend((x,y,z) for x,z in points)
    n=len(points);faces=[tuple(range(n-1,-1,-1))]
    for row in range(len(levels)-1):
        for j in range(n):
            a=row*n+j;b=row*n+(j+1)%n;faces.append((a,b,b+n,a+n))
    faces.append(tuple((len(levels)-1)*n+j for j in range(n)))
    obj=mesh_obj('White_RoundedDeck',verts,faces,0,smooth=True)
    obj.data.polygons[0].use_smooth=False;obj.data.polygons[-1].use_smooth=False
    return color_bands(obj,'Tile')

def opening(count=32):
    return [(.981002867-.79*math.cos(math.pi*j/count),1.26*math.sin(math.pi*j/count)) for j in range(count+1)]

def arch_outline_clean():
    x0=-.194898665;x1=2.15690446;r=.22
    points=[(x0,0),(x0,2.15-r)]
    for i in range(1,6):
        a=math.pi-math.pi*.5*i/5;points.append((x0+r+r*math.cos(a),2.15-r+r*math.sin(a)))
    for i in range(6):
        a=math.pi*.5-math.pi*.5*i/5;points.append((x1-r+r*math.cos(a),2.15-r+r*math.sin(a)))
    return points+[(x1,0)]+list(reversed(opening()))

def tinted_glazing(name,outline,side):
    # Flat recessed glazing with continuous color; the surround is real geometry.
    cx=.981002867;cy=1.75;z=side*.1415
    verts=[(cx,cy,z)]+[(x,y,z) for x,y in outline]
    faces=[(0,j+1,(j+1)%len(outline)+1) for j in range(len(outline))]
    obj=mesh_obj(name,verts,faces,2,smooth=False)
    for p in obj.data.polygons:
        if p.normal.y*side<0:p.flip()
    obj.data.update()
    mat=bpy.data.materials.get('Aquatic_GradientSource')
    if not mat:
        mat=source_material('Aquatic_GradientSource',(.035,.47,.72))
        attr=mat.node_tree.nodes.new('ShaderNodeVertexColor');attr.layer_name='GlazeColor'
        bsdf=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
        for socket in ('Base Color','Emission Color'):mat.node_tree.links.new(attr.outputs['Color'],bsdf.inputs[socket])
    obj.data.materials.clear();obj.data.materials.append(mat)
    attr=obj.data.color_attributes.new(name='GlazeColor',type='FLOAT_COLOR',domain='CORNER')
    for i,loop in enumerate(obj.data.loops):
        v=verts[loop.vertex_index];t=max(0,min(1,(v[1]-1.57)/.36))
        color=tuple(a+(b-a)*t for a,b in zip((.035,.30,.55),(.16,.65,.85)))
        attr.data[i].color=(*color,1)
    return obj

def arch():
    base=extrude_panel('White_ArchEnvelope',arch_outline_clean(),.40,0,0)
    outline=rr(.981002867,1.75,1.44,.36,.16,8)
    for side in (-1,1):
        cutter=extrude_panel('Temporary_InsetTool',outline,.24,0,0)
        cutter.location.y=side*.26
        bpy.context.view_layer.objects.active=base;base.select_set(True)
        mod=base.modifiers.new('Recessed aqua glazing','BOOLEAN')
        mod.operation=enum(mod,'operation','DIFFERENCE');mod.solver=enum(mod,'solver','EXACT');mod.object=cutter
        bpy.ops.object.modifier_apply(modifier=mod.name)
        bpy.data.objects.remove(cutter,do_unlink=True)
    mod=base.modifiers.new('Continuous radiused shell','BEVEL');mod.width=.024;mod.segments=2
    for polygon in base.data.polygons:polygon.use_smooth=True
    mod.affect=enum(mod,'affect','EDGES');mod.harden_normals=True
    weighted=base.modifiers.new('Planar shell normals','WEIGHTED_NORMAL');weighted.keep_sharp=True;weighted.weight=50
    return collect('Aquatic_Arch',[base]+[tinted_glazing('Inset_AquaPanel',outline,s) for s in (-1,1)])

_prepare_target=prepare_low
def prepare_low(name,obj):
    target=_prepare_target(name,obj)
    # Color boundaries come from geometry, avoiding ray-projection fringe pixels.
    uv=obj.data.uv_layers.get('PaletteUV')
    for p in obj.data.polygons:
        y=sum(obj.data.vertices[v].co.z for v in p.vertices)/len(p.vertices)
        blue=(.16<y<.205 or 3.50<y<3.60) if name=='Pillar' else (-.15<y<-.08 and name!='Arch')
        for li in p.loop_indices:uv.data[li].uv=(.3125 if blue else .0625,.5)
    return target

def build():
    global scene,SCENE,palette_materials
    scene=bpy.data.scenes.get(SCENE_NAME) or bpy.data.scenes.new(SCENE_NAME);SCENE=scene;bpy.context.window.scene=scene
    if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
    for obj in list(scene.objects):bpy.data.objects.remove(obj,do_unlink=True)
    scene.render.engine=bpy.data.scenes['Aero_BuildingKit'].render.engine;scene.cycles.samples=32
    scene.unit_settings.system=enum(scene.unit_settings,'system','METRIC');scene.unit_settings.scale_length=1
    palette_materials=[source_material('Aquatic_White',(.97,.985,1)),source_material('Aquatic_Ice',(.56,.82,.94)),
                       source_material('Aquatic_Blue',(.055,.46,.72))]
    tree=ast.parse((ROOT/'Tools/BuildingAero/build_assets.py').read_text(encoding='utf-8-sig'))
    names={'U','mesh_obj','rr','lathe','top_plate','extrude_panel'}
    exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in names],type_ignores=[]),'geometry_helpers','exec'),globals())
    for name in ('Pillar','Arch','Floor_Tile','Step_Tile'):
        if name=='Pillar':low=pillar();high=pillar(128,True)
        else:
            low=arch() if name=='Arch' else slab(name=='Step_Tile')
            high=low.copy();high.data=low.data.copy();scene.collection.objects.link(high)
        low=collect('LP_Aquatic_'+name,[low],True);high.name='HP_Aquatic_'+name
        mat=prepare_low(name,low);PAIRS[name]=(low,high,mat)
        low.hide_render=True;high.hide_render=True
    finish(True)
    print('AQUATIC_ARCHITECTURE_BUILT',[(n,len(l.data.polygons)) for n,(l,h,m) in PAIRS.items()])

# The matching high/low shells need only a short projection distance. This avoids
# picking the opposite wall of the inset or an adjacent collar while baking.
_bake_source=ast.parse((ROOT/'Tools/BuildingAero/build_lowpoly_baked.py').read_text(encoding='utf-8-sig'))
_bake=next(n for n in _bake_source.body if isinstance(n,ast.FunctionDef) and n.name=='bake_one')
_code=ast.unparse(_bake).replace('cage_extrusion = 0.09','cage_extrusion = 0.025').replace('max_ray_distance = 0.18','max_ray_distance = 0.06')
exec(compile(_code,'short_projection_bake','exec'),globals())

def finish(save_only=False):
    report={}
    for name,(low,high,mat) in PAIRS.items():
        pts=[Vector(p) for p in low.bound_box]
        report[name]={'triangles':len(low.data.polygons),'vertices':len(low.data.vertices),
            'min':[min(p[i] for p in pts) for i in (0,2,1)],'max':[max(p[i] for p in pts) for i in (0,2,1)],
            'uvLayers':[u.name for u in low.data.uv_layers]}
        low['style']='Reference sample 2: continuous white architecture, recessed aqua glazing'
        if not save_only:
            low.hide_render=False;low.hide_set(False);high.hide_render=True;high.hide_set(True)
            low.location=U({'Pillar':(-4.7,0,0),'Arch':(-1.6,0,0),'Floor_Tile':(1.1,.12,1.8),'Step_Tile':(1.1,.26,-3)}[name])
            if name=='Step_Tile':low.scale=(1,.35,1)
    (SOURCE/'AquaticArchitectureReport.json').write_text(json.dumps(report,indent=2))
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'BuildingKit_AquaticArchitecture.blend'))
    print('AQUATIC_SOURCE_SAVED',json.dumps(report))
