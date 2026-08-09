// GsiCapture — a standalone development tool (never shipped in the
// production app) that records raw GSI payloads to disk, for replacing
// this project's hand-built fixtures with real captures from a live
// Dota 2 client. Per docs/FAIR_PLAY.md's "no screenshots stored by
// default" spirit applied to this analogous case: this tool is an
// explicit, deliberate, developer-invoked capture, never something that
// runs silently in the shipped application.
//
// Intended to run ALONGSIDE the normal overlay (which listens on a
// separate port), not instead of it — see docs/GSI_SETUP.md for wiring
// both into the GSI .cfg's "uri" via a second gamestate_integration
// config file.

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

var port = args.Length > 0 && int.TryParse(args[0], out var p) ? p : 3001;
var outputDir = args.Length > 1 ? args[1] : Path.Combine(AppContext.BaseDirectory, "captured");
Directory.CreateDirectory(outputDir);

Console.WriteLine($"GsiCapture listening on http://127.0.0.1:{port}/gsi");
Console.WriteLine($"Saving payloads to: {outputDir}");
Console.WriteLine("Press Ctrl+C to stop.");

var builder = WebApplication.CreateSlimBuilder();
builder.WebHost.UseKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, port));
builder.Logging.ClearProviders();

var app = builder.Build();

var sequence = 0;
var startedAt = DateTime.Now;

app.MapPost("/gsi", async (HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();

    var index = Interlocked.Increment(ref sequence);
    var fileName = $"{index:D4}-{DateTime.Now:HHmmss}.json";
    var path = Path.Combine(outputDir, fileName);
    await File.WriteAllTextAsync(path, body);

    Console.WriteLine($"[{index:D4}] saved {body.Length} bytes -> {fileName}");

    return Results.Ok();
});

await app.RunAsync();
