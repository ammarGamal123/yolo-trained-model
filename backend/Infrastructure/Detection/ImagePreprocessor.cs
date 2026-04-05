// Converts a Bitmap to a CHW-normalized float array for YOLO model input.

using System.Drawing;
using System.Drawing.Imaging;

namespace DeepLearning.Infrastructure.Detection;

/// <summary>
/// Converts a <see cref="Bitmap"/> into a CHW (Channels × Height × Width) float array
/// suitable for YOLO model input.
///
/// <para>
/// The conversion pipeline is:
/// <list type="number">
///   <item>Resize the image to the model's target width and height (typically 640×640).</item>
///   <item>Read each pixel and separate the R, G, B channels into three distinct planes.</item>
///   <item>Normalize each channel from 0–255 to 0.0–1.0 by dividing by 255.</item>
///   <item>Pack the planes into a flat float array: [all R, all G, all B].</item>
/// </list>
/// </para>
///
/// <para>
/// This static class has no state and no external dependencies.
/// It is a pure transformation utility used internally by <see cref="OnnxObjectDetector"/>.
/// </para>
/// </summary>
public static class ImagePreprocessor
{
    /// <summary>
    /// Converts a source bitmap to a CHW-normalized float array.
    /// Uses fast LockBits for pixel access instead of slow GetPixel.
    /// </summary>
    /// <param name="source">The original image in any size.</param>
    /// <param name="targetWidth">Target width in pixels (must match the model's expected input width).</param>
    /// <param name="targetHeight">Target height in pixels (must match the model's expected input height).</param>
    /// <returns>
    /// A flat float array of length 3 × targetHeight × targetWidth,
    /// laid out as [R₀₀, R₀₁, ..., Rₕ₋₁,ᵥ₋₁, G₀₀, G₀₁, ..., B₀₀, B₀₁, ...].
    /// Each value is in the range 0.0 to 1.0.
    /// </returns>
    public static float[] ToChwArray(Bitmap source, int targetWidth, int targetHeight)
    {
        float scale = Math.Min((float)targetWidth / source.Width, (float)targetHeight / source.Height);
        int newWidth = (int)(source.Width * scale);
        int newHeight = (int)(source.Height * scale);
        int padX = (targetWidth - newWidth) / 2;
        int padY = (targetHeight - newHeight) / 2;

        using Bitmap resized = new(targetWidth, targetHeight);
        using (Graphics g = Graphics.FromImage(resized))
        {
            g.Clear(Color.FromArgb(114, 114, 114)); // Standard YOLO pad color
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            g.DrawImage(source, padX, padY, newWidth, newHeight);
        }

        int pixelCount = targetWidth * targetHeight;
        float[] data = new float[3 * pixelCount];

        Rectangle rect = new(0, 0, targetWidth, targetHeight);
        BitmapData bitmapData = resized.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

        try
        {
            unsafe
            {
                byte* ptr = (byte*)bitmapData.Scan0;
                int stride = bitmapData.Stride;

                for (int y = 0; y < targetHeight; y++)
                {
                    byte* row = ptr + (y * stride);
                    for (int x = 0; x < targetWidth; x++)
                    {
                        int index = y * targetWidth + x;
                        int pixelIndex = x * 3;

                        data[index] = row[pixelIndex + 2] / 255f;         // R
                        data[pixelCount + index] = row[pixelIndex + 1] / 255f; // G
                        data[2 * pixelCount + index] = row[pixelIndex] / 255f;  // B
                    }
                }
            }
        }
        finally
        {
            resized.UnlockBits(bitmapData);
        }

        return data;
    }
}
