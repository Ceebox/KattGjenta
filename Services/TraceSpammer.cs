using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace KattGjenta;

public sealed class TraceSpammer : BackgroundService
{
    private readonly ILogger<TraceSpammer> mLogger;
    private readonly ActivitySource mActivitySource = new(nameof(TraceSpammer));
    private readonly Random mRandom = new();
    private readonly TracingConfig mConfig;

    private volatile bool mIsRunning = false;

    private TracerProvider? mTracerProvider;

    public TraceSpammer(ILogger<TraceSpammer> logger, TracingConfig config)
    {
        mLogger = logger;
        mConfig = config;

        this.BuildTracerProvider();
    }

    public TracingConfig Config => mConfig;

    public bool IsRunning => mIsRunning;

    public void Start()
    {
        mIsRunning = true;
    }

    public void Stop()
    {
        mIsRunning = false;
    }

    public void UpdateConfig()
    {
        this.BuildTracerProvider();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (this.IsRunning && mConfig.Rate > 0)
            {
                var perSecond = mConfig.Rate;
                var interval = TimeSpan.FromSeconds(1.0 / perSecond);

                for (var i = 0; i < mConfig.Rate; i++)
                {
                    if (!this.IsRunning)
                    {
                        break;
                    }

                    using var rootActivity = mActivitySource.StartActivity($"Parent - {DateTime.Now.ToLongTimeString()}", ActivityKind.Internal);
                    rootActivity?.SetTag("spammer.id", Guid.NewGuid().ToString());
                    rootActivity?.SetTag("timestamp", DateTime.UtcNow);

                    await this.CreateChildSpansAsync(
                        rootActivity,
                        mConfig.TraceDepth,
                        mConfig.ChildrenPerNode,
                        mConfig.MinChildDurationMs,
                        mConfig.MaxChildDurationMs
                    );

                    await Task.Delay(interval, stoppingToken);
                }
            }
            else
            {
                await Task.Delay(500, stoppingToken);
            }
        }
    }

    /// <summary>
    /// Recursively creates child spans under the given parent activity.
    /// </summary>
    /// <param name="parent">Parent activity.</param>
    /// <param name="depth">Remaining depth.</param>
    /// <param name="children">Amount of children to spawn per node.</param>
    /// <param name="minMs">Minimum duration in milliseconds for each span.</param>
    /// <param name="maxMs">Maximum duration in milliseconds for each span.</param>
    private async Task CreateChildSpansAsync(Activity? parent, int depth, int children = 1, int minMs = 10, int maxMs = 100)
    {
        if (depth <= 0 || parent == null)
        {
            return;
        }

        for (var i = 0; i < children; i++)
        {
            var childName = $"{parent.DisplayName.Replace("Parent", $"Child {depth} - ({i})")}";
            using var child = mActivitySource.StartActivity(
                childName,
                ActivityKind.Internal,
                parent.Context
            );

            child?.SetTag("parent.id", parent.Id);
            child?.SetTag("child.index", i);
            child?.SetTag("timestamp", DateTime.UtcNow);

            // Random duration for this child
            var duration = mRandom.Next(minMs, maxMs + 1);
            await Task.Delay(duration);

            // Recurse for next depth level
            await CreateChildSpansAsync(child, depth - 1, children, minMs, maxMs);
        }
    }


    private void BuildTracerProvider()
    {
        mTracerProvider?.Dispose();

        mTracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(nameof(TraceSpammer))
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(mConfig.OtlpEndpoint);
                options.Protocol = mConfig.Protocol switch
                {
                    OtlpProtocol.Grpc => OtlpExportProtocol.Grpc,
                    OtlpProtocol.HttpProtobuf => OtlpExportProtocol.HttpProtobuf,
                    _ => OtlpExportProtocol.Grpc
                };
            })
            .Build();

        mLogger.LogInformation(
            "Tracer Provider built for endpoint {endpoint} using {protocol}",
            mConfig.OtlpEndpoint,
            mConfig.Protocol
        );
    }
}
