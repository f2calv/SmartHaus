namespace CasCap.Services;

/// <summary>
/// Provides query access to KNX bus state, group addresses and telegram history.
/// </summary>
/// <remarks>
/// Key functions are made accessible via the KNX controllers.
/// </remarks>
public class KnxQueryService(ILogger<KnxQueryService> logger, IOptions<KnxConfig> config, IKnxState knxState, KnxGroupAddressLookupService knxGroupAddressLookupSvc, IStateChangeQueue stateChangeQueue, IKnxTelegramBroker<KnxOutgoingTelegram> outgoingBroker) : IKnxQueryService
{
    /// <summary>
    /// Sends a value to the KNX bus for a given group address name.
    /// </summary>
    /// <param name="request">The <see cref="KnxStateChangeRequest"/> containing the group address name and value to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="State"/> object if the group address was found and the value was queued, null otherwise.</returns>
    //lets create specific methods to control bus and lighting with better logging and state polling, this generic method is not ideal for external use
    public /*partial*/ async Task<State?> Send2Bus(
        KnxStateChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        await knxGroupAddressLookupSvc.GetLookup(cancellationToken);
        var kga = knxGroupAddressLookupSvc.GetKGAByName(request.GroupAddressName);
        if (kga is null)
        {
            logger.LogError("{ClassName} groupAddressName={GroupAddressName}, actualValue={ActualValue}",
                nameof(KnxQueryService), request.GroupAddressName, request.ActualValue);
            return null;
        }

        var stateBefore = await knxState.GetKnxState(request.GroupAddressName, cancellationToken);

        var groupValue = kga.ToGroupValue(request.ActualValue);
        await outgoingBroker.PublishAsync(new KnxOutgoingTelegram { Kga = kga, GroupValueData = groupValue.Value }, cancellationToken);
        logger.LogInformation("{ClassName} sending {DptSubType} value {ActualValue} ({Value}) to {GroupAddress}",
            nameof(KnxQueryService), kga.GetDptBase(), request.ActualValue, groupValue.Value, kga.Name);

        var iterations = 0;
        while (iterations < 5)
        {
            await Task.Delay(100, cancellationToken);
            var stateAfter = await knxState.GetKnxState(request.GroupAddressName, cancellationToken);
            logger.LogInformation("{ClassName} state is currently {ValueAfter}", nameof(KnxQueryService), stateAfter?.Value);
            if (stateAfter is not null && !stateAfter.Equals(stateBefore))
            {
                logger.LogInformation("{ClassName} state changed for {GroupAddress} from {ValueBefore} to {ValueAfter} after {Iterations} poll(s)",
                    nameof(KnxQueryService), request.GroupAddressName, stateBefore?.Value, stateAfter.Value, iterations + 1);
                return stateAfter;
            }
            iterations++;
        }

        logger.LogWarning("{ClassName} state did not change for {GroupAddress} from {ValueBefore} to {ValueAfter} after {Iterations} poll(s)",
            nameof(KnxQueryService), request.GroupAddressName, stateBefore?.Value, request.ActualValue, iterations);

        return await knxState.GetKnxState(request.GroupAddressName, cancellationToken);
    }

    /// <summary>
    /// Sets the state of a KNX lighting group address by resolving the appropriate
    /// command and feedback <see cref="LightingFunction"/> pair from the request properties
    /// (<see cref="KnxLightStateChangeRequest.IsOn"/>, <see cref="KnxLightStateChangeRequest.DimValue"/>
    /// or <see cref="KnxLightStateChangeRequest.HexColour"/>).
    /// </summary>
    /// <remarks>
    /// When turning a light off (<see cref="KnxLightStateChangeRequest.IsOn"/>=<see langword="false"/>),
    /// the method queries the <see cref="LightingFunction.VFB"/> feedback address to determine
    /// whether the light was previously dimmed. Non-dimmable lights (no <see cref="LightingFunction.VFB"/> address
    /// configured) or lights still at full brightness receive a simple
    /// <see cref="LightingFunction.SW"/>=<see langword="false"/> command, while dimmed lights
    /// receive <see cref="LightingFunction.VAL"/>=0 to clear the MDT dimming-priority lock.
    /// </remarks>
    /// <param name="request">The <see cref="KnxLightStateChangeRequest"/> containing the group address base name and desired state.</param>
    /// <param name="dryRun">When <see langword="true"/>, resolves and validates the request without sending commands to the bus.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="KnxStateChangeResponse"/> containing the outcome and state of each affected group address.</returns>
    public async Task<KnxStateChangeResponse> SetLightState(
        KnxLightStateChangeRequest request,
        bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        var isDimmed = false;
        if (request.IsOn is false)
        {
            await knxGroupAddressLookupSvc.GetLookup(cancellationToken);
            var vfbName = $"{request.GroupName}-{LightingFunction.VFB}";
            var vfbKga = knxGroupAddressLookupSvc.GetKGAByName(vfbName);
            if (vfbKga is not null)
            {
                var vfbState = await knxState.GetKnxState(vfbName, cancellationToken);
                isDimmed = vfbState is not null && vfbState.Value != "100";
                logger.LogInformation("{ClassName} {GroupAddress} {Function}={VfbValue}, {IsDimmedLabel}={IsDimmed}",
                    nameof(KnxQueryService), request.GroupName, nameof(LightingFunction.VFB), vfbState?.Value, nameof(isDimmed), isDimmed);
            }
            else
                logger.LogInformation("{ClassName} {GroupAddress} has no {Function} address, using {OffFunction}=false for off",
                    nameof(KnxQueryService), request.GroupName, nameof(LightingFunction.VFB), nameof(LightingFunction.SW));
        }

        return await ResolveAndSendAsync(request.GroupName, request.Resolve(isDimmed), dryRun, cancellationToken);
    }

