namespace ControleAcademiaAPI.Services
{
    public interface IJwtToken
    {
        string GenerateToken(string username, string role);
    }
}
