"""Floor and step construction: random closed chunks, blueprints and matching stage meshes."""
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
SCENE_NAME='Aero_DeckConstruction'
tree=ast.parse((ROOT/'Tools/BuildingAero/build_pillar_stages.py').read_text())
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in
    {'enum','solid_piece','preview_material'}],type_ignores=[]),'pillar_chunk_helpers','exec'),globals())


def blueprint(kind,scene,material):
    vertices,faces=[],[]
    def tube(points):
        start=len(vertices)
        for i,p in enumerate(points):
            p=Vector(p); tangent=(Vector(points[min(i+1,len(points)-1)])-Vector(points[max(i-1,0)])).normalized()
            axis=Vector((0,0,1)) if abs(tangent.z)<.9 else Vector((0,1,0))
            a=tangent.cross(axis).normalized(); b=tangent.cross(a).normalized()
            for j in range(3): vertices.append(tuple(p+.007*(a*math.cos(j*math.tau/3)+b*math.sin(j*math.tau/3))))
        for i in range(len(points)-1):
            for j in range(3):
                a=start+i*3+j; b=start+i*3+(j+1)%3; faces.append((a,b,b+3,a+3))
        faces.append(tuple(start+j for j in (2,1,0)))
        faces.append(tuple(start+(len(points)-1)*3+j for j in range(3)))
    if kind=='Floor_Tile':
        # Subdivided edge parameters bend along the same six spans as the floor.
        outline=[(.012,-.012-3.976*j/6) for j in range(7)]+[(3.988,-.012-3.976*j/6) for j in range(6,-1,-1)]
    else:
        outline=[]; inset=.065; rx=.30-inset; ry=rx/.35
        for cx,cy,start in [(3.70,-.30/.35,0),(.30,-.30/.35,90),(.30,-4+.30/.35,180),(3.70,-4+.30/.35,270)]:
            for j in range(6):
                angle=math.radians(start+18*j); outline.append((cx+rx*math.cos(angle),cy+ry*math.sin(angle)))
    for z in (-.012,-.188): tube([(x,y,z) for x,y in outline]+[(outline[0][0],outline[0][1],z)])
    for i in range(0,len(outline),max(1,len(outline)//4)):
        x,y=outline[i]; tube([(x,y,-.012),(x,y,-.188)])
    mesh=bpy.data.meshes.new(kind+'_Blueprint'); mesh.from_pydata(vertices,[],faces); mesh.materials.append(material)
    bake=mesh.uv_layers.new(name='BakeUV'); palette=mesh.uv_layers.new(name='PaletteUV')
    for face in mesh.polygons:
        face.use_smooth=True
        for i in face.loop_indices: bake.data[i].uv=(.5,.5); palette.data[i].uv=(.3125,.5)
    mesh.update(); obj=bpy.data.objects.new(kind+'_Guides',mesh); scene.collection.objects.link(obj); return obj


def get_source(kind,scene):
    source=next((o for o in scene.objects if o.get('deck_completed_source')==kind),None)
    # The radial floor is newer than the original rounded slab in the kit source.
    if kind=='Floor_Tile' and source is not None and len(source.data.polygons)!=136:
        bpy.data.objects.remove(source,do_unlink=True); source=None
    if source is None:
        filename='BuildingKit_RadialFloor.blend' if kind=='Floor_Tile' else 'BuildingKit_AquaticArchitecture.blend'
        name='LP_Radial_Floor_Tile' if kind=='Floor_Tile' else 'LP_Aquatic_Step_Tile'
        with bpy.data.libraries.load(str(SOURCE/filename),link=False) as (src,dst): dst.objects=[name]
        source=dst.objects[0]; scene.collection.objects.link(source); source['deck_completed_source']=kind
    source.hide_render=True; source.hide_set(True)
    return source


def export(objects,path):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects: obj.hide_set(False); obj.location=(0,0,0); obj.scale=(1,1,1); obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',
        bake_space_transform=True,apply_scale_options='FBX_SCALE_ALL',add_leaf_bones=False,bake_anim=False,
        use_mesh_modifiers=True,mesh_smooth_type='FACE',use_triangles=True,path_mode='STRIP',embed_textures=False)


def build_kind(kind,scene,seed,row):
    for obj in list(scene.objects):
        if obj.name.startswith(tuple(kind+s for s in ('_Stage','_Chunk','_Piece','_Guides'))): bpy.data.objects.remove(obj,do_unlink=True)
    source=get_source(kind,scene); material=source.data.materials[0]
    face=min(source.data.polygons,key=lambda p:(p.center-Vector((2,-2,0))).length)
    uv=source.data.uv_layers.get('BakeUV')
    cap_uv=tuple(sum(uv.data[i].uv[axis] for i in face.loop_indices)/len(face.loop_indices) for axis in range(2))
    rng=random.Random(seed); seeds=[]
    while len(seeds)<80:
        point=Vector((rng.uniform(.08,3.92),rng.uniform(-3.75,-.25),rng.uniform(-.17,-.03)))
        if all((point-other).length>.12 for other in seeds): seeds.append(point)
    pieces=[solid_piece(source,i,seeds,scene,prefix=kind,cap_uv=cap_uv) for i in range(80)]
    order=list(range(80)); rng.shuffle(order); guides=blueprint(kind,scene,material); stages=[]; report=[]
    for stage in range(11):
        if stage==10:
            obj=bpy.data.objects.new(kind+'_Stage_10',source.data.copy()); scene.collection.objects.link(obj)
        else:
            parts=[]
            for piece_id in order[:stage*8]:
                part=bpy.data.objects.new(kind+'_StagePart',pieces[piece_id].data.copy()); scene.collection.objects.link(part); parts.append(part)
            guide=bpy.data.objects.new(kind+'_StageGuide',guides.data.copy()); scene.collection.objects.link(guide); parts.append(guide)
            bpy.ops.object.select_all(action='DESELECT')
            for part in parts: part.select_set(True)
            bpy.context.view_layer.objects.active=parts[0]
            if len(parts)>1: bpy.ops.object.join()
            obj=bpy.context.object; obj.name=f'{kind}_Stage_{stage:02}'
        obj.data.materials.clear(); obj.data.materials.append(material)
        for face in obj.data.polygons: face.material_index=0
        bpy.context.view_layer.objects.active=obj
        mod=obj.modifiers.new('Construction triangles','TRIANGULATE'); mod.quad_method=enum(mod,'quad_method','FIXED')
        bpy.ops.object.modifier_apply(modifier=mod.name)
        stages.append(obj); obj['filled_piece_ids']=order[:stage*8]
        report.append({'stage':stage,'seed':seed,'filledPieceIds':list(obj['filled_piece_ids']),'triangles':len(obj.data.polygons)})
    for i,piece in enumerate(pieces):
        piece.name=f'{kind}_Chunk_{order.index(i)//8+1:02}_{i:02}'; piece.data.materials.clear(); piece.data.materials.append(material)
        for face in piece.data.polygons: face.material_index=0
        bpy.context.view_layer.objects.active=piece
        mod=piece.modifiers.new('Chunk triangles','TRIANGULATE'); mod.quad_method=enum(mod,'quad_method','FIXED')
        bpy.ops.object.modifier_apply(modifier=mod.name)
    EXPORT.mkdir(parents=True,exist_ok=True)
    export(pieces,EXPORT/(kind+'Chunks.fbx')); export(stages[:-1],EXPORT/(kind+'Stages.fbx'))
    topology=[]
    for obj in pieces+stages:
        bm=bmesh.new(); bm.from_mesh(obj.data)
        topology.append({'name':obj.name,'boundaryEdges':sum(e.is_boundary for e in bm.edges),
            'nonManifoldEdges':sum(not e.is_manifold for e in bm.edges),'volume':abs(bm.calc_volume())}); bm.free()
    for obj in pieces+[guides]: obj.hide_render=True; obj.hide_set(True)
    white=preview_material('DeckStageReview_White',(.92,.96,1)); blue=preview_material('DeckStageReview_Blue',(.035,.48,.8))
    for i,obj in enumerate(stages):
        obj.location=(i*4.8,row,0); obj.scale=(1,.35 if kind=='Step_Tile' else 1,1)
        obj.data.materials.clear(); obj.data.materials.append(white); obj.data.materials.append(blue)
        palette=obj.data.uv_layers.get('PaletteUV')
        for face in obj.data.polygons: face.material_index=1 if palette.data[face.loop_indices[0]].uv.x>.2 else 0
    (SOURCE/(kind+'ConstructionReport.json')).write_text(json.dumps(report,indent=2))
    (SOURCE/(kind+'ConstructionTopology.json')).write_text(json.dumps(topology,indent=2))
    chunks=[t for t in topology if '_Chunk_' in t['name']]
    return {'kind':kind,'chunks':len(pieces),'sourceTriangles':len(source.data.polygons),
        'minChunkVolume':min(t['volume'] for t in chunks),'maxChunkVolume':max(t['volume'] for t in chunks),
        'boundaryEdges':sum(t['boundaryEdges'] for t in topology),'nonManifoldEdges':sum(t['nonManifoldEdges'] for t in topology),
        'stages':[r['triangles'] for r in report]}


def build():
    scene=bpy.data.scenes.get(SCENE_NAME) or bpy.data.scenes.new(SCENE_NAME); bpy.context.window.scene=scene
    if bpy.context.object and bpy.context.object.mode!='OBJECT': bpy.ops.object.mode_set(mode='OBJECT')
    for obj in list(scene.objects):
        if obj.get('deck_current_fbx'): bpy.data.objects.remove(obj,do_unlink=True)
    result=[build_kind('Floor_Tile',scene,146892,0),build_kind('Step_Tile',scene,962415,-6)]
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Deck_ConstructionStages.blend'))
    print(json.dumps(result))


build()
