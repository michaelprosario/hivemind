using System.Reflection;
using FileRepo.Core.Files;
using FileRepo.Infrastructure;
using FileRepo.Web;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);
var maxSize = builder.Configuration.GetValue<long?>("FileRepository:MaxFileSizeBytes") ?? 10_485_760;
builder.Services.AddSingleton(new FileValidationRules { MaxFileSizeBytes = maxSize });
builder.Services.AddFileRepositoryInfrastructure(builder.Configuration);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = maxSize);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = maxSize);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new() { Title = "File Repository API", Version = "v1" });
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    o.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));
});
builder.Services.AddScoped<IFileService, FileService>();
var app = builder.Build();
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue("Swagger:Enabled", false)) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseExceptionHandler();
app.MapControllers();
app.Run();
public partial class Program { }
