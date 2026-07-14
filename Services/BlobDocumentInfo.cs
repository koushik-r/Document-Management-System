namespace DocumentManagement.Api.Services;

public class BlobDocumentInfo
{
    public string FileName { get; set; } = "";
    public long Size { get; set; }
    public string ContentType { get; set; } = "application/octet-stream";
    public DateTimeOffset UploadedAt { get; set; }
}