using App.Api.Diagnostics;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddSafeProblemDetails();
WebApplication app = builder.Build();

app.UseSafeProblemDetails();
app.Run();

public partial class Program;
