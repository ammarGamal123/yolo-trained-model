// YOLO detection engine using ONNX Runtime: preprocess -> infer -> parse -> NMS.

using System.Diagnostics;
using System.Drawing;
using DeepLearning.Application.Abstractions;
using DeepLearning.Application.Configuration;
using DeepLearning.Domain.Entities;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace DeepLearning.Infrastructure.Detection;

/// <summary>
/// YOLO object detection engine powered by ONNX Runtime.
/// Handles the complete pipeline: image preprocessing, model inference, output parsing, and NMS.
///
/// <para>
/// This class is sealed and implements <see cref="IObjectDetector"/> for dependency injection.
/// To swap the detection backend, implement <see cref="IObjectDetector"/> and register it in Program.cs
/// instead of this class.
/// </para>
/// </summary>
public sealed class OnnxObjectDetector : IObjectDetector
{
    private readonly DetectionOptions _options;
    private readonly InferenceSession _session;
    private readonly string _inputName;
    private readonly DetectionMetrics _metrics = new();

    /// <summary>
    /// Minimum bounding box area as fraction of image area.
    /// Boxes smaller than this are filtered out as noise.
    /// </summary>
    private const float MinBoxAreaFraction = 0.001f;

    /// <summary>
    /// Loads the ONNX model from the path specified in <paramref name="options"/>.
    /// Configured for maximum inference speed with optimized session options.
    /// </summary>
    /// <param name="options">Must supply <see cref="DetectionOptions.ModelPath"/>.</param>
    /// <exception cref="FileNotFoundException">Thrown when the model file does not exist.</exception>
    public OnnxObjectDetector(DetectionOptions options)
    {
        _options = options;

        // Optimize session for maximum speed
        var sessionOptions = new SessionOptions
        {
            // Use all available CPU cores
            ExecutionMode = ExecutionMode.ORT_PARALLEL,
            // Enable graph optimization
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            // Enable memory pattern optimization (faster allocation)
            EnableMemoryPattern = true,
            // Enable CPU memory arena
            EnableCpuMemArena = true,
        };

        // Set intra-op threads to use all CPU cores
        int cpuCount = Environment.ProcessorCount;
        sessionOptions.IntraOpNumThreads = cpuCount;
        sessionOptions.InterOpNumThreads = Math.Max(1, cpuCount / 2);

        _session = new InferenceSession(_options.ModelPath, sessionOptions);
        _inputName = _session.InputMetadata.Keys.First();
    }

    /// <inheritdoc />
    public IReadOnlyList<DetectionResult> Detect(Bitmap image)
    {
        var sw = Stopwatch.StartNew();

        float[] chwData = ImagePreprocessor.ToChwArray(image, _options.ModelWidth, _options.ModelHeight);
        var inputTensor = new DenseTensor<float>(chwData, [1, 3, _options.ModelHeight, _options.ModelWidth]);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(_inputName, inputTensor)
        };

        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(inputs);
        IReadOnlyList<DetectionResult> rawDetections = ParseDetections(results, image.Width, image.Height);

        IReadOnlyList<DetectionResult> nmsResult = NmsProcessor.Apply(
            rawDetections, 
            _options.IouThreshold,
            _options.UseSoftNms,
            _options.SoftNmsSigma);

        var filtered = nmsResult
            .Where(d => d.Confidence >= _options.ConfidenceThreshold)
            .ToList();

        if (_options.MergeCloseDetections && filtered.Count > 1)
        {
            filtered = MergeCloseDetections(filtered);
        }

        // Clean up messy detection: 
        // Suppress "soap" (index 0) if it detects the printed graphic inside a "soap-cover" (index 1) box
        filtered = FilterContainedBoxes(filtered, containerClassId: 1, containedClassId: 0);

        sw.Stop();
        _metrics.RecordInference(sw.Elapsed.TotalMilliseconds);

