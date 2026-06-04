namespace BellaSync.Application.Common.Interfaces;

/// <summary>
/// Abstracción para guardar archivos subidos (comprobantes bancarios,
/// logos del salón). Inyectada en los handlers/controllers que necesitan
/// persistir archivos.
///
/// En dev: LocalFileStorage escribe a wwwroot/uploads/{category}/.
/// En prod: implementación S3/R2 (TODO cuando llegue el deploy).
///
/// El handler no sabe NADA del transporte — recibe stream + metadata,
/// devuelve URL pública. Eso permite swappear el storage sin tocar
/// lógica de negocio (mismo patrón que IWhatsAppSender).
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Guarda un archivo y devuelve la URL pública para servirlo.
    ///
    /// Categoría = subcarpeta lógica (ej. "vouchers", "logos"). El
    /// implementador elige el path real y genera nombre único (Guid)
    /// preservando la extensión original.
    /// </summary>
    /// <param name="category">Subcarpeta (vouchers / logos / ...).</param>
    /// <param name="fileName">Nombre original — usado SOLO para extraer extensión.</param>
    /// <param name="contentType">MIME (image/jpeg, image/png, …).</param>
    /// <param name="content">Stream del archivo.</param>
    /// <returns>URL relativa al server para servir el archivo (ej. "/uploads/vouchers/abc123.jpg").</returns>
    Task<string> SaveAsync(
        string category,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken ct);

    /// <summary>
    /// Borra un archivo previamente guardado. Idempotente: si el archivo
    /// no existe, no falla. Usado al reemplazar logos (borramos el viejo).
    /// </summary>
    Task DeleteAsync(string url, CancellationToken ct);
}

/// <summary>
/// Validaciones compartidas para uploads (tipo, tamaño). Centralizadas
/// para que vouchers y logos las apliquen igual.
/// </summary>
public static class FileUploadRules
{
    /// <summary>5 MB — suficiente para fotos modernas de teléfono y screenshots.</summary>
    public const long MaxFileSizeBytes = 5 * 1024 * 1024;

    /// <summary>Tipos de imagen aceptados.</summary>
    public static readonly HashSet<string> AllowedImageContentTypes = new()
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/webp",
        "image/heic",  // iPhone
        "image/heif",
    };

    /// <summary>True si el contentType es una imagen aceptada.</summary>
    public static bool IsAllowedImage(string contentType) =>
        AllowedImageContentTypes.Contains(contentType.ToLowerInvariant());
}
