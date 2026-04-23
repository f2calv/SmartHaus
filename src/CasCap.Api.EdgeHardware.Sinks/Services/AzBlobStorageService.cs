using Azure.Core;

namespace CasCap.Services;

/// <inheritdoc cref="IBlobStorage"/>
public class AzBlobStorageService(Uri blobContainerUri, string containerName, TokenCredential credential)
    : AzBlobStorageBase(blobContainerUri, containerName, credential), IBlobStorage
{ }
