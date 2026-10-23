# Fluid UI feedback

The interface uses short glass and water gestures throughout the existing uGUI prefabs. Motion runs on unscaled time. Gameplay amounts and purchase state remain authoritative; these components only present their changes.

- Inventory and upgrades reveal over 0.24 seconds with a 10-unit rise and 1.5% scale settling, and dismiss over 0.14 seconds. Closing releases raycasts immediately. Reopening reverses the current transition. Logical visibility changes immediately so menu shortcuts and cursor restoration still work during the fade.
- Emotes, interaction prompts and status panels fade without moving their managed transforms. The existing prompt float and player-centered radial placement remain in charge of positioning.
- Menu entries, inventory rows and emote options reveal with small sibling delays. Profession boards fade when selected; their pan positions and masks stay fixed.
- Buttons keep their original hit rectangles, rotation and scale. Pressing compresses only the icon; releasing adds a small buoyant lift. Hovered or keyboard-focused icons drift slowly inside the button while the existing glass light follows the pointer or centers on keyboard selection. Text stays still.
- Inventory changes ease the displayed number to the latest amount, float a signed green/blue delta and lift the resource icon by up to 3 units. Initial binding and hidden updates snap directly to the current amount. Repeated updates retarget the visible counter without changing wallet data.
- A previously available skill becoming purchased receives a glass pulse and a soft two-tone confirmation. Initial ownership binding, disabled clicks and repeated snapshots do not celebrate a purchase.
- Hover, press, open, close, inventory changes and purchase confirmation have quiet synthesized glass cues from one shared UI audio source. Hover and amount cues are throttled. `AeroUIAudio` exposes volume and sound enable settings in the Inspector.
- Upgrade tooltips fade over 0.12 seconds and remain noninteractive. Existing pointer clamping and descriptive content are preserved.

`AeroUIJuiceAuthoring.Apply` updates source prefabs through Unity and preserves their layout and serialized callbacks. The full interface builder also calls this pass so rebuilding the interface retains its feedback configuration.

## Review

[Motion preview](Previews/UI-Feedback.gif), [upgrades](Previews/UI-Feedback-Upgrades.png), [inventory](Previews/UI-Feedback-Inventory.png), [emotes](Previews/UI-Feedback-Emotes.png).

The preview samples the presentation in a disposable Edit Mode scene with illustrative values. It does not invoke gameplay actions, award resources or purchases, or enter Play Mode. Compilation, source review and prefab inspection are the validation used for this pass. No automated tests or Play Mode sessions were run. Runtime input, network event timing and audio playback still need a gameplay review.