    /// <summary>
    /// Sets the vertical position and/or slats of a KNX blind or shutter by sending values to the
    /// <see cref="ShutterFunction.POS"/> and/or <see cref="ShutterFunction.POSSLATS"/> command addresses
    /// and polling the corresponding feedback addresses for confirmation.
    /// </summary>
    /// <remarks>
    /// When <see cref="KnxShutterStateChangeRequest.Slats"/> is set, the method queries the
    /// <see cref="ShutterFunction.DIRECTION"/> feedback address to determine the last movement
    /// direction. The <see cref="ShutterFunction.POSSLATS"/> command is always applied relative
    /// to the last direction of travel, so the slats value is inverted when the shutter last
    /// moved upward.
    /// </remarks>
    /// <param name="request">The <see cref="KnxShutterStateChangeRequest"/> containing the group address base name and desired position/slats values.</param>
    /// <param name="dryRun">When <see langword="true"/>, resolves and validates the request without sending commands to the bus.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="KnxStateChangeResponse"/> containing the outcome and state of each affected group address.</returns>
    public async Task<KnxStateChangeResponse> SetShutterState(
        KnxShutterStateChangeRequest request,
        bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        bool? lastDirectionDown = null;
        if (request.Slats is not null)
        {
            await knxGroupAddressLookupSvc.GetLookup(cancellationToken);
            var directionName = $"{request.GroupName}-{ShutterFunction.DIRECTION}";
            var directionState = await knxState.GetKnxState(directionName, cancellationToken);
            if (directionState is not null)
            {
                lastDirectionDown = directionState.Value == "Down" || directionState.Value == "1";
                logger.LogInformation("{ClassName} {GroupAddress} {Function}={DirectionValue}, {DirectionLabel}={LastDirectionDown}",
                    nameof(KnxQueryService), request.GroupName, nameof(ShutterFunction.DIRECTION), directionState.Value, nameof(lastDirectionDown), lastDirectionDown);
            }
            else
                logger.LogWarning("{ClassName} {GroupAddress} has no {Function} state, slats value will not be adjusted",
                    nameof(KnxQueryService), request.GroupName, nameof(ShutterFunction.DIRECTION));
        }

        return await ResolveAndSendAsync(request.GroupName, request.Resolve(lastDirectionDown), dryRun, cancellationToken);
    }

    /// <summary>
    /// Switches a KNX power outlet on or off by sending the <see cref="PowerOutletFunction.SD_SW"/>
    /// command and polling the <see cref="PowerOutletFunction.SD_FB"/> feedback address for confirmation.
    /// </summary>
    /// <param name="request">The <see cref="KnxPowerOutletStateChangeRequest"/> containing the group address base name and desired on/off state.</param>
    /// <param name="dryRun">When <see langword="true"/>, resolves and validates the request without sending commands to the bus.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="KnxStateChangeResponse"/> containing the outcome and state of the affected group address.</returns>
    public Task<KnxStateChangeResponse> SetPowerOutletState(
        KnxPowerOutletStateChangeRequest request,
        bool dryRun = false,
        CancellationToken cancellationToken = default)
        => ResolveAndSendAsync(request.GroupName, request.Resolve(), dryRun, cancellationToken);

    /// <summary>
    /// Adjusts the temperature setpoint of a KNX HVAC group address by sending the desired
    /// value to <see cref="HvacFunction.SETP_UPDATE"/> and polling the
    /// <see cref="HvacFunction.SETP"/> feedback address for confirmation.
    /// </summary>
    /// <param name="request">The <see cref="KnxHvacZoneStateChangeRequest"/> containing the group address base name and desired setpoint.</param>
    /// <param name="dryRun">When <see langword="true"/>, resolves and validates the request without sending commands to the bus.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="KnxStateChangeResponse"/> containing the outcome and state of the affected group address.</returns>
    public Task<KnxStateChangeResponse> SetHvacState(
        KnxHvacZoneStateChangeRequest request,
        bool dryRun = false,
        CancellationToken cancellationToken = default)
        => ResolveAndSendAsync(request.GroupName, request.Resolve(), dryRun, cancellationToken);

