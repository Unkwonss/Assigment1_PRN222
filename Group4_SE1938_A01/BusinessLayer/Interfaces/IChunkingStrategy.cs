using System.Collections.Generic;

namespace BusinessLayer.Services.Chunking
{
    /// <summary>
    /// Interface cho các chiến lược phân nhỏ văn bản (Chunking).
    /// </summary>
    public interface IChunkingStrategy
    {
        /// <summary>
        /// Tên của chiến lược chunking (VD: Fixed Size, Sentence, Paragraph).
        /// </summary>
        string StrategyName { get; }

        /// <summary>
        /// Phân nhỏ văn bản đầu vào thành danh sách các chuỗi (chunks).
        /// </summary>
        /// <param name="fullText">Văn bản cần phân nhỏ</param>
        /// <returns>Danh sách các chunks</returns>
        List<string> Chunk(string fullText);
    }
}
