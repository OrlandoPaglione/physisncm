namespace ControleAcademiaAPI.Services
{
    public interface ICriptografiaService
    {
        string CriptografarSenha(string login, string senha);
    }
}
