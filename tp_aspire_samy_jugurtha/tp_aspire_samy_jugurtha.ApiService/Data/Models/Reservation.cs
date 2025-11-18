namespace tp_aspire_samy_jugurtha.ApiService.Data.Models;

public class Reservation
{
    public int ReservationID { get; set; }
    public int SalleID { get; set; }
    public int UtilisateurID { get; set; }
    public string Objet { get; set; } = null!;
    public DateTime HeureDebut { get; set; }
    public DateTime HeureFin { get; set; }

    public Salle? Salle { get; set; }
    public Utilisateur? Utilisateur { get; set; }
}