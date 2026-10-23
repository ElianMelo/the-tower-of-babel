"""Run through Blender MCP. Only authors pillar construction assets.

The original completed pillar and its baked UVs are retained. Stages are closed
irregular Voronoi chunks in a seeded random order, plus sparse blue guides.
Unity uses the original finished FBX in slot 10 and one shared material in every slot.
"""
from pathlib import Path
import bpy
import bmesh
import math
import json
import random
from mathutils import Vector

ROOT = Path('D:/PersonalProjects/the-tower-of-babel')
SOURCE = ROOT / 'ArtSource/Buildings/FrutigerAero'
EXPORT = ROOT / 'Assets/_Project/Models/Buildings/FrutigerAero/Construction'
SCENE_NAME = 'Aero_PillarConstruction'
LABELS = ['Blueprint']+[f'Random fill {i*10}%' for i in range(1,10)]+['Complete']
PROFILE = [(0,.37),(.025,.395),(.06,.41002339),(.16,.41002339),(.205,.39),(.24,.34),
           (.32,.31),(.45,.296),(2.9,.286),(3.40,.30),(3.50,.34),(3.60,.38),
           (3.70,.397),(3.86,.41002339),(3.97,.41002339),(4,.385)]


def enum(owner, prop, identifier):
    return next(i.identifier for i in owner.bl_rna.properties[prop].enum_items if i.identifier == identifier)


def radius(z):
    for (a,r),(b,s) in zip(PROFILE,PROFILE[1:]):
        if a <= z <= b:
            return r + (s-r)*(z-a)/(b-a)
    return PROFILE[-1][1]


def source_pillar(scene):
    obj = scene.objects.get('LP_Aquatic_Pillar')
    if obj is None:
        with bpy.data.libraries.load(str(SOURCE/'BuildingKit_AquaticArchitecture.blend'),link=False) as (src,dst):
            dst.objects = ['LP_Aquatic_Pillar']
        obj = dst.objects[0]
        scene.collection.objects.link(obj)
    obj.location = (0,0,0)
    obj.hide_render = True
    obj.hide_set(True)
    return obj


def solid_piece(source, index, seeds, scene, prefix='Pillar', cap_uv=None):
    obj = bpy.data.objects.new(f'{prefix}_Piece_{index:02}',source.data.copy())
    scene.collection.objects.link(obj)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bake_uv = bm.loops.layers.uv.get('BakeUV')
    palette_uv = bm.loops.layers.uv.get('PaletteUV')
    seed=seeds[index]
    # A clipped Voronoi cell makes an irregular solid chunk. No radial sectors,
    # horizontal rows or shared lift heights determine the construction order.
    neighbors=sorted((p for i,p in enumerate(seeds) if i!=index),key=lambda p:(p-seed).length_squared)
    planes=[((seed+p)*.5,(p-seed).normalized()) for p in neighbors]
    for center,normal in planes:
        center,normal=Vector(center),Vector(normal)
        bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),
                              dist=.000001,plane_co=center,plane_no=normal,clear_outer=True,clear_inner=False)
        cut_edges=[e for e in bm.edges if e.is_boundary and all(abs((v.co-center).dot(normal))<.00001 for v in e.verts)]
        cap_faces=bmesh.ops.holes_fill(bm,edges=cut_edges,sides=0)['faces']
        for face in cap_faces:
            face.smooth=False
            for loop in face.loops:
                p=loop.vert.co
                if bake_uv: loop[bake_uv].uv=cap_uv if cap_uv is not None else (.5+p.x*.4,.5+(p.y if abs(normal.z)>.5 else p.z*.2)*.4)
                if palette_uv: loop[palette_uv].uv=(.0625,.5)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    return obj


