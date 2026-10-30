using App.Api.Diagnostics;

WebApplicationBuilder construtor = WebApplication.CreateBuilder(args);
construtor.Services.AdicionarProblemDetailsSeguro();
WebApplication aplicacao = construtor.Build();

aplicacao.UsarProblemDetailsSeguro();
aplicacao.Run();

public partial class Program;