    private async Task<KnxStateChangeResponse> ResolveAndSendAsync(
        string groupName,
        List<(object Function, object Feedback, object Value)>? resolved,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var changeId = Guid.NewGuid();
        var sw = Stopwatch.StartNew();

        if (resolved is null)
        {
            logger.LogError("{ClassName} no value provided in request for {GroupName}",
                nameof(KnxQueryService), groupName);
            return new KnxStateChangeResponse
            {
                ChangeId = changeId,
                DurationMs = sw.ElapsedMilliseconds,
                Results = [new KnxStateChangeResult
                {
                    GroupAddress = groupName,
                    Outcome = StateChangeOutcome.NoValueProvided,
                }]
            };
        }

        await knxGroupAddressLookupSvc.GetLookup(cancellationToken);

        var results = new List<KnxStateChangeResult>(resolved.Count);
        foreach (var (function, feedback, value) in resolved)
        {
            var functionName = $"{groupName}-{function}";
            var feedbackName = $"{groupName}-{feedback}";

            var kga = knxGroupAddressLookupSvc.GetKGAByName(functionName);
            if (kga is null)
            {
                logger.LogError("{ClassName} function address {GroupAddressName} not found",
                    nameof(KnxQueryService), functionName);
                results.Add(new KnxStateChangeResult
                {
                    GroupAddress = feedbackName,
                    Outcome = StateChangeOutcome.NotFound,
                });
                continue;
            }

            var desiredValue = value.ToString();
            var stateBefore = await knxState.GetKnxState(feedbackName, cancellationToken);
            if (stateBefore is not null && stateBefore.Value == desiredValue)
            {
                logger.LogWarning("{ClassName} {GroupAddress} already has desired value {DesiredValue}, skipping send",
                    nameof(KnxQueryService), feedbackName, value);
                results.Add(new KnxStateChangeResult
                {
                    GroupAddress = feedbackName,
                    Outcome = StateChangeOutcome.AlreadyAtDesiredValue,
                    State = stateBefore,
                });
                continue;
            }

            results.Add(new KnxStateChangeResult
            {
                GroupAddress = feedbackName,
                Outcome = dryRun ? StateChangeOutcome.DryRun : StateChangeOutcome.Queued,
                State = stateBefore,
            });
        }

        if (!dryRun)
        {
            var itemsToQueue = resolved
                .Where(r => results.Any(res =>
                    res.GroupAddress == $"{groupName}-{r.Feedback}" &&
                    res.Outcome is StateChangeOutcome.Queued))
                .ToList();

            if (itemsToQueue.Count > 0)
            {
                if (config.Value.LiteMode)
                {
                    logger.LogInformation("{ClassName} lite mode, sending {Count} command(s) inline for {GroupName}",
                        nameof(KnxQueryService), itemsToQueue.Count, groupName);
                    foreach (var (function, feedback, value) in itemsToQueue)
                        await SendValueAsync(groupName, function, feedback, value, cancellationToken);
                }
                else
                {
                    stateChangeQueue.Enqueue(new KnxStateChangeItem
                    {
                        GroupName = groupName,
                        Resolved = itemsToQueue,
                    });
                    logger.LogInformation("{ClassName} enqueued {Count} state change(s) for {GroupName}",
                        nameof(KnxQueryService), itemsToQueue.Count, groupName);
                }
            }
        }
        else
            logger.LogInformation("{ClassName} dry run completed for {GroupName}, {Count} result(s) resolved",
                nameof(KnxQueryService), groupName, results.Count);

        return new KnxStateChangeResponse
        {
            ChangeId = changeId,
            IsDryRun = dryRun,
            DurationMs = sw.ElapsedMilliseconds,
            Results = [.. results],
        };
    }

