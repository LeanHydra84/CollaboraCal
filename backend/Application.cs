using System;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using CollaboraCal.JsonRequests;

namespace CollaboraCal
{
    public static class Application
    {
        public static ActiveSessionHandler Sessions { get; }
        public static DatabaseController Database { get; }
        public static AccountController Accounts { get; }
        public static CalendarManager CalendarManager { get; }

        static Application()
        {
            Sessions = new ActiveSessionHandler();
            Database = new DatabaseController();
            Accounts = new AccountController();
            CalendarManager = new CalendarManager();
        }

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();

                Console.WriteLine("IsDevelopment");
            }

            app.UseHttpsRedirection();

            // Default
            app.MapGet("/", DefaultResponse).WithOpenApi();

            // User
            app.MapPost("/login", Login).WithName("Login").WithOpenApi();
            app.MapPost("/newuser", CreateUser).WithName("New User").WithOpenApi();
            app.MapPost("/changename", ChangeName).WithName("Update Name").WithOpenApi();

            // Calendar
            app.MapPost("/newcalendar", CreateCalendar).WithName("New Calendar").WithOpenApi();
            app.MapPost("/newevent", CreateEvent).WithName("Create Event").WithOpenApi();

            app.MapGet("/getallcalendar", GetAllCalendars).WithName("Get All Calendars From User").WithOpenApi();
            app.MapGet("/getevents", GetEventsDuringTimeFrame).WithName("Get All Events In Calendar Within Timeframe").WithOpenApi();

            app.Run();
        }

        private static IResult DefaultResponse()
        {
            return TypedResults.Ok("Default Response");
        }

        private static IResult Login(
            [FromHeader(Name = "EMail")] string? email,
            [FromHeader(Name = "Password")] string? password)
        {
            if (email == null || password == null) return TypedResults.BadRequest("Missing 'EMail' and/or 'Password' headers");
            var result = Sessions.Login(email, password);
            if (result == null) return TypedResults.Unauthorized();
            return TypedResults.Ok(new LoginResponse(result, email));
        }

        private static IResult CreateUser(
            [FromHeader(Name = "Email")] string? email,
            [FromHeader(Name = "Password")] string? password,
            [FromHeader(Name = "ConfirmPassword")] string? confpassword,
            [FromHeader(Name = "Name")] string? name)
        {
            if (email == null || name == null || password == null || confpassword == null) return TypedResults.BadRequest();
            return TypedResults.Ok(Accounts.CreateUser(email, name, password, confpassword));
        }

        private static IResult ChangeName(
            [FromHeader(Name = "Email")] string? email,
            [FromHeader(Name = "Authentication")] string? authentication,
            [FromBody] string? newName
        )
        {
            if (email == null || newName == null || authentication == null) return TypedResults.BadRequest();
            bool success = Accounts.ChangeName(email, authentication, newName);
            if (success) return TypedResults.Ok();
            else return TypedResults.Unauthorized();
        }

        private static IResult CreateCalendar(
            [FromHeader(Name = "Email")] string? email,
            [FromHeader(Name = "Authentication")] string? authentication,
            [FromBody] string? jsonBody
        )
        {
            if (email == null || authentication == null)
                return TypedResults.BadRequest("Missing user or authentication");

            if (!Sessions.ValidateAuthentication(email, authentication))
            {
                return TypedResults.Unauthorized();
            }

            if (jsonBody == null) return TypedResults.BadRequest("Missing body");
            var data = JsonConvert.DeserializeObject<NewCalendarData>(jsonBody);
            if (data == null) return TypedResults.BadRequest("Malformed body");

            bool success = CalendarManager.CreateCalendar(email, data);
            if (success)
                return TypedResults.Ok("Calendar Created");
            else
                return TypedResults.Forbid();
        }

        private static IResult CreateEvent(
            [FromHeader(Name = "Email")] string? email,
            [FromHeader(Name = "Authentication")] string? authentication,
            [FromBody] string? jsonBody
        )
        {
            if (email == null || authentication == null) return TypedResults.BadRequest();
            if (jsonBody == null) return TypedResults.BadRequest();

            if (!Sessions.ValidateAuthentication(email, authentication))
            {
                return TypedResults.Unauthorized();
            }

            var body = JsonConvert.DeserializeObject<NewEventData>(jsonBody);
            if (body == null) return TypedResults.BadRequest();

            bool success = CalendarManager.CreateEvent(email, body);
            if (success)
                return TypedResults.Ok();
            else
                return TypedResults.Forbid();
        }

        private static IResult GetAllCalendars(
            [FromHeader(Name = "Email")] string? email,
            [FromHeader(Name = "Authentication")] string? authentication
        )
        {
            if (email == null || authentication == null) return TypedResults.BadRequest();
            if (!Sessions.ValidateAuthentication(email, authentication))
            {
                return TypedResults.Unauthorized();
            }

            List<Calendar>? calendars = Database.GetLightAllCalendars(email);
            if (calendars == null) return TypedResults.NotFound();

            var organized = calendars.Select(a => new CalendarListResponse(a.ID, a.Name, a.Description)).ToList();
            string asJson = JsonConvert.SerializeObject(organized);

            return TypedResults.Ok(organized);
        }

        private static IResult GetEventsDuringTimeFrame(
            [FromHeader(Name = "Email")] string? email,
            [FromHeader(Name = "Authentication")] string? authentication,
            [FromBody] string? jsonBody
        )
        {
            if (email == null || authentication == null) return TypedResults.BadRequest();
            if (!Sessions.ValidateAuthentication(email, authentication)) return TypedResults.Unauthorized();

            if (jsonBody == null) return TypedResults.BadRequest("Missing body");
            CalendarEventRequest? request = JsonConvert.DeserializeObject<CalendarEventRequest>(jsonBody);
            if (request == null) return TypedResults.BadRequest("Malformed body");

            return TypedResults.Ok();
        }


        private record LoginResponse(string authentication, string email);

        private record CalendarListResponse(int id, string? name, string? description);
    }

}
