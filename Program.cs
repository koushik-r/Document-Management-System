using DocumentManagement.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Remove the global Kestrel body limit.
// Each endpoint declares its own [RequestSizeLimit] instead.
builder.WebHost.ConfigureKestrel(options =>
    options.Limits.MaxRequestBodySize = null);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();

builder.Services.AddCors(options =>
    options.AddPolicy("AllowAngular", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseCors("AllowAngular");
app.MapControllers();
app.Run();