    /// <summary>
    /// Sends a value to the KNX bus for a specific function address and polls the feedback
    /// address until the desired value is reached or the polling window expires.
    /// </summary>
    /// <remarks>
    /// Used internally by higher-level helpers such as <see cref="SetLightState"/>,
    /// <see cref="SetShutterState"/>, and <see cref="SetPowerOutletState"/>, and also by
    /// <see cref="KnxAutomationBgService"/> to process queued <see cref="KnxStateChangeItem"/> writes.
    /// </remarks>
    /// <param name="groupName">The base group name (e.g. <c>DG-LI-Office-DL-South</c>).</param>
    /// <param name="function">The function enum value (e.g. <see cref="LightingFunction.VAL"/>).</param>
    /// <param name="feedback">The feedback enum value (e.g. <see cref="LightingFunction.VFB"/>).</param>
    /// <param name="value">The value to send to the bus.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="KnxStateChangeResult"/> describing the outcome.</returns>
    public async Task<KnxStateChangeResult> SendValueAsync(
        string groupName,
        object function,
        object feedback,
        object value,
        CancellationToken cancellationToken = default)
    {
        var functionName = $"{groupName}-{function}";
        var feedbackName = $"{groupName}-{feedback}";

        var kga = knxGroupAddressLookupSvc.GetKGAByName(functionName);
        if (kga is null)
        {
            logger.LogError("{ClassName} function address {GroupAddressName} not found",
                nameof(KnxQueryService), functionName);
            return new KnxStateChangeResult
            {
                GroupAddress = feedbackName,
                Outcome = StateChangeOutcome.NotFound,
            };
        }

        var desiredValue = value.ToString();
        var stateBefore = await knxState.GetKnxState(feedbackName, cancellationToken);
        if (stateBefore is not null && stateBefore.Value == desiredValue)
        {
            logger.LogWarning("{ClassName} {GroupAddress} already has desired value {DesiredValue}, skipping send",
                nameof(KnxQueryService), feedbackName, value);
            return new KnxStateChangeResult
            {
                GroupAddress = feedbackName,
                Outcome = StateChangeOutcome.AlreadyAtDesiredValue,
                State = stateBefore,
            };
        }

        var groupValue = kga.ToGroupValue(value);
        await outgoingBroker.PublishAsync(new KnxOutgoingTelegram { Kga = kga, GroupValueData = groupValue.Value }, cancellationToken);
        logger.LogInformation("{ClassName} sending {DptSubType} desired value {DesiredValue} ({GroupValue}) to {GroupAddressName}",
            nameof(KnxQueryService), kga.GetDptBase(), value, groupValue, kga.Name);

        var lastSeenValue = stateBefore?.Value;
        var staleIterations = 0;
        while (staleIterations < config.Value.StateChangeMaxPollIterations)
        {
            await Task.Delay(config.Value.StateChangePollingDelayMs, cancellationToken);
            var stateAfter = await knxState.GetKnxState(feedbackName, cancellationToken);
            if (stateAfter is null)
            {
                staleIterations++;
                continue;
            }

            if (stateAfter.Value == desiredValue)
            {
                logger.LogInformation("{ClassName} {GroupAddress} reached desired value {DesiredValue}",
                    nameof(KnxQueryService), feedbackName, desiredValue);
                return new KnxStateChangeResult
                {
                    GroupAddress = feedbackName,
                    Outcome = StateChangeOutcome.Changed,
                    State = stateAfter,
                };
            }

            if (stateAfter.Value != lastSeenValue)
            {
                logger.LogDebug("{ClassName} {GroupAddress} transitioning from {ValueBefore} to {ValueAfter}, desired {DesiredValue}",
                    nameof(KnxQueryService), feedbackName, lastSeenValue, stateAfter.Value, desiredValue);
                lastSeenValue = stateAfter.Value;
                staleIterations = 0;
            }
            else
                staleIterations++;
        }

        var finalState = await knxState.GetKnxState(feedbackName, cancellationToken);
        logger.LogWarning("{ClassName} {GroupAddress} did not reach desired value {DesiredValue}, current value is {CurrentValue} after {Iterations} poll(s)",
            nameof(KnxQueryService), feedbackName, desiredValue, finalState?.Value, staleIterations);

        return new KnxStateChangeResult
        {
            GroupAddress = feedbackName,
            Outcome = StateChangeOutcome.NotChanged,
            State = finalState,
        };
    }

    /// <summary>
    /// Sends a value directly to the KNX bus for a given group address name without
    /// feedback polling.
    /// </summary>
    /// <remarks>
    /// Used by <see cref="KnxAutomationBgService"/> to process fire-and-forget
    /// <see cref="KnxStateChangeItem.DirectWrites"/> that do not require confirmation polling.
    /// </remarks>
    /// <param name="groupAddressName">The full group address name (e.g. <c>SYS-[Night_Day]</c>).</param>
    /// <param name="value">The value to send to the bus.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SendDirectAsync(string groupAddressName, object value, CancellationToken cancellationToken = default)
    {
        await knxGroupAddressLookupSvc.GetLookup(cancellationToken);
        var kga = knxGroupAddressLookupSvc.GetKGAByName(groupAddressName);
        if (kga is null)
        {
            logger.LogError("{ClassName} group address {GroupAddressName} not found for direct write",
                nameof(KnxQueryService), groupAddressName);
            return;
        }

        var groupValue = kga.ToGroupValue(value);
        await outgoingBroker.PublishAsync(new KnxOutgoingTelegram { Kga = kga, GroupValueData = groupValue.Value }, cancellationToken);
        logger.LogInformation("{ClassName} direct write {DptSubType} value {Value} ({GroupValue}) to {GroupAddressName}",
            nameof(KnxQueryService), kga.GetDptBase(), value, groupValue, kga.Name);
    }

    /// <summary>
    /// Returns a collection of <see cref="KnxGroupAddressParsed"/> objects, filtered by the specified <see cref="GroupAddressFilter"/>.
    /// </summary>
    /// <param name="groupAddressFilter"><inheritdoc cref="GroupAddressFilter" path="/summary"/></param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A filtered collection of <see cref="KnxGroupAddressParsed"/> objects.</returns>
    public /*partial*/ async Task<IEnumerable<KnxGroupAddressParsed>> GetGroupAddresses(
        GroupAddressFilter groupAddressFilter = GroupAddressFilter.None,
        CancellationToken cancellationToken = default)
    {
        var gaLookup = await knxGroupAddressLookupSvc.GetLookup(cancellationToken);
        if (groupAddressFilter is GroupAddressFilter.None)
            return gaLookup.Values.ToList();

        var allState = await knxState.GetAllState(cancellationToken);
        var keys = allState.Keys;

        if (groupAddressFilter is GroupAddressFilter.Active)
        {
            var activeAddresses = gaLookup.Values.Where(p => keys.Contains(p.Name)).ToList();
            logger.LogInformation("{ClassName} filtering {Count} group addresses to return {ActiveCount} active addresses",
                nameof(KnxQueryService), gaLookup.Count, activeAddresses.Count);
            return activeAddresses;
        }
        else if (groupAddressFilter is GroupAddressFilter.Inactive)
        {
            var inactiveAddresses = gaLookup.Values.Where(p => !keys.Contains(p.Name)).ToList();
            logger.LogInformation("{ClassName} filtering {Count} group addresses to return {InactiveCount} inactive addresses",
                nameof(KnxQueryService), gaLookup.Count, inactiveAddresses.Count);
            return inactiveAddresses;
        }
        else
            throw new NotImplementedException($"GroupAddressFilter {groupAddressFilter} not implemented");
    }

