using System.Net;
using System.Text.RegularExpressions;

namespace KroModIx.Plugin.Satisfactory.Services.Ficsit;

/// <summary>Konvertiert Mod-Beschreibungen (Mix aus Markdown, HTML und
/// BBCode) in Plain-Text. ficsit.app-Descriptions sind primär Markdown mit
/// Shields.io-Badges und GitHub-Links; das Icarus-Muster (HTML+BBCode) bleibt
/// als Fallback erhalten für Cross-Platform-Kompatibilität.
///
/// <para>Verhalten in Reihenfolge:</para>
/// <list type="number">
/// <item><b>Markdown</b>: Headings <c># </c>/<c>## </c>/<c>### </c> → Text
///   ohne Prefix (falls Rest existiert); Bold/Italic <c>**text**</c>/<c>*text*</c>/
///   <c>_text_</c> → text; Links <c>[Label](URL)</c> → Label; Bilder
///   <c>![alt](URL)</c> → <c>[Bild]</c>; Inline-Code <c>`code`</c> → code;
///   Code-Blocks <c>```…```</c> → Inhalt; Zitate <c>&gt; text</c> → <c>» text</c>;
///   Listen <c>- </c>/<c>* </c>/<c>1. </c> → <c>• </c>; Trenner
///   <c>---</c>/<c>***</c> → Leerzeile.</item>
/// <item><b>BBCode</b>: <c>[url=…]Text[/url]</c> → Text; <c>[img]…[/img]</c> weg;
///   <c>[*]</c> → „• "; <c>[br]</c>/<c>[hr]</c> → Zeilenumbruch;
///   generischer Fallback strippt alle <c>[tag]</c>/<c>[/tag]</c>-Wrapper.</item>
/// <item><b>HTML</b>: <c>&lt;br&gt;</c>/<c>&lt;/p&gt;</c> → Umbruch;
///   <c>&lt;li&gt;</c> → „• "; alle sonstigen Tags weg; HTML-Entities
///   werden dekodiert.</item>
/// <item>Aufräumen: Trailing-Spaces vor Newlines weg; ≥3 Newlines → 2.</item>
/// </list>
/// </summary>
public static class HtmlStrip
{
    // Markdown (VOR BBCode/HTML — Markdown kann `[link](url)`-Klammern haben,
    // die BBCode nicht triggern sollen, und `**` darf nicht als HTML gesehen werden)
    // Code-Block ```…``` — Inhalt bleibt, Fences weg.
    private static readonly Regex MdCodeBlockRegex = new(@"```[a-zA-Z]*\n?(.*?)\n?```",
        RegexOptions.Compiled | RegexOptions.Singleline);
    // Bild ![alt](url) → [Bild]  — VOR Link-Regex weil das ! sonst als
    // Teil einer Zeile davor erscheinen könnte.
    private static readonly Regex MdImageRegex = new(@"!\[[^\]]*\]\([^\)]*\)", RegexOptions.Compiled);
    // Link [Label](url) → Label
    private static readonly Regex MdLinkRegex = new(@"\[([^\]]+)\]\([^\)]*\)", RegexOptions.Compiled);
    // Auto-Link <https://…> → https://…
    private static readonly Regex MdAutoLinkRegex = new(@"<(https?://[^>]+)>", RegexOptions.Compiled);
    // Heading am Zeilenanfang: `# ` bis `###### ` → Prefix weg
    private static readonly Regex MdHeadingRegex = new(@"^\s{0,3}#{1,6}\s+", RegexOptions.Compiled | RegexOptions.Multiline);
    // Bold ** oder __ (nicht greedy, single-line)
    private static readonly Regex MdBoldRegex = new(@"\*\*([^*\n]+?)\*\*|__([^_\n]+?)__", RegexOptions.Compiled);
    // Italic * oder _ (auch nicht greedy, aber vorsichtig — nicht mitten im Wort)
    private static readonly Regex MdItalicRegex = new(@"(?<![*\w])\*([^*\n]+?)\*(?!\w)|(?<![_\w])_([^_\n]+?)_(?!\w)", RegexOptions.Compiled);
    // Inline-Code `text` → text
    private static readonly Regex MdCodeInlineRegex = new(@"`([^`\n]+?)`", RegexOptions.Compiled);
    // Zitat am Zeilenanfang: `> ` → `» `
    private static readonly Regex MdBlockquoteRegex = new(@"^\s{0,3}>\s?", RegexOptions.Compiled | RegexOptions.Multiline);
    // Listen-Item am Zeilenanfang: `- `/`* `/`+ `/`1. ` → `• `
    private static readonly Regex MdListItemRegex = new(@"^\s{0,3}(?:[-*+]|\d+\.)\s+", RegexOptions.Compiled | RegexOptions.Multiline);
    // Horizontale Trenner: `---`/`***`/`___` als eigene Zeile → Leerzeile
    private static readonly Regex MdHrRegex = new(@"^\s{0,3}(?:-{3,}|\*{3,}|_{3,})\s*$", RegexOptions.Compiled | RegexOptions.Multiline);

