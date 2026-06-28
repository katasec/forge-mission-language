using Markdig;
using Microsoft.AspNetCore.Components;

namespace ForgeUI.Services;

public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public static MarkupString Render(string? markdown) =>
        new(string.IsNullOrEmpty(markdown) ? "" : Markdown.ToHtml(markdown, Pipeline));
}
