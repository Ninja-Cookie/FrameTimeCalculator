using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using VideoLibrary;

namespace FrameTimeCalculator
{
    internal class Program
    {
        static  string  VideoPath           = string.Empty;
        static  string  ComparePath         = string.Empty;
        const   float   ThresholdDefault    = 0.6f;
        static  float   Threshold           = ThresholdDefault;

        static readonly string CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

        enum FrameState
        {
            NONE,
            Found,
            Waiting,
            Ended
        }

        static async Task<string> DownloadYouTubeVideo(string url)
        {
            Console.WriteLine($"Downloading to \"{CurrentDirectory}\"...");

            var youtube         = YouTube.Default;
            YouTubeVideo video  = null;

            try { video = await youtube.GetVideoAsync(url); } catch { return null; }
            string path = Path.Combine(CurrentDirectory, video.FullName);
            try { File.WriteAllBytes(path, video.GetBytes()); } catch { return null; }

            Console.WriteLine($"Download Complete");

            return path;
        }

        readonly static string[] Options = new[]
        {
            "Process",
            "Download from YouTube then Process",
            "Download from YouTube only"
        };

        static async Task Main(string[] args)
        {
            int option = ProcessOption();

            switch (option)
            {
                case 0:
                    AskForPaths();
                    ProcessVideo();
                break;

                case 1:
                    await AskForYouTube();
                    AskForPaths(true);
                    ProcessVideo();
                break;

                case 2:
                    await AskForYouTube();
                break;
            }

            Console.Read();
            System.Environment.Exit(0);
        }

        static async Task AskForYouTube()
        {
            Console.Clear();

            while (true)
            {
                Console.Write($"Full URL to YouTube Video: ");
                string url = Console.ReadLine()?.Trim();

                var path = await DownloadYouTubeVideo(url);
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    VideoPath = path;
                    break;
                }

                WriteError($"Download failed... Please check the URL");
            }
        }

