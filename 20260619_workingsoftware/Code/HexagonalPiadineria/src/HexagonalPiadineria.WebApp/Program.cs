using Microsoft.EntityFrameworkCore;
using HexagonalPiadineria.Domain;
using HexagonalPiadineria.Domain.Ports;
using HexagonalPiadineria.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<PiadineriaDbContext>(o => o.UseInMemoryDatabase("piadineria"));
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddSingleton<IKitchenNotifier, ConsoleKitchenNotifier>();
builder.Services.AddScoped<OrderService>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference();

app.MapControllers();
app.Run();
