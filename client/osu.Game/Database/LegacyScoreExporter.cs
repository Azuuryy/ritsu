// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Linq;
using System.Threading;
using osu.Framework.Platform;
using osu.Game.Extensions;
using osu.Game.IO.Legacy;
using osu.Game.Overlays.Notifications;
using osu.Game.Scoring;

namespace osu.Game.Database
{
    public class LegacyScoreExporter : LegacyExporter<ScoreInfo>
    {
        private readonly string? profileName;

        public LegacyScoreExporter(Storage storage, string? profileName = null)
            : base(storage)
        {
            this.profileName = profileName;
        }

        protected override string GetFilename(ScoreInfo score)
        {
            string scoreString = score.GetDisplayString();
            string filename = $"{scoreString} ({score.Date.LocalDateTime:yyyy-MM-dd_HH-mm})";

            return filename;
        }

        protected override string FileExtension => @".osr";

        public override void ExportToStream(ScoreInfo model, Stream outputStream, ProgressNotification? notification, CancellationToken cancellationToken = default)
        {
            var file = model.Files.SingleOrDefault();
            if (file == null)
                return;

            using var inputStream = UserFileStorage.GetStream(file.File.GetStoragePath());
            if (profileName == null)
                inputStream.CopyTo(outputStream);
            else
                RewriteUsername(inputStream, outputStream, profileName, cancellationToken);
        }

        internal static void RewriteUsername(Stream input, Stream output, string username, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new InvalidDataException("The profile name cannot be empty.");
            if (!input.CanSeek)
                throw new InvalidDataException("The replay stream cannot be read safely.");

            using var reader = new SerializationReader(input);
            if (reader.ReadByte() > 3 || reader.ReadInt32() < 0)
                throw new InvalidDataException("The replay format is not supported.");
            reader.ReadString();
            long usernameStart = input.Position;
            reader.ReadString();
            long usernameEnd = input.Position;
            input.Position = 0;
            byte[] buffer = new byte[81920];
            long remaining = usernameStart;
            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int count = input.Read(buffer, 0, (int)System.Math.Min(buffer.Length, remaining));
                if (count == 0)
                    throw new EndOfStreamException();
                output.Write(buffer, 0, count);
                remaining -= count;
            }
            cancellationToken.ThrowIfCancellationRequested();
            using (var writer = new SerializationWriter(output, true))
                writer.Write(username);
            input.Position = usernameEnd;
            int bytesRead;
            while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                output.Write(buffer, 0, bytesRead);
            }
        }
    }
}