    /// <summary>
    /// Returns the raw unfiltered <see cref="KnxGroupAddressXml"/> entries from the ETS XML export.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of all <see cref="KnxGroupAddressXml"/> entries.</returns>
    public /*partial*/ Task<List<KnxGroupAddressXml>> GetGroupAddressesRaw(CancellationToken cancellationToken = default)
        => knxGroupAddressLookupSvc.GetGroupAddressesRaw(cancellationToken);

    /// <summary>
    /// Returns all parsed group addresses grouped into <see cref="KnxGroupAddressGroup"/> records.
    /// Addresses sharing the same positional and categorical metadata are collected under a single group.
    /// Each child <see cref="KnxGroupAddressGroupFunction"/> is enriched with the last known
    /// <see cref="State"/> values when available.
    /// </summary>
    /// <param name="groupAddressFilter"><inheritdoc cref="GroupAddressFilter" path="/summary"/></param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of <see cref="KnxGroupAddressGroup"/> records.</returns>
    public async Task<List<KnxGroupAddressGroup>> GetGroupAddressesGrouped(
        GroupAddressFilter groupAddressFilter = GroupAddressFilter.None,
        CancellationToken cancellationToken = default)
    {
        var groups = await knxGroupAddressLookupSvc.GetGroupAddressesGrouped(cancellationToken);
        await BindState(groups, cancellationToken);

        if (groupAddressFilter is GroupAddressFilter.Active)
            return groups.Where(g => g.Children.Any(c => c.Value is not null)).ToList();
        else if (groupAddressFilter is GroupAddressFilter.Inactive)
            return groups.Where(g => g.Children.All(c => c.Value is null)).ToList();

        return groups;
    }

    /// <summary>
    /// Turn off all switched-on lights in the house.
    /// </summary>
    /// <returns>An array of group names that will be affected by the change.</returns>
    public async Task<string[]> TurnAllLightsOff(CancellationToken cancellationToken = default)
    {
        var groups = await knxGroupAddressLookupSvc.GetGroupAddressesGrouped(cancellationToken);
        await BindState(groups, cancellationToken);
        var lights = groups.Where(p => p.Category == GroupAddressCategory.LI
            && p.Floor is not null
            && p.Room is not null
            && p.Children.Any(c => c.Function == LightingFunction.SW_FB.ToString() && c?.Value == "True")).ToList();
        var groupNames = lights.Select(p => p.GroupName).ToArray();
        logger.LogInformation("{ClassName} found {Count} lights to turn off {@Lights}",
           nameof(KnxQueryService), groupNames.Length, groupNames);
        foreach (var light in lights)
            await SetLightState(new KnxLightStateChangeRequest { GroupName = light.GroupName, IsOn = false }, cancellationToken: cancellationToken);
        return groupNames;
    }

    /// <summary>
    /// Open all closed shutters in the house.
    /// </summary>
    /// <returns>An array of group names that will be affected by the change.</returns>
    public async Task<string[]> OpenAllShutters(CancellationToken cancellationToken = default)
    {
        var groups = await knxGroupAddressLookupSvc.GetGroupAddressesGrouped(cancellationToken);
        await BindState(groups, cancellationToken);
        var shutters = groups.Where(p => p.Category == GroupAddressCategory.BL
            && p.Floor is not null
            && p.Room is not null
            && p.Orientation is not null).ToList();
        shutters = shutters.Where(p => p.Children.Any(c => c.Function == ShutterFunction.POS_FB.ToString() && c?.Value != "0")).ToList();
        var groupNames = shutters.Select(p => p.GroupName).ToArray();
        logger.LogInformation("{ClassName} found {Count} closed shutters to open {@Shutters}",
            nameof(KnxQueryService), groupNames.Length, groupNames);
        foreach (var shutter in shutters)
            await SetShutterState(new KnxShutterStateChangeRequest { GroupName = shutter.GroupName, VPosition = 0 }, cancellationToken: cancellationToken);
        return groupNames;
    }

    /// <summary>
    /// Close all open shutters in the house.
    /// </summary>
    /// <returns>An array of group names that will be affected by the change.</returns>
    public async Task<string[]> CloseAllShutters(CancellationToken cancellationToken = default)
    {
        var groups = await knxGroupAddressLookupSvc.GetGroupAddressesGrouped(cancellationToken);
        await BindState(groups, cancellationToken);
        var shutters = groups.Where(p => p.Category == GroupAddressCategory.BL
            && p.Floor is not null
            && p.Room is not null
            && p.Children.Any(c => c.Function == ShutterFunction.POS_FB.ToString() && c?.Value != "100")).ToList();
        var groupNames = shutters.Select(p => p.GroupName).ToArray();
        logger.LogInformation("{ClassName} found {Count} opened shutters to close {@Shutters}",
            nameof(KnxQueryService), groupNames.Length, groupNames);
        foreach (var shutter in shutters)
            await SetShutterState(new KnxShutterStateChangeRequest { GroupName = shutter.GroupName, VPosition = 100 }, cancellationToken: cancellationToken);
        return groupNames;
    }

