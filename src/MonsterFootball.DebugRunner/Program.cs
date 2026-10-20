// Debug runner for the headless match simulation.
//
//   match [seed] [--all] [--brief]  play one match and print a readable log (highlights by default) and statistics;
//                                   each action shows the ratings and odds behind it unless --brief
//   batch [count] [--from SEED]     play many matches in parallel, print aggregate figures and health checks;
//                                   exits 1 if any health check fails (used by CI)
//   fingerprint [seed ...]          SHA-256 of each seed's match fingerprint (default seeds 1–5);
//                                   CI compares this output across operating systems

using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MonsterFootball.Simulation;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Diagnostics;
using MonsterFootball.Simulation.Players;

var command = args.Length > 0 ? args[0] : "help";
var options = args.Skip(1).Where(a => a.StartsWith("--", StringComparison.Ordinal)).ToList();
var values = args.Skip(1).Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToArray();

switch (command)
{
    case "match":
        {
            var seed = values.Length > 0 ? int.Parse(values[0], CultureInfo.InvariantCulture) : 123456;
            Console.WriteLine($"Seed {seed}");
            Console.Write(MatchReport.Create(Play(seed), allEvents: options.Contains("--all"), details: !options.Contains("--brief")));
            return 0;
        }

    case "batch":
        {
            var count = values.Length > 0 ? int.Parse(values[0], CultureInfo.InvariantCulture) : 1000;
            var from = OptionValue("--from") ?? 1;
            var stopwatch = Stopwatch.StartNew();

            // Matches are independent; parallelism lives here, never inside the simulation (ADR-005).
            var summaries = new MatchSummary[count];
            Parallel.For(0, count, i => summaries[i] = MatchSummary.From(Play(from + i)));
            var batch = BatchSummary.From(summaries);

            Console.WriteLine($"{count} matches (seeds {from}–{from + count - 1}) in {stopwatch.Elapsed.TotalSeconds:0.0} s");
            Console.WriteLine();
            Console.WriteLine(FormatBatch(batch));
            Console.WriteLine();
            var checks = BatchHealth.Evaluate(batch);
            foreach (var check in checks)
                Console.WriteLine(check);

            var failed = checks.Count(c => !c.Passed);
            Console.WriteLine();
            Console.WriteLine(failed == 0 ? "HEALTHY" : $"PATHOLOGICAL: {failed} check(s) failed");
            return failed == 0 ? 0 : 1;
        }

    case "fingerprint":
        {
            var seeds = values.Length > 0 ? values.Select(v => int.Parse(v, CultureInfo.InvariantCulture)).ToArray() : [1, 2, 3, 4, 5];
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
        Console.WriteLine("  match [seed] [--all] [--brief] play one match and print the log and statistics");
        Console.WriteLine("  batch [count] [--from SEED]   play many matches and print aggregate figures and health checks");
        Console.WriteLine("  fingerprint [seed ...]        print a determinism hash per seed");
        return command == "help" ? 0 : 1;
}

int? OptionValue(string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? int.Parse(args[index + 1], CultureInfo.InvariantCulture) : null;
}

static MatchResult Play(int seed) =>
    new MatchSimulation(MatchConfig.Default, SampleTeam("Blue", 1), SampleTeam("Red", 101), seed).RunToCompletion();

static TeamDefinition SampleTeam(string name, int firstId) =>
    new(name, Enum.GetValues<PlayingPosition>().Select((slot, i) =>
        new PlayerDefinition(new PlayerId(firstId + i), $"{name} {slot}", i + 1, slot, PlayerStats.Uniform(5))));

static string FormatBatch(BatchSummary b) => string.Join('\n',
[
    string.Create(CultureInfo.InvariantCulture, $"Goals per match            {b.AverageGoals:0.00}   (max {b.MaxGoalsInMatch})"),
    string.Create(CultureInfo.InvariantCulture, $"Shots per match            {b.AverageShots:0.0}"),
    string.Create(CultureInfo.InvariantCulture, $"Shots on target per match  {b.AverageShotsOnTarget:0.0}"),
    string.Create(CultureInfo.InvariantCulture, $"Passes per match           {b.AveragePasses:0}"),
    string.Create(CultureInfo.InvariantCulture, $"Pass completion            {b.PassCompletion:P1}"),
    string.Create(CultureInfo.InvariantCulture, $"Home win / draw / away win {b.HomeWinShare:P1} / {b.DrawShare:P1} / {b.AwayWinShare:P1}"),
    string.Create(CultureInfo.InvariantCulture, $"Home possession            {b.AverageHomePossession:P1}"),
    string.Create(CultureInfo.InvariantCulture, $"Tackles per match          {b.AverageTackles:0}"),
    string.Create(CultureInfo.InvariantCulture, $"Carries per match          {b.AverageCarries:0}"),
    string.Create(CultureInfo.InvariantCulture, $"Possession changes         {b.AveragePossessionChanges:0}"),
    string.Create(CultureInfo.InvariantCulture, $"Striker share of goals     {b.StrikerGoalShare:P0}"),
    string.Create(CultureInfo.InvariantCulture, $"Outfield distance (km)     {b.AverageOutfieldDistanceKm:0.0}"),
    string.Create(CultureInfo.InvariantCulture, $"Outfield stamina at FT     {b.AverageOutfieldStaminaAtFullTime:0.0}"),
    string.Create(CultureInfo.InvariantCulture, $"Ball in play (min)         {b.AverageBallInPlayMinutes:0.0}"),
    string.Create(CultureInfo.InvariantCulture, $"Throw-ins per match        {b.AverageThrowIns:0.0}"),
    string.Create(CultureInfo.InvariantCulture, $"Goal kicks per match       {b.AverageGoalKicks:0.0}"),
    string.Create(CultureInfo.InvariantCulture, $"Corners per match          {b.AverageCorners:0.0}"),
    string.Create(CultureInfo.InvariantCulture, $"Offsides per match         {b.AverageOffsides:0.0}"),
    string.Create(CultureInfo.InvariantCulture, $"Fouls per match            {b.AverageFouls:0.0}"),
    string.Create(CultureInfo.InvariantCulture, $"Clearances per match       {b.AverageClearances:0.0}"),
    string.Create(CultureInfo.InvariantCulture, $"Yellow / red cards         {b.AverageYellowCards:0.00} / {b.AverageRedCards:0.00}"),
    string.Create(CultureInfo.InvariantCulture, $"Abandoned matches          {b.AbandonedShare:P1}"),
    string.Create(CultureInfo.InvariantCulture, $"Penalties per match        {b.AveragePenalties:0.00} ({b.PenaltyConversion:P0} scored)"),
    string.Create(CultureInfo.InvariantCulture, $"Added time per half (min)  {b.AverageAddedMinutesPerPeriod:0.0}"),
    string.Create(CultureInfo.InvariantCulture, $"Halves ended by safety cap {b.SafetyCapPeriodShare:P0}"),
]);
