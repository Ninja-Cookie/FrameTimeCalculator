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
        static string VideoPath     = string.Empty;
        static string ComparePath   = string.Empty;

        static readonly string DownloadPath = AppDomain.CurrentDomain.BaseDirectory;

        static async Task<string> DownloadYouTubeVideo(string url)
        {
            Console.WriteLine($"Downloading to \"{DownloadPath}\"...");

            var youtube         = YouTube.Default;
            YouTubeVideo video  = null;

            try { video = await youtube.GetVideoAsync(url); } catch { return null; }
            string path = Path.Combine(DownloadPath, video.FullName);
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
            Console.Clear();

            int     totalFoundFrames    = 0;
            double  fps                 = 30;
            bool    found               = false;
            bool    waiting             = false;

            List<(int, int)> framesFound = new List<(int, int)>();

            using (var video        = new VideoCapture(VideoPath))                          {
            using (var compare      = Cv2.ImRead(ComparePath, ImreadModes.ReducedColor8))   {
            using (var frame        = new Mat())
            {
                int frames      = video.FrameCount;
                int frameStart  = 0;
                fps             = video.Fps;

                try
                {
                    for (int i = 0; i < frames; i++)
                    {
                        WriteProgress(i, frames);

                        if (!video.Read(frame))
                            continue;

                        if (found = CompareFrame(frame, compare, 0.6))
                        {
                            WriteProgressFrame(i);
                            totalFoundFrames++;
                        }

                        if (found && !waiting)
                        {
                            frameStart = i;
                            waiting = true;
                        }
                        else if (waiting && !found)
                        {
                            framesFound.Add((frameStart, i));
                            waiting = false;
                        }
                    }
                } catch (Exception ex) { Console.Clear(); Console.WriteLine(ex); return; }
            }}}

            WriteResult(totalFoundFrames, fps, framesFound);
        }

        static void WriteResult(int totalFoundFrames, double fps, List<(int, int)> framesFound)
        {
            Console.Clear();

            Console.WriteLine($"----------");

            foreach (var frameFound in framesFound)
            {
                var timeStart   = TimeSpan.FromSeconds(frameFound.Item1 / fps);
                var timeTook    = TimeSpan.FromSeconds(frameFound.Item2 / fps) - timeStart;
                Console.WriteLine($"{FormatTimeSpan(timeStart, false)} (for {FormatTimeSpan(timeTook)} seconds)");
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

        static bool CompareFrame(Mat frame, Mat compare, double threshold, TemplateMatchModes compareMethod = TemplateMatchModes.CCoeffNormed)
        {
            Cv2.Resize(frame, frame, new Size(compare.Width, compare.Height));
            try { Cv2.MatchTemplate(frame, compare, frame, compareMethod); } catch { return false; }
            Cv2.MinMaxLoc(frame, out _, out double value, out _, out _);
            return value >= threshold;
        }

        static string FormatTimeSpan(TimeSpan timeSpan, bool justSeconds = true)
        {
            return justSeconds ? $"{timeSpan.TotalSeconds:0.000}" : $"{timeSpan.ToString(@"hh\:mm\:ss\.fff")}";
        }
    }
}
