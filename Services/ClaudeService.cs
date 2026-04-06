using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Mogify.Api.Controllers;
using Mogify.Api.Models;
using Mogify.Api.Prompts;
using System.Text.Json;

namespace Mogify.Api.Services;

public class ClaudeService
{
    private const string Model = "claude-sonnet-4-20250514";
    private readonly AnthropicClient _client;

    public ClaudeService(AnthropicClient client)
    {
        _client = client;
    }

    public async Task<List<object>> EnhanceShortlistAsync(
        List<ShortlistItem> shortlist,
        string careerGoals,
        string interests,
        StudentProfile profile)
    {
        var universitiesList = string.Join("\n", shortlist.Select((u, i) =>
            $"{i + 1}. {u.Name} (slug: {u.Slug}, current fit score: {u.FitScore}, type: {u.RecommendationType}, offer: {u.TypicalOffer})"));

        var systemPrompt = $"""
            You are a UK university admissions expert helping a student find their best-fit universities.
            The student's grades and location have already been used to produce an initial ranked list.
            Your job is to rewrite the reasoning for each university to explain specifically how it
            connects to the student's career goals and interests. Be concrete and specific — name
            actual programmes, societies, alumni outcomes, or research strengths that align with
            what the student wants to do. Keep each reasoning to 2 sentences maximum.
            Also adjust fit_score by up to ±10 if the career goals strongly align or clash with
            what that university is known for.

            Student profile:
            - Predicted grades: {profile.PredictedGrades}
            - Subject: {profile.TargetSubject}
            - Career goals: {careerGoals}
            - Interests: {interests}

            Return a JSON array with one object per university in the same order, each with:
            slug, fit_score (adjusted), reasoning (career-personalised).
            Return only valid JSON, no explanation.
            """;

        var userMsg = $"Universities to personalise:\n{universitiesList}";

        var response = await _client.Messages.GetClaudeMessageAsync(new MessageParameters
        {
            Model = Model,
            MaxTokens = 1500,
            System = [new SystemMessage(systemPrompt)],
            Messages = [new Message(RoleType.User, userMsg)]
        });

        var json = response.Message.ToString().Trim();
        if (json.StartsWith("```"))
            json = System.Text.RegularExpressions.Regex.Replace(json, @"^```[^\n]*\n?", "").TrimEnd('`').Trim();

        try
        {
            var enhancements = Newtonsoft.Json.JsonConvert.DeserializeObject<List<dynamic>>(json)!;
            var enhancementBySlug = enhancements.ToDictionary(
                e => (string)e.slug,
                e => e);

            return shortlist.Select(u =>
            {
                if (enhancementBySlug.TryGetValue(u.Slug, out var enhancement))
                {
                    return (object)new
                    {
                        slug               = u.Slug,
                        name               = u.Name,
                        location           = u.Location,
                        fit_score          = (int)enhancement.fit_score,
                        recommendation_type = u.RecommendationType,
                        reasoning          = (string)enhancement.reasoning,
                        typical_offer      = u.TypicalOffer,
                        admissions_test    = u.AdmissionsTest
                    };
                }
                // Fall back to original if Claude didn't return this university
                return (object)new
                {
                    slug               = u.Slug,
                    name               = u.Name,
                    location           = u.Location,
                    fit_score          = u.FitScore,
                    recommendation_type = u.RecommendationType,
                    reasoning          = u.Reasoning,
                    typical_offer      = u.TypicalOffer,
                    admissions_test    = u.AdmissionsTest
                };
            }).ToList();
        }
        catch
        {
            // If Claude returns bad JSON, return original list untouched
            return shortlist.Select(u => (object)new
            {
                slug               = u.Slug,
                name               = u.Name,
                location           = u.Location,
                fit_score          = u.FitScore,
                recommendation_type = u.RecommendationType,
                reasoning          = u.Reasoning,
                typical_offer      = u.TypicalOffer,
                admissions_test    = u.AdmissionsTest
            }).ToList();
        }
    }

