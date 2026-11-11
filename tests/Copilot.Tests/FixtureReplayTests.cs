using System.Collections.Generic;
using System.IO;
using Copilot.Engine.Lethal;
using Copilot.Engine.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Copilot.Tests
{
    /// <summary>
    /// Replays every fixtures/**/*.json file. A fixture is { "name", "expected", "snapshot" } where
    /// "snapshot" is exactly what the HDT plugin writes. Fixtures without "expected" are skipped
    /// (raw recordings that have not been reviewed yet).
    /// </summary>
    public class FixtureReplayTests
    {
        public static IEnumerable<object[]> Fixtures()
        {
            var dir = Path.Combine(System.AppContext.BaseDirectory, "fixtures");
            var any = false;
            if (Directory.Exists(dir))
                foreach (var f in Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories))
                {
                    any = true;
                    yield return new object[] { Path.GetRelativePath(dir, f) };
                }
            if (!any) yield return new object[] { "" };
        }

        [Theory]
        [MemberData(nameof(Fixtures))]
        public void Replay(string relPath)
        {
            if (relPath == "") return;
            var obj = JObject.Parse(File.ReadAllText(Path.Combine(System.AppContext.BaseDirectory, "fixtures", relPath)));
            var expected = (string)obj["expected"];
            if (expected == null) return;
            var snap = obj["snapshot"].ToObject<GameSnapshot>();
            var r = LethalSolver.Solve(snap);
            Assert.Equal(expected, r.Status.ToString());
        }
    }
}
