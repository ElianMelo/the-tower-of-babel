"""Approximate the Unity water-glass finish in the editable Blender source."""
import bpy
from pathlib import Path

root=Path('D:/PersonalProjects/the-tower-of-babel')
scene=bpy.data.scenes['Aero_BuildingKit']
bpy.context.window.scene=scene
materials={slot.material for obj in scene.objects if obj.type=='MESH' for slot in obj.material_slots
           if slot.material and slot.material.name.startswith('Aero_BuildingAtlas')}
for material in materials:
    material.use_nodes=True
    nodes=material.node_tree.nodes;links=material.node_tree.links
    p=nodes.get('Principled BSDF')
    for socket in ('Base Color','Metallic','Roughness','Emission Color'):
        for link in list(p.inputs[socket].links):links.remove(link)
    p.inputs['Metallic'].default_value=0
    p.inputs['Roughness'].default_value=.11
    p.inputs['Transmission Weight'].default_value=.52
    p.inputs['IOR'].default_value=1.45
    p.inputs['Coat Weight'].default_value=.5
    p.inputs['Emission Strength'].default_value=.08
    coords=nodes.new('ShaderNodeTexCoord')
    mapping=nodes.new('ShaderNodeMapping');mapping.name='Internal water flow'
    mapping.inputs['Scale'].default_value=(.30,.30,.30)
    links.new(coords.outputs['Object'],mapping.inputs['Vector'])
    for axis,speed in ((0,.0012),(1,.0009),(2,-.0007)):
        driver=mapping.inputs['Location'].driver_add('default_value',axis).driver
        driver.expression=f'frame * {speed}'
    caustic=nodes.new('ShaderNodeTexImage');caustic.name='Internal blue caustics'
    caustic.image=bpy.data.images.load(str(root/'Assets/_Project/Models/Buildings/FrutigerAero/Aero_WaterCaustics.png'),check_existing=True)
    caustic.image.colorspace_settings.name='Non-Color';caustic.extension='REPEAT'
    caustic.projection='BOX';caustic.projection_blend=.3
    links.new(mapping.outputs['Vector'],caustic.inputs['Vector'])
    ramp=nodes.new('ShaderNodeValToRGB');ramp.name='White glass / soft blue water'
    ramp.color_ramp.elements[0].position=.08
    ramp.color_ramp.elements[0].color=(.91,.965,1,1)
    ramp.color_ramp.elements[1].position=.85
    ramp.color_ramp.elements[1].color=(.50,.79,1,1)
    links.new(caustic.outputs['Color'],ramp.inputs['Fac'])
    links.new(ramp.outputs['Color'],p.inputs['Base Color'])
    links.new(ramp.outputs['Color'],p.inputs['Emission Color'])
scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(root/'ArtSource/Buildings/FrutigerAero/BuildingKit.blend'))
print('BLENDER_WHITE_WATER_GLASS_UPDATED', len(materials))
