using System.ComponentModel.DataAnnotations.Schema;

namespace Content.Server.Database;

public partial class Preference
{
    [Column("chat_panel_settings")]
    public string ChatPanelSettings { get; set; } = "";
}
