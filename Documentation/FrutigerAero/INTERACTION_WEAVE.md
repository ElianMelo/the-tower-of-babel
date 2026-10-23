# Interaction asset weave

Gathering stones and constructing building stages use a slow, buoyant figure-eight weave in place of the former fast positional shake. The motion has a small lateral sway, a shallow rising arc, depth movement and a restrained tilt. It eases in over 0.22 seconds, settles through the final 18% of the action and eases back on cancellation. Cooldown, disable and pool unbinding restore the source renderer immediately.

The existing strength and tempo settings remain serialized under their original names, preserving prefab and resource-definition values. Their inspector labels are now **Weave Strength** and **Weave Tempo**. The default strength is 0.05 world units and tempo 24 gives a 2.6-second cycle, with at most a 2.4-degree tilt at that strength. Larger tempo values run faster. Position amplitude stays independent of instance scale.

`AeroInteractionMotion` creates reusable render-only copies on first interaction. The source transforms, mesh assets, colliders, resource IDs and construction positions stay fixed. The copies follow the source's current mesh, shared materials, material-property block, rendering layers and enabled state, so aquatic finishes and stage changes continue to use their normal data. Copies live outside the gameplay object's hierarchy to keep collider queries, outline renderer caches and pooled-building ownership unchanged. They hide when motion ends and are destroyed with their owner. No prefab hierarchy or material asset is rewritten.

This change applies to the existing gathering and construction asset motion. The processor had no shake to replace. The XP bar and button feedback keep their existing motion.

## Visual review

[Edit Mode animation](Previews/Interaction-Weave.gif) shows the existing stone and pillar prefabs in a disposable preview scene. `AeroInteractionMotionPreview.Capture()` samples the actual controller and saves the frames; `Tools/ExperienceFeedback/assemble_weave_preview.py` assembles the animation.

Unity compilation passed. At 0.5, 1.5 and 2.5 seconds the visual pose changed while both source-root matrices and both collider matrices remained unchanged. Source renderers were restored after the preview. `InteractionWeaveInspection.json` records the samples. The original Tower scene stayed clean in Edit Mode. No automated tests or Play Mode sessions were run; live interaction/cancellation, network timing and pooled reuse remain unverified in gameplay.
