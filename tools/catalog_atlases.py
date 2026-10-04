#!/usr/bin/env python3
"""Describe transparent atlas regions for Unity without altering or re-encoding the source images.

Requires Pillow. Sprite rectangles, anchors and measured body outlines are written to
painted-atlas.json; the original generated RGBA PNG bytes remain untouched.
"""
import json
import math
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / "unity/Assets/Resources/Concept"

# Species order in each authored sheet, tail length, spine height, and painted eye anchor within its silhouette.
SHEETS = {
    "original": [
        ("player", .45, .60, .865, .47, .12),
        ("clown", .45, .60, .87, .43, .12),
        ("tang", .36, .60, .86, .47, .11),
        ("puffer", .30, .50, .82, .35, .12),
        ("angel", .38, .52, .84, .47, .065),
        ("shark", .60, .46, .855, .43, .065),
    ],
    "remaining": [
        ("minnow", .48, .57, .835, .48, .14),
        ("parrot", .45, .575, .825, .45, .12),
        ("snapper", .45, .55, .825, .45, .12),
        ("barracuda", .40, .48, .795, .50, .12),
        ("grouper", .38, .60, .815, .44, .10),
        ("tuna", .45, .54, .83, .47, .12),
    ],
}


# Painted body outlines that FishVolume fits its sculpted bodies to, behind the head, so the projected
# skin and the painted fins meet the sculpted edge. Each column is scanned outward for the outer edge
# of the painted contour between 70% and 160% of a rough prior half height, which skips the scales,
# spots and bands inside the body. The prior is the body LegacyBody in LegacyFishAnatomy.cs builds:
# (shape half height, height, shoulder, nose, rear, front, peduncle, lift). It only steers the search.
BODY_PRIORS = {
    "parrot": (.50, .82, .10, 1.18, 1.10, .45, .22, .01),
    "barracuda": (.20, .80, .00, 1.28, .75, .85, .30, 0),
    "grouper": (.56, .79, .15, 1.09, .70, .45, .30, .06),
    "tuna": (.36, .97, .00, 1.28, 1.65, .68, .10, .01),
}
# Outline samples (sprite units from the spine): x = OUTLINE_X0 + i * OUTLINE_DX, tail root to past the shoulder.
OUTLINE_X0, OUTLINE_DX, OUTLINE_COUNT = -1.04, 0.04, 46
INK_LUMINANCE = 0.22
# The painted contour is never wider than this; a longer dark run is a fin's ink touching the body.
CONTOUR_WIDTH = 0.03


