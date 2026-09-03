using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;

namespace PortalNes.Rendering3D
{
    public static class TileMeshFactory
    {
        public const int Columns = 32;
        public const int Rows = 30;
        public const int MaximumTiles = 2048;
        public const int MaximumBoxDepthSegments = 8;
        private const int BoxVerticesPerTile = (MaximumBoxDepthSegments * 4 + 1) * 4;
        private enum BoxFace { Left, Right, Top, Bottom }

        public static Mesh CreateScreenQuad(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = new[]
            {
                new Vector3(-128, -120, 0), new Vector3(128, -120, 0),
                new Vector3(-128, 120, 0), new Vector3(128, 120, 0)
            };
            mesh.uv = new[]
            {
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, 0), new Vector2(1, 0)
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh CreateTileGrid(string name, out Vector3[] vertices, out Vector2[] uv)
        {
            int count = MaximumTiles;
            vertices = new Vector3[count * 4];
            uv = new Vector2[count * 4];
            var triangles = new int[count * 6];
            for (int tile = 0; tile < count; tile++)
            {
                int vi = tile * 4, ti = tile * 6;
                triangles[ti] = vi; triangles[ti + 1] = vi + 2; triangles[ti + 2] = vi + 1;
                triangles[ti + 3] = vi + 2; triangles[ti + 4] = vi + 3; triangles[ti + 5] = vi + 1;
            }
            var mesh = new Mesh { name = name, vertices = vertices, uv = uv, triangles = triangles };
            mesh.MarkDynamic();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static void SetTileQuad(Vector3[] vertices, Vector2[] uv, int tileIndex,
            int leftPixel, int topPixel, int rightPixel, int bottomPixel, float depth, bool visible)
        {
            int vi = tileIndex * 4;
            if (!visible)
            {
                for (int i = 0; i < 4; i++) vertices[vi + i] = Vector3.zero;
                return;
            }
            float left = leftPixel - 128, right = rightPixel - 128;
            float top = 120 - topPixel, bottom = 120 - bottomPixel;
            vertices[vi] = new Vector3(left, bottom, depth); vertices[vi + 1] = new Vector3(right, bottom, depth);
            vertices[vi + 2] = new Vector3(left, top, depth); vertices[vi + 3] = new Vector3(right, top, depth);
            float u0 = leftPixel / 256f, u1 = rightPixel / 256f;
            float v0 = topPixel / 240f, v1 = bottomPixel / 240f;
            uv[vi] = new Vector2(u0, v1); uv[vi + 1] = new Vector2(u1, v1);
            uv[vi + 2] = new Vector2(u0, v0); uv[vi + 3] = new Vector2(u1, v0);
        }

        public static void SetTileDepth(Vector3[] vertices, int tileIndex, float depth)
        {
            int start = tileIndex * 4;
            for (int i = 0; i < 4; i++) vertices[start + i].z = depth;
        }

        public static Mesh CreateSpriteGrid(string name, out Vector3[] vertices, out Vector2[] uv)
        {
            const int spriteCount = 64;
            vertices = new Vector3[spriteCount * 4];
            uv = new Vector2[spriteCount * 4];
            var triangles = new int[spriteCount * 6];
            for (int i = 0; i < spriteCount; i++)
            {
                int vi = i * 4, ti = i * 6;
                triangles[ti] = vi; triangles[ti + 1] = vi + 2; triangles[ti + 2] = vi + 1;
                triangles[ti + 3] = vi + 2; triangles[ti + 4] = vi + 3; triangles[ti + 5] = vi + 1;
            }
            var mesh = new Mesh { name = name, vertices = vertices, uv = uv, triangles = triangles };
            mesh.MarkDynamic();
            return mesh;
        }

        public static void SetSpriteQuad(Vector3[] vertices, Vector2[] uv, int index, int x, int y,
            int height, float depth, bool visible)
        {
            int vi = index * 4;
            if (!visible)
            {
                for (int i = 0; i < 4; i++) vertices[vi + i] = Vector3.zero;
                return;
            }
            float left = x - 128, right = left + 8, top = 120 - y, bottom = top - height;
            vertices[vi] = new Vector3(left, bottom, depth); vertices[vi + 1] = new Vector3(right, bottom, depth);
            vertices[vi + 2] = new Vector3(left, top, depth); vertices[vi + 3] = new Vector3(right, top, depth);
            float u0 = x / 256f, u1 = (x + 8) / 256f, v0 = y / 240f, v1 = (y + height) / 240f;
            uv[vi] = new Vector2(u0, v1); uv[vi + 1] = new Vector2(u1, v1);
            uv[vi + 2] = new Vector2(u0, v0); uv[vi + 3] = new Vector2(u1, v0);
        }

        public static Mesh CreateTileExtrusionGrid(string name, out Vector3[] vertices, out Vector2[] uv)
        {
            int count = MaximumTiles;
            int facesPerTile = MaximumBoxDepthSegments * 4 + 1;
            vertices = new Vector3[count * BoxVerticesPerTile];
            uv = new Vector2[count * BoxVerticesPerTile];
            var triangles = new int[count * facesPerTile * 6];
            for (int tile = 0; tile < count; tile++)
            {
                int vi = tile * BoxVerticesPerTile, ti = tile * facesPerTile * 6;
                for (int side = 0; side < facesPerTile; side++)
                {
                    int v = vi + side * 4, t = ti + side * 6;
                    triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                    triangles[t + 3] = v + 2; triangles[t + 4] = v + 1; triangles[t + 5] = v + 3;
                }
            }
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32,
                vertices = vertices, uv = uv, triangles = triangles };
            mesh.MarkDynamic();
            return mesh;
        }

        public static Mesh CreateCompactExtrusionMesh(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
            return mesh;
        }

        public static void AppendTileExtrusion(List<Vector3> vertices, List<Vector2> uv, List<int> triangles,
            int leftPixel, int topPixel, int rightPixel, int bottomPixel,
            float frontDepth, float thickness, bool showLeft, bool showRight, bool showTop, bool showBottom,
            int sourceOriginX, int sourceOriginY, int surfaceUnitWidth, int surfaceUnitHeight)
        {
            if (thickness <= 0) return;
            float left = leftPixel - 128, right = rightPixel - 128;
            float top = 120 - topPixel, bottom = 120 - bottomPixel, backDepth = frontDepth + thickness;
            surfaceUnitWidth = Mathf.Max(1, surfaceUnitWidth);
            surfaceUnitHeight = Mathf.Max(1, surfaceUnitHeight);
            for (int segment = 0; segment < MaximumBoxDepthSegments; segment++)
            {
                float segmentFront = frontDepth + segment * 8f;
                if (segmentFront >= backDepth) break;
                float segmentBack = segment == MaximumBoxDepthSegments - 1
                    ? backDepth : Mathf.Min(segmentFront + 8f, backDepth);
                int leftSourceX = sourceOriginX + segment % surfaceUnitWidth * 8;
                int rightSourceX = sourceOriginX - (surfaceUnitWidth - 1) * 8 + segment % surfaceUnitWidth * 8;
                int topSourceY = sourceOriginY + segment % surfaceUnitHeight * 8;
                int bottomSourceY = sourceOriginY - (surfaceUnitHeight - 1) * 8 + segment % surfaceUnitHeight * 8;
                if (showLeft) AppendSegment(vertices, uv, triangles,
                    new Vector3(left, bottom, segmentFront), new Vector3(left, bottom, segmentBack),
                    new Vector3(left, top, segmentFront), new Vector3(left, top, segmentBack),
                    leftSourceX, sourceOriginY, BoxFace.Left);
                if (showRight) AppendSegment(vertices, uv, triangles,
                    new Vector3(right, top, segmentFront), new Vector3(right, top, segmentBack),
                    new Vector3(right, bottom, segmentFront), new Vector3(right, bottom, segmentBack),
                    rightSourceX, sourceOriginY, BoxFace.Right);
                if (showTop) AppendSegment(vertices, uv, triangles,
                    new Vector3(left, top, segmentFront), new Vector3(left, top, segmentBack),
                    new Vector3(right, top, segmentFront), new Vector3(right, top, segmentBack),
                    sourceOriginX, topSourceY, BoxFace.Top);
                if (showBottom) AppendSegment(vertices, uv, triangles,
                    new Vector3(right, bottom, segmentFront), new Vector3(right, bottom, segmentBack),
                    new Vector3(left, bottom, segmentFront), new Vector3(left, bottom, segmentBack),
                    sourceOriginX, bottomSourceY, BoxFace.Bottom);
            }
            AppendBack(vertices, uv, triangles, left, top, right, bottom, backDepth,
                leftPixel / 256f, topPixel / 240f, rightPixel / 256f, bottomPixel / 240f);
        }

        private static void AppendSegment(List<Vector3> vertices, List<Vector2> uv, List<int> triangles,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d, int sourceX, int sourceY, BoxFace face)
        {
            if (sourceX < 0 || sourceX >= 256 || sourceY < 0 || sourceY >= 240) return;
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start + 2); triangles.Add(start + 1); triangles.Add(start + 3);
            float u0 = sourceX / 256f, u1 = Mathf.Min(256, sourceX + 8) / 256f;
            float v0 = sourceY / 240f, v1 = Mathf.Min(240, sourceY + 8) / 240f;
            AppendFaceUv(uv, face, u0, v0, u1, v1);
        }

