namespace tp_aspire_samy_jugurtha.ApiService.Data.Models;

public class Utilisateur
{
    public int UtilisateurID { get; set; }
    public string? ExternalID { get; set; }
    public string NomComplet { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Role { get; set; } = "Employe";
}