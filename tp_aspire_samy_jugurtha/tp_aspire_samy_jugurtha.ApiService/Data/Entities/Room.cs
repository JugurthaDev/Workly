namespace tp_aspire_samy_jugurtha.ApiService.Data.Entities;

public class Room
{
    public int Id { get; set; }
    public int WorkspaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }

    public Workspace? Workspace { get; set; }
}
