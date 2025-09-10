using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.S3;
using Amazon.SQS;
using AWSTransactionApi.Interfaces.Card;
using AWSTransactionApi.Interfaces.Notification;
using AWSTransactionApi.Services.Card;
using AWSTransactionApi.Services.Notification;

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

builder.Services.AddSingleton<IAmazonSQS>(sp =>
{
    return new AmazonSQSClient(Amazon.RegionEndpoint.USEast2);
});


builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddSingleton<IAmazonDynamoDB>(sp => new AmazonDynamoDBClient(Amazon.RegionEndpoint.USWest2));
builder.Services.AddSingleton<IDynamoDBContext>(sp => new DynamoDBContext(sp.GetRequiredService<IAmazonDynamoDB>()));
builder.Services.AddSingleton<IAmazonS3>(sp => new AmazonS3Client(Amazon.RegionEndpoint.USWest2));

builder.Services.AddScoped<ICardService, CardService>();
builder.Services.AddSingleton<INotificationService, NotificationService>();



var app = builder.Build();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Transaction API V1");
    });
}
else {
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Transaction API V1");
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
