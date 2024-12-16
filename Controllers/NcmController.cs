using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.Authorization;
using System.Linq;

[ApiController]
[Route("api/[controller]")]
public class NcmController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;

    public NcmController(IHttpClientFactory httpClientFactory, IMemoryCache cache)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
    }

    [HttpPost("consulta")]
    [Authorize]
    public async Task<IActionResult> ConsultarNcm([FromBody] ProdutoRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Descricao))
        {
            return BadRequest(new { error = "A descrição do produto é obrigatória." });
        }

        // Gerar uma chave única para o cache com base na descrição
        var cacheKey = $"NCM_{request.Descricao.ToLowerInvariant()}";

        // Verificar se o valor já está no cache
        if (_cache.TryGetValue(cacheKey, out string ncmResposta))
        {
            var ncmCodigoCache = ExtrairCodigoNcm(ncmResposta);
            return Ok(new
            {
                descricao = request.Descricao,
                ncm = ncmResposta,
                ncm_codigo = ncmCodigoCache,
                cache = "Sim, o resultado foi recuperado do cache."
            });
        }

        try
        {
            // Tentar encontrar o NCM diretamente
            ncmResposta = await ConsultarNcmNaIA(request.Descricao);

            if (string.IsNullOrEmpty(ncmResposta))
            {
                if (ncmResposta?.Contains("não é possível determinar o código NCM", StringComparison.OrdinalIgnoreCase) == true ||
                    ncmResposta?.Contains("Para determinar o código NCM", StringComparison.OrdinalIgnoreCase) == true)
                {
                    // Expandir descrição e tentar novamente
                    var descricaoExpandida = await ExpandirDescricao(request.Descricao);
                    ncmResposta = await ConsultarNcmNaIA(descricaoExpandida);
                    var ncmCodigo = ExtrairCodigoNcm(ncmResposta);
                    var ncmValido = ValidarNcm(ncmCodigo, descricaoExpandida);

                    // Armazenar no cache
                    // _cache.Set(cacheKey, ncmResposta, TimeSpan.FromMinutes(10));
                    _cache.Set(cacheKey, ncmResposta, TimeSpan.FromHours(2));

                    return Ok(new
                    {
                        descricaoOriginal = request.Descricao,
                        descricaoExpandida = descricaoExpandida,
                        ncm = ncmResposta,
                        ncm_codigo = ncmValido,
                        aproximado = "Sim, este código foi baseado em uma descrição expandida."
                    });
                }
            }

            var ncmCodigoFinal = ExtrairCodigoNcm(ncmResposta);
            var ncmCodigo1 = ExtrairCodigoNcm(ncmResposta);
            string entrada = ncmResposta;
            string chave = "A subcategoria do produto é ";

            string resultado = null;

            // Verifica se a frase-chave existe na string de entrada
            if (entrada.Contains(chave))
            {
                // Encontra o índice inicial do conteúdo desejado
                int inicio = entrada.IndexOf(chave) + chave.Length;
                // Extrai tudo a partir do índice
                resultado = entrada.Substring(inicio).Trim();
            }

            var ncmValidofinal = ValidarNcm(ncmCodigoFinal, resultado);

            // Armazenar no cache
            _cache.Set(cacheKey, ncmResposta, TimeSpan.FromHours(4));
            string aproximado = ncmValidofinal != ncmCodigo1
                                ? "Sim, a classificação foi aproximada pois o código original não é mais válido."
                                : "Não, a classificação é precisa.";
            return Ok(new
            {
                descricao = request.Descricao,
                ncm = ncmResposta,
                ncm_codigo = ncmValidofinal,              
                aproximado = aproximado

            });




           
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(500, new { error = "Erro ao consultar o NCM.", details = ex.Message });
        }
    }


