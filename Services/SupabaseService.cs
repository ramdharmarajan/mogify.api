using Mogify.Api.Models;
using Newtonsoft.Json;
using Supabase;

namespace Mogify.Api.Services;

public class SupabaseService
{
    private readonly Client _client;

    public SupabaseService(Client client)
    {
        _client = client;
    }

    public async Task<List<University>> GetUniversitiesAsync()
    {
        var result = await _client.From<University>().Get();
        return result.Models;
    }

    public async Task<University?> GetUniversityAsync(string slug)
    {
        var result = await _client.From<University>()
            .Where(u => u.Slug == slug)
            .Single();
        return result;
    }

    public async Task<List<Course>> GetCoursesForSubjectAsync(string subject)
    {
        var result = await _client.From<Course>()
            .Where(c => c.Subject == subject)
            .Get();
        return result.Models;
    }

    public async Task<Course?> GetCourseAsync(string universitySlug, string subject)
    {
        var result = await _client.From<Course>()
            .Where(c => c.UniversitySlug == universitySlug && c.Subject == subject)
            .Single();
        return result;
    }

    public async Task<List<InterviewQuestion>> GetInterviewQuestionsAsync(string universitySlug, string subject)
    {
        var result = await _client.From<InterviewQuestion>()
            .Where(q => q.UniversitySlug == universitySlug && q.Subject == subject)
            .Get();
        return result.Models;
    }

    public async Task<List<Scholarship>> GetScholarshipsAsync()
    {
        var result = await _client.From<Scholarship>().Get();
        return result.Models;
    }

    public async Task<Scholarship?> GetScholarshipAsync(string id)
    {
        var result = await _client.From<Scholarship>()
            .Where(s => s.Id == id)
            .Single();
        return result;
    }

    // ── PS Sessions ────────────────────────────────────────────────────────────

    public async Task<PsSession> CreatePsSessionAsync(string userId)
    {
        var record = new PsSessionRecord { UserId = userId };
        var result = await _client.From<PsSessionRecord>().Insert(record);
        return ToSession(result.Models.First());
    }

    public async Task<PsSession?> GetPsSessionAsync(string id)
    {
        var result = await _client.From<PsSessionRecord>()
            .Where(s => s.Id == id)
            .Single();
        return result == null ? null : ToSession(result);
    }

    public async Task<List<PsSession>> GetPsSessionsForUserAsync(string userId)
    {
        var result = await _client.From<PsSessionRecord>()
            .Where(s => s.UserId == userId)
            .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
            .Get();
        return result.Models.Select(ToSession).ToList();
    }

    public async Task SavePsSessionMessagesAsync(string sessionId, List<PsMessage> messages)
    {
        var json = JsonConvert.SerializeObject(messages);
        var record = new PsSessionRecord { Id = sessionId, Messages = json };
        await _client.From<PsSessionRecord>()
            .Where(s => s.Id == sessionId)
            .Update(record);
    }

    private static PsSession ToSession(PsSessionRecord r) => new()
    {
        Id = r.Id,
        UserId = r.UserId,
        CreatedAt = r.CreatedAt,
        Messages = JsonConvert.DeserializeObject<List<PsMessage>>(r.Messages) ?? []
    };

    // ── Interview Sessions ─────────────────────────────────────────────────────

    public async Task<InterviewSession> CreateInterviewSessionAsync(string userId, string universitySlug, string subject)
    {
        var record = new InterviewSessionRecord
        {
            UserId = userId,
            UniversitySlug = universitySlug,
            Subject = subject
        };
        var result = await _client.From<InterviewSessionRecord>().Insert(record);
        return ToInterviewSession(result.Models.First());
    }

    public async Task<InterviewSession?> GetInterviewSessionAsync(string id)
    {
        var result = await _client.From<InterviewSessionRecord>()
            .Where(s => s.Id == id)
            .Single();
        return result == null ? null : ToInterviewSession(result);
    }

    public async Task SaveInterviewTurnsAsync(string sessionId, List<InterviewTurn> turns)
    {
        var json = JsonConvert.SerializeObject(turns);
        var record = new InterviewSessionRecord { Id = sessionId, Turns = json };
        await _client.From<InterviewSessionRecord>()
            .Where(s => s.Id == sessionId)
            .Update(record);
    }

    private static InterviewSession ToInterviewSession(InterviewSessionRecord r) => new()
    {
        Id = r.Id,
        UserId = r.UserId,
        UniversitySlug = r.UniversitySlug,
        Subject = r.Subject,
        CreatedAt = r.CreatedAt,
        Turns = JsonConvert.DeserializeObject<List<InterviewTurn>>(r.Turns) ?? []
    };
}
