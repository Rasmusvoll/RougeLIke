# Art style: storybook woodland, low-poly

The target is the look of the board game *Root* (Kyle Ferrin's art) rebuilt as low-poly 3D:
a warm storybook forest, cute but scrappy critters, flat colour with bold ink lines, and a
battlefield that reads like a game board on a table.

## Rules of thumb

- **Shapes:** chunky, rounded, readable from the battle camera. Big simple masses first (a round
  body, a big head, stubby limbs), details second. Faceted low-poly is fine and wanted; no smooth
  shading, no fine detail that disappears at a distance.
- **Colour:** flat fills from the palette below, two or three colours per model, one accent at most.
  No textures on units. Muted and warm: nothing fully saturated, nothing pure black or pure white.
- **Ink:** every solid object gets a dark brown outline (not black). Tiny trim pieces may skip it.
- **Shading:** one hard step between lit and shadow. Shadows are a warm, slightly purple tint of the
  base colour, never grey. Lit faces keep a faint facet shade so the low-poly form still reads.
- **Light:** one warm key light from the upper left-front, like late-afternoon sun. Cast shadows on.
- **Faces:** critters get dot eyes (Ink) with a small cream highlight. Cute first, scrappy second.

## Palette (sRGB)

| Name           | Hex       | Use |
|----------------|-----------|-----|
| Ink            | `#2B1F1A` | Outlines, eyes, claws' dark tips |
| Parchment      | `#EAD9B0` | Board / clearing ground, UI panels |
| Parchment Dark | `#CDB582` | Board edge, worn paths, UI borders |
| Cream          | `#F2E4C4` | Bellies, muzzles, eye highlights, bone |
| Forest         | `#4F6B3A` | Forest floor, foliage |
| Pine           | `#2F4A33` | Dark foliage, battle background |
| Moss           | `#8A9A4B` | Light foliage, grass tufts, Crawler hide |
| Ochre          | `#D19A3A` | Accents, Crawler belly, shell, mushroom stems |
| Rust           | `#C2562B` | Brute hide accents, enemy team, mushroom caps |
| Brick          | `#9E3B2A` | Pincer, deep red accents |
| Bark           | `#6B4A2F` | Tree trunks, Brute hide |
| Wood           | `#A87A4C` | Light wood, stumps, legs |
| Stone          | `#8C8577` | Rocks, boulders |
| Stone Light    | `#B5AD9A` | Rock highlights |
| Plum           | `#6E4A6E` | Tails, rare accents |
| Acid           | `#A8C23A` | Acid sacs and spit |
| Eyrie Blue     | `#3F6E94` | Player team colour |
| Marquise Orange| `#D9792B` | Enemy team colour |

## Shader settings

All 3D art uses `RougeLike/Toon` (`Assets/Art/Shaders/Toon.shader`).

| Setting          | Default            | Notes |
|------------------|--------------------|-------|
| Outline width    | 3 px               | Constant on screen. Use 0 on flat overlays (team zones). |
| Outline colour   | Ink `#2B1F1A`      | |
| Shadow tint      | `#9E858F` (multiply) | Warm purple shadow. |
| Shadow threshold | 0.05, softness 0.03 | One crisp step. |
| Facet shading    | 0.12               | Faint shading on the lit side. |
| Paper grain      | 0 (0.08 on ground) | Painted blotches for large flat surfaces. |

## Pipeline

- Models are built by script in Blender: `build_unit_placeholders.py` (units) and
  `build_arena_props.py` (trees, rocks, mushrooms, the clearing). Run them with
  `python ArtSource/blender_send.py <script>` while the Blender MCP add-on is listening.
- Colours live in each script's `PALETTE`, which mirrors the table above. Materials keep their
  colour through FBX; `ToonModelPostprocessor` swaps in the toon shader on import.
- The scripts write smoothed normals into the vertex colour so the outline hull has no cracks on
  faceted meshes.
- Unit parts keep their origin at the attach point; bodies keep their origin on the ground. Slot
  positions live in the body definition assets, so reshape bodies around them.

## UI

The screens are parchment boards with ink frames laid on a dark Pine table. Buttons are inked
wooden plaques (Wood by default, Eyrie Blue for the main action, Brick for destructive actions,
Ochre for "go" and for the selected choice). Content shows as cards with an icon in a sunken socket;
the card's trim colour is the rarity: Common Parchment Dark, Uncommon Moss, Rare Eyrie Blue,
Epic Plum, Legendary Ochre. Energy is shown as Ochre pips.

- **Styles:** `Assets/UI/Builder/UnitBuilder.uss` holds the shared look (both screens load it);
  `Assets/UI/Battle/Battle.uss` adds the battle layout. Reusable pieces (sockets, stat chips, pips,
  section headings) are built by `Assets/Scripts/UI/StoryUI.cs`.
- **UI art:** `RougeLike > UI > Draw UI Art` redraws the plaques, panels, cards, pips, frame and
  stat icons into `Assets/UI/Textures` from code (`Assets/Scripts/Editor/UiArt/UiArtGenerator.cs`).
  Change the palette there, not in an image editor.
- **Content icons:** `RougeLike > UI > Render Content Icons` photographs every body and part model
  with the toon shader (thicker 12 px ink at render size, cropped to fit) into `Assets/Art/Icons`
  and draws a medallion for every buff from its main stat. It assigns each definition's `icon`.
  Re-run it after adding content or changing a model.

### Fonts

| Use | Font | Source | Licence |
|-----|------|--------|---------|
| Titles, headings, buttons | Alegreya SC (ExtraBold, Bold) | [google/fonts](https://github.com/google/fonts/tree/main/ofl/alegreyasc), by Huerta Tipográfica | SIL Open Font License 1.1 |
| Body text, numbers, hints | Alegreya Sans (Medium, Bold, Medium Italic) | [google/fonts](https://github.com/google/fonts/tree/main/ofl/alegreyasans), by Huerta Tipográfica | SIL Open Font License 1.1 |

The licence texts sit next to the fonts in `Assets/UI/Fonts` (`OFL-AlegreyaSC.txt`,
`OFL-AlegreyaSans.txt`). UI Toolkit uses the dynamic SDF font assets (`*_SDF.asset`) made from the
`.ttf` files.
