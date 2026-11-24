namespace SmartTicketApi.DTOs;

public record LoginRequest(string Email, string Password);

public record AuthResponse(
    string Token,
    int UserId,
    string FullName,
    string Email,
    string Role,
    List<string> Permissions
);

public record CreateTicketRequest(
    string Subject,
    string Description,
    string? Priority,
    int? DepartmentId,
    string? SourceChannel = "WebPortal"
);

public record AddMessageRequest(
    string Body,
    bool IsInternal = false
);

public record UpdateTicketStatusRequest(
    string Status,
    string? ResolutionNote
);

public record AssignTicketRequest(
    int? AssigneeId
);

public record UpdatePriorityRequest(
    string Priority
);

public record LinkParentTicketRequest(
    long ParentTicketId
);

public record SubmitCsatRequest(
    byte Rating,
    string? Feedback
);

public record WebhookTicketRequest(
    string SourceSystem,
    string RequesterEmail,
    string Subject,
    string Description,
    string? Severity
);

public record CreateKbArticleRequest(
    string Category,
    string Title,
    string Tags,
    string Content
);


public record TransferDepartmentRequest(
    int DepartmentId
);

public record CreateUserRequest(
    string FullName,
    string Email,
    string Password,
    int RoleId,
    int? DepartmentId,
    string? Skills
);
