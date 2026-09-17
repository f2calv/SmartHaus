using StackExchange.Redis;
using System.Reflection;

namespace CasCap.Tests.Unit;

/// <summary>
/// An <see cref="IDatabase"/> whose every operation is a no-op, so the comms stream consumer starts
/// and idles without a Redis server.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CasCap.Services.CommunicationsBgService"/> creates its consumer group before anything
/// else, so the stream path cannot be avoided when driving the service through its public
/// <c>ExecuteAsync</c> surface. Reads return empty, which parks the consumer on its polling delay.
/// </para>
/// <para>
/// <see cref="DispatchProxy"/> is used rather than several hundred hand-written members; it is part
/// of the base class library, so this introduces no mocking framework. It generates a derived proxy
/// type at runtime, so this class cannot be sealed.
/// </para>
/// </remarks>
public class StubRedisDatabase : DispatchProxy
{
    /// <summary>Creates an inert database proxy.</summary>
    public static IDatabase Create() => Create<IDatabase, StubRedisDatabase>()!;

    /// <inheritdoc/>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var returnType = targetMethod?.ReturnType;
        if (returnType is null || returnType == typeof(void))
            return null;
        if (returnType == typeof(Task))
            return Task.CompletedTask;
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var resultType = returnType.GetGenericArguments()[0];
            return typeof(Task).GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(resultType)
                .Invoke(null, [Empty(resultType)]);
        }
        return Empty(returnType);
    }

    //An empty array rather than null, so a caller reading the stream can inspect Length safely.
    private static object? Empty(Type type) =>
        type.IsArray ? Array.CreateInstance(type.GetElementType()!, 0)
        : type.IsValueType ? Activator.CreateInstance(type)
        : null;
}
