"""Compose unmodified native Unity captures with browser-drawn captions."""
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parent


def run(*args):
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", *map(str, args)], check=True)


for timing in ("before", "after", "before-feed", "after-feed"):
    for style in ("painted", "sculpted"):
        directory = ROOT / f"{timing}-{style}"
        if len(list(directory.glob("frame_*.png"))) == 120:
            run("-framerate", 30, "-i", directory / "frame_%03d.png", "-c:v", "libx264",
                "-crf", 15, "-pix_fmt", "yuv420p", "-movflags", "+faststart", directory / "native.mp4")
        if not (directory / "native.mp4").exists():
            raise RuntimeError(f"Missing native sequence: {directory.name}")


def compose(left, right, captions, name):
    run("-i", ROOT / left / "native.mp4", "-i", ROOT / right / "native.mp4",
        "-loop", 1, "-framerate", 30, "-i", ROOT / captions,
        "-filter_complex", "[0:v]scale=640:480:flags=lanczos[a];[1:v]scale=640:480:flags=lanczos[b];"
        "[a][b]hstack,pad=1280:592:0:72:color=0x071a27[base];[base][2:v]overlay=shortest=1[v]",
        "-map", "[v]", "-c:v", "libx264", "-crf", 17, "-pix_fmt", "yuv420p",
        "-movflags", "+faststart", ROOT / f"{name}.mp4")
    run("-i", ROOT / f"{name}.mp4", "-filter_complex",
        "fps=20,split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=sierra2_4a",
        "-loop", 0, ROOT / f"{name}.gif")
    run("-i", ROOT / f"{name}.mp4", "-ss", 1, "-frames:v", 1, ROOT / f"{name}-still.png")


compose("after-painted", "after-sculpted", "labels-options.png", "both-improved-options")
for style in ("painted", "sculpted"):
    compose(f"before-{style}", f"after-{style}", f"labels-{style}.png", f"{style}-before-after")
    compose(f"before-feed-{style}", f"after-feed-{style}", f"labels-feed-{style}.png", f"{style}-feeding-before-after")
print("Composed both improved styles, turn and feeding comparisons, GIFs and stills.")
