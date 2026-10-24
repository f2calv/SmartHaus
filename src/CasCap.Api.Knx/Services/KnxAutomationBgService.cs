using Innovative.SolarCalculator;

namespace CasCap.Services;

/// <summary>
/// Forwards local commands on every metadata-ready replica and runs day/night scheduling only
/// within the monitor's ownership lifetime.
/// </summary>
public sealed class KnxAutomationBgService(
    ILogger<KnxAutomationBgService> logger,
    IOptions<KnxConfig> knxConfig,
    TimeProvider timeProvider,
    KnxGroupAddressLookupService knxGroupAddressLookupSvc,
    KnxSenderBgService knxSenderBgSvc,
    IStateChangeQueue stateChangeQueue,
    IKnxQueryService knxQuerySvc
    ) : IBgFeature
{
    /// <inheritdoc/>
    public string FeatureName => "Knx";

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("{ClassName} starting", nameof(KnxAutomationBgService));
        await knxGroupAddressLookupSvc.WaitUntilReadyAsync(cancellationToken).ConfigureAwait(false);
        await Parallel.ForEachAsync(ReadQueueAsync(cancellationToken),
            new ParallelOptions { MaxDegreeOfParallelism = knxConfig.Value.QueueMaxConcurrency, CancellationToken = cancellationToken },
            async (item, token) => await ProcessItemAsync(item, token).ConfigureAwait(false)).ConfigureAwait(false);
        logger.LogInformation("{ClassName} exiting", nameof(KnxAutomationBgService));
    }

    /// <summary>Runs the sender and optional scheduler as children of the acquired monitor lease.</summary>
    internal Task RunOwnedAsync(KnxWorkerLifetime workerLifetime, CancellationToken cancellationToken)
    {
        if (!knxConfig.Value.DayNightEnabled)
            return knxSenderBgSvc.ExecuteAsync(cancellationToken);

        Func<CancellationToken, Task>[] workers =
            [knxSenderBgSvc.ExecuteAsync, token => RunDayNightAsync(workerLifetime, token)];
        return workerLifetime.RunAsync(workers, cancellationToken);
    }

    /// <summary>
    /// Waits until the KNX connection becomes active, polling at
    /// <see cref="KnxConfig.ConnectionPollingDelayMs"/> intervals and escalating to
    /// <see cref="LogLevel.Warning"/> every <see cref="KnxConfig.ConnectionLogEscalationInterval"/> attempts.
    /// </summary>
    private async Task WaitForConnectionAsync(KnxWorkerLifetime workerLifetime, CancellationToken cancellationToken)
    {
        var attempt = 1;
        while (!workerLifetime.Health.ConnectionActive)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.Log(attempt % knxConfig.Value.ConnectionLogEscalationInterval == 0 ? LogLevel.Warning : LogLevel.Trace,
                "{ClassName} readiness probe not yet healthy, attempt {Attempt}, retry in {RetryMs}ms...",
                nameof(KnxAutomationBgService), attempt, knxConfig.Value.ConnectionPollingDelayMs);
            await Task.Delay(TimeSpan.FromMilliseconds(knxConfig.Value.ConnectionPollingDelayMs), timeProvider, cancellationToken).ConfigureAwait(false);
            attempt++;
        }
        logger.LogInformation("{ClassName} KNX connection active after {Attempt} attempt(s)",
            nameof(KnxAutomationBgService), attempt);
    }

    /// <summary>
    /// Continuously drains the <see cref="IStateChangeQueue"/> and dispatches each item
    /// for processing, respecting <see cref="KnxConfig.QueueMaxConcurrency"/>.
    /// </summary>
    private async IAsyncEnumerable<KnxStateChangeItem> ReadQueueAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!stateChangeQueue.TryDequeue(out var item))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(knxConfig.Value.QueuePollingDelayMs), timeProvider, cancellationToken).ConfigureAwait(false);
                continue;
            }

            yield return item;
        }
    }

    /// <summary>
    /// Processes a single <see cref="KnxStateChangeItem"/>: sends direct writes and/or
    /// resolved state changes to the KNX bus.
    /// </summary>
    private async Task ProcessItemAsync(KnxStateChangeItem item, CancellationToken cancellationToken)
    {
        try
        {
            if (item.DirectWrites is not null)
            {
                logger.LogInformation("{ClassName} processing {Count} direct write(s) for {GroupName}",
                    nameof(KnxAutomationBgService), item.DirectWrites.Count, item.GroupName);

                foreach (var (groupAddressName, value) in item.DirectWrites)
                    await knxQuerySvc.SendDirectAsync(groupAddressName, value, cancellationToken).ConfigureAwait(false);
            }

            if (item.Resolved is not null)
            {
                logger.LogInformation("{ClassName} processing {Count} state change(s) for {GroupName}",
                    nameof(KnxAutomationBgService), item.Resolved.Count, item.GroupName);

                foreach (var (function, feedback, value) in item.Resolved)
                {
                    var result = await knxQuerySvc.SendValueAsync(item.GroupName, function, feedback, value, cancellationToken).ConfigureAwait(false);
                    logger.LogInformation("{ClassName} {GroupAddress} completed with outcome {Outcome}",
                        nameof(KnxAutomationBgService), result.GroupAddress, result.Outcome);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
        {
            logger.LogError(ex, "{ClassName} error processing state change for {GroupName}",
                nameof(KnxAutomationBgService), item.GroupName);
        }
    }

    /// <summary>
    /// Pushes <c>DPT_DayNight</c> (Day=0, Night=1) status into the bus every <see cref="KnxConfig.DayNightPollingDelayMs"/>
    /// using sunrise/sunset times from the <see cref="SolarTimes"/> library.
    /// Weekday/weekend sunrise hour overrides are applied from <see cref="KnxConfig"/>.
    /// The first push occurs immediately on startup.
    /// </summary>
    private async Task RunDayNightAsync(KnxWorkerLifetime workerLifetime, CancellationToken cancellationToken)
    {
        await WaitForConnectionAsync(workerLifetime, cancellationToken).ConfigureAwait(false);
        var tz = TimeZoneInfo.GetSystemTimeZones()
            .FirstOrDefault(p => p.DisplayName.Contains(knxConfig.Value.DayNightTimeZoneLocation, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Unable to find TimeZoneInfo for location '{knxConfig.Value.DayNightTimeZoneLocation}'");
        while (!cancellationToken.IsCancellationRequested)
        {
            await UpdateDayNightAsync().ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromMilliseconds(knxConfig.Value.DayNightPollingDelayMs), timeProvider, cancellationToken).ConfigureAwait(false);
        }

        async Task UpdateDayNightAsync()
        {
            var kga = knxGroupAddressLookupSvc.GetKGAByName(knxConfig.Value.DayNightGroupAddressName);
            if (kga is null)
                return;

            var localTime = TimeZoneInfo.ConvertTimeFromUtc(timeProvider.GetUtcNow().UtcDateTime, tz);
            var solarTimes = new SolarTimes(localTime, knxConfig.Value.DayNightLatitude, knxConfig.Value.DayNightLongitude);
            var sunrise = TimeZoneInfo.ConvertTimeFromUtc(solarTimes.Sunrise.ToUniversalTime(), tz);
            var sunset = TimeZoneInfo.ConvertTimeFromUtc(solarTimes.Sunset.ToUniversalTime(), tz);
            var solarNoon = TimeZoneInfo.ConvertTimeFromUtc(solarTimes.SolarNoon.ToUniversalTime(), tz);

            //send default day night
            await SendDayNightUpdateAsync(kga).ConfigureAwait(false);

            sunrise = GetSunriseOverride(sunrise, localTime);
            //TESTING: unique DayNight GA per sleeping room which can be overridden and not wake up people too early
            //TODO: create a collection of destination group address here?
            var kgaOGEltern = knxGroupAddressLookupSvc.GetKGAByName("SYS-[Night_Day]-Bedroom");
            if (kgaOGEltern is not null)
                await SendDayNightUpdateAsync(kgaOGEltern).ConfigureAwait(false);

            async Task SendDayNightUpdateAsync(KnxGroupAddressParsed kga)
            {
                //1.024 DPT_DayNight standard is Day=0 (false), Night=1 (true)
                var dayNightValue = !IsDay();

                cancellationToken.ThrowIfCancellationRequested();
                await ProcessItemAsync(new KnxStateChangeItem
                {
                    GroupName = kga.Name,
                    DirectWrites = [(kga.Name, dayNightValue)]
                }, cancellationToken).ConfigureAwait(false);

                var logLevel = string.IsNullOrEmpty(knxConfig.Value.BusLoggingGroupAddressFilter)
                    || kga.Name.Contains(knxConfig.Value.BusLoggingGroupAddressFilter, StringComparison.OrdinalIgnoreCase)
                    ? LogLevel.Information
                    : LogLevel.Trace;
                if (IsDay())
                    logger.Log(logLevel, "{ClassName} processed Day/Night value {ActualValue} to {GroupAddress}, "
                        + "local time is {LocalTime:yyyy-MM-dd HH:mm:ss} sunset in {MinutesUntilSunset} minute(s) at {Sunset:HH:mm:ss.fffzzz}, "
                        + "solar noon in {MinutesUntilSolarNoon} minute(s) at {SolarNoon:HH:mm:ss.fffzzz}",
                        nameof(KnxAutomationBgService), dayNightValue, kga.Name,
                        localTime, (int)sunset.Subtract(localTime).TotalMinutes, sunset,
                        (int)solarNoon.Subtract(localTime).TotalMinutes, solarNoon);
                else
                    logger.Log(logLevel, "{ClassName} processed Day/Night value {ActualValue} to {GroupAddress}, "
                        + "local time is {LocalTime:yyyy-MM-dd HH:mm:ss} sunrise in {MinutesUntilSunrise} minute(s) at {Sunrise:HH:mm:ss.fffzzz}",
                        nameof(KnxAutomationBgService), dayNightValue, kga.Name,
                        localTime, (int)sunrise.Subtract(localTime).TotalMinutes, sunrise);
            }

            bool IsDay() => localTime > sunrise && localTime < sunset;

        }
    }

    private DateTime GetSunriseOverride(DateTime sunrise, DateTime localTime)
    {
        var isWeekend = localTime.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        var overrideHour = isWeekend
            ? knxConfig.Value.DayNightSunriseHourOverrideWeekend
            : knxConfig.Value.DayNightSunriseHourOverrideWeekday;
        if (overrideHour <= 0 || sunrise.Hour >= overrideHour)
            return sunrise;

        var sunriseOverride = new DateTime(sunrise.Year, sunrise.Month, sunrise.Day, overrideHour, 0, 0, DateTimeKind.Utc);
        logger.LogInformation("{ClassName} sunrise {Sunrise} is too early! Overridden with {SunriseOverride}",
            nameof(KnxAutomationBgService), sunrise, sunriseOverride);
        return sunriseOverride;
    }
}
