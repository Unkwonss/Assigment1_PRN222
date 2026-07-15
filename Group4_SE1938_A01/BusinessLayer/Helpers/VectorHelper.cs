using System;
using System.Numerics;

namespace BusinessLayer.Helpers
{
    public static class VectorHelper
    {
        /// <summary>
        /// SIMD-accelerated cosine similarity.
        /// Uses System.Numerics.Vector&lt;float&gt; to process multiple floats per CPU cycle.
        /// Fallback: scalar loop for the tail elements that don't fill a SIMD lane.
        /// </summary>
        public static float CosineSimilarity(float[] vectorA, float[] vectorB)
        {
            if (vectorA.Length == 0 || vectorB.Length == 0) return 0f;
            if (vectorA.Length != vectorB.Length) return 0f;

            int length = vectorA.Length;

            // Use SIMD if available and vector is large enough
            if (Vector.IsHardwareAccelerated && length >= Vector<float>.Count)
            {
                return CosineSimilaritySimd(vectorA, vectorB, length);
            }

            return CosineSimilarityScalar(vectorA, vectorB, length);
        }

        private static float CosineSimilaritySimd(float[] vectorA, float[] vectorB, int length)
        {
            int simdLength = Vector<float>.Count;
            int simdBound = length - (length % simdLength);

            var sumDot = Vector<float>.Zero;
            var sumA = Vector<float>.Zero;
            var sumB = Vector<float>.Zero;

            for (int i = 0; i < simdBound; i += simdLength)
            {
                var va = new Vector<float>(vectorA, i);
                var vb = new Vector<float>(vectorB, i);
                sumDot += va * vb;
                sumA += va * va;
                sumB += vb * vb;
            }

            // Reduce SIMD vectors to scalars
            float dotProduct = 0f, magnitudeA = 0f, magnitudeB = 0f;
            for (int i = 0; i < simdLength; i++)
            {
                dotProduct += sumDot[i];
                magnitudeA += sumA[i];
                magnitudeB += sumB[i];
            }

            // Handle tail elements
            for (int i = simdBound; i < length; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                magnitudeA += vectorA[i] * vectorA[i];
                magnitudeB += vectorB[i] * vectorB[i];
            }

            float magnitude = MathF.Sqrt(magnitudeA) * MathF.Sqrt(magnitudeB);
            return magnitude == 0f ? 0f : dotProduct / magnitude;
        }

        private static float CosineSimilarityScalar(float[] vectorA, float[] vectorB, int length)
        {
            float dotProduct = 0f;
            float magnitudeA = 0f;
            float magnitudeB = 0f;

            for (int i = 0; i < length; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                magnitudeA += vectorA[i] * vectorA[i];
                magnitudeB += vectorB[i] * vectorB[i];
            }

            float magnitude = MathF.Sqrt(magnitudeA) * MathF.Sqrt(magnitudeB);
            return magnitude == 0f ? 0f : dotProduct / magnitude;
        }
    }
}
