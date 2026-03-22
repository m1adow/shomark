using ShoMark.Application.Common;
using ShoMark.Application.DTOs.Tags;
using ShoMark.Application.Interfaces;
using ShoMark.Domain.Entities;
using ShoMark.Domain.Interfaces;

namespace ShoMark.Application.Services;

public class TagService : ITagService
{
    private readonly ITagRepository _tagRepository;

    public TagService(ITagRepository tagRepository)
    {
        _tagRepository = tagRepository;
    }

    public async Task<Result<TagDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var tag = await _tagRepository.GetByIdAsync(id, ct);
        if (tag is null)
            return Result<TagDto>.Failure("Tag not found", "NOT_FOUND");

        return Result<TagDto>.Success(MapToDto(tag));
    }

    public async Task<Result<IReadOnlyList<TagDto>>> GetAllAsync(CancellationToken ct = default)
    {
        var tags = await _tagRepository.GetAllAsync(ct);
        return Result<IReadOnlyList<TagDto>>.Success(
            tags.Select(MapToDto).ToList());
    }

    public async Task<Result<TagDto>> CreateAsync(CreateTagRequest request, CancellationToken ct = default)
    {
        var existing = await _tagRepository.GetBySlugAsync(request.Slug, ct);
        if (existing is not null)
            return Result<TagDto>.Failure("A tag with this slug already exists", "DUPLICATE");

        var tag = new Tag { Name = request.Name, Slug = request.Slug };
        var created = await _tagRepository.AddAsync(tag, ct);
        return Result<TagDto>.Success(MapToDto(created));
    }

    public async Task<Result<TagDto>> UpdateAsync(Guid id, UpdateTagRequest request, CancellationToken ct = default)
    {
        var tag = await _tagRepository.GetByIdAsync(id, ct);
        if (tag is null)
            return Result<TagDto>.Failure("Tag not found", "NOT_FOUND");

        tag.Name = request.Name;
        tag.Slug = request.Slug;

        await _tagRepository.UpdateAsync(tag, ct);
        return Result<TagDto>.Success(MapToDto(tag));
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var tag = await _tagRepository.GetByIdAsync(id, ct);
        if (tag is null)
            return Result<bool>.Failure("Tag not found", "NOT_FOUND");

        await _tagRepository.DeleteAsync(id, ct);
        return Result<bool>.Success(true);
    }

    private static TagDto MapToDto(Tag t) => new(t.Id, t.Name, t.Slug, t.CreatedAt);
}