        private static void AppendFaceUv(List<Vector2> uv, BoxFace face, float u0, float v0, float u1, float v1)
        {
            switch (face)
            {
                case BoxFace.Left:
                    uv.Add(new Vector2(u0, v1)); uv.Add(new Vector2(u1, v1));
                    uv.Add(new Vector2(u0, v0)); uv.Add(new Vector2(u1, v0));
                    break;
                case BoxFace.Right:
                    uv.Add(new Vector2(u0, v0)); uv.Add(new Vector2(u1, v0));
                    uv.Add(new Vector2(u0, v1)); uv.Add(new Vector2(u1, v1));
                    break;
                case BoxFace.Top:
                    uv.Add(new Vector2(u0, v0)); uv.Add(new Vector2(u0, v1));
                    uv.Add(new Vector2(u1, v0)); uv.Add(new Vector2(u1, v1));
                    break;
                default:
                    uv.Add(new Vector2(u1, v0)); uv.Add(new Vector2(u1, v1));
                    uv.Add(new Vector2(u0, v0)); uv.Add(new Vector2(u0, v1));
                    break;
            }
        }

        private static void AppendBack(List<Vector3> vertices, List<Vector2> uv, List<int> triangles,
            float left, float top, float right, float bottom, float depth,
            float u0, float v0, float u1, float v1)
        {
            int start = vertices.Count;
            vertices.Add(new Vector3(left, bottom, depth)); vertices.Add(new Vector3(right, bottom, depth));
            vertices.Add(new Vector3(left, top, depth)); vertices.Add(new Vector3(right, top, depth));
            uv.Add(new Vector2(u0, v1)); uv.Add(new Vector2(u1, v1));
            uv.Add(new Vector2(u0, v0)); uv.Add(new Vector2(u1, v0));
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start + 2); triangles.Add(start + 1); triangles.Add(start + 3);
        }

