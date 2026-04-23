using Knx.Falcon;

namespace CasCap.Services;

/// <summary>
/// Handles the exported KNX Group Address file from ETS software, the file contents
/// are parsed, validated and converted into DTO.
/// </summary>
public class KnxGroupAddressLookupService(ILogger<KnxGroupAddressLookupService> logger, IOptions<KnxConfig> config, IHostEnvironment environment, KnxGroupAddressLookupHealthCheck knxGroupAddressLookupHealthCheck)
{

    private Dictionary<string, KnxGroupAddressParsed> dLookupByAddress { get; set; } = [];
    private Dictionary<string, KnxGroupAddressParsed> dLookupByName { get; set; } = [];
    private List<KnxGroupAddressXml> _xmlGroupAddresses = [];

    private static readonly SemaphoreSlim semaphoreSlim = new(1, 1);

    /// <summary>
    /// Populates and returns the dictionary of parsed group addresses keyed by address.
    /// </summary>
    public async Task<Dictionary<string, KnxGroupAddressParsed>> GetLookup(CancellationToken cancellationToken = default)
    {
        if (dLookupByAddress.Count == 0)
        {
            await semaphoreSlim.WaitAsync(cancellationToken);
            if (dLookupByAddress.Count > 0) return dLookupByAddress;
            await PopulateLookup(cancellationToken);
            if (dLookupByAddress.Count > 0)
                knxGroupAddressLookupHealthCheck.GroupAddressesLoaded = true;
            else
                logger.LogCritical("{ClassName} group address lookup is empty", nameof(KnxGroupAddressLookupService));
            semaphoreSlim.Release();
        }
        return dLookupByAddress;
    }

    /// <summary>
    /// Returns the <see cref="KnxGroupAddressXml"/> entries from the ETS XML export, excluding
    /// placeholder addresses whose name contains a '?' character. These placeholders are
    /// reserved slots in ETS that carry no meaningful configuration and are filtered out
    /// during loading.
    /// Ensures the lookup has been populated first.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of <see cref="KnxGroupAddressXml"/> entries with placeholders removed.</returns>
    public async Task<List<KnxGroupAddressXml>> GetGroupAddressesRaw(CancellationToken cancellationToken = default)
    {
        await GetLookup(cancellationToken);
        return _xmlGroupAddresses;
    }

    /// <summary>
    /// Returns all parsed <see cref="KnxGroupAddressParsed"/> entries grouped into
    /// <see cref="KnxGroupAddressGroup"/> records. Addresses that share the same positional
    /// and categorical metadata (everything except the function suffix) are collected
    /// under a single group.
    /// Ensures the lookup has been populated first.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of <see cref="KnxGroupAddressGroup"/> records.</returns>
    public async Task<List<KnxGroupAddressGroup>> GetGroupAddressesGrouped(CancellationToken cancellationToken = default)
    {
        var lookup = await GetLookup(cancellationToken);
        return BuildGroups(lookup.Values);
    }

    /// <summary>
    /// Retrieve a KnxGroupAddressParsed object by its address.
    /// e.g. 1/2/3
    /// </summary>
    public KnxGroupAddressParsed? GetKGAByAddress(string GroupAddress, [CallerMemberName] string? caller = null)
    {
        if (dLookupByAddress.TryGetValue(GroupAddress, out var kga))
            return kga;
        logger.LogCritical("{ClassName} group address '{GroupAddress}' not found in lookup, export a fresh XML file!? (Called from '{Caller}')",
            nameof(KnxGroupAddressLookupService), GroupAddress, caller);
        return null;
    }

    /// <summary>
    /// Retrieve a KnxGroupAddressParsed object by its name.
    /// e.g. EG-LI-Entrance-DL-SW
    /// </summary>
    public KnxGroupAddressParsed? GetKGAByName(string GroupAddressName, [CallerMemberName] string? caller = null)
    {
        if (dLookupByName.TryGetValue(GroupAddressName, out var kga))
            return kga;
        logger.LogCritical("{ClassName} group address '{GroupAddressName}' not found! Export a fresh XML file!? (Called from '{Caller}')",
            nameof(KnxGroupAddressLookupService), GroupAddressName, caller);
        //throw new GenericException($"group address name '{GroupAddressName}' not found in lookup!");
        return null;
    }

