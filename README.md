## Image Overlay (fork)
This is a fork of [algernon-A/ImageOverlay](https://github.com/algernon-A/ImageOverlay) for Cities: Skylines 2.

### Changes in this fork

#### Terrain-conforming overlay (rendering overhaul)
The original mod projected the overlay onto a flat plane at a fixed elevation. On uneven terrain this caused the image to float above hills or clip into valleys.

This fork replaces the flat plane with a **terrain-conforming mesh**: a 64×64 grid where each vertex is placed at the actual terrain height sampled from the game's `TerrainSystem`. The overlay now drapes onto the landscape like a sticker — following every hill, slope and valley.

Additional rendering improvements:
- Vertices are generated in local space (relative to the overlay centre) to eliminate floating-point precision drift that made the image slide when rotating the camera over large world coordinates.
- Image rotation is baked into UV coordinates so the mesh stays world-axis-aligned while the projected image rotates.

#### Overlay lock
A new **Overlay lock** section in the settings panel:

| Control | Description |
|---|---|
| **Lock overlay position** (toggle) | Disables all keyboard shortcuts for moving, rotating and resizing the overlay, preventing accidental nudges. **Ctrl+O** (show/hide) continues to work while locked. |
| **Snap to terrain and lock** (button) | Resets the elevation offset to zero so the overlay sits flush at terrain level, then locks it in one click. |

#### Turkish localisation (tr-TR)
Full Turkish translation added for all UI strings including the new lock feature.

---

## Original description
A simple mod to **Overlay an image on the game map**.

Includes the ability to quickly and easily select and change between different images for the overlay.  The size, position, rotation, and transparency of the overlay can also be adjusted.

Works both in-game and in the editor.

## Instructions
### Selecting the overlay files
- Place any desired overlays in ".png" format in the "Overlays" directory in your local settings (%LocalAppData%Low\Colossal Order\Cities Skylines II).
- The images can be any size up to a maximum of 16 384 pixels in either dimension. The images don't *have* to be square, but it's best if they are; if not square, they will be stretched out to a square in-game.
- In the game's 'Settings' menu there will be a mod setting entry there for 'Image overlay' where you can choose the overlay image file that you want.
- You can change the overlay file at any time.  Press the 'refresh files' button to rescan the Overlays directory for changes (so you can add or remove overlay files even when in-game).

### Displaying the overlay
- Press **Control-O** to activate the overlay. Press **Control-O** again to hide it.  There may be a slight pause in the game on the first activation as the image file is loaded.
- The overlay will be automatically scaled to the vanilla playable area size (14 336m per side) and will be centred around middle of the map.
- The overlay now drapes onto the terrain surface. Use **Snap to terrain and lock** in the settings panel to pin it to exact terrain level in one click.

### Repositioning the overlay
- Press **Control-PageUp** and **Control-PageDown** to raise or lower the elevation offset of the overlay.
- To rotate the overlay, press **Control-.** (period) or **Control-,** (comma) to rotate 1 degree at a time, or **Control-Shift-.** / **Control-Shift-,** to rotate 90 degrees at a time.
- To move the overlay horizontally, use the **arrow keys** with either **Control** (move 1m at a time) or **Control-Shift** (move 10m at a time).
- You can also use the sliders in the settings panel to change the position, elevation, and rotation.
- Enable **Lock overlay position** in the settings panel to prevent accidental movement.

### Resizing the overlay
- Press **Control-Minus** and **Control-Equals** to shrink or expand the size of the overlay.
- For large adjustments use the slider in the settings panel.

### Changing the overlay's transparency
- Use the slider in the settings panel to change the overlay's transparency. 0% (default) is fully opaque and 100% is fully transparent (invisible).

## Meta
### Translations
This fork adds Turkish (tr-TR) localisation. Other translations from the original project are preserved.

### Original mod
The original mod is by [algernon-A](https://github.com/algernon-A) and is available on [Paradox Mods](https://mods.paradoxplaza.com/mods/74539/Windows).
Support for the original mod: [Cities Skylines Modding Discord](https://discord.gg/HTav7ARPs2).