        public static void SetTileExtrusionBounds(Vector3[] vertices, Vector2[] uv, int tileIndex,
            int leftPixel, int topPixel, int rightPixel, int bottomPixel,
            float frontDepth, float thickness, bool visible,
            bool showLeft = true, bool showRight = true, bool showTop = true, bool showBottom = true,
            int sourceOriginX = 0, int sourceOriginY = 0, int surfaceUnitWidth = 1, int surfaceUnitHeight = 1)
        {
            int start = tileIndex * BoxVerticesPerTile;
            if (!visible || thickness <= 0)
            {
                for (int i = 0; i < BoxVerticesPerTile; i++) vertices[start + i] = Vector3.zero;
                return;
            }
            float left = leftPixel - 128, right = rightPixel - 128;
            float top = 120 - topPixel, bottom = 120 - bottomPixel, backDepth = frontDepth + thickness;
            surfaceUnitWidth = Mathf.Max(1, surfaceUnitWidth);
            surfaceUnitHeight = Mathf.Max(1, surfaceUnitHeight);
            for (int segment = 0; segment < MaximumBoxDepthSegments; segment++)
            {
                float segmentFront = frontDepth + segment * 8f;
                bool active = segmentFront < backDepth;
                // Keep the box closed even for unusually deep profiles. The
                // final supported segment stretches over any remaining depth.
                float segmentBack = segment == MaximumBoxDepthSegments - 1
                    ? backDepth : Mathf.Min(segmentFront + 8f, backDepth);
                int leftSourceX = sourceOriginX + segment % surfaceUnitWidth * 8;
                int rightSourceX = sourceOriginX - (surfaceUnitWidth - 1) * 8 + segment % surfaceUnitWidth * 8;
                int topSourceY = sourceOriginY + segment % surfaceUnitHeight * 8;
                int bottomSourceY = sourceOriginY - (surfaceUnitHeight - 1) * 8 + segment % surfaceUnitHeight * 8;
                SetSegment(vertices, uv, start + segment * 4, showLeft && active,
                    new Vector3(left, bottom, segmentFront), new Vector3(left, bottom, segmentBack),
                    new Vector3(left, top, segmentFront), new Vector3(left, top, segmentBack),
                    leftSourceX, sourceOriginY, BoxFace.Left);
                int rightStart = start + MaximumBoxDepthSegments * 4 + segment * 4;
                SetSegment(vertices, uv, rightStart, showRight && active,
                    new Vector3(right, top, segmentFront), new Vector3(right, top, segmentBack),
                    new Vector3(right, bottom, segmentFront), new Vector3(right, bottom, segmentBack),
                    rightSourceX, sourceOriginY, BoxFace.Right);
                int topStart = start + MaximumBoxDepthSegments * 8 + segment * 4;
                SetSegment(vertices, uv, topStart, showTop && active,
                    new Vector3(left, top, segmentFront), new Vector3(left, top, segmentBack),
                    new Vector3(right, top, segmentFront), new Vector3(right, top, segmentBack),
                    sourceOriginX, topSourceY, BoxFace.Top);
                int bottomStart = start + MaximumBoxDepthSegments * 12 + segment * 4;
                SetSegment(vertices, uv, bottomStart, showBottom && active,
                    new Vector3(right, bottom, segmentFront), new Vector3(right, bottom, segmentBack),
                    new Vector3(left, bottom, segmentFront), new Vector3(left, bottom, segmentBack),
                    sourceOriginX, bottomSourceY, BoxFace.Bottom);
            }
            int backStart = start + MaximumBoxDepthSegments * 16;
            float u0 = leftPixel / 256f, u1 = rightPixel / 256f;
            float v0 = topPixel / 240f, v1 = bottomPixel / 240f;
            SetBack(vertices, uv, backStart, left, top, right, bottom, backDepth, u0, v0, u1, v1);
        }

