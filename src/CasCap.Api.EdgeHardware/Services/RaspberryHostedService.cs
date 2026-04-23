namespace CasCap.Services;

/// <summary>Hosted service for Raspberry Pi camera and motion detection.</summary>
public class RaspberryHostedService : IHostedService
{
    /// <summary>Logger instance.</summary>
    protected readonly ILogger _logger;

    private readonly IHostEnvironment _env;

    /// <summary>Camera device.</summary>
    protected readonly ICameraDevice _cameraDev;

    /// <summary>Motion detection device.</summary>
    protected readonly IMotionDetectionDevice _motionDetection;

    /// <summary>Blob storage service.</summary>
    protected readonly IBlobStorage _blobStorage;

    /// <summary>Initializes a new instance of the <see cref="RaspberryHostedService"/> class.</summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="env">Host environment.</param>
    /// <param name="cameraDev">Camera device.</param>
    /// <param name="motionDetection">Motion detection device.</param>
    /// <param name="blobStorage">Blob storage service.</param>
    public RaspberryHostedService(ILogger<RaspberryHostedService> logger,
        IHostEnvironment env,
        ICameraDevice cameraDev,
        IMotionDetectionDevice motionDetection,
        IBlobStorage blobStorage
        )
    {
        _logger = logger;
        _env = env;
        _cameraDev = cameraDev;
        _motionDetection = motionDetection;
        _motionDetection.MotionDetectedEvent += OnMotionDetectedEvent;

        _blobStorage = blobStorage;
    }

    //Task SendDeviceToCloudMessageAsync(string messageString)
    //{
    //    return SendDeviceToCloudMessageAsync(messageString, null);
    //}

    //Task SendDeviceToCloudMessageAsync(string messageString, KeyValuePair<string, string> property)
    //{
    //    return SendDeviceToCloudMessageAsync(messageString, new List<KeyValuePair<string, string>> { property });
    //}

    //async Task SendDeviceToCloudMessageAsync(string messageString, List<KeyValuePair<string, string>> properties)
    //{
    //    var message = new Message(Encoding.ASCII.GetBytes(messageString));
    //    if (!properties.IsNullOrEmpty())
    //        foreach (var property in properties)
    //            message.Properties.Add(property);

    //    await _deviceClient.SendEventAsync(message);
    //    Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss.fff}\tsending message '{messageString}' to iothub");
    //}

    //bool motionDetected = false;

