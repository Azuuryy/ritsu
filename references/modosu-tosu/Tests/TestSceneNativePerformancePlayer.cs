using System;
using System.IO;
using System.Text.Json;
using NUnit.Framework;
using osu.Game.Rulesets.Modosu.Integration;
using osu.Game.Rulesets.Modosu.Mods;
using osu.Game.Rulesets.Modosu.Tests.Mods;

namespace osu.Game.Rulesets.Modosu.Tests.Screens
{
    public partial class TestSceneNativePerformancePlayer : OsuModTestScene
    {
        [Test]
        public void TestNativePerformanceLivePlayerWithFsa()
        {
            CreateModTest(new ModTestData
            {
                Mod = new OsuModFunSpacingAdjust(),
                Autoplay = true,
                PassCondition = () => readFrame() is { Phase: "play", Pp: > 0, TotalStars: > 0, Hits.Great: > 5 } frame &&
                    frame.ModsJson.Contains("FSA") && frame.ProcessId == Environment.ProcessId
            });
        }

        private static ModosuPerformanceFrame? readFrame()
        {
            try
            {
                string path = Path.Combine(Path.GetTempPath(), "modosu-tosu", $"{Environment.ProcessId}.json");
                return JsonSerializer.Deserialize<ModosuPerformanceFrame>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            catch (IOException)
            {
                return null;
            }
        }
    }
}
