namespace tp_aspire_samy_jugurtha.ApiService.Data.Entities;

public class Desk
{
    public int Id { get; set; }
    public int WorkspaceId { get; set; }
    public string Code { get; set; } = string.Empty;

    public Workspace? Workspace { get; set; }
}
