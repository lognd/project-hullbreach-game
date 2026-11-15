using System;

namespace Hullbreach.Hud
{
    // Engine-free world-to-minimap mapping: a square map showing a disc of
    // WorldRadius around (CenterX, CenterY), +Y up, origin at the map centre.
    // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
    public readonly struct MinimapView
    {
        public readonly float CenterX;
        public readonly float CenterY;
        // World units from the centre to the map edge.
        public readonly float WorldRadius;
        // Pixels from the centre to the map edge (half the map size).
        public readonly float PixelRadius;

        public MinimapView(float centerX, float centerY, float worldRadius, float pixelRadius)
        {
            CenterX = centerX;
            CenterY = centerY;
            WorldRadius = worldRadius > 1e-3f ? worldRadius : 1e-3f;
            PixelRadius = pixelRadius;
        }

        // Pixels per world unit.
        public float Scale => PixelRadius / WorldRadius;

        // World point to map offset from the map centre in pixels (+Y up). May lie outside the map.
        public void ToMap(float worldX, float worldY, out float mapX, out float mapY)
        {
            float s = Scale;
            mapX = (worldX - CenterX) * s;
            mapY = (worldY - CenterY) * s;
        }

        // Same, but a point beyond the map edge is pinned to the edge (inset by marginPx) along its direction.
        // Returns true when the point was inside the map.
        public bool ToMapClamped(float worldX, float worldY, float marginPx, out float mapX, out float mapY)
        {
            ToMap(worldX, worldY, out mapX, out mapY);
            float limit = Math.Max(0f, PixelRadius - marginPx);
            float len = (float)Math.Sqrt(mapX * mapX + mapY * mapY);
            if (len <= limit) return true;
            float k = limit / len;
            mapX *= k;
            mapY *= k;
            return false;
        }

        // Map pixel (row 0 at the top) back to world; the inverse the raster uses.
        public void PixelToWorld(int px, int py, int size, out float worldX, out float worldY)
        {
            float mx = (px + 0.5f) - size * 0.5f;
            float my = size * 0.5f - (py + 0.5f);
            float inv = 1f / Scale;
            worldX = CenterX + mx * inv;
            worldY = CenterY + my * inv;
        }

        // A view that shows a whole circle (arena or storm) with a little breathing room.
        public static MinimapView Fit(float centerX, float centerY, float contentRadius, float pixelRadius, float padding = 1.1f)
            => new MinimapView(centerX, centerY, contentRadius * padding, pixelRadius);
    }

    // A filled disc on the radar (a planet).
    public readonly struct MinimapDisc
    {
        public readonly float X, Y, Radius;
        public readonly byte R, G, B;
        public MinimapDisc(float x, float y, float radius, byte r, byte g, byte b)
        { X = x; Y = y; Radius = radius; R = r; G = g; B = b; }
    }

    // CPU raster of the static part of the radar into an RGBA32 byte buffer, row 0 at the TOP
    // (call FlipRows before handing it to a Unity texture, which is bottom-up).
    // Layers: dark base, danger tint outside the current ring, planets, next ring, current ring.
    // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
    public static class MinimapRaster
    {
        public const int BytesPerPixel = 4;

        public static byte[] Allocate(int size) => new byte[size * size * BytesPerPixel];

        // ringRadius <= 0 means no storm: no tint and no rings.
        public static void Render(byte[] rgba, int size, MinimapView view, MinimapDisc[] planets,
            float ringX, float ringY, float ringRadius, float nextX, float nextY, float nextRadius,
            float ringWidthPx = 1.5f)
        {
            if (rgba == null) throw new ArgumentNullException(nameof(rgba));
            if (rgba.Length < size * size * BytesPerPixel) throw new ArgumentException("buffer too small", nameof(rgba));
            float ringHalf = ringWidthPx * 0.5f / view.Scale; // half width in world units
            bool storm = ringRadius > 0f;
            float mapR = size * 0.5f;
            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    int i = (py * size + px) * BytesPerPixel;
                    float dxp = px + 0.5f - mapR, dyp = py + 0.5f - mapR;
                    float edge = (float)Math.Sqrt(dxp * dxp + dyp * dyp);
                    if (edge > mapR) { rgba[i] = rgba[i + 1] = rgba[i + 2] = rgba[i + 3] = 0; continue; } // round map
                    view.PixelToWorld(px, py, size, out float wx, out float wy);
                    float r = 10, g = 14, b = 24, a = 200;
                    if (storm && Dist(wx, wy, ringX, ringY) > ringRadius) { r = 70; g = 12; b = 16; a = 215; } // danger side
                    if (planets != null)
                    {
                        for (int k = 0; k < planets.Length; k++)
                        {
                            if (Dist(wx, wy, planets[k].X, planets[k].Y) <= planets[k].Radius)
                            { r = planets[k].R; g = planets[k].G; b = planets[k].B; a = 255; break; }
                        }
                    }
                    if (storm)
                    {
                        if (nextRadius > 0f && Math.Abs(Dist(wx, wy, nextX, nextY) - nextRadius) <= ringHalf)
                        { r = 90; g = 230; b = 120; a = 255; }
                        if (Math.Abs(Dist(wx, wy, ringX, ringY) - ringRadius) <= ringHalf)
                        { r = 255; g = 255; b = 255; a = 255; }
                    }
                    if (edge > mapR - 1.5f) { r = 120; g = 130; b = 150; a = 255; } // frame
                    rgba[i] = (byte)r; rgba[i + 1] = (byte)g; rgba[i + 2] = (byte)b; rgba[i + 3] = (byte)a;
                }
            }
        }

        // Flip rows in place (top-down raster to Unity's bottom-up textures).
        public static void FlipRows(byte[] rgba, int size)
        {
            int stride = size * BytesPerPixel;
            var tmp = new byte[stride];
            for (int y = 0; y < size / 2; y++)
            {
                int a = y * stride, b = (size - 1 - y) * stride;
                Buffer.BlockCopy(rgba, a, tmp, 0, stride);
                Buffer.BlockCopy(rgba, b, rgba, a, stride);
                Buffer.BlockCopy(tmp, 0, rgba, b, stride);
            }
        }

        static float Dist(float ax, float ay, float bx, float by)
        {
            float dx = ax - bx, dy = ay - by;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
