using BellaSync.Application.Common.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace BellaSync.Infrastructure.Services;

/// <summary>
/// Implementación de IFileStorage que guarda archivos en el filesystem
/// local, bajo wwwroot/uploads/{category}/. Pensada para dev y on-prem
/// chico. Para producción cloud → swap por S3FileStorage / R2FileStorage.
///
/// Servido a clientes vía app.UseStaticFiles() (configurado en Program.cs).
/// La URL devuelta es relativa: "/uploads/vouchers/{guid}.jpg".
///
/// NO usa CDN, NO comprime, NO versiona. Bueno para arrancar; cuando se
/// escale, abstraerlo es trivial gracias a la interfaz.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private const string UploadsFolder = "uploads";
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<LocalFileStorage> _logger;

    public LocalFileStorage(IWebHostEnvironment env, ILogger<LocalFileStorage> logger)
    {
        _env = env;
        _logger = logger;
    }

    public async Task<string> SaveAsync(
        string category,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("category obligatorio", nameof(category));

        // Sanitizamos category — solo letras/dígitos. Nadie nos hace path
        // traversal poniendo "../../etc/passwd" como categoría.
        var safeCategory = new string(category.Where(char.IsLetterOrDigit).ToArray());
        if (safeCategory.Length == 0) safeCategory = "misc";

        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(ext) || ext.Length > 10) ext = ".bin";
        // Sanitizamos ext: solo letras/dígitos/punto.
        ext = new string(ext.Where(c => char.IsLetterOrDigit(c) || c == '.').ToArray()).ToLowerInvariant();

        // Nombre único = Guid; el filename original no se usa para nada
        // más que diagnóstico, así que no lo conservamos para evitar
        // problemas de unicode / longitud / colisión.
        var newName = $"{Guid.NewGuid():N}{ext}";

        // wwwroot puede no existir en algunas plantillas de .NET — lo
        // creamos perezosamente.
        var webRoot = _env.WebRootPath
            ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var folder = Path.Combine(webRoot, UploadsFolder, safeCategory);
        Directory.CreateDirectory(folder);

        var fullPath = Path.Combine(folder, newName);
        using (var fs = File.Create(fullPath))
        {
            await content.CopyToAsync(fs, ct);
        }

        var publicUrl = $"/{UploadsFolder}/{safeCategory}/{newName}";
        _logger.LogInformation(
            "Archivo guardado: {Path} ({Bytes} bytes, {ContentType}) → {Url}",
            fullPath, new FileInfo(fullPath).Length, contentType, publicUrl);

        return publicUrl;
    }

    public Task DeleteAsync(string url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url)) return Task.CompletedTask;

        // Solo borramos archivos locales que hayamos servido nosotros.
        // URLs externas (http://) las dejamos en paz — no es nuestro storage.
        if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;

        try
        {
            var webRoot = _env.WebRootPath
                ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            // url = "/uploads/logos/abc.jpg" → quitamos el "/" inicial
            var relative = url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(webRoot, relative);

            // Sanity: no permitir paths fuera de wwwroot/uploads
            var uploadsRoot = Path.GetFullPath(Path.Combine(webRoot, UploadsFolder));
            var fullPathNormalized = Path.GetFullPath(fullPath);
            if (!fullPathNormalized.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("DeleteAsync rechazado por path fuera de uploads: {Url}", url);
                return Task.CompletedTask;
            }

            if (File.Exists(fullPathNormalized))
            {
                File.Delete(fullPathNormalized);
                _logger.LogInformation("Archivo borrado: {Path}", fullPathNormalized);
            }
        }
        catch (Exception ex)
        {
            // Borrar no es crítico. Loguear y seguir.
            _logger.LogWarning(ex, "Falla borrando {Url}", url);
        }

        return Task.CompletedTask;
    }
}
