namespace tp_aspire_samy_jugurtha.ApiService.Data.Models;

public class Salle
{
    public int SalleID { get; set; }
    public string NomSalle { get; set; } = null!;
    public string? Emplacement { get; set; }
    public int Capacite { get; set; }
    public bool EstActive { get; set; } = true;
}