// Debug runner for the headless match simulation.
// Single-match and batch modes arrive in later M1 subtasks (M1.10, M1.11).
//
//   fingerprint [seed ...]   SHA-256 of each seed's match fingerprint (default seeds 1–5).
//                            CI compares this output across operating systems.

using System.Security.Cryptography;
using System.Text;
using MonsterFootball.Simulation;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Diagnostics;
using MonsterFootball.Simulation.Players;

if (args.Length > 0 && args[0] == "fingerprint")
{
    var seeds = args.Length > 1 ? args[1..].Select(int.Parse).ToArray() : [1, 2, 3, 4, 5];
    foreach (var seed in seeds)
    {
        var result = new MatchSimulation(MatchConfig.Default, SampleTeam("Blue", 1), SampleTeam("Red", 101), seed).RunToCompletion();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(MatchFingerprint.Create(result))));
        Console.WriteLine($"{seed} {result.HomeGoals}-{result.AwayGoals} {hash}");
    }

    return 0;
}

Console.WriteLine("Monster Football debug runner");
Console.WriteLine("Commands: fingerprint [seed ...]");
Console.WriteLine("Single-match and batch modes arrive in M1.10 and M1.11.");
return 0;

static TeamDefinition SampleTeam(string name, int firstId) =>
    new(name, Enum.GetValues<PlayingPosition>().Select((slot, i) =>
        new PlayerDefinition(new PlayerId(firstId + i), $"{name} {slot}", i + 1, slot, PlayerStats.Uniform(5))));