    private async Task BindState(List<KnxGroupAddressGroup> groups, CancellationToken cancellationToken = default)
    {
        var allState = await knxState.GetAllState(cancellationToken);
        foreach (var group in groups)
        {
            foreach (var child in group.Children)
            {
                if (allState.TryGetValue(child.Name, out var state))
                {
                    child.Value = state.Value;
                    child.ValueLabel = state.ValueLabel;
                    child.TimestampUtc = state.TimestampUtc;
                }
            }
        }
    }

    /// <summary>
    /// This method returns the names of group addresses filtered by the specified criteria: <see cref="GroupAddressCategory"/>, <see cref="FloorType"/> and <see cref="CompassOrientation"/>.
    /// </summary>
    /// <param name="category">The <see cref="GroupAddressCategory"/> to filter by.</param>
    /// <param name="floor">The <see cref="FloorType"/> to filter by (e.g. EG, OG, DG, KG).</param>
    /// <param name="orientation">The <see cref="CompassOrientation"/> to filter by (e.g. North, East, South, West).</param>
    /// <param name="function">The group address function to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of matching group address names.</returns>
    public async Task<List<string>> FilterGroupAddresses(
        string? category = null,
        string? floor = null,
        string? orientation = null,
        string? function = null,
        CancellationToken cancellationToken = default)
    {
        var categoryType = category is not null ? category.ParseEnum<GroupAddressCategory>() : (GroupAddressCategory?)null;
        var floorType = floor is not null ? floor.ParseEnum<FloorType>() : (FloorType?)null;
        var compassOrientation = orientation is not null ? orientation.ParseEnum<CompassOrientation>() : (CompassOrientation?)null;
        var groups = await GetFilteredGroupAddresses(categoryType, floorType, compassOrientation, function, cancellationToken);
        return groups
            .SelectMany(g => g.Children)
            .Select(c => c.Name)
            .ToList();
    }

    private async Task<List<KnxGroupAddressGroup>> GetFilteredGroupAddresses(
        GroupAddressCategory? category = null,
        FloorType? floor = null,
        CompassOrientation? orientation = null,
        string? function = null,
        CancellationToken cancellationToken = default)
    {
        var groups = await knxGroupAddressLookupSvc.GetGroupAddressesGrouped(cancellationToken);
        var filtered = groups
            .Where(p => category is null || p.Category == category)
            .Where(p => floor is null || p.Floor == floor)
            .Where(p => orientation is null || p.Orientation == orientation);

        if (function is not null)
        {
            filtered = filtered
                .Select(g => g with { Children = g.Children.Where(c => c.Function == function).ToList() })
                .Where(g => g.Children.Count > 0);
        }

        return filtered.ToList();
    }

    /// <summary>
    /// Returns the distinct floors in the house ordered from top to bottom (DG, OG, EG, KG).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of <see cref="KnxRoom"/> records.</returns>
    public async Task<List<KnxRoom>> ListFloors(CancellationToken cancellationToken = default)
    {
        var groups = await knxGroupAddressLookupSvc.GetGroupAddressesGrouped(cancellationToken);
        return groups
            .Where(p => p.Floor is not null)
            .Select(p => p.Floor!.Value)
            .Distinct()
            .OrderByFloor()
            .Select(f => new KnxRoom { Floor = f })
            .ToList();
    }

    /// <summary>
    /// Returns the distinct rooms in the house grouped by floor, ordered from top to bottom.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of <see cref="KnxRoom"/> records.</returns>
    public async Task<List<KnxRoom>> ListRooms(CancellationToken cancellationToken = default)
    {
        var groups = await knxGroupAddressLookupSvc.GetGroupAddressesGrouped(cancellationToken);
        return groups
            .Where(p => p.Floor is not null && p.Room is not null)
            .Select(p => new { p.Floor, p.Room })
            .Distinct()
            .OrderByFloor(p => p.Floor!.Value)
            .ThenBy(p => p.Room.ToString())
            .Select(p => new KnxRoom { Floor = p.Floor!.Value, Room = p.Room!.Value })
            .ToList();
    }

    /// <summary>
    /// Returns all shutters/blinds in the house with current state, optionally filtered by room, grouped by floor.
    /// </summary>
    /// <param name="room">Optional room name to filter by (e.g. Kitchen, LivingRoom).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of <see cref="KnxShutter"/> records with bound state values.</returns>
    public async Task<List<KnxShutter>> ListShutters(
        string? room = null,
        CancellationToken cancellationToken = default)
    {
        var roomType = room is not null ? room.ParseEnum<RoomType>() : (RoomType?)null;
        var groups = await knxGroupAddressLookupSvc.GetGroupAddressesGrouped(cancellationToken);
        var filtered = groups
            .Where(p => p.Category == GroupAddressCategory.BL
                && p.Floor is not null
                && p.Room is not null
                && (roomType is null || p.Room == roomType))
            .OrderByFloor(p => p.Floor!.Value)
            .ThenBy(p => p.Room.ToString())
            .ThenBy(p => p.GroupName)
            .ToList();
        await BindState(filtered, cancellationToken);
        return filtered.Select(KnxShutter.FromGroup).ToList();
    }

