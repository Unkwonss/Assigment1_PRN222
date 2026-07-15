using System;
using System.Collections.Generic;
using BusinessLayer.Interfaces;

namespace BusinessLayer.Services.Chunking
{
    /// <summary>
    /// Chunking strategy dựa trên kích thước cố định (số ký tự) với độ chồng lấp (overlap).
    /// Optimized: pre-allocate list capacity, uses Substring for minimal allocations.
    /// </summary>
    public class FixedSizeChunker : IChunkingStrategy
    {
        private readonly int _chunkSize;
        private readonly int _overlap;

        public string StrategyName => $"Fixed Size ({_chunkSize} chars, {_overlap} overlap)";

        public FixedSizeChunker(int chunkSize = 500, int overlap = 100)
        {
            if (chunkSize <= 0) throw new ArgumentException("Chunk size must be greater than 0");
            if (overlap < 0 || overlap >= chunkSize) throw new ArgumentException("Overlap must be >= 0 and < chunkSize");
            
            _chunkSize = chunkSize;
            _overlap = overlap;
        }

        public List<string> Chunk(string fullText)
        {
            if (string.IsNullOrEmpty(fullText)) return new List<string>();

            int step = _chunkSize - _overlap;
            // Pre-allocate: estimate number of chunks to avoid list resizing
            int estimatedCount = (fullText.Length / step) + 1;
            var chunks = new List<string>(estimatedCount);

            int currentIndex = 0;
            while (currentIndex < fullText.Length)
            {
                int length = Math.Min(_chunkSize, fullText.Length - currentIndex);
                chunks.Add(fullText.Substring(currentIndex, length));

                // Move forward, but go back by overlap
                currentIndex += step;
            }

            return chunks;
        }
    }
}
