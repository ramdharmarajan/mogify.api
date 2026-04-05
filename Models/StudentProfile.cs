using Newtonsoft.Json;

namespace Mogify.Api.Models;

public class StudentProfile
{
    [JsonProperty("predicted_grades")]
    public string? PredictedGrades { get; set; }

    [JsonProperty("target_subject")]
    public string? TargetSubject { get; set; }

    [JsonProperty("school_type")]
    public string? SchoolType { get; set; }

    [JsonProperty("location_preference")]
    public string? LocationPreference { get; set; }

    [JsonProperty("admissions_test_score")]
    public string? AdmissionsTestScore { get; set; }

    [JsonProperty("key_experiences")]
    public string? KeyExperiences { get; set; }

    [JsonProperty("ucas_choices")]
    public List<UcasChoice>? UcasChoices { get; set; }
}

public class UcasChoice
{
    [JsonProperty("uni_slug")]
    public string UniversitySlug { get; set; } = string.Empty;

    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty("offer")]
    public string? Offer { get; set; }
}
