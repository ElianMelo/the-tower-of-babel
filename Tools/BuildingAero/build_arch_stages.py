"""Build arch-only random chunks using the pillar cutting/export structure."""
from pathlib import Path
import ast
import bpy
import bmesh
import math
import json
import random
from mathutils import Vector

ROOT=Path('D:/PersonalProjects/the-tower-of-babel')
SOURCE=ROOT/'ArtSource/Buildings/FrutigerAero'
EXPORT=ROOT/'Assets/_Project/Models/Buildings/FrutigerAero/Construction'
SCENE_NAME='Aero_ArchConstruction'
SEED=914317

# Import geometry helpers, without running the pillar or finished-kit builders.
tree=ast.parse((ROOT/'Tools/BuildingAero/build_pillar_stages.py').read_text())
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in
    {'enum','solid_piece','preview_material'}],type_ignores=[]),'pillar_chunk_helpers','exec'),globals())
tree=ast.parse((ROOT/'Tools/BuildingAero/build_aquatic_architecture.py').read_text(encoding='utf-8-sig'))
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in
    {'opening','arch_outline_clean'}],type_ignores=[]),'arch_profile_helpers','exec'),globals())


def blueprint(scene,material):
    vertices,faces=[],[]
    def tube(points):
        start=len(vertices)
        for i,p in enumerate(points):
            p=Vector(p)
            tangent=(Vector(points[min(i+1,len(points)-1)])-Vector(points[max(i-1,0)])).normalized()
            axis=Vector((0,0,1)) if abs(tangent.z)<.9 else Vector((0,1,0))
            a=tangent.cross(axis).normalized(); b=tangent.cross(a).normalized()
            for j in range(3): vertices.append(tuple(p+.006*(a*math.cos(j*math.tau/3)+b*math.sin(j*math.tau/3))))
        for i in range(len(points)-1):
            for j in range(3):
                a=start+i*3+j; b=start+i*3+(j+1)%3; faces.append((a,b,b+3,a+3))
        faces.append(tuple(start+j for j in (2,1,0)))
        faces.append(tuple(start+(len(points)-1)*3+j for j in range(3)))
    profile=arch_outline_clean()
    # Outer and inner edge guides sit just within the completed white silhouette.
    split=len(profile)-len(opening())
    for depth in (-.184,.184):
        points=[]
        for i,(x,z) in enumerate(profile):
            if i<split:
                x=max(-.1829,min(2.1449,x)); z=max(.012,min(2.138,z))
            else:
                normal=Vector(((x-.981002867)/(.79*.79),z/(1.26*1.26))).normalized()
                x+=normal.x*.012; z=max(.012,z+normal.y*.012)
            points.append((x,depth,z))
        tube(points+[points[0]])
        # A sparse inset loop also identifies the aqua panel in the blueprint.
        # Use a standard rounded rectangle with short straight upper/lower edges.
        window=[]
        for cx,cz,start in [(1.541,1.77,0),(.421,1.77,90),(.421,1.73,180),(1.541,1.73,270)]:
            for j in range(5):
                angle=math.radians(start+j*22.5); window.append((cx+.16*math.cos(angle),depth*.72,cz+.16*math.sin(angle)))
        tube(window+[window[0]])
    for x,z in [(-.18,.02),(2.14,.02),(-.18,1.93),(2.14,1.93),(.98,2.138),(.98,1.272)]:
        tube([(x,-.184,z),(x,.184,z)])
    mesh=bpy.data.meshes.new('Arch_Blueprint'); mesh.from_pydata(vertices,[],faces); mesh.materials.append(material)
    bake=mesh.uv_layers.new(name='BakeUV'); palette=mesh.uv_layers.new(name='PaletteUV')
    # The arch uses its baked color map rather than the pillar's palette toggle.
    for poly in mesh.polygons:
        poly.use_smooth=True
        for i in poly.loop_indices: bake.data[i].uv=BLUE_UV; palette.data[i].uv=(.3125,.5)
    mesh.update(); obj=bpy.data.objects.new('Arch_Guides',mesh); scene.collection.objects.link(obj); return obj


