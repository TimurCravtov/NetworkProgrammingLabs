using Microsoft.VisualStudio.TestPlatform.TestHost;


public class Simulation
{
    [Fact]
    public async void StartSimulation()
    {
        string[] args = Array.Empty<string>();
        
        var app = await Program.StartServerAsync(args);
        await app.StartAsync();
    }
}
