using System;
using System.Collections.Generic;
using BusinessLayer.Interfaces;

namespace BusinessLayer.Services.Chunking
{
    /// <summary>
    /// Recursive Chunker phân chia văn bản dựa trên nhiều mức độ (Đoạn -> Câu -> Từ)
    /// để đảm bảo kích thước chunk không vượt quá giới hạn nhưng vẫn giữ được ngữ nghĩa.
    /// </summary>
    public class RecursiveChunker : IChunkingStrategy
    {
        private readonly int _maxChunkSize;

        public string StrategyName => $"Recursive Chunker (Max {_maxChunkSize})";

        public RecursiveChunker(int maxChunkSize = 800)
        {
            _maxChunkSize = maxChunkSize;
        }

        public List<string> Chunk(string fullText)
        {
            var chunks = new List<string>();
            if (string.IsNullOrEmpty(fullText)) return chunks;

            RecursiveSplit(fullText, chunks);
            return chunks;
        }

        private void RecursiveSplit(string text, List<string> chunks)
        {
            if (text.Length <= _maxChunkSize)
            {
                chunks.Add(text);
                return;
            }

            // Try to split by paragraph first
            string[] parts = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
            {
                foreach (var part in parts) RecursiveSplit(part, chunks);
                return;
            }

            // If still too large, split by sentence
            parts = text.Split(new[] { ". ", "? ", "! " }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
            {
                foreach (var part in parts)
                {
                    // Adding period back for semantic correctness is tricky here without knowing the exact terminator,
                    // but we approximate.
                    RecursiveSplit(part + ".", chunks); 
                }
                return;
            }

            // If still too large, fallback to fixed size
            int currentIndex = 0;
            while (currentIndex < text.Length)
            {
                int length = Math.Min(_maxChunkSize, text.Length - currentIndex);
                chunks.Add(text.Substring(currentIndex, length));
                currentIndex += length;
            }
        }
    }
}
