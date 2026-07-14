using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

namespace DocumentManagement.Api.Services;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient _container;
    private readonly BlobContainerClient _stagingContainer;

    public BlobStorageService(IConfiguration config)
    {
        var connStr = config["AzureBlob:ConnectionString"]!;
        var containerName = config["AzureBlob:ContainerName"]!;
        var blobService = new BlobServiceClient(connStr);
        _container = blobService.GetBlobContainerClient(containerName);
        _container.CreateIfNotExists(PublicAccessType.None);
        _stagingContainer = blobService.GetBlobContainerClient($"{containerName}-staging");
        _stagingContainer.CreateIfNotExists(PublicAccessType.None);
    }

    public async Task<IEnumerable<BlobDocumentInfo>> ListAsync()
    {
        var docs = new List<BlobDocumentInfo>();
        await foreach (var item in _container.GetBlobsAsync())
        {
            docs.Add(new BlobDocumentInfo
            {
                FileName = item.Name,
                Size = item.Properties.ContentLength ?? 0,
                ContentType = item.Properties.ContentType ?? "application/octet-stream",
                UploadedAt = item.Properties.LastModified ?? DateTimeOffset.UtcNow
            });
        }
        return docs;
    }

    public async Task<BlobDocumentInfo> UploadAsync(string fileName, Stream stream, string contentType)
    {
        var blob = _container.GetBlobClient(fileName);
        await blob.UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
            TransferOptions = new StorageTransferOptions
            {
                InitialTransferSize = 4 * 1024 * 1024,
                MaximumTransferSize = 8 * 1024 * 1024,
                MaximumConcurrency = 4
            }
        });
        var props = await blob.GetPropertiesAsync();
        return new BlobDocumentInfo
        {
            FileName = fileName,
            Size = props.Value.ContentLength,
            ContentType = contentType,
            UploadedAt = props.Value.LastModified
        };
    }

    public async Task<(Stream stream, string contentType)> DownloadAsync(string fileName)
    {
        var blob = _container.GetBlobClient(fileName);
        var response = await blob.DownloadStreamingAsync();
        return (response.Value.Content, response.Value.Details.ContentType ?? "application/octet-stream");
    }

    public Task DeleteAsync(string fileName)
        => _container.GetBlobClient(fileName).DeleteIfExistsAsync();

    public async Task StageBlockAsync(string uploadId, int blockIndex, Stream blockData)
    {
        using var buffer = new MemoryStream();
        await blockData.CopyToAsync(buffer);
        buffer.Position = 0;
        var blob = _stagingContainer.GetBlockBlobClient(uploadId);
        var blockId = BlockId(blockIndex);
        await blob.StageBlockAsync(blockId, buffer);
    }

    public async Task<BlobDocumentInfo> CommitBlocksAsync(
        string uploadId, string fileName, string contentType, int totalBlocks)
    {
        var stagingBlob = _stagingContainer.GetBlockBlobClient(uploadId);
        var blockIds = Enumerable.Range(0, totalBlocks).Select(BlockId).ToList();

        await stagingBlob.CommitBlockListAsync(blockIds, new CommitBlockListOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        });

        var destBlob = _container.GetBlockBlobClient(fileName);
        var copyOp = await destBlob.StartCopyFromUriAsync(stagingBlob.Uri);
        await copyOp.WaitForCompletionAsync();

        await stagingBlob.DeleteIfExistsAsync();

        var props = await destBlob.GetPropertiesAsync();
        return new BlobDocumentInfo
        {
            FileName = fileName,
            Size = props.Value.ContentLength,
            ContentType = contentType,
            UploadedAt = props.Value.LastModified
        };
    }

    public async Task AbortUploadAsync(string uploadId)
    {
        await _stagingContainer.GetBlobClient(uploadId).DeleteIfExistsAsync();
    }

    private static string BlockId(int index)
        => Convert.ToBase64String(
               System.Text.Encoding.UTF8.GetBytes(index.ToString("D8")));
}