def prepare_source(scene):
    source=next((o for o in scene.objects if o.get('arch_completed_source')),None)
    if source is None:
        with bpy.data.libraries.load(str(SOURCE/'BuildingKit_AquaticArchitecture.blend'),link=False) as (src,dst):
            dst.objects=['LP_Aquatic_Arch']
        source=dst.objects[0]; scene.collection.objects.link(source); source['arch_completed_source']=True
    source.location=(0,0,0); source.hide_render=True; source.hide_set(True)
    # Closed, very thin backings on the two inset sheets make fracture cells solid.
    # Their visible front surfaces and the original completed model stay intact.
    fracture=bpy.data.objects.new('Arch_FractureSource',source.data.copy()); scene.collection.objects.link(fracture)
    bm=bmesh.new(); bm.from_mesh(fracture.data)
    unvisited=set(bm.faces); open_faces=[]
    while unvisited:
        stack=[unvisited.pop()]; component=[]
        while stack:
            face=stack.pop(); component.append(face)
            for edge in face.edges:
                for other in edge.link_faces:
                    if other in unvisited: unvisited.remove(other); stack.append(other)
        if any(e.is_boundary for f in component for e in f.edges): open_faces.extend(component)
    if open_faces: bmesh.ops.solidify(bm,geom=open_faces,thickness=-.003)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(fracture.data); bm.free(); fracture.data.update()
    fracture.hide_render=True; fracture.hide_set(True)
    return source,fracture


def inside(x,z):
    if ((x-.981002867)/.79)**2+(z/1.26)**2<1: return False
    if z>1.93:
        if x<.0251 and (x-.0251)**2+(z-1.93)**2>.22**2: return False
        if x>1.9369 and (x-1.9369)**2+(z-1.93)**2>.22**2: return False
    return True


def export(objects,path):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects: obj.hide_set(False); obj.location=(0,0,0); obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',
        bake_space_transform=True,apply_scale_options='FBX_SCALE_ALL',add_leaf_bones=False,bake_anim=False,
        use_mesh_modifiers=True,mesh_smooth_type='FACE',use_triangles=True,path_mode='STRIP',embed_textures=False)


