using RepairConnect.Services.Common;

namespace RepairConnect.Services;

public sealed class TechnicianDocumentStorage : ITechnicianDocumentStorage
{
    public const long MaxFileSize = 5 * 1024 * 1024;
    private readonly string _root;

    public TechnicianDocumentStorage(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configured = configuration["TechnicianDocuments:StoragePath"];
        _root = Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.ContentRootPath, "App_Data", "technician-documents")
            : Path.IsPathRooted(configured) ? configured : Path.Combine(environment.ContentRootPath, configured));
        var webRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(environment.WebRootPath
            ?? Path.Combine(environment.ContentRootPath, "wwwroot")));
        if (_root.Equals(webRoot, StringComparison.OrdinalIgnoreCase) ||
            _root.StartsWith(webRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Technician document storage must be outside the public web root.");
    }

    public async Task<ServiceResult<string>> SaveAsync(int userId, IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file.Length <= 0 || file.Length > MaxFileSize)
            return ServiceResult<string>.Failure("Upload one document of at most 5 MB.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".pdf" or ".png" or ".jpg" or ".jpeg"))
            return ServiceResult<string>.Failure("Upload a PDF, PNG or JPEG document.");

        await using var source = file.OpenReadStream();
        using var contents = new MemoryStream();
        var buffer = new byte[81920];
        int count;
        while ((count = await source.ReadAsync(buffer, cancellationToken)) != 0)
        {
            if (contents.Length + count > MaxFileSize)
                return ServiceResult<string>.Failure("Upload one document of at most 5 MB.");
            await contents.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
        }

        var bytes = contents.ToArray();
        if (!MatchesSignature(extension, bytes))
            return ServiceResult<string>.Failure("The document contents do not match its PDF, PNG or JPEG file type.");

        var relativePath = $"{userId}/{Guid.NewGuid():N}{extension}";
        var absolutePath = Resolve(relativePath)!;
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        try
        {
            await using var destination = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await destination.WriteAsync(bytes, cancellationToken);
        }
        catch
        {
            Delete(relativePath);
            throw;
        }
        return ServiceResult<string>.Success(relativePath);
    }

    public Task<StoredTechnicianDocument?> OpenAsync(string document, CancellationToken cancellationToken = default)
    {
        var absolutePath = Resolve(document);
        if (absolutePath == null || !File.Exists(absolutePath)) return Task.FromResult<StoredTechnicianDocument?>(null);
        var extension = Path.GetExtension(absolutePath).ToLowerInvariant();
        var contentType = extension switch { ".pdf" => "application/pdf", ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", _ => null };
        if (contentType == null) return Task.FromResult<StoredTechnicianDocument?>(null);
        try
        {
            Stream content = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            return Task.FromResult<StoredTechnicianDocument?>(new(content, $"Qualification{extension}", contentType));
        }
        catch (FileNotFoundException) { return Task.FromResult<StoredTechnicianDocument?>(null); }
        catch (DirectoryNotFoundException) { return Task.FromResult<StoredTechnicianDocument?>(null); }
    }

    public void Delete(string document)
    {
        var absolutePath = Resolve(document);
        if (absolutePath != null && File.Exists(absolutePath)) File.Delete(absolutePath);
    }

    private string? Resolve(string document)
    {
        if (string.IsNullOrWhiteSpace(document) || Path.IsPathRooted(document)) return null;
        try
        {
            var absolutePath = Path.GetFullPath(Path.Combine(_root, document));
            return absolutePath.StartsWith(Path.TrimEndingDirectorySeparator(_root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? absolutePath : null;
        }
        catch (ArgumentException) { return null; }
        catch (NotSupportedException) { return null; }
        catch (PathTooLongException) { return null; }
    }

    private static bool MatchesSignature(string extension, byte[] data) => extension switch
    {
        ".pdf" => data.AsSpan().StartsWith("%PDF-"u8),
        ".png" => data.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        ".jpg" or ".jpeg" => data.AsSpan().StartsWith(new byte[] { 255, 216, 255 }),
        _ => false
    };
}
