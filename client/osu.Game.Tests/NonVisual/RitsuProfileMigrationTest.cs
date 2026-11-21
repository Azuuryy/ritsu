// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.LocalPriority;
using osu.Game.Offline;

namespace osu.Game.Tests.NonVisual
{
    public class RitsuProfileMigrationTest
    {
        [Test]
        public void TestLinkedStorageProtectsRitsuRecordings()
        {
            using var storage = new TemporaryNativeStorage(nameof(RitsuProfileMigrationTest));
            var profiles = new RitsuProfileManager(storage, null!);
            Guid id = profiles.ActiveProfileId;
            Guid localScore = Guid.NewGuid();
            Guid externalScore = Guid.NewGuid();
            profiles.AssociateNewScore(localScore, id, "osu", 0, "[]", localPlay: true);
            profiles.AssociateNewScore(externalScore, id, "osu", 0, "[]");

            using var linked = new LocalProfileManager(new LocalProfileStore(storage), profiles, linkedStorage: true, migratingProfiles: false);
            using var isolated = new LocalProfileManager(new LocalProfileStore(storage), profiles, linkedStorage: false, migratingProfiles: false);
            Assert.That(linked.CanDeleteScore(id, localScore), Is.False);
            Assert.That(isolated.CanDeleteScore(id, localScore), Is.True);
            Assert.That(isolated.CanDeleteScore(id, externalScore), Is.False);
        }

        [Test]
        public void TestMigrationKeepsProfileIdsAndExistingScoreAssociations()
        {
            using var storage = new TemporaryNativeStorage(nameof(RitsuProfileMigrationTest));
            var oldProfiles = new RitsuProfileManager(storage, null!);
            Guid first = oldProfiles.ActiveProfileId;
            oldProfiles.AddProfile("Second");
            Guid second = oldProfiles.ActiveProfileId;
            oldProfiles.SetIncludeRelaxAutopilot(true);
            Guid scoreId = Guid.NewGuid();
            oldProfiles.AssociateNewScore(scoreId, first, "osu", 0, "[]");

            var profiles = new RitsuProfileManager(storage, null!);
            var store = new LocalProfileStore(storage);
            Assert.That(store.MigrateRitsuProfiles(profiles), Is.True);
            Assert.That(store.GetProfiles().Select(profile => profile.Id), Is.EqualTo(new[] { first, second }));
            Assert.That(store.ActiveProfile?.Id, Is.EqualTo(second));
            Assert.That(store.ActiveProfile?.IncludeRelaxAutopilot, Is.True);
            Assert.That(profiles.GetScoreProfileId(scoreId), Is.EqualTo(first));

            store.CompleteRitsuMigration();
            var reopened = new LocalProfileStore(storage);
            Assert.That(reopened.MigrateRitsuProfiles(new RitsuProfileManager(storage, null!)), Is.False);
            Assert.That(reopened.ActiveProfile?.Id, Is.EqualTo(second));
            Assert.That(reopened.GetProfiles().Count, Is.EqualTo(2));
        }
    }
}
