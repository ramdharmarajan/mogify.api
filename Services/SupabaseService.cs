using Mogify.Api.Models;
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

    public async Task<Scholarship?> GetScholarshipAsync(int id)
    {
        var result = await _client.From<Scholarship>()
            .Where(s => s.Id == id)
            .Single();
        return result;
    }
}