    private async void OnMotionDetectedEvent(object? sender, MotionDetectedEventArgs e)
    {
        //i.e. System.Diagnostics.Process
        if (/*!motionDetected && */e.MotionStarted)
        {
            //motionDetected = true;
            Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss.fff}\tmotion detected, taking picture!");
            var version = 3;
            if (version == 0)
                await _cameraDev.TakePicture();
            else if (version == 1)
            {
                var raw = await _cameraDev.StoreToMemory();
                Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss.fff}\tjpg created");
                var fileName = $"{DateTime.UtcNow}.raw";//:yyyy-MM-dd-HH-mm-ss-fff"
                await _blobStorage.UploadBlob(fileName, raw.bytes, CancellationToken.None);
                Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss.fff}\tjpg uploaded");
            }
            else if (version == 2)
            {
                var jpg = await _cameraDev.TakePicture();
                Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss.fff}\tjpg created");
                var bytes = await File.ReadAllBytesAsync(jpg.filePath);
                await _blobStorage.UploadBlob(Path.GetFileName(jpg.filePath), bytes, CancellationToken.None);
                Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss.fff}\tjpg uploaded");
            }
            else if (version == 3)
            {
                //await SendDeviceToCloudMessageAsync("photo taken!", new KeyValuePair<string, string>("cameraEvent", "true"));
                var jpg = await _cameraDev.TakePicture();
                Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss.fff}\tjpg created");
                var webp = await ConvertJpg2Webp(jpg.filePath);
                Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss.fff}\tjpg -> webp");

                //await _blobStorageSvc.UploadBytes(await File.ReadAllBytesAsync(jpg.filePath),
                //    $"{jpg.utcDate:yyyy/MM/dd/HH}/{Path.GetFileName(jpg.filePath)}");
                //Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss.fff}\tjpg uploaded");
                File.Delete(jpg.filePath);
                Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss.fff}\tjpg deleted");

                await _blobStorage.UploadBlob($"{jpg.timestampUtc:yyyy/MM/dd/HH}/{Path.GetFileName(webp.filePath)}", webp.bytes, CancellationToken.None);
                Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss.fff}\twebp uploaded");

                //Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss.fff}\ttry upload to googlephotos");//can't do this cos of OAuth - this would have to be a local svc?
                //await GooglePhotosDevTEMP(webp.filePath);


                File.Delete(webp.filePath);
                Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss.fff}\twebp deleted");
            }
            else
                throw new NotSupportedException($"unexpected version {version}");
            //motionDetected = false;
        }
        else
            Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss.fff}\tmotion stopped");
    }
    /*
    private Album albumRaspiCamLatest;
    private Album albumRaspiCamAll;
    private string lastId;

    public async Task GooglePhotosDevTEMP(string path, string albumTitle = "test")
    {
        //var json = string.Empty;
        if (!await _googlePhotosSvc.LoginAsync()) return;

        albumRaspiCamLatest ??= await _googlePhotosSvc.GetOrCreateAlbumAsync(nameof(albumRaspiCamLatest));
        albumRaspiCamAll ??= await _googlePhotosSvc.GetOrCreateAlbumAsync(nameof(albumRaspiCamAll));
        //json = await _googlePhotosSvc.GetAlbums<string>();
        //WriteJSON(json, nameof(albumsGetResponse));
        //var albumRes = await _googlePhotosSvc.GetAlbums();

        //var album = await _googlePhotosSvc.GetOrCreateAlbumAsync(albumTitle);

        //foreach (var path in new[] {
        //    Path.Combine(_appConfig.LocalPath, "test.jpg"),
        //    Path.Combine(_appConfig.LocalPath, "test.png"),
        //    Path.Combine(_appConfig.LocalPath, "test.webp"),
        //})
        //{
        var result = await _googlePhotosSvc.UploadSingle(path, albumRaspiCamAll.id);

        //remove the previous 'wallpaper'
        if (lastId is not null)
            await _googlePhotosSvc.RemoveMediaItemsFromAlbumAsync(albumRaspiCamLatest.id, new[] { lastId });
        //add a new one
        await _googlePhotosSvc.AddMediaItemsToAlbumAsync(albumRaspiCamLatest.id, new[] { result.mediaItem.id });
        lastId = result.mediaItem.id;
        //_logger.LogDebug(json);
        //var mediaItems = json.FromJson<mediaItemsCreateResponse>();
        //}

        //json = await _googlePhotosSvc.GetAlbums<string>();
        //WriteJSON(json, nameof(albumsGetResponse));

        //json = await _googlePhotosSvc.GetSharedAlbums<string>();
        //WriteJSON(json, "sharedAlbums");

        //json = await _googlePhotosSvc.GetMediaItems<string>();
        //WriteJSON(json, "mediaItems");

        //void WriteJSON(string str, string fileName)
        //{
        //    File.WriteAllText(Path.Combine(_appConfig.LocalPath, $"{fileName}.json"), str);
        //}
    }
    */

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        //await GooglePhotosDevTEMP();
        //await GooglePhotosDevTEMP(Path.Combine(_appConfig.LocalPath, "test.webp"));

        //https://github.com/techyian/MMALSharp
        if (_env.IsProduction() && 1 == 2)
        {
            await _cameraDev.TakePicture();
            _logger.LogDebug("{ClassName} photo taken :)", nameof(RaspberryHostedService));

            await _cameraDev.TakeVideo(TimeSpan.FromSeconds(5));
            _logger.LogDebug("{ClassName} video recorded!", nameof(RaspberryHostedService));

            var raw = await _cameraDev.StoreToMemory();
            _logger.LogDebug("{ClassName} raw picture taken, {Bytes} bytes!", nameof(RaspberryHostedService), raw.bytes.Length);
        }

        _logger.LogDebug("{ClassName} start motion sensor loop...", nameof(RaspberryHostedService));
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(2_000, cancellationToken);
        }
        _logger.LogDebug("{ClassName} start {MotionDetectionDev} dispose...", nameof(RaspberryHostedService), nameof(_motionDetection));
        _logger.LogDebug("{ClassName} start {CameraDev} dispose...", nameof(RaspberryHostedService), nameof(_cameraDev));
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task<(string filePath, byte[] bytes)> ConvertJpg2Webp(string jpgFullPath)
    {
        await Task.Delay(0);
        var path = Path.GetDirectoryName(jpgFullPath);
        var webpFileName = Path.GetFileNameWithoutExtension(jpgFullPath);
        var webpFullPath = Path.Combine(path!, webpFileName) + ".webp";
        var cmd = $"cwebp {jpgFullPath} -o {webpFullPath} -metadata all";
        //var cmd = $"convert {jpgFullPath} -quality 50 -define webp:lossless=true {webpFullPath}.webp";//imagemagick 6.9

        _logger.LogDebug("{ClassName} {Cmd}", nameof(RaspberryHostedService), cmd);
        var output = cmd.Bash();
        _logger.LogDebug("{ClassName} {Output}", nameof(RaspberryHostedService), output);

        if (!File.Exists(webpFullPath)) throw new FileNotFoundException(webpFullPath);

        var bytes = await File.ReadAllBytesAsync(webpFullPath);
        return (webpFullPath, bytes);
    }
}
