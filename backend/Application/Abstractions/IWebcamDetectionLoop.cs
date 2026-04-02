// Contract for the real-time webcam detection loop — single Run() method that blocks until ESC.

namespace DeepLearning.Application.Abstractions;

/// <summary>
/// Contract for the webcam capture and real-time detection loop.
/// Implementations manage camera access, frame acquisition, and the detection display pipeline.
/// </summary>
public interface IWebcamDetectionLoop
{
    /// <summary>
    /// Starts the real-time detection loop. Blocks until the user exits (e.g. by pressing ESC).
    /// </summary>
    void Run();
}
