#!/usr/bin/env python3
"""Cut the drafted concept art (art-drafts/concepts/) into game sprites.

For every drafted species this writes, into unity/Assets/Resources/Concept/:

  fish_<key>_body.png   the fish without its tail fin
  fish_<key>_open.png   the same with the mouth open (bite frame)
  fish_<key>_tail.png   the tail fin, hinged at the peduncle so the game can wag it

plus fish.json with the pivots, pixels-per-unit and painted-eye placement FishArt needs.
From the props sheet it cuts the jellyfish bell (the tentacles stay procedural so they can
sway), hue-rotated into every jelly colour the game spawns: jelly_bell_<hue>.png + jelly.json,
and the coral group as one hero reef piece: reef_cluster.png + reef.json.
The sheet has a flat navy background, so the fish are keyed out by flood-filling it
from the edges; everything enclosed by a fish (dark stripes, outlines) stays opaque.

Usage: python3 tools/cut_concepts.py        (needs Pillow)
"""
import colorsys
import json
import math
import os
from collections import deque

from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SHEET = os.path.join(ROOT, "art-drafts", "concepts", "fish-polished-v1.png")
ITEMS = os.path.join(ROOT, "art-drafts", "concepts", "items-polished-v1.png")
JELLY_HUES = (300, 320, 190, 270, 200)  # Game.SpawnJelly picks from these
OUT = os.path.join(ROOT, "unity", "Assets", "Resources", "Concept")

BG = (8, 44, 70)  # flat navy behind every concept fish
CELL = 512        # the sheet is a 3 x 2 grid of 512 px cells
HL = 1.3          # FishArt.HL: half length of an r = 1 fish
STICKER = (240, 255, 255)

# key, cell column, cell row, painted eye gets expression overlays, has an open-mouth frame
FISH = [
    ("player", 0, 0, True, True),
    ("clown", 1, 0, True, True),
    ("tang", 2, 0, True, True),
    ("puffer", 0, 1, True, True),
    ("angel", 1, 1, True, True),
    ("shark", 2, 1, False, False),
]


def bg_dist(c):
    return ((c[0] - BG[0]) ** 2 + (c[1] - BG[1]) ** 2 + (c[2] - BG[2]) ** 2) ** 0.5


def key_out(im):
    """RGBA copy of im with the background flood-filled away from the border."""
    w, h = im.size
    px = im.load()
    outside = bytearray(w * h)
    q = deque((x, y) for x in range(w) for y in (0, h - 1))
    q.extend((x, y) for y in range(h) for x in (0, w - 1))
    while q:
        x, y = q.popleft()
        i = y * w + x
        if outside[i] or bg_dist(px[x, y]) > 13:
            continue
        outside[i] = 1
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < w and 0 <= ny < h and not outside[ny * w + nx]:
                q.append((nx, ny))
    out = Image.new("RGBA", (w, h))
    op = out.load()
    for y in range(h):
        for x in range(w):
            c = px[x, y]
            if not outside[y * w + x]:
                op[x, y] = c + (255,)
                continue
            a = max(0.0, min(1.0, (bg_dist(c) - 3) / 12))
            if a <= 0:
                op[x, y] = (0, 0, 0, 0)
                continue
            # un-mix the navy so edges don't carry a background fringe
            r, g, b = (max(0, min(255, int((c[k] - (1 - a) * BG[k]) / a))) for k in range(3))
            op[x, y] = (r, g, b, int(a * 255))
    return out


def drop_specks(im):
    """Clear every opaque island except the fish itself (and pieces at least 2% of its size)."""
    w, h = im.size
    px = im.load()
    label = [0] * (w * h)
    sizes = [0]
    for y in range(h):
        for x in range(w):
            if px[x, y][3] == 0 or label[y * w + x]:
                continue
            n = len(sizes)
            sizes.append(0)
            stack = [(x, y)]
            label[y * w + x] = n
            while stack:
                cx, cy = stack.pop()
                sizes[n] += 1
                for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
                    if 0 <= nx < w and 0 <= ny < h and not label[ny * w + nx] and px[nx, ny][3] > 0:
                        label[ny * w + nx] = n
                        stack.append((nx, ny))
    keep = max(sizes) * 0.02
    for y in range(h):
        for x in range(w):
            if label[y * w + x] and sizes[label[y * w + x]] < keep:
                px[x, y] = (0, 0, 0, 0)
    return im


def alpha_at(im, x, y):
    return im.getpixel((x, y))[3]


def runs(im, x):
    """Contiguous opaque vertical runs in column x as (y0, y1)."""
    h = im.size[1]
    res, start = [], None
    for y in range(h):
        on = alpha_at(im, x, y) > 128
        if on and start is None:
            start = y
        if not on and start is not None:
            res.append((start, y - 1))
            start = None
    if start is not None:
        res.append((start, h - 1))
    return res