        public static void SetTileExtrusion(Vector3[] vertices, Vector2[] uv, int tileIndex,
            int x, int y, float frontDepth, float thickness, bool visible)
        {
            SetTileExtrusionBounds(vertices, uv, tileIndex, x * 8, y * 8, x * 8 + 8, y * 8 + 8,
                frontDepth, thickness, visible, true, true, true, true, x * 8, y * 8);
        }

        private static void SetSegment(Vector3[] vertices, Vector2[] uv, int start, bool visible,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d, int sourceX, int sourceY, BoxFace face)
        {
            if (!visible || sourceX < 0 || sourceX >= 256 || sourceY < 0 || sourceY >= 240)
            {
                ClearFace(vertices, start);
                return;
            }
            vertices[start] = a; vertices[start + 1] = b;
            vertices[start + 2] = c; vertices[start + 3] = d;
            float u0 = sourceX / 256f, u1 = Mathf.Min(256, sourceX + 8) / 256f;
            float v0 = sourceY / 240f, v1 = Mathf.Min(240, sourceY + 8) / 240f;
            switch (face)
            {
                case BoxFace.Left:
                    uv[start] = new Vector2(u0, v1); uv[start + 1] = new Vector2(u1, v1);
                    uv[start + 2] = new Vector2(u0, v0); uv[start + 3] = new Vector2(u1, v0);
                    break;
                case BoxFace.Right:
                    uv[start] = new Vector2(u0, v0); uv[start + 1] = new Vector2(u1, v0);
                    uv[start + 2] = new Vector2(u0, v1); uv[start + 3] = new Vector2(u1, v1);
                    break;
                case BoxFace.Top:
                    uv[start] = new Vector2(u0, v0); uv[start + 1] = new Vector2(u0, v1);
                    uv[start + 2] = new Vector2(u1, v0); uv[start + 3] = new Vector2(u1, v1);
                    break;
                default: // Bottom
                    uv[start] = new Vector2(u1, v0); uv[start + 1] = new Vector2(u1, v1);
                    uv[start + 2] = new Vector2(u0, v0); uv[start + 3] = new Vector2(u0, v1);
                    break;
            }
        }

