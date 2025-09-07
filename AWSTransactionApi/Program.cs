using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using AWSTransactionApi.Interfaces.Card;
using AWSTransactionApi.Services.Card;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton<IAmazonDynamoDB>(sp =>
{
    return new AmazonDynamoDBClient(Amazon.RegionEndpoint.USEast2);
});

builder.Services.AddSingleton<IDynamoDBContext>(sp =>
{
    var client = sp.GetRequiredService<IAmazonDynamoDB>();
    return new DynamoDBContext(client);
});

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<ICardService, CardService>();


var app = builder.Build();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else {
    app.UseSwagger();
    app.UseSwaggerUI();
}

    app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
