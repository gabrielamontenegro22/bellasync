using BellaSync.Application.Common.Handlers;

namespace BellaSync.Application.Features.Auth.MyProfile;

/// <summary>
/// Actualiza datos editables del propio user. Por ahora solo el nombre
/// completo. El email NO se permite cambiar acá — requeriría flujo de
/// verificación (envío de link al email nuevo + confirmación) que es
/// scope mayor. Si la admin necesita corregir un email mal escrito, hoy
/// la salida es desactivar el user y crear uno nuevo.
/// </summary>
public sealed record UpdateMyProfileCommand(
    string FullName
) : ICommand<MyProfileResponse>;
