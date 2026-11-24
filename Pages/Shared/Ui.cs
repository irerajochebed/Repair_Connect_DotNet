using Microsoft.AspNetCore.Html;
namespace RepairConnect.Pages.Shared;

// Shared presentation helpers; all displayed records come from the server.
public static class Ui
{
    public static string ServiceIcon(string name) => name.ToLowerInvariant() switch { "plumbing" => "droplet", "electrical" => "bolt", "home appliances" => "appliance", _ => "tool" };
    public static IHtmlContent Icon(string name, string css = "")
    {
        var path = name switch
        {
            "droplet" => "<path d='M12 3s-7 7-7 12a7 7 0 0 0 14 0c0-5-7-12-7-12Z'/><path d='M8 15a4 4 0 0 0 4 4'/>",
            "bolt" => "<path d='m13 2-9 12h7l-1 8L20 9h-7l1-7Z'/>",
            "appliance" => "<rect x='4' y='3' width='16' height='18' rx='2'/><path d='M4 8h16M7 5.5h.01M10 5.5h.01'/><circle cx='12' cy='14' r='4'/>",
            "hammer" => "<path d='m14 12-8 9-3-3 9-8M10 5l3-3 8 8-3 3-3-3-3 1-3-3 1-3Z'/>",
            "paint" => "<rect x='3' y='3' width='14' height='6' rx='1'/><path d='M17 6h3v7h-9v3'/><rect x='9' y='16' width='4' height='6' rx='1'/>",
            "home" => "<path d='m3 10 9-7 9 7M5 9v12h14V9M9 21v-8h6v8'/>",
            "search" => "<circle cx='10.5' cy='10.5' r='6.5'/><path d='m16 16 5 5'/>",
            "pin" => "<path d='M20 10c0 6-8 12-8 12S4 16 4 10a8 8 0 1 1 16 0Z'/><circle cx='12' cy='10' r='2.5'/>",
            "check" => "<path d='m5 12 4 4L19 6'/>",
            "shield" => "<path d='m12 3 8 3v6c0 5-8 9-8 9s-8-4-8-9V6l8-3Z'/><path d='m8 12 3 3 5-6'/>",
            "clock" => "<circle cx='12' cy='12' r='9'/><path d='M12 7v5l3 2'/>",
            "file" => "<path d='M14 2H5v20h14V7l-5-5ZM14 2v6h5M8 12h8M8 16h6'/>",
            "users" => "<circle cx='9' cy='8' r='3'/><path d='M3 21v-3a6 6 0 0 1 12 0v3M16 5a3 3 0 0 1 0 6M18 15a5 5 0 0 1 3 4v2'/>",
            "user" => "<circle cx='12' cy='7' r='4'/><path d='M4 22v-3a8 8 0 0 1 16 0v3'/>",
            "plus" => "<path d='M12 5v14M5 12h14'/>",
            "chevron" => "<path d='m9 5 7 7-7 7'/>",
            "menu" => "<path d='M4 6h16M4 12h16M4 18h16'/>",
            "logout" => "<path d='M10 4H4v16h6M8 12h13m-5-5 5 5-5 5'/>",
            _ => "<path d='M14.7 6.3a5 5 0 0 0-6-3L12 7l-4 4-3.7-3.3a5 5 0 0 0 6 6L18 21l3-3-7.3-7.3a5 5 0 0 0 1-4.4Z'/>"
        };
        return new HtmlString($"<svg class='icon {css}' width='24' height='24' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='1.7' stroke-linecap='round' stroke-linejoin='round' aria-hidden='true'>{path}</svg>");
    }
}
