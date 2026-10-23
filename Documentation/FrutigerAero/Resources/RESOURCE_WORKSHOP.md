# Stone and processor assets

The later [sand, glass and gathering feedback pass](SAND_GLASS.md) extends this workshop with a sand resource, glass kiln, capped Unity particles, rock cooldown bricks and spatial audio. Its validation report supersedes the historical validation note below for the updated resource flow.

The stone resource is now a faceted three-rock cluster with a generated silvery gray stone texture. The processor is an open-front machine with a pearl ceramic housing, azure roller hubs, brushed steel drums, a hopper, press, output tray and three cyan status lights. Its rounded white/blue surfaces follow the project's nature-and-technology direction.

## Assets and textures

- Editable Blender source: `ArtSource/Resources/Stone_And_Processor.blend`, scene `Aero_ResourceWorkshop`.
- Rebuild script: `Tools/BuildingAero/build_resource_station.py`, executed through the connected Blender instance.
- Exported models: `Assets/_Project/Models/Resources/StoneCluster.fbx` and `StoneProcessor.fbx`.
- Texture source and exact prompts: [TexturePrompts.md](TexturePrompts.md). Both PNGs were generated with built-in image generation, then copied unchanged into `Assets/_Project/Models/Resources/Textures/`.
- `StoneSurface-Imagegen.png` supplies the raw stone and tinted processed sample. `ProcessorAtlas-Imagegen.png` contains four UV regions: pearl ceramic, azure metal, dark steel and pale cyan enamel. Material UVs stay inside each quadrant to avoid bleeding across swatches.
- Six shared URP Lit materials live in the models' `Materials/` folder. Metallic and smoothness are material settings; the generated PNGs are base-color textures, not normal or roughness maps.

The stone uses 960 triangles. The processor uses 8,804 triangles across nine meshes, including the feed stone and three status lights. Existing prefab GUIDs, root positions, resource data, processor recipe and interaction duration are preserved. The visible children are offset to meet the current world ground while keeping the root identity stable. The existing stone sphere and processor box colliders stay fixed during animation.

## Working motion

`ProcessorWorkAnimation` is attached to the existing processor prefab and bound to its separate moving parts. `ResourceProcessor` starts the animation at interaction begin, supplies normalized progress, and stops it on cancellation or server rejection. Server-confirmed completion adds a brief outward/upward output-stone motion and brighter cyan lights. Duplicate completion notifications do not restart that response.

The hopper and output tray are empty while idle. On interaction, raw stone drops into the hopper over 0.4 seconds, bobs and tilts while the rollers counter-rotate, then sinks and shrinks into the machine between 55% and 94% progress. Cancellation and rejection hide the feed without producing a block. Only confirmed completion reveals processed stone: it grows onto the tray, rises gently, slides outward and disappears after 1.1 seconds. Status lights follow progress and brighten on completion.

Work blends in and out over roughly 0.22 seconds. Disabling the processor restores its part transforms and original light properties, and hides both stones. Root transforms and collider dimensions are never animated. The presentation does not grant resources or change recipes or upgrade calculations. The reward receipt now uses the output tray's world position as its bubble origin. It is local interaction feedback; remote player work animation is not synchronized.

## Authoring and review

`ResourceWorkshopAuthoring.Apply()` updates the existing prefabs through Unity and binds the models, materials and animation references. Use it only from a saved, clean Tower scene in this project. The menu entry is **Tower of Babel → Resources → Apply textured stone and processor**.

`ResourceWorkshopPreview.Capture()` renders disposable Edit Mode preview objects and samples their animation without entering Play Mode. `Tools/BuildingAero/assemble_processor_work.py` assembles those rendered frames into the review GIF. Its four-second cycle is illustrative; gameplay speed still follows the actual interaction progress and server response.

- [World placement](Previews/Stone-Processor-World.png) shows the updated live scene prefab instances.
- [Idle asset lineup](Previews/Stone-Processor-Idle.png) shows the imported geometry and materials.
- [Working cycle](Previews/Processor-Working.gif) shows rollers, feed stone, press, status lights and completion response.
- [Asset inspection](ResourceWorkshopInspection.json) records prefab identity, mesh counts and texture references.
- [Motion inspection](ProcessorMotionInspection.json) records stone visibility at idle, entry, work, consumption, cancellation, completion and collection; fixed collider bounds; no compilation failure; and no URP Lit shader messages.

Compilation and rendered Edit Mode inspection passed. The Tower scene remains clean and the Editor remains in Edit Mode. No automated tests or Play Mode sessions were run; server-driven gameplay behavior was reviewed in code and remains unverified in a live session.