def build():
    global CAP_UV,BLUE_UV
    scene=bpy.data.scenes.get(SCENE_NAME) or bpy.data.scenes.new(SCENE_NAME); bpy.context.window.scene=scene
    if bpy.context.object and bpy.context.object.mode!='OBJECT': bpy.ops.object.mode_set(mode='OBJECT')
    for obj in list(scene.objects):
        if obj.name.startswith(('Arch_Stage_','Arch_Chunk_','Arch_Piece_','Arch_Guides','Arch_FractureSource','Arch_Label_')):
            bpy.data.objects.remove(obj,do_unlink=True)
    source,fracture=prepare_source(scene); material=source.data.materials[0]
    uv=source.data.uv_layers.get('BakeUV')
    def mean_uv(face): return tuple(sum(uv.data[i].uv[axis] for i in face.loop_indices)/len(face.loop_indices) for axis in range(2))
    white_face=min(source.data.polygons,key=lambda p:(p.center-Vector((-.08,-.2,.75))).length)
    blue_face=min(source.data.polygons,key=lambda p:(p.center-Vector((.98,-.1415,1.75))).length)
    CAP_UV=mean_uv(white_face); BLUE_UV=mean_uv(blue_face)
    rng=random.Random(SEED); seeds=[]
    while len(seeds)<80:
        x=rng.uniform(-.16,2.12); z=rng.uniform(.025,2.125)
        if not inside(x,z): continue
        point=Vector((x,rng.uniform(-.13,.13),z))
        if all((point-other).length>.105 for other in seeds): seeds.append(point)
    pieces=[solid_piece(fracture,i,seeds,scene,prefix='Arch',cap_uv=CAP_UV) for i in range(80)]
    order=list(range(80)); rng.shuffle(order); guides=blueprint(scene,material)
    stages=[]; report=[]; topology=[]
    for stage in range(11):
        if stage==10:
            obj=bpy.data.objects.new('Arch_Stage_10',source.data.copy()); scene.collection.objects.link(obj)
        else:
            parts=[]
            for piece_id in order[:stage*8]:
                part=bpy.data.objects.new(f'Arch_StagePart_{piece_id}',pieces[piece_id].data.copy()); scene.collection.objects.link(part); parts.append(part)
            guide=bpy.data.objects.new('Arch_StageGuide',guides.data.copy()); scene.collection.objects.link(guide); parts.append(guide)
            bpy.ops.object.select_all(action='DESELECT')
            for part in parts: part.select_set(True)
            bpy.context.view_layer.objects.active=parts[0]
            if len(parts)>1: bpy.ops.object.join()
            obj=bpy.context.object; obj.name=f'Arch_Stage_{stage:02}'
        obj.data.materials.clear(); obj.data.materials.append(material)
        for face in obj.data.polygons: face.material_index=0
        bpy.context.view_layer.objects.active=obj
        mod=obj.modifiers.new('Construction triangles','TRIANGULATE'); mod.quad_method=enum(mod,'quad_method','FIXED')
        bpy.ops.object.modifier_apply(modifier=mod.name)
        obj['filled_piece_ids']=order[:stage*8]; obj['construction_stage']=stage; stages.append(obj)
        report.append({'stage':stage,'randomSeed':SEED,'filledPieceIds':list(obj['filled_piece_ids']),'triangles':len(obj.data.polygons)})
    for i,piece in enumerate(pieces):
        piece.name=f'Arch_Chunk_{order.index(i)//8+1:02}_{i:02}'; piece.data.materials.clear(); piece.data.materials.append(material)
        for face in piece.data.polygons: face.material_index=0
        bpy.context.view_layer.objects.active=piece
        mod=piece.modifiers.new('Chunk triangles','TRIANGULATE'); mod.quad_method=enum(mod,'quad_method','FIXED')
        bpy.ops.object.modifier_apply(modifier=mod.name)
    EXPORT.mkdir(parents=True,exist_ok=True)
    export(pieces,EXPORT/'ArchChunks.fbx'); export(stages[:-1],EXPORT/'ArchStages.fbx')
    for obj in stages+pieces:
        bm=bmesh.new(); bm.from_mesh(obj.data)
        topology.append({'name':obj.name,'boundaryEdges':sum(e.is_boundary for e in bm.edges),'nonManifoldEdges':sum(not e.is_manifold for e in bm.edges),'volume':abs(bm.calc_volume())})
        bm.free()
    for piece in pieces: piece.hide_render=True; piece.hide_set(True)
    guides.hide_render=True; guides.hide_set(True)
    white=preview_material('ArchStageReview_White',(.92,.96,1)); blue=preview_material('ArchStageReview_Blue',(.035,.48,.8))
    for i,obj in enumerate(stages):
        obj.location=(i*2.75,0,0); obj.data.materials.clear(); obj.data.materials.append(white); obj.data.materials.append(blue)
        bake=obj.data.uv_layers.get('BakeUV')
        for face in obj.data.polygons:
            p=sum((obj.data.vertices[j].co for j in face.vertices),Vector())/len(face.vertices)
            guide=all((bake.data[j].uv-Vector(BLUE_UV)).length<.00001 for j in face.loop_indices)
            face.material_index=1 if guide or (1.57<p.z<1.93 and abs(p.y)>.137 and abs(p.y)<.145) else 0
    bpy.ops.object.select_all(action='DESELECT')
    for obj in stages: obj.select_set(True)
    bpy.context.view_layer.objects.active=stages[0]
    (SOURCE/'ArchConstructionReport.json').write_text(json.dumps(report,indent=2))
    (SOURCE/'ArchConstructionTopology.json').write_text(json.dumps(topology,indent=2))
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Arch_ConstructionStages.blend'))
    print(json.dumps({'stages':report,'chunkCount':len(pieces),'capUV':CAP_UV,'blueUV':BLUE_UV,
        'chunkBoundaryEdges':sum(t['boundaryEdges'] for t in topology if t['name'].startswith('Arch_Chunk_'))}))


build()