    /// <summary>
    /// Deserializes the ETS XML export file from disk and returns every
    /// <see cref="KnxGroupAddressXml"/> entry, including placeholder addresses whose name
    /// contains a '?' character. This is primarily intended for tests that need to verify
    /// the deserialization against the raw XML element count.
    /// </summary>
    /// <param name="path">Optional file path. Defaults to <c>knxgroupaddresses.xml</c> in <see cref="AppDomain.BaseDirectory"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of all <see cref="KnxGroupAddressXml"/> entries including placeholders.</returns>
    public static async Task<List<KnxGroupAddressXml>> DeserializeGroupAddressesFromFile(string? path = null, CancellationToken cancellationToken = default)
    {
        var fullPath = path ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "knxgroupaddresses.xml");
        var xml = await File.ReadAllTextAsync(fullPath, cancellationToken);
        var gae = xml.FromXml<KnxGroupAddressXmlExport>();
        return (gae ?? throw new InvalidOperationException("Failed to deserialize group address XML.")).GroupRange
            .SelectMany(r1 => r1.GroupRange)
            .SelectMany(r2 => r2.GroupAddress)
            .ToList();
    }

    private async Task PopulateLookup(CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var gae = await LoadGroupAddressesFromFile(cancellationToken);
        if (gae is not null)
        {
            _xmlGroupAddresses = gae.GroupRange.SelectMany(p => p.GroupRange.SelectMany(q => q.GroupAddress))
                .Where(p => !p.Name.Contains('?')).ToList();//filter out test/holding addresses
            var deDupe = new Dictionary<string, object?>();
            var duplicates = false;
            foreach (var xga in _xmlGroupAddresses)
            {
                if (xga.DPTs is null || !xga.DPTs.StartsWith("DPST-")) continue;
                var kga = new KnxGroupAddressParsed(xga);
                if (kga.Category == GroupAddressCategory.Unknown)
                {
                    logger.LogWarning("{ClassName} XML group address category is unknown '{GroupAddressName}'", nameof(KnxGroupAddressLookupService), kga.Name);
                    continue;
                }
                else if (!deDupe.TryAdd(kga.Name, null))
                {
                    logger.LogCritical("{ClassName} duplicate group address name detected '{GroupAddressName}'", nameof(KnxGroupAddressLookupService), kga.Name);
                    duplicates = true;
                }
                if (!dLookupByAddress.TryAdd(kga.GroupAddress, kga))
                    throw new GenericException($"duplicate group address detected '{kga.GroupAddress}'");
                if (!dLookupByName.TryAdd(kga.Name, kga))
                    throw new GenericException($"duplicate group address name detected '{kga.Name}'");
            }
            if (duplicates)
                throw new GenericException("duplicate group address names detected!");
            logger.LogInformation("{ClassName} {Count} filtered group addresses loaded in {ElapsedMilliseconds}ms",
                nameof(KnxGroupAddressLookupService), dLookupByAddress.Count, sw.ElapsedMilliseconds);
        }
    }

    private async Task<KnxGroupAddressXmlExport?> LoadGroupAddressesFromFile(CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        KnxGroupAddressXmlExport? gae = null;
        var fullPath = environment.IsDevelopment()
            ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, config.Value.GroupAddressXmlFilePath)
            : config.Value.GroupAddressXmlFilePath;

        if (File.Exists(fullPath))
        {
            var str = await File.ReadAllTextAsync(fullPath, cancellationToken);
            gae = str.FromXml<KnxGroupAddressXmlExport>();
            if (gae is not null)
            {
                var count = gae.GroupRange.SelectMany(p => p.GroupRange.SelectMany(q => q.GroupAddress)).Count();
                logger.LogInformation("{ClassName} XML group address file '{FullPath}' contains '{Count}' unfiltered addresses, loaded in {ElapsedMilliseconds}ms",
                    nameof(KnxGroupAddressLookupService), fullPath, count, sw.ElapsedMilliseconds);
            }
        }
        else
        {
            logger.LogCritical("{ClassName} XML group address file not found at '{FullPath}'", nameof(KnxGroupAddressLookupService), fullPath);
        }
        return gae;
    }

    /// <summary>Logs a summary of datapoint type usage across all loaded group addresses.</summary>
    public void DptSummary()
    {
        var summary = from p in dLookupByAddress.Values
                      where 1 == 1 && p.Major > 0
                      group p by new
                      {
                          //p.Category,
                          p.DPTs,
                          DPTName = p.GetDptBase().DatapointSubtype.ToString()
                      } into g
                      orderby /*g.Key.Category,*/ g.Count() descending
                      select new
                      {
                          //g.Key.Category,
                          g.Key.DPTs,
                          g.Key.DPTName,
                          count = g.Count()
                      };
        foreach (var s in summary)
        {
            logger.LogDebug("{Count}\t{DPTs}\t{DPTName}", s.count, s.DPTs, s.DPTName);
            //logger.LogDebug("{Count}\t{Category}\t{DPTs}\t{DPTName}", s.count, s.Category, s.DPTs, s.DPTName);
        }
    }

    /// <summary>Logs a frequency summary of name segments split by <c>-</c> across all group addresses.</summary>
    public void SplitNameSummary()
    {
        var l = new List<string>();
        foreach (var val in dLookupByAddress.Values)
        {
            //logger.LogDebug(val.Name);
            var sections = val.Name.Split('-');
            foreach (var section in sections)
            {
                l.Add(section);
                //d.TryAdd(section, null);
            }
        }

        var summary = from s in l
                      where 1 == 1
                      group s by new
                      {
                          s
                      } into g
                      orderby g.Count() descending
                      select new
                      {
                          g.Key.s,
                          count = g.Count()
                      };
        foreach (var s in summary)
        {
            logger.LogDebug("{Count}\t{S}", s.count, s.s);
        }
    }

    /// <summary>
    /// Groups a collection of <see cref="KnxGroupAddressParsed"/> records into
    /// <see cref="KnxGroupAddressGroup"/> records by stripping the function suffix
    /// from each name and collecting addresses that share the same prefix.
    /// </summary>
    internal static List<KnxGroupAddressGroup> BuildGroups(IEnumerable<KnxGroupAddressParsed> addresses)
    {
        var groups = new Dictionary<string, List<KnxGroupAddressParsed>>();
        foreach (var kga in addresses)
        {
            var parentName = GetGroupName(kga);
            if (!groups.TryGetValue(parentName, out var list))
            {
                list = [];
                groups[parentName] = list;
            }
            list.Add(kga);
        }

        return groups.Select(kvp =>
        {
            var first = kvp.Value[0];
            return new KnxGroupAddressGroup
            {
                GroupName = kvp.Key,
                Category = first.Category,
                Floor = first.Floor,
                Room = first.Room,
                Location = first.Location,
                Orientation = first.CompassDirection,
                HorizontalPosition = first.HorizontalPosition,
                VerticalPosition = first.VerticalPosition,
                LightStyle = first.LightStyle,
                Identifier = first.Identifier,
                IsOutside = first.IsOutside,
                Children = kvp.Value.Select(kga => new KnxGroupAddressGroupFunction
                {
                    Name = kga.Name,
                    GroupAddress = kga.GroupAddress,
                    Function = kga.Function,
                    IsFeedback = kga.IsFeedback,
                    DPTs = kga.DPTs,
                    Major = kga.Major,
                    Minor = kga.Minor,
                }).ToList(),
            };
        }).ToList();
    }

    /// <summary>
    /// Derives the group name by removing the function suffix from the
    /// <see cref="KnxGroupAddressParsed.Name"/>. For addresses without a parsed function
    /// (e.g. <see cref="GroupAddressCategory.PM"/> base sensors) the full name is returned.
    /// </summary>
    private static string GetGroupName(KnxGroupAddressParsed kga)
    {
        if (kga.Function is null)
            return kga.Name;

        var name = kga.Name;
        var suffixLength = kga.Function.Length + 1; // +1 for the hyphen
        return name.Length > suffixLength
            ? name[..^suffixLength]
            : name;
    }
}
