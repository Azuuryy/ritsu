// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.IO;
using System.Text;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace osu.Game.LocalPriority
{
    /// <summary>
    /// Saves profile data so a crash or power loss can't leave it empty, and loads it back with a fallback.
    /// The framework's <see cref="Storage.CreateFileSafely"/> only flushes to disk on Windows, so on Linux a crash
    /// shortly after a save could leave a 0-byte file behind.
    /// </summary>
    public static class LocalDurableFile
    {
        /// <summary>
        /// The previous good version, kept next to the file on every save.
        /// </summary>
        public const string BACKUP_SUFFIX = @".bak";

        /// <summary>
        /// A file that could not be read is moved here, so nothing is overwritten.
        /// </summary>
        public const string BROKEN_SUFFIX = @".broken";

        public static void WriteText(Storage storage, string path, string text) =>
            Write(storage, path, stream =>
            {
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, true))
                    writer.Write(text);
            });

        /// <summary>
        /// Writes a temporary file, flushes it to disk, then puts it in place, keeping the old version as <see cref="BACKUP_SUFFIX"/>.
        /// </summary>
        public static void Write(Storage storage, string path, Action<Stream> write)
        {
            string fullPath = storage.GetFullPath(path, true);
            string backupPath = fullPath + BACKUP_SUFFIX;
            string temporaryPath = $@"{fullPath}.{Guid.NewGuid():N}.tmp";

            try
            {
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    write(stream);
                    stream.Flush(true);
                }

                // An empty or missing file is never worth keeping, and must not replace the last good backup.
                if (!File.Exists(fullPath) || new FileInfo(fullPath).Length == 0)
                {
                    File.Move(temporaryPath, fullPath, true);
                    return;
                }

                try
                {
                    File.Replace(temporaryPath, fullPath, backupPath, true);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
                {
                    File.Copy(fullPath, backupPath, true);
                    File.Move(temporaryPath, fullPath, true);
                }
            }
            catch
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch
                {
                    // ignore.
                }

                throw;
            }
        }

        /// <summary>
        /// Reads a file saved with <see cref="Write"/>. Returns null if neither it nor its backup exists.
        /// If it is missing, empty or <paramref name="parse"/> rejects it (returns null or throws), the backup is loaded and
        /// written back in its place, and a damaged file is kept as <see cref="BROKEN_SUFFIX"/>. With no usable backup, null is
        /// returned the same way. A <see cref="NotSupportedException"/> from <paramref name="parse"/> (a file from a newer
        /// version) is passed on, leaving the file untouched.
        /// </summary>
        public static T? Read<T>(Storage storage, string path, Func<string, T?> parse)
            where T : class
        {
            string backup = path + BACKUP_SUFFIX;
            bool exists = storage.Exists(path);

            if (!exists && !storage.Exists(backup))
                return null;

            T? value = null;
            string? backupText = null;

            if (exists && tryRead(storage, path, parse, out value, out _))
                return value;

            bool restored = storage.Exists(backup) && tryRead(storage, backup, parse, out value, out backupText);

            if (exists)
            {
                try
                {
                    storage.Move(path, path + BROKEN_SUFFIX);
                }
                catch (Exception e)
                {
                    Logger.Error(e, $@"Could not move the damaged {path} aside");
                }
            }

            if (restored)
            {
                // Put the backup back right away: otherwise closing the game before the next save would leave no file at all.
                try
                {
                    WriteText(storage, path, backupText!);
                }
                catch (Exception e)
                {
                    Logger.Error(e, $@"Could not restore {path} from its backup");
                }
            }

            string fullPath = storage.GetFullPath(path);
            string what = exists ? @"was damaged" : @"was missing";
            string kept = exists ? $@" The damaged file was kept as {Path.GetFileName(fullPath)}{BROKEN_SUFFIX}." : string.Empty;

            Logger.Log(restored
                ? $@"{fullPath} {what}, so its previous copy ({BACKUP_SUFFIX}) was loaded.{kept}"
                : $@"{fullPath} {what} and has no usable backup, so it starts empty.{kept}",
                LoggingTarget.Runtime, LogLevel.Important);

            return restored ? value : null;
        }

        private static bool tryRead<T>(Storage storage, string path, Func<string, T?> parse, out T? value, out string? text)
            where T : class
        {
            value = null;
            text = null;

            try
            {
                using (var stream = storage.GetStream(path, FileAccess.Read, FileMode.Open))
                using (var reader = new StreamReader(stream))
                    text = reader.ReadToEnd();

                if (string.IsNullOrWhiteSpace(text))
                    return false;

                value = parse(text);
                return value != null;
            }
            catch (NotSupportedException)
            {
                throw;
            }
            catch (Exception e)
            {
                Logger.Error(e, $@"Could not read {path}");
                return false;
            }
        }
    }
}
