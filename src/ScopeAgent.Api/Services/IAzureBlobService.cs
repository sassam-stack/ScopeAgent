namespace ScopeAgent.Api.Services;

public interface IAzureBlobService
{
    Task<byte[]> DownloadPdfFromUrlAsync(string blobUrl);
    Task<byte[]> DownloadImageFromUrlAsync(string blobUrl);
}