        private static void SetSide(Vector3[] vertices, Vector2[] uv, int start,
            Vector3 frontA, Vector3 frontB, Vector3 backA, Vector3 backB,
            float ua, float va, float ub, float vb)
        {
            vertices[start] = frontA; vertices[start + 1] = backA;
            vertices[start + 2] = frontB; vertices[start + 3] = backB;
            uv[start] = new Vector2(ua, va); uv[start + 1] = new Vector2(ua, va);
            uv[start + 2] = new Vector2(ub, vb); uv[start + 3] = new Vector2(ub, vb);
        }

        private static void ClearFace(Vector3[] vertices, int start)
        {
            vertices[start] = vertices[start + 1] = vertices[start + 2] = vertices[start + 3] = Vector3.zero;
        }

        private static void SetBack(Vector3[] vertices, Vector2[] uv, int start,
            float left, float top, float right, float bottom, float depth,
            float u0, float v0, float u1, float v1)
        {
            vertices[start] = new Vector3(left, bottom, depth); vertices[start + 1] = new Vector3(right, bottom, depth);
            vertices[start + 2] = new Vector3(left, top, depth); vertices[start + 3] = new Vector3(right, top, depth);
            uv[start] = new Vector2(u0, v1); uv[start + 1] = new Vector2(u1, v1);
            uv[start + 2] = new Vector2(u0, v0); uv[start + 3] = new Vector2(u1, v0);
        }

        public static Mesh CreateBeveledPrism(float bevel)
        {
            bevel = Mathf.Clamp(bevel, 0.001f, 0.49f);
            var ring = new[]
            {
                new Vector2(-.5f + bevel,-.5f), new Vector2(.5f - bevel,-.5f),
                new Vector2(.5f,-.5f + bevel), new Vector2(.5f,.5f - bevel),
                new Vector2(.5f - bevel,.5f), new Vector2(-.5f + bevel,.5f),
                new Vector2(-.5f,.5f - bevel), new Vector2(-.5f,-.5f + bevel)
            };
            return CreatePrism("NES Beveled Tile", ring);
        }

