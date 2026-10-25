# Screenshot and preview outputs

All generated screenshots, screen prints, before/after comparisons, and motion previews must be saved in the project-root [_Screenshots-and-previews-to-delete](../_Screenshots-and-previews-to-delete/) folder.

This applies to manual captures, Unity editor review tools, capture scripts, and future visual validation work. Create the destination when needed and organize captures into descriptive subfolders, for example:

```text
_Screenshots-and-previews-to-delete/
  Documentation/FrutigerAero/Buildings/Finish/
  Documentation/FrutigerAero/Lighting/CameraTurn/
```

The folder is ignored by Git and reserved for disposable captures awaiting deletion. Do not create new screenshot or preview output folders under `Assets/`, `Documentation/`, or `Frutiger Reference/`.

Keep durable written findings, settings, and performance reports in `Documentation/`. Documentation links to captures should point to their actual location in the deletion folder; the images may be removed during cleanup.

Supplied visual references and production art assets remain in their established locations.
