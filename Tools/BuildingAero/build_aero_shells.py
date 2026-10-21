"""Pearl shells, blue enamel and bubble relief. Execute through live Blender MCP.

Reuses the low-poly pipeline without changing the earlier saved source scene.
Call build(), bake_one(name) for each model, then finish().
"""
from pathlib import Path
exec(compile((Path('D:/PersonalProjects/the-tower-of-babel/Tools/BuildingAero/build_lowpoly_baked.py')).read_text(encoding='utf-8-sig'),'lowpoly_pipeline','exec'),globals())

SCENE_NAME='Aero_PearlShellKit'
PROFILE=[(0,.37),(.045,.41002339),(.17,.395),(.24,.34),(.34,.285),(.43,.27),
         (1.7,.25),(3.27,.265),(3.42,.315),(3.55,.38),(3.72,.41002339),
         (3.88,.40),(3.95,.385),(4,.37)]
BLUE=(.035,.37,.87)
WHITE=(.96,.98,1)

def tint_mesh(obj,colors):
    # Bake a smooth enamel gradient rather than hard blue material stripes.
    m=bpy.data.materials.get('AeroShell_VertexEnamel')
    if not m:
        m=source_material('AeroShell_VertexEnamel',WHITE)
        p=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
        a=m.node_tree.nodes.new('ShaderNodeVertexColor');a.layer_name='AeroTint'
        for socket in ('Base Color','Emission Color'):m.node_tree.links.new(a.outputs['Color'],p.inputs[socket])
    obj.data.materials.clear();obj.data.materials.append(m)
    attr=obj.data.color_attributes.new(name='AeroTint',type='FLOAT_COLOR',domain='CORNER')
    for i,loop in enumerate(obj.data.loops):attr.data[i].color=(*colors[loop.vertex_index],1)
    return obj

def blend(a,b,t):return tuple(x+(y-x)*t for x,y in zip(a,b))

def pillar_high():
    parts=[lathe('Sculpt_PearlShell',PROFILE,0,128)]
    # Four flowing glazed panels; the rounded white ribs remain dominant.
    for face in range(4):
        verts=[];faces=[];colors=[]
        for row in range(81):
            t=row/80;y=.49+t*2.94
            center=face*math.pi/2+.10*math.sin(t*math.pi*1.7)
            envelope=math.sin(math.pi*t)**.45
            for col in range(17):
                u=col/8-1;angle=center+u*.46*envelope
                relief=.014*envelope*(1-u*u)
                r=radius(y)+.001+relief
                verts.append((r*math.cos(angle),y,r*math.sin(angle)))
                saturation=(1-abs(u)**4)*envelope
                gradient=.60+.25*math.sin(t*math.pi)
                colors.append(blend(WHITE,blend((.28,.69,1),BLUE,gradient),saturation))
        for row in range(80):
            for col in range(16):
                a=row*17+col;faces.append((a,a+1,a+18,a+17))
        obj=mesh_obj('Sculpt_FlowingEnamel',verts,faces,0,smooth=True)
        for p in obj.data.polygons:
            if p.normal.dot(Vector((p.center.x,p.center.y,0)))<0:p.flip()
        obj.data.update();parts.append(tint_mesh(obj,colors))
    # The old volutes and acanthus leaves become restrained bubble lenses.
    for face in range(4):
        center=face*math.pi/2;verts=[];faces=[];colors=[]
        for row in range(33):
            t=row/32;y=3.49+t*.35;width=.46*math.sin(math.pi*t)**.5
            for col in range(17):
                u=col/8-1;angle=center+u*width
                r=radius(y)+.002+.024*math.sin(math.pi*t)*(1-u*u)
                verts.append((r*math.cos(angle),y,r*math.sin(angle)))
                color=blend(BLUE,(.46,.82,1),t)
                # A soft white crescent evokes early Aero bubble iconography.
                highlight=.80*math.exp(-((t-.76)/.13)**2)*max(0,1-u*u)
                color=blend(color,WHITE,highlight)
                colors.append(blend(WHITE,color,math.sin(math.pi*t)**.35*(1-abs(u)**5)))
        for row in range(32):
            for col in range(16):
                a=row*17+col;faces.append((a,a+1,a+18,a+17))
        obj=mesh_obj('Sculpt_BubbleCapital',verts,faces,0,smooth=True)
        for p in obj.data.polygons:
            if p.normal.dot(Vector((p.center.x,p.center.y,0)))<0:p.flip()
        obj.data.update();parts.append(tint_mesh(obj,colors))
    for y in (.21,3.39,3.94):
        points=[((radius(y)+.002)*math.cos(j*math.tau/128),y,(radius(y)+.002)*math.sin(j*math.tau/128)) for j in range(129)]
        parts.append(tube('Sculpt_SoftIceCollar',points,.010,1))
    return parts

