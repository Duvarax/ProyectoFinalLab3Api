using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using ProyectoFinalLab3.Models;
using Newtonsoft.Json;
using System.Text;
using Google.Cloud.Translation.V2;

namespace ProyectoFinalLab3.Controllers;

[Route("[controller]")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class JuegoController : ControllerBase
{   
    private readonly DataContext _context;
    private readonly IConfiguration config;
    private readonly IWebHostEnvironment environment;
    private readonly string ClientId;
    private readonly string ClientSecret;
    public JuegoController(DataContext context, IConfiguration config, IWebHostEnvironment environment)
    {
        this._context = context;
        this.config = config;
        this.environment = environment;
        ClientId = config["IGDBClientId"];
        ClientSecret = config["IGDBClientSecret"];
    }

    [HttpGet("obtener")]
    public IActionResult obtenerJuegos(){
        
        return Ok(_context.Juegos.ToList());
    }
    [HttpPost("guardar")]
    public IActionResult agregarJuego([FromBody] Juego juego)
    {
        if(juego != null)
        {   
            _context.Add(juego);
            _context.SaveChanges();
            return Ok(juego);
        }else{
            return BadRequest("JUEGO ES INVALIDO");
        }
    }

    [HttpPost("buscar")]
    public async Task<IActionResult> buscarJuego([FromBody] JuegoNombre juego){
        
        using(var httpClient = new HttpClient())
        {
            String url = "https://api.igdb.com/v4/games";
            var _accessToken = await GetAccessToken();
            httpClient.DefaultRequestHeaders.Add("Client-ID", ClientId);
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_accessToken.ToString()}");
            var requestBody = $"fields id, name,summary, cover.image_id, first_release_date,involved_companies.company.name  ; search \"{juego.nombre}\"; limit 10;";
            var response = await httpClient.PostAsync(url, new StringContent(requestBody));
            var games = await response.Content.ReadFromJsonAsync<List<GameApiIGDB>>();
            List<Juego> listaJuegosEncontrados = new List<Juego>();
            foreach(var game in games)
            {   
                
                long milisegundos = game.first_release_date; 
                DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    .AddMilliseconds(milisegundos);

                string urlImagen;
                if(game.cover == null){
                    Cover covr = new Cover();
                    covr.id = 0;
                    covr.image_id = "https://res.cloudinary.com/dhg4fafod/image/upload/v1686803043/dguxtrvp4fscpxbxobhm.png";
                    urlImagen = "https://res.cloudinary.com/dhg4fafod/image/upload/v1686803043/dguxtrvp4fscpxbxobhm.png";
                    game.cover = covr;
                }else{
                    urlImagen = $"https://images.igdb.com/igdb/image/upload/t_cover_big/{game.cover.image_id}.jpg";
                }
                if(game.summary == null){
                    game.summary = "summary dont exist";
                }
                if(game.involved_companies == null){
                    
                    InvolvedCompany ic = new InvolvedCompany();
                    Company c = new Company();
                    c.Name = "Company dont exist";
                    ic.Company = c;
                    InvolvedCompany[] compañias = new InvolvedCompany[1];
                    compañias[0] = ic;
                    game.involved_companies = compañias;
                }
                string fechaFormateada = dateTime.ToString("dd-MM-yyyy");
                
                Juego juegoAux = new Juego
                    {
                        Id = game.id,
                        Nombre = game.name,
                        Descripcion = game.summary,
                        Autor = game.involved_companies[0].Company.Name,
                        fechaLanzamiento = DateTime.Parse(fechaFormateada),
                        Portada = urlImagen
                    };
                listaJuegosEncontrados.Add(juegoAux);
                
            }
            return Ok(listaJuegosEncontrados);
        }

    }

    [HttpPost]
    public IActionResult obtenerJuego([FromBody] Juego juego)
    {   
        
        int count = _context.Juegos.Count(j => j.Nombre == juego.Nombre);
        if(count == 0){
            _context.Add(juego);
            _context.SaveChanges();
            return Ok(juego);
        }else{
            var buscarJuego = _context.Juegos.FirstOrDefault(j => j.Id == juego.Id);
            return Ok(buscarJuego);
        }
    }
    


    public async Task<string> GetAccessToken()
    {

        string url = $"https://id.twitch.tv/oauth2/token?client_id={ClientId}&client_secret={ClientSecret}&grant_type=client_credentials";
        try{
             using (var httpClient = new HttpClient())
        {

            var response = await httpClient.PostAsync(url, null);

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var accessToken = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(jsonResponse).access_token;
                return accessToken;
            }else{
                return "";
            }
            
        }
        }catch(Exception ex)
        {
            return ex.ToString();
        }
       
    }



    
}