// Método para consultar o NCM usando a OpenAI
private async Task<string> ConsultarNcmNaIA(string descricaoProduto)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer sk-proj-JoA"); //chave IA 

        var requestContent = new
        {
            model = "gpt-3.5-turbo", // gpt-3.5-turbo ou "gpt-4"
            temperature = 0, // Menos aleatoriedade, respostas mais consistentes para não ter diferença de ambientes
            messages = new[]
            {
                new { role = "system", content = "Você é um assistente especializado em buscar códigos NCM e categoria de produtos." },
                new { role = "user", content = $"Descrição do produto: {descricaoProduto}  Qual é o código NCM mais adequado e informe a  Subcategoria do produto como ultima informação na resposta, e sempre respondendo desta forma A subcategoria do produto é" }
            }
        };

        var content = new StringContent(JsonSerializer.Serialize(requestContent), Encoding.UTF8, "application/json");

        var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Erro na resposta da OpenAI: {response.StatusCode}, Detalhes: {errorContent}");
            return null; // Retorna nulo para ativar o fallback
        }

        var responseString = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(responseString);
        var ncmResposta = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return ncmResposta?.Trim();
    }

    // Método para expandir descrições vagas ou superficiais
    private async Task<string> ExpandirDescricao(string descricaoProduto)
    {
        var client = _httpClientFactory.CreateClient();
         client.DefaultRequestHeaders.Add("Authorization", "Bearer sk-proj-JoA"); //chave IA 

        var requestContent = new
        {
            model = "gpt-3.5-turbo", //gpt-3.5-turbo ou "gpt-4"
            messages = new[]
            {
                new { role = "system", content = "Você é um assistente especializado em corrigir e expandir descrições de produtos para que possam ser usados em classificações NCM." },
                new { role = "user", content = $"Descrição do produto: {descricaoProduto} Corrija a descrição para que fique clara." }
            }
        };

        var content = new StringContent(JsonSerializer.Serialize(requestContent), Encoding.UTF8, "application/json");

        var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Erro ao expandir descrição: {response.StatusCode}, Detalhes: {errorContent}");
            return descricaoProduto; // Retorna a descrição original se falhar
        }

        var responseString = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(responseString);
        var descricaoExpandida = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return descricaoExpandida?.Trim();
    }

    // Método auxiliar para extrair apenas o código do NCM
    private string ExtrairCodigoNcm(string ncmResposta)
    {
        if (string.IsNullOrWhiteSpace(ncmResposta))
        {
            return "0000.00.00"; // Retorna um valor genérico se falhar
        }

        // Regex para localizar o código NCM no formato "XXXX.XX.XX" ou "XXXXXXXX"
        var match = System.Text.RegularExpressions.Regex.Match(ncmResposta, @"\b\d{8}\b|\b\d{4}\.\d{2}\.\d{2}\b");

        if (match.Success)
        {
            // Obtem o código encontrado
            string ncmEncontrado = match.Value;

            // Se estiver no formato sem pontos (XXXXXXXX), formata para "0000.00.00"
            if (!ncmEncontrado.Contains("."))
            {
                ncmEncontrado = $"{ncmEncontrado.Substring(0, 4)}.{ncmEncontrado.Substring(4, 2)}.{ncmEncontrado.Substring(6, 2)}";
            }

            return ncmEncontrado;
        }

        return "0000.00.00"; // Retorna genérico se nenhum código for encontrado
    }

    // Função para normalizar o código NCM
    string NormalizarNcm(string ncm)
    {
        // Remove quaisquer pontos ou caracteres não numéricos
        string ncmLimpo = new string(ncm.Where(char.IsDigit).ToArray());

        // Verifica se o código tem exatamente 8 dígitos
        if (ncmLimpo.Length == 8)
        {
            // Formata o código no padrão 0000.00.00
            return $"{ncmLimpo.Substring(0, 4)}.{ncmLimpo.Substring(4, 2)}.{ncmLimpo.Substring(6, 2)}";
        }

        // Lança exceção ou retorna vazio caso o código seja inválido
        throw new FormatException("O código NCM deve ter exatamente 8 dígitos.");
    }

    private string ValidarNcm(string ncmSugerido, string descricaoProduto)
    {
        // Padronizar o código NCM
        if (string.IsNullOrWhiteSpace(ncmSugerido))
        {
          
           

        }

        ncmSugerido = ncmSugerido.Trim();
        ncmSugerido = NormalizarNcm(ncmSugerido);
        // Verificar a tabela
        if (NcmTableLoader.NcmTable.ContainsKey(ncmSugerido))
        {
            return ncmSugerido; // Retorna se for válido
        }

        /// vamos tentar validar pela descricao o codigo na tabela 
        /// orlando 13/12/2024.
        // return "Código NCM inválido ou obsoleto.";
        var ncmSugeridoPorDescricao = NcmTableLoader.SugerirNcmPorDescricao(descricaoProduto,85);
        if (!string.IsNullOrEmpty(ncmSugeridoPorDescricao))
        {
            return ncmSugeridoPorDescricao; // Retorna o código sugerido pela descrição
        }

        // Tentar validar pela descrição na tabela

        // Retorna inválido se não encontrado
        return "Código NCM inválido ou obsoleto.";
    }



}