def lens(name,cx,cy,rx,ry,side,depth=.035):
    verts=[(cx,cy,side*(.195+depth))];colors=[(.15,.57,.97)];faces=[];sides=64;rings=12
    for ring in range(1,rings+1):
        r=ring/rings
        for j in range(sides):
            a=j*math.tau/sides;x=r*math.cos(a);y=r*math.sin(a)
            verts.append((cx+rx*x,cy+ry*y,side*(.195+depth*math.sqrt(max(0,1-r*r)))))
            c=blend(BLUE,(.35,.78,1),(y+1)*.5)
            glint=.90*math.exp(-((x+.22)**2/.35+(y-.54)**2/.025))
            c=blend(c,WHITE,glint);colors.append(blend(c,(.68,.89,1),r**12*.75))
    for j in range(sides):faces.append((0,1+j,1+(j+1)%sides))
    for ring in range(rings-1):
        for j in range(sides):
            a=1+ring*sides+j;b=1+ring*sides+(j+1)%sides;faces.append((a,b,b+sides,a+sides))
    o=mesh_obj(name,verts,faces,0,smooth=True)
    for p in o.data.polygons:
        if p.normal.y*side<0:p.flip()
    o.data.update();return tint_mesh(o,colors)

def arch_high():
    parts=[extrude_panel('Sculpt_WhiteArchShell',arch_outline(32,8),.36,0,.04)]
    for side in (-1,1):
        profile=pointed_profile(count=64);band=[]
        for i,(x,y) in enumerate(profile):
            tangent=(Vector(profile[min(i+1,len(profile)-1)])-Vector(profile[max(i-1,0)])).normalized()
            outward=Vector((-tangent.y,tangent.x))
            band.append((x+outward.x*.115,y+outward.y*.115+.012,side*.195))
        parts.append(tube('Sculpt_BlueArchGlaze',band,.035,2,12))
        parts.append(tube('Sculpt_WhiteArchLip',[(x,y-.043,z) for x,y,z in band],.012,0,10))
        parts.append(lens('Sculpt_AeroBubble',.981,1.75,.14,.16,side))
        # Broad orbital arcs replace narrow sunburst engraving.
        for k in range(2):
            points=[]
            for j in range(65):
                t=j/64;a=math.pi*(.10+t*.81);r=.22+k*.09
                points.append((.981+math.cos(a)*r,1.73+math.sin(a)*r*.60,side*.197))
            parts.append(tube('Sculpt_BubbleOrbit',points,.012 if k==0 else .008,1,10))
        parts.append(lens('Sculpt_SatelliteBubble',1.25,1.82,.042,.048,side,.014))
    return parts

def slab_high(step=False):
    parts=[rounded_slab('Sculpt_PearlSlab',step)]
    outline=rr(2,-2,3.65,3.65,.34,16)
    parts.append(tube('Sculpt_AzureGlaze',[(x,.006,z) for x,z in outline+[outline[0]]],.033,2,12))
    # Soft luminous edge through the white shell's side wall.
    edge=rr(2,-2,3.99,3.99,.23,16)
    parts.append(tube('Sculpt_IceEdge',[(x,-.10,z) for x,z in edge+[edge[0]]],.022,1,10))
    for cx,cz,sgn in [(.54,-.54,1),(3.46,-3.46,-1)]:
        for k in range(2):
            points=[]
            for j in range(49):
                a=j/48*math.pi*.75;r=.21+k*.13
                points.append((cx+sgn*r*math.cos(a),.009,cz-sgn*r*math.sin(a)))
            parts.append(tube('Sculpt_WaterRipple',points,.013 if k==0 else .009,1,10))
    if step:
        parts.append(top_plate('Sculpt_IceNosing',2,-3.50,3.15,.25,.009,1,.10))
    return parts

def finish(save_only=False):
    report={}
    for name,(low,high,mat) in PAIRS.items():
        pts=[Vector(p) for p in low.bound_box]
        report[name]={'triangles':len(low.data.polygons),'vertices':len(low.data.vertices),
            'highTriangles':sum(len(p.vertices)-2 for p in high.data.polygons),
            'min':[min(p[i] for p in pts) for i in (0,2,1)],'max':[max(p[i] for p in pts) for i in (0,2,1)],
            'uvLayers':[u.name for u in low.data.uv_layers]}
        low['style']='Frutiger Aero pearl shells, flowing blue enamel, bubble lenses; white/blue only'
        if not save_only:
            low.hide_render=False;low.hide_set(False);high.hide_render=True;high.hide_set(True)
            low.location=U({'Pillar':(-4.7,0,0),'Arch':(-1.6,0,0),'Floor_Tile':(1.1,.12,1.8),'Step_Tile':(1.1,.26,-3)}[name])
            if name=='Step_Tile':low.scale=(1,.35,1)
    (SOURCE/'AeroShellBakeReport.json').write_text(json.dumps(report,indent=2))
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'BuildingKit_AeroShells.blend'))
    print('AERO_SHELL_SOURCE_SAVED',json.dumps(report))
