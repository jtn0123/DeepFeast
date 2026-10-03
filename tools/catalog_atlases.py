#!/usr/bin/env python3
"""Describe transparent atlas regions for Unity without altering or re-encoding the source images.

Requires Pillow. Sprite rectangles and anchors are written to painted-atlas.json;
the original generated RGBA PNG bytes remain untouched.
"""
import json
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
    catalog = dict(fish=[], props=[])
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
