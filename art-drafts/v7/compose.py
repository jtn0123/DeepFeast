"""Compose unmodified native Unity captures with browser-drawn captions."""
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parent


def run(*args):
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", *map(str, args)], check=True)


for name in ('before-turn', 'after-turn', 'before-feed', 'after-feed'):
    directory = ROOT / name
    if len(list(directory.glob('frame_*.png'))) == 120:
        run('-framerate', 30, '-i', directory / 'frame_%03d.png', '-c:v', 'libx264',
            '-crf', 15, '-pix_fmt', 'yuv420p', '-movflags', '+faststart', directory / 'native.mp4')
    if not (directory / 'native.mp4').exists():
        raise RuntimeError(f'Missing native sequence: {directory.name}')


def compose(left, right, captions, name, closeup=False):
    framing = "crop=460:240:250:20,scale=640:-2:flags=lanczos,pad=640:480:0:(oh-ih)/2:color=0x091f2e" if closeup else "scale=640:480:flags=lanczos"
    run("-i", ROOT / left / "native.mp4", "-i", ROOT / right / "native.mp4",
        "-loop", 1, "-framerate", 30, "-i", ROOT / captions,
        "-filter_complex", f"[0:v]{framing}[a];[1:v]{framing}[b];"
        "[a][b]hstack,pad=1280:592:0:72:color=0x071a27[base];[base][2:v]overlay=shortest=1[v]",
        "-map", "[v]", "-c:v", "libx264", "-crf", 17, "-pix_fmt", "yuv420p",
        "-movflags", "+faststart", ROOT / f"{name}.mp4")
    run("-i", ROOT / f"{name}.mp4", "-filter_complex",
        "fps=20,split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=sierra2_4a",
        "-loop", 0, ROOT / f"{name}.gif")
    run("-i", ROOT / f"{name}.mp4", "-ss", 1, "-frames:v", 1, ROOT / f"{name}-still.png")


compose('before-turn', 'after-turn', 'labels-turn.png', 'sculpted-turn-before-after')
compose('before-feed', 'after-feed', 'labels-feed.png', 'sculpted-swim-feeding-before-after')
compose('before-feed', 'after-feed', 'labels-feed.png', 'fin-swim-closeup', closeup=True)
print('Composed the refined 3D turn and swimming/feeding comparisons.')