        public static Mesh CreateCylinder(int segments)
        {
            segments = Mathf.Clamp(segments, 3, 32);
            var ring = new Vector2[segments];
            for (int i = 0; i < segments; i++)
            {
                float angle = -Mathf.PI * 0.5f + i * Mathf.PI * 2f / segments;
                ring[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.5f;
            }
            return CreatePrism("NES Cylinder Tile", ring);
        }

        public static Mesh CreateUnitBox(string name = "NES Unit Box")
        {
            return CreatePrism(name, new[]
            {
                new Vector2(-.5f, -.5f), new Vector2(-.5f, .5f),
                new Vector2(.5f, .5f), new Vector2(.5f, -.5f)
            });
        }

        public static Mesh CreatePixelExtrusion(ulong mask, byte[] sideColorSources = null)
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                if ((mask & (1UL << (y * 8 + x))) == 0) continue;
                float left = -.5f + x / 8f, right = -.5f + (x + 1) / 8f;
                float top = .5f - y / 8f, bottom = .5f - (y + 1) / 8f;
                float u0 = x / 8f, u1 = (x + 1) / 8f, v0 = 1f - y / 8f, v1 = 1f - (y + 1) / 8f;
                int source = sideColorSources != null && sideColorSources.Length == 64
                    ? sideColorSources[y * 8 + x] : y * 8 + x;
                Vector2 pixelUv = new Vector2((source % 8 + .5f) / 8f, 1f - (source / 8 + .5f) / 8f);
                AddQuad(vertices, uv, triangles,
                    new Vector3(left,bottom,0), new Vector3(right,bottom,0), new Vector3(left,top,0), new Vector3(right,top,0),
                    new Vector2(u0,v1), new Vector2(u1,v1), new Vector2(u0,v0), new Vector2(u1,v0));
                AddQuad(vertices, uv, triangles,
                    new Vector3(right,bottom,1), new Vector3(left,bottom,1), new Vector3(right,top,1), new Vector3(left,top,1),
                    new Vector2(u1,v1), new Vector2(u0,v1), new Vector2(u1,v0), new Vector2(u0,v0));
                if (x == 0 || (mask & (1UL << (y * 8 + x - 1))) == 0)
                    AddSideQuad(vertices, uv, triangles, new Vector3(left,bottom,0), new Vector3(left,top,0), new Vector3(left,bottom,1), new Vector3(left,top,1), pixelUv);
                if (x == 7 || (mask & (1UL << (y * 8 + x + 1))) == 0)
                    AddSideQuad(vertices, uv, triangles, new Vector3(right,top,0), new Vector3(right,bottom,0), new Vector3(right,top,1), new Vector3(right,bottom,1), pixelUv);
                if (y == 0 || (mask & (1UL << ((y - 1) * 8 + x))) == 0)
                    AddSideQuad(vertices, uv, triangles, new Vector3(left,top,0), new Vector3(right,top,0), new Vector3(left,top,1), new Vector3(right,top,1), pixelUv);
                if (y == 7 || (mask & (1UL << ((y + 1) * 8 + x))) == 0)
                    AddSideQuad(vertices, uv, triangles, new Vector3(right,bottom,0), new Vector3(left,bottom,0), new Vector3(right,bottom,1), new Vector3(left,bottom,1), pixelUv);
            }
            var mesh = new Mesh { name = "NES Pixel Extrusion" };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        public static void AppendPixelExtrusion(List<Vector3> vertices, List<Vector2> uv, List<int> triangles,
            ulong mask, int originX, int originY, float frontDepth, float thickness,
            byte[] sideColorSources = null)
        {
            if (mask == 0 || thickness <= 0) return;
            float backDepth = frontDepth + thickness;
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                int local = y * 8 + x;
                if ((mask & (1UL << local)) == 0) continue;
                int screenX = originX + x, screenY = originY + y;
                if (screenX < 0 || screenX >= 256 || screenY < 0 || screenY >= 240) continue;
                float left = screenX - 128, right = left + 1;
                float top = 120 - screenY, bottom = top - 1;
                float u0 = screenX / 256f, u1 = (screenX + 1) / 256f;
                float v0 = screenY / 240f, v1 = (screenY + 1) / 240f;
                int source = sideColorSources != null && sideColorSources.Length == 64
                    ? sideColorSources[local] : local;
                float sideU = (originX + source % 8 + .5f) / 256f;
                float sideV = (originY + source / 8 + .5f) / 240f;
                Vector2 sideUv = new Vector2(sideU, sideV);
                AddQuad(vertices, uv, triangles,
                    new Vector3(left,bottom,frontDepth), new Vector3(right,bottom,frontDepth),
                    new Vector3(left,top,frontDepth), new Vector3(right,top,frontDepth),
                    new Vector2(u0,v1), new Vector2(u1,v1), new Vector2(u0,v0), new Vector2(u1,v0));
                AddQuad(vertices, uv, triangles,
                    new Vector3(right,bottom,backDepth), new Vector3(left,bottom,backDepth),
                    new Vector3(right,top,backDepth), new Vector3(left,top,backDepth),
                    new Vector2(u1,v1), new Vector2(u0,v1), new Vector2(u1,v0), new Vector2(u0,v0));
                if (x == 0 || (mask & (1UL << (local - 1))) == 0)
                    AddSideQuad(vertices, uv, triangles, new Vector3(left,bottom,frontDepth), new Vector3(left,top,frontDepth), new Vector3(left,bottom,backDepth), new Vector3(left,top,backDepth), sideUv);
                if (x == 7 || (mask & (1UL << (local + 1))) == 0)
                    AddSideQuad(vertices, uv, triangles, new Vector3(right,top,frontDepth), new Vector3(right,bottom,frontDepth), new Vector3(right,top,backDepth), new Vector3(right,bottom,backDepth), sideUv);
                if (y == 0 || (mask & (1UL << (local - 8))) == 0)
                    AddSideQuad(vertices, uv, triangles, new Vector3(left,top,frontDepth), new Vector3(right,top,frontDepth), new Vector3(left,top,backDepth), new Vector3(right,top,backDepth), sideUv);
                if (y == 7 || (mask & (1UL << (local + 8))) == 0)
                    AddSideQuad(vertices, uv, triangles, new Vector3(right,bottom,frontDepth), new Vector3(left,bottom,frontDepth), new Vector3(right,bottom,backDepth), new Vector3(left,bottom,backDepth), sideUv);
            }
        }

        public static void AppendPixelFaces(List<Vector3> vertices, List<Vector2> uv, List<int> triangles,
            ulong mask, int originX, int originY, float depth)
        {
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                int local = y * 8 + x;
                if ((mask & (1UL << local)) == 0) continue;
                int screenX = originX + x, screenY = originY + y;
                if (screenX < 0 || screenX >= 256 || screenY < 0 || screenY >= 240) continue;
                float left = screenX - 128, right = left + 1;
                float top = 120 - screenY, bottom = top - 1;
                float u0 = screenX / 256f, u1 = (screenX + 1) / 256f;
                float v0 = screenY / 240f, v1 = (screenY + 1) / 240f;
                AddQuad(vertices, uv, triangles,
                    new Vector3(left, bottom, depth), new Vector3(right, bottom, depth),
                    new Vector3(left, top, depth), new Vector3(right, top, depth),
                    new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u0, v0), new Vector2(u1, v0));
            }
        }

        private static void AddQuad(List<Vector3> vertices, List<Vector2> uv, List<int> triangles,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uva, Vector2 uvb, Vector2 uvc, Vector2 uvd)
        {
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            uv.Add(uva); uv.Add(uvb); uv.Add(uvc); uv.Add(uvd);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
            triangles.Add(start + 2); triangles.Add(start + 3); triangles.Add(start + 1);
        }

        private static void AddSideQuad(List<Vector3> vertices, List<Vector2> uv, List<int> triangles,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 pixelUv)
        {
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(c); vertices.Add(b); vertices.Add(d);
            // Hold the source pixel constant across its entire extrusion wall.
            // Sampling across the full sprite would cross transparent texels
            // and produce alternating colored/transparent stripes from the side.
            uv.Add(pixelUv); uv.Add(pixelUv); uv.Add(pixelUv); uv.Add(pixelUv);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start + 2); triangles.Add(start + 1); triangles.Add(start + 3);
        }

        private static Mesh CreatePrism(string name, Vector2[] ring)
        {
            int count = ring.Length;
            var vertices = new List<Vector3>(count * 4 + 2);
            var uv = new List<Vector2>(count * 4 + 2);
            var triangles = new List<int>(count * 12);
            int frontCenter = vertices.Count; vertices.Add(new Vector3(0, 0, 0)); uv.Add(new Vector2(.5f, .5f));
            int frontStart = vertices.Count;
            for (int i = 0; i < count; i++) { vertices.Add(new Vector3(ring[i].x, ring[i].y, 0)); uv.Add(ring[i] + Vector2.one * .5f); }
            int backCenter = vertices.Count; vertices.Add(new Vector3(0, 0, 1)); uv.Add(new Vector2(.5f, .5f));
            int backStart = vertices.Count;
            for (int i = 0; i < count; i++) { vertices.Add(new Vector3(ring[i].x, ring[i].y, 1)); uv.Add(ring[i] + Vector2.one * .5f); }
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                triangles.Add(frontCenter); triangles.Add(frontStart + next); triangles.Add(frontStart + i);
                triangles.Add(backCenter); triangles.Add(backStart + i); triangles.Add(backStart + next);
                int side = vertices.Count;
                vertices.Add(new Vector3(ring[i].x, ring[i].y, 0)); vertices.Add(new Vector3(ring[next].x, ring[next].y, 0));
                vertices.Add(new Vector3(ring[i].x, ring[i].y, 1)); vertices.Add(new Vector3(ring[next].x, ring[next].y, 1));
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(1, 1));
                triangles.Add(side); triangles.Add(side + 1); triangles.Add(side + 2);
                triangles.Add(side + 1); triangles.Add(side + 3); triangles.Add(side + 2);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
