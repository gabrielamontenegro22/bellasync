using BellaSync.Application.Common.Handlers;

namespace BellaSync.Application.Features.Auth.MyProfile;

/// <summary>
/// Lee el perfil del user autenticado actual. Sin parámetros — el
/// handler resuelve la identidad desde ICurrentUserService.
/// </summary>
public sealed record GetMyProfileQuery() : IQuery<MyProfileResponse>;
