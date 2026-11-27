using RepairConnect.Services.Common;

namespace RepairConnect.Services;

public interface ITechnicianDocumentStorage
{
    Task<ServiceResult<string>> SaveAsync(int userId, IFormFile file, CancellationToken cancellationToken = default);
    Task<StoredTechnicianDocument?> OpenAsync(string document, CancellationToken cancellationToken = default);
    void Delete(string document);
}

public sealed record StoredTechnicianDocument(Stream Content, string FileName, string ContentType);
