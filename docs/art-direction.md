# Campaign art direction

- Visual target: the supplied mobile gameplay screenshot, compared at 1920 × 1080.
- Map: angular territory silhouettes, black extruded sides, matte shaded faces, cyan coastal glow, thick dotted sea routes, and white shoreline endpoints.
- Typography: white digits with dark outlines; Inter for counters and names, Roboto Condensed for the phase title and campaign interface.
- Layout: a full-screen ocean, circular top-left controls, a right-side commander rail, a bottom card stack, and a central turn medallion.
- Territory names are available through accessible marker labels, keyboard focus, and the territory list.
- Map shape changes are display-only; `classic-topology.json` remains the authoritative rules graph.
- Portrait and infantry assets were created with the built-in image-generation tool and saved to `client/public/assets/commanders.png` and `client/public/assets/infantry.png`.
- The reference screenshot is used for private visual verification and is not included in the published assets.

## Commander sheet prompt

```text
Use case: stylized-concept. Asset type: one production sprite sheet of original commander portraits for a polished territory-conquest browser game. Exactly six portraits on a strict 3-column by 2-row grid of equal square cells; landscape canvas 1536x1024. In each cell a head-and-shoulders portrait centered inside a navy circular backdrop, with transparent pixels outside the circle. Identical scale and crop, portrait fills 85% of its cell; generous safe inset from cell edges; no frames, no badges, no numbers, no lettering. Top row left: a confident French-style Napoleonic officer with a navy bicorne, black moustache, red military coat and gold braid. Top row center: an adventurous woman sea captain with a burgundy tricorn, auburn hair, teal coat and gold earrings. Top row right: a poised East Asian woman commander with black hair, blue-white military coat and silver armor. Bottom row left: a weathered white-bearded admiral in an orange coat and dark captain's hat. Bottom row center: a regal blonde commander in purple velvet with a small gold crown and ornate collar. Bottom row right: a charismatic Black woman general in green military dress and a gold headscarf. All are original character designs. Style: premium hand-painted 3D cartoon game illustration, expressive faces, crisp dark contours, rich chiaroscuro, polished metallic decorations and embroidered cloth, vibrant saturated colors, strong readable silhouettes at avatar size. Single coherent illustration style. No map, UI, logos, text, watermark, or other objects.
```

## Infantry prompt

```text
Use case: stylized-concept. Asset type: an original miniature infantry figure for the army-reserve medallion of a strategy game UI. A single full-body Napoleonic infantryman holding a long upright musket at his right side, standing at attention, facing almost directly forward. Tall bicorne, military coat, crossed straps, knee boots, recognizable crisp silhouette. Entire figure sculpted from luminous translucent ice-blue glass and pale cyan enamel with sharp silver-blue highlights, subtle inner glow, collectible 3D tabletop figurine quality. The figure is centered vertically on a square 1024x1024 transparent canvas, with head near 8% and boots near 91%; nothing cropped. No pedestal, no circular frame, no text, numbers, UI, other figures, backdrop, or watermark. Clean transparent pixels around the figure. Premium polished cartoon 3D render, clearly legible at a small avatar size.
```
