namespace ShoMark.Application.DTOs.Tags;

public record TagDto(Guid Id, string Name, string Slug, DateTime CreatedAt);

public record CreateTagRequest(string Name, string Slug);

public record UpdateTagRequest(string Name, string Slug);
