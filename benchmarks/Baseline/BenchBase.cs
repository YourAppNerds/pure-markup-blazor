using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Baseline;

public interface IBenchTarget
{
    void BuildPublic(RenderTreeBuilder builder);
    void Rerender();
    void Toggle();
}

public abstract class BenchBase : ComponentBase
{
    public void BuildPublic(RenderTreeBuilder builder) => BuildRenderTree(builder);
    public void Rerender() => StateHasChanged();
}

public sealed record Row(int Id, string Name, string Email, int Score);

public static class BenchData
{
    public static readonly Row[] Rows = Enumerable.Range(1, 1000)
        .Select(i => new Row(i, $"Person {i}", $"person{i}@example.com", (i * 37) % 100)).ToArray();
}
