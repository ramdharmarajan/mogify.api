using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Mogify.Api.Models;

[Table("courses")]
public class Course : BaseModel
{
    [PrimaryKey("id")]
    public string Id { get; set; } = string.Empty;

    [Column("university_slug")]
    public string UniversitySlug { get; set; } = string.Empty;

    [Column("subject")]
    public string Subject { get; set; } = string.Empty;

    [Column("ucas_codes")]
    public string[]? UcasCodes { get; set; }

    [Column("typical_offer")]
    public string? TypicalOffer { get; set; }

    [Column("entry_requirements")]
    public string? EntryRequirements { get; set; }

    [Column("admissions_test")]
    public string? AdmissionsTest { get; set; }

    [Column("admissions_test_notes")]
    public string? AdmissionsTestNotes { get; set; }

    [Column("interview_format")]
    public string? InterviewFormat { get; set; }

    [Column("interview_format_detail")]
    public string? InterviewFormatDetail { get; set; }

    [Column("interview_style")]
    public string? InterviewStyle { get; set; }

    [Column("ps_guidance")]
    public string? PsGuidance { get; set; }

    [Column("ps_focus_area")]
    public string? PsFocusArea { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}
