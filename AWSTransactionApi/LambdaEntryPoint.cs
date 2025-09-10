using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.AspNetCoreServer;
using Amazon.S3;
using Amazon.SQS;
using Microsoft.AspNetCore.Hosting;

namespace AWSTransactionApi
{
    public class LambdaEntryPoint : APIGatewayProxyFunction
    {
        protected override void Init(IWebHostBuilder builder)
        {
            
            builder.UseStartup<ProgramStartup>();
        }
    }

    public class ProgramStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
            services.AddSingleton<IAmazonDynamoDB>(sp =>
            {
                return new AmazonDynamoDBClient(Amazon.RegionEndpoint.USEast2);
            });

            services.AddSingleton<IDynamoDBContext>(sp =>
            {
                var client = sp.GetRequiredService<IAmazonDynamoDB>();
                return new DynamoDBContext(client);
            });

            services.AddSingleton<IAmazonS3>(sp =>
            {
                return new AmazonS3Client(Amazon.RegionEndpoint.USEast2);
            });

            services.AddSingleton<IAmazonSQS>(sp =>
            {
                return new AmazonSQSClient(Amazon.RegionEndpoint.USEast2);
            });

            services.AddScoped<Interfaces.Card.ICardService, Services.Card.CardService>();
            services.AddScoped<Interfaces.Notification.INotificationService, Services.Notification.NotificationService>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Transaction API V1");
                });
            }
            else
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Transaction API V1");
                });
            }

            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
