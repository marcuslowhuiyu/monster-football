// Debug runner for the headless match simulation.
//
//   match [seed] [--all]     play one match and print a readable log (highlights by default) and statistics
//   fingerprint [seed ...]   SHA-256 of each seed's match fingerprint (default seeds 1–5);
//                            CI compares this output across operating systems.
//
// Batch mode arrives in M1.11.

using System.Security.Cryptography;
using System.Text;
using MonsterFootball.Simulation;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Diagnostics;
using MonsterFootball.Simulation.Players;

var command = args.Length > 0 ? args[0] : "help";
var options = args.Skip(1).Where(a => a.StartsWith("--", StringComparison.Ordinal)).ToHashSet();
var values = args.Skip(1).Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToArray();

switch (command)
{
    case "match":
        {
            var seed = values.Length > 0 ? int.Parse(values[0]) : 123456;
            var result = Play(seed);
            Console.WriteLine($"Seed {seed}");
            Console.Write(MatchReport.Create(result, allEvents: options.Contains("--all")));
            return 0;
        }

    case "fingerprint":
        {
            var seeds = values.Length > 0 ? values.Select(int.Parse).ToArray() : [1, 2, 3, 4, 5];
            foreach (var seed in seeds)
            {
                var result = Play(seed);
                var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(MatchFingerprint.Create(result))));
                Console.WriteLine($"{seed} {result.HomeGoals}-{result.AwayGoals} {hash}");
            }

            return 0;
        }

    default:
        Console.WriteLine("Monster Football debug runner");
        Console.WriteLine("  match [seed] [--all]     play one match and print the log and statistics");
        Console.WriteLine("  fingerprint [seed ...]   print a determinism hash per seed");
        return command == "help" ? 0 : 1;
}

static MatchResult Play(int seed) =>
    new MatchSimulation(MatchConfig.Default, SampleTeam("Blue", 1), SampleTeam("Red", 101), seed).RunToCompletion();

static TeamDefinition SampleTeam(string name, int firstId) =>
    new(name, Enum.GetValues<PlayingPosition>().Select((slot, i) =>
        new PlayerDefinition(new PlayerId(firstId + i), $"{name} {slot}", i + 1, slot, PlayerStats.Uniform(5))));
