"""Author three distinct rounded broadleaf shrubs in the live Blender scene."""
import bpy
import bmesh
import math
import random
import json
from pathlib import Path
from mathutils import Vector

ROOT=Path('D:/PersonalProjects/the-tower-of-babel')
EXPORT=ROOT/'Assets/_Project/Models/Foliage'
SOURCE=ROOT/'ArtSource/Foliage'
scene=bpy.data.scenes.get('Aero_GreenBushes') or bpy.data.scenes.new('Aero_GreenBushes')
bpy.context.window.scene=scene
if bpy.context.object and bpy.context.object.mode!='OBJECT': bpy.ops.object.mode_set(mode='OBJECT')
for obj in list(scene.objects):
    if obj.name.startswith('AeroBush_'): bpy.data.objects.remove(obj,do_unlink=True)
for folder in (EXPORT,SOURCE): folder.mkdir(parents=True,exist_ok=True)


def enum(owner,prop,identifier):
    return next(i.identifier for i in owner.bl_rna.properties[prop].enum_items if i.identifier==identifier)


def bush(name,seed,spread,height,lobes,leaves):
    rng=random.Random(seed)
    vertices,faces,colors=[],[],[]

    def vertex(p,color):
        vertices.append(tuple(p)); colors.append((*color,max(0,min(1,p[2]/height))))
        return len(vertices)-1

    def foliage_color(shade=1):
        return (.075*shade,.34*shade,.024*shade)

    crowns=[]
    for i in range(lobes):
        a=i*math.tau/lobes+rng.uniform(-.4,.4)
        offset=spread*(.18 if i else .05)
        center=Vector((math.cos(a)*offset,math.sin(a)*offset,height*rng.uniform(.36,.52)))
        scale=Vector((spread*rng.uniform(.27,.36),spread*rng.uniform(.26,.34),height*rng.uniform(.33,.43)))
        crowns.append((center,scale))
        start=len(vertices)
        for lat in range(7):
            phi=math.pi*lat/6
            for lon in range(12):
                theta=math.tau*lon/12
                n=Vector((math.sin(phi)*math.cos(theta),math.sin(phi)*math.sin(theta),math.cos(phi)))
                p=center+Vector((n.x*scale.x,n.y*scale.y,n.z*scale.z))*.88
                p.z=max(.025,p.z)
                vertex(p,foliage_color(rng.uniform(.7,1.1)*(1+.13*n.z)))
        for lat in range(6):
            for lon in range(12):
                a=start+lat*12+lon; b=start+lat*12+(lon+1)%12
                faces.extend([(a,b,b+12),(a,b+12,a+12)])
    # Individually shaped, slightly cupped leaves break up the rounded canopy silhouette.
    for leaf in range(leaves):
        center,scale=rng.choice(crowns)
        a=rng.uniform(0,math.tau); z=rng.uniform(-.35,1)
        n=Vector((math.sqrt(1-z*z)*math.cos(a),math.sqrt(1-z*z)*math.sin(a),z))
        root=center+Vector((n.x*scale.x,n.y*scale.y,n.z*scale.z))*.94
        tangent=Vector((0,0,1))-n*n.z
        if tangent.length<.1: tangent=Vector((1,0,0))
        direction=(tangent.normalized()+n*.28).normalized()
        side=direction.cross(n).normalized()
        up=side.cross(direction).normalized()
        length=spread*rng.uniform(.20,.30); width=length*rng.uniform(.38,.5)
        root-=direction*length*.4
        shade=rng.uniform(.88,1.38)
        outline=[(0,0),(-width*.5,length*.22),(-width,length*.52),(0,length),(width,length*.52),(width*.5,length*.22)]
        ids=[]
        for x,y in outline:
            p=root+side*x+direction*y+up*(math.sin(y/length*math.pi)*length*.12)
            p.z=max(.012,p.z)
            ids.append(vertex(p,foliage_color(shade)))
        ridge=vertex(root+direction*length*.49+up*length*.21,foliage_color(shade*1.16))
        underside=vertex(root+direction*length*.49-up*length*.035,foliage_color(shade*.75))
        for i in range(6):
            faces.extend([(ids[i],ids[(i+1)%6],ridge),(ids[(i+1)%6],ids[i],underside)])
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(vertices,[],faces); mesh.update()
    color=mesh.color_attributes.new(name='LeafColorAndWind',type='FLOAT_COLOR',domain='CORNER')
    for i,loop in enumerate(mesh.loops): color.data[i].color=colors[loop.vertex_index]
    for poly in mesh.polygons: poly.use_smooth=True
    # Weld sphere poles, remove zero-area faces and orient every closed leaf/crown outward.
    bm=bmesh.new(); bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001)
    bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=.000001)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(mesh); bm.free(); mesh.update()
    obj=bpy.data.objects.new(name,mesh); scene.collection.objects.link(obj)
    material=bpy.data.materials.get('AeroBush_LeafPreview') or bpy.data.materials.new('AeroBush_LeafPreview')
    material.use_nodes=True
    bsdf=next(n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    attr=next((n for n in material.node_tree.nodes if n.type=='VERTEX_COLOR'),None) or material.node_tree.nodes.new('ShaderNodeVertexColor')
    attr.layer_name='LeafColorAndWind'
    material.node_tree.links.new(attr.outputs['Color'],bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value=.44
    material.diffuse_color=(.1,.46,.025,1)
    mesh.materials.append(material)
    return obj


objects=[bush('AeroBush_Small',71,.64,.48,3,72),
         bush('AeroBush_Medium',193,1.05,.78,5,125),
         bush('AeroBush_Large',358,1.46,1.14,7,185)]
bpy.ops.object.select_all(action='DESELECT')
for obj in objects: obj.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
options=dict(filepath=str(EXPORT/'AeroBushes.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',
             bake_space_transform=True,apply_scale_options='FBX_SCALE_ALL',add_leaf_bones=False,bake_anim=False,
             use_mesh_modifiers=True,mesh_smooth_type='FACE',use_triangles=True,path_mode='STRIP',embed_textures=False)
props=bpy.ops.export_scene.fbx.get_rna_type().properties
if 'colors_type' in props:
    options['colors_type']=next(i.identifier for i in props['colors_type'].enum_items if i.identifier=='LINEAR')
bpy.ops.export_scene.fbx(**options)
report=[]
for i,obj in enumerate(objects):
    report.append({'name':obj.name,'vertices':len(obj.data.vertices),'triangles':len(obj.data.polygons),'size':list(obj.dimensions)})
    obj.location.x=i*2
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.shading.color_type=enum(area.spaces.active.shading,'color_type','MATERIAL')
        region=next(r for r in area.regions if r.type=='WINDOW')
        with bpy.context.temp_override(area=area,region=region): bpy.ops.view3d.view_selected(use_all_regions=False)
(SOURCE/'AeroBushReport.json').write_text(json.dumps(report,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Aero_GreenBushes.blend'))
print(json.dumps(report))
