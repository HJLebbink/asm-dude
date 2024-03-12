using AsmDude2LS;

try
{
    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddHostedService<Worker>();

    var host = builder.Build();
    host.Run();
} catch (Exception e)
{
    Console.WriteLine(e);
}
