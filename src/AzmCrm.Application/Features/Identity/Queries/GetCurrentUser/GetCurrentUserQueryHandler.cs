using AzmCrm.Application.Features.Identity.DTOs;
using AzmCrm.Application.Localization;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Identity.Queries.GetCurrentUser;

internal sealed class GetCurrentUserQueryHandler(
    ICurrentUserService currentUserService,
    IIdentityService identityService,
    ILocalizationService localization)
    : IRequestHandler<GetCurrentUserQuery, Result<CurrentUserDto>>
{
    public async Task<Result<CurrentUserDto>> Handle(GetCurrentUserQuery request, CancellationToken ct)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId == null)
        {
            return Result<CurrentUserDto>.Failure(localization[LocalizationKeys.Identity.UserNotAuthenticated]);
        }

        var user = await identityService.GetUserByIdAsync(currentUserService.UserId.Value, ct);

        if (user == null)
        {
            user = await identityService.GetOrCreateExternalUserAsync(
                currentUserService.UserId.Value,
                currentUserService.Username ?? "Unknown User",
                currentUserService.Email,
                currentUserService.Roles,
                ct);
        }

        var dto = new CurrentUserDto(
            user.Id,
            user.FullName,
            user.UserName!,
            user.Email!,
            user.MobileNumber,
            currentUserService.Roles,
            currentUserService.AccessToken ?? string.Empty
        );

        return Result<CurrentUserDto>.Success(dto);
    }
}
