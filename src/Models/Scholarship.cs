using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Mogify.Api.Models;

[Table("scholarships")]
public class Scholarship : BaseModel
{
    [PrimaryKey("id")]
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("provider")]
    public string? Provider { get; set; }

    [Column("value")]
    public string? Value { get; set; }

    [Column("eligibility")]
    public string? Eligibility { get; set; }

    [Column("deadline")]
    public string? Deadline { get; set; }

    [Column("competitiveness")]
    public string? Competitiveness { get; set; }

    [Column("tips")]
    public string? Tips { get; set; }

    [Column("url")]
    public string? Url { get; set; }

    [Column("subjects")]
    public string[]? Subjects { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}