def body_outline(key, image, ppu, anchor_x, anchor_y):
    """Top and bottom painted body edges in sprite units, with stray hits on inner ink or fins removed."""
    hh, height, shoulder, nose, rear, front, peduncle, lift = BODY_PRIORS[key]
    half = 1.3 * hh * height
    pixels = image.load()

    def prior(x):
        if x >= shoulder:
            return half * max(0.000016, 1 - ((x - shoulder) / nose) ** 2) ** front, lift
        t = min(1, max(0, (x + 1.04) / (shoulder + 1.04)))
        return half * (peduncle + (1 - peduncle) * math.sin(t * math.pi / 2) ** rear), lift * t

    def ink(x, y):
        p = pixels[int(round(anchor_x + x * ppu)), int(round(anchor_y - y * ppu))]
        luminance = (0.2126 * p[0] + 0.7152 * p[1] + 0.0722 * p[2]) / 255
        return p[3] > 128 and luminance < INK_LUMINANCE, p[3] < 40

    def scan(x, side):
        reach, center = prior(x)
        s, step = 0.70 * reach, 1 / ppu
        while s < 1.6 * reach + 0.05:
            dark, clear = ink(x, center + side * s)
            if clear:
                return center + side * s
            if dark:
                start = s
                while ink(x, center + side * (s + step))[0] and s - start < CONTOUR_WIDTH:
                    s += step
                return center + side * s
            s += step
        return None

    fine = 0.01
    xs = [OUTLINE_X0 + i * fine for i in range(int(round((OUTLINE_COUNT - 1) * OUTLINE_DX / fine)) + 1)]
    result = []
    for side in (1, -1):
        edge = [scan(x, side) for x in xs]
        # A wide running median follows the smooth contour through short runs of stray hits. Where the
        # window is cut off at the tail root, it is taken about the window's slope, or it would lean
        # inward and throw out the body's steady taper into the stalk.
        def median(values):
            values = sorted(values)
            return values[len(values) // 2] if values else None

        trend = []
        for i in range(len(edge)):
            lo, hi = max(0, i - 25), min(len(edge), i + 26)
            window = [(xs[j], edge[j]) for j in range(lo, hi) if edge[j] is not None]
            slope = 0
            if i < 25 and len(window) > 10:
                split = xs[(lo + hi) // 2]
                a = [(x, v) for x, v in window if x < split]
                b = [(x, v) for x, v in window if x >= split]
                if a and b:
                    slope = (median(v for _, v in b) - median(v for _, v in a)) / (median(x for x, _ in b) - median(x for x, _ in a))
            trend.append(median(v - slope * (x - xs[i]) for x, v in window))
        kept = [v if v is not None and t is not None and abs(v - t) <= 0.035 else None for v, t in zip(edge, trend)]
        samples = []
        for i in range(OUTLINE_COUNT):
            center = int(round(i * OUTLINE_DX / fine))
            near = [v for v in kept[max(0, center - 3):center + 4] if v is not None]
            samples.append(round(sum(near) / len(near), 4) if near else None)
        known = [i for i, v in enumerate(samples) if v is not None]
        assert len(known) > OUTLINE_COUNT * 0.8, (key, side, len(known))
        for i, v in enumerate(samples):
            if v is None:
                lower = max((k for k in known if k < i), default=None)
                upper = min((k for k in known if k > i), default=None)
                if lower is None or upper is None:
                    samples[i] = samples[upper if lower is None else lower]
                else:
                    f = (i - lower) / (upper - lower)
                    samples[i] = round(samples[lower] + (samples[upper] - samples[lower]) * f, 4)
        result.append(samples)
    return result


def regions(image):
    """Connected alpha silhouettes; analysis only, no bitmap edits or background removal."""
    w, h = image.size
    mask = bytearray(a > 64 for a in image.getchannel("A").tobytes())
    result = []
    for idx in range(w * h):
        if not mask[idx]:
            continue
        todo = [idx]
        mask[idx] = 0
        count, x0, y0, x1, y1 = 0, w, h, 0, 0
        while todo:
            i = todo.pop()
            x, y = i % w, i // w
            count += 1
            x0, y0, x1, y1 = min(x0, x), min(y0, y), max(x1, x + 1), max(y1, y + 1)
            neighbors = []
            if x: neighbors.append(i - 1)
            if x + 1 < w: neighbors.append(i + 1)
            if y: neighbors.append(i - w)
            if y + 1 < h: neighbors.append(i + w)
            for j in neighbors:
                if mask[j]:
                    mask[j] = 0
                    todo.append(j)
        if count > 1000:
            result.append((x0, y0, x1, y1))
    return sorted(result, key=lambda box: ((box[1] + box[3]) / 2, box[0]))


def frame(box, anchor_x, anchor_y, image):
    x0, y0, x1, y1 = box
    # Two pixels preserve antialiased edge coverage without sampling the neighboring rows.
    x, y = max(0, x0 - 2), max(0, y0 - 2)
    width, height = min(image.width, x1 + 2) - x, min(image.height, y1 + 2) - y
    return dict(x=x, y=y, width=width, height=height,
                pivotX=round((anchor_x - x) / width, 6),
                pivotY=round(1 - (anchor_y - y) / height, 6))


def main():
    catalog = dict(outlineX0=OUTLINE_X0, outlineDX=OUTLINE_DX, fish=[], props=[])
    for sheet, species in SHEETS.items():
        image = Image.open(DEST / f"atlas-{sheet}-v2.png")
        assert image.mode == "RGBA" and image.getchannel("A").getextrema()[0] == 0
        boxes = regions(image)
        assert len(boxes) == 12, (sheet, len(boxes))
        for i, (key, tail, spine, ex, ey, er) in enumerate(species):
            closed, opened = sorted(boxes[i * 2:i * 2 + 2], key=lambda box: box[0])
            cw, ch = closed[2] - closed[0], closed[3] - closed[1]
            ppu = cw / (1.3 * (1.84 + tail))
            anchor_x = closed[2] - 1.3 * ppu
            anchor_y = closed[1] + ch * spine
            # Match the tail origin and spine, so opening a jaw does not move the whole fish.
            open_x = opened[0] + (anchor_x - closed[0])
            open_y = opened[1] + (anchor_y - closed[1])
            entry = dict(key=key, atlas=f"atlas-{sheet}-v2", ppu=round(ppu, 5),
                         closed=frame(closed, anchor_x, anchor_y, image),
                         open=frame(opened, open_x, open_y, image),
                         eyeX=round((closed[0] + cw * ex - anchor_x) / ppu, 5),
                         eyeY=round((anchor_y - closed[1] - ch * ey) / ppu, 5),
                         eyeRX=round(ch * er / ppu, 5), eyeRY=round(ch * er * 1.05 / ppu, 5))
            if key in BODY_PRIORS:
                entry["bodyTop"], entry["bodyBottom"] = body_outline(key, image, ppu, anchor_x, anchor_y)
            catalog["fish"].append(entry)
            print(f"{key:10s} closed={closed} open={opened}")
    image = Image.open(DEST / "atlas-props-v2.png")
    boxes = regions(image)
    assert len(boxes) == 6, len(boxes)
    # The objects have different heights, so identify the two rows by their grounding bases.
    boxes.sort(key=lambda box: (box[3], box[0]))
    prop_widths = dict(branch=1.05, brain=1.2, tube=1.2, fan=1.6, rock=2.4, deep=2.2)
    for row, names in [(boxes[:3], ["branch", "brain", "tube"]), (boxes[3:], ["fan", "rock", "deep"])]:
        for box, key in zip(sorted(row, key=lambda box: box[0]), names):
            ppu = (box[2] - box[0]) / prop_widths[key]
            catalog["props"].append(dict(key=key, atlas="atlas-props-v2", ppu=round(ppu, 5),
                                         frame=frame(box, (box[0] + box[2]) / 2, box[3] - 2, image)))
    # Environment sprites retain the generator's original alpha. Botanical height is one game unit;
    # reef formations share a 2.4-unit width. Group by grounding bases, since authored heights vary.
    for sheet, rows in [
        ("plants", [["kelp-teal", "kelp-olive", "seaweed-red"], ["seagrass", "anemone-rose", "anemone-deep"]]),
        ("reefs", [["reef-shelf", "reef-arch"], ["reef-terrace", "reef-abyss"]]),
    ]:
        image = Image.open(DEST / f"atlas-{sheet}-v3.png")
        boxes = sorted(regions(image), key=lambda box: (box[3], box[0]))
        assert len(boxes) == sum(map(len, rows)), (sheet, boxes)
        offset = 0
        for names in rows:
            row = sorted(boxes[offset:offset + len(names)], key=lambda box: box[0])
            offset += len(names)
            for box, key in zip(row, names):
                ppu = box[3] - box[1] if sheet == "plants" else (box[2] - box[0]) / 2.4
                catalog["props"].append(dict(key=key, atlas=f"atlas-{sheet}-v3", ppu=round(ppu, 5),
                    frame=frame(box, (box[0] + box[2]) / 2, box[3] - 2, image)))
                print(f"{key:16s} frame={box}")
    out = DEST / "painted-atlas.json"
    out.write_text(json.dumps(catalog, indent=2) + "\n")
    print(f"Wrote {out.relative_to(ROOT)}; source PNGs unchanged.")


if __name__ == "__main__":
    main()
