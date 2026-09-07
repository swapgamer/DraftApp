namespace DraftDatastore.API.Storage;

/// <summary>Optional Azure Blob configuration. When omitted, local wwwroot storage is used for development.</summary>
public sealed class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";

    public string? ConnectionString { get; init; }
    public string PlayerImagesContainer { get; init; } = "player-images";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString);
}
