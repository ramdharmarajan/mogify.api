using Mogify.Api.Models;
using System.Collections.Concurrent;

namespace Mogify.Api.Services;

/// <summary>
/// In-memory session store for PS and interview sessions.
/// Replace with Supabase persistence before launch.
/// </summary>
public class SessionStore
{
    private readonly ConcurrentDictionary<string, PsSession> _psSessions = new();
    private readonly ConcurrentDictionary<string, InterviewSession> _interviewSessions = new();

    // PS Sessions
    public PsSession CreatePsSession(string userId)
    {
        var session = new PsSession { UserId = userId };
        _psSessions[session.Id] = session;
        return session;
    }

    public PsSession? GetPsSession(string id) =>
        _psSessions.TryGetValue(id, out var session) ? session : null;

    public List<PsSession> GetPsSessionsForUser(string userId) =>
        _psSessions.Values.Where(s => s.UserId == userId).OrderByDescending(s => s.CreatedAt).ToList();

    public void AddPsMessage(string sessionId, PsMessage message)
    {
        if (_psSessions.TryGetValue(sessionId, out var session))
            session.Messages.Add(message);
    }

    // Interview Sessions
    public InterviewSession CreateInterviewSession(string userId, string universitySlug, string subject)
    {
        var session = new InterviewSession
        {
            UserId = userId,
            UniversitySlug = universitySlug,
            Subject = subject
        };
        _interviewSessions[session.Id] = session;
        return session;
    }

    public InterviewSession? GetInterviewSession(string id) =>
        _interviewSessions.TryGetValue(id, out var session) ? session : null;
}
