namespace tp_aspire_samy_jugurtha.ApiService.Data.Entities;

public class Workspace
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;

    public ICollection<Room> Rooms { get; set; } = new List<Room>();
    public ICollection<Desk> Desks { get; set; } = new List<Desk>();
}