    /// <summary>
    /// Returns the current state of a specific shutter/blind including position,
    /// slats position, direction and diagnostics.
    /// </summary>
    /// <param name="groupName">The shutter group name to look up (e.g. <c>OG-BL-FamilyBathroom-West</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching <see cref="KnxShutter"/> with bound state, or <see langword="null"/> if not found.</returns>
    public async Task<KnxShutter?> GetShutter(
        string groupName,
        CancellationToken cancellationToken = default)
    {
        var groups = await knxGroupAddressLookupSvc.GetGroupAddressesGrouped(cancellationToken);
        var group = groups.FirstOrDefault(g => g.GroupName == groupName && g.Category == GroupAddressCategory.BL);
        if (group is null)
            return null;

        await BindState([group], cancellationToken);
        return KnxShutter.FromGroup(group);
    }

    /// <summary>
    /// Returns all lights in the house with current state, optionally filtered by room, grouped by floor.
    /// </summary>
    /// <param name="room">Optional room name to filter by (e.g. Kitchen, LivingRoom).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of <see cref="KnxLight"/> records with bound state values.</returns>
    public async Task<List<KnxLight>> ListLights(
        string? room = null,
        CancellationToken cancellationToken = default)
    {
        var roomType = room is not null ? room.ParseEnum<RoomType>() : (RoomType?)null;
        var groups = await knxGroupAddressLookupSvc.GetGroupAddressesGrouped(cancellationToken);
        var filtered = groups
            .Where(p => p.Category == GroupAddressCategory.LI
                && p.Floor is not null
                && p.Room is not null
                && (roomType is null || p.Room == roomType))
            .OrderByFloor(p => p.Floor!.Value)
            .ThenBy(p => p.Room.ToString())
            .ThenBy(p => p.GroupName)
            .ToList();
        await BindState(filtered, cancellationToken);
        return filtered.Select(KnxLight.FromGroup).ToList();
    }

    /// <summary>
    /// Returns the current state of a specific light including switch state,
    /// dimming value, RGB, HSV and lux values.
    /// </summary>
    /// <param name="groupName">The lighting group name to look up (e.g. <c>DG-LI-Office-DL-South</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching <see cref="KnxLight"/> with bound state, or <see langword="null"/> if not found.</returns>
    public async Task<KnxLight?> GetLight(
        string groupName,
        CancellationToken cancellationToken = default)
    {
        var groups = await knxGroupAddressLookupSvc.GetGroupAddressesGrouped(cancellationToken);
        var group = groups.FirstOrDefault(g => g.GroupName == groupName && g.Category == GroupAddressCategory.LI);
        if (group is null)
            return null;

        await BindState([group], cancellationToken);
        return KnxLight.FromGroup(group);
    }

    /// <summary>
    /// Returns all switchable power outlets in the house with current state, optionally filtered by room, grouped by floor.
    /// </summary>
    /// <param name="room">Optional room name to filter by (e.g. Office, LivingRoom).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of <see cref="KnxPowerOutlet"/> records with bound state values.</returns>
    public async Task<List<KnxPowerOutlet>> ListPowerOutlets(
        string? room = null,
        CancellationToken cancellationToken = default)
    {
        var roomType = room is not null ? room.ParseEnum<RoomType>() : (RoomType?)null;
        var groups = await knxGroupAddressLookupSvc.GetGroupAddressesGrouped(cancellationToken);
        var filtered = groups
            .Where(p => p.Category == GroupAddressCategory.SD
                && p.Floor is not null
                && p.Room is not null
                && (roomType is null || p.Room == roomType))
            .OrderByFloor(p => p.Floor!.Value)
            .ThenBy(p => p.Room.ToString())
            .ThenBy(p => p.GroupName)
            .ToList();
        await BindState(filtered, cancellationToken);
        return filtered.Select(KnxPowerOutlet.FromGroup).ToList();
    }

    /// <summary>
    /// Returns the current state of a specific power outlet.
    /// </summary>
    /// <param name="groupName">The power outlet group name to look up (e.g. <c>DG-SD-Office</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching <see cref="KnxPowerOutlet"/> with bound state, or <see langword="null"/> if not found.</returns>
    public async Task<KnxPowerOutlet?> GetPowerOutlet(
        string groupName,
        CancellationToken cancellationToken = default)
    {
        var groups = await knxGroupAddressLookupSvc.GetGroupAddressesGrouped(cancellationToken);
        var group = groups.FirstOrDefault(g => g.GroupName == groupName && g.Category == GroupAddressCategory.SD);
        if (group is null)
            return null;

        await BindState([group], cancellationToken);
        return KnxPowerOutlet.FromGroup(group);
    }

    /// <summary>
    /// Validates whether a group name (e.g. <c>OG-BL-FamilyBathroom-West</c>) exists and returns
    /// the matching <see cref="KnxGroupAddressGroup"/> with all its child functions, or
    /// <see langword="null"/> if not found.
    /// </summary>
    /// <param name="groupName">The group name to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching <see cref="KnxGroupAddressGroup"/> or <see langword="null"/>.</returns>
    public async Task<KnxGroupAddressGroup?> ValidateGroupName(
        string groupName,
        CancellationToken cancellationToken = default)
    {
        var groups = await knxGroupAddressLookupSvc.GetGroupAddressesGrouped(cancellationToken);
        return groups.FirstOrDefault(g => g.GroupName == groupName);
    }