def find_eye(im, x0, x1, y0, y1):
    """Largest white blob in the region that has a dark pupil inside its bounds."""
    px = im.load()
    seen = set()
    best = None
    for y in range(y0, y1):
        for x in range(x0, x1):
            if (x, y) in seen:
                continue
            c = px[x, y]
            if not (c[3] > 200 and min(c[:3]) > 205):
                continue
            stack, pts = [(x, y)], []
            seen.add((x, y))
            while stack:
                p = stack.pop()
                pts.append(p)
                for n in ((p[0] + 1, p[1]), (p[0] - 1, p[1]), (p[0], p[1] + 1), (p[0], p[1] - 1)):
                    if x0 <= n[0] < x1 and y0 <= n[1] < y1 and n not in seen:
                        cn = px[n]
                        if cn[3] > 200 and min(cn[:3]) > 205:
                            seen.add(n)
                            stack.append(n)
            if len(pts) < 30:
                continue
            bx0, bx1 = min(p[0] for p in pts), max(p[0] for p in pts)
            by0, by1 = min(p[1] for p in pts), max(p[1] for p in pts)
            dark = sum(1 for yy in range(by0, by1 + 1) for xx in range(bx0, bx1 + 1)
                       if px[xx, yy][3] > 200 and max(px[xx, yy][:3]) < 45)
            square = min(bx1 - bx0, by1 - by0) / max(1, bx1 - bx0, by1 - by0)
            score = dark * square
            if best is None or score > best[0]:
                best = (score, bx0, bx1, by0, by1)
    if best is None:
        return None
    _, bx0, bx1, by0, by1 = best
    return ((bx0 + bx1) / 2, (by0 + by1) / 2, (bx1 - bx0) / 2 + 1.5, (by1 - by0) / 2 + 1.5)


def skin_colour(im, ex, ey, rx, ry):
    """Median body colour in a ring around the eye, for the blink/scowl lids."""
    px = im.load()
    picks = []
    for k in range(72):
        for s in (1.35, 1.55, 1.75):
            a = k / 72 * math.tau
            x, y = int(ex + math.cos(a) * rx * s), int(ey + math.sin(a) * ry * s)
            if not (0 <= x < im.size[0] and 0 <= y < im.size[1]):
                continue
            c = px[x, y]
            if c[3] > 250 and min(c[:3]) < 200 and max(c[:3]) > 70:
                picks.append(c[:3])
    picks.sort(key=lambda c: sum(c))
    return picks[len(picks) // 2] if picks else (200, 200, 200)


def sticker(im, width):
    """Light outline behind the whole silhouette (the player's sticker edge)."""
    a = im.getchannel("A")
    grown = a.filter(ImageFilter.MaxFilter(width * 2 + 1)).filter(ImageFilter.GaussianBlur(1))
    base = Image.new("RGBA", im.size, STICKER + (0,))
    base.putalpha(grown)
    base.alpha_composite(im)
    return base


def tail_mask(im, cut, run):
    """Opaque pixels left of the cut that connect to the peduncle run."""
    w, h = im.size
    m = bytearray(w * h)
    q = deque((cut - 1, y) for y in range(run[0], run[1] + 1))
    while q:
        x, y = q.popleft()
        if not (0 <= x < cut and 0 <= y < h) or m[y * w + x] or alpha_at(im, x, y) == 0:
            continue
        m[y * w + x] = 1
        q.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))
    return m


def open_mouth(im, snout_x, mouth_y, length, height):
    """Paint an open mouth into the snout, clipped to the silhouette."""
    rx, ry = length * 0.07, height * 0.14
    cx = snout_x - rx * 0.6
    mouth = Image.new("L", im.size)
    d = ImageDraw.Draw(mouth)
    d.ellipse([cx - rx, mouth_y - ry, cx + rx, mouth_y + ry], fill=255)
    rim = Image.new("L", im.size)
    ImageDraw.Draw(rim).ellipse([cx - rx - 3, mouth_y - ry - 3, cx + rx + 3, mouth_y + ry + 3], fill=255)
    tongue = Image.new("L", im.size)
    ImageDraw.Draw(tongue).ellipse([cx - rx * 0.75, mouth_y + ry * 0.15, cx + rx * 0.6, mouth_y + ry * 1.1], fill=255)
    out = im.copy()
    a = im.getchannel("A")
    for mask, col in ((rim, (12, 26, 44)), (mouth, (59, 13, 22)), (tongue, (214, 84, 112))):
        mask = Image.composite(mask, Image.new("L", im.size), a.point(lambda v: 255 if v > 200 else 0))
        mask = mask.filter(ImageFilter.GaussianBlur(0.6))
        layer = Image.new("RGBA", im.size, col + (0,))
        layer.putalpha(mask)
        out.alpha_composite(layer)
    out.putalpha(a)
    return out


