using Newtonsoft.Json;

namespace Mogify.Api.Models;

public class InterviewSession
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonProperty("university_slug")]
    public string UniversitySlug { get; set; } = string.Empty;

    [JsonProperty("subject")]
    public string Subject { get; set; } = string.Empty;

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonProperty("turns")]
    public List<InterviewTurn> Turns { get; set; } = [];
}

public class InterviewTurn
{
    [JsonProperty("question")]
    public string Question { get; set; } = string.Empty;

    [JsonProperty("answer")]
    public string? Answer { get; set; }

    [JsonProperty("feedback")]
    public string? Feedback { get; set; }

    [JsonProperty("score")]
    public int? Score { get; set; }
}
