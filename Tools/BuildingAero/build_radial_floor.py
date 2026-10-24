"""Build only the shared, sector-ready floor patch in the user's Blender.

The unit patch is bent to each annular cell by Unity. Other kit meshes stay unchanged.
"""
from pathlib import Path
exec(compile(Path('D:/PersonalProjects/the-tower-of-babel/Tools/BuildingAero/build_aquatic_architecture.py').read_text(encoding='utf-8-sig'),'aquatic_helpers','exec'),globals())
SCENE_NAME='Aero_RadialFloor'

def build_floor():
    global scene,SCENE,palette_materials
    scene=bpy.data.scenes.get(SCENE_NAME) or bpy.data.scenes.new(SCENE_NAME)
    SCENE=scene;bpy.context.window.scene=scene
    for obj in list(scene.objects):bpy.data.objects.remove(obj,do_unlink=True)
    scene.render.engine=bpy.data.scenes['Aero_AquaticArchitecture'].render.engine
    scene.cycles.samples=32;scene.unit_settings.scale_length=1
    palette_materials=[source_material('RadialFloor_White',(.97,.985,1)),source_material('RadialFloor_Ice',(.56,.82,.94)),source_material('RadialFloor_Blue',(.055,.46,.72))]
    tree=ast.parse((ROOT/'Tools/BuildingAero/build_assets.py').read_text(encoding='utf-8-sig'))
    exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in {'U','mesh_obj'}],type_ignores=[]),'floor_geometry_helpers','exec'),globals())
    exec(compile((ROOT/'Tools/BuildingAero/radial_floor_patch.py').read_text(),'radial_patch','exec'),globals())
    vertices,faces=radial_floor_patch()
    low=mesh_obj('LP_Radial_Floor_Tile',vertices,faces,0)
    color_bands(low,'Tile')
    low=collect('LP_Radial_Floor_Tile',[low],True)
    high=low.copy();high.data=low.data.copy();scene.collection.objects.link(high);high.name='HP_Radial_Floor_Tile'
    mat=prepare_low('Floor_Tile',low);PAIRS['Floor_Tile']=(low,high,mat)
    low['layout']='4 m inward X / 4 m negative-Z arc parameter; 16 angular spans, flush faces; Unity bends by instance radius and scale'
    print('RADIAL_FLOOR_BUILT',len(low.data.polygons),'triangles',len(low.data.vertices),'vertices')

def save_floor():
    low,high,mat=PAIRS['Floor_Tile'];low.hide_render=False;low.hide_set(False);high.hide_render=True;high.hide_set(True)
    bpy.context.view_layer.objects.active=low;low.select_set(True)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'BuildingKit_RadialFloor.blend'))
    print('RADIAL_FLOOR_SOURCE_SAVED',str(SOURCE/'BuildingKit_RadialFloor.blend'))
