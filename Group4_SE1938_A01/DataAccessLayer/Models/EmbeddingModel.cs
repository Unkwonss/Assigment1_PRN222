using System;
using System.Collections.Generic;

namespace Domain.Models;

public partial class EmbeddingModel
{
    public int ModelId { get; set; }

    public string ModelName { get; set; } = null!;

    public string Provider { get; set; } = null!;

    public virtual ICollection<DocumentIndex> DocumentIndices { get; set; } = new List<DocumentIndex>();

    public virtual ICollection<Experiment> Experiments { get; set; } = new List<Experiment>();
}
