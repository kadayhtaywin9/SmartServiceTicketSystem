using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTicketApi.Entities;

[Table("roles")]
public class Role
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

[Table("permissions")]
public class Permission
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("slug")]
    public string Slug { get; set; } = string.Empty;

    [Column("module")]
    public string Module { get; set; } = string.Empty;
}

[Table("role_permissions")]
public class RolePermission
{
    [Column("role_id")]
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    [Column("permission_id")]
    public int PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}

[Table("departments")]
public class Department
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("keywords")]
    public string? Keywords { get; set; }

    [Column("lead_user_id")]
    public int? LeadUserId { get; set; }
}

[Table("users")]
public class User
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("role_id")]
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    [Column("department_id")]
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    [Column("full_name")]
    public string FullName { get; set; } = string.Empty;

    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Column("password_hash")]
    public string PasswordHash { get; set; } = string.Empty;

    [Column("skills")]
    public string? Skills { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<UserAsset> Assets { get; set; } = new List<UserAsset>();
}

[Table("user_assets")]
public class UserAsset
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("asset_tag")]
    public string AssetTag { get; set; } = string.Empty;

    [Column("device_model")]
    public string DeviceModel { get; set; } = string.Empty;

    [Column("os_version")]
    public string OsVersion { get; set; } = string.Empty;

    [Column("serial_number")]
    public string SerialNumber { get; set; } = string.Empty;

    [Column("assigned_date")]
    public DateTime AssignedDate { get; set; }
}

[Table("sla_policies")]
public class SlaPolicy
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("priority")]
    public string Priority { get; set; } = "Medium";

    [Column("first_response_mins")]
    public int FirstResponseMins { get; set; }

    [Column("resolution_mins")]
    public int ResolutionMins { get; set; }

    [Column("business_hours_only")]
    public bool BusinessHoursOnly { get; set; } = true;
}

[Table("tickets")]
public class Ticket
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("ticket_code")]
    public string TicketCode { get; set; } = string.Empty;

    [Column("parent_ticket_id")]
    public long? ParentTicketId { get; set; }
    public Ticket? ParentTicket { get; set; }

    public ICollection<Ticket> ChildTickets { get; set; } = new List<Ticket>();

    [Column("requester_id")]
    public int RequesterId { get; set; }
    public User Requester { get; set; } = null!;

    [Column("assignee_id")]
    public int? AssigneeId { get; set; }
    public User? Assignee { get; set; }

    [Column("department_id")]
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;

    [Column("sla_policy_id")]
    public int SlaPolicyId { get; set; }
    public SlaPolicy SlaPolicy { get; set; } = null!;

    [Column("source_channel")]
    public string SourceChannel { get; set; } = "WebPortal";

    [Column("subject")]
    public string Subject { get; set; } = string.Empty;

    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Column("status")]
    public string Status { get; set; } = "Open";

    [Column("priority")]
    public string Priority { get; set; } = "Medium";

    [Column("sentiment_label")]
    public string SentimentLabel { get; set; } = "Calm";

    [Column("sentiment_score")]
    public decimal SentimentScore { get; set; } = 0.10m;

    [Column("ai_reasoning")]
    public string? AiReasoning { get; set; }

    [Column("escalation_level")]
    public int EscalationLevel { get; set; } = 0;

    [Column("first_response_due_at")]
    public DateTime FirstResponseDueAt { get; set; }

    [Column("resolution_due_at")]
    public DateTime ResolutionDueAt { get; set; }

    [Column("first_responded_at")]
    public DateTime? FirstRespondedAt { get; set; }

    [Column("resolved_at")]
    public DateTime? ResolvedAt { get; set; }

    [Column("is_sla_breached")]
    public bool IsSlaBreached { get; set; } = false;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TicketMessage> Messages { get; set; } = new List<TicketMessage>();
    public ICollection<TicketAttachment> Attachments { get; set; } = new List<TicketAttachment>();
    public CsatSurvey? CsatSurvey { get; set; }
}

[Table("ticket_messages")]
public class TicketMessage
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("ticket_id")]
    public long TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;

    [Column("sender_id")]
    public int SenderId { get; set; }
    public User Sender { get; set; } = null!;

    [Column("body")]
    public string Body { get; set; } = string.Empty;

    [Column("is_internal")]
    public bool IsInternal { get; set; } = false;

    [Column("contains_masked_pii")]
    public bool ContainsMaskedPii { get; set; } = false;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("kb_articles")]
public class KbArticle
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("category")]
    public string Category { get; set; } = string.Empty;

    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("tags")]
    public string Tags { get; set; } = string.Empty;

    [Column("content")]
    public string Content { get; set; } = string.Empty;

    [Column("helpful_count")]
    public int HelpfulCount { get; set; } = 0;

    [Column("is_published")]
    public bool IsPublished { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("ticket_audit_logs")]
public class TicketAuditLog
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("ticket_id")]
    public long? TicketId { get; set; }

    [Column("actor_id")]
    public int? ActorId { get; set; }

    [Column("actor_name")]
    public string ActorName { get; set; } = string.Empty;

    [Column("action")]
    public string Action { get; set; } = string.Empty;

    [Column("old_value")]
    public string? OldValue { get; set; }

    [Column("new_value")]
    public string? NewValue { get; set; }

    [Column("ip_address")]
    public string? IpAddress { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("csat_surveys")]
public class CsatSurvey
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("ticket_id")]
    public long TicketId { get; set; }

    [Column("rating")]
    public byte Rating { get; set; }

    [Column("feedback")]
    public string? Feedback { get; set; }

    [Column("submitted_at")]
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}


[Table("ticket_attachments")]
public class TicketAttachment
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("ticket_id")]
    public long TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;

    [Column("uploaded_by")]
    public int UploadedBy { get; set; }
    public User Uploader { get; set; } = null!;

    [Column("file_name")]
    public string FileName { get; set; } = string.Empty;

    [Column("stored_name")]
    public string StoredName { get; set; } = string.Empty;

    [Column("content_type")]
    public string ContentType { get; set; } = "application/octet-stream";

    [Column("file_size")]
    public long FileSize { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
