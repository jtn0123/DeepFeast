from pathlib import Path
import subprocess

out = Path(__file__).resolve().parent
filters = "[0:v]scale=640:480:flags=lanczos[b];[1:v]scale=640:480:flags=lanczos[a];[b][a]hstack,pad=1280:592:0:72:color=0x071a27[base];[base][2:v]overlay=shortest=1[v]"
subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-framerate", "30", "-i", str(out/"before/frame_%03d.png"), "-framerate", "30", "-i", str(out/"after/frame_%03d.png"), "-loop", "1", "-framerate", "30", "-i", str(out/"labels.png"), "-filter_complex", filters, "-map", "[v]", "-c:v", "libx264", "-crf", "17", "-pix_fmt", "yuv420p", "-movflags", "+faststart", str(out/"fish-turn-before-after.mp4")], check=True)
subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(out/"fish-turn-before-after.mp4"), "-filter_complex", "fps=20,split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=sierra2_4a", "-loop", "0", str(out/"fish-turn-before-after.gif")], check=True)
subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(out/"fish-turn-before-after.mp4"), "-ss", "1", "-frames:v", "1", str(out/"mid-turn.png")], check=True)
print("Composed MP4, looping GIF and midpoint still.")
