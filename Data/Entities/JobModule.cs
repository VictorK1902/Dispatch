using System.ComponentModel.DataAnnotations;

namespace Dispatch.Data.Entities;

public class JobModule
{
    public int Id { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
