using Innovative.SolarCalculator;

namespace CasCap.Services;

/// <summary>
/// The <see cref="KnxAutomationBgService"/> is the primary automation background service
/// that runs up to two concurrent tasks after the KNX connection becomes active:
/// <list type="bullet">
/// <item><description>
/// <b>Queue processor</b> — continuously drains the <see cref="IStateChangeQueue"/> and
/// dispatches direct writes and resolved state changes to the KNX bus.
/// </description></item>
/// <item><description>
/// <b>Day/night detection</b> — when enabled, pushes <c>DPT_DayNight</c> (Day=0, Night=1)
/// status into the bus every <see cref="KnxConfig.DayNightPollingDelayMs"/> using sunrise/sunset times from the
/// <see cref="SolarTimes"/> library.
/// </description></item>
/// </list>
/// </summary>
public class KnxAutomationBgService(
    ILogger<KnxAutomationBgService> logger,
    IOptions<KnxConfig> knxConfig,
    KnxGroupAddressLookupService knxGroupAddressLookupSvc,
    KnxConnectionHealthCheck knxConnectionHealthCheck,
    IStateChangeQueue stateChangeQueue,
    IKnxQueryService knxQuerySvc
    ) : IBgFeature
{
    private readonly SemaphoreSlim _queueSemaphore = new(knxConfig.Value.QueueMaxConcurrency, knxConfig.Value.QueueMaxConcurrency);

    /// <inheritdoc/>
    public string FeatureName => "Knx";

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("{ClassName} starting", nameof(KnxAutomationBgService));
        try
        {
            await WaitForConnectionAsync(cancellationToken);

            var tasks = new List<Task> { ProcessQueueAsync(cancellationToken) };

            if (knxConfig.Value.DayNightEnabled)
                tasks.Add(RunDayNightAsync(cancellationToken));
            else
                logger.LogWarning("{ClassName} day/night detection disabled",
                    nameof(KnxAutomationBgService));

            //await-await-WhenAny propagates the first faulted task immediately so the
            //service crashes and the pod restarts rather than running in a degraded state.
            await await Task.WhenAny(tasks);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException) { throw; }
        logger.LogInformation("{ClassName} exiting", nameof(KnxAutomationBgService));
    }

    /// <summary>
    /// Waits until the KNX connection becomes active, polling at
    /// <see cref="KnxConfig.ConnectionPollingDelayMs"/> intervals and escalating to
    /// <see cref="LogLevel.Warning"/> every <see cref="KnxConfig.ConnectionLogEscalationInterval"/> attempts.
    /// </summary>
    private async Task WaitForConnectionAsync(CancellationToken cancellationToken)
    {
        var attempt = 1;
        while (!knxConnectionHealthCheck.ConnectionActive)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.Log(attempt % knxConfig.Value.ConnectionLogEscalationInterval == 0 ? LogLevel.Warning : LogLevel.Trace,
                "{ClassName} readiness probe not yet healthy, attempt {Attempt}, retry in {RetryMs}ms...",
                nameof(KnxAutomationBgService), attempt, knxConfig.Value.ConnectionPollingDelayMs);
            await Task.Delay(knxConfig.Value.ConnectionPollingDelayMs, cancellationToken);
            attempt++;
        }
        logger.LogInformation("{ClassName} KNX connection active after {Attempt} attempt(s)",
            nameof(KnxAutomationBgService), attempt);
    }

    /// <summary>
    /// Continuously drains the <see cref="IStateChangeQueue"/> and dispatches each item
    /// for processing, respecting <see cref="KnxConfig.QueueMaxConcurrency"/>.
    /// </summary>
    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!stateChangeQueue.TryDequeue(out var item))
            {
                await Task.Delay(knxConfig.Value.QueuePollingDelayMs, cancellationToken);
                continue;
            }

            await _queueSemaphore.WaitAsync(cancellationToken);
            _ = ProcessItemAsync(item, cancellationToken);
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
                    await knxQuerySvc.SendDirectAsync(groupAddressName, value, cancellationToken);
            }

            if (item.Resolved is not null)
            {
                logger.LogInformation("{ClassName} processing {Count} state change(s) for {GroupName}",
                    nameof(KnxAutomationBgService), item.Resolved.Count, item.GroupName);

                foreach (var (function, feedback, value) in item.Resolved)
                {
                    var result = await knxQuerySvc.SendValueAsync(item.GroupName, function, feedback, value, cancellationToken);
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
        finally
        {
            _queueSemaphore.Release();
        }
    }

    /// <summary>
    /// Pushes <c>DPT_DayNight</c> (Day=0, Night=1) status into the bus every <see cref="KnxConfig.DayNightPollingDelayMs"/>
    /// using sunrise/sunset times from the <see cref="SolarTimes"/> library.
    /// Weekday/weekend sunrise hour overrides are applied from <see cref="KnxConfig"/>.
    /// The first push occurs immediately on startup.
    /// </summary>
    private async Task RunDayNightAsync(CancellationToken cancellationToken)
    {
        var tz = TimeZoneInfo.GetSystemTimeZones()
            .FirstOrDefault(p => p.DisplayName.Contains(knxConfig.Value.DayNightTimeZoneLocation, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Unable to find TimeZoneInfo for location '{knxConfig.Value.DayNightTimeZoneLocation}'");
        while (!cancellationToken.IsCancellationRequested)
        {
            UpdateDayNight();
            await Task.Delay(knxConfig.Value.DayNightPollingDelayMs, cancellationToken);
        }

        void UpdateDayNight()
        {
            var kga = knxGroupAddressLookupSvc.GetKGAByName(knxConfig.Value.DayNightGroupAddressName);
            if (kga is null)
                return;

            var localTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            var solarTimes = new SolarTimes(localTime, knxConfig.Value.DayNightLatitude, knxConfig.Value.DayNightLongitude);
            var sunrise = TimeZoneInfo.ConvertTimeFromUtc(solarTimes.Sunrise.ToUniversalTime(), tz);
            var sunset = TimeZoneInfo.ConvertTimeFromUtc(solarTimes.Sunset.ToUniversalTime(), tz);
            var solarNoon = TimeZoneInfo.ConvertTimeFromUtc(solarTimes.SolarNoon.ToUniversalTime(), tz);

            //send default day night
            EnqueueDayNightUpdate(kga);

            //lets now override sunrise
            {
                var sunriseOverride = sunrise;
                var overrideActive = false;
                if (knxConfig.Value.DayNightSunriseHourOverrideWeekend > 0 && IsWeekend()
                    && sunrise.Hour < knxConfig.Value.DayNightSunriseHourOverrideWeekend)
                {
                    sunriseOverride = new DateTime(sunrise.Year, sunrise.Month, sunrise.Day, knxConfig.Value.DayNightSunriseHourOverrideWeekend, 0, 0, DateTimeKind.Utc);
                    overrideActive = true;
                }
                else if (knxConfig.Value.DayNightSunriseHourOverrideWeekday > 0 && IsWeekday()
                    && sunrise.Hour < knxConfig.Value.DayNightSunriseHourOverrideWeekday)
                {
                    sunriseOverride = new DateTime(sunrise.Year, sunrise.Month, sunrise.Day, knxConfig.Value.DayNightSunriseHourOverrideWeekday, 0, 0, DateTimeKind.Utc);
                    overrideActive = true;
                }
                if (overrideActive)
                    logger.LogInformation("{ClassName} sunrise {Sunrise} is too early! Overridden with {SunriseOverride}",
                        nameof(KnxAutomationBgService), sunrise, sunriseOverride);
                sunrise = sunriseOverride;
            }
            //TESTING: unique DayNight GA per sleeping room which can be overridden and not wake up people too early
            //TODO: create a collection of destination group address here?
            var kgaOGEltern = knxGroupAddressLookupSvc.GetKGAByName("SYS-[Night_Day]-Bedroom");
            if (kgaOGEltern is not null)
                EnqueueDayNightUpdate(kgaOGEltern);

            void EnqueueDayNightUpdate(KnxGroupAddressParsed kga)
            {
                //1.024 DPT_DayNight standard is Day=0 (false), Night=1 (true)
                var dayNightValue = !IsDay();

                stateChangeQueue.Enqueue(new KnxStateChangeItem
                {
                    GroupName = kga.Name,
                    DirectWrites = [(kga.Name, dayNightValue)],
                });

                var logLevel = string.IsNullOrEmpty(knxConfig.Value.BusLoggingGroupAddressFilter)
                    || kga.Name.Contains(knxConfig.Value.BusLoggingGroupAddressFilter, StringComparison.OrdinalIgnoreCase)
                    ? LogLevel.Information
                    : LogLevel.Trace;
                if (IsDay())
                    logger.Log(logLevel, "{ClassName} enqueued Day/Night value {ActualValue} to {GroupAddress}, "
                        + "local time is {LocalTime:yyyy-MM-dd HH:mm:ss} sunset in {MinutesUntilSunset} minute(s) at {Sunset:HH:mm:ss.fffzzz}, "
                        + "solar noon in {MinutesUntilSolarNoon} minute(s) at {SolarNoon:HH:mm:ss.fffzzz}",
                        nameof(KnxAutomationBgService), dayNightValue, kga.Name,
                        localTime, (int)sunset.Subtract(localTime).TotalMinutes, sunset,
                        (int)solarNoon.Subtract(localTime).TotalMinutes, solarNoon);
                else
                    logger.Log(logLevel, "{ClassName} enqueued Day/Night value {ActualValue} to {GroupAddress}, "
                        + "local time is {LocalTime:yyyy-MM-dd HH:mm:ss} sunrise in {MinutesUntilSunrise} minute(s) at {Sunrise:HH:mm:ss.fffzzz}",
                        nameof(KnxAutomationBgService), dayNightValue, kga.Name,
                        localTime, (int)sunrise.Subtract(localTime).TotalMinutes, sunrise);
            }

            bool IsDay() => localTime > sunrise && localTime < sunset;

            bool IsWeekend() => new[] { DayOfWeek.Saturday, DayOfWeek.Sunday }.Contains(localTime.DayOfWeek);

            bool IsWeekday() => !IsWeekend();
        }
    }
}