        return filtered;
    }

    private List<DetectionResult> MergeCloseDetections(List<DetectionResult> detections)
    {
        var merged = new List<DetectionResult>();
        var used = new HashSet<int>();

        for (int i = 0; i < detections.Count; i++)
        {
            if (used.Contains(i)) continue;

            var current = detections[i];
            var closeGroup = new List<DetectionResult> { current };
            used.Add(i);

            for (int j = i + 1; j < detections.Count; j++)
            {
                if (used.Contains(j)) continue;
                if (detections[j].ClassId != current.ClassId) continue;

                float distance = NmsProcessor.CalculateCenterDistance(current, detections[j]);
                if (distance < _options.MergeDistanceThreshold)
                {
                    closeGroup.Add(detections[j]);
                    used.Add(j);
                }
            }

            if (closeGroup.Count == 1)
            {
                merged.Add(current);
            }
            else
            {
                float avgX1 = closeGroup.Average(d => d.X1);
                float avgY1 = closeGroup.Average(d => d.Y1);
                float avgX2 = closeGroup.Average(d => d.X2);
                float avgY2 = closeGroup.Average(d => d.Y2);
                float maxConf = closeGroup.Max(d => d.Confidence);

                merged.Add(new DetectionResult
                {
                    ClassId = current.ClassId,
                    Confidence = maxConf,
                    X1 = avgX1,
                    Y1 = avgY1,
                    X2 = avgX2,
                    Y2 = avgY2
                });
            }
        }

        return merged;
    }

    private List<DetectionResult> FilterContainedBoxes(List<DetectionResult> detections, int containerClassId, int containedClassId)
    {
        var valid = new List<DetectionResult>();
        var containers = detections.Where(d => d.ClassId == containerClassId).ToList();

        foreach (var d in detections)
        {
            if (d.ClassId == containedClassId)
            {
                bool isContained = false;
                foreach (var container in containers)
                {
                    // Check if 'd' is mostly inside 'container'
                    float overlapX1 = Math.Max(d.X1, container.X1);
                    float overlapY1 = Math.Max(d.Y1, container.Y1);
                    float overlapX2 = Math.Min(d.X2, container.X2);
                    float overlapY2 = Math.Min(d.Y2, container.Y2);

                    float overlapArea = Math.Max(0f, overlapX2 - overlapX1) * Math.Max(0f, overlapY2 - overlapY1);
                    
                    if (overlapArea / d.Area > 0.45f) // If more than 45% of the soap graphic is inside the box
                    {
                        isContained = true;
                        break;
                    }
                }
                if (isContained) continue; // Drop the graphic reading
            }
            valid.Add(d);
        }
        return valid;
    }

    /// <inheritdoc />
    public DetectionMetrics GetMetrics() => _metrics;

    /// <summary>
    /// Infers the number of classes directly from the model output tensor shape.
    /// </summary>
    /// <remarks>
    /// YOLO-style outputs are assumed to be:
    ///   [1, C, N] (channels-first) or [1, N, C] (channels-last),
    /// where C = 4 + classCount.
    /// </remarks>
    public int InferClassCount()
    {
        float[] chwData = new float[3 * _options.ModelWidth * _options.ModelHeight];
        var inputTensor = new DenseTensor<float>(
            chwData,
            [1, 3, _options.ModelHeight, _options.ModelWidth]);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(_inputName, inputTensor)
        };

        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(inputs);
        (Tensor<float> tensor, int[] dimensions) = FindDetectionHead(results);

        // For YOLO outputs, classCount is (channelCount - 4), where the 4 are box params.
        int dimA = dimensions[1];
        int dimB = dimensions[2];
        bool isChannelsFirst = dimA < dimB;
        int channelCount = isChannelsFirst ? dimA : dimB;
        int classCount = channelCount - 4;

        if (classCount <= 0)
        {
            throw new InvalidOperationException(
                $"Unable to infer class count from model output. Computed: {classCount}.");
        }

        return classCount;
    }

    /// <inheritdoc />
    public void Dispose() => _session.Dispose();

    /// <summary>
    /// Parses the raw ONNX output tensor into detection objects.
    /// Supports both channels-first [1, C, N] and channels-last [1, N, C] layouts automatically.
    /// </summary>
    private IReadOnlyList<DetectionResult> ParseDetections(
        IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results,
        int originalWidth,
        int originalHeight)
    {
        (Tensor<float> tensor, int[] dimensions) = FindDetectionHead(results);
        float[] data = tensor.ToArray();

        int dimA = dimensions[1];
        int dimB = dimensions[2];
        bool isChannelsFirst = dimA < dimB;
        int channelCount = isChannelsFirst ? dimA : dimB;
        int candidateCount = isChannelsFirst ? dimB : dimA;
        int classCount = channelCount - 4;

        float ValueAt(int channel, int box)
            => isChannelsFirst
                ? data[(channel * candidateCount) + box]
                : data[(box * channelCount) + channel];

        float scale = Math.Min((float)_options.ModelWidth / originalWidth, (float)_options.ModelHeight / originalHeight);
        float padX = (_options.ModelWidth - originalWidth * scale) / 2f;
        float padY = (_options.ModelHeight - originalHeight * scale) / 2f;

        List<DetectionResult> detections = new(capacity: 256);

        for (int box = 0; box < candidateCount; box++)
        {
            float centerX = ValueAt(0, box);
            float centerY = ValueAt(1, box);
            float width = ValueAt(2, box);
            float height = ValueAt(3, box);

            int bestClassId = -1;
            float bestConfidence = 0f;

            for (int classIndex = 0; classIndex < classCount; classIndex++)
            {
                float classConfidence = ValueAt(classIndex + 4, box);

                if (classConfidence > bestConfidence)
                {
                    bestConfidence = classConfidence;
                    bestClassId = classIndex;
                }
            }

            if (bestConfidence < _options.ConfidenceThreshold)
            {
                continue;
            }

            // Calculate pixel coordinates mapping back from letterbox
            float x1 = (centerX - (width / 2f) - padX) / scale;
            float y1 = (centerY - (height / 2f) - padY) / scale;
            float x2 = (centerX + (width / 2f) - padX) / scale;
            float y2 = (centerY + (height / 2f) - padY) / scale;

            // Skip boxes that are degenerate (zero or negative area)
            if (x2 <= x1 || y2 <= y1)
            {
                continue;
            }

            // Clip to image bounds
            x1 = Math.Max(0, x1);
            y1 = Math.Max(0, y1);
            x2 = Math.Min(originalWidth, x2);
            y2 = Math.Min(originalHeight, y2);

            detections.Add(new DetectionResult
            {
                ClassId = bestClassId,
                Confidence = bestConfidence,
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2
            });
        }

        return detections;
    }

    /// <summary>
    /// Finds the primary detection output tensor from the model's results.
    /// Identifies it by shape: a 3D tensor where one dimension is the channel count (>=6)
    /// and the other is the candidate box count (>=1000).
    /// </summary>
    private static (Tensor<float> Tensor, int[] Dimensions) FindDetectionHead(
        IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results)
    {
        foreach (DisposableNamedOnnxValue result in results)
        {
            Tensor<float> tensor = result.AsTensor<float>();
            int[] dimensions = tensor.Dimensions.ToArray();

            if (dimensions.Length == 3 && dimensions[0] == 1)
            {
                int channels = Math.Min(dimensions[1], dimensions[2]);
                int boxes = Math.Max(dimensions[1], dimensions[2]);

                if (channels >= 6 && boxes >= 1000)
                {
                    return (tensor, dimensions);
                }
            }
        }

        Tensor<float> firstTensor = results.First().AsTensor<float>();
        return (firstTensor, firstTensor.Dimensions.ToArray());
    }
}
