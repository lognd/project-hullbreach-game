using System;

namespace Hullbreach.Structure
{
    // The 8-node serendipity quadrilateral, and its unit stiffness matrix;
    // see docs/reference/hullbreach-structure.md#q8element for why.
    // frob:doc docs/reference/hullbreach-structure.md#q8element
    public static class Q8Element
    {
        // frob:doc docs/reference/hullbreach-structure.md#q8element
        public const int NodeCount = 8;
        // frob:doc docs/reference/hullbreach-structure.md#q8element
        public const int DofCount = 16;   // 8 nodes x 2 dof

        // Tests use these to build rigid-body modes.
        // frob:doc docs/reference/hullbreach-structure.md#q8element
        public static readonly float[,] ReferenceNodes =
        {
            { -1f, -1f }, {  1f, -1f }, {  1f,  1f }, { -1f,  1f },   // corners
            {  0f, -1f }, {  1f,  0f }, {  0f,  1f }, { -1f,  0f },   // midsides
        };

        // Keyed by (class, h) though h is always 1 in this game; the API
        // still takes h to stay general.
        static readonly System.Collections.Generic.Dictionary<(byte, float), float[,]> KHatCache
            = new System.Collections.Generic.Dictionary<(byte, float), float[,]>();

        // Quantized because KHat is not linear in nu, so a continuous nu
        // would defeat the precomputation.
        // frob:doc docs/reference/hullbreach-structure.md#q8element
        public static float NuFor(byte poissonClass)
        {
            switch (poissonClass)
            {
                case 0: return 0.30f;
                case 1: return 0.25f;
                case 2: return 0.35f;
                default: throw new ArgumentOutOfRangeException(nameof(poissonClass));
            }
        }

        // Computed once and reused for every block sharing that class.
        // frob:doc docs/reference/hullbreach-structure.md#q8element
        public static float[,] KHatFor(byte poissonClass, float h)
        {
            var key = (poissonClass, h);
            if (!KHatCache.TryGetValue(key, out var k))
            {
                k = new float[DofCount, DofCount];
                UnitStiffness(NuFor(poissonClass), h, k);
                KHatCache[key] = k;
            }
            return k;
        }

        // frob:doc docs/reference/hullbreach-structure.md#q8element
        public static void ShapeFunctions(float xi, float eta, float[] into)
        {
            for (int i = 0; i < 4; i++)
            {
                float xi_i = ReferenceNodes[i, 0];
                float eta_i = ReferenceNodes[i, 1];
                into[i] = 0.25f * (1f + xi * xi_i) * (1f + eta * eta_i) * (xi * xi_i + eta * eta_i - 1f);
            }

            for (int i = 4; i < 8; i++)
            {
                float xi_i = ReferenceNodes[i, 0];
                float eta_i = ReferenceNodes[i, 1];
                if (xi_i == 0f)
                {
                    into[i] = 0.5f * (1f - xi * xi) * (1f + eta * eta_i);
                }
                else
                {
                    into[i] = 0.5f * (1f + xi * xi_i) * (1f - eta * eta);
                }
            }
        }

        // frob:doc docs/reference/hullbreach-structure.md#q8element
        public static void ShapeDerivatives(float xi, float eta, float[] dNdXi, float[] dNdEta)
        {
            for (int i = 0; i < 4; i++)
            {
                float xi_i = ReferenceNodes[i, 0];
                float eta_i = ReferenceNodes[i, 1];
                dNdXi[i] = 0.25f * xi_i * (1f + eta * eta_i) * (2f * xi * xi_i + eta * eta_i);
                dNdEta[i] = 0.25f * eta_i * (1f + xi * xi_i) * (xi * xi_i + 2f * eta * eta_i);
            }

            for (int i = 4; i < 8; i++)
            {
                float xi_i = ReferenceNodes[i, 0];
                float eta_i = ReferenceNodes[i, 1];
                if (xi_i == 0f)
                {
                    dNdXi[i] = -xi * (1f + eta * eta_i);
                    dNdEta[i] = 0.5f * (1f - xi * xi) * eta_i;
                }
                else
                {
                    dNdXi[i] = 0.5f * xi_i * (1f - eta * eta);
                    dNdEta[i] = -eta * (1f + xi * xi_i);
                }
            }
        }

