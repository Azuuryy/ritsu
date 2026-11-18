// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Game.Database;
using osu.Game.IO.Legacy;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class RitsuReplayExportTest
    {
        [TestCase("éclair 🎵")]
        [TestCase("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789")]
        public void UsernameRewritePreservesReplayBytes(string name)
        {
            using var original = new MemoryStream();
            using (var writer = new SerializationWriter(original, true))
            {
                writer.Write((byte)0);
                writer.Write(30000017);
                writer.Write("beatmap-hash");
                writer.Write("original");
                writer.Write(new byte[] { 5, 4, 3, 2, 1 });
            }
            byte[] source = original.ToArray();
            using var output = new MemoryStream();
            LegacyScoreExporter.RewriteUsername(new MemoryStream(source), output, name);
            using var reader = new SerializationReader(new MemoryStream(output.ToArray()));
            Assert.That(reader.ReadByte(), Is.EqualTo(0));
            Assert.That(reader.ReadInt32(), Is.EqualTo(30000017));
            Assert.That(reader.ReadString(), Is.EqualTo("beatmap-hash"));
            Assert.That(reader.ReadString(), Is.EqualTo(name));
            Assert.That(reader.ReadBytes(reader.RemainingBytes), Is.EqualTo(source.TakeLast(9).ToArray()));
        }

        [Test]
        public void InvalidReplayIsRejected()
        {
            Assert.Throws<InvalidDataException>(() => LegacyScoreExporter.RewriteUsername(new MemoryStream(new byte[] { 5, 0, 0, 0, 0 }), new MemoryStream(), "name"));
            Assert.Throws<EndOfStreamException>(() => LegacyScoreExporter.RewriteUsername(new MemoryStream(new byte[] { 0 }), new MemoryStream(), "name"));
            Assert.Throws<InvalidDataException>(() => LegacyScoreExporter.RewriteUsername(new MemoryStream(), new MemoryStream(), " "));
        }
    }
}