    public async Task<string> SendPsCoachMessageAsync(
        StudentProfile profile,
        List<string> targetUniversities,
        List<PsMessage> history,
        string userMessage)
    {
        var profileJson = JsonSerializer.Serialize(profile);
        var universitiesStr = string.Join(", ", targetUniversities);
        var systemPrompt = SystemPrompts.PsCoach(profileJson, universitiesStr, profile.TargetSubject ?? "");

        var messages = history
            .Select(m => new Message(
                m.Role == "user" ? RoleType.User : RoleType.Assistant,
                m.Content))
            .ToList();
        messages.Add(new Message(RoleType.User, userMessage));

        var response = await _client.Messages.GetClaudeMessageAsync(new MessageParameters
        {
            Model = Model,
            MaxTokens = 1000,
            System = [new SystemMessage(systemPrompt)],
            Messages = messages
        });

        return response.Message.ToString();
    }

    public async Task<string> GetInterviewFeedbackAsync(
        string universitySlug,
        string subject,
        string interviewFormat,
        List<InterviewQuestion> questions,
        string question,
        string answer)
    {
        var questionsJson = JsonSerializer.Serialize(questions.Select(q => new
        {
            q.Question,
            q.WhatIsBeingTested,
            q.StrongApproach,
            q.CommonMistakes
        }));
        var systemPrompt = SystemPrompts.InterviewCoach(interviewFormat, universitySlug, subject, questionsJson);

        var userMsg = $"Question asked: {question}\n\nStudent's answer: {answer}\n\nPlease provide examiner-style feedback with a score out of 10.";

        var response = await _client.Messages.GetClaudeMessageAsync(new MessageParameters
        {
            Model = Model,
            MaxTokens = 1000,
            System = [new SystemMessage(systemPrompt)],
            Messages = [new Message(RoleType.User, userMsg)]
        });

        return response.Message.ToString();
    }

    public async Task<string> GeneratePsDraftAsync(StudentProfile profile, List<PsSession> sessions)
    {
        var profileJson = JsonSerializer.Serialize(profile);
        var transcript = string.Join("\n\n---\n\n", sessions.SelectMany(s =>
            s.Messages.Select(m => $"{m.Role.ToUpper()}: {m.Content}")));

        var systemPrompt = $"""
            You are an expert UK university personal statement editor.
            Given a student profile and coaching session transcripts, write a compelling
            personal statement in the student's own voice for the 2026 UCAS format
            (three questions: why this subject, super-curricular activities, skills and achievements).
            Student profile: {profileJson}
            """;

        var userMsg = $"Coaching session transcripts:\n\n{transcript}\n\nPlease write the personal statement draft.";

        var response = await _client.Messages.GetClaudeMessageAsync(new MessageParameters
        {
            Model = Model,
            MaxTokens = 2000,
            System = [new SystemMessage(systemPrompt)],
            Messages = [new Message(RoleType.User, userMsg)]
        });

        return response.Message.ToString();
    }

    public async Task<string> GetPsFeedbackAsync(string draft)
    {
        var systemPrompt = """
            You are an expert UK university personal statement coach.
            Give detailed, structured feedback on the personal statement draft.
            Comment on: opening impact, subject passion, academic depth, extra-curriculars,
            writing quality, and alignment with Russell Group expectations.
            Be specific with suggestions for improvement.
            """;

        var response = await _client.Messages.GetClaudeMessageAsync(new MessageParameters
        {
            Model = Model,
            MaxTokens = 1500,
            System = [new SystemMessage(systemPrompt)],
            Messages = [new Message(RoleType.User, $"Please review this personal statement:\n\n{draft}")]
        });

        return response.Message.ToString();
    }

    public async Task<string> GetInterviewSessionSummaryAsync(InterviewSession session)
    {
        var turns = session.Turns.Where(t => t.Answer != null).ToList();
        var avgScore = turns.Where(t => t.Score.HasValue).Select(t => t.Score!.Value).DefaultIfEmpty(0).Average();

        var systemPrompt = "You are an expert interview coach. Summarise this mock interview session with overall performance, key strengths, and top 3 areas to improve.";

        var content = string.Join("\n\n", turns.Select(t =>
            $"Q: {t.Question}\nA: {t.Answer}\nFeedback: {t.Feedback}\nScore: {t.Score}/10"));

        var userMsg = $"Interview session for {session.UniversitySlug} {session.Subject}:\n\n{content}\n\nAverage score: {avgScore:F1}/10";

        var response = await _client.Messages.GetClaudeMessageAsync(new MessageParameters
        {
            Model = Model,
            MaxTokens = 1000,
            System = [new SystemMessage(systemPrompt)],
            Messages = [new Message(RoleType.User, userMsg)]
        });

        return response.Message.ToString();
    }
}
