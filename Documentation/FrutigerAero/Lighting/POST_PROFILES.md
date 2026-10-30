# Post-processing looks

Added 2026-10-29. The user asked for two switchable profiles, plus an optional Y2K variant. **Restrained** stays the default until another look is accepted.

| Look | Asset | Intent |
| --- | --- | --- |
| Restrained | `UI/FrutigerAero/AeroAtmosphere.asset` (edited in place; tests and editor tools load it by path) | Close to the accepted look, slightly cooler and clearer |
| Bright Aero | `Environment/Lighting/AeroPost_Bright.asset` | The reference look: bright, saturated sky blues and greens, wide cyan-white glow |
| Y2K | `Environment/Lighting/AeroPost_Y2K.asset` | Bright Aero with film grain and a light colour fringe, after the grainy found-photo references |

Override values:

| Override | Restrained | Bright Aero | Y2K |
| --- | --- | --- | --- |
| Bloom threshold / intensity / scatter | 0.95 / 0.55 / 0.68 | 0.92 / 0.85 / 0.70, high quality | 0.92 / 1.0 / 0.70 |
| Bloom tint | (0.92, 0.97, 1) | (0.85, 0.95, 1) | same as Bright |
| Tonemapping | Neutral | Neutral | Neutral |
| Exposure / contrast / saturation | 0 / 6 / 12 | +0.1 / 12 / 26 | +0.1 / 12 / 30 |
| White balance (temperature / tint) | −6 / +2 | −10 / +3 | −10 / +3 |
| Lift / gain | slight cyan | cyan, no offset | same |
| Split toning | — | shadows `#2E7BD6`, balance −25 | same |
| Vignette | 0.14 navy | 0.12 navy | 0.16 navy |
| Chromatic aberration | — | 0.04 | 0.06 |
| Film grain | — | — | Thin1 0.15 |
| Screen-space lens flare | accepted 0.035 (untouched) | 0.05 | 0.05 |
| Day haze colour | (0.62, 0.76, 0.86) | (0.80, 0.91, 1) | (0.78, 0.90, 1) |

Design notes:

- **Neutral tonemapping is kept.** ACES greys and darkens the cool whites the references depend on.
- **PC grades in HDR.** `PC_RPAsset` colour grading is now HDR, so highlights don't clip before tonemapping. Mobile stays LDR.
- **Marble stays readable.** Marble albedo is capped at 0.94, and every look keeps the bloom threshold at 0.88 or above. A pixel test checks that fewer than 2% of sunlit marble pixels clip under Bright Aero.
- **The first Bright pass was rejected.** It used bloom 1.15, a lifted black level and a highlight split tone, and the result looked hazy and desaturated. The table above is the corrected version.

## Switching

`Scripts/Rendering/AeroPostProfileSwitcher.cs` sits on the Tower scene's **Global Volume**:

- It swaps `Volume.sharedProfile` with a 0.5 s crossfade on a temporary twin volume.
- It sets `AeroDayNightCycle.dayFogColor` for the look's haze.
- It remembers the choice in `PlayerPrefs` (`Aero.PostLook`) through an injectable store.

To change looks, use the F3 menu: **World → Post look → Cycle**. The tower exit's local volume (priority 5) keeps layering on top of every look.

Review captures are under `_Screenshots-and-previews-to-delete/aero-shading/<label>/post-*.png`.
