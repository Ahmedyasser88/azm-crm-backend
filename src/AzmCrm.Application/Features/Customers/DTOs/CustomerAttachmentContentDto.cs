namespace AzmCrm.Application.Features.Customers.DTOs;

public sealed record CustomerAttachmentContentDto(Stream Content, string ContentType, string FileName);
