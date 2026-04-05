using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Mogify.Api.Models;

[Table("ps_sessions")]
public class PsSessionRecord : BaseModel
{
    [PrimaryKey("id")]
    public string Id { get; set; } = string.Empty;

    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("messages")]
    public string Messages { get; set; } = "[]";
}
