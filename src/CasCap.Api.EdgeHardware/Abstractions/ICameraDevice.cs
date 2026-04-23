namespace CasCap.Abstractions;

/// <summary>Camera device abstraction for capturing images and video.</summary>
public interface ICameraDevice
{
    /// <summary>Captures an image and returns the raw bytes.</summary>
    /// <returns>A tuple containing the image bytes and the UTC timestamp.</returns>
    Task<(byte[] bytes, DateTime timestampUtc)> StoreToMemory();

    /// <summary>Takes a picture and saves it to disk.</summary>
    /// <param name="timestampUtc">Optional UTC timestamp for the filename.</param>
    /// <param name="format">DateTime format string for the filename.</param>
    /// <returns>A tuple containing the file path and the UTC timestamp.</returns>
    Task<(string filePath, DateTime timestampUtc)> TakePicture(DateTime? timestampUtc = null, string format = "yyyy-MM-dd-HH-mm-ss-fff");

    /// <summary>Records video for the specified duration and saves it to disk.</summary>
    /// <param name="duration">Recording duration.</param>
    /// <param name="timestampUtc">Optional UTC timestamp for the filename.</param>
    /// <param name="format">DateTime format string for the filename.</param>
    /// <returns>A tuple containing the file path and the UTC timestamp.</returns>
    Task<(string filePath, DateTime timestampUtc)> TakeVideo(TimeSpan duration, DateTime? timestampUtc = null, string format = "yyyy-MM-dd-HH-mm-ss-fff");
}
