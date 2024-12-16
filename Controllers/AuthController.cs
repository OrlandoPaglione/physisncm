using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly JwtTokenService _jwtTokenService;

    public AuthController(JwtTokenService jwtTokenService)
    {
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost("login")]
    [AllowAnonymous] // Permite acesso sem autenticação
    public IActionResult Login([FromBody] LoginRequest request)
    {
        // Substitua essa lógica por validação real
        if (request.Username == "admin" && request.Password == "password")
        {
            var token = _jwtTokenService.GenerateToken("1974"); // Exemplo de ID do usuário
            return Ok(new { Token = token });
        }

        return Unauthorized();
    }
}

public class LoginRequest
{
    public string Username { get; set; }
    public string Password { get; set; }
}
