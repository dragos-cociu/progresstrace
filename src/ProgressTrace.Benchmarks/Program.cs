using System.Text.Json;
using System.Text.Json.Serialization;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    if (args.Length != 5 || args[0] != "run" || args[1] != "--input" || args[3] != "--output")
    {
        Console.Error.WriteLine("Usage: progresstrace-benchmarks run --input <directory> --output <path>");
        return 2;
    }

    try
    {
        var result = ProgressTrace.Benchmarks.BenchmarkRunner.Run(args[2]);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(result, options);
        await File.WriteAllBytesAsync(args[4], [.. bytes, (byte)'\n']);
        return 0;
    }
    catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException)
    {
        Console.Error.WriteLine("Benchmark run failed: input could not be processed.");
        return 1;
    }
}