        static float GetThreshold()
        {
            Console.Clear();

            while (true)
            {
                Console.Write($"Threshold (0.00 - 1.00) (How close the frame should match) (Default: {Threshold}): ");
                string threshold = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(threshold))
                    return ThresholdDefault;

                if (float.TryParse(threshold, out var result) && result >= 0f && result <= 1f)
                    return result;

                WriteError("Invalid Threshold...");
            }
        }

        static void WriteError(string message)
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("");
        }

        static void AskForPaths(bool onlyCompare = false)
        {
            Console.Clear();

            if (!onlyCompare)
            {
                while (true)
                {
                    Console.Write($"Full Path to Video (drag/drop): ");
                    VideoPath = Console.ReadLine()?.Trim().Replace("\"", "");

                    if (!string.IsNullOrEmpty(VideoPath) && File.Exists(VideoPath))
                        break;

                    WriteError($"File at \"{VideoPath}\" not found...");
                }
            }

            while (true)
            {
                Console.Write($"Full Path to Compare Frame Image (drag/drop): ");
                ComparePath = Console.ReadLine()?.Trim().Replace("\"", ""); ;

                if (!string.IsNullOrEmpty(ComparePath) && File.Exists(ComparePath))
                    break;

                WriteError($"File at \"{ComparePath}\" not found...");
            }
        }

        static int ProcessOption()
        {
            int defaultOption = 1;

            while (true)
            {
                for (int i = 0; i < Options.Length; i++)
                    Console.WriteLine($"{i + 1}) {Options[i]}" + (i == defaultOption - 1 ? " (default)" : ""));

                Console.WriteLine("");
                Console.Write($"Select Option (1 - {Options.Length}): ");
                string option = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(option))
                    return defaultOption - 1;

                if (int.TryParse(option, out int value) && value - 1 > 0 && value - 1 < Options.Length)
                    return value - 1;

                WriteError("Option was invalid...");
            }
        }

        static void ProcessVideo()
        {
            Threshold = GetThreshold();

            Console.Clear();

            int     totalFoundFrames    = 0;
            double  fps                 = 0;

            List<FrameData> framesFound = new List<FrameData>();
            
            using (var video        = new VideoCapture(VideoPath))                          {
            using (var compare      = Cv2.ImRead(ComparePath, ImreadModes.ReducedColor8))   {
            using (var frame        = new Mat())
            using (var tempFrame    = new Mat())
            {
                int totalFrames = video.FrameCount;
                fps             = video.Fps;
                var frameState  = FrameState.NONE;

                FrameData.Frame frameStart = default;
                byte[] previousFrame = Array.Empty<byte>();

                for (int i = 0; i < totalFrames; i++)
                {
                    WriteProgress(i, totalFrames);
                    if (!video.Read(frame))
                        break;

                    if (ProcessFrame(frame, tempFrame, compare, i))
                    {
                        totalFoundFrames++;
                        frameState = frameState == FrameState.NONE ? FrameState.Found : FrameState.Waiting;
                    }
                    else if (frameState == FrameState.Found || frameState == FrameState.Waiting)
                    {
                        frameState = FrameState.Ended;
                    }

                    switch (frameState)
                    {
                        case FrameState.Found:      frameStart      = new FrameData.Frame(i, previousFrame = frame.ImEncode()); break;
                        case FrameState.Waiting:    /*previousFrame = frame.ImEncode();*/                                       break; // If wanting the ending frame, got to process each frame not knowing which will be the last one
                        case FrameState.Ended:
                            frameState = FrameState.NONE;
                            framesFound.Add(new FrameData(frameStart, new FrameData.Frame(i, previousFrame)));
                        break;
                    }
                }
            }}}

            if (fps <= 0)
                return;

            WriteResult(totalFoundFrames, fps, framesFound);
        }

        static bool ProcessFrame(Mat frame, Mat tempFrame, Mat compare, int frameIndex)
        {
            if (!CompareFrame(frame, tempFrame, compare, Threshold))
                return false;

            WriteProgressFrame(frameIndex);
            return true;
        }

        static void WriteResult(int totalFoundFrames, double fps, List<FrameData> framesFound)
        {
            Console.Clear();

            Console.WriteLine($"----------");

            foreach (var frameFound in framesFound)
            {
                var timeStart   = TimeSpan.FromSeconds(frameFound.StartingFrame.FrameIndex / fps);
                var timeTook    = TimeSpan.FromSeconds(frameFound.Length / fps);
                Console.WriteLine($"{FormatTimeSpan(timeStart, false)} (for {FormatTimeSpan(timeTook)} seconds)");

                FrameData.TryExportFrame(frameFound.StartingFrame, Path.Combine(CurrentDirectory, "Snapshots", $"{Path.GetFileNameWithoutExtension(VideoPath).Trim().Replace(" ", "_")}"), $"Frame{frameFound.StartingFrame.FrameIndex}_{timeStart.Hours:00}-{timeStart.Minutes:00}-{timeStart.Seconds:00}");
            }

            Console.WriteLine($"----------");
            Console.WriteLine("");
            Console.WriteLine($"Total Frames: {totalFoundFrames}");
            Console.WriteLine($"Total Time: {FormatTimeSpan(TimeSpan.FromSeconds(totalFoundFrames / fps), false)}");
        }

        static void WriteProgress(int frame, int totalFrames)
        {
            Console.SetCursorPosition(0, 0);
            Console.Write($"Processing... {(((float)frame / (float)totalFrames) * 100f).ToString("00")}%");
        }

        static void WriteProgressFrame(int frame)
        {
            Console.SetCursorPosition(0, 1);
            Console.Write($"Adding frame at {frame}...");
        }

        static bool CompareFrame(Mat frame, Mat tempFrame, Mat compare, double threshold, TemplateMatchModes compareMethod = TemplateMatchModes.CCoeffNormed)
        {
            try
            {
                Cv2.Resize(frame, tempFrame, new Size(compare.Width, compare.Height));
                Cv2.MatchTemplate(tempFrame, compare, tempFrame, compareMethod);
                Cv2.MinMaxLoc(tempFrame, out _, out double value, out _, out _);
                return value >= threshold;
            }
            catch (Exception ex)
            {
                WriteError(ex.Message);
                return false;
            }
        }

        static string FormatTimeSpan(TimeSpan timeSpan, bool justSeconds = true)
        {
            return justSeconds ? $"{timeSpan.TotalSeconds:0.000}" : $"{timeSpan.ToString(@"hh\:mm\:ss\.fff")}";
        }
    }
}
