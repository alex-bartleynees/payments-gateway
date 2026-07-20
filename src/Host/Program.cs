using Host.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.RegisterServices();

var app = builder.Build();

app.RegisterAppConfig();
app.Run();

// Exposed so integration tests can bootstrap the real host via WebApplicationFactory<Program>.
public partial class Program { }