    /// <summary>
    /// Validates whether a full group address name (e.g. <c>EG-LI-Entrance-DL-SW</c>) or a
    /// numeric group address (e.g. <c>1/2/3</c>) exists and returns the matching
    /// <see cref="KnxGroupAddressParsed"/>, or <see langword="null"/> if not found.
    /// </summary>
    /// <param name="groupAddress">The full group address name or numeric address to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching <see cref="KnxGroupAddressParsed"/> or <see langword="null"/>.</returns>
    public async Task<KnxGroupAddressParsed?> ValidateGroupAddress(
        string groupAddress,
        CancellationToken cancellationToken = default)
    {
        await knxGroupAddressLookupSvc.GetLookup(cancellationToken);
        return groupAddress.Contains('/')
            ? knxGroupAddressLookupSvc.GetKGAByAddress(groupAddress)
            : knxGroupAddressLookupSvc.GetKGAByName(groupAddress);
    }

    /// <summary>
    /// When we want an extra special magic number, this is it!
    /// </summary>
    /// <returns>A magic number</returns>
    public int LetsTestXmlComments()
    {
        return 1337;
    }

    /// <summary>
    /// Returns all KNX Group Addresses state including values and timestamps.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary keyed by group address name.</returns>
    public /*partial*/ Task<Dictionary<string, State>> GetAllState(CancellationToken cancellationToken = default)
        => knxState.GetAllState(cancellationToken);

    /// <summary>
    /// Returns all lighting and the current state of each light.
    /// </summary>
    /// <param name="floor">The <see cref="FloorType"/> to filter by (e.g. EG, OG, DG, KG).</param>
    /// <param name="orientation">The <see cref="CompassOrientation"/> to filter by (e.g. North, East, South, West).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary keyed by group address name.</returns>
    public async Task<Dictionary<string, State?>> GetAllLighting(
        string? floor = null,
        string? orientation = null,
        CancellationToken cancellationToken = default)
    {
        var floorType = floor is not null ? floor.ParseEnum<FloorType>() : (FloorType?)null;
        var compassOrientation = orientation is not null ? orientation.ParseEnum<CompassOrientation>() : (CompassOrientation?)null;
        return await GetAllLighting(floorType, compassOrientation, cancellationToken);
    }

    /// <inheritdoc cref="GetAllLighting(string?, string?, CancellationToken)"/>
    public async Task<Dictionary<string, State?>> GetAllLighting(FloorType? floor = null,
        CompassOrientation? orientation = null,
        CancellationToken cancellationToken = default)
    {
        var groups = await GetFilteredGroupAddresses(GroupAddressCategory.LI, floor, orientation, function: LightingFunction.SW_FB.ToString(), cancellationToken);
        await BindState(groups, cancellationToken);
        groups = groups.Where(p => p.Category == GroupAddressCategory.LI).ToList();
        var d = groups
            .SelectMany(g => g.Children)
            .ToDictionary(c => c.Name, c => c.Value is null ? null : new State
            {
                GroupAddress = c.Name,
                Value = c.Value,
                ValueLabel = c.ValueLabel,
                TimestampUtc = c.TimestampUtc!.Value
            });
        return d;
    }

    /// <summary>
    /// Returns all HVAC zones in the house with current state, optionally filtered by room, grouped by floor.
    /// </summary>
    /// <param name="room">Optional room name to filter by (e.g. Office, LivingRoom).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of <see cref="KnxHvacZone"/> records with bound state values.</returns>
    public async Task<List<KnxHvacZone>> ListHvacZones(
        string? room = null,
        CancellationToken cancellationToken = default)
    {
        var roomType = room is not null ? room.ParseEnum<RoomType>() : (RoomType?)null;
        var groups = await knxGroupAddressLookupSvc.GetGroupAddressesGrouped(cancellationToken);
        var filtered = groups
            .Where(p => p.Category == GroupAddressCategory.HZ
                && p.Floor is not null
                && p.Room is not null
                && (roomType is null || p.Room == roomType))
            .OrderByFloor(p => p.Floor!.Value)
            .ThenBy(p => p.Room.ToString())
            .ThenBy(p => p.GroupName)
            .ToList();
        await BindState(filtered, cancellationToken);
        return filtered.Select(KnxHvacZone.FromGroup).ToList();
    }

    /// <summary>
    /// Returns the current state of a specific HVAC zone including setpoint, temperature
    /// and valve feedback values.
    /// </summary>
    /// <param name="groupName">The HVAC group name to look up (e.g. <c>EG-HZ-Kitchen</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching <see cref="KnxHvacZone"/> with bound state, or <see langword="null"/> if not found.</returns>
    public async Task<KnxHvacZone?> GetHvacZone(
        string groupName,
        CancellationToken cancellationToken = default)
    {
        var groups = await knxGroupAddressLookupSvc.GetGroupAddressesGrouped(cancellationToken);
        var group = groups.FirstOrDefault(g => g.GroupName == groupName && g.Category == GroupAddressCategory.HZ);
        if (group is null)
            return null;

        await BindState([group], cancellationToken);
        return KnxHvacZone.FromGroup(group);
    }
}
