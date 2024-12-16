using System;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMemoryCache(); // Habilitar o cache
// Configuração do JWT
var key = Encoding.ASCII.GetBytes("e869f83189a572d9593c779066157741720c64a231bbfab54af88798af3ddf19ba93ba1a87c2eb06974b37accf621eea732d66d6a4d39ce0e47f747b160500167fc9f37281acba2b8c5fcc11b3c5a76a3d3e36d9c31a10d5a87a78f04f6b9bc411939f81fe111871040277ab7159c4b1a87018b78e5136229f7bea84c2a4d1f40c2576eb"); // Substitua por uma chave segura
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false,
        ClockSkew = TimeSpan.Zero
    };
});

// Carregar a tabela NCM durante a inicialização
var ncmFilePath = Path.Combine(AppContext.BaseDirectory, "dados", "Tabela_NCM_Vigente.json");
NcmTableLoader.LoadNcmTable(ncmFilePath);


// Registrar serviços
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddSingleton(new JwtTokenService("e869f83189a572d9593c7790661577417ff550af88798af3ddf19ba93ba1a87c2eb06974b37accf621eea732d66d6a4d39ce0e47f747b160500167fc9f37281acba2b8c5fcc11b3c5a76a3d3e36d9c31a10d5a87a78f04f6b9bc411939f81fe111871040277ab7159c4b1a87018b78e5136229f7bea84c2a4d1f40c2576eb")); // Linha adicionada
// Configuração do CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirTudo", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Configuração do Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Insira o token JWT no formato: Bearer {seu token}"
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8083); // Porta 8082
   // options.ListenAnyIP(8090);
    ///options.ListenLocalhost(8083); //reverso
});

var app = builder.Build();

// Configuração do pipeline esta aberto tanto em prd quando dev
//if (app.Environment.IsDevelopment())
//{
if (app.Environment.IsDevelopment() || app.Environment.IsStaging() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("./swagger/v1/swagger.json", "PhysisNCM API v1");
        c.RoutePrefix = string.Empty;
    });
}
//app.UseHttpsRedirection();

app.UseCors("PermitirTudo");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
