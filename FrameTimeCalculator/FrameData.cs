using System.IO;

namespace FrameTimeCalculator
{
    internal class FrameData
    {
        internal FrameData(Frame startingFrame, Frame endingFrame)
        {
            StartingFrame   = startingFrame;
            EndingFrame     = endingFrame;
            Length          = EndingFrame.FrameIndex - StartingFrame.FrameIndex;
        }

        internal Frame  StartingFrame   { get; }
        internal Frame  EndingFrame     { get; }
        internal int    Length          { get; }

        internal struct Frame
        {
            internal int    FrameIndex;
            internal byte[] FrameImage;

            internal Frame(int frameIndex, byte[] frameImage)
            {
                FrameIndex = frameIndex;
                FrameImage = frameImage;
            }
        }

        internal static bool TryExportFrame(Frame frame, string folderPath, string fileName, string fileType = ".png")
        {
            if (!Directory.Exists(folderPath))
                try { Directory.CreateDirectory(folderPath); } catch { return false; }

            string path = Path.Combine(folderPath, $"{fileName}{fileType}");
            if (File.Exists(path))
                return false;

            try
            {
                File.WriteAllBytes(path, frame.FrameImage);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
