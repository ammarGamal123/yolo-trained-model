// YOLO detection engine using ONNX Runtime: preprocess -> infer -> parse -> NMS.

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

    /// <summary>
    /// Minimum bounding box area as fraction of image area.
    /// Boxes smaller than this are filtered out as noise.
    /// </summary>
    private const float MinBoxAreaFraction = 0.0005f;

    /// <summary>
    /// Loads the ONNX model from the path specified in <paramref name="options"/>.
    /// </summary>
    /// <param name="options">Must supply <see cref="DetectionOptions.ModelPath"/>.</param>
    /// <exception cref="FileNotFoundException">Thrown when the model file does not exist.</exception>
    public OnnxObjectDetector(DetectionOptions options)
    {
        _options = options;
        _session = new InferenceSession(_options.ModelPath);
        _inputName = _session.InputMetadata.Keys.First();
    }

    /// <inheritdoc />
    public IReadOnlyList<DetectionResult> Detect(Bitmap image)
    {
        float[] chwData = ImagePreprocessor.ToChwArray(image, _options.ModelWidth, _options.ModelHeight);
        var inputTensor = new DenseTensor<float>(chwData, [1, 3, _options.ModelHeight, _options.ModelWidth]);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(_inputName, inputTensor)
        };

        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(inputs);
        IReadOnlyList<DetectionResult> rawDetections = ParseDetections(results, image.Width, image.Height);

        IReadOnlyList<DetectionResult> nmsResult = NmsProcessor.Apply(rawDetections, _options.IouThreshold);

        // Filter out very small boxes (likely noise)
        float minArea = image.Width * image.Height * MinBoxAreaFraction;
        return nmsResult
            .Where(d => d.Area >= minArea)
            .ToList();
    }

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

        float scaleX = (float)originalWidth / _options.ModelWidth;
        float scaleY = (float)originalHeight / _options.ModelHeight;
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

            // Calculate pixel coordinates
            float x1 = (centerX - (width / 2f)) * scaleX;
            float y1 = (centerY - (height / 2f)) * scaleY;
            float x2 = (centerX + (width / 2f)) * scaleX;
            float y2 = (centerY + (height / 2f)) * scaleY;

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
