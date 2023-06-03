using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using ProyectoFinalLab3.Models;
using Newtonsoft.Json;
using System.Text;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace ProyectoFinalLab3.Controllers;

[Route("[controller]")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class UsuarioController : ControllerBase
{   
    private readonly DataContext _context;
    private readonly IConfiguration config;
    private readonly IWebHostEnvironment environment;
    private const string ClientId = "hrf9wxam9zk4vcvevscji61l2jr8wj";
    private const string ClientSecret = "1az53ys5j6yxmd2t0oku561ouci6qh";
    public UsuarioController(DataContext context, IConfiguration config, IWebHostEnvironment environment, IHttpClientFactory _httpClientFactory)
    {
        this._context = context;
        this.config = config;
        this.environment = environment;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> login([FromBody] LoginView loginView)
    {
        try
        {
            var usuario = _context.Usuarios.FirstOrDefault(x => x.Email == loginView.Email);
            if (usuario == null)
            {
                return NotFound();
            }

            string hashed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password: loginView.Clave,
                salt: System.Text.Encoding.ASCII.GetBytes(config["Salt"]),
                prf: KeyDerivationPrf.HMACSHA1,
                iterationCount: 1000,
                numBytesRequested: 256 / 8));
            var user = await _context.Usuarios.FirstOrDefaultAsync(x => x.Email == loginView.Email);
            if (user == null || user.Clave != hashed)
            {
                return BadRequest("Nombre de usuario o clave incorrecta");
            }
            else
            {
                var key = new SymmetricSecurityKey(
                    System.Text.Encoding.ASCII.GetBytes(config["TokenAuthentication:SecretKey"]));
                var credenciales = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
                var claims = new List<Claim>
                {   
                    new Claim("Id", user.Id+""),
                    new Claim(ClaimTypes.Name, user.Email),
                    new Claim("FullName", user.Nombre + " " + user.Apellido),
                };

                var token = new JwtSecurityToken(
                    issuer: config["TokenAuthentication:Issuer"],
                    audience: config["TokenAuthentication:Audience"],
                    claims: claims,
                    expires: DateTime.Now.AddMinutes(60),
                    signingCredentials: credenciales
                );
                return Ok(new JwtSecurityTokenHandler().WriteToken(token));
            }
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("registro")]
    [AllowAnonymous]
    public async Task<IActionResult> registroUsuario([FromBody] Usuario usuario){
        try
        {
            int count = _context.Usuarios.Count(x => x.Email == usuario.Email);
            if(count >= 1){
                return BadRequest("Email ya esta ocupado");
            }
            string hashed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password: usuario.Clave,
                salt: System.Text.Encoding.ASCII.GetBytes(config["Salt"]),
                prf: KeyDerivationPrf.HMACSHA1,
                iterationCount: 1000,
                numBytesRequested: 256 / 8));

            usuario.Clave = hashed;
            var addUsuario = _context.Add(usuario);
            return Ok(_context.SaveChanges());
        }
        catch (Exception ex)
        {
            
            return BadRequest(ex.Message);
        }
    }

    	private Usuario ObtenerUsuarioLogueado()
    {
        var email = User.Identity.Name;
        var usuario = _context.Usuarios.FirstOrDefault(p => p.Email == email);
        return usuario;
    }

    [HttpGet("perfil")]
    public IActionResult GetUsuarioActual()
    {
        var identity = HttpContext.User.Identity as ClaimsIdentity;
        if (identity != null)
        {
            var emailClaim = identity.FindFirst(ClaimTypes.Name);
            var fullNameClaim = identity.FindFirst("FullName");
            var roleClaim = identity.FindFirst(ClaimTypes.Role);
            
            var email = emailClaim?.Value;
            var fullName = fullNameClaim?.Value;
            var role = roleClaim?.Value;

            var usuario = _context.Usuarios.FirstOrDefault(x => x.Email == email);
            //Falta traer los juegos a los que se hizo pregunta recientemente
            if (usuario != null)
            {
                return Ok(usuario);
            }
        }

        return Unauthorized();
    }

    [HttpPut("editar")]
    public IActionResult modificarPerfil([FromBody] Usuario usuarioEditado)
    {
        Usuario usuarioActual = ObtenerUsuarioLogueado();
        int count = _context.Usuarios.Count(x => x.Email == usuarioEditado.Email);
        if(usuarioActual.Clave == usuarioEditado.Clave){

            usuarioActual.Nombre = usuarioEditado.Nombre;
            usuarioActual.Apellido = usuarioEditado.Apellido;
            usuarioActual.NombreUsuario = usuarioEditado.NombreUsuario;
            if(count >= 1){
                return BadRequest("Email ya esta ocupado");
            }else{
                usuarioActual.Email = usuarioEditado.Email;
            }

        }else{
            return BadRequest("Clave Incorrecta");
        }


        return Ok(_context.SaveChanges());

        
    }

    [HttpPut("editar-contraseña")]
    public IActionResult modificarContraseña([FromBody] EditContraseñaView editContraseña)
    {
        Usuario usuarioActual = ObtenerUsuarioLogueado();
        string contraseña_antigua = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password: editContraseña.contraseñaAntigua,
                salt: System.Text.Encoding.ASCII.GetBytes(config["Salt"]),
                prf: KeyDerivationPrf.HMACSHA1,
                iterationCount: 1000,
                numBytesRequested: 256 / 8));
        string contraseña_nueva = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password: editContraseña.contraseñaNueva,
                salt: System.Text.Encoding.ASCII.GetBytes(config["Salt"]),
                prf: KeyDerivationPrf.HMACSHA1,
                iterationCount: 1000,
                numBytesRequested: 256 / 8));

        if(usuarioActual.Clave == contraseña_antigua)
        {
            usuarioActual.Clave = contraseña_nueva;
            return Ok(_context.SaveChanges());
        }else{
            return BadRequest("Contraseña antigua es incorrecta");
        }

        
    }


}