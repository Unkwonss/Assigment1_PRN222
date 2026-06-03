using System;
using System.Collections.Generic;
using BusinessLayer.Interfaces;

namespace BusinessLayer.Services.Chunking
{
    /// <summary>
    /// Chunking strategy dựa trên dấu kết thúc câu (dấu chấm, chấm hỏi, chấm than).
    /// Đảm bảo không ngắt giữa câu.
    /// </summary>
    public class SentenceChunker : IChunkingStrategy
    {
        public string StrategyName => "Sentence Chunker";

        public List<string> Chunk(string fullText)
        {
            var chunks = new List<string>();
            if (string.IsNullOrEmpty(fullText)) return chunks;

            // Simple split by sentence terminators
            string[] sentences = fullText.Split(new[] { ". ", "? ", "! " }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var sentence in sentences)
            {
                string chunk = sentence.Trim();
                if (!string.IsNullOrEmpty(chunk))
                {
                    // Add back the terminator (approximation)
                    chunks.Add(chunk + ".");
                }
            }

            return chunks;
        }
    }
}
