"""Assemble native Unity captures; no illustrated or generated frames are used."""
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parent

def run(*args):
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", *map(str, args)], check=True)

for style in ("current", "painted", "sculpted"):
    if len(list((ROOT / style).glob("frame_*.png"))) == 120:
        run("-framerate", 30, "-i", ROOT / style / "frame_%03d.png", "-c:v", "libx264", "-crf", 15, "-pix_fmt", "yuv420p", "-movflags", "+faststart", ROOT / style / "native.mp4")
    if not (ROOT / style / "native.mp4").exists():
        raise RuntimeError(f"Missing native capture for {style}")

def compose(styles, width, labels, filename):
    height = width * 3 // 4
    args = []
    for style in styles:
        args += ["-i", ROOT / style / "native.mp4"]
    args += ["-loop", 1, "-framerate", 30, "-i", ROOT / labels]
    filters = ";".join(f"[{i}:v]scale={width}:{height}:flags=lanczos[v{i}]" for i in range(len(styles)))
    streams = "".join(f"[v{i}]" for i in range(len(styles)))
    filters += f";{streams}hstack=inputs={len(styles)},pad={width*len(styles)}:{height+112}:0:72:color=0x071a27[base];[base][{len(styles)}:v]overlay=shortest=1[v]"
    run(*args, "-filter_complex", filters, "-map", "[v]", "-c:v", "libx264", "-crf", 17, "-pix_fmt", "yuv420p", "-movflags", "+faststart", ROOT / f"{filename}.mp4")
    run("-i", ROOT / f"{filename}.mp4", "-filter_complex", "fps=20,split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=sierra2_4a", "-loop", 0, ROOT / f"{filename}.gif")
    run("-i", ROOT / f"{filename}.mp4", "-ss", 1, "-frames:v", 1, ROOT / f"{filename}-front.png")

compose(["painted", "sculpted"], 640, "labels-options.png", "both-3d-options")
compose(["current", "painted", "sculpted"], 480, "labels-current.png", "current-and-both-options")
print("Composed both native 3D drafts, current comparison, GIFs and frontal stills.")
