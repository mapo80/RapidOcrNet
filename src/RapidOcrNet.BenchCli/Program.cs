using System.CommandLine;
using System.CommandLine.Invocation;
using RapidOcrNet.BenchCli;

var imagesOpt = new Option<string>("--images", "Folder or file with test images") { IsRequired = true };
var recOpt = new Option<string>("--rec", "Path to recognition ONNX model") { IsRequired = true };
var detOpt = new Option<string?>("--det", "Path to detection ONNX model");
var clsOpt = new Option<string?>("--cls", "Path to classification ONNX model");
var outOpt = new Option<string>("--out", "Output directory") { IsRequired = true };
var jsonOpt = new Option<bool>("--json", "Save per-image JSON results");
var runsOpt = new Option<int>("--runs", () => 3, "Repetitions for latency measurement");

var root = new RootCommand("RapidOcrNet benchmark CLI")
{
    imagesOpt, recOpt, detOpt, clsOpt, outOpt, jsonOpt, runsOpt
};

root.SetHandler(async (string images, string rec, string? det, string? cls, string outDir, bool json, int runs) =>
{
    var opts = new BenchOptions
    {
        Images = images,
        Rec = rec,
        Det = det,
        Cls = cls,
        OutDir = outDir,
        SaveJson = json,
        Runs = runs
    };
    await Bench.RunAsync(opts);
}, imagesOpt, recOpt, detOpt, clsOpt, outOpt, jsonOpt, runsOpt);

return await root.InvokeAsync(args);
