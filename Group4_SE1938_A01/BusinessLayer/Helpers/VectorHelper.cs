using System;

namespace BusinessLayer.Helpers
{
    public static class VectorHelper
    {
        public static float CosineSimilarity(float[] vectorA, float[] vectorB)
        {
            if (vectorA.Length == 0 || vectorB.Length == 0) return 0f;
            if (vectorA.Length != vectorB.Length) return 0f;

            float dotProduct = 0f;
            float magnitudeA = 0f;
            float magnitudeB = 0f;

            for (int i = 0; i < vectorA.Length; i++)
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

