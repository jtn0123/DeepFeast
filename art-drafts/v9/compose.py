"""Encode native Unity review frames and compare matching white-shark turn inputs."""
from pathlib import Path
import shutil
import subprocess

ROOT = Path(__file__).resolve().parent
GROUPS = ("sharks", "atlantic-a", "atlantic-b", "extras", "legacy-a", "legacy-b", "legacy-c")


def run(*args):
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", *map(str, args)], check=True)


def encode(directory):
    frames = list(directory.glob("frame_*.png"))
    if len(frames) == 120:
        run("-framerate", 30, "-i", directory / "frame_%03d.png", "-c:v", "libx264",
            "-crf", 15, "-pix_fmt", "yuv420p", "-movflags", "+faststart", directory / "native.mp4")
    if not (directory / "native.mp4").exists():
        raise RuntimeError(f"Missing native capture: {directory.name}")


def gif(name, scale=None):
    sizing = f",scale={scale}:flags=lanczos" if scale else ""
    run("-i", ROOT / f"{name}.mp4", "-filter_complex",
        f"fps=20{sizing},split[a][b];[a]palettegen=stats_mode=diff[p];"
        "[b][p]paletteuse=dither=sierra2_4a", "-loop", 0, ROOT / f"{name}.gif")


for group in GROUPS:
    encode(ROOT / group)
    shutil.copyfile(ROOT / group / "native.mp4", ROOT / f"{group}.mp4")
    run("-i", ROOT / f"{group}.mp4", "-frames:v", 1, ROOT / f"{group}-still.png")

encode(ROOT / "after-turn")
before = ROOT.parent / "v8" / "after-turn"
baseline = ROOT / "before-turn"
baseline.mkdir(exist_ok=True)
for file in ("native.mp4", "native-samples.json", "frame_000.png", "frame_015.png", "frame_030.png"):
    source = before / file
    if source.exists(): shutil.copyfile(source, baseline / file)
    elif not (baseline / file).exists(): raise RuntimeError(f"Missing baseline: {file}")
framing = "crop=480:270:230:438,scale=640:-2:flags=lanczos,pad=640:480:0:(oh-ih)/2:color=0x091f2e"
run("-i", baseline / "native.mp4", "-i", ROOT / "after-turn/native.mp4",
    "-loop", 1, "-framerate", 30, "-i", ROOT / "labels-shark.png",
    "-filter_complex", f"[0:v]{framing}[a];[1:v]{framing}[b];"
    "[a][b]hstack,pad=1280:592:0:72:color=0x071a27[base];[base][2:v]overlay=shortest=1[v]",
    "-map", "[v]", "-c:v", "libx264", "-crf", 17, "-pix_fmt", "yuv420p",
    "-movflags", "+faststart", ROOT / "shark-before-after.mp4")
run("-i", ROOT / "shark-before-after.mp4", "-frames:v", 1, ROOT / "shark-before-after-still.png")
gif("shark-before-after")
gif("sharks", "960:720")
run("-i", ROOT / "atlantic-a.mp4", "-i", ROOT / "atlantic-b.mp4", "-filter_complex",
    "[0:v]scale=640:480:flags=lanczos[a];[1:v]scale=640:480:flags=lanczos[b];[a][b]hstack[v]",
    "-map", "[v]", "-c:v", "libx264", "-crf", 17, "-pix_fmt", "yuv420p",
    "-movflags", "+faststart", ROOT / "new-fish.mp4")
run("-i", ROOT / "new-fish.mp4", "-frames:v", 1, ROOT / "new-fish-still.png")
gif("new-fish")
print("Composed eight looping groups, all eight requested fish and matching white-shark comparison.")
