using PoETest.API.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add DB context
builder.Services.AddDbContext<PoETestContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("PoETestDB")));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
var app = builder.Build();


app.UseDefaultFiles();
app.UseStaticFiles();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();