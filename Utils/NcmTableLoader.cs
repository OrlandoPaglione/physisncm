﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using FuzzySharp;

public static class NcmTableLoader
{
    public static ConcurrentDictionary<string, string> NcmTable { get; private set; }

    public static void LoadNcmTable(string filePath)
    {
        try
        {
            // Verificar se o arquivo existe
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Arquivo não encontrado no caminho especificado: {filePath}");
                return;
            }

            // Ler o conteúdo do arquivo JSON
            var jsonContent = File.ReadAllText(filePath);

            // Configurar opções de desserialização
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true, // Ignorar diferenças de maiúsculas/minúsculas nos nomes das propriedades
                ReadCommentHandling = JsonCommentHandling.Skip, // Ignorar comentários no JSON
                AllowTrailingCommas = true // Permitir vírgulas finais
            };

            // Desserializar o JSON com nó raiz
            var root = JsonSerializer.Deserialize<RootObject>(jsonContent, options);
            if (root?.Nomenclaturas == null || !root.Nomenclaturas.Any())
            {
                Console.WriteLine("Nenhum registro encontrado no campo 'Nomenclaturas'.");
                return;
            }

            // Filtrar apenas os registros vigentes e carregar no dicionário
            NcmTable = new ConcurrentDictionary<string, string>(
                root.Nomenclaturas
                    .Where(entry => entry.Data_Fim == "31/12/9999") // Filtro por vigência
                    .ToDictionary(entry => entry.Codigo, entry => entry.Descricao)
            );

            if (NcmTable == null || !NcmTable.Any())
            {
                Console.WriteLine("Nenhum registro válido encontrado na tabela NCM.");
                return;
            }

            Console.WriteLine($"Tabela NCM carregada com sucesso! Total de registros válidos: {NcmTable.Count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao carregar a tabela NCM: {ex.Message}");
            throw;
        }
    }

    public static string SugerirNcmPorDescricao(string descricaoProduto, string codigolocal, int limiteSimilaridade = 80)
    {
        if (NcmTable == null || !NcmTable.Any())
        {
            Console.WriteLine("Tabela NCM não carregada ou está vazia.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(descricaoProduto))
        {
            Console.WriteLine("Descrição do produto inválida ou vazia.");
            return null;
        }

        // Filtrar apenas códigos que tenham exatamente 10 caracteres
        var ncmFiltrado = NcmTable.Where(entry => entry.Key.Length == 10).ToList();

        if (!ncmFiltrado.Any())
        {
            Console.WriteLine("Nenhum código NCM com 10 caracteres encontrado.");
            return null;
        }

        // Normalizar a descrição do produto para comparação
        var descricaoProdutoNormalizada = RemoverAcentos(descricaoProduto.ToLowerInvariant().Trim());

        // Verificar correspondência por subconjunto
        var resultadoSubconjunto = ncmFiltrado
            .FirstOrDefault(entry =>
                RemoverAcentos(entry.Value.ToLowerInvariant())
                .Contains(descricaoProdutoNormalizada));

        if (resultadoSubconjunto.Value != null)
        {
            Console.WriteLine("Encontrado por subconjunto:");
            Console.WriteLine($"Codigo: {resultadoSubconjunto.Key}, Descricao: {resultadoSubconjunto.Value}");
            return resultadoSubconjunto.Key;
        }

        // Avaliar a similaridade global com cada descrição na tabela NCM filtrada
        var resultadoSimilares = ncmFiltrado
            .Select(entry => new
            {
                Codigo = entry.Key,
                Descricao = entry.Value,
                Similaridade = Fuzz.TokenSortRatio(descricaoProdutoNormalizada, RemoverAcentos(entry.Value.ToLowerInvariant())),
                TamanhoDescricao = entry.Value.Length,
                PalavrasCoincidentes = descricaoProdutoNormalizada.Split(' ').Count(palavra => RemoverAcentos(entry.Value.ToLowerInvariant()).Contains(palavra))
            })
            .OrderByDescending(x => x.Similaridade)
            .ThenByDescending(x => x.PalavrasCoincidentes) // Priorizar mais palavras coincidentes
            .ThenByDescending(x => x.TamanhoDescricao)    // Priorizar descrições mais completas
            .ToList();

        // Log para depuração
        foreach (var resultado in resultadoSimilares)
        {
            Console.WriteLine($"Codigo: {resultado.Codigo}, Descricao: {resultado.Descricao}, Similaridade: {resultado.Similaridade}, PalavrasCoincidentes: {resultado.PalavrasCoincidentes}, Tamanho: {resultado.TamanhoDescricao}");
        }

        // Filtrar resultados acima do limite de similaridade
        resultadoSimilares = resultadoSimilares
            .Where(x => x.Similaridade >= limiteSimilaridade)
            .ToList();

        /*if (!resultadoSimilares.Any())
        {
            Console.WriteLine("Nenhum resultado encontrado com similaridade acima do limite.");
            return null;
        }*/
        if (!resultadoSimilares.Any())
        {
            Console.WriteLine("Nenhum resultado encontrado com similaridade acima do limite.");

            // Procurar pelo código mais próximo formatado
            var closestCode = ncmFiltrado
                .Select(entry => new
                {
                    Codigo = entry.Key,
                    Similaridade = Fuzz.Ratio(entry.Key, codigolocal) // Baseado no código de entrada
                })
                .OrderByDescending(x => x.Similaridade)
                .FirstOrDefault();

            if (closestCode != null)
            {
                Console.WriteLine($"Código formatado mais próximo encontrado: {closestCode.Codigo}");
                return closestCode.Codigo;
            }

            return null;
        }


        // Retorna o código mais similar
        return resultadoSimilares.FirstOrDefault()?.Codigo;
    }


    // Função auxiliar para remover acentos
    private static string RemoverAcentos(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return texto;

        var normalizedString = texto.Normalize(System.Text.NormalizationForm.FormD);
        var stringBuilder = new System.Text.StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }





}

public class NcmEntry
{
    [JsonPropertyName("Codigo")]
    public string Codigo { get; set; }

    [JsonPropertyName("Descricao")]
    public string Descricao { get; set; }

    [JsonPropertyName("Data_Inicio")]
    public string Data_Inicio { get; set; }

    [JsonPropertyName("Data_Fim")]
    public string Data_Fim { get; set; }

    [JsonPropertyName("Tipo_Ato_Ini")]
    public string Tipo_Ato_Ini { get; set; }

    [JsonPropertyName("Numero_Ato_Ini")]
    public string Numero_Ato_Ini { get; set; }

    [JsonPropertyName("Ano_Ato_Ini")]
    public string Ano_Ato_Ini { get; set; }
}

public class RootObject
{
    [JsonPropertyName("Data_Ultima_Atualizacao_NCM")]
    public string DataUltimaAtualizacaoNCM { get; set; }

    [JsonPropertyName("Ato")]
    public string Ato { get; set; }

    [JsonPropertyName("Nomenclaturas")]
    public List<NcmEntry> Nomenclaturas { get; set; }
}
