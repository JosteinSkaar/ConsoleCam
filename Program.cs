using SkiaSharp;
using OpenCvSharp;
using System.Diagnostics;

const int captureDeviceIndex = 1; // Change this to the index of your camera if needed

// VideoCapture is used to capture video from a camera
using var capture = new VideoCapture(
    captureDeviceIndex,
    VideoCaptureAPIs.ANY,
    [(int)VideoCaptureProperties.Fps, 60]
);

// Performance Watches
Stopwatch frameWatch = new();
frameWatch.Start();
Stopwatch perfWatch = new();
perfWatch.Start();

Console.CursorVisible = false; // Hide the cursor for a cleaner output;

// Variable for storing the last frame size
// This is used to clear the console only when the size changes
System.Drawing.Size lastFrameSize = new (0, 0);
while (true)
{
    // Variables for console dimensions
    int conWidth = Console.WindowWidth;
    int conHeight = Console.WindowHeight;

    // Check if the console size has changed
    if (lastFrameSize.Width != conWidth || lastFrameSize.Height != conHeight)
        Console.Clear();

    // Read a frame from the camera
    using var frame = new Mat();
    capture.Read(frame);

    // Calculate the frame factor so we resize height and width proportionally
    double scaleFactor = (double)frame.Width / (double)conWidth;
    double newWidth = frame.Width / scaleFactor;
    // The extra height is to ensure the aspect ratio is maintained due to the console's character aspect ratio
    // 2.5 is a rough estimate of the character height
    double newHeight = frame.Height / (scaleFactor * 2.5);

    // Resize the frame using the calculated dimensions
    using var resizedMat = new Mat();
    Cv2.Resize(frame, resizedMat, new Size((int)newWidth, (int)newHeight), 0, 0, InterpolationFlags.Nearest);

    // Convert the resized Mat to a SKBitmap so we can read the pixel data
    var bitmap = SKBitmap.Decode(resizedMat.ToMemoryStream());

    long imageProcessingTime = perfWatch.ElapsedMilliseconds;
    perfWatch.Restart();

    // Variable to hold pixel brightness values
    byte minBrightness = 255;
    byte maxBrightness = 0;
    List<byte> pixelBrightness = [];

    SKColor color;
    byte brightness;
    // Loop through each pixel in the bitmap
    // We limit the loop to the console dimensions to avoid unnecessary processing
    for (int y = 0; y < bitmap.Height; y++)
    {
        if (y >= conHeight - 1)
            break;

        for (int x = 0; x < bitmap.Width; x++)
        {
            if (x >= conWidth - 1)
                break;

            color = bitmap.GetPixel(x, y);
            // Calculate the brightness as the average of the RGB values
            brightness = (byte)((color.Red + color.Green + color.Blue) / 3);

            minBrightness = Math.Min(minBrightness, brightness);
            maxBrightness = Math.Max(maxBrightness, brightness);
            pixelBrightness.Add(brightness);
        }
    }

    // Calculate the average brightness of the pixels
    // This is used to normalize the brightness values for ASCII character mapping
    byte averageBrightness = (byte)(pixelBrightness.Sum(e => e) / pixelBrightness.Count);

    long pixelLoop = perfWatch.ElapsedMilliseconds;
    perfWatch.Restart();


    // Loop through the pixel brightness values and map them to ASCII characters
    // We use Parallel.For to speed up the character mapping process
    char[] asciiCharsArr = new char[pixelBrightness.Count + pixelBrightness.Count / (conWidth - 1)];
    Parallel.For(0, pixelBrightness.Count, i =>
    {
        asciiCharsArr[i + i / (conWidth - 1)] = getPixelChar(pixelBrightness[i], maxBrightness, 16);
        if ((i + 1) % (conWidth - 1) == 0)
            asciiCharsArr[i + i / (conWidth - 1) + 1] = '\n';
    });

    // Remove any trailing nulls and build the string
    string asciiArt = new string(asciiCharsArr).Replace("\0", "");

    long asciiLoop = perfWatch.ElapsedMilliseconds;
    perfWatch.Restart();

    // Store the current console dimensions to check for changes in the next iteration
    lastFrameSize = new(conWidth, conHeight);

    // Setting the cursor position to the top left corner of the console
    // This ensures that the ASCII art is drawn from the top left corner
    // Which replaces the previous frame
    // This is much faster than clearing the console for each frame
    Console.SetCursorPosition(0, 0);
    await Console.Out.WriteAsync(asciiArt);

    long writeTime = perfWatch.ElapsedMilliseconds;

    // Update the console title with performance metrics
    Console.Title = $"ConsoleCam - FPS: {1000.0 / frameWatch.ElapsedMilliseconds:F2} - Input: {frame.Width}x{frame.Height} - Output: {resizedMat.Width:F0}x{resizedMat.Height:F0} - Performance: Image: {imageProcessingTime}ms, PixelLoop: {pixelLoop}ms, AsciiLoop: {asciiLoop}ms, Write: {writeTime}ms";
    frameWatch.Restart();
    perfWatch.Restart();
}

/// <summary>
/// Maps a pixel brightness value to an ASCII character based on the specified scale.
/// </summary>
/// <param name="brightness">The brightness value of the pixel (0-255).</param>
/// <param name="maxBrightness">The maximum brightness value in the image (0-255).</param>
/// <param name="scale">The scale of ASCII characters to use (default is 32
static char getPixelChar(byte brightness, byte maxBrightness, int scale = 32)
{
    if (maxBrightness == 0)
        maxBrightness = 1; // Avoid division by zero

    string asciiChars = scale switch
    {
        2 => " #",
        4 => " .#@",
        5 => " ░▒▓█",
        8 => " .:-=#%@",
        16 => " .,:-~+=*#%@&$",
        32 => " .'`^\",:;Il!i~+_-?][}{1)(|\\/*tfjrxnuvczXYUJCLQ0OZmwqpdbkhao*#MW&8%B@$",
        _ => " .:-=+*#%@",
    };

    // Calculate the index of the ASCII character based on the brightness value
    int index = brightness * (asciiChars.Length - 1) / maxBrightness;
    
    return asciiChars[index];
}