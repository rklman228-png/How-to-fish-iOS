using UnityEngine;

namespace HowToFish.Mobile
{
    internal static class ProceduralMobileIcons
    {
        public static Sprite CreateDropIcon() => Draw(96, tex =>
        {
            Line(tex, 28, 23, 28, 55, 5);
            Line(tex, 28, 38, 41, 26, 5);
            Line(tex, 28, 38, 15, 26, 5);
            Line(tex, 56, 22, 56, 50, 5);
            Line(tex, 46, 50, 66, 50, 5);
            Line(tex, 47, 50, 47, 62, 5);
            Line(tex, 66, 50, 66, 62, 5);
            Line(tex, 47, 62, 66, 62, 5);
        });

        public static Sprite CreateCrouchIcon() => Draw(96, tex =>
        {
            Circle(tex, 64, 24, 9, 5);
            Line(tex, 57, 34, 43, 48, 6);
            Line(tex, 43, 48, 63, 55, 6);
            Line(tex, 63, 55, 76, 70, 6);
            Line(tex, 43, 48, 27, 62, 6);
            Line(tex, 27, 62, 45, 72, 6);
            Line(tex, 45, 72, 63, 72, 6);
        });

        private static Sprite Draw(int size, System.Action<Texture2D> draw)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "HowToFishMobileIcon",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, 0);
            tex.SetPixels32(pixels);
            draw(tex);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f);
        }

        private static void Plot(Texture2D tex, int x, int y, int radius)
        {
            for (int oy = -radius; oy <= radius; oy++)
            for (int ox = -radius; ox <= radius; ox++)
            {
                if (ox * ox + oy * oy > radius * radius) continue;
                int px = x + ox, py = y + oy;
                if ((uint)px >= tex.width || (uint)py >= tex.height) continue;
                tex.SetPixel(px, py, Color.black);
            }
        }

        private static void Line(Texture2D tex, int x0, int y0, int x1, int y1, int width)
        {
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                Plot(tex, x0, y0, Mathf.Max(1, width / 2));
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        private static void Circle(Texture2D tex, int cx, int cy, int radius, int width)
        {
            const int steps = 72;
            var last = new Vector2Int(cx + radius, cy);
            for (int i = 1; i <= steps; i++)
            {
                float a = i * Mathf.PI * 2f / steps;
                var next = new Vector2Int(
                    Mathf.RoundToInt(cx + Mathf.Cos(a) * radius),
                    Mathf.RoundToInt(cy + Mathf.Sin(a) * radius));
                Line(tex, last.x, last.y, next.x, next.y, width);
                last = next;
            }
        }
    }
}
