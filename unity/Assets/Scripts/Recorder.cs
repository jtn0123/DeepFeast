using System.IO;
using System.Text;
using Unity.Collections;
using UnityEngine;

namespace DeepFeast
{
    /// <summary>Records a headless test run as video (-record fps): the game steps a fixed time per frame
    /// however long each frame takes, every frame is saved from the capture target, and the mixed sound
    /// goes to audio.wav beside them. While the audio renderer records, Unity sends the speakers silence.</summary>
    public sealed class Recorder
    {
        readonly string dir;
        readonly BinaryWriter wav;
        readonly int channels;
        Texture2D image;
        int frames;
        long samples;

        public bool Audio => wav != null;
        // Unscaled time for the game and its animation. A recording steps it by the frame, as it does
        // game time; otherwise it is the wall clock.
        public static float UnscaledDelta => Time.captureDeltaTime > 0 ? Time.captureDeltaTime : Time.unscaledDeltaTime;
        public static float UnscaledTime => Time.captureDeltaTime > 0 ? Time.frameCount * Time.captureDeltaTime : Time.unscaledTime;

        public Recorder(string dir, float fps)
        {
            this.dir = dir;
            Time.captureDeltaTime = 1 / fps;
            channels = AudioSettings.speakerMode switch
            {
                AudioSpeakerMode.Mono => 1,
                AudioSpeakerMode.Quad => 4,
                AudioSpeakerMode.Surround => 5,
                AudioSpeakerMode.Mode5point1 => 6,
                AudioSpeakerMode.Mode7point1 => 8,
                _ => 2,
            };
            if (AudioRenderer.Start())
            {
                wav = new BinaryWriter(File.Create(System.IO.Path.Combine(dir, "audio.wav")));
                // The header is written on close, once the length is known.
                wav.Write(new byte[44]);
            }
            Debug.Log($"[DeepFeast] recording at {fps:0} fps " + (Audio ? $"with {channels}-channel audio at {AudioSettings.outputSampleRate} Hz" : "without audio"));
        }

        public void Frame(RenderTexture target)
        {
            image ??= new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            var old = RenderTexture.active;
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, image.width, image.height), 0, 0);
            RenderTexture.active = old;
            // High-quality JPEG keeps a minute of 1080p frames to a few hundred megabytes.
            File.WriteAllBytes(System.IO.Path.Combine(dir, $"frame_{frames++:00000}.jpg"), image.EncodeToJPG(95));
            if (wav == null) return;
            int count = AudioRenderer.GetSampleCountForCaptureFrame();
            using var buffer = new NativeArray<float>(count * channels, Allocator.Temp);
            AudioRenderer.Render(buffer);
            foreach (float s in buffer) wav.Write((short)Mathf.RoundToInt(Mathf.Clamp(s, -1, 1) * 32767));
            samples += count;
        }

        public void Close()
        {
            Time.captureDeltaTime = 0;
            Debug.Log($"[DeepFeast] recorded {frames} frames" + (Audio ? $" and {samples} audio samples" : ""));
            if (image != null) Object.Destroy(image);
            if (wav == null) return;
            AudioRenderer.Stop();
            int rate = AudioSettings.outputSampleRate, bytes = (int)(samples * channels * 2);
            wav.Seek(0, SeekOrigin.Begin);
            wav.Write(Encoding.ASCII.GetBytes("RIFF")); wav.Write(36 + bytes); wav.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            wav.Write(16); wav.Write((short)1); wav.Write((short)channels); wav.Write(rate);
            wav.Write(rate * channels * 2); wav.Write((short)(channels * 2)); wav.Write((short)16);
            wav.Write(Encoding.ASCII.GetBytes("data")); wav.Write(bytes);
            wav.Dispose();
        }
    }
}
