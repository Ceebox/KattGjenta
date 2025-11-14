using System.ComponentModel.DataAnnotations;

namespace KattGjenta;

public sealed class TracingConfig
{
    public event EventHandler? Changed;

    private readonly ILogger<TracingConfig> mLogger;
    private string mOtlpEndpoint = "http://localhost:4317/";
    private int mRate = 3;
    private int mTraceDepth = 1;
    private int mChildrenPerNode = 1;
    private int mMinChildDurationMs = 10;
    private int mMaxChildDurationMs = 100;
    private OtlpProtocol mProtocol = OtlpProtocol.Grpc;

    public TracingConfig(ILogger<TracingConfig> logger)
    {
        mLogger = logger;
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private void TryChange<T>(ref T field, T value, string propertyName)
    {
        var context = new ValidationContext(this) { MemberName = propertyName };
        var results = new List<ValidationResult>();

        if (Validator.TryValidateProperty(value, context, results))
        {
            field = value;
            OnChanged();
        }
        else
        {
            foreach (var r in results)
            {
                mLogger.LogInformation($"{propertyName} invalid: {r.ErrorMessage}");
            }
        }
    }

    [Required]
    [Url(ErrorMessage = "OTLP Endpoint must be a valid URL.")]
    public string OtlpEndpoint
    {
        get => mOtlpEndpoint;
        set => this.TryChange(ref mOtlpEndpoint, value, nameof(OtlpEndpoint));
    }

    [Required]
    public OtlpProtocol Protocol
    {
        get => mProtocol;
        set => this.TryChange(ref mProtocol, value, nameof(Protocol));
    }

    [Range(1, int.MaxValue, ErrorMessage = "Rate must be at least 1.")]
    public int Rate
    {
        get => mRate;
        set => this.TryChange(ref mRate, value, nameof(Rate));
    }

    [Range(1, int.MaxValue, ErrorMessage = "Trace depth must be >= 1.")]
    public int TraceDepth
    {
        get => mTraceDepth;
        set => this.TryChange(ref mTraceDepth, value, nameof(TraceDepth));
    }

    [Range(1, int.MaxValue, ErrorMessage = "Children per node must be >= 1.")]
    public int ChildrenPerNode
    {
        get => mChildrenPerNode;
        set => this.TryChange(ref mChildrenPerNode, value, nameof(ChildrenPerNode));
    }

    [Range(0, int.MaxValue, ErrorMessage = "Min duration must be >= 0.")]
    public int MinChildDurationMs
    {
        get => mMinChildDurationMs;
        set => this.TryChange(ref mMinChildDurationMs, value, nameof(MinChildDurationMs));
    }

    [Range(1, int.MaxValue, ErrorMessage = "Max duration must be >= 1.")]
    [CustomValidation(typeof(TracingConfig), nameof(ValidateMaxDuration))]
    public int MaxChildDurationMs
    {
        get => mMaxChildDurationMs;
        set => this.TryChange(ref mMaxChildDurationMs, value, nameof(MaxChildDurationMs));
    }

    public static ValidationResult? ValidateMaxDuration(int maxValue, ValidationContext context)
    {
        var instance = (TracingConfig)context.ObjectInstance;
        return maxValue >= instance.MinChildDurationMs
            ? ValidationResult.Success
            : new ValidationResult("Max duration must be >= Min duration.");
    }
}
