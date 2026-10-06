using UnityEngine;

namespace DeepFeast
{
    // The autoplay bot, for -autoplay testing and recordings.
    public sealed partial class Game
    {
        // The bot plays the way a person does: it picks a meal and stays on it, gives up on one it cannot
        // catch, flees hunters, and cruises on through open water when nothing is in reach. Its heading
        // turns like a swimmer's, so it never dithers between two meals or hangs against the surface.
        Fish botTarget, botSkip;
        float botChase, botWanderDir = 1;
        Vector2 botHeading = Vector2.right;
        // How long the bot's hero barely moved, out of how long it was watched, since the last stat:
        // sampled every quarter second against the distance it covers at a third of its cruising speed.
        float botStill, botWatch, botSample;
        Vector2 botFrom;

        void WatchBot(float rdt)
        {
            botWatch += rdt;
            if ((botSample -= rdt) > 0) return;
            botSample = 0.25f;
            if (Dist(player.x - botFrom.x, player.y - botFrom.y) < Data.SpeedFor(player.r) * 0.3f * 0.25f) botStill += 0.25f;
            botFrom = new Vector2(player.x, player.y);
        }

        // The share of the watch the hero spent barely moving, in percent; the watch starts again.
        float TakeBotStill()
        {
            float still = 100 * botStill / Mathf.Max(botWatch, 1e-3f);
            botStill = botWatch = 0;
            return still;
        }

        // A fish that fled into a corner or past the end of the world, out of reach of the hero's
        // body (kept a radius from the ends and nearly half a radius from the surface), is let go.
        bool Reachable(Fish f)
        {
            float pr = player.r, hx = Mathf.Clamp(f.x, pr, World.W - pr);
            float hy = U.ClampSafe(f.y, pr * 0.4f, world.FloorY(hx) - pr * 0.6f);
            return Dist(f.x - hx, f.y - hy) < (pr + f.r * 0.5f) * 0.9f;
        }

        Vector2 BotSteer(float dt, out bool dash)
        {
            dash = false;
            float pr = player.r;
            Vector2 avoid = Vector2.zero;
            foreach (var f in fish)
            {
                if (f == finale || f.r < pr * Data.DANGER) continue;
                float dx = f.x - player.x, dy = f.y - player.y, d = Mathf.Max(1, Dist(dx, dy));
                bool hunting = f.state == FState.Chase;
                float R = f.r * (hunting ? 4.5f : 2.6f) + pr * 5;
                if (d >= R) continue;
                float w = 1 - d / R; w *= w * (hunting ? 9 : 4);
                avoid -= new Vector2(dx, dy) / d * w;
                if (hunting && d < f.r * 2.4f + pr * 4) dash = true;
            }
            foreach (var j in jellies)
            {
                // Two lengths of its sting: wider would push a giant hero into a corner of the sea.
                float dx = j.x - player.x, dy = j.y + j.r * 0.7f - player.y, d = Mathf.Max(1, Dist(dx, dy));
                float R = (pr * 0.7f + j.r * 0.85f) * 2;
                if (d < R) { float w = 1 - d / R; avoid -= new Vector2(dx, dy) / d * w * w * 6; }
            }

            // A meal is kept until it is eaten, outgrows the hero or outruns it for four seconds; one
            // that got away is left alone after that. Only a clearly better meal takes its place.
            if (botTarget != null && (!fish.Contains(botTarget) || botTarget.r > pr * Data.EAT || botChase > 4))
            {
                if (botChase > 4) botSkip = botTarget;
                botTarget = null;
            }
            Fish best = null;
            float bestV = 0, heldV = 0;
            foreach (var f in fish)
            {
                if (f == finale || f == botSkip || f.r > pr * Data.EAT || !Reachable(f)) continue;
                float v = (f.r / pr) / (Dist(f.x - player.x, f.y - player.y) + pr * 3);
                if (f == botTarget) heldV = v;
                if (v > bestV) { bestV = v; best = f; }
            }
            if (best != null && best != botTarget && (botTarget == null || bestV > heldV * 1.5f)) { botTarget = best; botChase = 0; }
            botChase += dt;

            Vector2 seek;
            float close = float.MaxValue;
            Pearl pearl = null;
            foreach (var p in pearls)
            {
                float d = Dist(p.x - player.x, p.y - player.y);
                if (d < close) { close = d; pearl = p; }
            }
            if (finale != null)
            {
                // The finale is worth more than any snack: follow the gold arrow and dash in close.
                float dx = finale.x - player.x, dy = finale.y - player.y, d = Mathf.Max(1, Dist(dx, dy));
                seek = new Vector2(dx, dy) / d * 4; close = d;
                if (d < pr * 6 && pStamina > 0.4f) dash = true;
            }
            else if (pearl != null) seek = new Vector2(pearl.x - player.x, pearl.y - player.y) / Mathf.Max(1, close) * 1.5f;
            else if (botTarget != null)
            {
                float dx = botTarget.x - player.x, dy = botTarget.y - player.y, d = Mathf.Max(1, Dist(dx, dy));
                seek = new Vector2(dx, dy) / d; close = d;
                // A meal that keeps slipping away earns a short lunge, as a player would make.
                if (botChase > 1.2f && d < pr * 5 && pStamina > 0.3f) dash = true;
            }
            else
            {
                // Nothing in reach: cruise on through the middle of the water, where new fish turn up.
                if (player.x < 600) botWanderDir = 1; else if (player.x > World.W - 600) botWanderDir = -1;
                float top = pr * 2 + 60, bottom = world.FloorY(player.x) - pr * 2.5f;
                float level = bottom > top ? Mathf.Clamp(player.y, top, bottom) : (top + bottom) / 2;
                seek = new Vector2(botWanderDir, Mathf.Clamp((level - player.y) / (pr * 4), -0.7f, 0.7f));
            }

            var want = seek + avoid;
            // Pressing into the seabed, the surface or either end of the sea gets nowhere: slide along it instead.
            if (want.y > 0 && world.FloorY(player.x) - player.y < pr * 1.2f) want.y = 0;
            if (want.y < 0 && player.y < pr * 1.2f) want.y = 0;
            if (want.x < 0 && player.x < pr * 1.2f || want.x > 0 && player.x > World.W - pr * 1.2f) want.x = 0;
            // Cornered with nowhere to slide, it makes for its meal and risks a sting.
            if (want.sqrMagnitude < 1e-4f) want = seek.sqrMagnitude > 1e-4f ? seek : new Vector2(botWanderDir, 0);
            // Fleeing and the last stretch to a meal turn sharply; otherwise the hero eases round.
            float turn = avoid.sqrMagnitude > 1 ? 10 : close < pr * 5 ? 12 : 5;
            botHeading = ((Vector2)Vector3.RotateTowards(botHeading, want.normalized, dt * turn, 0)).normalized;
            return botHeading;
        }
    }
}