        // frob:doc docs/reference/hullbreach-structure.md#q8element
        public static void StrainDisplacement(float xi, float eta, float h, float[,] b)
        {
            var dNdXi = new float[NodeCount];
            var dNdEta = new float[NodeCount];
            ShapeDerivatives(xi, eta, dNdXi, dNdEta);

            // J = (h/2) * I on a uniform axis-aligned grid, so dN/dx and
            // dN/dy fall straight out of dN/dxi, dN/deta by a scalar.
            float scale = 2f / h;

            for (int i = 0; i < NodeCount; i++)
            {
                float dNdx = scale * dNdXi[i];
                float dNdy = scale * dNdEta[i];

                int cx = 2 * i;
                int cy = 2 * i + 1;

                b[0, cx] = dNdx; b[0, cy] = 0f;
                b[1, cx] = 0f; b[1, cy] = dNdy;
                b[2, cx] = dNdy; b[2, cy] = dNdx;
            }
        }

        // D = E * ConstitutiveUnit(nu).
        // frob:doc docs/reference/hullbreach-structure.md#q8element
        public static void ConstitutiveUnit(float nu, float[,] dHat)
        {
            float factor = 1f / (1f - nu * nu);
            dHat[0, 0] = factor; dHat[0, 1] = factor * nu; dHat[0, 2] = 0f;
            dHat[1, 0] = factor * nu; dHat[1, 1] = factor; dHat[1, 2] = 0f;
            dHat[2, 0] = 0f; dHat[2, 1] = 0f; dHat[2, 2] = factor * (1f - nu) / 2f;
        }

        // KHat = integral of B^T DHat B over the element, by 3x3 Gauss.
        // frob:doc docs/reference/hullbreach-structure.md#q8element
        public static void UnitStiffness(float nu, float h, float[,] kHat)
        {
            for (int i = 0; i < DofCount; i++)
            for (int j = 0; j < DofCount; j++)
                kHat[i, j] = 0f;

            var dHat = new float[3, 3];
            ConstitutiveUnit(nu, dHat);

            // 3x3 Gauss: 2x2 (reduced) would admit a spurious zero-energy
            // mode; precomputed once so the extra cost is irrelevant.
            float[] pts = { -(float)Math.Sqrt(3.0 / 5.0), 0f, (float)Math.Sqrt(3.0 / 5.0) };
            float[] wts = { 5f / 9f, 8f / 9f, 5f / 9f };

            // det J = (h/2)^2 for a uniform axis-aligned block.
            float detJ = (h / 2f) * (h / 2f);

            var b = new float[3, DofCount];
            var db = new float[3, DofCount]; // DHat * B

            for (int gi = 0; gi < 3; gi++)
            for (int gj = 0; gj < 3; gj++)
            {
                float xi = pts[gi];
                float eta = pts[gj];
                float w = wts[gi] * wts[gj] * detJ;

                StrainDisplacement(xi, eta, h, b);

                for (int r = 0; r < 3; r++)
                for (int c = 0; c < DofCount; c++)
                {
                    float sum = 0f;
                    for (int k = 0; k < 3; k++)
                        sum += dHat[r, k] * b[k, c];
                    db[r, c] = sum;
                }

                for (int i = 0; i < DofCount; i++)
                for (int j = 0; j < DofCount; j++)
                {
                    float sum = 0f;
                    for (int k = 0; k < 3; k++)
                        sum += b[k, i] * db[k, j];
                    kHat[i, j] += sum * w;
                }
            }
        }
    }
}
