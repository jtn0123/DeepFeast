using System.Collections.Generic;
using UnityEngine;

namespace DeepFeast
{
    public enum FState { Wander, Chase, Flee, Leave }

    public sealed class School
    {
        public int dir, n;
        public float fleeT, turnT, cx, cy, vx;
        public bool wasFleeing;
    }

    public sealed class Fish
    {
        public int id;
        public Species sp;
        public bool shark;
        public float r, x, y, vx, vy, face = 1, faceS = 1, tilt, wag, phase, cruise, homeY, homeT;
        public float cool, chaseT, alertT, mouth, chomp, eatCool, aggro, slotX, slotY, life;
        public int dir, leaveDir, bumpT = -1;
        public FState state;
        public School school;
        public FishView view;
    }

    public sealed class Jelly
    {
        public float x, y, r, vx, vy, phase, pf, hue;
        public SpriteRenderer bell, glow;
    }

    public sealed class Pearl
    {
        public float x, y, r, life, ph;
        public PearlKind kind;
        public SpriteRenderer body, star, glow, ring;
    }

    public enum PearlKind { Shield, Magnet, Burst, Lantern }

    // What each pearl grants: predators bounce off the shield, the magnet reels in fish small
    // enough to eat, the burst gives speed and a dash that never tires, and the abyss lantern
    // lights the dark so hunters lose your trail.
    public static class Powers
    {
        public const int Count = 4;
        public static readonly string[] Names = { "SHIELD", "MAGNET", "BURST", "LANTERN" };
        public static readonly Color[] Colors = { U.Hex("#8cf0ff"), U.Hex("#ff86d8"), U.Hex("#a6ff5c"), U.Hex("#ffc35a") };
        public static readonly float[] Durations = { 7, 7, 6, 14 };
    }

    public enum PType { Bubble, Bit, Spark, Ring, Wake }

    public struct Particle
    {
        public PType type;
        public float x, y, vx, vy, life, max, size, ph;
        public Color col;
    }

    /// <summary>Pooled sprite renderers for bubbles, crumbs, sparks and shock rings.</summary>
    public sealed class Particles
    {
        public const int MAX = 800;
        public readonly List<Particle> list = new List<Particle>(MAX);
        readonly SpriteRenderer[] pool = new SpriteRenderer[MAX];
        readonly bool[] additive = new bool[MAX];

        public Particles(Transform parent)
        {
            var root = new GameObject("Particles").transform;
            root.SetParent(parent, false);
            for (int i = 0; i < MAX; i++)
            {
                pool[i] = Gfx.SpriteObject("P", root, Gfx.Disc, Layer.Particles);
                pool[i].enabled = false;
            }
        }

        public void Add(Particle p) { if (list.Count < MAX) list.Add(p); }

        public void Clear() => list.Clear();

        public void Step(float dt)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var p = list[i];
                p.life -= dt;
                if (p.life <= 0) { list.RemoveAt(i); continue; }
                if (p.type == PType.Bubble)
                {
                    p.ph += dt * 4;
                    p.x += (p.vx + Mathf.Sin(p.ph) * p.size * 1.5f) * dt; p.y += p.vy * dt;
                    if (p.y < 0) p.life = 0;
                }
                else if (p.type == PType.Bit || p.type == PType.Spark)
                {
                    p.vx *= 1 - dt * 2.5f; p.vy *= 1 - dt * 2.5f;
                    if (p.type == PType.Bit) p.vy += p.size * 6 * dt;
                    p.x += p.vx * dt; p.y += p.vy * dt;
                }
                list[i] = p;
            }
        }

        public void Render()
        {
            int n = list.Count;
            for (int i = 0; i < MAX; i++)
            {
                var sr = pool[i];
                if (i >= n) { if (sr.enabled) sr.enabled = false; continue; }
                var p = list[i];
                float a = Mathf.Clamp01(p.life / p.max);
                bool add = p.type == PType.Spark;
                if (additive[i] != add) { additive[i] = add; sr.sharedMaterial = add ? Gfx.Additive : Gfx.Alpha; }
                sr.enabled = true;
                sr.sortingOrder = p.type == PType.Wake ? Layer.FishBase - 1 : Layer.Particles;
                float d;
                switch (p.type)
                {
                    case PType.Bubble: sr.sprite = Gfx.Bubble; d = p.size / 0.46f; sr.color = new Color(1, 1, 1, a); break;
                    case PType.Bit: sr.sprite = Gfx.Spark; d = p.size * 2.4f; sr.color = U.WithA(p.col, a * 0.8f); break;
                    case PType.Spark: sr.sprite = Gfx.Spark; d = p.size * (0.5f + a) * 3.2f; sr.color = U.WithA(p.col, a); break;
                    case PType.Wake: sr.sprite = Gfx.Glow; d = p.size * (1.8f + (1 - a)); sr.color = U.WithA(p.col, a * 0.32f); break;
                    default: sr.sprite = Gfx.Ring; d = p.size * (0.3f + (1 - a)) * 2 / 0.9f; sr.color = U.WithA(p.col, 0.6f * a); break;
                }
                var t = sr.transform;
                t.localPosition = U.V3(p.x, p.y, -i * 0.0001f);
                t.localScale = p.type == PType.Wake ? new Vector3(d * 2.4f, d * 0.32f, 1) : new Vector3(d, d, 1);
                t.localRotation = p.type == PType.Wake ? Quaternion.Euler(0, 0, -Mathf.Atan2(p.vy, p.vx) * Mathf.Rad2Deg) : Quaternion.identity;
            }
        }
    }
}
