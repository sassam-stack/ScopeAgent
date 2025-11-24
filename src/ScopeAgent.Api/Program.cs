using ScopeAgent.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Register HttpClient for Azure Blob Service
builder.Services.AddHttpClient<IAzureBlobService, AzureBlobService>();

// Register HttpClient for Computer Vision Service
builder.Services.AddHttpClient<IComputerVisionService, ComputerVisionService>();

// Register HttpClient for YOLO Service
// NOTE: YOLO service is currently disabled - preserved for future use
// builder.Services.AddHttpClient<IYoloService, YoloService>();

// Register services
builder.Services.AddScoped<IComputerVisionService, ComputerVisionService>();
// NOTE: YOLO service is currently disabled - preserved for future use
// builder.Services.AddScoped<IYoloService, YoloService>();

// Configuration
builder.Services.Configure<ComputerVisionConfig>(builder.Configuration.GetSection("ComputerVision"));
// NOTE: YOLO config is kept but service is disabled
builder.Services.Configure<YoloConfig>(builder.Configuration.GetSection("Yolo"));

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowReactApp");
app.UseAuthorization();
app.MapControllers();

app.Run();

