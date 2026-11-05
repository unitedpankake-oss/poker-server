namespace PokerServer;

internal static class WebClientPage
{
    // Keep the page, styles and client in one response. The UI must also work
    // when a proxy blocks asset URLs or the process has a different content root.
    public static string Load()
    {
        var html = Read("index.html");
        var css = Read("style.css");
        var signalR = Read("vendor.signalr-8.0.29.min.js");
        var client = Read("app.js");
        return html.Replace("<link rel=\"stylesheet\" href=\"/style.css\">", $"<style>{css}</style>")
            .Replace("<script src=\"/app.js\" defer></script>", "")
            .Replace("</body>", $"<script>{signalR}</script><script>{client}</script></body>");
    }

    private static string Read(string name)
    {
        using var stream = typeof(WebClientPage).Assembly
            .GetManifestResourceStream($"PokerServer.wwwroot.{name}")
            ?? throw new InvalidOperationException($"Missing embedded web client resource: {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
