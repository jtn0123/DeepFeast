# Shark anatomy pass

The v8 shark combined a long pointed face, a very large straight dorsal triangle, steeply drooping pectorals and a generic asymmetric tail. This pass separates the three sharks through body shape and solid fin geometry, so the color pattern does not have to carry their identity.

| Internal key | Species | Geometry cues |
| --- | --- | --- |
| `shark` | White shark, *Carcharodon carcharias* | Stocky torpedo, short conical front, thicker shoulders, moderate curved first dorsal, swept pectorals, near-balanced crescent tail and caudal keels. |
| `tiger_shark` | Tiger shark, *Galeocerdo cuvier* | Broad, blunt front, deeper belly, farther-back first dorsal, broad pectorals, long unequal upper tail lobe with a small subterminal notch. |
| `mako_shark` | Shortfin mako, *Isurus oxyrinchus* | Slimmer body, distinctly pointed snout, narrow swept pectorals, compact dorsal, near-equal lunate tail and prominent small peduncle keels. |

The first dorsal uses curved leading and concave trailing edges rather than a flat triangle outline. Main fins have cartilage thickness and broad camber. Horizontal pectoral and pelvic fins use thickness along their surface normal, with a convex upper surface and a thin rim. Their roots fit the body cross-section and remain at zero fin-flex weight; they receive the same body displacement as their attachment points. The caudal fins have a broad solid contour and no decorative ray corrugation.

The first native side, oblique and head-on captures exposed a remaining straight V in the caudal outline and blade-like pectorals in profile. A second geometry pass builds each tail lobe from separate curved leading and trailing boundaries with a small rounded tip and a closed bridge at the stalk. Tail flex, phase, color and thickness derive from position across all three patches, keeping their shared seams together while swimming. White and mako tail height is slightly reduced, and pectoral dihedral is increased modestly to show a surface in side view. This second pass still needs the integrating agent's final native render and validation.

The second native inspection found a dark triangular band at the tail bridge. This was an internal mesh normal seam: each patch closed its shared border with overlapping side walls. The final correction removes those internal walls, keeps the exterior rim closed, and matches border sample counts so shared surface normals weld smoothly. The integrating agent rebuilds and recaptures from this corrected source.

This changes procedural meshes inside the existing seven cached renderer parts. Eye placement, teeth, gill marks, pigmentation and species rotation are integrated by the companion changes in `FishVolume`, its shader and the species catalog.

References consulted:

- [NOAA white shark](https://www.fisheries.noaa.gov/species/white-shark): torpedo body, large pointed first dorsal and pectoral markings.
- [NOAA shortfin mako](https://www.fisheries.noaa.gov/species/atlantic-shortfin-mako-shark): very pointed snout, metallic blue sides, pale underside and long gill slits; the shortfin form keeps shorter pectorals than the longfin species.
- [NOAA tiger shark field photographs](https://www.fisheries.noaa.gov/feature-story/fishing-sharks-gulf-mexico): wide head and mottled stripes.
- [Florida Museum tiger shark profile](https://www.floridamuseum.ufl.edu/discover-fish/species-profiles/tiger-shark/): blunt wide snout, stout front, rearward dorsal origin, long upper caudal lobe and subterminal notch.

These are stylized game models at small screen sizes, not scanned specimens or measured anatomical reconstructions. Render-side lighting, head-on projection and mouth articulation need to be reviewed together with the geometry in the native Unity captures. Native runtime and build validation are performed by the integrating agent; this note does not claim that they have already passed.
