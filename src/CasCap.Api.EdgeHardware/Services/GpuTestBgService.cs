using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;

namespace CasCap.Services;

/// <summary>
/// Windows-only diagnostic service that detects all ILGPU accelerators and logs their
/// device capabilities once on startup.
/// </summary>
/// <remarks>
/// Registration is gated to Windows in
/// <see cref="CasCap.Extensions.EdgeHardwareServiceCollectionExtensions.AddEdgeHardware"/>.
/// </remarks>
public class GpuTestBgService(ILogger<GpuTestBgService> logger) : IBgFeature
{
    /// <inheritdoc/>
    public string FeatureName => "EdgeHardware";

    /// <inheritdoc/>
    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("{ClassName} starting", nameof(GpuTestBgService));
        GpuDetect();
        logger.LogInformation("{ClassName} complete", nameof(GpuTestBgService));
        return Task.CompletedTask;
    }

    /// <summary>Detects all available accelerators and logs device information about each of them.</summary>
    private void GpuDetect()
    {
        // Create main context
        using (var context = Context.CreateDefault())
        {
            // For each available device...
            foreach (var device in context)
            {
                // Create accelerator for the given device.
                // Note that all accelerators have to be disposed before the global context is disposed
                using var accelerator = device.CreateAccelerator(context);
                logger.LogInformation("{ClassName} accelerator info {AcceleratorType}, {AcceleratorName}",
                    nameof(GpuTestBgService), accelerator.AcceleratorType, accelerator.Name);
                PrintAcceleratorInfo(accelerator);
            }
        }

        // CPU accelerators can also be created manually with custom settings.
        // The following code snippet creates a CPU accelerator with 4 threads
        // and highest thread priority.
        using (var context = Context.Create(builder => builder.CPU(new CPUDevice(4, 1, 1))))
        {
            using var accelerator = context.CreateCPUAccelerator(0, CPUAcceleratorMode.Auto, ThreadPriority.Highest);
            logger.LogInformation("{ClassName} accelerator info {AcceleratorType}, {AcceleratorName}",
                nameof(GpuTestBgService), accelerator.AcceleratorType, accelerator.Name);
            PrintAcceleratorInfo(accelerator);
        }
    }

    /// <summary>Logs detailed information on the given accelerator.</summary>
    /// <param name="accelerator">The target accelerator.</param>
    private void PrintAcceleratorInfo(Accelerator accelerator)
    {
        logger.LogDebug("{ClassName} Name: {Name}", nameof(GpuTestBgService), accelerator.Name);
        logger.LogDebug("{ClassName} MemorySize: {MemorySize}", nameof(GpuTestBgService), accelerator.MemorySize);
        logger.LogDebug("{ClassName} MaxThreadsPerGroup: {MaxThreadsPerGroup}", nameof(GpuTestBgService), accelerator.MaxNumThreadsPerGroup);
        logger.LogDebug("{ClassName} MaxSharedMemoryPerGroup: {MaxSharedMemoryPerGroup}", nameof(GpuTestBgService), accelerator.MaxSharedMemoryPerGroup);
        logger.LogDebug("{ClassName} MaxGridSize: {MaxGridSize}", nameof(GpuTestBgService), accelerator.MaxGridSize);
        logger.LogDebug("{ClassName} MaxConstantMemory: {MaxConstantMemory}", nameof(GpuTestBgService), accelerator.MaxConstantMemory);
        logger.LogDebug("{ClassName} WarpSize: {WarpSize}", nameof(GpuTestBgService), accelerator.WarpSize);
        logger.LogDebug("{ClassName} NumMultiprocessors: {NumMultiprocessors}", nameof(GpuTestBgService), accelerator.NumMultiprocessors);
    }
}
