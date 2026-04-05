namespace DeepLearning.Domain.Entities;

public sealed class DetectionMetrics
{
    private readonly object _lock = new();
    private readonly Queue<double> _recentTimes = new();
    private const int RollingWindowSize = 30;

    public int TotalInferences { get; private set; }
    public double TotalElapsedMs { get; private set; }
    public double LastInferenceMs { get; private set; }
    public double AverageFPS => TotalElapsedMs > 0 ? (TotalInferences / (TotalElapsedMs / 1000.0)) : 0;
    public double CurrentFPS
    {
        get
        {
            lock (_lock)
            {
                if (_recentTimes.Count == 0) return 0;
                double avgMs = _recentTimes.Average();
                return avgMs > 0 ? 1000.0 / avgMs : 0;
            }
        }
    }

    public void RecordInference(double elapsedMs)
    {
        lock (_lock)
        {
            TotalInferences++;
            TotalElapsedMs += elapsedMs;
            LastInferenceMs = elapsedMs;
            _recentTimes.Enqueue(elapsedMs);
            if (_recentTimes.Count > RollingWindowSize)
                _recentTimes.Dequeue();
        }
    }
}
