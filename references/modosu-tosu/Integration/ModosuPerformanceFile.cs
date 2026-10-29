using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using osu.Framework.Logging;

namespace osu.Game.Rulesets.Modosu.Integration
{
    /// <summary>
    /// Coalesces frames and writes them atomically away from the game thread.
    /// </summary>
    internal static class ModosuPerformanceFile
    {
        private static Channel<ModosuPerformanceFrame> createChannel() => Channel.CreateBounded<ModosuPerformanceFrame>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
        private static readonly Channel<ModosuPerformanceFrame> frames = createChannel();
        private static readonly Channel<ModosuPerformanceFrame> selections = createChannel();

        private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);
        private static string currentSession = string.Empty;

        static ModosuPerformanceFile()
        {
            _ = Task.Run(() => writeFrames(frames, string.Empty, true));
            _ = Task.Run(() => writeFrames(selections, ".selection", false));
        }

        public static void PublishSelection(ModosuPerformanceFrame frame) => selections.Writer.TryWrite(frame);

        public static void BeginSession(string sessionId) => Volatile.Write(ref currentSession, sessionId);

        public static void Publish(ModosuPerformanceFrame frame)
        {
            if (frame.SessionId == Volatile.Read(ref currentSession))
                frames.Writer.TryWrite(frame);
        }

        private static async Task writeFrames(Channel<ModosuPerformanceFrame> channel, string suffix, bool guardSession)
        {
            string directory = Path.Combine(Path.GetTempPath(), "modosu-tosu");
            string destination = Path.Combine(directory, $"{Environment.ProcessId}{suffix}.json");
            string temporary = destination + ".tmp";
            bool reportedError = false;

            await foreach (var frame in channel.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                try
                {
                    if (guardSession && frame.SessionId != Volatile.Read(ref currentSession))
                        continue;

                    Directory.CreateDirectory(directory);
                    await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(frame, jsonOptions)).ConfigureAwait(false);
                    if (!guardSession || frame.SessionId == Volatile.Read(ref currentSession))
                        File.Move(temporary, destination, overwrite: true);
                    else
                        File.Delete(temporary);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
                {
                    if (!reportedError)
                        Logger.Error(exception, "Could not publish Modosu performance for tosu.");
                    reportedError = true;
                }
            }
        }
    }
}
