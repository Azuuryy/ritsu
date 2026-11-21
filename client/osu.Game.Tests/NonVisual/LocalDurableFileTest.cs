// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.IO;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.LocalPriority;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class LocalDurableFileTest
    {
        private TemporaryNativeStorage storage = null!;

        [SetUp]
        public void SetUp() => storage = new TemporaryNativeStorage($"local-durable-file-{Guid.NewGuid():N}");

        [TearDown]
        public void TearDown() => storage.Dispose();

        private string read(string path) => LocalDurableFile.Read(storage, path, text => text)!;

        private string contents(string path) => File.ReadAllText(storage.GetFullPath(path));

        [Test]
        public void TestMissingFileReadsAsNull()
        {
            Assert.That(LocalDurableFile.Read(storage, "data.json", text => text), Is.Null);
        }

        [Test]
        public void TestSaveKeepsPreviousVersion()
        {
            LocalDurableFile.WriteText(storage, "data.json", "one");
            LocalDurableFile.WriteText(storage, "data.json", "two");

            Assert.That(read("data.json"), Is.EqualTo("two"));
            Assert.That(contents("data.json" + LocalDurableFile.BACKUP_SUFFIX), Is.EqualTo("one"));
            Assert.That(storage.GetFiles(string.Empty, "*.tmp"), Is.Empty);
        }

        [Test]
        public void TestEmptyFileFallsBackToBackup()
        {
            LocalDurableFile.WriteText(storage, "data.json", "one");
            LocalDurableFile.WriteText(storage, "data.json", "two");
            File.WriteAllText(storage.GetFullPath("data.json"), string.Empty);

            Assert.That(read("data.json"), Is.EqualTo("one"));
            Assert.That(storage.Exists("data.json" + LocalDurableFile.BROKEN_SUFFIX), Is.True);

            // Written back right away, so the next start finds it even if nothing is saved in between.
            Assert.That(contents("data.json"), Is.EqualTo("one"));
            Assert.That(contents("data.json" + LocalDurableFile.BACKUP_SUFFIX), Is.EqualTo("one"));
        }

        [Test]
        public void TestMissingFileFallsBackToBackup()
        {
            LocalDurableFile.WriteText(storage, "data.json", "one");
            LocalDurableFile.WriteText(storage, "data.json", "two");
            File.Delete(storage.GetFullPath("data.json"));

            Assert.That(read("data.json"), Is.EqualTo("one"));
            Assert.That(contents("data.json"), Is.EqualTo("one"));
        }

        [Test]
        public void TestEmptyFileNeverReplacesBackup()
        {
            LocalDurableFile.WriteText(storage, "data.json", "one");
            File.WriteAllText(storage.GetFullPath("data.json"), string.Empty);
            File.WriteAllText(storage.GetFullPath("data.json" + LocalDurableFile.BACKUP_SUFFIX), "good");

            LocalDurableFile.WriteText(storage, "data.json", "two");

            Assert.That(read("data.json"), Is.EqualTo("two"));
            Assert.That(contents("data.json" + LocalDurableFile.BACKUP_SUFFIX), Is.EqualTo("good"));
        }

        [Test]
        public void TestRejectedFileWithoutBackupStartsEmpty()
        {
            File.WriteAllText(storage.GetFullPath("data.json", true), "garbage");

            Assert.That(LocalDurableFile.Read<string>(storage, "data.json", _ => throw new InvalidDataException()), Is.Null);
            Assert.That(contents("data.json" + LocalDurableFile.BROKEN_SUFFIX), Is.EqualTo("garbage"));
        }

        [Test]
        public void TestUnsupportedFileIsLeftAlone()
        {
            File.WriteAllText(storage.GetFullPath("data.json", true), "future");

            Assert.Throws<NotSupportedException>(() => LocalDurableFile.Read<string>(storage, "data.json", _ => throw new NotSupportedException()));
            Assert.That(contents("data.json"), Is.EqualTo("future"));
        }
    }
}
