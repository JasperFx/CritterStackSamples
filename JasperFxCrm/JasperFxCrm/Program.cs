using JasperFx;
using JasperFxCrm;

// The whole application is composed by AddJasperFxCrm / MapJasperFxCrm so that another
// host (JasperFx's private one, with the real vendor clients) can compose the same
// features and replace only the fakes. This Program is the public sample's host.
var builder = WebApplication.CreateBuilder(args);
builder.AddJasperFxCrm();

var app = builder.Build();
app.MapJasperFxCrm();

return await app.RunJasperFxCommands(args);

public partial class Program;
