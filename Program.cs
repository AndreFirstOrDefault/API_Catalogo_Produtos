using APICatalogo.Context;
using APICatalogo.DTOs.Mappings;
using APICatalogo.Extensions;
using APICatalogo.Filters;
using APICatalogo.Repositories;
using APICatalogo.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using APICatalogo.Models;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Tratando a referencia ciclica -------------------------------------------------------- REFERENCIA CICLICA -----------------------
builder.Services.AddControllers().AddJsonOptions(options =>
options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles)
// Adicionando o pacote Json Patch
.AddNewtonsoftJson();

// Incluindo os perfis do Identity ------------------------------------------------------ PERFIS DO IDENTITY -----------------------
builder.Services.AddIdentity<ApplicationUser, IdentityRole>().
    AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();



// habilita e configura a autentica��o jwt bearer na aplica��o --------------------------- JWT BEARER -------------------------------
var secretKey = builder.Configuration["JWT:SecretKey"]
    ?? throw new ArgumentException("invalid secret key!!");
// --------------------------------------------------------------------------------------- ADICIONA AUTENTICA��O -----------------
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = false;
    options.TokenValidationParameters = new TokenValidationParameters()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.Zero,
        ValidAudience = builder.Configuration["JWT:ValidAudience"],
        ValidIssuer = builder.Configuration["JWT:ValidIssuer"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

builder.Services.AddScoped<ITokenService, TokenService>();


builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "apicatalogo", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Bearer JWT",
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme, Id = "Bearer"
                }

            },
            new string[]{}

        }
    });
});

// Habilitando o CORS - Cross Origin Resource Sharing ---------------------------------------------- CORS POLITICA NOMEADA ---------
// Politica nomeada
var OrigensComAcessoPermitido = "_origensComAcessoPermitido";

builder.Services.AddCors(options =>
{
    options.AddPolicy(OrigensComAcessoPermitido,
        policy =>
        {
            policy.WithOrigins("https://apirequest.io")
            .WithMethods("GET", "POST")
                .AllowAnyHeader()
                .AllowCredentials(); 
        });
});

// Habilitando o CORS - Cross Origin Resource Sharing ---------------------------------------------- CORS POLITICA PADR�O -----------
// Politica padr�o

//builder.Services.AddCors(options =>
//{
//    options.AddDefaultPolicy(
//        policy =>
//        {
//            policy.WithOrigins("https://apirequest.io")
//                .WithMethods("GET", "POST")
//                .AllowAnyHeader()
//                .AllowCredentials();
//        });
//});

// Incluindo o servi�o de autentica��o e autoriza��o
//builder.Services.AddAuthentication("Bearer").AddJwtBearer();
//  --------------------------------------------------------------------------------------- ADICIONA AUTORIZA��O -----------------
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("SuperAdminOnly", policy => policy.RequireRole("Admin").RequireClaim("id", "Andre"));
    options.AddPolicy("UserOnly", policy => policy.RequireRole("User"));
    options.AddPolicy("ExclusiveOnly", policy => policy.RequireAssertion(context => 
        context.User.HasClaim(claim => claim.Type == "id" && claim.Value == "Andre") || context.User.IsInRole("SuperAdmin")));

});

string mySqlConnection = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
                                options.UseMySql(mySqlConnection, 
                                ServerVersion.AutoDetect(mySqlConnection)));

// Registrando o servi�o de log
builder.Services.AddScoped<ApiLoggingFilter>();

// Registrando o repository
builder.Services.AddScoped<ICategoriaRepository, CategoriaRepository>();
builder.Services.AddScoped<IProdutoRepository, ProdutoRepository>();

// Registrando o servi�o do reposit�rio gen�rico
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

// Registrando o servi�o do UnitOfWork
builder.Services.AddScoped<IUnityOfWork,UnitOfWork>();

// Registrando o servi�o do Auto mapper
builder.Services.AddAutoMapper(typeof(ProdutoDTOMappingProfile));

// Tamb�m daria certo para o automapper -----------------------------------------------------------------------------
//builder.Services.AddAutoMapper(typeof(Program));

// Teste
var valor1 = builder.Configuration["chave1"];
var valor2 = builder.Configuration["secao1:chave2"];

// Transiente cria uma nova inst�ncia sempre que for chamada
builder.Services.AddTransient<IMeuServico,MeuServico>();

// Desabilitando o mecanismo de inferencia para a inje��o de dependencia nos controladores
builder.Services.Configure <ApiBehaviorOptions>(options =>
{
    options.DisableImplicitFromServicesParameters = true;
});

// Configurando o provedor de log customizado
//builder.Logging.AddProvider(new CustomLoggerProvider(new CustomLoggerProviderConfiguration
//{
//    LogLevel = LogLevel.Information
//}));

// Adicionar o filtro criado como um filtro global
builder.Services.AddControllers(options =>
{
    options.Filters.Add(typeof(ApiExceptionFilter));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.ConfigureExceptionHandler();
}

app.UseHttpsRedirection();



//// Habilitando o CORS - Cross Origin Resource Sharing com politica nomeada
//app.UseCors(OrigensComAcessoPermitido);

// Habilitando o CORS - Cross Origin Resource Sharing com politica padr�o
app.UseCors();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
