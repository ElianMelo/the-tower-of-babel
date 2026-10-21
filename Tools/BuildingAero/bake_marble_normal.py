"""Bake a tangent-space marble relief map in Blender; leave the user's scene active."""
import bpy
import json
from pathlib import Path
from array import array

root=Path('D:/PersonalProjects/the-tower-of-babel')
original=bpy.context.window.scene
scene=bpy.data.scenes.new('Aero_MarbleNormalBake_Temporary')
scene.render.engine=original.render.engine
scene.cycles.samples=16
scene.cycles.use_denoising=False
scene.render.bake.use_selected_to_active=False
scene.render.bake.margin=16
bpy.context.window.scene=scene
mesh=bpy.data.meshes.new('MarbleNormalBakePlane')
mesh.from_pydata([(-.75,-.5,0),(.75,-.5,0),(.75,.5,0),(-.75,.5,0)],[],[(0,1,2,3)])
uv=mesh.uv_layers.new()
for loop,coord in zip(uv.data,[(0,0),(1,0),(1,1),(0,1)]):loop.uv=coord
obj=bpy.data.objects.new('MarbleNormalBakePlane',mesh);scene.collection.objects.link(obj)
mat=bpy.data.materials.new('MarbleNormalBakeTemporary');mat.use_nodes=True;obj.data.materials.append(mat)
nodes=mat.node_tree.nodes;links=mat.node_tree.links
principled=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
source=nodes.new('ShaderNodeTexImage')
source.image=bpy.data.images.load(str(root/'Assets/_Project/Models/Buildings/FrutigerAero/Aero_WhiteMarble.png'),check_existing=True)
vein=nodes.new('ShaderNodeBump');vein.inputs['Strength'].default_value=.32;vein.inputs['Distance'].default_value=.002
links.new(source.outputs['Color'],vein.inputs['Height'])
noise=nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=180;noise.inputs['Detail'].default_value=2
grain=nodes.new('ShaderNodeBump');grain.inputs['Strength'].default_value=.3;grain.inputs['Distance'].default_value=.00035
links.new(noise.outputs['Fac'],grain.inputs['Height']);links.new(vein.outputs['Normal'],grain.inputs['Normal'])
links.new(grain.outputs['Normal'],principled.inputs['Normal'])
image=bpy.data.images.new('Aero_WhiteMarble_Normal',width=2048,height=1365,float_buffer=True,is_data=True)
image.colorspace_settings.name='Non-Color'
target=nodes.new('ShaderNodeTexImage');target.image=image
for node in nodes:node.select=False
target.select=True;nodes.active=target
obj.select_set(True);bpy.context.view_layer.objects.active=obj
normal_type=next(i.identifier for i in bpy.ops.object.bake.get_rna_type().properties['type'].enum_items if i.identifier=='NORMAL')
try:
    bpy.ops.object.bake(type=normal_type)
    image.filepath_raw=str(root/'Assets/_Project/Models/Buildings/FrutigerAero/Aero_WhiteMarble_Normal.png')
    image.file_format=next(i.identifier for i in image.bl_rna.properties['file_format'].enum_items if i.identifier=='PNG')
    image.save()
    data=array('f',[0])*len(image.pixels);image.pixels.foreach_get(data)
    report={'width':image.size[0],'height':image.size[1],'space':'tangent OpenGL / Unity',
            'redRange':[min(data[0::4]),max(data[0::4])],
            'greenRange':[min(data[1::4]),max(data[1::4])],
            'blueRange':[min(data[2::4]),max(data[2::4])],
            'source':'Cycles normal bake of marble vein relief and fine mineral grain'}
    (root/'ArtSource/Buildings/FrutigerAero/MarbleNormalBake.json').write_text(json.dumps(report,indent=2))
    print('NORMAL_MAP_BAKED',json.dumps(report))
finally:
    bpy.context.window.scene=original
    bpy.data.objects.remove(obj,do_unlink=True)
    bpy.data.meshes.remove(mesh)
    bpy.data.materials.remove(mat)
    bpy.data.scenes.remove(scene)
