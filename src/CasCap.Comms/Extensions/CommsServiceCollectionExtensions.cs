namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the shared Signalizr communications pipeline.</summary>
public static class CommsServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="CommsConfig"/>, the <see cref="CommsStreamSinkService"/> and the
    /// <see cref="CommsMediaStore"/>, so any pod can write events for the <c>Comms</c> feature to deliver.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddCommsStreamSink(this IServiceCollection services)
    {
        services.AddCasCapConfiguration<CommsConfig>();
        services.TryAddSingleton<IEventSink<CommsEvent>, CommsStreamSinkService>();
        services.TryAddSingleton<CommsMediaStore>();
        return services;
    }

    /// <summary>
    /// Registers the Signalizr client, the voice pipeline, duplicate suppression and the default
    /// <see cref="ICommsEventFormatter"/> and <see cref="ICommsGroupRouter"/>, then the
    /// <see cref="CommunicationsBgService"/> background feature.
    /// </summary>
    /// <remarks>
    /// Register an <see cref="ICommsEventFormatter"/>, <see cref="ICommsGroupRouter"/> or
    /// <see cref="ISignalMessageDeduplicator"/> first to replace a default. Register an
    /// <see cref="ICommsResponder"/> to answer inbound messages; without one they are only logged.
    /// Voice processing is registered unconditionally; <see cref="SpeechToTextConfig.Mode"/> and
    /// <see cref="TextToSpeechConfig.Mode"/> decide whether it does any work.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration, for the Signalizr client.</param>
    /// <param name="lite">
    /// When <see langword="true"/>, registers the dependencies without the <see cref="IBgFeature"/>.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddComms(this IServiceCollection services, IConfiguration configuration, bool lite = false)
    {
        services.AddCommsStreamSink();
        services.AddSignalizrClient(configuration);
        services.AddSpeechToText();
        services.AddTextToSpeech();
        services.TryAddSingleton<ISignalMessageDeduplicator, RedisSignalMessageDeduplicator>();
        services.TryAddSingleton<ICommsEventFormatter, PlainCommsEventFormatter>();
        services.TryAddSingleton<ICommsGroupRouter, MonitorSourcesGroupRouter>();

        if (!lite)
            services.AddSingleton<IBgFeature, CommunicationsBgService>();
        return services;
    }
}