    // BBCode
    private static readonly Regex BbBrRegex = new(@"\[br\s*/?\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BbHrRegex = new(@"\[hr\s*/?\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BbListItemRegex = new(@"\[\*\]", RegexOptions.Compiled);
    private static readonly Regex BbUrlNamedRegex = new(@"\[url=[^\]]*\](.*?)\[/url\]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex BbImgRegex = new(@"\[img[^\]]*\].*?\[/img\]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex BbTagRegex = new(@"\[/?[a-zA-Z\*][a-zA-Z0-9]*(?:=[^\]]*)?\]", RegexOptions.Compiled);

    // HTML
    private static readonly Regex BrRegex = new(@"<br\s*/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PEndRegex = new(@"</p>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LiStartRegex = new(@"<li[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TagRegex = new(@"<[^>]+>", RegexOptions.Compiled);

    private static readonly Regex MultiNewlineRegex = new(@"\n{3,}", RegexOptions.Compiled);
    private static readonly Regex TrailingSpacesRegex = new(@"[ \t]+\n", RegexOptions.Compiled);

    public static string ToPlainText(string? source)
    {
        if (string.IsNullOrEmpty(source)) return "";
        var s = source.Replace("\r\n", "\n").Replace('\r', '\n');

        // 1. Markdown — Reihenfolge wichtig: Code-Block/Image/Link vor Bold/Italic
        //    (die im Content von Links vorkommen könnten).
        s = MdCodeBlockRegex.Replace(s, "$1");
        s = MdImageRegex.Replace(s, "[Bild]");
        s = MdLinkRegex.Replace(s, "$1");
        s = MdAutoLinkRegex.Replace(s, "$1");
        s = MdHeadingRegex.Replace(s, "");
        s = MdBoldRegex.Replace(s, m => !string.IsNullOrEmpty(m.Groups[1].Value) ? m.Groups[1].Value : m.Groups[2].Value);
        s = MdItalicRegex.Replace(s, m => !string.IsNullOrEmpty(m.Groups[1].Value) ? m.Groups[1].Value : m.Groups[2].Value);
        s = MdCodeInlineRegex.Replace(s, "$1");
        s = MdBlockquoteRegex.Replace(s, "» ");
        s = MdListItemRegex.Replace(s, "• ");
        s = MdHrRegex.Replace(s, "");

        // 2. BBCode
        s = BbImgRegex.Replace(s, "");
        s = BbUrlNamedRegex.Replace(s, "$1");
        s = BbBrRegex.Replace(s, "\n");
        s = BbHrRegex.Replace(s, "\n");
        s = BbListItemRegex.Replace(s, "\n• ");
        s = BbTagRegex.Replace(s, "");

        // 3. HTML
        s = BrRegex.Replace(s, "\n");
        s = PEndRegex.Replace(s, "\n\n");
        s = LiStartRegex.Replace(s, "\n• ");
        s = TagRegex.Replace(s, "");

        s = WebUtility.HtmlDecode(s);
        s = TrailingSpacesRegex.Replace(s, "\n");
        s = MultiNewlineRegex.Replace(s, "\n\n");
        return s.Trim();
    }
}
