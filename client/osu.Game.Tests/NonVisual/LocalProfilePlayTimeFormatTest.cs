// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.

using System;
using System.Reflection;
using NUnit.Framework;
using osu.Game.LocalPriority;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    [SetCulture("en-US")]
    public class LocalProfilePlayTimeFormatTest
    {
        private static string call(string method, double seconds) =>
            (string)typeof(LocalProfileOverlay).GetNestedType("DetailSection", BindingFlags.NonPublic)!
                                               .GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!
                                               .Invoke(null, new object[] { TimeSpan.FromSeconds(seconds) })!;

        [TestCase(0, "0h 0m", "0 minutes")]
        [TestCase(59, "0h 0m", "0 minutes")]
        [TestCase(61, "0h 1m", "1 minute")]
        [TestCase(80 * 60, "1h 20m", "80 minutes")]
        [TestCase(1 * 3600 + 59 * 60 + 40, "1h 59m", "2 hours")]
        [TestCase(23 * 3600 + 59 * 60 + 59, "23h 59m", "24 hours")]
        [TestCase(24 * 3600, "1d 0h 0m", "24 hours")]
        [TestCase(3 * 86400 + 4 * 3600 + 12 * 60 + 30, "3d 4h 12m", "76 hours")]
        [TestCase(1234 * 86400.0 + 5 * 3600, "1,234d 5h 0m", "29,621 hours")]
        public void TestFormat(double seconds, string value, string tooltip)
        {
            Assert.That(call("playTime", seconds), Is.EqualTo(value));
            Assert.That(call("playTimeTooltip", seconds), Is.EqualTo(tooltip));
        }
    }
}