def blueprint(height, stage, scene, material):
    verts, faces = [], []

    def tube(points, thickness=.009):
        start = len(verts)
        for i,p in enumerate(points):
            p = Vector(p)
            tangent = (Vector(points[min(i+1,len(points)-1)])-Vector(points[max(0,i-1)])).normalized()
            axis = Vector((0,0,1)) if abs(tangent.z)<.9 else Vector((0,1,0))
            a = tangent.cross(axis).normalized()
            b = tangent.cross(a).normalized()
            for j in range(3):
                q = p + thickness*(a*math.cos(j*math.tau/3)+b*math.sin(j*math.tau/3))
                verts.append(tuple(q))
        for i in range(len(points)-1):
            for j in range(3):
                a = start+i*3+j
                b = start+i*3+(j+1)%3
                faces.append((a,b,b+3,a+3))
        faces.append(tuple(start+j for j in (2,1,0)))
        faces.append(tuple(start+(len(points)-1)*3+j for j in range(3)))

    bottom, top = max(height+.012,.012),3.988
    if bottom >= top:
        return None
    ys = [bottom]+[y for y,r in PROFILE if bottom+.001<y<top-.001]+[top]
    for rail in range(4):
        angle = math.tau*rail/4 + math.pi/4
        tube([((radius(y)-.012)*math.cos(angle),(radius(y)-.012)*math.sin(angle),y) for y in ys])
    # Profile landmarks communicate the foot, shaft, collar and capital without a triangle web.
    for y in [.012,.16,.45,3.4,3.6,3.988]:
        if y < bottom-.001: continue
        r = radius(y)-.012
        tube([(r*math.cos(i*math.tau/12),r*math.sin(i*math.tau/12),y) for i in range(13)])
    mesh = bpy.data.meshes.new(f'Pillar_Blueprint_{stage:02}')
    mesh.from_pydata(verts,[],faces)
    mesh.materials.append(material)
    mesh.uv_layers.new(name='BakeUV')
    palette = mesh.uv_layers.new(name='PaletteUV')
    for poly in mesh.polygons:
        poly.use_smooth = True
        for li in poly.loop_indices:
            mesh.uv_layers[0].data[li].uv = (.5,.5)
            palette.data[li].uv = (.3125,.5)
    mesh.update()
    obj = bpy.data.objects.new(f'Pillar_Guides_{stage:02}',mesh)
    scene.collection.objects.link(obj)
    return obj


def preview_material(name, color):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    bsdf.inputs['Base Color'].default_value = (*color,1)
    bsdf.inputs['Roughness'].default_value = .25
    bsdf.inputs['Metallic'].default_value = .1
    mat.diffuse_color = (*color,1)
    return mat


