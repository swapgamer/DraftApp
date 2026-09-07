using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace DraftDatastore.API.Storage;

public interface IPlayerImageStorage
{
    Task SaveAsync(string blobPath, Stream content, string contentType, CancellationToken cancellationToken);
}

public sealed class PlayerImageStorage(
    IWebHostEnvironment host,
    IOptions<BlobStorageOptions> options) : IPlayerImageStorage
{
    public async Task SaveAsync(string blobPath, Stream content, string contentType, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (settings.IsConfigured)
        {
            var container = new BlobContainerClient(settings.ConnectionString, settings.PlayerImagesContainer);
            var blob = container.GetBlobClient(blobPath.Replace("\\", "/"));
            await blob.UploadAsync(content, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
            }, cancellationToken);
            return;
        }

        var destination = Path.Combine(host.WebRootPath, "player-images", blobPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await using var output = File.Create(destination);
        await content.CopyToAsync(output, cancellationToken);
    }
}
