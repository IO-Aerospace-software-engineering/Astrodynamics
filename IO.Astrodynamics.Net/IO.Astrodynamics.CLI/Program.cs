using System;
using System.Reflection;
using IO.Astrodynamics.CLI.Commands;

namespace IO.Astrodynamics.CLI;

using Cocona;

class Program
{
    static void Main(string[] args)
    {
        var builder = CoconaApp.CreateBuilder();

        var app = builder.Build();
        app.AddCommands<EphemerisCommand>();
        app.AddCommands<OrientationCommand>();
        app.AddCommands<GeometryFinderCommand>();
        app.AddCommands<OrbitalParametersConverterCommand>();
        app.AddCommands<FrameConverterCommand>();
        app.AddCommands<TimeConverterCommand>();
        app.AddCommands<BodyInformationCommand>();
        app.AddCommands<PropagateCommand>();
        var assName = Assembly.GetExecutingAssembly().GetName();
        app.AddSubCommand("--version", (a) => { Console.WriteLine($"{assName.Name} v{assName.Version} experimental"); });

        app.Run();
    }
}