using System;
using System.Collections.Generic;

namespace Domain.Models;

public partial class Aimodel
{
    public int ModelId { get; set; }

    public string ModelName { get; set; } = null!;

    public string ModelType { get; set; } = null!;

    public virtual ICollection<Experiment> Experiments { get; set; } = new List<Experiment>();
}
