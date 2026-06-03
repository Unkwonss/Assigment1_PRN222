using System;
using System.Collections.Generic;
using BusinessLayer.Interfaces;

namespace BusinessLayer.Services.Chunking
{
    /// <summary>
    /// Chunking strategy dựa trên đoạn văn (chia theo dấu xuống dòng kép).
    /// </summary>
    public class ParagraphChunker : IChunkingStrategy
    {
        public string StrategyName => "Paragraph Chunker";

        public List<string> Chunk(string fullText)
        {
            var chunks = new List<string>();
            if (string.IsNullOrEmpty(fullText)) return chunks;

            string[] paragraphs = fullText.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var para in paragraphs)
            {
                string chunk = para.Trim();
                if (!string.IsNullOrEmpty(chunk))
                {
                    chunks.Add(chunk);
                }
            }

            return chunks;
        }
    }
}
