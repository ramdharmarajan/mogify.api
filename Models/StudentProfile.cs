using System.Text.Json.Serialization;

namespace Mogify.Api.Models;

public class StudentProfile
{
    [JsonPropertyName("predicted_grades")]
    public string? PredictedGrades { get; set; }

    [JsonPropertyName("target_subject")]
    public string? TargetSubject { get; set; }

    [JsonPropertyName("school_type")]
    public string? SchoolType { get; set; }

    [JsonPropertyName("location_preference")]
    public string? LocationPreference { get; set; }

    [JsonPropertyName("admissions_test_score")]
    public string? AdmissionsTestScore { get; set; }

    [JsonPropertyName("key_experiences")]
    public string? KeyExperiences { get; set; }

    [JsonPropertyName("ucas_choices")]
    public List<UcasChoice>? UcasChoices { get; set; }
}

public class UcasChoice
{
    [JsonPropertyName("uni_slug")]
    public string UniversitySlug { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("offer")]
    public string? Offer { get; set; }
}
