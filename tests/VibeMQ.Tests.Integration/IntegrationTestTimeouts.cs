namespace VibeMQ.Tests.Integration;

/// <summary>
/// Shared timeout and delay values for integration tests.
/// Values are tuned to reduce idle waiting while staying stable on CI.
/// </summary>
internal static class IntegrationTestTimeouts {
    public static readonly TimeSpan BrokerStartupTimeout = TimeSpan.FromSeconds(6);
    public static readonly TimeSpan BrokerShutdownWaitTimeout = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan ClientCommandTimeout = TimeSpan.FromSeconds(4);
    public static readonly TimeSpan MessageDeliveryTimeout = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan MultipleMessagesDeliveryTimeout = TimeSpan.FromSeconds(6);

    public const int PortProbeRetryDelayMs = 25;
    public const int SubscriptionActivationDelayMs = 50;
    public const int PostUnsubscribePropagationDelayMs = 150;
    public const int HostedServiceSubscriptionDelayMs = 250;
    public const int HostedServiceDeliveryDelayMs = 500;
}
