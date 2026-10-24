using CasCap.Abstractions;
using CasCap.Services;
using CasCap.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using System.Net;

namespace CasCap.Tests;

/// <summary>Checks generated MCP schemas and facade behavior against the fake appliance provider.</summary>
[Trait("Category", "Integration")]
public sealed class MieleMcpContractTests(MieleApiWebApplicationFactory factory) : IClassFixture<MieleApiWebApplicationFactory>
{
    [Theory]
    [InlineData(nameof(AppliancesMcpQueryService.GetAllAppliances), "get_all_appliances", null)]
    [InlineData(nameof(AppliancesMcpQueryService.GetAllAppliancesSummary), "get_all_appliances_summary", null)]
    [InlineData(nameof(AppliancesMcpQueryService.GetAppliance), "get_appliance", null)]
    [InlineData(nameof(AppliancesMcpQueryService.GetApplianceIdentification), "get_appliance_identification", null)]
    [InlineData(nameof(AppliancesMcpQueryService.GetApplianceState), "get_appliance_state", null)]
    [InlineData(nameof(AppliancesMcpQueryService.GetApplianceActions), "get_appliance_actions", null)]
    [InlineData(nameof(AppliancesMcpQueryService.GetAppliancePrograms), "get_appliance_programs", null)]
    [InlineData(nameof(AppliancesMcpQueryService.ExecuteApplianceAction), "execute_appliance_action", "action")]
    [InlineData(nameof(AppliancesMcpQueryService.StartApplianceProgram), "start_appliance_program", "programRequest")]
    public void ToolSchemas(string methodName, string expectedName, string? commandParameter)
    {
        var method = typeof(AppliancesMcpQueryService).GetMethod(methodName)!;
        var tool = McpServerTool.Create(method, _ => null!).ProtocolTool;
        var properties = tool.InputSchema.GetProperty("properties");

        Assert.Equal(expectedName, tool.Name);
        Assert.False(properties.TryGetProperty("cancellationToken", out _));
        if (commandParameter is null)
            return;

        var command = properties.GetProperty(commandParameter);
        Assert.Equal("object", command.GetProperty("type").GetString());
        var fields = command.GetProperty("properties");
        Assert.True(fields.TryGetProperty("programId", out _));
        Assert.False(fields.TryGetProperty("runOnTime", out _));
        Assert.False(tool.Annotations!.ReadOnlyHint);
        Assert.True(tool.Annotations.DestructiveHint);
        Assert.False(tool.Annotations.IdempotentHint);
        if (commandParameter == "action")
        {
            Assert.True(fields.TryGetProperty("powerOn", out _));
            Assert.True(fields.TryGetProperty("targetTemperature", out _));
        }
        else
            Assert.Contains(command.GetProperty("required").EnumerateArray(), field => field.GetString() == "programId");
    }

    [Theory]
    [InlineData(nameof(AppliancesMcpQueryService.GetAllAppliances))]
    [InlineData(nameof(AppliancesMcpQueryService.GetAllAppliancesSummary))]
    [InlineData(nameof(AppliancesMcpQueryService.GetAppliance))]
    [InlineData(nameof(AppliancesMcpQueryService.GetApplianceIdentification))]
    [InlineData(nameof(AppliancesMcpQueryService.GetApplianceState))]
    [InlineData(nameof(AppliancesMcpQueryService.GetApplianceActions))]
    [InlineData(nameof(AppliancesMcpQueryService.GetAppliancePrograms))]
    [InlineData(nameof(AppliancesMcpQueryService.ExecuteApplianceAction))]
    [InlineData(nameof(AppliancesMcpQueryService.StartApplianceProgram))]
    public async Task Cancellation(string methodName)
    {
        var tools = CreateTools();
        factory.Provider.Reset(block: true);
        using var stopping = new CancellationTokenSource();
        var token = stopping.Token;
        Task operation = methodName switch
        {
            nameof(AppliancesMcpQueryService.GetAllAppliances) => tools.GetAllAppliances(cancellationToken: token),
            nameof(AppliancesMcpQueryService.GetAllAppliancesSummary) => tools.GetAllAppliancesSummary(cancellationToken: token),
            nameof(AppliancesMcpQueryService.GetAppliance) => tools.GetAppliance("example", cancellationToken: token),
            nameof(AppliancesMcpQueryService.GetApplianceIdentification) => tools.GetApplianceIdentification("example", cancellationToken: token),
            nameof(AppliancesMcpQueryService.GetApplianceState) => tools.GetApplianceState("example", cancellationToken: token),
            nameof(AppliancesMcpQueryService.GetApplianceActions) => tools.GetApplianceActions("example", token),
            nameof(AppliancesMcpQueryService.GetAppliancePrograms) => tools.GetAppliancePrograms("example", cancellationToken: token),
            nameof(AppliancesMcpQueryService.ExecuteApplianceAction) => tools.ExecuteApplianceAction("example", new() { PowerOn = true }, token),
            nameof(AppliancesMcpQueryService.StartApplianceProgram) => tools.StartApplianceProgram("example", new() { ProgramId = 24 }, token),
            _ => throw new ArgumentOutOfRangeException(nameof(methodName)),
        };
        await factory.Provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await stopping.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        await factory.Provider.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Single(factory.Provider.Calls);
    }

    [Theory]
    [InlineData(false, HttpStatusCode.NoContent, true)]
    [InlineData(false, HttpStatusCode.BadRequest, false)]
    [InlineData(false, HttpStatusCode.NotFound, false)]
    [InlineData(true, HttpStatusCode.NoContent, true)]
    [InlineData(true, HttpStatusCode.BadRequest, false)]
    [InlineData(true, HttpStatusCode.NotFound, false)]
    public async Task CommandResults(bool program, HttpStatusCode status, bool expected)
    {
        var tools = CreateTools();
        factory.Provider.Reset(status);
        var result = program
            ? await tools.StartApplianceProgram("example", new() { ProgramId = 24 }, TestContext.Current.CancellationToken)
            : await tools.ExecuteApplianceAction("example", new() { PowerOn = true }, TestContext.Current.CancellationToken);

        Assert.Equal(expected, result);
        Assert.Single(factory.Provider.Calls);
    }

    private AppliancesMcpQueryService CreateTools()
        => new(factory.Services.GetRequiredService<IMieleQueryService>());
}
