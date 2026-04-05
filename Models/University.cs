using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Mogify.Api.Models;

[Table("universities")]
public class University : BaseModel
{
    [PrimaryKey("id")]
    public string Id { get; set; } = string.Empty;

    [Column("slug")]
    public string Slug { get; set; } = string.Empty;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("location")]
    public string? Location { get; set; }

    [Column("type")]
    public string? Type { get; set; }

    [Column("character")]
    public string? Character { get; set; }

    [Column("what_they_look_for")]
    public string? WhatTheyLookFor { get; set; }

    [Column("red_flags")]
    public string? RedFlags { get; set; }

    [Column("acceptance_rate")]
    public decimal? AcceptanceRate { get; set; }

    [Column("contextual_admissions")]
    public bool? ContextualAdmissions { get; set; }

    [Column("contextual_notes")]
    public string? ContextualNotes { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}