def build():
    scene = bpy.data.scenes.get(SCENE_NAME) or bpy.data.scenes.new(SCENE_NAME)
    bpy.context.window.scene = scene
    if bpy.context.object and bpy.context.object.mode!='OBJECT': bpy.ops.object.mode_set(mode='OBJECT')
    for obj in list(scene.objects):
        if obj.name.startswith(('Pillar_Stage_','Pillar_Solid_','Pillar_Guides_','Pillar_Piece_','Pillar_Chunk_','StageLabel_')):
            bpy.data.objects.remove(obj,do_unlink=True)
    source = source_pillar(scene)
    scene.unit_settings.system = enum(scene.unit_settings,'system','METRIC')
    scene.unit_settings.scale_length = 1
    material = source.data.materials[0]
    white = preview_material('PillarStageReview_White',(.92,.96,1))
    blue = preview_material('PillarStageReview_Blue',(.035,.48,.8))
    report, stages = [], []
    rng=random.Random(712809)
    seeds=[]
    while len(seeds)<80:
        angle=rng.uniform(0,math.tau); radius=.27*math.sqrt(rng.random())
        point=Vector((radius*math.cos(angle),radius*math.sin(angle),rng.uniform(.015,3.985)))
        if all((point-other).length>.105 for other in seeds): seeds.append(point)
    pieces=[solid_piece(source,i,seeds,scene) for i in range(len(seeds))]
    order=list(range(len(pieces))); rng.shuffle(order)
    EXPORT.mkdir(parents=True,exist_ok=True)
    for stage in range(11):
        if stage==10:
            obj = bpy.data.objects.new('Pillar_Stage_10',source.data.copy())
            scene.collection.objects.link(obj)
        else:
            parts = []
            for piece_id in order[:len(pieces)*stage//10]:
                part=bpy.data.objects.new(f'Pillar_Solid_{stage:02}_{piece_id}',pieces[piece_id].data.copy())
                scene.collection.objects.link(part); parts.append(part)
            guides = blueprint(0,stage,scene,material)
            if guides: parts.append(guides)
            bpy.ops.object.select_all(action='DESELECT')
            for part in parts: part.select_set(True)
            bpy.context.view_layer.objects.active = parts[0]
            if len(parts)>1: bpy.ops.object.join()
            obj = bpy.context.object
            obj.name = f'Pillar_Stage_{stage:02}'
        # Join into exactly one submesh. UV1 preserves white/blue regions in Unity.
        obj.data.materials.clear()
        obj.data.materials.append(material)
        for p in obj.data.polygons: p.material_index = 0
        bpy.context.view_layer.objects.active = obj
        mod = obj.modifiers.new('Fixed construction triangles','TRIANGULATE')
        mod.quad_method = enum(mod,'quad_method','FIXED')
        bpy.ops.object.modifier_apply(modifier=mod.name)
        obj['construction_stage'] = stage
        obj['random_seed'] = 712809
        obj['filled_piece_ids'] = order[:len(pieces)*stage//10]
        obj['phase'] = LABELS[stage]
        obj['unity_material'] = 'Aero_WhiteMarble_Pillar'
        obj.location = (stage*1.35,0,0)
        stages.append(obj)
        report.append({'stage':stage,'phase':LABELS[stage],'randomSeed':712809,'filledPieceIds':list(obj['filled_piece_ids']),
                       'triangles':len(obj.data.polygons),
                       'vertices':len(obj.data.vertices),'uvLayers':[uv.name for uv in obj.data.uv_layers]})
    # Separate meshes let Unity form and fly only the pieces added on this step.
    bpy.ops.object.select_all(action='DESELECT')
    for index,piece in enumerate(pieces):
        piece.name=f'Pillar_Chunk_{order.index(index)//8+1:02}_{index:02}'
        piece.data.materials.clear(); piece.data.materials.append(material)
        for face in piece.data.polygons: face.material_index=0
        bpy.context.view_layer.objects.active=piece
        modifier=piece.modifiers.new('Chunk triangles','TRIANGULATE')
        modifier.quad_method=enum(modifier,'quad_method','FIXED')
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        piece.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(EXPORT/'PillarChunks.fbx'),use_selection=True,object_types={'MESH'},
                            axis_forward='-Z',axis_up='Y',bake_space_transform=True,apply_scale_options='FBX_SCALE_ALL',
                            add_leaf_bones=False,bake_anim=False,use_mesh_modifiers=True,mesh_smooth_type='FACE',use_triangles=True,
                            path_mode='STRIP',embed_textures=False)
    for piece in pieces: piece.hide_render=True; piece.hide_set(True)
    # Export at the original origin, independent of the review lineup.
    bpy.ops.object.select_all(action='DESELECT')
    for obj in stages[:-1]:
        obj.location=(0,0,0)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = stages[0]
    bpy.ops.export_scene.fbx(filepath=str(EXPORT/'PillarStages.fbx'),use_selection=True,object_types={'MESH'},
                            axis_forward='-Z',axis_up='Y',bake_space_transform=True,apply_scale_options='FBX_SCALE_ALL',
                            add_leaf_bones=False,bake_anim=False,use_mesh_modifiers=True,mesh_smooth_type='FACE',use_triangles=True,
                            path_mode='STRIP',embed_textures=False)
    # Separate preview-only material slots in Blender; the exported meshes keep one shared material.
    for stage,obj in enumerate(stages):
        obj.location=(stage*1.35,0,0)
        obj.data.materials.clear()
        obj.data.materials.append(white)
        obj.data.materials.append(blue)
        palette = obj.data.uv_layers.get('PaletteUV')
        for poly in obj.data.polygons:
            poly.material_index = 1 if palette.data[poly.loop_indices[0]].uv.x>.2 else 0
        curve=bpy.data.curves.new(f'StageLabel_{stage:02}',type='FONT')
        curve.body=f'{stage:02}\n{LABELS[stage]}'
        curve.align_x=enum(curve,'align_x','CENTER')
        curve.size=.13
        label=bpy.data.objects.new(f'StageLabel_{stage:02}',curve)
        scene.collection.objects.link(label)
        label.location=(stage*1.35,-.35,-.28)
        label.rotation_euler=(math.pi/2,0,0)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in stages: obj.select_set(True)
    bpy.context.view_layer.objects.active=stages[0]
    for area in bpy.context.screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.shading.color_type=enum(area.spaces.active.shading,'color_type','MATERIAL')
            region = next(r for r in area.regions if r.type=='WINDOW')
            with bpy.context.temp_override(area=area,region=region): bpy.ops.view3d.view_selected(use_all_regions=False)
    (SOURCE/'PillarConstructionReport.json').write_text(json.dumps(report,indent=2))
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Pillar_ConstructionStages.blend'))
    print('PILLAR_STAGES_EXPORTED',json.dumps(report))


build()
