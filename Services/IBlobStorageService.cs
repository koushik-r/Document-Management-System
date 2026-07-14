namespace DocumentManagement.Api.Services;

public interface IBlobStorageService
{
    Task<IEnumerable<BlobDocumentInfo>> ListAsync();
    Task<BlobDocumentInfo> UploadAsync(string fileName, Stream stream, string contentType);
    Task<(Stream stream, string contentType)> DownloadAsync(string fileName);
    Task DeleteAsync(string fileName);
    Task StageBlockAsync(string uploadId, int blockIndex, Stream blockData);
    Task<BlobDocumentInfo> CommitBlocksAsync(string uploadId, string fileName, string contentType, int totalBlocks);
    Task AbortUploadAsync(string uploadId);
}