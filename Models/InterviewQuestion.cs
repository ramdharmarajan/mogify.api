using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Mogify.Api.Models;

[Table("interview_questions")]
public class InterviewQuestion : BaseModel
{
    [PrimaryKey("id")]
    public string Id { get; set; } = string.Empty;

    [Column("university_slug")]
    public string UniversitySlug { get; set; } = string.Empty;

    [Column("subject")]
    public string Subject { get; set; } = string.Empty;

    [Column("question")]
    public string Question { get; set; } = string.Empty;

    [Column("type")]
    public string? Type { get; set; }

    [Column("difficulty")]
    public string? Difficulty { get; set; }

    [Column("what_is_being_tested")]
    public string? WhatIsBeingTested { get; set; }

    [Column("strong_approach")]
    public string? StrongApproach { get; set; }

    [Column("common_mistakes")]
    public string? CommonMistakes { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}
