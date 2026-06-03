using System;
using System.Collections.Generic;

namespace Domain.Models;

public partial class ChunkingStrategy
{
    public int StrategyId { get; set; }

    public string StrategyName { get; set; } = null!;

    public virtual ICollection<DocumentIndex> DocumentIndices { get; set; } = new List<DocumentIndex>();

    public virtual ICollection<Experiment> Experiments { get; set; } = new List<Experiment>();
}
