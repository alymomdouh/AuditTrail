using AuditTrail.webApi.Data;
using Microsoft.EntityFrameworkCore;

namespace AuditTrail.webApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddDbContext<AuditTrailwebApiContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("AuditTrailwebApiContext") ?? throw new InvalidOperationException("Connection string 'AuditTrailwebApiContext' not found.")));

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            // code removed for brevity
            app.UseAuthorization();
            app.Use(next => context =>
            {
                context.Request.EnableBuffering();
                return next(context);
            });



            // app.UseMiddleware<AuditLogMiddleware>();
            // third way to save audit log
            app.UseWhen(context => context.Request.Path.StartsWithSegments("/api"), appBuilder =>
            {
                appBuilder.UseMiddleware<AuditLogMiddleware>();
            });

            app.MapControllers();

            app.Run();
        }
    }
}
