using Microsoft.Maui.Graphics;
using Markdig;
using System.Net;

namespace MauiLesson_4_ChatWithAI_v3;

public class ChatMessage
{
    public string Text { get; set; } = string.Empty;
    public bool IsUser { get; set; }

    public LayoutOptions Alignment => IsUser ? LayoutOptions.End : LayoutOptions.Start;
    public Color BackgroundColor => IsUser ? Color.FromArgb("#D1F2EB") : Color.FromArgb("#EAECEE");

    public string FormattedText
    {
        get
        {
            if (IsUser)
                return WebUtility.HtmlEncode(Text);
            else
                return Markdown.ToHtml(Text ?? "");
        }
    }
}