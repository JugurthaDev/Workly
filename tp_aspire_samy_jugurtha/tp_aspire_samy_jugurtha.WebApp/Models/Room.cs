namespace tp_aspire_samy_jugurtha.WebApp.Models;

public class Room
{
    public int Id { get; set; }
    public int WorkspaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
}