def crop4(im):
    """Crop to the opaque bounds plus a margin, padded to multiples of 4 for block compression."""
    x0, y0, x1, y1 = im.getchannel("A").getbbox()
    x0, y0 = max(0, x0 - 3), max(0, y0 - 3)
    w, h = x1 + 3 - x0, y1 + 3 - y0
    w, h = (w + 3) // 4 * 4, (h + 3) // 4 * 4
    out = Image.new("RGBA", (w, h))
    out.paste(im.crop((x0, y0, min(im.size[0], x0 + w), min(im.size[1], y0 + h))), (0, 0))
    return out, x0, y0


def pivot(px_x, px_y, x0, y0, size):
    """Pixel position in the cell -> Unity normalised pivot of the cropped sprite (y up)."""
    return round((px_x - x0) / size[0], 5), round(1 - (px_y - y0) / size[1], 5)


def hue_of(im):
    """Median hue (degrees) of the saturated opaque pixels."""
    hues = sorted(colorsys.rgb_to_hsv(*(v / 255 for v in c[:3]))[0] for c in im.getdata()
                  if c[3] > 200 and max(c[:3]) - min(c[:3]) > 40)
    return hues[len(hues) // 2] * 360


def rotate_hue(im, degrees):
    out = im.copy()
    px = out.load()
    for y in range(out.size[1]):
        for x in range(out.size[0]):
            c = px[x, y]
            if c[3] == 0:
                continue
            h, s, v = colorsys.rgb_to_hsv(*(k / 255 for k in c[:3]))
            r, g, b = colorsys.hsv_to_rgb((h + degrees / 360) % 1, s, v)
            px[x, y] = (int(r * 255 + 0.5), int(g * 255 + 0.5), int(b * 255 + 0.5), c[3])
    return out


def cut_bell():
    """The jelly bell, cut off just under its scalloped rim where the tentacles separate."""
    region = Image.open(ITEMS).convert("RGB").crop((200, 20, 620, 260))
    im = drop_specks(key_out(region))
    w, h = im.size
    widths = [sum(1 for x in range(w) if alpha_at(im, x, y) > 128) for y in range(h)]
    rim = 0  # widest row of the bell: the first peak before the width falls away under the scallops
    for y in range(h):
        if widths[y] > widths[rim]:
            rim = y
        elif widths[y] < widths[rim] * 0.9:
            break
    xs = [x for x in range(w) if alpha_at(im, x, rim) > 128]
    cx, half = (min(xs) + max(xs)) / 2, (max(xs) - min(xs)) / 2
    # under the rim the tentacles enclose dark haze: keep only the bright scallops, then stop
    cut = rim + int(half * 0.17)
    px = im.load()
    for y in range(rim, h):
        for x in range(w):
            c = px[x, y]
            a = min(c[3], int(255 * max(0.0, min(1.0, (bg_dist(c) - 60) / 90))))
            if y >= cut - 4:
                a = 0 if y >= cut else int(a * (cut - y) / 5)
            px[x, y] = c[:3] + (a,)
    im = drop_specks(im)
    bell, x0, y0 = crop4(im)
    px_, py_ = pivot(cx, rim, x0, y0, bell.size)
    base = hue_of(bell)
    for hue in JELLY_HUES:
        rotate_hue(bell, hue - base).save(os.path.join(OUT, f"jelly_bell_{hue}.png"))
    with open(os.path.join(OUT, "jelly.json"), "w") as f:
        json.dump({"ppu": round(half, 4), "pivotX": px_, "pivotY": py_}, f, indent=1)
    print(f"bell    rim={rim} cut={cut} half={half:.0f} base_hue={base:.0f}")


def cut_cluster():
    """Branching coral, tube sponges and brain coral as one piece; pivot on its base, 2.4 units wide."""
    im = key_out(Image.open(ITEMS).convert("RGB").crop((880, 500, 1440, 1000)))
    px = im.load()
    for y in range(im.size[1]):  # pockets of background enclosed by branches
        for x in range(im.size[0]):
            if px[x, y][3] and bg_dist(px[x, y]) < 9:
                px[x, y] = (0, 0, 0, 0)
    im, x0, y0 = crop4(drop_specks(im))
    w, h = im.size
    bottom = max(y for y in range(h) if any(alpha_at(im, x, y) > 128 for x in range(w)))
    px_, py_ = pivot(w / 2, bottom - h * 0.03, 0, 0, im.size)
    im.save(os.path.join(OUT, "reef_cluster.png"))
    with open(os.path.join(OUT, "reef.json"), "w") as f:
        json.dump({"ppu": round(w / 2.4, 4), "pivotX": px_, "pivotY": py_}, f, indent=1)
    print(f"reef    size={im.size}")


def main():
    sheet = Image.open(SHEET).convert("RGB")
    os.makedirs(OUT, exist_ok=True)
    cut_bell()
    cut_cluster()
    meta = []
    for key, col, row, overlay, has_open in FISH:
        cell = sheet.crop((col * CELL, row * CELL, (col + 1) * CELL, (row + 1) * CELL))
        fish = drop_specks(key_out(cell))
        w, h = fish.size
        cols = [sum(1 for y in range(h) if alpha_at(fish, x, y) > 128) for x in range(w)]
        xs = [x for x in range(w) if cols[x]]
        xmin, xmax = min(xs), max(xs)
        length = xmax - xmin
        cut = min(range(xmin + int(length * 0.12), xmin + int(length * 0.45)), key=lambda x: cols[x])
        snout_ys = [y for x in range(xmax - 3, xmax + 1) for y in range(h) if alpha_at(fish, x, y) > 128]
        snout_y = sum(snout_ys) / len(snout_ys)
        run = min(runs(fish, cut), key=lambda r: abs((r[0] + r[1]) / 2 - snout_y))
        ped_y = (run[0] + run[1]) / 2

        ys = [y for y in range(h) if any(alpha_at(fish, x, y) > 128 for x in range(xmin, xmax + 1, 2))]
        eye = find_eye(fish, xmin + int(length * 0.6), xmax + 1, min(ys), max(ys) + 1) if overlay else None
        eye_x = eye[0] if eye else xmax - length * 0.15
        body_h = max((r[1] - r[0] for r in runs(fish, int(eye_x))), default=length * 0.4)
        lid = skin_colour(fish, *eye) if eye else (200, 200, 200)

        if key == "player":
            fish = sticker(fish, 6)
        opened = open_mouth(fish, xmax, snout_y + body_h * 0.04, length, body_h) if has_open else fish

        # split the tail off at the peduncle; the tail keeps a strip under the body so the joint never gaps
        m = tail_mask(fish, cut, (max(0, run[0] - 8), min(h - 1, run[1] + 8)))
        overlap = int(length * 0.05)
        tail = Image.new("RGBA", fish.size)
        tp, fp = tail.load(), fish.load()
        for y in range(h):
            for x in range(w):
                if (x < cut and m[y * w + x]) or (cut <= x < cut + overlap and run[0] - 2 <= y <= run[1] + 2):
                    tp[x, y] = fp[x, y]

        def body_of(src):
            b = src.copy()
            bp = b.load()
            for y in range(h):
                for x in range(w):
                    c = bp[x, y]
                    if c[3] == 0:
                        continue
                    if x < cut and m[y * w + x]:
                        bp[x, y] = (0, 0, 0, 0)
                    elif cut <= x < cut + 3 and run[0] - 8 <= y <= run[1] + 8:
                        bp[x, y] = c[:3] + (int(c[3] * (x - cut + 1) / 4),)  # feather the joint
            return b

        ppu = (xmax - cut) / (HL * 1.86)
        ox, oy = xmax - HL * ppu, ped_y          # shape origin of the rig, in cell pixels
        tx = ox - HL * 0.84 * ppu                  # where FishView hinges the tail

        body, bx0, by0 = crop4(body_of(fish))
        body.save(os.path.join(OUT, f"fish_{key}_body.png"))
        if has_open:
            crop4(body_of(opened))[0].save(os.path.join(OUT, f"fish_{key}_open.png"))
        tail_c, tx0, ty0 = crop4(tail)
        tail_c.save(os.path.join(OUT, f"fish_{key}_tail.png"))

        bpx, bpy = pivot(ox, oy, bx0, by0, body.size)
        tpx, tpy = pivot(tx, oy, tx0, ty0, tail_c.size)
        entry = {
            "key": key, "ppu": round(ppu, 4), "bodyPX": bpx, "bodyPY": bpy, "tailPX": tpx, "tailPY": tpy,
            "open": has_open, "overlay": eye is not None,
            "eyeX": 0.0, "eyeY": 0.0, "eyeRX": 0.0, "eyeRY": 0.0,
            "lidR": round(lid[0] / 255, 4), "lidG": round(lid[1] / 255, 4), "lidB": round(lid[2] / 255, 4),
            "hh": round(body_h / 2 / ppu, 4),
        }
        if eye:
            entry.update(eyeX=round((eye[0] - ox) / ppu, 4), eyeY=round((oy - eye[1]) / ppu, 4),
                         eyeRX=round(eye[2] / ppu, 4), eyeRY=round(eye[3] / ppu, 4))
        meta.append(entry)
        print(f"{key:7s} cut={cut} ped_y={ped_y:.0f} ppu={ppu:.1f} eye={eye} lid={lid}")
    with open(os.path.join(OUT, "fish.json"), "w") as f:
        json.dump({"fish": meta}, f, indent=1)


if __name__ == "__main__":
    main()
