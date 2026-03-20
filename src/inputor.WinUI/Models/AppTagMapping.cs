namespace Inputor.App.Models;

public sealed class AppTagMapping
{
    public string AppName { get; init; } = string.Empty;
    public List<string> Tags { get; init; } = [];
}
